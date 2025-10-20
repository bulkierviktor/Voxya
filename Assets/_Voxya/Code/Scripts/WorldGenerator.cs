using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public PlayerController player;
    public Transform playerTransform;

    public GameObject chunkPrefab;
    public GameObject cityPrefab; // opcional: si null, usamos placeholder

    public int seed = 0;
    public int viewDistanceInChunks = 3; // pruebas (4 en final)
    public int cityCount = 5;
    public int cityMinDistance = 40;
    public int cityMaxDistance = 80;

    private Dictionary<Vector2, Chunk> activeChunks = new Dictionary<Vector2, Chunk>();
    private HashSet<Vector2> chunksRequested = new HashSet<Vector2>();
    private BiomeManager biomeManager;
    private CityManager cityManager;
    private WorldData worldData;

    private Coroutine chunkCoroutine;
    private Transform citiesRoot;

    void Start()
    {
        if (seed == 0)
            seed = Random.Range(int.MinValue + 1, int.MaxValue);

        biomeManager = new BiomeManager(seed);
        cityManager = new CityManager(seed, cityCount, cityMinDistance, cityMaxDistance);
        cityManager.GenerateCities();

        worldData = new WorldData(seed);
        worldData.cities = new List<CityData>(cityManager.cities);

        var citiesGO = GameObject.Find("Cities");
        if (citiesGO == null) citiesGO = new GameObject("Cities");
        citiesRoot = citiesGO.transform;

        chunkCoroutine = StartCoroutine(ChunkUpdateCoroutine());
        StartCoroutine(EnablePlayerAfterWorldGen());
    }

    private IEnumerator ChunkUpdateCoroutine()
    {
        UpdateChunksAroundPlayer();
        while (true)
        {
            yield return new WaitForSeconds(0.2f);
            UpdateChunksAroundPlayer();
        }
    }

    void UpdateChunksAroundPlayer()
    {
        if (playerTransform == null)
        {
            if (player != null && player.transform != null) playerTransform = player.transform;
            else return;
        }

        Vector3 p = playerTransform.position;
        int pcx = Mathf.FloorToInt(p.x / Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(p.z / Chunk.chunkSize);

        int r = viewDistanceInChunks;
        HashSet<Vector2> needed = new HashSet<Vector2>();

        for (int dx = -r; dx <= r; dx++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                needed.Add(coord);

                if (!activeChunks.ContainsKey(coord) && !chunksRequested.Contains(coord))
                {
                    StartCoroutine(CreateChunkRoutine(coord));
                    chunksRequested.Add(coord);
                }
            }
        }

        var toRemove = new List<Vector2>();
        foreach (var kv in activeChunks)
        {
            if (!needed.Contains(kv.Key))
            {
                Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var c in toRemove) activeChunks.Remove(c);
    }

    private IEnumerator CreateChunkRoutine(Vector2 chunkCoord)
    {
        yield return null;

        Vector3 worldPos = new Vector3(chunkCoord.x * Chunk.chunkSize, 0, chunkCoord.y * Chunk.chunkSize);
        GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
        Chunk chunk = go.GetComponent<Chunk>();
        if (chunk != null)
        {
            Biome biome = biomeManager.GetBiomeForChunk((int)chunkCoord.x, (int)chunkCoord.y);
            chunk.Initialize(this, chunkCoord, biome);
            activeChunks[chunkCoord] = chunk;
        }

        // Spawn placeholder SOLO en el centro de la ciudad
        if (cityManager.TryGetCityCenterAtChunk(chunkCoord, out CityData city))
        {
            SpawnCityCenterPlaceholder(city);
        }

        chunksRequested.Remove(chunkCoord);
    }

    private void SpawnCityCenterPlaceholder(CityData city)
    {
        Vector2 wc = city.WorldCenterXZ(Chunk.chunkSize);
        float plateauY = EstimateCityPlateauHeight(city);
        Vector3 pos = new Vector3(wc.x, plateauY + 4f, wc.y); // elevar un poco sobre el suelo

        GameObject go;
        if (cityPrefab != null)
        {
            go = Instantiate(cityPrefab, pos, Quaternion.identity, citiesRoot);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(citiesRoot, true);
            go.transform.position = pos;
            float size = Mathf.Max(Chunk.chunkSize * 0.75f, 8f);
            go.transform.localScale = new Vector3(size, 8f, size);
            go.name = $"City_{city.centerChunk.x}_{city.centerChunk.y}";
        }
    }

    private float EstimateCityPlateauHeight(CityData city)
    {
        Vector2 wc = city.WorldCenterXZ(Chunk.chunkSize);
        return Chunk.GetTerrainHeight(wc.x, wc.y) + 1;
    }

    public Chunk GetChunk(int x, int z)
    {
        activeChunks.TryGetValue(new Vector2(x, z), out Chunk chunk);
        return chunk;
    }

    // Compatibilidad con consultas fuera del chunk local
    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        int worldX = Mathf.FloorToInt(worldPosition.x);
        int worldY = Mathf.FloorToInt(worldPosition.y);
        int worldZ = Mathf.FloorToInt(worldPosition.z);

        if (worldY < 0 || worldY >= 256)
            return BlockType.Air;

        int chunkX = Mathf.FloorToInt(worldX / (float)Chunk.chunkSize);
        int chunkZ = Mathf.FloorToInt(worldZ / (float)Chunk.chunkSize);

        Chunk chunk = GetChunk(chunkX, chunkZ);
        if (chunk == null) return BlockType.Air;

        int localX = worldX - chunkX * Chunk.chunkSize;
        int localZ = worldZ - chunkZ * Chunk.chunkSize;

        return chunk.GetBlock(localX, worldY, localZ);
    }

    // Para Chunk: ¿este (worldX, worldZ) está dentro de alguna ciudad?
    public bool TryGetCityForWorldXZ(float worldX, float worldZ, out CityData city)
    {
        return cityManager.TryGetCityForWorldXZ(worldX, worldZ, Chunk.chunkSize, out city);
    }

    public Biome GetBiomeForChunk(Vector2 chunkCoord)
    {
        return biomeManager.GetBiomeForChunk((int)chunkCoord.x, (int)chunkCoord.y);
    }

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