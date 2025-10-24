using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Genera columnas de voxeles (Grass/Dirt/Stone) con suavizado local opcional por bioma
    public class DefaultChunkGenerator : IChunkGenerator
    {
        public void GenerateChunkVoxels(ChunkCoord coord, VoxelChunkData chunk, VoxelWorldConfig cfg, IBiomeProvider biomeProvider, IVoxelNoise2D noise)
        {
            int N = chunk.Size;
            int H = chunk.Height;

            // Alturas base
            int[,] heights = new int[N, N];

            for (int lx = 0; lx < N; lx++)
            {
                for (int lz = 0; lz < N; lz++)
                {
                    float worldXBlocks = coord.x * N + lx;
                    float worldZBlocks = coord.z * N + lz;

                    var biome = biomeProvider.GetBiomeAt(worldXBlocks * cfg.BlockSizeMeters, worldZBlocks * cfg.BlockSizeMeters);
                    int h = TerrainHeightProvider.GetHeightBlocks(worldXBlocks, worldZBlocks, cfg, biome, noise);
                    heights[lx, lz] = h;
                }
            }

            // Suavizado por bioma (tomando el bioma en el centro del chunk)
            {
                var centerBiome = biomeProvider.GetBiomeAt(
                    (coord.x * N + N * 0.5f) * cfg.BlockSizeMeters,
                    (coord.z * N + N * 0.5f) * cfg.BlockSizeMeters
                );
                for (int it = 0; it < centerBiome.SmoothIterations; it++)
                {
                    for (int lx = 0; lx < N; lx++)
                    for (int lz = 0; lz < N; lz++)
                    {
                        int sum = 0, cnt = 0;
                        for (int ox = -1; ox <= 1; ox++)
                        for (int oz = -1; oz <= 1; oz++)
                        {
                            int nx = lx + ox, nz = lz + oz;
                            if (nx < 0 || nx >= N || nz < 0 || nz >= N) continue;
                            sum += heights[nx, nz]; cnt++;
                        }
                        int avg = (cnt > 0) ? Mathf.RoundToInt(sum / (float)cnt) : heights[lx, lz];
                        heights[lx, lz] = Mathf.RoundToInt(Mathf.Lerp(heights[lx, lz], avg, centerBiome.SmoothFactor));
                    }
                }
            }

            // Relleno de voxeles por columna
            float half = 4f; // grosor de tierra antes de piedra
            for (int lx = 0; lx < N; lx++)
            {
                for (int lz = 0; lz < N; lz++)
                {
                    int h = heights[lx, lz];
                    for (int y = 0; y < H; y++)
                    {
                        if (y == h - 1) chunk.Set(lx, y, lz, BlockType.Grass);
                        else if (y < h - 1 && y > h - 1 - half) chunk.Set(lx, y, lz, BlockType.Dirt);
                        else if (y <= h - 1 - half) chunk.Set(lx, y, lz, BlockType.Stone);
                        else chunk.Set(lx, y, lz, BlockType.Air);
                    }
                }
            }
        }
    }
}