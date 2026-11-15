using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>Seasonal tint of field grass/flowers: pure rules + the real bus consumer (first ValVertEventBus subscriber in gameplay).</summary>
public class FieldSeasonalTests
{
    readonly List<Object> _junk = new List<Object>();
    [SetUp] public void SetUp()
    {
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
        foreach (var b in Object.FindObjectsByType<ValVertRuntimeBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Object.DestroyImmediate(b.gameObject);
    }
    [TearDown] public void TearDown()
    {
        foreach (var o in _junk) if (o) Object.DestroyImmediate(o);
        _junk.Clear();
        var moon = GameObject.Find("Moon"); if (moon) Object.DestroyImmediate(moon);
        ValVertEventBus.ResetForDomain(); ValVertServices.ResetForDomain();
    }

    // ---------------- pure rules ----------------
    [Test] public void Phenology_MatchesSeasonsInstance()
    {
        var go = new GameObject("S"); _junk.Add(go); var s = go.AddComponent<ValVertSeasons>();
        foreach (var doy in new[] { 5f, 100f, 120f, 180f, 270f, 300f, 320f, 340f, 364f })
        {
            s.Evaluate(doy); ValVertSeasons.Phenology(doy, out var leaf, out var aut, out var lit, out var name);
            Assert.AreEqual(s.LeafCover, leaf, 1e-6f, "leaf " + doy); Assert.AreEqual(s.Autumn, aut, 1e-6f, "autumn " + doy);
            Assert.AreEqual(s.Litter, lit, 1e-6f, "litter " + doy); Assert.AreEqual(s.SeasonName, name);
        }
    }

    [Test] public void Rules_SummerBloomsAutumnCuresWinterGoesDormant()
    {
        var jun = ValVertFieldSeason.Evaluate(170f / 365f); var oct = ValVertFieldSeason.Evaluate(293f / 365f); var dec = ValVertFieldSeason.Evaluate(350f / 365f);
        Assert.Greater(jun.bloom, 0.95f); Assert.Less(oct.bloom, 0.15f); Assert.AreEqual(0f, dec.bloom, 1e-4f);
        Assert.Greater(jun.grass.g, 0.95f); Assert.Greater(jun.grass.b, 0.95f, "high summer: neutral tint (pack texture colours show as authored)");
        Assert.Less(oct.grass.b, jun.grass.b - 0.2f, "curing yellows the grass (blue channel falls)"); Assert.Greater(oct.grass.r, oct.grass.b);
        Assert.Less(dec.grass.g, jun.grass.g, "dormant grass is darker/bleached");
        Assert.AreEqual(jun.bloom, jun.flower.a, 1e-6f); Assert.AreEqual(1f, jun.grass.a);
    }

    [Test] public void Rules_ContinuousAcrossYearWrap_AndBounded()
    {
        var a = ValVertFieldSeason.Evaluate(0f); var b = ValVertFieldSeason.Evaluate(1f);
        Assert.AreEqual(a.grass.g, b.grass.g, 1e-4f); Assert.AreEqual(a.bloom, b.bloom, 1e-4f);
        Look prev = default; bool have = false;
        for (int d = 0; d <= 365; d++)
        {
            var l = ValVertFieldSeason.Evaluate(d / 365f);
            foreach (var c in new[] { l.grass.r, l.grass.g, l.grass.b, l.flower.r, l.flower.g, l.flower.b, l.bloom }) { Assert.IsFalse(float.IsNaN(c)); Assert.GreaterOrEqual(c, 0f); Assert.LessOrEqual(c, 1.2f); }
            if (have) Assert.Less(Mathf.Abs(l.grass.g - prev.g), 0.06f, "no daily jump at day " + d);
            prev = new Look { g = l.grass.g }; have = true;
        }
    }
    struct Look { public float g; }

    // ---------------- consumer on the real bus ----------------
    class Rig { public GameObject grassRoot, flowerRoot, host; public Material gMat, fMat; public Renderer gR, fR; public ValVertFieldSeasonal seas; }

    Rig Build(string suffix)
    {
        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        var r = new Rig { gMat = new Material(sh) { name = "g" }, fMat = new Material(sh) { name = "f" } };
        r.gMat.SetColor("_BaseColor", Color.white); r.fMat.SetColor("_BaseColor", Color.white);
        _junk.Add(r.gMat); _junk.Add(r.fMat);
        r.grassRoot = Quad("TG_" + suffix, r.gMat, out r.gR); r.flowerRoot = Quad("TF_" + suffix, r.fMat, out r.fR);
        // two chunks sharing the same source material (SRP-batcher friendly: one clone per source)
        var extra = GameObject.CreatePrimitive(PrimitiveType.Quad); extra.transform.SetParent(r.grassRoot.transform); extra.GetComponent<Renderer>().sharedMaterial = r.gMat;
        r.host = new GameObject("TH_" + suffix); _junk.Add(r.host); r.host.SetActive(false);
        r.seas = r.host.AddComponent<ValVertFieldSeasonal>(); r.seas.grassRoots = new[] { "TG_" + suffix }; r.seas.flowerRoots = new[] { "TF_" + suffix };
        return r;
    }
    GameObject Quad(string name, Material m, out Renderer rend)
    {
        var root = new GameObject(name); _junk.Add(root);
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad); q.transform.SetParent(root.transform); rend = q.GetComponent<Renderer>(); rend.sharedMaterial = m;
        return root;
    }

    [Test] public void Consumer_AppliesOnSeasonChanged_NeverTouchesSharedAssets_RestoresOnDisable()
    {
        var r = Build("a");
        r.host.SetActive(true);
        Assert.AreEqual(3, r.seas.BoundRendererCount); Assert.AreEqual(2, r.seas.CloneCount, "one clone per source material");
        Assert.AreNotSame(r.gMat, r.gR.sharedMaterial, "renderer now uses a runtime clone");
        ValVertEventBus.Publish(new ValVertSeasonChanged(170f / 365f));
        var summer = r.gR.sharedMaterial.GetColor("_BaseColor"); var fSummer = r.fR.sharedMaterial.GetColor("_BaseColor");
        ValVertEventBus.Publish(new ValVertSeasonChanged(293f / 365f));
        var oct = r.gR.sharedMaterial.GetColor("_BaseColor"); var fOct = r.fR.sharedMaterial.GetColor("_BaseColor");
        Assert.AreNotEqual(summer, oct, "grass tint changed with the season event");
        Assert.Greater(fSummer.a, 0.95f); Assert.Less(fOct.a, 0.15f, "flowers fade out in autumn");
        Assert.AreEqual(Color.white, r.gMat.GetColor("_BaseColor"), "project/shared material never mutated");
        Assert.AreEqual(Color.white, r.fMat.GetColor("_BaseColor"));
        int applied = r.seas.Applied;
        r.host.SetActive(false);
        Assert.AreSame(r.gMat, r.gR.sharedMaterial, "original shared material restored"); Assert.AreSame(r.fMat, r.fR.sharedMaterial);
        Assert.AreEqual(0, ValVertEventBus.ListenerCount<ValVertSeasonChanged>(), "subscription disposed");
        ValVertEventBus.Publish(new ValVertSeasonChanged(0.1f)); Assert.AreEqual(applied, r.seas.Applied, "no delivery after disable");
    }

    [Test] public void Consumer_ReplaysLastSeasonOnEnable_AndReenableIsClean()
    {
        ValVertEventBus.Publish(new ValVertSeasonChanged(293f / 365f));   // published BEFORE the consumer exists (sticky value)
        var r = Build("b"); r.host.SetActive(true);
        Assert.AreEqual(1, r.seas.Applied, "late subscriber receives the sticky last value");
        Assert.Less(r.fR.sharedMaterial.GetColor("_BaseColor").a, 0.15f);
        r.host.SetActive(false); r.host.SetActive(true);
        Assert.AreEqual(1, ValVertEventBus.ListenerCount<ValVertSeasonChanged>(), "exactly one subscription after re-enable");
        Assert.AreEqual(2, r.seas.CloneCount);
    }

    [Test] public void Consumer_MissingRoots_AreHarmless()
    {
        var go = new GameObject("TH_none"); _junk.Add(go); go.SetActive(false);
        var s = go.AddComponent<ValVertFieldSeasonal>(); s.grassRoots = new[] { "does_not_exist" }; s.flowerRoots = new string[0];
        go.SetActive(true); ValVertEventBus.Publish(new ValVertSeasonChanged(0.5f));
        Assert.AreEqual(0, s.BoundRendererCount); Assert.AreEqual(1, s.Applied);
    }

    [Test] public void Bootstrap_SeasonEvent_IsNotStaleAfterDateJump()
    {
        var go = new GameObject("TH_boot"); _junk.Add(go); go.SetActive(false);
        var clock = go.AddComponent<ValVertClock>(); clock.running = false; clock.month = 10; clock.day = 20; clock.hour = 11f;
        go.AddComponent<ValVertSeasons>();
        var cfg = ScriptableObject.CreateInstance<ValVertWorldConfig>(); _junk.Add(cfg);
        ValVertRuntimeBootstrap.EnsureOn(go, cfg);
        go.SetActive(true);
        Assert.IsTrue(ValVertEventBus.TryGetLast<ValVertSeasonChanged>(out var a));
        float expectLeaf, expectAut; ValVertSeasons.Phenology(a.YearFraction * 365f, out expectLeaf, out expectAut, out _, out _);
        Assert.Greater(expectAut, 0.5f, "20 Oct is deep in autumn (a stale default of 0 would fail this)");
        Assert.AreEqual(expectLeaf, a.LeafCover, 1e-4f); Assert.AreEqual(expectAut, a.Autumn, 1e-4f);
    }
}
