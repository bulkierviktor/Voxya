using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    // --- VARIABLES ---
    public WorldGenerator world;
    public Vector2 chunkPosition; // Necesario para que el WorldGenerator nos ubique
    public Biome biome; // Biome type for this chunk

    public static int chunkSize = 16;
    public static int chunkHeight = 100;

    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    // --- CONSTANTES DE TEXTURAS ---
    private const float TextureAtlasSizeInBlocks = 2f;
    private const float NormalizedBlockTextureSize = 1f / TextureAtlasSizeInBlocks;
    private static readonly Vector2 GrassTopTexture = new Vector2(0, 1);
    private static readonly Vector2 GrassSideTexture = new Vector2(1, 1);
    private static readonly Vector2 DirtTexture = new Vector2(0, 0);
    private static readonly Vector2 StoneTexture = new Vector2(1, 0);

    // --- PUNTO DE PARTIDA ---
    public void Initialize(WorldGenerator world, Vector2 position, Biome biome)
    {
        this.world = world;
        this.chunkPosition = position;
        this.biome = biome;

        PopulateVoxelMap();
        GenerateChunkMesh();
        CreateMesh();
    }

    // --- L�GICA DE GENERACI�N DE DATOS ---
    void PopulateVoxelMap()
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                // Usamos la posici�n del chunk que nos dio el WorldGenerator
                float worldX = (chunkPosition.x * chunkSize) + x;
                float worldZ = (chunkPosition.y * chunkSize) + z;

                int worldHeight = GetTerrainHeight(worldX, worldZ);

                for (int y = 0; y < chunkHeight; y++)
                {
                    if (y == worldHeight - 1) voxelMap[x, y, z] = BlockType.Grass;
                    else if (y < worldHeight - 1 && y > worldHeight - 5) voxelMap[x, y, z] = BlockType.Dirt;
                    else if (y <= worldHeight - 5) voxelMap[x, y, z] = BlockType.Stone;
                    else voxelMap[x, y, z] = BlockType.Air;
                }
            }
        }
    }

    public int GetTerrainHeight(float worldX, float worldZ)
    {
        float noiseScale = 25f;
        int terrainMaxHeight = 40;
        float noiseX = worldX / noiseScale;
        float noiseZ = worldZ / noiseScale;
        float perlinValue = Mathf.PerlinNoise(noiseX, noiseZ);
        
        // Apply biome height multiplier
        float biomeMultiplier = BiomeManager.GetBiomeHeightMultiplier(biome);
        return Mathf.RoundToInt(perlinValue * terrainMaxHeight * biomeMultiplier);
    }

    // --- L�GICA DE CREACI�N DE MALLA ---
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

                    Vector3 blockPos = new Vector3(x, y, z);
                    for (int i = 0; i < 6; i++) // Itera las 6 caras
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
        Vector3 globalNeighborPos = this.transform.position + neighborPos;

        if (neighborPos.x < 0 || neighborPos.x >= chunkSize ||
            neighborPos.y < 0 || neighborPos.y >= chunkHeight ||
            neighborPos.z < 0 || neighborPos.z >= chunkSize)
        {
            return world.GetBlockAt(globalNeighborPos) == BlockType.Air;
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
            vertices.Add(blockPos + VoxelFaceData[faceIndex, i]);
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
        UnityEngine.Debug.Log($"Creando malla para el chunk en {chunkPosition}. V�rtices: {vertices.Count}, Tri�ngulos: {triangles.Count}");

        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh; // Para las colisiones
    }

    public BlockType GetBlock(int x, int y, int z)
    {
        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize)
        {
            return BlockType.Air;
        }
        return voxelMap[x, y, z];
    }

    // --- DATOS EST�TICOS (CONSTANTES) ---
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
