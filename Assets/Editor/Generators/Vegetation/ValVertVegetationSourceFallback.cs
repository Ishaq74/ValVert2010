using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reconstructs the legacy, external vegetation masks from the committed terrain's painted alphamap.
/// This is a deterministic local fallback for clones without the original VV_WORK/clean source bundle:
/// road/path exclusion, canopy-vs-open-ground, meadow-vs-forest-floor, and the four painted label classes.
/// It does not modify the terrain or the scene. Original clean/ files always take precedence.
/// </summary>
public static class ValVertVegetationSourceFallback
{
    const string TerrainPath = "Assets/World/Terrain/ValVertTerrain.asset";
    const int LabelResolution = 2049;

    public static void GenerateFromTerrainBatch()
    {
        try
        {
            var generated = GenerateFromTerrain(ValVertPaths.Work);
            Debug.Log("[VV][VegetationInputs] TERRAIN_DERIVED generated=" + string.Join(",", generated));
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[VV][VegetationInputs] FAILED: " + ex);
            EditorApplication.Exit(1);
        }
    }

    public static List<string> GenerateFromTerrain(string workDirectory)
    {
        var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainPath);
        if (!terrainData) throw new FileNotFoundException("Committed terrain data not found", TerrainPath);
        if (terrainData.alphamapLayers < 8)
            throw new InvalidDataException($"Expected 8 painted terrain layers, got {terrainData.alphamapLayers}");

        int a = terrainData.alphamapResolution;
        var alpha = terrainData.GetAlphamaps(0, 0, a, a);
        var exclusion = new byte[a * a];
        var canopy = new byte[a * a];
        var meadow = new byte[a * a];
        for (int y = 0; y < a; y++)
        for (int x = 0; x < a; x++)
        {
            float c0 = alpha[y, x, 0];
            float c1 = alpha[y, x, 1];
            float sum = c0 + c1;
            int label = Label(alpha[y, x, 4], alpha[y, x, 5], alpha[y, x, 6], alpha[y, x, 7]);
            int i = y * a + x;
            exclusion[i] = (byte)(label != 0 || alpha[y, x, 2] + alpha[y, x, 3] > 0.3f ? 255 : 0);
            canopy[i] = (byte)(label == 0 && sum > 0.01f && c1 / sum > 0.5f ? 255 : 0);
            meadow[i] = (byte)(label == 0 && sum > 0.01f && c0 / sum > 0.5f ? 255 : 0);
        }

        string clean = Path.Combine(workDirectory, "clean");
        Directory.CreateDirectory(clean);
        var generated = new List<string>();
        WritePngIfMissing(clean, "veg_excl.png", a, exclusion, generated);
        WritePngIfMissing(clean, "canopy_orig.png", a, canopy, generated);
        WritePngIfMissing(clean, "veg_meadow.png", a, meadow, generated);
        WriteLabelIfMissing(clean, alpha, a, generated);
        WriteMeshesIfMissing(clean, generated);
        if (generated.Count > 0)
        {
            File.WriteAllText(Path.Combine(clean, "vegetation_masks_provenance.json"),
                "{\n  \"method\": \"terrain-alphamap-fallback-v1\",\n" +
                "  \"source\": \"Assets/World/Terrain/ValVertTerrain.asset\",\n" +
                "  \"note\": \"Derived from committed painted terrain layers; not the original external raster masks.\",\n" +
                "  \"generated\": [\"" + string.Join("\", \"", generated) + "\"]\n}\n");
        }
        return generated;
    }

    static int Label(float c4, float c5, float c6, float c7)
    {
        if (c4 > 0.5f) return 1;
        if (c5 > 0.5f) return 2;
        if (c6 > 0.5f) return 3;
        if (c7 > 0.5f) return 4;
        return 0;
    }

    static void WritePngIfMissing(string directory, string name, int size, byte[] gray, List<string> generated)
    {
        string path = Path.Combine(directory, name);
        if (File.Exists(path)) return;
        var pixels = new Color32[gray.Length];
        for (int i = 0; i < gray.Length; i++) pixels[i] = new Color32(gray[i], gray[i], gray[i], 255);
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        try
        {
            texture.SetPixels32(pixels);
            // EncodeToPNG requires a readable CPU copy until after encoding.
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            generated.Add(name);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    static void WriteLabelIfMissing(string clean, float[,,] alpha, int resolution, List<string> generated)
    {
        string path = Path.Combine(clean, "label_v3.raw");
        if (File.Exists(path)) return;
        var labels = new byte[LabelResolution * LabelResolution];
        for (int y = 0; y < LabelResolution; y++)
        for (int x = 0; x < LabelResolution; x++)
        {
            int sx = Mathf.RoundToInt(x * (resolution - 1f) / (LabelResolution - 1f));
            int sy = Mathf.RoundToInt(y * (resolution - 1f) / (LabelResolution - 1f));
            labels[y * LabelResolution + x] = (byte)Label(alpha[sy, sx, 4], alpha[sy, sx, 5], alpha[sy, sx, 6], alpha[sy, sx, 7]);
        }
        File.WriteAllBytes(path, labels);
        generated.Add("label_v3.raw");
    }

    static void WriteMeshesIfMissing(string clean, List<string> generated)
    {
        string path = Path.Combine(clean, "woods_meshes.json");
        if (File.Exists(path)) return;
        File.WriteAllText(path, "{\"items\":[],\"trees\":[],\"hide\":[],\"hideLen\":[]}\n");
        generated.Add("woods_meshes.json");
    }
}
