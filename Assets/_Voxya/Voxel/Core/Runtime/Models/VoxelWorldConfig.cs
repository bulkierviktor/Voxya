using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Config global (ScriptableObject) para tuning sin recompilar
    [CreateAssetMenu(menuName = "Voxya/Voxel/Voxel World Config", fileName = "VoxelWorldConfig")]
    public class VoxelWorldConfig : ScriptableObject
    {
        [Header("Dimensiones")]
        [Min(8)] public int ChunkSize = 16;
        [Min(16)] public int ChunkHeight = 128;
        [Min(0.02f)] public float BlockSizeMeters = 1f / 11f;

        [Header("Mundo")]
        public int Seed = 12345;
        [Min(1)] public int ViewDistanceInChunks = 8;
        [Min(0.05f)] public float UpdateScanInterval = 0.15f;

        [Header("Terreno global")]
        [Min(2f)] public float TerrainMaxHeightMeters = 18f;

        [Header("Streaming")]
        [Min(1)] public int MaxBuildsPerFrame = 4;
        [Min(0)] public int PreloadMargin = 2;
        [Min(0)] public int ColliderDistance = 2;

        [Header("Persistencia")]
        public bool EnableSaveLoad = true;

        public int TerrainMaxHeightBlocks => Mathf.Clamp(Mathf.RoundToInt(TerrainMaxHeightMeters / Mathf.Max(0.0001f, BlockSizeMeters)), 1, ChunkHeight);
    }
}