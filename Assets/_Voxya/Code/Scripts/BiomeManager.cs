using UnityEngine;

public class BiomeManager
{
    private int seed;
    private float perlinScale = 0.08f;

    public BiomeManager(int seed)
    {
        this.seed = seed;
    }

    // Devuelve un Biome basado en distancia en chunks + ruido Perlin
    public Biome GetBiomeForChunk(int chunkX, int chunkZ)
    {
        // Distancia al origen en chunks
        float distance = new Vector2(chunkX, chunkZ).magnitude;

        // Base por distancia: progresión de dificultad
        Biome baseBiome;
        if (distance < 6f) baseBiome = Biome.Plains;
        else if (distance < 14f) baseBiome = Biome.Hills;
        else baseBiome = Biome.Snow;

        // Ruido para insertar parches
        float n = Mathf.PerlinNoise((chunkX + seed) * perlinScale, (chunkZ + seed) * perlinScale);

        if (n > 0.78f) return Biome.Desert;
        if (n > 0.62f) return Biome.Forest;

        if (distance >= 6f && distance < 14f) return Biome.Hills;

        return baseBiome;
    }
}