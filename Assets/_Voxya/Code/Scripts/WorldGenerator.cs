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
                    // Creamos un Vector2 para la posición, para que el código sea más limpio.
                    Vector2 position = new Vector2(x, z);

                    // ¡ESTA ES LA LÍNEA CLAVE!
                    // Le damos al chunk todo lo que necesita y le ordenamos que se construya.
                    newChunk.Initialize(this, position);

                    // Lo añadimos a nuestro diccionario para tenerlo controlado.
                    chunkObjects.Add(position, newChunk);
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
            UnityEngine.Debug.Log("Mundo generado. Controles del jugador activados.");
        }
    }
    // --- FIN DE LA ADICIÓN 4 ---

    // --- INICIO DE LA ADICIÓN 5: La función GetBlockAt que falta ---
    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        // Extraemos las coordenadas enteras de la posición
        int worldX = Mathf.FloorToInt(worldPosition.x);
        int worldY = Mathf.FloorToInt(worldPosition.y);
        int worldZ = Mathf.FloorToInt(worldPosition.z);

        // El resto de la lógica es la misma que ya teníamos, pero usando estas variables
        if (worldY < 0 || worldY >= 256)
        {
            return BlockType.Air;
        }

        int chunkX = Mathf.FloorToInt(worldX / 16f);
        int chunkZ = Mathf.FloorToInt(worldZ / 16f);

        Chunk chunk = GetChunk(chunkX, chunkZ);

        if (chunk == null)
        {
            return BlockType.Air;
        }

        int localX = worldX - chunkX * 16;
        int localZ = worldZ - chunkZ * 16;

        return chunk.GetBlock(localX, worldY, localZ);
    }
 
}