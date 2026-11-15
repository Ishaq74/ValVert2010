using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// World REGION ACTIVATION by horizontal distance from the viewer to each region's world bounds.
///
/// What it is: GameObject.SetActive on registered roots (or on each direct child of a root), with
/// load/unload hysteresis, bounds-based distance and viewer safety rules.
/// What it is NOT: asynchronous asset streaming, Addressables, or additive multi-scene loading — all
/// content stays resident in memory; activation only saves rendering/physics/Update cost.
///
/// Safety rules (always on):
///  * Distance is measured to the region's XZ bounds (Renderers' meshes + Terrains), never to the root
///    pivot, so standing anywhere over a 1 km terrain gives distance 0.
///  * A region is never deactivated while the viewer is inside its XZ bounds (expanded by safetyMargin)
///    or while the viewer is a descendant of the region root.
///  * AlwaysActive regions are activated at registration and are never deactivated.
///  * Roots already inactive when registered (authoring choice) are only activated if AlwaysActive.
///  * Disabling/destroying this component re-activates every region it deactivated.
/// </summary>
public class ValVertWorldStreaming : MonoBehaviour
{
    public enum Policy { DistanceActivated = 0, AlwaysActive = 1 }

    [Serializable]
    public class Region
    {
        public string id;
        public Transform root;
        public float loadRadius = 350f;
        public float unloadRadius = 420f;
        [FormerlySerializedAs("keepWarm")]
        [Tooltip("AlwaysActive: activated at registration and never deactivated (formerly 'keepWarm').")]
        public bool alwaysActive;

        [NonSerialized] public Bounds bounds;
        [NonSerialized] public bool hasBounds;
        [NonSerialized] public bool active;
        [NonSerialized] public bool deactivatedByUs;
        [NonSerialized] public float lastDistance = float.PositiveInfinity;
        [NonSerialized] public int transitions;
        public Policy policy => alwaysActive ? Policy.AlwaysActive : Policy.DistanceActivated;
    }

    public Transform viewer;
    public List<Region> regions = new List<Region>();
    [Min(0f)] public float updateInterval = 0.5f;
    [Tooltip("Minimum gap enforced between load and unload radius (m) so regions cannot flicker.")]
    [Min(0f)] public float minHysteresis = 10f;
    [Tooltip("Regions whose XZ bounds, expanded by this margin, contain the viewer are never deactivated.")]
    [Min(0f)] public float safetyMargin = 8f;
    public bool publishEvents = true;

    float _t;
    public int ActiveCount { get { int n = 0; foreach (var r in regions) if (r.root && r.root.gameObject.activeSelf) n++; return n; } }

    void OnEnable()
    {
        ValVertServices.Register(this);
        if (!viewer && Camera.main) viewer = Camera.main.transform;
    }

    void OnDisable()
    {
        RestoreAll();
        ValVertServices.Unregister(this);
    }

    /// <summary>Register (or re-register by id) a region. Re-registration replaces root/radii and recomputes
    /// bounds; a previously deactivated old root is re-activated first.</summary>
    public Region Register(string id, Transform root, float loadRadius, float unloadRadius, bool alwaysActive = false)
    {
        if (!root || string.IsNullOrEmpty(id)) return null;
        if (float.IsNaN(loadRadius) || float.IsNaN(unloadRadius) || loadRadius < 0f) throw new ArgumentException("invalid radii for region " + id);
        if (unloadRadius < loadRadius + minHysteresis) unloadRadius = loadRadius + minHysteresis;

        var r = Find(id);
        if (r != null)
        {
            if (r.root != root && r.root && r.deactivatedByUs) r.root.gameObject.SetActive(true);
        }
        else { r = new Region { id = id }; regions.Add(r); }
        r.root = root; r.loadRadius = loadRadius; r.unloadRadius = unloadRadius; r.alwaysActive = alwaysActive;
        r.deactivatedByUs = false;
        RefreshBounds(r);
        if (alwaysActive && !root.gameObject.activeSelf) root.gameObject.SetActive(true);
        r.active = root.gameObject.activeSelf;
        return r;
    }

    /// <summary>Register each direct child of <paramref name="parent"/> as its own region (id = prefix/childName#index).</summary>
    public int RegisterChildren(string prefix, Transform parent, float loadRadius, float unloadRadius)
    {
        if (!parent) return 0;
        int n = 0;
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (Register(prefix + "/" + c.name + "#" + i, c, loadRadius, unloadRadius) != null) n++;
        }
        return n;
    }

    public bool Unregister(string id)
    {
        var r = Find(id); if (r == null) return false;
        if (r.root && r.deactivatedByUs) r.root.gameObject.SetActive(true);
        regions.Remove(r); return true;
    }

    public Region Find(string id) { foreach (var r in regions) if (r.id == id) return r; return null; }

    public void RestoreAll()
    {
        foreach (var r in regions)
            if (r.root && r.deactivatedByUs) { r.root.gameObject.SetActive(true); r.deactivatedByUs = false; r.active = true; }
    }

    /// <summary>Recompute world bounds from mesh/terrain data (works for inactive objects too).</summary>
    public static bool ComputeBounds(Transform root, out Bounds b)
    {
        b = default; bool any = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf.sharedMesh) continue;
            Encapsulate(ref b, ref any, Transform(mf.sharedMesh.bounds, mf.transform.localToWorldMatrix));
        }
        foreach (var sk in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if (sk.sharedMesh) Encapsulate(ref b, ref any, Transform(sk.sharedMesh.bounds, sk.transform.localToWorldMatrix));
        foreach (var t in root.GetComponentsInChildren<Terrain>(true))
        {
            if (!t.terrainData) continue;
            var p = t.transform.position; var s = t.terrainData.size;
            Encapsulate(ref b, ref any, new Bounds(p + s * 0.5f, s));
        }
        if (!any) { b = new Bounds(root.position, Vector3.zero); }
        return any;
    }

    public void RefreshBounds(Region r) { if (r?.root) r.hasBounds = ComputeBounds(r.root, out r.bounds); }

    static void Encapsulate(ref Bounds b, ref bool any, Bounds x) { if (!any) { b = x; any = true; } else b.Encapsulate(x); }
    static Bounds Transform(Bounds lb, Matrix4x4 m)
    {
        var c = m.MultiplyPoint3x4(lb.center); var e = lb.extents;
        var ax = m.MultiplyVector(new Vector3(e.x, 0, 0)); var ay = m.MultiplyVector(new Vector3(0, e.y, 0)); var az = m.MultiplyVector(new Vector3(0, 0, e.z));
        var ext = new Vector3(Mathf.Abs(ax.x) + Mathf.Abs(ay.x) + Mathf.Abs(az.x), Mathf.Abs(ax.y) + Mathf.Abs(ay.y) + Mathf.Abs(az.y), Mathf.Abs(ax.z) + Mathf.Abs(ay.z) + Mathf.Abs(az.z));
        return new Bounds(c, ext * 2f);
    }

    /// <summary>Horizontal distance from p to the region's XZ bounds (0 inside). Falls back to the pivot only
    /// for regions that contain no meshes/terrains.</summary>
    public static float HorizontalDistance(Region r, Vector3 p)
    {
        var b = r.bounds;
        float dx = Mathf.Max(0f, Mathf.Max(b.min.x - p.x, p.x - b.max.x));
        float dz = Mathf.Max(0f, Mathf.Max(b.min.z - p.z, p.z - b.max.z));
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;
        if (_t < updateInterval) return;
        _t = 0f;
        Tick();
    }

    public void Tick()
    {
        if (!viewer) return;
        var p = viewer.position;
        for (int i = regions.Count - 1; i >= 0; i--)
        {
            var r = regions[i];
            if (!r.root) { regions.RemoveAt(i); continue; }   // destroyed root: drop registration
            float d = HorizontalDistance(r, p);
            r.lastDistance = d;
            bool isActive = r.root.gameObject.activeSelf;
            if (r.alwaysActive)
            {
                if (!isActive) SetActive(r, true, d);
                continue;
            }
            // Only re-activate what this component deactivated: objects disabled by authoring/other systems stay off.
            if (!isActive && r.deactivatedByUs && d <= r.loadRadius) SetActive(r, true, d);
            else if (isActive && d >= r.unloadRadius && !Protected(r, p, d)) SetActive(r, false, d);
        }
    }

    bool Protected(Region r, Vector3 p, float d)
    {
        if (viewer && viewer.IsChildOf(r.root)) return true;
        if (d <= safetyMargin) return true;
        return false;
    }

    void SetActive(Region r, bool on, float d)
    {
        r.root.gameObject.SetActive(on);
        r.active = on; r.deactivatedByUs = !on; r.transitions++;
        if (publishEvents) ValVertEventBus.Publish(new ValVertRegionActivationChanged(r.id, on, d));
    }
}
