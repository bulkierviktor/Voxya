using System.Collections; // ADICIÓN: Necesario para las Corutinas (la espera del jugador).
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    // --- INICIO DE LA ADICIÓN 1: Referencia al Jugador ---
    public PlayerController player;
    // --- FIN DE LA ADICIÓN 1 ---

    // Tu código base no cambia.
    public int worldSizeInChunks = 10;
    public GameObject chunkPrefab;
    public Dictionary<Vector2, Chunk> chunkObjects = new Dictionary<Vector2, Chunk>();

    void Start()
    {
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                // Tu línea de Instantiate no cambia.
                GameObject newChunkObject = Instantiate(chunkPrefab, new Vector3(x * 16, 0, z * 16), Quaternion.identity);

                // --- INICIO DE LA ADICIÓN 2: Configuración del Chunk ---
                // Ahora que el chunk tiene "orejas", hablamos con él.
                Chunk newChunk = newChunkObject.GetComponent<Chunk>();
                if (newChunk != null)
                {
                    // 1. Le decimos dónde está.
                    newChunk.chunkPosition = new Vector2(x, z);
                    // 2. Le decimos quiénes somos.
                    newChunk.worldGenerator = this;
                    // 3. Lo añadimos al directorio (esto ya lo tenías implícito, ahora lo hacemos explícito).
                    chunkObjects.Add(new Vector2(x, z), newChunk);
                }
                // --- FIN DE LA ADICIÓN 2 ---
            }
        }

        // --- INICIO DE LA ADICIÓN 3: Activar la espera del jugador ---
        StartCoroutine(EnablePlayerAfterWorldGen());
        // --- FIN DE LA ADICIÓN 3 ---
    }

    // Tu función GetChunk no cambia.
    public Chunk GetChunk(int x, int z)
    {
        chunkObjects.TryGetValue(new Vector2(x, z), out Chunk chunk);
        return chunk;
    }

    // --- INICIO DE LA ADICIÓN 4: La Corutina de Espera ---
    private IEnumerator EnablePlayerAfterWorldGen()
    {
        // Espera un frame para dar tiempo a los chunks a procesarse.
        yield return new WaitForEndOfFrame();
        if (player != null)
        {
            // Llama a la función pública que añadimos en PlayerController.
            player.EnableControls();
            Debug.Log("Mundo generado. Controles del jugador activados.");
        }
    }
    // --- FIN DE LA ADICIÓN 4 ---

    // --- INICIO DE LA ADICIÓN 5: La función GetBlockAt que falta ---
    public BlockType GetBlockAt(int worldX, int worldY, int worldZ)
    {
        // Comprobación de seguridad para evitar errores si se pide un bloque fuera de la altura del mundo.
        if (worldY < 0 || worldY >= 256)
        {
            return BlockType.Air;
        }

        // Calcula a qué chunk pertenecen esas coordenadas globales.
        int chunkX = Mathf.FloorToInt(worldX / 16f);
        int chunkZ = Mathf.FloorToInt(worldZ / 16f);

        // Usa tu función GetChunk para obtener el chunk correspondiente.
        Chunk chunk = GetChunk(chunkX, chunkZ);

        // Si el chunk no existe (porque está fuera del mundo generado), es aire.
        if (chunk == null)
        {
            return BlockType.Air;
        }

        // Calcula la coordenada local dentro de ese chunk.
        int localX = worldX - chunkX * 16;
        int localZ = worldZ - chunkZ * 16;

        // Le pide al chunk que le diga qué bloque hay en esa coordenada local.
        // Asumimos que `Chunk` tendrá una función `GetBlock` en el futuro.
        // Por ahora, para que compile, devolvemos Air. La lógica real la haremos después.
        // return chunk.GetBlock(localX, worldY, localZ);
        return BlockType.Air; // Placeholder para compilar
    }
    // --- FIN DE LA ADICIÓN 5 ---
}