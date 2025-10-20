using System.Collections.Generic;
using UnityEngine;

public class CityManager
{
    private int seed;
    private System.Random rng;
    private int cityCount;
    private int minDistance;
    private int maxDistance;

    public List<CityData> cities = new List<CityData>();

    public CityManager(int seed, int cityCount = 5, int minDistance = 40, int maxDistance = 80)
    {
        this.seed = seed;
        this.rng = new System.Random(seed);
        this.cityCount = cityCount;
        this.minDistance = minDistance;
        this.maxDistance = maxDistance;
    }

    public void GenerateCities()
    {
        cities.Clear();

        int attemptsLimit = 10000;
        int attempts = 0;

        while (cities.Count < cityCount && attempts < attemptsLimit)
        {
            attempts++;
            CityData candidate = new CityData();

            if (cities.Count == 0)
            {
                // Primera ciudad cerca del spawn: 3..6 chunks
                int firstRadius = rng.Next(3, 7);
                float firstAngle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                candidate.centerChunk = new Vector2(
                    Mathf.RoundToInt(Mathf.Cos(firstAngle) * firstRadius),
                    Mathf.RoundToInt(Mathf.Sin(firstAngle) * firstRadius)
                );
                candidate.radiusChunks = rng.Next(2, 5); // 2..4
            }
            else
            {
                int radius = rng.Next(minDistance, maxDistance + 1);
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
                candidate.centerChunk = new Vector2(
                    Mathf.RoundToInt(Mathf.Cos(angle) * radius),
                    Mathf.RoundToInt(Mathf.Sin(angle) * radius)
                );
                candidate.radiusChunks = rng.Next(2, 5);
            }

            // Verificar distancia mínima entre ciudades
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
}