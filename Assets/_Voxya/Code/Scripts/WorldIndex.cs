using System;
using System.Collections.Generic;
using UnityEngine;

// Índice determinista por "regiones" (no voxeles) para saber
// bioma y POIs a gran distancia sin generar mallas.
public sealed class WorldIndex
{
    // Tamaño de región en bloques (independiente del chunkSize).
    // Ej.: 64 significa 4x4 chunks si chunkSize=16.
    public readonly int regionSizeBlocks;

    private readonly int seed;
    private readonly Dictionary<Vector2Int, RegionInfo> cache = new();

    public WorldIndex(int worldSeed, int regionSizeBlocks = 64)
    {
        seed = worldSeed;
        this.regionSizeBlocks = Mathf.Max(8, regionSizeBlocks);
    }

    public RegionInfo GetRegion(Vector2Int r)
    {
        if (cache.TryGetValue(r, out var info)) return info;

        // Semilla determinista por región
        ulong h = Hash2D((ulong)seed, (ulong)r.x, (ulong)r.y);
        var rng = new Rng64(h);

        // Bioma simple por ahora (puedes usar ruido 2D aquí también)
        Biome biome = PickBiome(rng.Next01());

        // Presencia de ciudad con prob. por bioma (ejemplo)
        float pCity = biome switch
        {
            Biome.Plains => 0.12f,
            Biome.Forest => 0.09f,
            Biome.Hills => 0.06f,
            Biome.Desert => 0.05f,
            Biome.Snow => 0.04f,
            _ => 0.06f
        };

        bool hasCity = rng.Next01() < pCity;

        // Offset local dentro de la región (en bloques). Evita bordes.
        int margin = 6;
        int max = regionSizeBlocks - margin;
        int cx = margin + (int)(rng.Next01() * (max - margin));
        int cz = margin + (int)(rng.Next01() * (max - margin));

        info = new RegionInfo
        {
            region = r,
            biome = biome,
            hasCity = hasCity,
            cityLocalOffsetBlocks = new Vector2Int(cx, cz)
        };
        cache[r] = info;
        return info;
    }

    public Vector2Int WorldBlocksToRegion(Vector2Int worldBlocks)
    {
        int rx = FloorDiv(worldBlocks.x, regionSizeBlocks);
        int rz = FloorDiv(worldBlocks.y, regionSizeBlocks);
        return new Vector2Int(rx, rz);
    }

    public Vector2Int RegionToWorldBlocks(Vector2Int region, Vector2Int localOffset)
    {
        return new Vector2Int(
            region.x * regionSizeBlocks + localOffset.x,
            region.y * regionSizeBlocks + localOffset.y
        );
    }

    private static int FloorDiv(int a, int b)
    {
        int q = a / b;
        int r = a % b;
        return (r != 0 && ((r < 0) ^ (b < 0))) ? q - 1 : q;
    }

    private static Biome PickBiome(float t01)
    {
        // Distribución simple; sustituye por ruido/lookup si quieres bandas.
        if (t01 < 0.22f) return Biome.Plains;
        if (t01 < 0.44f) return Biome.Forest;
        if (t01 < 0.62f) return Biome.Hills;
        if (t01 < 0.80f) return Biome.Desert;
        return Biome.Snow;
    }

    // Hash+RNG deterministas
    private static ulong Hash2D(ulong seed, ulong x, ulong z)
    {
        // SplitMix64 combinado
        ulong h = seed ^ (x + 0x9E3779B97F4A7C15UL);
        h = SplitMix64(h);
        h ^= (z + 0xBF58476D1CE4E5B9UL);
        h = SplitMix64(h);
        return h;
    }

    private static ulong SplitMix64(ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        x = (x ^ (x >> 30)) * 0xBF58476D1CE4E5B9UL;
        x = (x ^ (x >> 27)) * 0x94D049BB133111EBUL;
        return x ^ (x >> 31);
    }

    private struct Rng64
    {
        private ulong s;
        public Rng64(ulong seed) { s = seed; }
        public ulong NextU64() { s = SplitMix64(s); return s; }
        public float Next01() => (NextU64() >> 8) * (1.0f / (1UL << 56)); // [0,1)
    }

    public struct RegionInfo
    {
        public Vector2Int region;
        public Biome biome;
        public bool hasCity;
        public Vector2Int cityLocalOffsetBlocks;
    }
}