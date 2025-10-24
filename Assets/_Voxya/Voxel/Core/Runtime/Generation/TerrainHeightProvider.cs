using UnityEngine;

namespace Voxya.Voxel.Core
{
    public static class TerrainHeightProvider
    {
        // Calcula altura en BLOQUES para una coordenada (en BLOQUES) con dominio en metros
        public static int GetHeightBlocks(float worldXBlocks, float worldZBlocks, VoxelWorldConfig cfg, IBiome biome, IVoxelNoise2D noise)
        {
            float xMeters = worldXBlocks * cfg.BlockSizeMeters;
            float zMeters = worldZBlocks * cfg.BlockSizeMeters;

            // Domain warp ligero
            float wx = xMeters + biome.WarpStrengthMeters * (noise.Sample01(xMeters / biome.WarpScaleMeters, zMeters / biome.WarpScaleMeters) - 0.5f);
            float wz = zMeters + biome.WarpStrengthMeters * (noise.Sample01((xMeters + 123.4f) / biome.WarpScaleMeters, (zMeters + 456.7f) / biome.WarpScaleMeters) - 0.5f);

            float baseScale = Mathf.Max(1f, biome.BaseScaleMeters);
            float h01 = noise.Fbm01(wx / baseScale, wz / baseScale, biome.FbmOctaves, biome.FbmLacunarity, biome.FbmGain);

            int maxBlocks = cfg.TerrainMaxHeightBlocks;
            int hBlocks = Mathf.RoundToInt(h01 * maxBlocks * biome.HeightMul);
            return Mathf.Clamp(hBlocks, 1, cfg.ChunkHeight - 1);
        }
    }
}