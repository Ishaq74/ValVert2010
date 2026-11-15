// Val Vert — field grass generator (architecture: World/Vegetation).
// Placement/batching/exclusions extracted from ValVertWoods.Grass.
// Config SOURCE: Assets/_Project/Settings/World/Vegetation/grass_field_config.json
// Pack card files + GUIDs come from the verified Asset Store package (config pack.cards); nothing is invented.
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public static class ValVertGrassField
{
    const string GEN = "Assets/ValVert/Gen/";
    public const string ConfigPath = "Assets/_Project/Settings/World/Vegetation/grass_field_config.json";
    public const string ExpectedPackId = "138810";
    public const string ExpectedSchema = "2";

    [System.Serializable]
    public class PackCard
    {
        public string file;          // relative to pack.localRoot
        public string guid;          // expected .meta guid of the authentic package file
        public float weightMeadow, weightPark;
    }

    [System.Serializable]
    public class PackSlot
    {
        public string assetStoreId;
        public string assetStoreUrl;
        public string status;
        public string packageSha256;
        public string localRoot;
        public string kindExpected;
        public PackCard[] cards;
        public string note;
    }

    [System.Serializable]
    public class GrassConfig
    {
        public string schemaVersion;
        public string materialMode;
        public string materialFolder;
        public float[] cardHeightPark, cardHeightMeadow, cardWidthFactor;
        public string rootName;
        public string meshAssetPrefix;
        public string materialName;
        public int seed;
        public float chunkSize;
        public float parkSpacing;
        public float woodsSpacing;
        public float exclThreshold;
        public float canopyThresh;
        public float meadowThresh;
        public PackSlot pack;
        public string[] exclusionMasks; // documentary — Build uses same hard-wired masks as historical Grass
        public string exclusionMasksNote;
        public string notes;
    }

    static void L(string s) { Debug.Log("[VV][GrassField] " + s); }

    public static GrassConfig LoadConfig()
    {
        if (!File.Exists(ConfigPath))
            throw new System.InvalidOperationException("grass config missing: " + ConfigPath);
        return JsonUtility.FromJson<GrassConfig>(File.ReadAllText(ConfigPath));
    }

    static bool FinitePositive(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v > 0f;

    /// <summary>Non-mutating preflight.</summary>
    public static List<string> Preflight(GrassConfig cfg = null)
    {
        cfg = cfg ?? LoadConfig();
        var errs = new List<string>();
        if (cfg == null) { errs.Add("missing config object"); return errs; }
        if (cfg.schemaVersion != ExpectedSchema)
            errs.Add($"schemaVersion '{cfg.schemaVersion}' != expected '{ExpectedSchema}'");
        if (!FinitePositive(cfg.chunkSize)) errs.Add("chunkSize must be finite > 0");
        if (!FinitePositive(cfg.parkSpacing)) errs.Add("parkSpacing must be finite > 0");
        if (!FinitePositive(cfg.woodsSpacing)) errs.Add("woodsSpacing must be finite > 0");
        if (string.IsNullOrEmpty(cfg.rootName)) errs.Add("rootName unset");
        if (string.IsNullOrEmpty(cfg.meshAssetPrefix)) errs.Add("meshAssetPrefix unset");

        if (cfg.pack == null) errs.Add("pack slot missing");
        else if (cfg.pack.assetStoreId != ExpectedPackId)
            errs.Add($"pack.assetStoreId '{cfg.pack.assetStoreId}' != {ExpectedPackId}");

        if (cfg.materialMode != "pack_cutout") errs.Add("materialMode must be pack_cutout (legacy homemade path removed), got: " + cfg.materialMode);
        else if (cfg.pack != null)
        {
            if (string.IsNullOrEmpty(cfg.pack.localRoot)) errs.Add("pack.localRoot unset");
            else if (!Directory.Exists(cfg.pack.localRoot)) errs.Add("pack localRoot absent (import the Asset Store package into ignored Assets/ThirdParty): " + cfg.pack.localRoot);
            if (cfg.pack.cards == null || cfg.pack.cards.Length == 0) errs.Add("pack.cards empty");
            else foreach (var c in cfg.pack.cards)
            {
                var full = (cfg.pack.localRoot + "/" + c.file).Replace('\\', '/');
                if (!File.Exists(full)) { errs.Add("pack card missing: " + full); continue; }
                var g = AssetDatabase.AssetPathToGUID(full);
                if (g != c.guid) errs.Add($"pack card GUID mismatch {c.file}: asset {g} != config {c.guid} (not the authentic package?)");
            }
            if (string.IsNullOrEmpty(cfg.materialFolder)) errs.Add("materialFolder unset");
            else if (!cfg.materialFolder.StartsWith("Assets/ThirdParty/")) errs.Add("materialFolder must be under ignored Assets/ThirdParty/ (pack-derived materials must not be committed)");
            foreach (var arr in new[] { cfg.cardHeightPark, cfg.cardHeightMeadow, cfg.cardWidthFactor })
                if (arr == null || arr.Length != 2 || !FinitePositive(arr[0]) || arr[1] < arr[0]) errs.Add("card size ranges must be [min,max] finite > 0");
        }
        return errs;
    }

    static bool Built(float x, float z)
    {
        for (int k = 0; k < 5; k++)
        {
            var o = k == 0 ? Vector3.zero : Quaternion.Euler(0, k * 90, 0) * Vector3.forward * 0.6f;
            if (Physics.Raycast(new Vector3(x + o.x, 400f, z + o.z), Vector3.down, out var h, 800f) && !(h.collider is TerrainCollider))
                return true;
        }
        return false;
    }

    static Texture2D ResolveCard(GrassConfig cfg, PackCard c)
    {
        string path = (cfg.pack.localRoot + "/" + c.file).Replace('\\', '/');
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti != null)
        {
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = true;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    /// <summary>
    /// Idempotent-ish: destroys existing rootName in open scene, reuses mesh assets at stable paths
    /// (CreateAsset only when missing; otherwise overwrite contents to preserve .meta GUIDs).
    /// Not claimed byte-proven across machines.
    /// </summary>
    public static void Build(Terrain terr)
    {
        var cfg = LoadConfig();
        var errs = Preflight(cfg);
        if (errs.Count > 0)
            throw new System.InvalidOperationException("ValVertGrassField preflight failed:\n  " + string.Join("\n  ", errs));

        // Remove prior roots to avoid duplicates on rerun
        foreach (var existing in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (existing && existing.name == cfg.rootName && existing.parent == null)
                Object.DestroyImmediate(existing.gameObject);
        }

        Physics.SyncTransforms();
        EnsureFolder(cfg.materialFolder);
        int nCards = cfg.pack.cards.Length;
        var mats = new Material[nCards];
        for (int i = 0; i < nCards; i++)
        {
            var tex = ResolveCard(cfg, cfg.pack.cards[i]);
            if (tex == null) throw new System.InvalidOperationException("pack card failed to load: " + cfg.pack.cards[i].file);
            string mn = cfg.materialName + "_" + i;
            ValVertDress.RelocMat(mn, cfg.materialFolder + "/" + mn + ".mat");
            mats[i] = ValVertDress.Mat(mn, tex, null, true, Color.white, 0.05f);
            mats[i].SetFloat("_Cutoff", 0.5f);
            EditorUtility.SetDirty(mats[i]);
        }

        var ex = ValVertDress.Mask("veg_excl.png");
        var fm = ValVertDress.Mask("canopy_orig.png");
        var me = ValVertDress.Mask("veg_meadow.png");
        var rnd = new System.Random(cfg.seed);
        System.Func<float> R = () => (float)rnd.NextDouble();
        float CH = cfg.chunkSize;
        var root = new GameObject(cfg.rootName);
        long total = 0; int chunks = 0;
        float tp = terr.transform.position.y;
        for (float cx = -532; cx < 532; cx += CH)
        for (float cz = -354.6f; cz < 354.6f; cz += CH)
        {
            bool park = cx > -380 && cx < 500 && cz > -340 && cz < 140;
            float sp = park ? cfg.parkSpacing : cfg.woodsSpacing;
            var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var C = new List<Color>(); var T = new List<int>[nCards]; for (int ci = 0; ci < nCards; ci++) T[ci] = new List<int>();
            for (float x = cx; x < cx + CH; x += sp)
            for (float z = cz; z < cz + CH; z += sp)
            {
                float px = x + R() * sp, pz = z + R() * sp;
                if (px > 531 || pz > 354) continue;
                // Removed dead: ShotClear && magnitude<0 (always false)
                if (ValVertDress.S(ex, px, pz) > cfg.exclThreshold || ValVertWoods.Lab(px, pz) != 0 || ValVertWoods.Hidden(new Vector3(px, 0, pz))) continue;
                if (Built(px, pz)) continue;
                float f = ValVertDress.S(fm, px, pz), m = ValVertDress.S(me, px, pz);
                float p = f > cfg.canopyThresh ? 0.45f : 1f;
                if (R() > p) continue;
                bool meadow = m > cfg.meadowThresh;
                var hr = meadow ? cfg.cardHeightMeadow : cfg.cardHeightPark;
                float hgt = hr[0] + R() * (hr[1] - hr[0]);
                float w = hgt * (cfg.cardWidthFactor[0] + R() * (cfg.cardWidthFactor[1] - cfg.cardWidthFactor[0]));
                float y = terr.SampleHeight(new Vector3(px, 0, pz)) + tp - 0.04f;
                var tint = Color.white; tint.a = 1;
                float a0 = R() * Mathf.PI;
                // pick a pack card by weight (meadow vs lawn/woods floor)
                float tot = 0f; for (int ci = 0; ci < nCards; ci++) tot += meadow ? cfg.pack.cards[ci].weightMeadow : cfg.pack.cards[ci].weightPark;
                float pick = R() * tot; int card = nCards - 1;
                for (int ci = 0; ci < nCards; ci++) { pick -= meadow ? cfg.pack.cards[ci].weightMeadow : cfg.pack.cards[ci].weightPark; if (pick <= 0f) { card = ci; break; } }
                for (int q = 0; q < 3; q++)
                {
                    float a = a0 + q * Mathf.PI / 3;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * w * 0.5f;
                    var b = new Vector3(px, y, pz);
                    int k = V.Count;
                    V.Add(b - d); V.Add(b - d + Vector3.up * hgt); V.Add(b + d + Vector3.up * hgt); V.Add(b + d);
                    for (int r = 0; r < 4; r++) { N.Add(Vector3.up); C.Add(tint); }
                    U.Add(new Vector2(0, 0)); U.Add(new Vector2(0, 1)); U.Add(new Vector2(1, 1)); U.Add(new Vector2(1, 0));
                    T[card].AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                }
            }
            if (V.Count == 0) continue;
            var assetPath = GEN + cfg.meshAssetPrefix + chunks + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            bool created = false;
            if (mesh == null)
            {
                mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
                created = true;
            }
            else
            {
                mesh.Clear();
                mesh.indexFormat = IndexFormat.UInt32;
            }
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, U); mesh.SetColors(C);
            mesh.subMeshCount = nCards; for (int ci = 0; ci < nCards; ci++) mesh.SetTriangles(T[ci], ci);
            mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, assetPath);
            else EditorUtility.SetDirty(mesh);

            var go = new GameObject(cfg.meshAssetPrefix + chunks);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var lg = go.AddComponent<LODGroup>();
            lg.SetLODs(new[] { new LOD(0.06f, new Renderer[] { mr }) });
            lg.RecalculateBounds();
            total += V.Count / 12;
            chunks++;
        }
        AssetDatabase.SaveAssets();
        ValVertDress.Counts["_grass_clumps"] = (int)total;
        L($"mode={cfg.materialMode} cards={nCards} clumps={total} chunks={chunks}");
    }

    // ---- batch entry points (unity -batchmode -executeMethod ValVertGrassField.RebuildInScene) ----
    const string ScenePath = "Assets/Scenes/ValVert.unity";

    public static Terrain MainTerrainForTools() => MainTerrain();

    static Terrain MainTerrain()
    {
        var go = GameObject.Find("ValVertTerrain");
        var t = go ? go.GetComponentInChildren<Terrain>(true) : null;
        if (!t) t = go ? go.GetComponent<Terrain>() : null;
        if (!t) throw new System.InvalidOperationException("ValVertTerrain/Terrain not found in " + ScenePath);
        return t;
    }

    public static void RebuildInScene()
    {
        var sc = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        var t = MainTerrain();
        var tc = t.GetComponent<TerrainCollider>(); if (!tc) L("WARNING: terrain has no TerrainCollider; Built() raycasts see only other colliders");
        Build(t);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(sc);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(sc);
        var root = GameObject.Find(LoadConfig().rootName);
        int mr = 0, subs = 0; long verts = 0;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) { mr++; var m = r.GetComponent<MeshFilter>().sharedMesh; subs += m.subMeshCount; verts += m.vertexCount; }
        L($"SCENE SAVED root={root.name} chunks={mr} submeshes={subs} verts={verts}");
    }

    /// <summary>Renders close-up / eye-level frames of the generated grass (llvmpipe is fine) to /workspace/sys_grass_proof_*.png.</summary>
    public static void ProofShots()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        var cfg = LoadConfig();
        var root = GameObject.Find(cfg.rootName);
        if (!root) throw new System.InvalidOperationException("no grass root; run RebuildInScene first");
        var terr = MainTerrain();
        // pick the densest chunk (lawn) and a sparser one
        MeshFilter best = null; int bestN = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh.vertexCount > bestN) { bestN = mf.sharedMesh.vertexCount; best = mf; }
        var verts = best.sharedMesh.vertices;
        var focus = verts[verts.Length / 2];
        L($"proof chunk {best.name} verts={bestN} focus={focus}");
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) { }
        var camGo = new GameObject("ProofCam"); var cam = camGo.AddComponent<Camera>(); cam.nearClipPlane = 0.05f; cam.farClipPlane = 1500f; cam.fieldOfView = 55f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
        System.Action<string, Vector3, Vector3> shot = (name, pos, look) =>
        {
            cam.transform.position = pos; cam.transform.LookAt(look);
            cam.Render();
            var prev = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes("/workspace/sys_grass_proof_" + name + ".png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            L("shot " + name + " cam=" + pos);
        };
        float gy = terr.SampleHeight(focus) + terr.transform.position.y;
        var f = new Vector3(focus.x, gy + 0.15f, focus.z);
        shot("close", f + new Vector3(0.9f, 0.45f, 0.9f), f);
        shot("mid", f + new Vector3(3.5f, 1.0f, 3.5f), f);
        shot("eye", new Vector3(focus.x + 7f, gy + 1.65f, focus.z + 7f), new Vector3(focus.x, gy + 0.3f, focus.z));
        Object.DestroyImmediate(camGo); rt.Release();
    }
}
