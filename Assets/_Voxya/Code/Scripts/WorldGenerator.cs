using System.Collections; // ADICI�N: Necesario para las Corutinas (la espera del jugador).
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    // --- INICIO DE LA ADICI�N 1: Referencia al Jugador ---
    public PlayerController player;
    // --- FIN DE LA ADICI�N 1 ---

    // Tu c�digo base no cambia.
    public int worldSizeInChunks = 10;
    public GameObject chunkPrefab;
    public Dictionary<Vector2, Chunk> chunkObjects = new Dictionary<Vector2, Chunk>();

    void Start()
    {
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                // Tu l�nea de Instantiate no cambia.
                GameObject newChunkObject = Instantiate(chunkPrefab, new Vector3(x * 16, 0, z * 16), Quaternion.identity);

                // --- INICIO DE LA ADICI�N 2: Configuraci�n del Chunk ---
                // Ahora que el chunk tiene "orejas", hablamos con �l.
                Chunk newChunk = newChunkObject.GetComponent<Chunk>();
                if (newChunk != null)
                {
                    // 1. Le decimos d�nde est�.
                    newChunk.chunkPosition = new Vector2(x, z);
                    // 2. Le decimos qui�nes somos.
                    newChunk.worldGenerator = this;
                    // 3. Lo a�adimos al directorio (esto ya lo ten�as impl�cito, ahora lo hacemos expl�cito).
                    chunkObjects.Add(new Vector2(x, z), newChunk);
                }
                // --- FIN DE LA ADICI�N 2 ---
            }
        }

        // --- INICIO DE LA ADICI�N 3: Activar la espera del jugador ---
        StartCoroutine(EnablePlayerAfterWorldGen());
        // --- FIN DE LA ADICI�N 3 ---
    }

    // Tu funci�n GetChunk no cambia.
    public Chunk GetChunk(int x, int z)
    {
        chunkObjects.TryGetValue(new Vector2(x, z), out Chunk chunk);
        return chunk;
    }

    // --- INICIO DE LA ADICI�N 4: La Corutina de Espera ---
    private IEnumerator EnablePlayerAfterWorldGen()
    {
        // Espera un frame para dar tiempo a los chunks a procesarse.
        yield return new WaitForEndOfFrame();
        if (player != null)
        {
            // Llama a la funci�n p�blica que a�adimos en PlayerController.
            player.EnableControls();
            Debug.Log("Mundo generado. Controles del jugador activados.");
        }
    }
    // --- FIN DE LA ADICI�N 4 ---

    // --- INICIO DE LA ADICI�N 5: La funci�n GetBlockAt que falta ---
    public BlockType GetBlockAt(int worldX, int worldY, int worldZ)
    {
        // Comprobaci�n de seguridad para evitar errores si se pide un bloque fuera de la altura del mundo.
        if (worldY < 0 || worldY >= 256)
        {
            return BlockType.Air;
        }

        // Calcula a qu� chunk pertenecen esas coordenadas globales.
        int chunkX = Mathf.FloorToInt(worldX / 16f);
        int chunkZ = Mathf.FloorToInt(worldZ / 16f);

        // Usa tu funci�n GetChunk para obtener el chunk correspondiente.
        Chunk chunk = GetChunk(chunkX, chunkZ);

        // Si el chunk no existe (porque est� fuera del mundo generado), es aire.
        if (chunk == null)
        {
            return BlockType.Air;
        }

        // Calcula la coordenada local dentro de ese chunk.
        int localX = worldX - chunkX * 16;
        int localZ = worldZ - chunkZ * 16;

        // Le pide al chunk que le diga qu� bloque hay en esa coordenada local.
        // Asumimos que `Chunk` tendr� una funci�n `GetBlock` en el futuro.
        // Por ahora, para que compile, devolvemos Air. La l�gica real la haremos despu�s.
        // return chunk.GetBlock(localX, worldY, localZ);
        return BlockType.Air; // Placeholder para compilar
    }
    // --- FIN DE LA ADICI�N 5 ---
}