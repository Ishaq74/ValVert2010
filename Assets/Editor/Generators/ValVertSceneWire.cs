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

    /// <summary>GameObject.Find ignores inactive objects; the host is inactive while editing.</summary>
    public static GameObject FindHost()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.parent == null && t.name == HostName) return t.gameObject;
        return null;
    }

    public static GameObject Ensure()
    {
        var go = FindHost();
        // A new host starts INACTIVE so ExecuteAlways OnEnable (ValVertClock.Apply) does not bake fog/ambient/sun/sky.mat
        // into the scene while wiring; WireIntoScene flips m_IsActive in the saved YAML (scoped diff, same runtime result).
        if (!go) go = new GameObject(HostName);
        go.SetActive(false);   // always inactive while editing (also for an existing host); SaveScoped re-activates it in the YAML
        var clk = go.GetComponent<ValVertClock>(); bool freshClock = !clk;
        if (!clk) clk = go.AddComponent<ValVertClock>();
        if (!go.GetComponent<ValVertSeasons>()) go.AddComponent<ValVertSeasons>();
        if (!go.GetComponent<ValVertFieldSeasonal>()) go.AddComponent<ValVertFieldSeasonal>();   // first bus consumer (grass/flower tint)
        if (freshClock) { clk.month = 10; clk.day = 20; clk.hour = 11f; clk.timeScale = 60f; }   // same default as ValVertEnvWire (reference photos)
        var cfg = AssetDatabase.LoadAssetAtPath<ValVertWorldConfig>(ConfigPath);
        if (!cfg) throw new System.InvalidOperationException("missing " + ConfigPath);
        ValVertRuntimeBootstrap.EnsureOn(go, cfg);
        return go;
    }

    /// <summary>Opens the scene with the existing host pre-patched to m_IsActive: 0 in the YAML, so ValVertClock's ExecuteAlways
    /// OnEnable never runs on open (it would bake fog/ambient/sun/moon/sky.mat into the scene and the diff). SaveScoped restores it.</summary>
    public static UnityEngine.SceneManagement.Scene OpenQuiesced()
    {
        var text = System.IO.File.ReadAllText(ScenePath);
        int n = text.IndexOf("\n  m_Name: " + HostName + "\n", System.StringComparison.Ordinal);
        if (n >= 0)
        {
            int i = text.LastIndexOf("\n--- !u!1 &", n, System.StringComparison.Ordinal);
            int j = text.IndexOf("\n--- ", n, System.StringComparison.Ordinal);
            var doc = text.Substring(i, (j < 0 ? text.Length : j) - i);
            if (doc.Contains("  m_IsActive: 1"))
                System.IO.File.WriteAllText(ScenePath, text.Substring(0, i) + doc.Replace("  m_IsActive: 1", "  m_IsActive: 0") + (j < 0 ? "" : text.Substring(j)));
        }
        return EditorSceneManager.OpenScene(ScenePath);
    }

    /// <summary>Saves the open scene with the host inactive (no ExecuteAlways bake), then flips only the host's m_IsActive back to 1 in the YAML.</summary>
    public static void SaveScoped(UnityEngine.SceneManagement.Scene sc, GameObject host)
    {
        if (host.activeSelf) throw new System.InvalidOperationException("host must be inactive while saving (use Ensure())");
        EditorSceneManager.MarkSceneDirty(sc);
        EditorSceneManager.SaveScene(sc);
        ActivateHostInYaml(host);
    }

    public static void WireIntoScene()
    {
        var sc = OpenQuiesced();
        var go = Ensure();
        SaveScoped(sc, go);
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
