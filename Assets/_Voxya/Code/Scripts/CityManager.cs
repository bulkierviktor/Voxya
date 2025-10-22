using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class CityManager
{
    private readonly int seed;
    private readonly System.Random rng;
    private readonly int cityCount;
    private readonly int minDistance;
    private readonly int maxDistance;

    private readonly WorldIndex regionIndex; // NUEVO: índice por regiones (opcional)
    private readonly HashSet<Vector2Int> realizedRegions = new HashSet<Vector2Int>(); // regiones ya materializadas

    // Lista pública para que WorldGenerator pueda copiar a WorldData
    public List<CityData> cities = new List<CityData>();

    // Constructor clásico (sin índice) para compatibilidad
    public CityManager(int seed, int cityCount = 5, int minDistance = 40, int maxDistance = 80)
        : this(seed, null, cityCount, minDistance, maxDistance) { }

    // NUEVO: constructor con WorldIndex
    public CityManager(int seed, WorldIndex index, int cityCount = 5, int minDistance = 40, int maxDistance = 80)
    {
        this.seed = seed;
        this.rng = new System.Random(seed);
        this.cityCount = cityCount;
        this.minDistance = minDistance;
        this.maxDistance = maxDistance;
        this.regionIndex = index;
    }

    // Si hay WorldIndex, no pre‑generamos aquí; materializamos on‑demand.
    public void GenerateCities()
    {
        cities.Clear();

        if (regionIndex != null)
        {
            // Con índice por regiones, no generes nada ahora.
            // Se irán añadiendo con EnsureRegionCity(...) al acercarte.
            return;
        }

        int attemptsLimit = 10000;
        int attempts = 0;

        while (cities.Count < cityCount && attempts < attemptsLimit)
        {
            attempts++;

            CityData candidate;

            if (cities.Count == 0)
            {
                // Primera ciudad cerca del spawn: 3..6 chunks
                int firstRadius = rng.Next(3, 7);
                float firstAngle = (float)(rng.NextDouble() * Mathf.PI * 2f);

                Vector2 cc = new Vector2(
                    Mathf.RoundToInt(Mathf.Cos(firstAngle) * firstRadius),
                    Mathf.RoundToInt(Mathf.Sin(firstAngle) * firstRadius)
                );
                int rad = rng.Next(2, 5); // 2..4
                candidate = new CityData(cc, rad);
            }
            else
            {
                int radiusChunksFromOrigin = rng.Next(minDistance, maxDistance + 1);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);

                Vector2 cc = new Vector2(
                    Mathf.RoundToInt(Mathf.Cos(angle) * radiusChunksFromOrigin),
                    Mathf.RoundToInt(Mathf.Sin(angle) * radiusChunksFromOrigin)
                );
                int rad = rng.Next(2, 5);
                candidate = new CityData(cc, rad);
            }

            // Verificar distancia mínima entre ciudades (en chunks)
            bool ok = true;
            foreach (var c in cities)
            {
                if (Vector2.Distance(c.centerChunk, candidate.centerChunk) < minDistance)
                {
                    ok = false; break;
                }
            }

            if (ok) cities.Add(candidate);
        }

        if (cities.Count < cityCount)
        {
            Debug.LogWarning($"CityManager: solo pudo generar {cities.Count}/{cityCount} ciudades (seed {seed}).");
        }
    }

    // ¿Este chunk es el centro de una ciudad?
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

    // ¿Un punto del mundo (x,z) está dentro del radio de alguna ciudad?
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

    // NUEVO: asegura materializar la ciudad de una región (si existe) de forma determinista
    public void EnsureRegionCity(Vector2Int region, int chunkSize)
    {
        if (regionIndex == null) return;              // modo clásico sin índice
        if (realizedRegions.Contains(region)) return; // ya procesada

        var info = regionIndex.GetRegion(region);
        realizedRegions.Add(region);

        if (!info.hasCity) return;

        // Posición de la ciudad en BLOQUES de mundo (determinista)
        Vector2Int cityBlocks = regionIndex.RegionToWorldBlocks(region, info.cityLocalOffsetBlocks);

        // Convertimos a coordenadas de chunk (enteras)
        Vector2 centerChunk = new Vector2(
            Mathf.FloorToInt(cityBlocks.x / (float)chunkSize),
            Mathf.FloorToInt(cityBlocks.y / (float)chunkSize)
        );

        // Radio determinista 2..4 en chunks derivado de (seed, región)
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
            // Mapear a [0,1)
            return (h & 0x00FFFFFF) / (float)0x01000000;
        }
    }
}