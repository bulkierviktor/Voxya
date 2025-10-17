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
                    // Creamos un Vector2 para la posici�n, para que el c�digo sea m�s limpio.
                    Vector2 position = new Vector2(x, z);

                    // �ESTA ES LA L�NEA CLAVE!
                    // Le damos al chunk todo lo que necesita y le ordenamos que se construya.
                    newChunk.Initialize(this, position);

                    // Lo a�adimos a nuestro diccionario para tenerlo controlado.
                    chunkObjects.Add(position, newChunk);
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
            UnityEngine.Debug.Log("Mundo generado. Controles del jugador activados.");
        }
    }
    // --- FIN DE LA ADICI�N 4 ---

    // --- INICIO DE LA ADICI�N 5: La funci�n GetBlockAt que falta ---
    public BlockType GetBlockAt(Vector3 worldPosition)
    {
        // Extraemos las coordenadas enteras de la posici�n
        int worldX = Mathf.FloorToInt(worldPosition.x);
        int worldY = Mathf.FloorToInt(worldPosition.y);
        int worldZ = Mathf.FloorToInt(worldPosition.z);

        // El resto de la l�gica es la misma que ya ten�amos, pero usando estas variables
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