// Val Vert — field flowers generator (architecture: World/Vegetation).
// Flower cards = the authentic Asset Store 138810 grassFlower01..10 (config flowers.cards, GUID-checked).
// The 10 cards are packed into ONE derived atlas (ignored, under Assets/ThirdParty) so each 64 m chunk is a single draw call.
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.IO;

public static class ValVertFlowerField
{
    const string GEN = "Assets/Generated/";

    /// <summary>Packs the pack's flower cards into a derived atlas (area-averaged downscale, aspect kept, bottom-aligned
    /// because plants are rooted at the card's bottom edge). Returns per-card UV rectangles.</summary>
    public static Rect[] BuildAtlas(ValVertGrassField.GrassConfig cfg, out Texture2D atlas)
    {
        var fl = cfg.flowers; int n = fl.cards.Length, cell = fl.atlasCell, cols = fl.atlasCols, pad = fl.atlasPad;
        int rows = (n + cols - 1) / cols; int W = cols * cell, H = rows * cell;
        var px = new Color32[W * H];   // transparent black
        var rects = new Rect[n];
        for (int i = 0; i < n; i++)
        {
            string path = (cfg.pack.localRoot + "/" + fl.cards[i].file).Replace('\\', '/');
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && (!ti.isReadable || ti.textureCompression != TextureImporterCompression.Uncompressed))
            { ti.isReadable = true; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.mipmapEnabled = false; ti.SaveAndReimport(); }
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (src == null) throw new System.InvalidOperationException("flower card failed to load: " + path);
            var sp = src.GetPixels32();
            int inner = cell - 2 * pad;
            float scale = Mathf.Min(inner / (float)src.width, inner / (float)src.height);
            int dw = Mathf.Max(1, Mathf.RoundToInt(src.width * scale)), dh = Mathf.Max(1, Mathf.RoundToInt(src.height * scale));
            int cx = (i % cols) * cell, cy = (i / cols) * cell;
            int ox = cx + pad + (inner - dw) / 2, oy = cy + pad;   // bottom aligned inside the cell
            for (int y = 0; y < dh; y++)
                for (int x = 0; x < dw; x++)
                {
                    // box filter over the source footprint, alpha-weighted colour
                    int x0 = Mathf.FloorToInt(x / scale), x1 = Mathf.Min(src.width, Mathf.Max(x0 + 1, Mathf.CeilToInt((x + 1) / scale)));
                    int y0 = Mathf.FloorToInt(y / scale), y1 = Mathf.Min(src.height, Mathf.Max(y0 + 1, Mathf.CeilToInt((y + 1) / scale)));
                    float r = 0, g = 0, b = 0, a = 0; int cnt = 0;
                    for (int yy = y0; yy < y1; yy++) for (int xx = x0; xx < x1; xx++)
                    { var c = sp[yy * src.width + xx]; float w = c.a; r += c.r * w; g += c.g * w; b += c.b * w; a += c.a; cnt++; }
                    if (a <= 0f) { px[(oy + y) * W + ox + x] = new Color32(0, 0, 0, 0); continue; }
                    px[(oy + y) * W + ox + x] = new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / cnt));
                }
            rects[i] = new Rect(ox / (float)W, oy / (float)H, dw / (float)W, dh / (float)H);
        }
        // bleed opaque colour into transparent texels (avoid dark halos under mip/bilinear sampling)
        for (int pass = 0; pass < 3; pass++)
        {
            var next = (Color32[])px.Clone();
            for (int y = 1; y < H - 1; y++) for (int x = 1; x < W - 1; x++)
            {
                var c = px[y * W + x]; if (c.a != 0 || (c.r | c.g | c.b) != 0) continue;
                for (int d = 0; d < 4; d++)
                {
                    var nb = px[(y + (d == 0 ? 1 : d == 1 ? -1 : 0)) * W + x + (d == 2 ? 1 : d == 3 ? -1 : 0)];
                    if ((nb.r | nb.g | nb.b) != 0) { next[y * W + x] = new Color32(nb.r, nb.g, nb.b, 0); break; }
                }
            }
            px = next;
        }
        var t = new Texture2D(W, H, TextureFormat.RGBA32, false); t.SetPixels32(px); t.Apply();
        string dir = Path.GetDirectoryName(fl.atlasPath).Replace('\\', '/'); ValVertGrassField.EnsureFolder(dir);
        File.WriteAllBytes(fl.atlasPath, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(fl.atlasPath, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(fl.atlasPath);
        imp.textureType = TextureImporterType.Default; imp.sRGBTexture = true; imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = true; imp.mipmapEnabled = true; imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.5f;
        imp.wrapMode = TextureWrapMode.Clamp; imp.filterMode = FilterMode.Bilinear; imp.anisoLevel = 4; imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
        atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(fl.atlasPath);
        return rects;
    }

    public static void Build(Terrain terr)
    {
        var cfg = ValVertGrassField.LoadConfig();
        var errs = ValVertGrassField.Preflight(cfg);
        if (errs.Count > 0) throw new System.InvalidOperationException("ValVertFlowerField preflight failed:\n  " + string.Join("\n  ", errs));
        var fl = cfg.flowers;
        foreach (var existing in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (existing && existing.name == fl.rootName && existing.parent == null) Object.DestroyImmediate(existing.gameObject);

        Physics.SyncTransforms();
        var rects = BuildAtlas(cfg, out var atlas);
        ValVertGrassField.EnsureFolder(cfg.materialFolder);
        ValVertDress.RelocMat(fl.materialName, cfg.materialFolder + "/" + fl.materialName + ".mat");
        var mat = ValVertDress.Mat(fl.materialName, atlas, null, true, Color.white, 0.05f);
        mat.SetFloat("_Cutoff", 0.5f); EditorUtility.SetDirty(mat);

        var ex = ValVertDress.Mask("veg_excl.png"); var fm = ValVertDress.Mask("canopy_orig.png"); var me = ValVertDress.Mask("veg_meadow.png");
        var rnd = new System.Random(fl.seed); System.Func<float> R = () => (float)rnd.NextDouble();
        float tot = 0; foreach (var c in fl.cards) tot += c.weight;
        float CH = fl.chunkSize; var root = new GameObject(fl.rootName);
        long total = 0; int chunks = 0; float tp = terr.transform.position.y;
        for (float cx = -532; cx < 532; cx += CH)
        for (float cz = -354.6f; cz < 354.6f; cz += CH)
        {
            var V = new List<Vector3>(); var N = new List<Vector3>(); var U = new List<Vector2>(); var T = new List<int>();
            for (float x0 = cx; x0 < cx + CH; x0 += fl.spacingMeadow)
            for (float z0 = cz; z0 < cz + CH; z0 += fl.spacingMeadow)
            {
                float px = x0 + R() * fl.spacingMeadow, pz = z0 + R() * fl.spacingMeadow;
                if (px > 531 || pz > 354) continue;
                float m = ValVertDress.S(me, px, pz), f = ValVertDress.S(fm, px, pz);
                bool meadow = m > cfg.meadowThresh;
                // lawn flowers are sparser: thin the dense meadow grid by the spacing ratio
                float keep = meadow ? fl.probMeadow : fl.probPark * (fl.spacingMeadow * fl.spacingMeadow) / (fl.spacingPark * fl.spacingPark);
                if (f > cfg.canopyThresh) continue;                       // no flowers under closed canopy
                if (R() > keep) continue;
                if (ValVertDress.S(ex, px, pz) > cfg.exclThreshold || ValVertWoods.Lab(px, pz) != 0 || ValVertWoods.Hidden(new Vector3(px, 0, pz))) continue;
                if (ValVertGrassField.Built(px, pz)) continue;
                float pick = R() * tot; int card = fl.cards.Length - 1;
                for (int ci = 0; ci < fl.cards.Length; ci++) { pick -= fl.cards[ci].weight; if (pick <= 0f) { card = ci; break; } }
                var rc = rects[card];
                float hgt = fl.height[0] + R() * (fl.height[1] - fl.height[0]);
                float aspect = (rc.width * atlas.width) / (rc.height * atlas.height);   // card pixel aspect (w/h)
                float w = hgt * aspect * (fl.widthFactor[0] + R() * (fl.widthFactor[1] - fl.widthFactor[0]));
                float y = terr.SampleHeight(new Vector3(px, 0, pz)) + tp - 0.03f;
                float a0 = R() * Mathf.PI;
                for (int q = 0; q < 2; q++)
                {
                    float a = a0 + q * Mathf.PI / 2f;
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * w * 0.5f; var b = new Vector3(px, y, pz); int k = V.Count;
                    V.Add(b - d); V.Add(b - d + Vector3.up * hgt); V.Add(b + d + Vector3.up * hgt); V.Add(b + d);
                    for (int r = 0; r < 4; r++) N.Add(Vector3.up);
                    U.Add(new Vector2(rc.xMin, rc.yMin)); U.Add(new Vector2(rc.xMin, rc.yMax)); U.Add(new Vector2(rc.xMax, rc.yMax)); U.Add(new Vector2(rc.xMax, rc.yMin));
                    T.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                }
            }
            if (V.Count == 0) continue;
            var assetPath = GEN + fl.meshAssetPrefix + chunks + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath); bool created = mesh == null;
            if (created) mesh = new Mesh { indexFormat = IndexFormat.UInt32 }; else { mesh.Clear(); mesh.indexFormat = IndexFormat.UInt32; }
            mesh.SetVertices(V); mesh.SetNormals(N); mesh.SetUVs(0, U); mesh.SetTriangles(T, 0); mesh.RecalculateBounds();
            if (created) AssetDatabase.CreateAsset(mesh, assetPath); else EditorUtility.SetDirty(mesh);
            var go = new GameObject(fl.meshAssetPrefix + chunks); go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = ShadowCastingMode.Off;
            var lg = go.AddComponent<LODGroup>(); lg.SetLODs(new[] { new LOD(0.04f, new Renderer[] { mr }) }); lg.RecalculateBounds();
            total += V.Count / 8; chunks++;
        }
        AssetDatabase.SaveAssets();
        ValVertGrassField.L($"flowers cards={fl.cards.Length} clumps={total} chunks={chunks}");
    }
}
