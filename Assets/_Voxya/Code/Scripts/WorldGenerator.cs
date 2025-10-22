using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldGenerator : MonoBehaviour
{
    private static WorldGenerator instance;

    // WorldGenerator.cs (campos)
    public WorldIndex RegionIndex { get; private set; }

    public PlayerController player;
    public Transform playerTransform;

    public GameObject chunkPrefab;
    public GameObject cityPrefab;

    [Min(0.02f)] public float blockSize = 1f / 11f;
    [Min(1f)] public float terrainMaxHeightMeters = 20f;
    [Min(0.1f)] public float noiseScaleMeters = 25f;

    public int seed = 0;

    public int viewDistanceInChunks = 6;

    public bool autoAdjustViewDistance = true;
    [Min(5f)] public float targetVisibleWidthMeters = 80f;

    public int cityCount = 5;
    public int cityMinDistance = 120;
    public int cityMaxDistance = 240;
    public bool enableCityFlattening = true;

    [Header("Streaming control")]
    [Min(1)] public int maxChunksCreatedPerFrame = 2;
    [Min(0)] public int preloadMarginChunks = 2;
    [Min(0)] public int colliderDistanceInChunks = 2;
    public float updateScanInterval = 0.2f;

    [Header("Spawn prep")]
    public bool waitSpawnAreaReady = true;
    [Min(0)] public int spawnEnsureRadiusChunks = 1;    // 1 = 3x3 ; 2 = 5x5
    [Min(0f)] public float spawnYOffsetBlocks = 2f;
    [Min(0.1f)] public float spawnCheckInterval = 0.05f;
    [Min(0.5f)] public float spawnTimeoutSeconds = 6f;

    private Dictionary<Vector2, Chunk> activeChunks = new Dictionary<Vector2, Chunk>();
    private HashSet<Vector2> enqueued = new HashSet<Vector2>();
    private Queue<Vector2> toCreate = new Queue<Vector2>();
    private Stack<GameObject> pool = new Stack<GameObject>();
    private HashSet<Vector2> lastNeeded = new HashSet<Vector2>();

    private BiomeManager biomeManager;
    private CityManager cityManager;
    private WorldData worldData;

    private Coroutine scanCoroutine;
    private Coroutine buildWorker;
    private Transform citiesRoot;

    private Rigidbody playerRb;
    private bool rbGravityWasEnabled = true;
    private bool rbKinematicWasEnabled = false;
    private CharacterController playerCc;
    private bool ccWasEnabled = true;
    private bool playerControllerWasEnabled = true;

    void OnValidate()
    {
        Chunk.blockSize = Mathf.Max(0.02f, blockSize);
        Chunk.terrainMaxHeightMeters = Mathf.Max(1f, terrainMaxHeightMeters);
        Chunk.noiseScaleMeters = Mathf.Max(0.1f, noiseScaleMeters);
        Chunk.enableCityFlattening = enableCityFlattening;
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            UnityEngine.Debug.LogWarning($"[WorldGenerator] Otra instancia detectada en '{name}'. Deshabilitando este componente.");
            enabled = false;
            return;
        }
        instance = this;
    }

    void Start()
    {
        Chunk.blockSize = Mathf.Max(0.02f, blockSize);
        Chunk.terrainMaxHeightMeters = Mathf.Max(1f, terrainMaxHeightMeters);
        Chunk.noiseScaleMeters = Mathf.Max(0.1f, noiseScaleMeters);
        Chunk.enableCityFlattening = enableCityFlattening;

        if (autoAdjustViewDistance)
        {
            float chunkMeters = Chunk.chunkSize * Chunk.blockSize;
            int r = Mathf.Max(1, Mathf.RoundToInt((targetVisibleWidthMeters / Mathf.Max(0.001f, chunkMeters) - 1f) * 0.5f));
            viewDistanceInChunks = Mathf.Clamp(r, 1, 48);
        }

        if (seed == 0)
            seed = UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue);

        // Inicializa managers
        biomeManager = new BiomeManager(seed);

        // Primero crea el índice por regiones (usa la misma seed)
        int regionSize = Chunk.chunkSize * 4;
        RegionIndex = new WorldIndex(seed, regionSize);

        // Ahora crea CityManager conectado al índice
        cityManager = new CityManager(seed, RegionIndex, cityCount, cityMinDistance, cityMaxDistance);
        cityManager.GenerateCities();

        worldData = new WorldData(seed) { cities = new List<CityData>(cityManager.cities) };

        var citiesGO = GameObject.Find("Cities");
        if (citiesGO == null) citiesGO = new GameObject("Cities");
        citiesRoot = citiesGO.transform;

        UnityEngine.Debug.Log(
            $"[WorldGenerator] blockSize={Chunk.blockSize:F6}m, chunkMeters={Chunk.chunkSize * Chunk.blockSize:F3}m, " +
            $"targetWidth={targetVisibleWidthMeters}m, viewDistanceInChunks={viewDistanceInChunks}, " +
            $"enableCityFlattening={enableCityFlattening}, cities={cityManager.cities.Count}, seed={seed}");

        colliderDistanceInChunks = Mathf.Max(colliderDistanceInChunks, spawnEnsureRadiusChunks);

        buildWorker = StartCoroutine(ChunkBuildWorker());
        scanCoroutine = StartCoroutine(ScanLoop());

        StartCoroutine(PrepareSpawnAndEnablePlayer());
    }

    public WorldIndex.RegionInfo GetRegionInfoAtWorld(float worldX, float worldZ)
    {
        int bx = Mathf.FloorToInt(worldX / Chunk.blockSize);
        int bz = Mathf.FloorToInt(worldZ / Chunk.blockSize);
        Vector2Int reg = RegionIndex.WorldBlocksToRegion(new Vector2Int(bx, bz));
        return RegionIndex.GetRegion(reg);
    }

    private IEnumerator ScanLoop()
    {
        UpdateChunksAroundPlayer(prewarm: true);
        while (true)
        {
            yield return new WaitForSeconds(updateScanInterval);
            UpdateChunksAroundPlayer(prewarm: false);
        }
    }

    void UpdateChunksAroundPlayer(bool prewarm)
    {
        if (playerTransform == null)
        {
            if (player != null && player.transform != null) playerTransform = player.transform;
            else return;
        }

        int playerBlockX = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
        int playerBlockZ = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
        int pcx = Mathf.FloorToInt(playerBlockX / (float)Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(playerBlockZ / (float)Chunk.chunkSize);

        int r = viewDistanceInChunks + (prewarm ? preloadMarginChunks : 0);

        // 1) construir el conjunto de necesarios
        HashSet<Vector2> needed = new HashSet<Vector2>();
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                needed.Add(coord);
            }
        }

        // 2) devolver a pool los que sobran
        foreach (var kv in activeChunks)
        {
            if (!needed.Contains(kv.Key))
                PoolChunk(kv.Key, kv.Value);
        }
        foreach (var old in new List<Vector2>(activeChunks.Keys))
            if (!needed.Contains(old)) activeChunks.Remove(old);

        // 3) encolar NUEVOS coords en orden radial (de dentro hacia fuera)
        EnqueueNewCoordsRadial(needed, pcx, pcz);

        // 4) colliders solo cerca del jugador
        foreach (var kv in activeChunks)
        {
            float dx = Mathf.Abs(kv.Key.x - pcx);
            float dz = Mathf.Abs(kv.Key.y - pcz);
            bool near = (dx <= colliderDistanceInChunks) && (dz <= colliderDistanceInChunks);
            kv.Value.SetColliderEnabled(near);
        }

        lastNeeded = needed;
    }

    private IEnumerator ChunkBuildWorker()
    {
        while (true)
        {
            int built = 0;
            while (built < maxChunksCreatedPerFrame && toCreate.Count > 0)
            {
                var coord = toCreate.Dequeue();
                CreateOrReuseChunk(coord);
                enqueued.Remove(coord);
                built++;
                yield return null;
            }
            yield return null;
        }
    }

    private void CreateOrReuseChunk(Vector2 chunkCoord)
    {
        Vector3 worldPos = new Vector3(
            chunkCoord.x * Chunk.chunkSize * Chunk.blockSize,
            0,
            chunkCoord.y * Chunk.chunkSize * Chunk.blockSize
        );

        // Materializa ciudades deterministas de la(s) región(es) que cubre este chunk
        if (RegionIndex != null && cityManager != null)
        {
            // Región del chunk actual (usa el origen del chunk en bloques)
            int chunkBlockX = Mathf.FloorToInt(chunkCoord.x * Chunk.chunkSize);
            int chunkBlockZ = Mathf.FloorToInt(chunkCoord.y * Chunk.chunkSize);
            Vector2Int region = RegionIndex.WorldBlocksToRegion(new Vector2Int(chunkBlockX, chunkBlockZ));

            // Asegura la región del chunk y vecinas (3x3) para evitar popping
            for (int rx = -1; rx <= 1; rx++)
            {
                for (int rz = -1; rz <= 1; rz++)
                {
                    cityManager.EnsureRegionCity(new Vector2Int(region.x + rx, region.y + rz), Chunk.chunkSize);
                }
            }
        }

        GameObject go;
        Chunk chunk;

        if (pool.Count > 0)
        {
            go = pool.Pop();
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.identity;
            go.SetActive(true);
            chunk = go.GetComponent<Chunk>();
        }
        else
        {
            go = Instantiate(chunkPrefab, worldPos, Quaternion.identity);
            chunk = go.GetComponent<Chunk>();
        }

        if (chunk != null)
        {
            Biome biome = biomeManager.GetBiomeForChunk((int)chunkCoord.x, (int)chunkCoord.y);
            chunk.Initialize(this, chunkCoord, biome);
            activeChunks[chunkCoord] = chunk;

            bool withCol = false;
            if (playerTransform != null)
            {
                int pcx = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize / Chunk.chunkSize);
                int pcz = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize / Chunk.chunkSize);
                float dx = Mathf.Abs(chunkCoord.x - pcx);
                float dz = Mathf.Abs(chunkCoord.y - pcz);
                withCol = (dx <= colliderDistanceInChunks) && (dz <= colliderDistanceInChunks);
            }
            chunk.SetColliderEnabled(withCol);
        }

        if (cityManager.TryGetCityCenterAtChunk(chunkCoord, out CityData city))
            SpawnCityCenterPlaceholder(city);
    }

    private void PoolChunk(Vector2 key, Chunk chunk)
    {
        if (chunk == null) return;
        var go = chunk.gameObject;
        chunk.SetColliderEnabled(false);
        go.SetActive(false);
        pool.Push(go);
    }

    private void SpawnCityCenterPlaceholder(CityData city)
    {
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
        float bx = worldX / Chunk.blockSize;
        float bz = worldZ / Chunk.blockSize;
        return cityManager.TryGetCityForWorldXZ(bx, bz, Chunk.chunkSize, out city);
    }

    public Biome GetBiomeForChunk(Vector2 chunkCoord)
    {
        return biomeManager.GetBiomeForChunk((int)chunkCoord.x, (int)chunkCoord.y);
    }

    // ==========================
    // Spawn prep
    // ==========================
    private IEnumerator PrepareSpawnAndEnablePlayer()
    {
        if (playerTransform == null && player != null) playerTransform = player.transform;

        if (player != null)
        {
            playerControllerWasEnabled = player.enabled;
            player.enabled = false;
        }

        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                rbGravityWasEnabled = playerRb.useGravity;
                rbKinematicWasEnabled = playerRb.isKinematic;
                playerRb.useGravity = false;
                playerRb.isKinematic = true;
                playerRb.Velocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }

            playerCc = playerTransform.GetComponent<CharacterController>();
            if (playerCc != null)
            {
                ccWasEnabled = playerCc.enabled;
                playerCc.enabled = false;
            }
        }

        // 1) Construcción SIN COLA de todo el área de spawn (garantiza colliders)
        ForceEnsureSpawnArea(spawnEnsureRadiusChunks);

        // 2) Espera opcional a que todo esté listo
        if (waitSpawnAreaReady && playerTransform != null)
        {
            float t0 = Time.realtimeSinceStartup;
            while (!IsSpawnAreaReady(spawnEnsureRadiusChunks, requireCenterCollider: true))
            {
                if (Time.realtimeSinceStartup - t0 > spawnTimeoutSeconds)
                {
                    UnityEngine.Debug.LogWarning("[WorldGenerator] Spawn timeout: continuando sin área completa.");
                    break;
                }
                yield return new WaitForSeconds(spawnCheckInterval);
            }
        }

        // 3) Colocar con precisión vía CapsuleCast (respeta height/radius/skin)
        if (playerTransform != null)
        {
            // Asegura colliders activos alrededor
            EnsureCollidersAround(spawnEnsureRadiusChunks);
            yield return new WaitForFixedUpdate();

            // Datos del CharacterController (si existe)
            float radius = 0.5f;
            float height = 2f;
            float skin = 0.05f;
            Vector3 centerOffset = Vector3.zero;

            if (playerCc != null)
            {
                radius = playerCc.radius;
                height = Mathf.Max(playerCc.height, radius * 2f + 0.01f);
                skin = Mathf.Max(0.01f, playerCc.skinWidth);
                centerOffset = playerCc.center;
            }

            // Punto de partida alto
            Vector3 worldCenter = playerTransform.position + centerOffset;
            Vector3 castStart = worldCenter + Vector3.up * (5f * Chunk.blockSize + height);

            // Definir extremos de la cápsula relativa al centro
            float half = height * 0.5f - radius;
            Vector3 top = castStart + Vector3.up * half;
            Vector3 bottom = castStart - Vector3.up * half;

            RaycastHit hit;
            float castDist = 200f;

            if (Physics.CapsuleCast(top, bottom, radius, Vector3.down, out hit, castDist, ~0, QueryTriggerInteraction.Ignore))
            {
                float feetY = hit.point.y + spawnYOffsetBlocks * Chunk.blockSize;
                // Queremos que la base de la cápsula quede justo sobre el suelo + skin
                float centerY = feetY + radius + (height * 0.5f - radius) + skin; // = feet + halfHeight + skin

                Vector3 p = playerTransform.position;
                p.y = centerY - centerOffset.y; // corrige el offset del CC
                playerTransform.position = p;
            }
            else
            {
                // Fallback: usa mapa de alturas
                int bx = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
                int bz = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
                int groundBlocks = Chunk.GetTerrainHeight(bx, bz);
                float feetY = groundBlocks * Chunk.blockSize + spawnYOffsetBlocks * Chunk.blockSize;
                float centerY = feetY + radius + (height * 0.5f - radius) + skin;

                Vector3 p = playerTransform.position;
                p.y = centerY - centerOffset.y;
                playerTransform.position = p;
            }

            Physics.SyncTransforms();
        }

        // 4) Restaurar componentes y controles
        if (playerCc != null) playerCc.enabled = ccWasEnabled;
        if (playerRb != null)
        {
            playerRb.isKinematic = rbKinematicWasEnabled;
            playerRb.useGravity = rbGravityWasEnabled;
            playerRb.Velocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
        if (player != null)
        {
            player.enabled = playerControllerWasEnabled;
            player.EnableControls();
        }

        UnityEngine.Debug.Log("[WorldGenerator] Spawn preparado. Controles del jugador activados.");
    }

    private void ForceEnsureSpawnArea(int radius)
    {
        if (playerTransform == null) return;

        int playerBlockX = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
        int playerBlockZ = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
        int pcx = Mathf.FloorToInt(playerBlockX / (float)Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(playerBlockZ / (float)Chunk.chunkSize);

        // Construir INMEDIATAMENTE todos los chunks del radio (no encolamos)
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                if (!activeChunks.ContainsKey(coord))
                {
                    CreateOrReuseChunk(coord);
                }
                else
                {
                    // Si ya existe, asegura collider encendido
                    activeChunks[coord].SetColliderEnabled(true);
                }
            }
        }
    }

    private bool IsSpawnAreaReady(int radius, bool requireCenterCollider)
    {
        if (playerTransform == null) return true;

        int playerBlockX = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
        int playerBlockZ = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
        int pcx = Mathf.FloorToInt(playerBlockX / (float)Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(playerBlockZ / (float)Chunk.chunkSize);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                if (!activeChunks.ContainsKey(coord))
                    return false;
            }
        }

        if (requireCenterCollider)
        {
            Vector2 center = new Vector2(pcx, pcz);
            if (activeChunks.TryGetValue(center, out var ch))
            {
                var mc = ch.GetComponent<MeshCollider>();
                if (mc == null || !mc.enabled || mc.sharedMesh == null)
                    return false;
            }
            else return false;
        }

        return true;
    }

    private void EnsureCollidersAround(int radius)
    {
        if (playerTransform == null) return;

        int playerBlockX = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
        int playerBlockZ = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);
        int pcx = Mathf.FloorToInt(playerBlockX / (float)Chunk.chunkSize);
        int pcz = Mathf.FloorToInt(playerBlockZ / (float)Chunk.chunkSize);

        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                if (activeChunks.TryGetValue(coord, out var ch))
                    ch.SetColliderEnabled(true);
            }
        }
    }

    // Encola coords ordenados por distancia al centro(pcx, pcz)
private void EnqueueNewCoordsRadial(HashSet<Vector2> needed, int pcx, int pcz)
    {
        // candidatos = los que aún no están activos ni encolados
        var candidates = new List<Vector2>();
        foreach (var coord in needed)
        {
            if (!activeChunks.ContainsKey(coord) && !enqueued.Contains(coord))
                candidates.Add(coord);
        }

        // orden radial (distancia al centro), con desempate estable
        candidates.Sort((a, b) =>
        {
            float dax = a.x - pcx, daz = a.y - pcz;
            float dbx = b.x - pcx, dbz = b.y - pcz;
            float da2 = dax * dax + daz * daz;
            float db2 = dbx * dbx + dbz * dbz;
            int cmp = da2.CompareTo(db2);
            if (cmp != 0) return cmp;
            // desempate consistente por X y luego Z
            cmp = a.x.CompareTo(b.x);
            if (cmp != 0) return cmp;
            return a.y.CompareTo(b.y);
        });

        // encola en orden radial
        foreach (var c in candidates)
        {
            enqueued.Add(c);
            toCreate.Enqueue(c);
        }
    }
}