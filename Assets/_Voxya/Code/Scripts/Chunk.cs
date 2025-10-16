using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 16;
    public static int chunkHeight = 100;
    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();
    private List<Vector2> uvs = new List<Vector2>();

    // --- Definición del Atlas de Texturas ---
    private const float TextureAtlasSizeInBlocks = 2f;
    private const float NormalizedBlockTextureSize = 1f / TextureAtlasSizeInBlocks;

    private static readonly Vector2 GrassTopTexture = new Vector2(0, 1);
    private static readonly Vector2 GrassSideTexture = new Vector2(1, 1);
    private static readonly Vector2 DirtTexture = new Vector2(0, 0);
    private static readonly Vector2 StoneTexture = new Vector2(1, 0);

    // --- TABLAS DE DATOS DE VOXEL ---
    private static readonly Vector3[] VoxelVertices = new Vector3[8]
    {
        new Vector3(0.0f, 0.0f, 0.0f), // 0
        new Vector3(1.0f, 0.0f, 0.0f), // 1
        new Vector3(1.0f, 1.0f, 0.0f), // 2
        new Vector3(0.0f, 1.0f, 0.0f), // 3
        new Vector3(0.0f, 0.0f, 1.0f), // 4
        new Vector3(1.0f, 0.0f, 1.0f), // 5
        new Vector3(1.0f, 1.0f, 1.0f), // 6
        new Vector3(0.0f, 1.0f, 1.0f)  // 7
    };

    private static readonly int[,] VoxelFaces = new int[6, 4]
    {
        {0, 3, 2, 1}, // Cara Trasera
        {5, 6, 7, 4}, // Cara Frontal
        {3, 7, 6, 2}, // Cara Superior
        {1, 5, 4, 0}, // Cara Inferior
        {4, 7, 3, 0}, // Cara Izquierda
        {2, 6, 5, 1}  // Cara Derecha
    };

    private static readonly Vector2[] VoxelUvs = new Vector2[4]
    {
        new Vector2(0.0f, 0.0f),
        new Vector2(0.0f, 1.0f),
        new Vector2(1.0f, 0.0f),
        new Vector2(1.0f, 1.0f)
    };

    void Start()
    {
        PopulateVoxelMap();
        GenerateChunkMesh();
        CreateMesh();
    }

    void AddVoxelFace(Vector3 position, int faceIndex, BlockType blockType)
    {
        Vector2 textureCoord = GetTextureCoord(blockType, faceIndex);
        int vertexIndex = vertices.Count;

        for (int i = 0; i < 4; i++)
        {
            vertices.Add(position + VoxelVertices[VoxelFaces[faceIndex, i]]);
        }

        uvs.Add(new Vector2(textureCoord.x * NormalizedBlockTextureSize, textureCoord.y * NormalizedBlockTextureSize) + VoxelUvs[0] * NormalizedBlockTextureSize);
        uvs.Add(new Vector2(textureCoord.x * NormalizedBlockTextureSize, textureCoord.y * NormalizedBlockTextureSize) + VoxelUvs[1] * NormalizedBlockTextureSize);
        uvs.Add(new Vector2(textureCoord.x * NormalizedBlockTextureSize, textureCoord.y * NormalizedBlockTextureSize) + VoxelUvs[2] * NormalizedBlockTextureSize);
        uvs.Add(new Vector2(textureCoord.x * NormalizedBlockTextureSize, textureCoord.y * NormalizedBlockTextureSize) + VoxelUvs[3] * NormalizedBlockTextureSize);

        // --- ¡LA CORRECCIÓN DEFINITIVA ESTÁ AQUÍ! ---
        // Este es el orden de bobinado (winding order) correcto para los vértices.
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 3);
    }

    Vector2 GetTextureCoord(BlockType blockType, int faceIndex)
    {
        if (blockType == BlockType.Grass)
        {
            return (faceIndex == 2) ? GrassTopTexture : GrassSideTexture;
        }
        else if (blockType == BlockType.Dirt)
        {
            return DirtTexture;
        }
        else // Stone
        {
            return StoneTexture;
        }
    }

    void CreateVoxel(Vector3 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;
        int z = (int)position.z;
        BlockType blockType = voxelMap[x, y, z];

        if (CheckVoxel(new Vector3(x, y, z - 1))) { AddVoxelFace(position, 0, blockType); }
        if (CheckVoxel(new Vector3(x, y, z + 1))) { AddVoxelFace(position, 1, blockType); }
        if (CheckVoxel(new Vector3(x, y + 1, z))) { AddVoxelFace(position, 2, blockType); }
        if (CheckVoxel(new Vector3(x, y - 1, z))) { AddVoxelFace(position, 3, blockType); }
        if (CheckVoxel(new Vector3(x - 1, y, z))) { AddVoxelFace(position, 4, blockType); }
        if (CheckVoxel(new Vector3(x + 1, y, z))) { AddVoxelFace(position, 5, blockType); }
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
        {
            for (int z = 0; z < chunkSize; z++)
            {
                float worldX = transform.position.x + x;
                float worldZ = transform.position.z + z;
                int worldHeight = GetTerrainHeight(worldX, worldZ);

                for (int y = 0; y < chunkHeight; y++)
                {
                    if (y == worldHeight - 1) { voxelMap[x, y, z] = BlockType.Grass; }
                    else if (y < worldHeight - 1 && y > worldHeight - 5) { voxelMap[x, y, z] = BlockType.Dirt; }
                    else if (y <= worldHeight - 5) { voxelMap[x, y, z] = BlockType.Stone; }
                    else { voxelMap[x, y, z] = BlockType.Air; }
                }
            }
        }
    }

    void GenerateChunkMesh()
    {
        for (int x = 0; x < chunkSize; x++)
            for (int y = 0; y < chunkHeight; y++)
                for (int z = 0; z < chunkSize; z++)
                    if (voxelMap[x, y, z] != BlockType.Air)
                        CreateVoxel(new Vector3(x, y, z));
    }

    bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize)
        {
            Vector3 worldPos = new Vector3(x, y, z) + transform.position;
            return worldPos.y >= GetTerrainHeight(worldPos.x, worldPos.z);
        }
        return voxelMap[x, y, z] == BlockType.Air;
    }

    public static int GetTerrainHeight(float worldX, float worldZ)
    {
        float noiseScale = 25f;
        int terrainMaxHeight = 20;
        float noiseX = worldX / noiseScale;
        float noiseZ = worldZ / noiseScale;
        float perlinValue = Mathf.PerlinNoise(noiseX, noiseZ);
        return Mathf.RoundToInt(perlinValue * terrainMaxHeight);
    }
}