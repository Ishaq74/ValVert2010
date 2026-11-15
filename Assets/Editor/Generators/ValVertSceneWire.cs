using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Persists the lightweight runtime layer into the committed scene: ONE host "ValVertEnvironment" with
/// ValVertClock + ValVertSeasons (no foliage/material lists: those stay with the full ValVertEnvWire.Wire, which
/// reuses this same host) + ValVertRuntimeBootstrap bound to Settings/World/ValVertWorldConfig.asset.
/// Idempotent: re-running changes nothing. Batch: -executeMethod ValVertSceneWire.WireIntoScene
/// </summary>
public static class ValVertSceneWire
{
    public const string ScenePath = "Assets/Scenes/ValVert.unity";
    public const string HostName = "ValVertEnvironment";
    public const string ConfigPath = "Assets/Settings/World/ValVertWorldConfig.asset";

    public static GameObject Ensure()
    {
        var go = GameObject.Find(HostName);
        // A new host starts INACTIVE so ExecuteAlways OnEnable (ValVertClock.Apply) does not bake fog/ambient/sun/sky.mat
        // into the scene while wiring; WireIntoScene flips m_IsActive in the saved YAML (scoped diff, same runtime result).
        if (!go) { go = new GameObject(HostName); go.SetActive(false); }
        var clk = go.GetComponent<ValVertClock>(); bool freshClock = !clk;
        if (!clk) clk = go.AddComponent<ValVertClock>();
        if (!go.GetComponent<ValVertSeasons>()) go.AddComponent<ValVertSeasons>();
        if (freshClock) { clk.month = 10; clk.day = 20; clk.hour = 11f; clk.timeScale = 60f; }   // same default as ValVertEnvWire (reference photos)
        var cfg = AssetDatabase.LoadAssetAtPath<ValVertWorldConfig>(ConfigPath);
        if (!cfg) throw new System.InvalidOperationException("missing " + ConfigPath);
        ValVertRuntimeBootstrap.EnsureOn(go, cfg);
        return go;
    }

    public static void WireIntoScene()
    {
        var sc = EditorSceneManager.OpenScene(ScenePath);
        var go = Ensure();
        bool wasInactive = !go.activeSelf;
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        if (wasInactive) ActivateHostInYaml(go);
        Debug.Log($"[VV][SceneWire] host={go.name} components={string.Join(",", System.Array.ConvertAll(go.GetComponents<Component>(), c => c.GetType().Name))}");
    }

    // Flip m_IsActive: 0 -> 1 on the host GameObject document only (text patch, verified).
    static void ActivateHostInYaml(GameObject go)
    {
        var id = GlobalObjectId.GetGlobalObjectIdSlow(go).targetObjectId;
        var text = System.IO.File.ReadAllText(ScenePath);
        var header = "--- !u!1 &" + id + "\n";
        int i = text.IndexOf(header, System.StringComparison.Ordinal);
        if (i < 0) throw new System.InvalidOperationException("host GameObject doc not found in scene YAML");
        int j = text.IndexOf("\n--- ", i + header.Length, System.StringComparison.Ordinal);
        var doc = text.Substring(i, (j < 0 ? text.Length : j) - i);
        if (!doc.Contains("  m_IsActive: 0")) throw new System.InvalidOperationException("host is not inactive in YAML");
        System.IO.File.WriteAllText(ScenePath, text.Substring(0, i) + doc.Replace("  m_IsActive: 0", "  m_IsActive: 1") + (j < 0 ? "" : text.Substring(j)));
        Debug.Log("[VV][SceneWire] host activated in YAML (fileID " + id + ")");
    }
}
