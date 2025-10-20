using System;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// Serializable world data that stores the seed and city information
/// Can be saved/loaded to persist world generation
/// </summary>
[Serializable]
public class WorldData
{
    public int seed;
    public List<CityData> cities = new List<CityData>();

    public WorldData(int seed)
    {
        this.seed = seed;
    }
}
