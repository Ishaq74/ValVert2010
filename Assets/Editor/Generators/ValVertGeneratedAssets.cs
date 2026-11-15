using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fresh-clone recovery of the git-ignored GENERATED assets that the committed scene references by GUID.
///
/// Generated assets are born with random GUIDs, so a plain regeneration would leave ValVert.unity pointing at nothing.
/// Pins (Assets/Settings/Architecture/generated_guid_pins.json: path -> guid) restore the GUIDs the scene was
/// saved with: after each generation step the .meta guid is rewritten and the asset re-imported (verified: the GUID database
/// resolves the pinned guid, the random one disappears).
///
/// Batch: Unity -batchmode -quit -projectPath . -executeMethod ValVertGeneratedAssets.RegenerateAll   (driven by Tools/rebuild/restore_world.py)
/// Never saves the scene. Exit code 1 if any pinned asset is missing or ends with a different GUID.
/// </summary>
public static class ValVertGeneratedAssets
{
    public const string PinsPath = "Assets/Settings/Architecture/generated_guid_pins.json";
    const string ScenePath = "Assets/Scenes/ValVert.unity";

    static void L(string s) { Debug.Log("[VV][GeneratedAssets] " + s); }

    public static Dictionary<string, string> LoadPins()
    {
        // the JSON is a flat path -> guid map (JsonUtility cannot read dictionaries): parse the "assets" object by hand
        var d = new Dictionary<string, string>();
        var text = File.ReadAllText(PinsPath);
        int a = text.IndexOf("\"assets\"", System.StringComparison.Ordinal);
        if (a < 0) throw new System.InvalidOperationException("pins file has no assets object");
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(text.Substring(a), "\"([^\"]+)\"\\s*:\\s*\"([0-9a-f]{32})\""))
            d[m.Groups[1].Value] = m.Groups[2].Value;
        return d;
    }

    /// <summary>Rewrites the .meta guid of every existing pinned asset whose guid differs, then re-imports it. Returns (applied, missing).</summary>
    public static (int applied, int missing) ApplyPins(Dictionary<string, string> pins)
    {
        int applied = 0, missing = 0; var changed = new List<string>();
        foreach (var kv in pins)
        {
            var meta = kv.Key + ".meta";
            if (!File.Exists(kv.Key) || !File.Exists(meta)) { missing++; continue; }
            var cur = AssetDatabase.AssetPathToGUID(kv.Key);
            if (string.IsNullOrEmpty(cur)) { AssetDatabase.ImportAsset(kv.Key, ImportAssetOptions.ForceSynchronousImport); cur = AssetDatabase.AssetPathToGUID(kv.Key); }
            if (cur == kv.Value) continue;
            var t = File.ReadAllText(meta);
            var m = System.Text.RegularExpressions.Regex.Match(t, "(?m)^guid: [0-9a-f]{32}");
            if (!m.Success) throw new System.InvalidOperationException("meta without guid line: " + meta);
            File.WriteAllText(meta, t.Substring(0, m.Index) + "guid: " + kv.Value + t.Substring(m.Index + m.Length));
            changed.Add(kv.Key); applied++;
        }
        AssetDatabase.StartAssetEditing();
        try { foreach (var p in changed) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate); }
        finally { AssetDatabase.StopAssetEditing(); }
        AssetDatabase.Refresh();
        return (applied, missing);
    }

    public static List<string> VerifyPins(Dictionary<string, string> pins)
    {
        var errs = new List<string>();
        foreach (var kv in pins)
        {
            if (!File.Exists(kv.Key)) { errs.Add("pinned asset missing: " + kv.Key); continue; }
            var g = AssetDatabase.AssetPathToGUID(kv.Key);
            if (g != kv.Value) errs.Add($"guid mismatch {kv.Key}: {g} != pin {kv.Value}");
            else if (AssetDatabase.GUIDToAssetPath(kv.Value) != kv.Key) errs.Add("guid does not resolve back to path: " + kv.Key);
        }
        return errs;
    }

    public static void RegenerateAll()
    {
        int code = 0;
        try
        {
            var pins = LoadPins(); L("pins=" + pins.Count);

            // 1. vendor-derived prefabs (trees/bushes) from the imported Asset Store packages
            if (!Directory.Exists("Assets/ThirdParty/RealisticTrees") || !Directory.Exists("Assets/ThirdParty/InnerverseInteractive"))
                throw new System.InvalidOperationException("vendor packages not imported (Assets/ThirdParty/RealisticTrees, Assets/ThirdParty/InnerverseInteractive): see Docs/clean-clone.md step 3");
            int pf = ValVertDress.RegenerateVendorPrefabs(); L("vendor prefabs written=" + pf);

            // 2. field grass + flower chunks (+ their pack-derived materials) in memory; scene is NOT saved
            var cfg = ValVertGrassField.LoadConfig();
            var errs = ValVertGrassField.Preflight(cfg);
            if (errs.Count > 0) throw new System.InvalidOperationException("grass preflight failed:\n  " + string.Join("\n  ", errs));
            EditorSceneManager.OpenScene(ScenePath);
            var terr = ValVertGrassField.MainTerrainForTools();
            ValVertGrassField.Build(terr);
            ValVertFlowerField.Build(terr);   // 191 flower chunks + derived atlas + flower_pack.mat (pack cards grassFlower01..10)
            AssetDatabase.SaveAssets();

            // 3. pin GUIDs back to what the committed scene/terrain reference
            var (applied, missing) = ApplyPins(pins);
            L($"pins applied={applied} missing={missing}");
            var verr = VerifyPins(pins);
            foreach (var e in verr) Debug.LogError("[VV][GeneratedAssets] " + e);
            if (verr.Count > 0) code = 1;

            // 4. terrain tree prototypes: TerrainData (committed, LFS) stores prefab GUID + root-GameObject fileID. The GUIDs are pinned above;
            //    the root fileIDs are pinned by Tools/architecture/tree_pins.py (restore_world applies them to the regenerated prefabs, then runs
            //    ReportTreesBatch). The terrain asset itself is never modified, so no rebind and no dirty LFS file.
            if (code == 0) L("REGENERATE_OK all pinned GUIDs resolve to their paths (tree fileIDs: run tree_pins.py apply, then ReportTreesBatch)");
        }
        catch (System.Exception ex) { Debug.LogError("[VV][GeneratedAssets] FAILED: " + ex); code = 1; }
        EditorApplication.Exit(code);
    }

    /// <summary>Opens the committed scene and reports every terrain tree prototype with its prefab (null = broken reference).</summary>
    public static int ReportTrees()
    {
        EditorSceneManager.OpenScene(ScenePath);
        int bad = 0, total = 0;
        foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var protos = t.terrainData.treePrototypes;
            for (int i = 0; i < protos.Length; i++)
            {
                total++; var pf = protos[i].prefab;
                var path = pf ? AssetDatabase.GetAssetPath(pf) : "<NULL>";
                if (!pf) bad++;
                L($"terrain={t.name} proto[{i}] {(pf ? pf.name : "NULL")} -> {path} instances={t.terrainData.treeInstanceCount}");
            }
        }
        L($"TREE_PROTOTYPES total={total} broken={bad}");
        return bad;
    }

    public static void ReportTreesBatch() { EditorApplication.Exit(ReportTrees() == 0 ? 0 : 1); }

    /// <summary>Check-only (no generation): fails when any pinned asset is absent/mismatched. Used by restore_world.py as the final gate.</summary>
    public static void VerifyOnly()
    {
        var errs = VerifyPins(LoadPins());
        foreach (var e in errs) Debug.LogError("[VV][GeneratedAssets] " + e);
        if (errs.Count == 0) L("VERIFY_OK");
        EditorApplication.Exit(errs.Count == 0 ? 0 : 1);
    }
}
