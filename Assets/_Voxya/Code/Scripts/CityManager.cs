using System.Collections.Generic;
// using System.Diagnostics; // QUITADO para evitar ambigüedad con Debug
using UnityEngine;

public class CityManager
{
    private readonly int seed;
    private readonly System.Random rng;
    private readonly int cityCount;
    private readonly int minDistance;
    private readonly int maxDistance;

    private readonly WorldIndex regionIndex; // índice por regiones (opcional)
    private readonly HashSet<Vector2Int> realizedRegions = new HashSet<Vector2Int>(); // regiones materializadas

    public List<CityData> cities = new List<CityData>();

    public CityManager(int seed, int cityCount = 5, int minDistance = 40, int maxDistance = 80)
        : this(seed, null, cityCount, minDistance, maxDistance) { }

    public CityManager(int seed, WorldIndex index, int cityCount = 5, int minDistance = 40, int maxDistance = 80)
    {
        this.seed = seed;
        this.rng = new System.Random(seed);
        this.cityCount = cityCount;
        this.minDistance = minDistance;
        this.maxDistance = maxDistance;
        this.regionIndex = index;
    }

    // NUEVO: 5 ciudades deterministas en anillos crecientes (en coordenadas de CHUNK)
    // - radios en chunks: 12, 36, 64, 96, 140 (creciente y con buen espaciado)
    // - ángulos deterministas por seed bien repartidos
    public void GenerateCities()
    {
        cities.Clear();
        realizedRegions.Clear();

        int[] radiiChunks = new int[] { 12, 36, 64, 96, 140 };
        if (cityCount != radiiChunks.Length)
        {
            // Ajusta si el inspector pide otro número
            radiiChunks = new int[cityCount];
            int baseR = 12;
            int step = 24;
            for (int i = 0; i < cityCount; i++)
                radiiChunks[i] = baseR + step * i + (i >= 3 ? (i - 2) * 10 : 0); // crece un poco más a partir de la 4ª
        }

        // ángulos repartidos alrededor del círculo, con ligera variación por seed
        float golden = 2.399963229728653f; // golden angle en radianes
        for (int i = 0; i < radiiChunks.Length; i++)
        {
            float baseAngle = i * golden; // bien repartido
            float jitter = ((float)rng.NextDouble() - 0.5f) * 0.35f; // pequeña variación
            float angle = baseAngle + jitter;

            int r = radiiChunks[i];
            Vector2 c = new Vector2(
                Mathf.RoundToInt(Mathf.Cos(angle) * r),
                Mathf.RoundToInt(Mathf.Sin(angle) * r)
            );

            // Radio propio de la ciudad en chunks (2..4)
            int radiusChunks = 2 + (i % 3); // 2,3,4,2,3...
            cities.Add(new CityData(c, radiusChunks));
        }
    }

    public bool TryGetCityCenterAtChunk(Vector2 chunkCoord, out CityData city)
    {
        foreach (var c in cities)
        {
            if (c.centerChunk == chunkCoord)
            {
                city = c;
                return true;
            }
        }
        city = null;
        return false;
    }

    public bool TryGetCityForWorldXZ(float worldX, float worldZ, int chunkSize, out CityData city)
    {
        foreach (var c in cities)
        {
            Vector2 wc = c.WorldCenterXZ(chunkSize);
            float dist = Vector2.Distance(new Vector2(worldX, worldZ), wc);
            float radiusWorld = c.radiusChunks * chunkSize;
            if (dist <= radiusWorld)
            {
                city = c;
                return true;
            }
        }
        city = null;
        return false;
    }

    public void EnsureRegionCity(Vector2Int region, int chunkSize)
    {
        if (regionIndex == null) return;
        if (realizedRegions.Contains(region)) return;

        var info = regionIndex.GetRegion(region);
        realizedRegions.Add(region);
        if (!info.hasCity) return;

        Vector2Int cityBlocks = regionIndex.RegionToWorldBlocks(region, info.cityLocalOffsetBlocks);

        Vector2 centerChunk = new Vector2(
            Mathf.FloorToInt(cityBlocks.x / (float)chunkSize),
            Mathf.FloorToInt(cityBlocks.y / (float)chunkSize)
        );

        int radiusChunks = 2 + (int)(Deterministic01(seed, region.x, region.y) * 3f);
        radiusChunks = Mathf.Clamp(radiusChunks, 2, 4);

        cities.Add(new CityData(centerChunk, radiusChunks));
    }

    private static float Deterministic01(int seed, int rx, int rz)
    {
        unchecked
        {
            uint h = (uint)seed;
            h ^= (uint)(rx * 374761393);
            h = (h << 5) | (h >> 27);
            h ^= (uint)(rz * 668265263);
            h *= 2246822519u;
            return (h & 0x00FFFFFF) / (float)0x01000000;
        }
    }
}