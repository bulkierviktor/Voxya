using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WorldGenerator : MonoBehaviour
{
    // Singleton para evitar duplicados sin usar APIs obsoletas
    private static WorldGenerator instance;

    public PlayerController player;
    public Transform playerTransform;

    public GameObject chunkPrefab;
    public GameObject cityPrefab; // opcional: si null, usamos placeholder

    [Min(0.02f)]
    public float blockSize = 1f / 11f;

    [Min(1f)] public float terrainMaxHeightMeters = 20f;
    [Min(0.1f)] public float noiseScaleMeters = 25f;

    public int seed = 0;

    // Radio visible (en chunks)
    public int viewDistanceInChunks = 6;

    // Autoajuste por metros visibles
    public bool autoAdjustViewDistance = true;
    [Min(5f)] public float targetVisibleWidthMeters = 80f;

    // Generación y ciudades
    public int cityCount = 5;
    public int cityMinDistance = 120;
    public int cityMaxDistance = 240;
    public bool enableCityFlattening = true;

    // Alisado de spikes
    [Header("Streaming control")]
    [Min(1)] public int maxChunksCreatedPerFrame = 2;
    [Min(0)] public int preloadMarginChunks = 2;           // anillos extra al inicio
    [Min(0)] public int colliderDistanceInChunks = 2;      // solo colliders cerca del jugador
    public float updateScanInterval = 0.2f;                // frecuencia de escaneo

    // Preparación del spawn
    [Header("Spawn prep")]
    public bool waitSpawnAreaReady = true;
    [Min(0)] public int spawnEnsureRadiusChunks = 1;       // asegura 3x3 alrededor
    [Min(0f)] public float spawnYOffsetBlocks = 2f;        // bloques por encima del terreno
    [Min(0.1f)] public float spawnCheckInterval = 0.05f;   // comprobación periódica
    [Min(0.5f)] public float spawnTimeoutSeconds = 6f;     // seguridad

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

    // Estado temporal de componentes del jugador
    private Rigidbody playerRb;
    private bool rbGravityWasEnabled = true;
    private bool rbKinematicWasEnabled = false;
    private CharacterController playerCc;
    private bool ccWasEnabled = true;
    private bool playerControllerWasEnabled = true;

    void OnValidate()
    {
        // Propaga cambios del Inspector incluso en modo edición
        Chunk.blockSize = Mathf.Max(0.02f, blockSize);
        Chunk.terrainMaxHeightMeters = Mathf.Max(1f, terrainMaxHeightMeters);
        Chunk.noiseScaleMeters = Mathf.Max(0.1f, noiseScaleMeters);
        Chunk.enableCityFlattening = enableCityFlattening;
    }

    void Awake()
    {
        // Singleton sin llamadas obsoletas
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
        // Aplicar escala global a Chunk
        Chunk.blockSize = Mathf.Max(0.02f, blockSize);
        Chunk.terrainMaxHeightMeters = Mathf.Max(1f, terrainMaxHeightMeters);
        Chunk.noiseScaleMeters = Mathf.Max(0.1f, noiseScaleMeters);
        Chunk.enableCityFlattening = enableCityFlattening;

        // Autoajustar viewDistance según metros deseados
        if (autoAdjustViewDistance)
        {
            float chunkMeters = Chunk.chunkSize * Chunk.blockSize;
            int r = Mathf.Max(1, Mathf.RoundToInt((targetVisibleWidthMeters / Mathf.Max(0.001f, chunkMeters) - 1f) * 0.5f));
            viewDistanceInChunks = Mathf.Clamp(r, 1, 48);
        }

        if (seed == 0)
            seed = UnityEngine.Random.Range(int.MinValue + 1, int.MaxValue);

        biomeManager = new BiomeManager(seed);
        cityManager = new CityManager(seed, cityCount, cityMinDistance, cityMaxDistance);
        cityManager.GenerateCities();

        worldData = new WorldData(seed) { cities = new List<CityData>(cityManager.cities) };

        var citiesGO = GameObject.Find("Cities");
        if (citiesGO == null) citiesGO = new GameObject("Cities");
        citiesRoot = citiesGO.transform;

        UnityEngine.Debug.Log(
            $"[WorldGenerator] blockSize={Chunk.blockSize:F6}m, chunkMeters={Chunk.chunkSize * Chunk.blockSize:F3}m, " +
            $"targetWidth={targetVisibleWidthMeters}m, viewDistanceInChunks={viewDistanceInChunks}, " +
            $"enableCityFlattening={enableCityFlattening}, cities={cityManager.cities.Count}, seed={seed}");

        // Asegurar que el radio de colliders cubre el radio de spawn
        colliderDistanceInChunks = Mathf.Max(colliderDistanceInChunks, spawnEnsureRadiusChunks);

        // Lanzar worker (creación repartida) y escaneo periódico
        buildWorker = StartCoroutine(ChunkBuildWorker());
        scanCoroutine = StartCoroutine(ScanLoop());

        // Preparar spawn y habilitar jugador cuando el suelo esté listo
        StartCoroutine(PrepareSpawnAndEnablePlayer());
    }

    private IEnumerator ScanLoop()
    {
        // Primer escaneo con precarga
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

        HashSet<Vector2> needed = new HashSet<Vector2>();
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dz = -r; dz <= r; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                needed.Add(coord);
                if (!activeChunks.ContainsKey(coord) && !enqueued.Contains(coord))
                {
                    enqueued.Add(coord);
                    toCreate.Enqueue(coord);
                }
            }
        }

        // Devolver a pool los que ya no se necesitan
        foreach (var kv in activeChunks)
        {
            if (!needed.Contains(kv.Key))
            {
                PoolChunk(kv.Key, kv.Value);
            }
        }
        foreach (var old in new List<Vector2>(activeChunks.Keys))
            if (!needed.Contains(old)) activeChunks.Remove(old);

        // Colliders solo cerca del jugador
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
                yield return null; // repartir carga
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

            // Colliders cerca del jugador
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
        // Capturar transform y componentes de física/control
        if (playerTransform == null && player != null) playerTransform = player.transform;

        if (player != null)
        {
            playerControllerWasEnabled = player.enabled;
            player.enabled = false; // desactivar lógica del jugador durante el spawn
        }

        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                rbGravityWasEnabled = playerRb.useGravity;
                rbKinematicWasEnabled = playerRb.isKinematic;
                playerRb.useGravity = false;
                playerRb.isKinematic = true; // evita cualquier caída
                playerRb.linearVelocity = Vector3.zero;
                playerRb.angularVelocity = Vector3.zero;
            }

            playerCc = playerTransform.GetComponent<CharacterController>();
            if (playerCc != null)
            {
                ccWasEnabled = playerCc.enabled;
                playerCc.enabled = false; // evita que CharacterController aplique movimientos/gravedad
            }
        }

        // Forzar encolado del área de spawn y construir el chunk central de inmediato
        ForceEnsureSpawnArea(spawnEnsureRadiusChunks);

        // Esperar a que el área esté lista (incluyendo collider en chunk central)
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

        // Posicionar jugador sobre el terreno
        if (playerTransform != null)
        {
            int bx = Mathf.FloorToInt(playerTransform.position.x / Chunk.blockSize);
            int bz = Mathf.FloorToInt(playerTransform.position.z / Chunk.blockSize);

            // Altura estimada por ruido (en bloques -> metros)
            int groundBlocks = Chunk.GetTerrainHeight(bx, bz);
            float groundY = groundBlocks * Chunk.blockSize;

            // Intentar raycast para obtener suelo real si el collider ya está
            EnsureCollidersAround(spawnEnsureRadiusChunks);
            yield return new WaitForFixedUpdate(); // dejar registrar colliders en física

            float feetYOffsetMeters = spawnYOffsetBlocks * Chunk.blockSize;
            float feetY = groundY + feetYOffsetMeters;

            RaycastHit hit;
            Vector3 from = new Vector3(playerTransform.position.x, groundY + 10f * Chunk.blockSize, playerTransform.position.z);
            if (Physics.Raycast(from, Vector3.down, out hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                feetY = hit.point.y + feetYOffsetMeters;
            }

            float centerY = feetY;
            var cc = playerCc; // CharacterController si existe
            if (cc != null)
            {
                // Transform.position es el CENTRO de la cápsula
                centerY = feetY + (cc.height * 0.5f) + cc.skinWidth;
            }
            else
            {
                // Fallback si no hay CharacterController: intenta usar bounds del collider
                float halfHeight = 1f; // valor por defecto 1 m
                var col = playerTransform.GetComponent<Collider>();
                if (col != null) halfHeight = col.bounds.extents.y;
                centerY = feetY + halfHeight + 0.01f;
            }

            Vector3 p = playerTransform.position;
            p.y = centerY;
            playerTransform.position = p;
            Physics.SyncTransforms();
        }

        // Restaurar componentes y controles
        if (playerCc != null) playerCc.enabled = ccWasEnabled;
        if (playerRb != null)
        {
            playerRb.isKinematic = rbKinematicWasEnabled;
            playerRb.useGravity = rbGravityWasEnabled;
            playerRb.linearVelocity = Vector3.zero;
            playerRb.angularVelocity = Vector3.zero;
        }
        if (player != null)
        {
            // Reactiva tu script y sus entradas
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

        // Encolar todos los chunks del radio
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dz = -radius; dz <= radius; dz++)
            {
                Vector2 coord = new Vector2(pcx + dx, pcz + dz);
                if (!activeChunks.ContainsKey(coord) && !enqueued.Contains(coord))
                {
                    enqueued.Add(coord);
                    toCreate.Enqueue(coord);
                }
            }
        }

        // Construir inmediatamente el chunk central si aún no existe (evita caída)
        Vector2 center = new Vector2(pcx, pcz);
        if (!activeChunks.ContainsKey(center))
        {
            CreateOrReuseChunk(center);
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
}