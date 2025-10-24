using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Voxya.Voxel.Core;

namespace Voxya.Voxel.Unity
{
    // Streaming radial con prioridad “front-first”, generación y meshing en background
    public class VoxelWorld : MonoBehaviour
    {
        [Header("Config")]
        public VoxelWorldConfig config;
        public Transform player;
        public GameObject chunkPrefab;

        // Core
        private IVoxelNoise2D noise;
        private IBiomeProvider biomeProvider;
        private IChunkGenerator generator;
        private IMesher mesher;
        private IChunkStorage storage;

        // Estado
        private readonly Dictionary<(int,int), VoxelChunk> active = new();
        private readonly HashSet<(int,int)> scheduled = new();
        private readonly ConcurrentQueue<(ChunkCoord coord, MeshData mesh)> readyMeshes = new();

        private float scanTimer;

        void Start()
        {
            if (config == null) { Debug.LogError("VoxelWorld: Config no asignado."); enabled = false; return; }
            if (player == null) { Debug.LogError("VoxelWorld: Player no asignado."); enabled = false; return; }
            if (chunkPrefab == null) { Debug.LogError("VoxelWorld: chunkPrefab no asignado."); enabled = false; return; }

            // Construir core
            noise = new OpenSimplex2D();
            noise.SetSeed(config.Seed);

            biomeProvider = new DefaultBiomeProvider(config.Seed);
            generator = new DefaultChunkGenerator();
            mesher = new GreedyMesher();
            storage = new BinaryChunkStorage();

            scanTimer = 0f;
        }

        void Update()
        {
            scanTimer += Time.deltaTime;
            if (scanTimer >= config.UpdateScanInterval)
            {
                scanTimer = 0f;
                ScanAndSchedule();
            }

            // Aplicar meshes listos
            int applied = 0;
            while (applied < config.MaxBuildsPerFrame && readyMeshes.TryDequeue(out var item))
            {
                ApplyMesh(item.coord, item.mesh);
                applied++;
            }
        }

        private void ScanAndSchedule()
        {
            int pcx = Mathf.FloorToInt(player.position.x / (config.BlockSizeMeters * config.ChunkSize));
            int pcz = Mathf.FloorToInt(player.position.z / (config.BlockSizeMeters * config.ChunkSize));

            int r = config.ViewDistanceInChunks + config.PreloadMargin;

            // Necesarios
            var needed = new HashSet<(int,int)>();
            for (int dx = -r; dx <= r; dx++)
            for (int dz = -r; dz <= r; dz++)
                needed.Add((pcx + dx, pcz + dz));

            // Devolver al pool lo que sobra
            var toRemove = new List<(int,int)>();
            foreach (var kv in active)
            {
                if (!needed.Contains(kv.Key))
                {
                    ObjectPools.ReturnChunk(kv.Value.gameObject);
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var k in toRemove) active.Remove(k);

            // Re-priorizar cola (front-first)
            var pq = new PriorityQueue<(int,int)>();
            Vector2 forward = new Vector2(player.forward.x, player.forward.z);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector2.up; else forward.Normalize();

            foreach (var c in needed)
            {
                if (active.ContainsKey(c)) continue;
                if (scheduled.Contains(c)) continue;

                int dx = c.Item1 - pcx;
                int dz = c.Item2 - pcz;

                int ring = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz));
                float dot = 1f - Vector2.Dot(forward, new Vector2(dx, dz).normalized);
                float dist = dx * dx + dz * dz;

                float key = ring * 10000f + dot * 100f + dist;
                pq.Enqueue(key, c);
            }

            int budget = Mathf.Max(1, config.MaxBuildsPerFrame * 3);
            int pushed = 0;
            while (pushed < budget && pq.Count > 0)
            {
                var coord = pq.Dequeue(out _);
                scheduled.Add(coord);
                _ = BuildChunkAsync(new ChunkCoord(coord.Item1, coord.Item2));
                pushed++;
            }
        }

        private async Task BuildChunkAsync(ChunkCoord coord)
        {
            try
            {
                var chunkData = new VoxelChunkData(config.ChunkSize, config.ChunkHeight);

                // Load or Generate
                bool loaded = false;
                if (config.EnableSaveLoad)
                    loaded = await storage.TryLoad(coord, config, chunkData);

                if (!loaded)
                {
                    generator.GenerateChunkVoxels(coord, chunkData, config, biomeProvider, noise);
                    if (config.EnableSaveLoad)
                        _ = storage.Save(coord, config, chunkData); // fire-and-forget
                }

                var mesh = mesher.BuildMesh(chunkData, config);
                readyMeshes.Enqueue((coord, mesh));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ApplyMesh(ChunkCoord coord, MeshData mesh)
        {
            var key = (coord.x, coord.z);
            if (!active.TryGetValue(key, out var vc))
            {
                var go = ObjectPools.RentChunk(chunkPrefab);
                go.transform.SetParent(transform, false);
                go.transform.position = coord.ToWorldPositionMeters(config);
                go.name = $"Chunk_{coord.x}_{coord.z}";

                vc = go.GetComponent<VoxelChunk>();
                if (vc == null) vc = go.AddComponent<VoxelChunk>();
                active[key] = vc;
            }

            // Collider sólo cerca
            int pcx = Mathf.FloorToInt(player.position.x / (config.BlockSizeMeters * config.ChunkSize));
            int pcz = Mathf.FloorToInt(player.position.z / (config.BlockSizeMeters * config.ChunkSize));
            bool enableCol = Mathf.Abs(coord.x - pcx) <= config.ColliderDistance &&
                             Mathf.Abs(coord.z - pcz) <= config.ColliderDistance;

            vc.ApplyMesh(mesh, enableCol);
            scheduled.Remove(key);
        }
    }
}