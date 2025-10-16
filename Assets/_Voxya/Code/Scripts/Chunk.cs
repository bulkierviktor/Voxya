using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class Chunk : MonoBehaviour
{
    public static int chunkSize = 16;
    public static int chunkHeight = 100;

    // Usamos nuestro nuevo enum para el mapa de voxels
    private BlockType[,,] voxelMap = new BlockType[chunkSize, chunkHeight, chunkSize];

    private List<Vector3> vertices = new List<Vector3>();
    private List<int> triangles = new List<int>();

    void Start()
    {
        PopulateVoxelMap();
        GenerateChunkMesh();
        CreateMesh();
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
                    if (y == worldHeight - 1) // La capa más alta
                    {
                        voxelMap[x, y, z] = BlockType.Grass;
                    }
                    else if (y < worldHeight - 1 && y > worldHeight - 5) // Las 3 capas debajo de la hierba
                    {
                        voxelMap[x, y, z] = BlockType.Dirt;
                    }
                    else if (y <= worldHeight - 5) // Todo lo demás por debajo
                    {
                        voxelMap[x, y, z] = BlockType.Stone;
                    }
                    else // Por encima de la altura del terreno
                    {
                        voxelMap[x, y, z] = BlockType.Air;
                    }
                }
            }
        }
    }

    void GenerateChunkMesh()
    {
        for (int x = 0; x < chunkSize; x++)
        {
            for (int y = 0; y < chunkHeight; y++)
            {
                for (int z = 0; z < chunkSize; z++)
                {
                    // Solo nos interesan los bloques que NO son de aire
                    if (voxelMap[x, y, z] != BlockType.Air)
                    {
                        CreateVoxel(new Vector3(x, y, z));
                    }
                }
            }
        }
    }

    bool CheckVoxel(Vector3 pos)
    {
        int x = Mathf.FloorToInt(pos.x);
        int y = Mathf.FloorToInt(pos.y);
        int z = Mathf.FloorToInt(pos.z);

        if (x < 0 || x >= chunkSize || y < 0 || y >= chunkHeight || z < 0 || z >= chunkSize)
        {
            // Consultamos la altura del terreno para los bloques fuera de este chunk
            Vector3 worldPos = new Vector3(x, y, z) + transform.position;
            return worldPos.y >= GetTerrainHeight(worldPos.x, worldPos.z);
        }

        // Devolvemos 'true' si el bloque en esa posición es aire
        return voxelMap[x, y, z] == BlockType.Air;
    }

    // --- El resto del código no necesita cambios ---
    // (CreateVoxel, AddVoxelFace, CreateMesh, GetTerrainHeight, VoxelVertices, VoxelTriangles)
    public static int GetTerrainHeight(float worldX, float worldZ)
    {
        float noiseScale = 25f;
        int terrainMaxHeight = 20;
        float noiseX = worldX / noiseScale;
        float noiseZ = worldZ / noiseScale;
        float perlinValue = Mathf.PerlinNoise(noiseX, noiseZ);
        return Mathf.RoundToInt(perlinValue * terrainMaxHeight);
    }

    void CreateVoxel(Vector3 position)
    {
        int x = (int)position.x;
        int y = (int)position.y;
        int z = (int)position.z;

        if (CheckVoxel(new Vector3(x, y, z - 1))) { AddVoxelFace(position, 0); }
        if (CheckVoxel(new Vector3(x, y, z + 1))) { AddVoxelFace(position, 1); }
        if (CheckVoxel(new Vector3(x, y + 1, z))) { AddVoxelFace(position, 2); }
        if (CheckVoxel(new Vector3(x, y - 1, z))) { AddVoxelFace(position, 3); }
        if (CheckVoxel(new Vector3(x - 1, y, z))) { AddVoxelFace(position, 4); }
        if (CheckVoxel(new Vector3(x + 1, y, z))) { AddVoxelFace(position, 5); }
    }

    private static readonly Vector3[] VoxelVertices = new Vector3[8]
    {
        new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(1, 1, 0), new Vector3(0, 1, 0),
        new Vector3(0, 1, 1), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 1, 1)
    };

    private static readonly int[,] VoxelTriangles = new int[6, 4]
    {
        {0, 3, 1, 2}, {5, 6, 4, 7}, {3, 7, 2, 6}, {1, 5, 0, 4}, {4, 0, 7, 3}, {1, 2, 6, 7}
    };

    void AddVoxelFace(Vector3 position, int faceIndex)
    {
        int vertexIndex = vertices.Count;
        for (int i = 0; i < 4; i++) { vertices.Add(position + VoxelVertices[VoxelTriangles[faceIndex, i]]); }
        triangles.Add(vertexIndex);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 2);
        triangles.Add(vertexIndex + 1);
        triangles.Add(vertexIndex + 3);
    }

    void CreateMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.RecalculateNormals();
        GetComponent<MeshFilter>().mesh = mesh;
    }
}