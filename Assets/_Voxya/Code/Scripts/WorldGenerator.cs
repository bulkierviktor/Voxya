using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    // Player references
    public PlayerController player;
    public Transform playerTransform; // Player transform for streaming chunks

    // Streaming and world generation parameters
    public int seed = 12345; // Seed for deterministic world generation
    public int viewDistanceInChunks = 3; // Number of chunks to render around player (default 3, can be increased to 4)
    
    // City generation parameters
    public int cityCount = 5;
    public float minCityDistance = 40f; // In chunks
    public float maxCityDistance = 80f; // In chunks
    public int minCityRadius = 2; // In chunks
    public int maxCityRadius = 4; // In chunks
    public GameObject cityPrefab; // Optional: if not set, placeholder will be used
    
    // Legacy parameter (kept for compatibility but not used for full world generation)
    public int worldSizeInChunks = 10;
    public GameObject chunkPrefab;
    public Dictionary<Vector2, Chunk> chunkObjects = new Dictionary<Vector2, Chunk>();
    
    // Managers for biomes and cities
    private BiomeManager biomeManager;
    private CityManager cityManager;
    private WorldData worldData;
    
    // Active chunks tracking
    private Dictionary<Vector2, Chunk> activeChunks = new Dictionary<Vector2, Chunk>();
    private Vector2 lastPlayerChunkPosition = new Vector2(float.MaxValue, float.MaxValue);
    
    // Parent objects for organization
    private GameObject chunksParent;
    private GameObject citiesParent;
    
    // Throttling for chunk updates
    private float updateInterval = 0.2f; // Update chunks every 0.2 seconds
    private Coroutine chunkUpdateCoroutine;

    void Start()
    {
        // Initialize world data and managers
        worldData = new WorldData(seed);
        biomeManager = new BiomeManager(seed);
        cityManager = new CityManager(worldData, cityCount, minCityDistance, maxCityDistance, minCityRadius, maxCityRadius);
        
        // Create parent objects for organization
        chunksParent = new GameObject("Chunks");
        citiesParent = new GameObject("Cities");
        
        // If playerTransform is not set, try to get it from player
        if (playerTransform == null && player != null)
        {
            playerTransform = player.transform;
        }
        
        // Enable player controls immediately for streaming (no need to wait)
        if (player != null)
        {
            player.EnableControls();
            Debug.Log("Player controls enabled. Streaming chunks around player.");
        }
        
        // Start chunk update coroutine
        chunkUpdateCoroutine = StartCoroutine(ChunkUpdateCoroutine());
    }

    /// <summary>
    /// Coroutine that periodically updates chunks around the player
    /// Runs every 0.2 seconds to check if player has moved to a new chunk
    /// </summary>
    private IEnumerator ChunkUpdateCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(updateInterval);
            
            if (playerTransform != null)
            {
                UpdateChunksAroundPlayer();
            }
        }
    }

    /// <summary>
    /// Updates chunks around the player's current position
    /// Loads chunks within view distance and unloads distant chunks
    /// </summary>
    private void UpdateChunksAroundPlayer()
    {
        // Get player's current chunk position
        Vector2 playerChunkPos = new Vector2(
            Mathf.FloorToInt(playerTransform.position.x / Chunk.chunkSize),
            Mathf.FloorToInt(playerTransform.position.z / Chunk.chunkSize)
        );
        
        // Only update if player has moved to a different chunk
        if (playerChunkPos == lastPlayerChunkPosition)
        {
            return;
        }
        
        lastPlayerChunkPosition = playerChunkPos;
        
        // Determine which chunks should be active
        HashSet<Vector2> chunksToKeep = new HashSet<Vector2>();
        
        for (int x = -viewDistanceInChunks; x <= viewDistanceInChunks; x++)
        {
            for (int z = -viewDistanceInChunks; z <= viewDistanceInChunks; z++)
            {
                Vector2 chunkPos = new Vector2(
                    playerChunkPos.x + x,
                    playerChunkPos.y + z
                );
                chunksToKeep.Add(chunkPos);
                
                // Create chunk if it doesn't exist
                if (!activeChunks.ContainsKey(chunkPos))
                {
                    StartCoroutine(CreateChunkRoutine(chunkPos));
                }
            }
        }
        
        // Remove chunks that are too far away
        List<Vector2> chunksToRemove = new List<Vector2>();
        foreach (var kvp in activeChunks)
        {
            if (!chunksToKeep.Contains(kvp.Key))
            {
                chunksToRemove.Add(kvp.Key);
            }
        }
        
        foreach (Vector2 chunkPos in chunksToRemove)
        {
            if (activeChunks.TryGetValue(chunkPos, out Chunk chunk))
            {
                Destroy(chunk.gameObject);
                activeChunks.Remove(chunkPos);
                chunkObjects.Remove(chunkPos); // Also remove from legacy dictionary
            }
        }
    }

    /// <summary>
    /// Creates a single chunk at the specified position
    /// Yields one frame to spread chunk generation over time (1 chunk per frame)
    /// Determines biome and spawns city if applicable
    /// </summary>
    private IEnumerator CreateChunkRoutine(Vector2 chunkPosition)
    {
        // Yield one frame to throttle chunk creation
        yield return null;
        
        // Determine biome for this chunk
        Biome biome = biomeManager.GetBiomeAtChunk(chunkPosition);
        
        // Instantiate chunk
        Vector3 worldPos = new Vector3(
            chunkPosition.x * Chunk.chunkSize,
            0,
            chunkPosition.y * Chunk.chunkSize
        );
        
        GameObject newChunkObject = Instantiate(chunkPrefab, worldPos, Quaternion.identity, chunksParent.transform);
        newChunkObject.name = $"Chunk_{chunkPosition.x}_{chunkPosition.y}";
        
        Chunk newChunk = newChunkObject.GetComponent<Chunk>();
        if (newChunk != null)
        {
            // Initialize chunk with biome
            newChunk.Initialize(this, chunkPosition, biome);
            
            // Add to active chunks
            activeChunks.Add(chunkPosition, newChunk);
            chunkObjects.Add(chunkPosition, newChunk); // Also add to legacy dictionary
            
            // Check if this chunk should have a city
            CityData cityAtChunk = cityManager.GetCityAtChunk(chunkPosition);
            if (cityAtChunk != null)
            {
                // Check if this is the city center
                CityData cityCenter = cityManager.GetCityCenterAtChunk(chunkPosition);
                if (cityCenter != null)
                {
                    SpawnCity(cityCenter, worldPos);
                }
            }
        }
    }

    /// <summary>
    /// Spawns a city at the given position
    /// If cityPrefab is assigned, instantiates it; otherwise creates a placeholder cube
    /// </summary>
    private void SpawnCity(CityData city, Vector3 worldPosition)
    {
        if (cityPrefab != null)
        {
            // Instantiate city prefab
            GameObject cityObject = Instantiate(cityPrefab, worldPosition, Quaternion.identity, citiesParent.transform);
            cityObject.name = $"City_{city.position.x}_{city.position.y}";
            Debug.Log($"Spawned city prefab at {city.position} with radius {city.radius}");
        }
        else
        {
            // Create placeholder cube
            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.transform.position = worldPosition + new Vector3(Chunk.chunkSize / 2f, 10f, Chunk.chunkSize / 2f);
            placeholder.transform.localScale = new Vector3(10f, 20f, 10f);
            placeholder.transform.parent = citiesParent.transform;
            placeholder.name = $"CityPlaceholder_{city.position.x}_{city.position.y}";
            
            // Add a distinctive color
            Renderer renderer = placeholder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.8f, 0.6f, 0.2f); // Gold/yellow color
            }
            
            Debug.Log($"Spawned city placeholder at {city.position} with radius {city.radius}");
        }
    }

    /// <summary>
    /// Gets chunk at specific coordinates (legacy compatibility)
    /// </summary>
    public Chunk GetChunk(int x, int z)
    {
        chunkObjects.TryGetValue(new Vector2(x, z), out Chunk chunk);
        return chunk;
    }

    /// <summary>
    /// Gets block at world position (used by chunks for neighbor checking)
    /// </summary>
    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        int worldX = Mathf.FloorToInt(worldPosition.x);
        int worldY = Mathf.FloorToInt(worldPosition.y);
        int worldZ = Mathf.FloorToInt(worldPosition.z);

        if (worldY < 0 || worldY >= Chunk.chunkHeight)
        {
            return BlockType.Air;
        }

        int chunkX = Mathf.FloorToInt(worldX / (float)Chunk.chunkSize);
        int chunkZ = Mathf.FloorToInt(worldZ / (float)Chunk.chunkSize);

        Chunk chunk = GetChunk(chunkX, chunkZ);

        if (chunk == null)
        {
            return BlockType.Air;
        }

        int localX = worldX - chunkX * Chunk.chunkSize;
        int localZ = worldZ - chunkZ * Chunk.chunkSize;

        return chunk.GetBlock(localX, worldY, localZ);
    }
    
    /// <summary>
    /// Legacy coroutine - kept for compatibility but now enables controls immediately
    /// </summary>
    private IEnumerator EnablePlayerAfterWorldGen()
    {
        yield return new WaitForEndOfFrame();
        if (player != null)
        {
            player.EnableControls();
            Debug.Log("Mundo generado. Controles del jugador activados.");
        }
    }
}
