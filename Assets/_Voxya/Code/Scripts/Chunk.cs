using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 16;
    public static int chunkHeight = 100;
    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];

    private WorldGenerator world;
    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    private const float TextureAtlasSizeInBlocks = 2f;
    private const float NormalizedBlockTextureSize = 1f / TextureAtlasSizeInBlocks;

    private static readonly Vector2 GrassTopTexture = new Vector2(0, 1);
    private static readonly Vector2 GrassSideTexture = new Vector2(1, 1);
    private static readonly Vector2 DirtTexture = new Vector2(0, 0);
    private static readonly Vector2 StoneTexture = new Vector2(1, 0);

    public void Initialize(WorldGenerator world)
    {
        this.world = world;
        PopulateVoxelMap();
        GenerateChunkMesh();
        CreateMesh();
    }

    void GenerateChunkMesh()
    {
        for (int y = 0; y < chunkHeight; y++)
            for (int x = 0; x < chunkSize; x++)
                for (int z = 0; z < chunkSize; z++)
                {
                    BlockType blockType = voxelMap[x, y, z];
                    if (blockType == BlockType.Air)
                        continue;

                    Vector3 blockPos = new Vector3(x, y, z);
                    for (int i = 0; i < 6; i++)
                    {
                        if (IsFaceVisible(blockPos, i))
                        {
                            CreateFace(blockType, blockPos, i);
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
            return world.GetBlockAt(transform.position + neighborPos) == BlockType.Air;
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

        // Este orden de triangulación es correcto y estándar
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
        if (blockType == BlockType.Grass)
        {
            return (faceIndex == 2) ? GrassTopTexture : GrassSideTexture;
        }
        if (blockType == BlockType.Dirt)
        {
            return DirtTexture;
        }
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
    }

    void PopulateVoxelMap()
    {
        for (int x = 0; x < chunkSize; x++)
            for (int z = 0; z < chunkSize; z++)
            {
                float worldX = transform.position.x + x;
                float worldZ = transform.position.z + z;
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

    public static int GetTerrainHeight(float worldX, float worldZ)
    {
        float noiseScale = 25f;
        int terrainMaxHeight = 40;
        float noiseX = worldX / noiseScale;
        float noiseZ = worldZ / noiseScale;
        float perlinValue = Mathf.PerlinNoise(noiseX, noiseZ);
        return Mathf.RoundToInt(perlinValue * terrainMaxHeight);
    }

    private static readonly Vector3[] FaceNormals = new Vector3[6]
    {
        Vector3.back, Vector3.forward, Vector3.up, Vector3.down, Vector3.left, Vector3.right
    };

    // --- ¡LA CORRECCIÓN ESTÁ AQUÍ! ---
    // Esta tabla de datos de vértices por cara está ahora en el orden correcto
    // para que todas las caras apunten hacia afuera.
    private static readonly Vector3[,] VoxelFaceData = new Vector3[6, 4]
    {
        // Cara Trasera (Z-)
        {new Vector3(0, 0, 0), new Vector3(0, 1, 0), new Vector3(1, 1, 0), new Vector3(1, 0, 0)},
        // Cara Frontal (Z+)
        {new Vector3(1, 0, 1), new Vector3(1, 1, 1), new Vector3(0, 1, 1), new Vector3(0, 0, 1)},
        // Cara Superior (Y+)
        {new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0)},
        // Cara Inferior (Y-)
        {new Vector3(1, 0, 0), new Vector3(1, 0, 1), new Vector3(0, 0, 1), new Vector3(0, 0, 0)},
        // Cara Izquierda (X-)
        {new Vector3(0, 0, 1), new Vector3(0, 1, 1), new Vector3(0, 1, 0), new Vector3(0, 0, 0)},
        // Cara Derecha (X+)
        {new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(1, 1, 1), new Vector3(1, 0, 1)}
    };
}