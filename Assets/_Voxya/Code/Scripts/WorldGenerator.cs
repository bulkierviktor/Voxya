using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public PlayerController player;
    public Transform playerTransform;

    public GameObject chunkPrefab;
    public GameObject cityPrefab; // opcional: si null, usamos placeholder

    // Escala del bloque en unidades de Unity (m). Para “1 bloque paisaje = 11 voxeles pequeños”, usa 1f/11f.
    [Min(0.02f)]
    public float blockSize = 1f / 11f;

    // Terreno definido en METROS (para que no cambie la forma/altura al variar blockSize)
    [Min(1f)] public float terrainMaxHeightMeters = 20f;
    [Min(0.1f)] public float noiseScaleMeters = 25f;

    // Autoajuste del radio de visión por metros objetivo
    [Header("Auto View Distance")]
    public bool autoAdjustViewDistance = true;
    [Min(5f)] public float targetVisibleWidthMeters = 80f;
    public bool enableCityFlattening = true; // toggle de depuración

    public int seed = 0;
    public int viewDistanceInChunks = 6; // sube/ baja según rendimiento
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
        // Aplicar escala global a Chunk
        Chunk.blockSize = Mathf.Max(0.02f, blockSize);
        Chunk.terrainMaxHeightMeters = Mathf.Max(1f, terrainMaxHeightMeters);
        Chunk.noiseScaleMeters = Mathf.Max(0.1f, noiseScaleMeters);

        // Propaga toggle de aplanado
        Chunk.enableCityFlattening = enableCityFlattening;

        // autoajustar viewDistance en función de blockSize y chunkSize
        if (autoAdjustViewDistance)
        {
            float chunkMeters = Chunk.chunkSize * Chunk.blockSize;
            // (2r + 1) * chunkMeters ≈ targetVisibleWidthMeters  =>  r ≈ ((target/chunkMeters) - 1) / 2
            int r = Mathf.Max(1, Mathf.RoundToInt((targetVisibleWidthMeters / Mathf.Max(0.001f, chunkMeters) - 1f) * 0.5f));
            viewDistanceInChunks = Mathf.Clamp(r, 1, 32);
        }

        if (seed == 0)
            seed = UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue);

        biomeManager = new BiomeManager(seed);
        cityManager = new CityManager(seed, cityCount, cityMinDistance, cityMaxDistance);
        cityManager.GenerateCities();

        worldData = new WorldData(seed);
        worldData.cities = new List<CityData>(cityManager.cities);

        var citiesGO = GameObject.Find("Cities");
        if (citiesGO == null) citiesGO = new GameObject("Cities");
        citiesRoot = citiesGO.transform;

        UnityEngine.Debug.Log($"[WorldGenerator] blockSize={Chunk.blockSize:F6}m, chunkMeters={Chunk.chunkSize * Chunk.blockSize:F3}m, targetWidth={targetVisibleWidthMeters}m, viewDistanceInChunks={viewDistanceInChunks}, seed={seed}");

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

        // Posición jugador en BLOQUES (no metros)
        int playerBlockX = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
        int playerBlockZ = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
        int pcx = Mathf.FloorToInt(playerBlockX / (float)Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(playerBlockZ / (float)Chunk.chunkSize);

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

        // Posición del chunk en METROS (unidades de Unity)
        Vector3 worldPos = new Vector3(
            chunkCoord.x * Chunk.chunkSize * Chunk.blockSize,
            0,
            chunkCoord.y * Chunk.chunkSize * Chunk.blockSize
        );

        GameObject go = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
        Chunk chunk = go.GetComponent<Chunk>();
        if (chunk != null)
        {
            Biome biome = biomeManager.GetBiomeForChunk((int)chunkCoord.x, (int)chunkCoord.y);
            chunk.Initialize(this, chunkCoord, biome);
            activeChunks[chunkCoord] = chunk;
        }

        if (cityManager.TryGetCityCenterAtChunk(chunkCoord, out CityData city))
        {
            SpawnCityCenterPlaceholder(city);
        }

        chunksRequested.Remove(chunkCoord);
    }

    private void SpawnCityCenterPlaceholder(CityData city)
    {
        // Centro en BLOQUES → convertir a METROS
        Vector2 centerBlocks = city.WorldCenterXZ(Chunk.chunkSize);
        float centerX = centerBlocks.x * Chunk.blockSize;
        float centerZ = centerBlocks.y * Chunk.blockSize;

        int plateauBlocks = Chunk.GetTerrainHeight(centerBlocks.x, centerBlocks.y);
        float plateauY = plateauBlocks * Chunk.blockSize;

        Vector3 pos = new Vector3(centerX, plateauY + 12f * Chunk.blockSize, centerZ);

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
            float size = Mathf.Max(Chunk.chunkSize * 0.75f * Chunk.blockSize, 12f * Chunk.blockSize);
            go.transform.localScale = new Vector3(size, 12f * Chunk.blockSize, size);
            go.name = $"City_{city.centerChunk.x}_{city.centerChunk.y}";
        }
    }

    public Chunk GetChunk(int x, int z)
    {
        activeChunks.TryGetValue(new Vector2(x, z), out Chunk chunk);
        return chunk;
    }

    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        // METROS → BLOQUES
        int bx = Mathf.FloorToInt(worldPosition.x / Chunk.blockSize);
        int by = Mathf.FloorToInt(worldPosition.y / Chunk.blockSize);
        int bz = Mathf.FloorToInt(worldPosition.z / Chunk.blockSize);

        if (by < 0 || by >= Chunk.chunkHeight)
            return BlockType.Air;

        int chunkX = Mathf.FloorToInt(bx / (float)Chunk.chunkSize);
        int chunkZ = Mathf.FloorToInt(bz / (float)Chunk.chunkSize);

        Chunk chunk = GetChunk(chunkX, chunkZ);
        if (chunk == null) return BlockType.Air;

        int localX = bx - chunkX * Chunk.chunkSize;
        int localZ = bz - chunkZ * Chunk.chunkSize;

        return chunk.GetBlock(localX, by, localZ);
    }

    public bool TryGetCityForWorldXZ(float worldX, float worldZ, out CityData city)
    {
        // CityManager opera en BLOQUES; convierte METROS → BLOQUES para su consulta
        float bx = worldX / Chunk.blockSize;
        float bz = worldZ / Chunk.blockSize;
        return cityManager.TryGetCityForWorldXZ(bx, bz, Chunk.chunkSize, out city);
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
            UnityEngine.Debug.Log("Mundo generado. Controles del jugador activados.");
            player.EnableControls();
        }
    }
}