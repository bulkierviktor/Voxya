using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    [Header("Chunk Prefab")]
    public GameObject chunkPrefab;
    [Header("World Settings")]
    public int worldSizeInChunks = 10;

    // 1. Diccionario para almacenar y acceder a todos los chunks generados
    public Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();

    void Start()
    {
        StartCoroutine(GenerateWorld());
    }

    IEnumerator GenerateWorld()
    {
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                Vector3 position = new Vector3(x * Chunk.chunkSize, 0, z * Chunk.chunkSize);
                GenerateChunk(position);
                yield return null; // Pausa de un frame para no congelar Unity
            }
        }
    }

    // 2. Función para generar un único chunk, guardarlo y prepararlo
    void GenerateChunk(Vector3 position)
    {
        GameObject chunkObject = Instantiate(chunkPrefab, position, Quaternion.identity);
        chunkObject.transform.SetParent(transform);
        Chunk newChunk = chunkObject.GetComponent<Chunk>();

        // Guardamos el chunk en el diccionario y lo inicializamos.
        // Al inicializar, le pasamos una referencia a este mismo script (el "mundo")
        // para que el chunk pueda comunicarse con él.
        chunks.Add(position, newChunk);
        newChunk.Initialize(this);
    }

    // 3. Función PÚBLICA para que cualquier chunk pueda preguntar por un bloque
    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        // Redondeamos la posición para asegurarnos de que estamos en la rejilla de bloques
        int blockX = Mathf.FloorToInt(worldPosition.x);
        int blockY = Mathf.FloorToInt(worldPosition.y);
        int blockZ = Mathf.FloorToInt(worldPosition.z);

        // Calculamos a qué chunk pertenece esa coordenada
        int chunkX = Mathf.FloorToInt(blockX / (float)Chunk.chunkSize) * Chunk.chunkSize;
        int chunkZ = Mathf.FloorToInt(blockZ / (float)Chunk.chunkSize) * Chunk.chunkSize;

        Vector3 chunkPos = new Vector3(chunkX, 0, chunkZ);

        // Si el chunk que buscamos ya ha sido generado, le preguntamos a él.
        // Esta es la parte que evita las paredes entre chunks.
        if (chunks.ContainsKey(chunkPos))
        {
            // La lógica para obtener el bloque exacto del otro chunk iría aquí.
            // Por ahora, una simulación basada en la altura del terreno es suficiente
            // para que el sistema de visibilidad funcione.
            int terrainHeight = Chunk.GetTerrainHeight(worldPosition.x, worldPosition.z);

            if (blockY >= terrainHeight) return BlockType.Air;
            if (blockY == terrainHeight - 1) return BlockType.Grass;
            if (blockY < terrainHeight - 4) return BlockType.Stone;
            return BlockType.Dirt;
        }
        else
        {
            // Si el chunk aún no existe (está fuera del mundo generado),
            // lo tratamos como si fuera sólido por debajo de la altura del terreno.
            int terrainHeight = Chunk.GetTerrainHeight(worldPosition.x, worldPosition.z);
            if (worldPosition.y >= terrainHeight) return BlockType.Air;
            return BlockType.Stone;
        }
    }
}