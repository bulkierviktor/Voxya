using System;
using System.Collections.Generic;

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