using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CityManager handles the generation and placement of cities in the world
/// Generates city positions deterministically based on seed
/// Ensures minimum separation between cities and places first city near spawn
/// </summary>
public class CityManager
{
    private WorldData worldData;
    private int cityCount;
    private float minCityDistance; // In chunks
    private float maxCityDistance; // In chunks
    private int minCityRadius;
    private int maxCityRadius;
    
    public CityManager(WorldData worldData, int cityCount = 5, 
                      float minCityDistance = 40f, float maxCityDistance = 80f,
                      int minCityRadius = 2, int maxCityRadius = 4)
    {
        this.worldData = worldData;
        this.cityCount = cityCount;
        this.minCityDistance = minCityDistance;
        this.maxCityDistance = maxCityDistance;
        this.minCityRadius = minCityRadius;
        this.maxCityRadius = maxCityRadius;
        
        GenerateCities();
    }

    /// <summary>
    /// Generates city positions deterministically based on seed
    /// First city is placed 3-6 chunks from origin
    /// Subsequent cities maintain minimum separation
    /// </summary>
    private void GenerateCities()
    {
        // Use seed for deterministic random generation
        Random.InitState(worldData.seed);
        
        // Generate first city near spawn (3-6 chunks from origin)
        float firstCityDistance = Random.Range(3f, 6f);
        float firstCityAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 firstCityPos = new Vector2(
            Mathf.Cos(firstCityAngle) * firstCityDistance,
            Mathf.Sin(firstCityAngle) * firstCityDistance
        );
        int firstCityRadius = Random.Range(minCityRadius, maxCityRadius + 1);
        worldData.cities.Add(new CityData(firstCityPos, firstCityRadius));
        
        // Generate remaining cities
        int attempts = 0;
        int maxAttempts = cityCount * 100; // Prevent infinite loops
        
        while (worldData.cities.Count < cityCount && attempts < maxAttempts)
        {
            attempts++;
            
            // Generate random position within distance range
            float distance = Random.Range(minCityDistance, maxCityDistance);
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 candidatePos = new Vector2(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance
            );
            
            // Check if position maintains minimum distance from all existing cities
            bool validPosition = true;
            foreach (CityData existingCity in worldData.cities)
            {
                float distanceBetween = Vector2.Distance(candidatePos, existingCity.position);
                if (distanceBetween < minCityDistance)
                {
                    validPosition = false;
                    break;
                }
            }
            
            if (validPosition)
            {
                int radius = Random.Range(minCityRadius, maxCityRadius + 1);
                worldData.cities.Add(new CityData(candidatePos, radius));
            }
        }
        
        Debug.Log($"Generated {worldData.cities.Count} cities with seed {worldData.seed}");
    }

    /// <summary>
    /// Checks if a chunk position is within any city's radius
    /// Returns the CityData if inside a city, null otherwise
    /// </summary>
    public CityData GetCityAtChunk(Vector2 chunkPosition)
    {
        foreach (CityData city in worldData.cities)
        {
            float distance = Vector2.Distance(chunkPosition, city.position);
            if (distance <= city.radius)
            {
                return city;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if a chunk position is the center of a city
    /// </summary>
    public CityData GetCityCenterAtChunk(Vector2 chunkPosition)
    {
        foreach (CityData city in worldData.cities)
        {
            // Check if chunk is the city center (rounded to nearest chunk)
            Vector2 centerChunk = new Vector2(
                Mathf.RoundToInt(city.position.x),
                Mathf.RoundToInt(city.position.y)
            );
            
            if (Vector2.Distance(chunkPosition, centerChunk) < 0.5f)
            {
                return city;
            }
        }
        return null;
    }

    public List<CityData> GetAllCities()
    {
        return worldData.cities;
    }
}
