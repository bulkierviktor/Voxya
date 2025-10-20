using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stores data for a single city in the world
/// </summary>
[Serializable]
public class CityData
{
    public Vector2 position; // Chunk coordinates (x, z)
    public int radius; // Radius in chunks

    public CityData(Vector2 position, int radius)
    {
        this.position = position;
        this.radius = radius;
    }
}

/// <summary>
/// Serializable world data that stores the seed and city information
/// Can be saved/loaded to persist world generation
/// </summary>
[Serializable]
public class WorldData
{
    public int seed;
    public List<CityData> cities;

    public WorldData(int seed)
    {
        this.seed = seed;
        this.cities = new List<CityData>();
    }
}
