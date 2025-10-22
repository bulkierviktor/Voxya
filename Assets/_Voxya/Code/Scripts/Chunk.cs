using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 16;
    public static int chunkHeight = 100;

    public static float blockSize = 1f;
    public static float terrainMaxHeightMeters = 20f;
    public static float noiseScaleMeters = 25f;
    public static bool enableCityFlattening = true;
    public static int noiseOctaves = 4;
    public static float noiseLacunarity = 2.0f;
    public static float noiseGain = 0.5f;
    public static float warpScaleMeters = 60f;
    public static float warpStrengthMeters = 12f;
    // Suavizado extra para praderas
    public static int plainsSmoothIterations = 2;
    public static float plainsSmoothFactor = 0.5f;

    public WorldGenerator world;
    public Vector2 chunkPosition;

    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];
    private readonly List<Vector3> vertices = new List<Vector3>(4000);
    private readonly List<int> triangles = new List<int>(6000);
    private readonly List<Vector2> uvs = new List<Vector2>(4000);
    private readonly List<Vector3> normals = new List<Vector3>(4000); // NUEVO

    private const float TextureAtlasSizeInBlocks = 2f;
    private const float NormalizedBlockTextureSize = 1f / TextureAtlasSizeInBlocks;
    private static readonly Vector2 GrassTopTexture = new Vector2(0, 1);
    private static readonly Vector2 GrassSideTexture = new Vector2(1, 1);
    private static readonly Vector2 DirtTexture = new Vector2(0, 0);
    private static readonly Vector2 StoneTexture = new Vector2(1, 0);

    private Biome biome;
    private Mesh generatedMesh; // NUEVO

    public void Initialize(WorldGenerator world, Vector2 position, Biome biome)
    {
        this.world = world;
        this.chunkPosition = position;
        this.biome = biome;

        PopulateVoxelMap();
        GenerateChunkMesh();
        CreateMesh();
    }

    void PopulateVoxelMap()
    {
        // Factor de relieve por bioma (reduce amplitud en praderas)
        float heightMultiplier = 1f;
        switch (biome)
        {
            case Biome.Plains: heightMultiplier = 0.65f; break; // más plano
            case Biome.Hills: heightMultiplier = 1.25f; break;
            case Biome.Desert: heightMultiplier = 0.8f; break;
            case Biome.Snow: heightMultiplier = 0.9f; break;
            case Biome.Forest: heightMultiplier = 1.0f; break;
        }

        float desiredBorderMeters = 4f;
        float smoothBorderBlocks = Mathf.Max(1f, desiredBorderMeters / Mathf.Max(0.0001f, blockSize));

        // 1) Alturas base en un grid 2D
        int[,] heights = new int[chunkSize, chunkSize];

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float worldXBlocks = (chunkPosition.x * chunkSize) + x;
                float worldZBlocks = (chunkPosition.y * chunkSize) + z;

                int baseHeight = GetTerrainHeight(worldXBlocks, worldZBlocks);
                int targetHeight = Mathf.RoundToInt(baseHeight * heightMultiplier);

                // Aplanado de ciudad (con borde suave)
                if (enableCityFlattening &&
                    world.TryGetCityForWorldXZ(worldXBlocks * blockSize, worldZBlocks * blockSize, out CityData city))
                {
                    Vector2 centerBlocks = city.WorldCenterXZ(chunkSize);
                    float distBlocks = Vector2.Distance(new Vector2(worldXBlocks, worldZBlocks), centerBlocks);
                    float radiusBlocks = city.radiusChunks * chunkSize;

                    int plateau = GetTerrainHeight(centerBlocks.x, centerBlocks.y);

                    if (distBlocks <= radiusBlocks - smoothBorderBlocks)
                        targetHeight = plateau;
                    else
                    {
                        float t = Mathf.InverseLerp(radiusBlocks, radiusBlocks - smoothBorderBlocks, distBlocks);
                        targetHeight = Mathf.RoundToInt(Mathf.Lerp(targetHeight, plateau, t));
                    }
                }

                heights[x, z] = Mathf.Clamp(targetHeight, 0, chunkHeight - 1);
            }
        }

        // 2) Suavizado local para praderas (reduce cortes bruscos)
        if (biome == Biome.Plains && plainsSmoothIterations > 0)
        {
            for (int it = 0; it < plainsSmoothIterations; it++)
            {
                for (int x = 0; x < chunkSize; x++)
                {
                    for (int z = 0; z < chunkSize; z++)
                    {
                        int sum = 0, cnt = 0;
                        for (int ox = -1; ox <= 1; ox++)
                            for (int oz = -1; oz <= 1; oz++)
                            {
                                int nx = x + ox, nz = z + oz;
                                if (nx < 0 || nx >= chunkSize || nz < 0 || nz >= chunkSize) continue;
                                sum += heights[nx, nz];
                                cnt++;
                            }
                        int avg = (cnt > 0) ? Mathf.RoundToInt(sum / (float)cnt) : heights[x, z];
                        heights[x, z] = Mathf.RoundToInt(Mathf.Lerp(heights[x, z], avg, plainsSmoothFactor));
                    }
                }
            }
        }

        // 3) Relleno voxel en columnas
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                int h = heights[x, z];
                for (int y = 0; y < chunkHeight; y++)
                {
                    if (y == h - 1) voxelMap[x, y, z] = BlockType.Grass;
                    else if (y < h - 1 && y > h - 5) voxelMap[x, y, z] = BlockType.Dirt;
                    else if (y <= h - 5) voxelMap[x, y, z] = BlockType.Stone;
                    else voxelMap[x, y, z] = BlockType.Air;
                }
            }
        }
    }

    public static int GetTerrainHeight(float worldX, float worldZ)
    {
        // worldX/worldZ vienen en BLOQUES; convertimos a metros
        float xMeters = worldX * blockSize;
        float zMeters = worldZ * blockSize;

        // Domain warp leve (rompe patrones rectos)
        float wx = xMeters + warpStrengthMeters * (Mathf.PerlinNoise(xMeters / warpScaleMeters, zMeters / warpScaleMeters) - 0.5f);
        float wz = zMeters + warpStrengthMeters * (Mathf.PerlinNoise((xMeters + 100f) / warpScaleMeters, (zMeters + 100f) / warpScaleMeters) - 0.5f);

        // fBm
        float freq = 1f / Mathf.Max(0.0001f, noiseScaleMeters);
        float amp = 1f, sum = 0f, norm = 0f;
        for (int i = 0; i < noiseOctaves; i++)
        {
            sum += amp * Mathf.PerlinNoise(wx * freq, wz * freq);
            norm += amp;
            amp *= noiseGain;
            freq *= noiseLacunarity;
        }
        float fbm = (norm > 0f) ? sum / norm : 0f;

        int terrainMaxHeightBlocks = Mathf.Clamp(
            Mathf.RoundToInt(terrainMaxHeightMeters / Mathf.Max(0.0001f, blockSize)),
            1, chunkHeight
        );

        return Mathf.RoundToInt(fbm * terrainMaxHeightBlocks);
    }

    void GenerateChunkMesh()
    {
        vertices.Clear(); triangles.Clear(); uvs.Clear(); normals.Clear();

        for (int y = 0; y < chunkHeight; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    BlockType blockType = voxelMap[x, y, z];
                    if (blockType == BlockType.Air) continue;

                    Vector3 blockPos = new Vector3(x, y, z); // en BLOQUES
                    for (int i = 0; i < 6; i++)
                    {
                        if (IsFaceVisible(blockPos, i))
                            CreateFace(blockType, blockPos, i);
                    }
                }
            }
        }
    }

    bool IsFaceVisible(Vector3 blockPos, int faceIndex)
    {
        Vector3 neighborLocal = blockPos + FaceNormals[faceIndex];

        if (neighborLocal.x < 0 || neighborLocal.x >= chunkSize ||
            neighborLocal.y < 0 || neighborLocal.y >= chunkHeight ||
            neighborLocal.z < 0 || neighborLocal.z >= chunkSize)
        {
            Vector3 neighborWorldPos = transform.position + (neighborLocal * blockSize);
            return world.GetBlockAt(neighborWorldPos) == BlockType.Air;
        }
        else
        {
            return voxelMap[(int)neighborLocal.x, (int)neighborLocal.y, (int)neighborLocal.z] == BlockType.Air;
        }
    }

    void CreateFace(BlockType blockType, Vector3 blockPos, int faceIndex)
    {
        int vertCount = vertices.Count;

        // añade 4 vértices
        for (int i = 0; i < 4; i++)
            vertices.Add((blockPos + VoxelFaceData[faceIndex, i]) * blockSize);

        // triángulos
        triangles.Add(vertCount);
        triangles.Add(vertCount + 1);
        triangles.Add(vertCount + 2);
        triangles.Add(vertCount);
        triangles.Add(vertCount + 2);
        triangles.Add(vertCount + 3);

        // UVs
        Vector2 textureCoord = GetTextureCoord(blockType, faceIndex);
        float x = textureCoord.x * NormalizedBlockTextureSize;
        float y = textureCoord.y * NormalizedBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + NormalizedBlockTextureSize, y + NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + NormalizedBlockTextureSize, y));

        // Normales planas por cara (NUEVO)
        Vector3 n = FaceNormals[faceIndex];
        normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);
    }

    Vector2 GetTextureCoord(BlockType blockType, int faceIndex)
    {
        if (blockType == BlockType.Grass) return (faceIndex == 2) ? GrassTopTexture : GrassSideTexture;
        if (blockType == BlockType.Dirt) return DirtTexture;
        return StoneTexture;
    }

    void CreateMesh()
    {
        if (generatedMesh == null)
        {
            generatedMesh = new Mesh();
            generatedMesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        }
        else
        {
            generatedMesh.Clear();
        }

        generatedMesh.SetVertices(vertices);
        generatedMesh.SetTriangles(triangles, 0);
        generatedMesh.SetUVs(0, uvs);
        generatedMesh.SetNormals(normals); // sin RecalculateNormals

        var mf = GetComponent<MeshFilter>();
        mf.sharedMesh = generatedMesh;

        var mc = GetComponent<MeshCollider>();
        // El collider se activa/desactiva desde WorldGenerator para chunks lejanos
        if (mc.enabled)
            mc.sharedMesh = generatedMesh;
    }

    public void SetColliderEnabled(bool state)
    {
        var mc = GetComponent<MeshCollider>();
        if (mc == null) return;
        if (state)
        {
            if (mc.sharedMesh != generatedMesh)
                mc.sharedMesh = generatedMesh;
            mc.enabled = true;
        }
        else
        {
            mc.enabled = false;
        }
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize)
            return BlockType.Air;
        return voxelMap[x, y, z];
    }

    private static readonly Vector3[] FaceNormals = { Vector3.back, Vector3.forward, Vector3.up, Vector3.down, Vector3.left, Vector3.right };
    private static readonly Vector3[,] VoxelFaceData = {
        {new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0)},
        {new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1)},
        {new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0)},
        {new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 0)},
        {new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0)},
        {new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1)}
    };
}