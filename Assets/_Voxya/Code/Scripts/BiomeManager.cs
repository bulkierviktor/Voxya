using UnityEngine;

/// <summary>
/// BiomeManager determines which biome a chunk belongs to based on its coordinates
/// Uses Perlin noise and distance calculations for biome distribution
/// </summary>
public class BiomeManager
{
    private int seed;
    private float biomeScale = 0.05f; // Controls how spread out biomes are

    public BiomeManager(int seed)
    {
        this.seed = seed;
    }

    /// <summary>
    /// Determines the biome for a given chunk position
    /// Uses Perlin noise with seed offset for deterministic biome generation
    /// </summary>
    public Biome GetBiomeAtChunk(Vector2 chunkPosition)
    {
        // Use seed to offset noise sampling for deterministic results
        float noiseX = (chunkPosition.x + seed * 0.1f) * biomeScale;
        float noiseZ = (chunkPosition.y + seed * 0.1f) * biomeScale;
        
        // Sample two noise values for more varied biome distribution
        float noise1 = Mathf.PerlinNoise(noiseX, noiseZ);
        float noise2 = Mathf.PerlinNoise(noiseX + 100f, noiseZ + 100f);
        
        // Combine noise values
        float combinedNoise = (noise1 + noise2) / 2f;
        
        // Distance from origin influences biome (spawn area tends to be plains)
        float distanceFromOrigin = chunkPosition.magnitude;
        
        // Map noise and distance to biome types
        // Near spawn (0-10 chunks): favor Plains
        if (distanceFromOrigin < 10f)
        {
            if (combinedNoise < 0.6f) return Biome.Plains;
            else if (combinedNoise < 0.8f) return Biome.Forest;
            else return Biome.Hills;
        }
        
        // Further out: full biome variety
        if (combinedNoise < 0.2f) return Biome.Desert;
        else if (combinedNoise < 0.4f) return Biome.Plains;
        else if (combinedNoise < 0.6f) return Biome.Forest;
        else if (combinedNoise < 0.8f) return Biome.Hills;
        else return Biome.Snow;
    }

    /// <summary>
    /// Returns a height multiplier for the given biome
    /// Used to vary terrain height based on biome type
    /// </summary>
    public static float GetBiomeHeightMultiplier(Biome biome)
    {
        switch (biome)
        {
            case Biome.Plains:
                return 0.8f; // Flatter terrain
            case Biome.Hills:
                return 1.5f; // Hillier terrain
            case Biome.Desert:
                return 0.9f; // Slightly varied terrain
            case Biome.Snow:
                return 1.3f; // Mountainous terrain
            case Biome.Forest:
                return 1.0f; // Normal terrain
            default:
                return 1.0f;
        }
    }
}
