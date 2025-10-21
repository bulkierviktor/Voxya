using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 16;
    public static int chunkHeight = 100;

    // Tamaño del bloque en METROS (lo fija WorldGenerator al arrancar)
    public static float blockSize = 1f;

    // Terreno definido en METROS (lo fija WorldGenerator)
    public static float terrainMaxHeightMeters = 20f;
    public static float noiseScaleMeters = 25f;

    public WorldGenerator world;
    public Vector2 chunkPosition;

    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    private const float TextureAtlasSizeInBlocks = 2f;
    private const float NormalizedBlockTextureSize = 1f / TextureAtlasSizeInBlocks;
    private static readonly Vector2 GrassTopTexture = new Vector2(0, 1);
    private static readonly Vector2 GrassSideTexture = new Vector2(1, 1);
    private static readonly Vector2 DirtTexture = new Vector2(0, 0);
    private static readonly Vector2 StoneTexture = new Vector2(1, 0);

    private Biome biome;

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
        float heightMultiplier = 1f;
        switch (biome)
        {
            case Biome.Plains: heightMultiplier = 1f; break;
            case Biome.Hills: heightMultiplier = 1.35f; break;
            case Biome.Desert: heightMultiplier = 0.8f; break;
            case Biome.Snow: heightMultiplier = 0.9f; break;
            case Biome.Forest: heightMultiplier = 1.1f; break;
        }

        const float smoothBorder = 8f;

        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                // Coordenadas en BLOQUES
                float worldXBlocks = (chunkPosition.x * chunkSize) + x;
                float worldZBlocks = (chunkPosition.y * chunkSize) + z;

                int baseHeight = GetTerrainHeight(worldXBlocks, worldZBlocks);
                int targetHeight = Mathf.RoundToInt(baseHeight * heightMultiplier);

                // Consulta de ciudad: convertimos a METROS para WorldGenerator.GetBlockAt
                if (world.TryGetCityForWorldXZ(worldXBlocks * blockSize, worldZBlocks * blockSize, out CityData city))
                {
                    Vector2 centerBlocks = city.WorldCenterXZ(chunkSize);
                    float distBlocks = Vector2.Distance(new Vector2(worldXBlocks, worldZBlocks), centerBlocks);
                    float radiusBlocks = city.radiusChunks * chunkSize;

                    int plateau = GetTerrainHeight(centerBlocks.x, centerBlocks.y);

                    if (distBlocks <= radiusBlocks - smoothBorder)
                    {
                        targetHeight = plateau;
                    }
                    else
                    {
                        float t = Mathf.InverseLerp(radiusBlocks, radiusBlocks - smoothBorder, distBlocks);
                        targetHeight = Mathf.RoundToInt(Mathf.Lerp(targetHeight, plateau, t));
                    }
                }

                for (int y = 0; y < chunkHeight; y++)
                {
                    if (y == targetHeight - 1) voxelMap[x, y, z] = BlockType.Grass;
                    else if (y < targetHeight - 1 && y > targetHeight - 5) voxelMap[x, y, z] = BlockType.Dirt;
                    else if (y <= targetHeight - 5) voxelMap[x, y, z] = BlockType.Stone;
                    else voxelMap[x, y, z] = BlockType.Air;
                }
            }
        }
    }

    // worldX/worldZ en BLOQUES (convertimos a METROS para muestrear ruido)
    public static int GetTerrainHeight(float worldX, float worldZ)
    {
        float xMeters = worldX * blockSize;
        float zMeters = worldZ * blockSize;

        float noiseX = xMeters / Mathf.Max(0.0001f, noiseScaleMeters);
        float noiseZ = zMeters / Mathf.Max(0.0001f, noiseScaleMeters);
        float perlinValue = Mathf.PerlinNoise(noiseX, noiseZ);

        int terrainMaxHeightBlocks = Mathf.Clamp(
            Mathf.RoundToInt(terrainMaxHeightMeters / Mathf.Max(0.0001f, blockSize)),
            1, chunkHeight
        );

        return Mathf.RoundToInt(perlinValue * terrainMaxHeightBlocks);
    }

    void GenerateChunkMesh()
    {
        for (int y = 0; y < chunkHeight; y++)
        {
            for (int x = 0; x < chunkSize; x++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    BlockType blockType = voxelMap[x, y, z];
                    if (blockType == BlockType.Air)
                        continue;

                    Vector3 blockPos = new Vector3(x, y, z); // en BLOQUES
                    for (int i = 0; i < 6; i++)
                    {
                        if (IsFaceVisible(blockPos, i))
                        {
                            CreateFace(blockType, blockPos, i);
                        }
                    }
                }
            }
        }
    }

    bool IsFaceVisible(Vector3 blockPos, int faceIndex)
    {
        Vector3 neighborPos = blockPos + FaceNormals[faceIndex];

        if (neighborPos.x < 0 || neighborPos.x >= chunkSize ||
            neighborPos.y < 0 || neighborPos.y >= chunkHeight ||
            neighborPos.z < 0 || neighborPos.z >= chunkSize)
        {
            Vector3 worldNeighborPos = transform.position + (neighborPos * blockSize); // METROS
            return world.GetBlockAt(worldNeighborPos) == BlockType.Air;
        }
        else
        {
            return voxelMap[(int)neighborPos.x, (int)neighborPos.y, (int)neighborPos.z] == BlockType.Air;
        }
    }

    void CreateFace(BlockType blockType, Vector3 blockPos, int faceIndex)
    {
        int vertCount = vertices.Count;

        for (int i = 0; i < 4; i++)
        {
            // BLOQUES → METROS
            vertices.Add((blockPos + VoxelFaceData[faceIndex, i]) * blockSize);
        }

        triangles.Add(vertCount);
        triangles.Add(vertCount + 1);
        triangles.Add(vertCount + 2);
        triangles.Add(vertCount);
        triangles.Add(vertCount + 2);
        triangles.Add(vertCount + 3);

        Vector2 textureCoord = GetTextureCoord(blockType, faceIndex);
        float x = textureCoord.x * NormalizedBlockTextureSize;
        float y = textureCoord.y * NormalizedBlockTextureSize;

        uvs.Add(new Vector2(x, y));
        uvs.Add(new Vector2(x, y + NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + NormalizedBlockTextureSize, y + NormalizedBlockTextureSize));
        uvs.Add(new Vector2(x + NormalizedBlockTextureSize, y));
    }

    Vector2 GetTextureCoord(BlockType blockType, int faceIndex)
    {
        if (blockType == BlockType.Grass) return (faceIndex == 2) ? GrassTopTexture : GrassSideTexture;
        if (blockType == BlockType.Dirt) return DirtTexture;
        return StoneTexture;
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize)
        {
            return BlockType.Air;
        }
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