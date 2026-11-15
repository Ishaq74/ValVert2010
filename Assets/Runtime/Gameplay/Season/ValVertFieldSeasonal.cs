using System.Collections.Generic;
using UnityEngine;

/// <summary>Pure seasonal rules for herbaceous field vegetation (grass cards + flower cards). No Unity state: unit-testable.</summary>
public static class ValVertFieldSeason
{
    public struct Look { public Color grass, flower; public float bloom; }

    static readonly Color Autumn = new Color(1.00f, 0.80f, 0.52f, 1f);   // curing grass: straw/yellow
    static readonly Color Dormant = new Color(0.72f, 0.67f, 0.56f, 1f);  // winter: bleached, darker
    static readonly Color FlowerAutumn = new Color(0.78f, 0.62f, 0.45f, 1f);
    static readonly Color Spring = new Color(0.93f, 1.04f, 0.80f, 1f);   // fresh yellow-green flush

    static float Ramp(float x, float a, float b) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    /// <param name="yearFraction">0..1 over the year (ValVertSeasonChanged.YearFraction)</param>
    public static Look Evaluate(float yearFraction)
    {
        float doy = Mathf.Repeat(yearFraction, 1f) * 365f;
        ValVertSeasons.Phenology(doy, out float leaf, out float autumn, out _, out _);
        // growth flush in April-May, curing from late September, dormant once the deciduous canopy is bare
        float flush = Mathf.Clamp01(1f - Mathf.Abs(doy - 125f) / 30f);
        float dormant = 1f - leaf;
        var g = Color.Lerp(Color.white, Spring, flush * 0.6f);
        g = Color.Lerp(g, Autumn, autumn * 0.75f);
        g = Color.Lerp(g, Dormant, dormant);
        // flowers: bloom mid-April..September, gone by late October (alpha erodes against the cutoff)
        float bloom = Ramp(doy, 100f, 140f) * (1f - Ramp(doy, 255f, 300f));
        var f = Color.Lerp(Color.white, FlowerAutumn, autumn);
        f = Color.Lerp(f, Dormant, dormant);
        f.a = bloom;
        g.a = 1f;
        return new Look { grass = g, flower = f, bloom = bloom };
    }
}

/// <summary>
/// First real ValVertEventBus consumer: tints the generated field vegetation (grass + flowers) from
/// ValVertSeasonChanged. It never mutates project assets: at enable it clones the chunk materials once, assigns the
/// clones to every chunk renderer (same clone per source material => SRP-batcher friendly) and restores the original
/// shared materials on disable. Subscribes in OnEnable (replayLast) and disposes in OnDisable (bus lifetime rules).
/// </summary>
public class ValVertFieldSeasonal : MonoBehaviour
{
    [Tooltip("Scene root GameObject names holding the generated chunks (grass cards, flowers).")]
    public string[] grassRoots = { "V3_Grass" };
    public string[] flowerRoots = { "V3_Flowers" };
    public string colorProperty = "_BaseColor";

    struct Binding { public Renderer r; public Material[] original; }
    readonly List<Binding> _bound = new List<Binding>();
    readonly Dictionary<Material, Material> _clone = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Color> _baseColor = new Dictionary<Material, Color>();
    readonly HashSet<Material> _flowerSources = new HashSet<Material>();
    System.IDisposable _sub;
    public int BoundRendererCount => _bound.Count;
    public int CloneCount => _clone.Count;
    public int Applied { get; private set; }
    public ValVertFieldSeason.Look LastLook { get; private set; }

    void OnEnable()
    {
        Bind();
        _sub = ValVertEventBus.Subscribe<ValVertSeasonChanged>(OnSeason, replayLast: true);
    }

    void OnDisable()
    {
        _sub?.Dispose(); _sub = null;
        Unbind();
    }

    /// <summary>Editor/test entry: (re)bind without relying on OnEnable.</summary>
    public void BindNow() => Bind();
    public void UnbindNow() => Unbind();

    void Bind()
    {
        Unbind();
        Collect(grassRoots, false); Collect(flowerRoots, true);
    }

    void Collect(string[] names, bool flower)
    {
        if (names == null) return;
        foreach (var n in names)
        {
            var root = string.IsNullOrEmpty(n) ? null : GameObject.Find(n);
            if (!root) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var src = r.sharedMaterials; var mats = new Material[src.Length];
                for (int i = 0; i < src.Length; i++)
                {
                    var m = src[i]; if (!m) continue;
                    if (!_clone.TryGetValue(m, out var c))
                    {
                        c = new Material(m) { name = m.name + " (seasonal)" };
                        _clone[m] = c; _baseColor[c] = m.HasProperty(colorProperty) ? m.GetColor(colorProperty) : Color.white;
                        if (flower) _flowerSources.Add(c);
                    }
                    mats[i] = c;
                }
                _bound.Add(new Binding { r = r, original = src });
                r.sharedMaterials = mats;
            }
        }
    }

    void Unbind()
    {
        foreach (var b in _bound) if (b.r) b.r.sharedMaterials = b.original;
        _bound.Clear();
        foreach (var c in _clone.Values) if (c) { if (Application.isPlaying) Destroy(c); else DestroyImmediate(c); }
        _clone.Clear(); _baseColor.Clear(); _flowerSources.Clear();
    }

    void OnSeason(ValVertSeasonChanged e) => Apply(ValVertFieldSeason.Evaluate(e.YearFraction));

    public void Apply(ValVertFieldSeason.Look look)
    {
        LastLook = look; Applied++;
        foreach (var kv in _baseColor)
        {
            var mat = kv.Key; if (!mat) continue;
            bool flower = _flowerSources.Contains(mat);
            var t = flower ? look.flower : look.grass;
            var c = kv.Value;
            mat.SetColor(colorProperty, new Color(c.r * t.r, c.g * t.g, c.b * t.b, flower ? c.a * look.bloom : c.a));
        }
    }
}
