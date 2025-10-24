using System.IO;
using UnityEditor;
using UnityEngine;

public static class VoxelCacheTools
{
    [MenuItem("Assets/Create/Voxya/Voxel/Open Chunks Cache Folder")]
    public static void OpenCacheFolder()
    {
        string path = Path.Combine(Application.persistentDataPath, "VoxyaChunks");
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        EditorUtility.RevealInFinder(path);
    }

    [MenuItem("Voxya/Voxel/Clear Chunks Cache")]
    public static void ClearCache()
    {
        string path = Path.Combine(Application.persistentDataPath, "VoxyaChunks");
        if (Directory.Exists(path)) Directory.Delete(path, true);
        Debug.Log("Cleared: " + path);
    }
}