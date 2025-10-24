using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CreateVoxelAtlas2x3
{
    [MenuItem("Voxya/Voxel/Create Voxel Atlas (2x3)")]
    public static void Create()
    {
        const int cell = 512;              // tamaño de celda
        int width = cell * 2;              // 2 columnas
        int height = cell * 3;             // 3 filas

        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.filterMode = FilterMode.Point;

        // Colores por celda (fila 0 = inferior)
        // (x,y): (0,0)=Snow, (1,0)=Stone, (0,1)=Sand, (1,1)=Dirt, (0,2)=Grass, (1,2)=Extra
        Color snow  = new Color(0.95f, 0.97f, 1.00f, 1f);
        Color stone = new Color(0.55f, 0.56f, 0.58f, 1f);
        Color sand  = new Color(0.93f, 0.87f, 0.58f, 1f);
        Color dirt  = new Color(0.49f, 0.34f, 0.20f, 1f);
        Color grass = new Color(0.49f, 0.80f, 0.28f, 1f);
        Color extra = new Color(0.40f, 0.40f, 0.90f, 1f);

        FillCell(tex, 0, 0, cell, snow);
        FillCell(tex, 1, 0, cell, stone);
        FillCell(tex, 0, 1, cell, sand);
        FillCell(tex, 1, 1, cell, dirt);
        FillCell(tex, 0, 2, cell, grass);
        FillCell(tex, 1, 2, cell, extra);

        tex.Apply(false, false);

        // Guardar en Assets/_Voxya/Voxel/...
        string texDir = "Assets/_Voxya/Voxel/Textures";
        string matDir = "Assets/_Voxya/Voxel/Materials";
        Directory.CreateDirectory(texDir);
        Directory.CreateDirectory(matDir);

        string texPath = Path.Combine(texDir, "VoxelAtlas_2x3.png");
        File.WriteAllBytes(texPath, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);

        // Import settings
        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.filterMode = FilterMode.Point;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.sRGBTexture = true;
        ti.mipmapEnabled = false; // evita bleeding en celdas pequeñas
        ti.SaveAndReimport();

        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // Crea material según pipeline
        bool isSRP = GraphicsSettings.currentRenderPipeline != null;
        Shader sh = Shader.Find(isSRP ? "Universal Render Pipeline/Lit" : "Standard");
        var mat = new Material(sh) { enableInstancing = true };

        if (isSRP) mat.SetTexture("_BaseMap", atlas);
        else       mat.SetTexture("_MainTex", atlas);

        // Opcional: parámetros para aspecto “mate”
        if (isSRP)
        {
            mat.SetFloat("_Smoothness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
        }
        else
        {
            mat.SetFloat("_Glossiness", 0.05f);
            mat.SetFloat("_Metallic", 0f);
        }

        string matPath = Path.Combine(matDir, "VoxelChunk-Mat.mat");
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Voxel Atlas",
            $"Atlas y material creados:\n{texPath}\n{matPath}\n\nAsigna el material al ChunkPrefab.",
            "OK");
    }

    private static void FillCell(Texture2D tex, int cx, int cy, int cell, Color color)
    {
        int x0 = cx * cell;
        int y0 = cy * cell;
        for (int y = 0; y < cell; y++)
        for (int x = 0; x < cell; x++)
            tex.SetPixel(x0 + x, y0 + y, color);
    }
}