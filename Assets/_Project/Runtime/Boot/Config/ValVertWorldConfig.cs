using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored world runtime configuration (SOURCE data, lives in Assets/_Project/Settings/World/).
/// Read by ValVertRuntimeBootstrap. Validated by ValVertWorldConfig.Validate() (also run by the
/// Editor validation suite).
/// </summary>
[CreateAssetMenu(menuName = "ValVert/World Config", fileName = "ValVertWorldConfig")]
public class ValVertWorldConfig : ScriptableObject
{
    [Serializable]
    public class RegionSpec
    {
        public string id;
        [Tooltip("Scene root GameObject name (GameObject.Find).")]
        public string objectName;
        public ValVertWorldStreaming.Policy policy = ValVertWorldStreaming.Policy.DistanceActivated;
        [Tooltip("Register every direct child as its own region (e.g. 32 m grass chunks).")]
        public bool perChild;
        [Min(0f)] public float loadRadius = 380f;
        [Min(0f)] public float unloadRadius = 460f;
    }

    [Header("Region activation (SetActive by bounds distance — not asset streaming)")]
    public bool enableRegionActivation = true;
    [Min(0.05f)] public float updateInterval = 0.5f;
    [Min(0f)] public float minHysteresis = 10f;
    [Min(0f)] public float safetyMargin = 8f;
    public List<RegionSpec> regions = new List<RegionSpec>();

    [Header("Legacy defaults (used when a RegionSpec has no radii)")]
    public float streamLoadRadius = 380f;
    public float streamUnloadRadius = 460f;

    [Header("Vegetation pack provenance (documentary for tools)")]
    public string grassPackAssetStoreId = "138810";
    public string grassPackStatus = "integrated_private_thirdparty";

    public List<string> Validate()
    {
        var e = new List<string>();
        var ids = new HashSet<string>();
        foreach (var r in regions)
        {
            if (r == null) { e.Add("null region"); continue; }
            if (string.IsNullOrEmpty(r.id)) e.Add("region without id");
            else if (!ids.Add(r.id)) e.Add("duplicate region id " + r.id);
            if (string.IsNullOrEmpty(r.objectName)) e.Add(r.id + ": objectName unset");
            if (float.IsNaN(r.loadRadius) || float.IsNaN(r.unloadRadius) || r.loadRadius < 0) e.Add(r.id + ": invalid radii");
            else if (r.policy == ValVertWorldStreaming.Policy.DistanceActivated && r.unloadRadius < r.loadRadius + minHysteresis)
                e.Add($"{r.id}: unloadRadius {r.unloadRadius} < loadRadius {r.loadRadius} + hysteresis {minHysteresis}");
        }
        return e;
    }
}
