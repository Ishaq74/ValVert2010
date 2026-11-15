using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity-side evidence for GUID-preserving moves (complements Tools/architecture/verify_guid_refs.py):
/// AssetDatabase must resolve every manifest destination to the recorded GUID, the sky shader must be found by
/// name and be the one used by sky_blend.mat, and the real scene's sky material reference must resolve.
/// Batch: unity -batchmode -nographics -quit -executeMethod ValVertMigrationCheck.Run   (exit code 0/2)
/// </summary>
public static class ValVertMigrationCheck
{
    const string Manifest = "Assets/_Project/Settings/Architecture/migration_manifest.json";
    [System.Serializable] class Move { public string from, to, guid, system; }
    [System.Serializable] class Man { public Move[] moves; }

    public static List<string> Check()
    {
        var errs = new List<string>();
        var man = JsonUtility.FromJson<Man>(File.ReadAllText(Manifest));
        foreach (var m in man.moves)
        {
            var g = AssetDatabase.AssetPathToGUID(m.to);
            if (string.IsNullOrEmpty(g)) errs.Add("AssetDatabase does not know " + m.to);
            else if (g != m.guid) errs.Add($"GUID changed {m.to}: {g} != {m.guid}");
            if (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(m.from)) && File.Exists(m.from)) errs.Add("old path still imported: " + m.from);
            if (m.to.EndsWith(".mat") && AssetDatabase.LoadAssetAtPath<Material>(m.to) == null) errs.Add("material fails to load: " + m.to);
            if (m.to.EndsWith(".prefab") && AssetDatabase.LoadAssetAtPath<GameObject>(m.to) == null) errs.Add("prefab fails to load: " + m.to);
        }
        var sh = Shader.Find("ValVert/SkyBlend");
        if (sh == null) errs.Add("Shader.Find(ValVert/SkyBlend) == null after move");
        var blend = AssetDatabase.LoadAssetAtPath<Material>(ValVertPaths.SkyMaterials + "sky_blend.mat");
        if (blend == null) errs.Add("sky_blend.mat missing");
        else if (blend.shader != sh) errs.Add("sky_blend.mat shader != ValVert/SkyBlend (" + (blend.shader ? blend.shader.name : "null") + ")");
        else if (!blend.shader.isSupported) errs.Add("sky_blend shader unsupported");
        return errs;
    }

    public static void Run()
    {
        var errs = Check();
        var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/ValVert.unity");
        var sky = RenderSettings.skybox;
        if (sky == null) errs.Add("scene RenderSettings.skybox is null (sky.mat reference broken)");
        else
        {
            var p = AssetDatabase.GetAssetPath(sky);
            if (p != ValVertPaths.SkyMaterials + "sky.mat") errs.Add("scene skybox resolves to unexpected path: " + p);
            else Debug.Log("[VV][MigrationCheck] scene skybox -> " + p);
        }
        foreach (var e in errs) Debug.LogError("[VV][MigrationCheck] FAIL " + e);
        Debug.Log("[VV][MigrationCheck] " + (errs.Count == 0 ? "ALL_OK" : errs.Count + " failures"));
        EditorApplication.Exit(errs.Count == 0 ? 0 : 2);
    }
}
