using System.Collections; // ¡Importante! Necesitamos este namespace para las Corrutinas
using System.Collections.Generic;
using UnityEngine;

public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator instance;

    [Header("World Settings")]
    public int worldSizeInChunks = 10;

    [Header("Chunk Prefab")]
    public GameObject chunkPrefab;

    public Dictionary<Vector3, Chunk> chunks = new Dictionary<Vector3, Chunk>();

    private void Awake()
    {
        if (instance == null) { instance = this; }
        else { Destroy(gameObject); }
    }

    void Start()
    {
        // En lugar de llamar directamente a la función, iniciamos la corrutina.
        StartCoroutine(GenerateWorldCoroutine());
    }

    // El tipo de retorno ahora es IEnumerator, que es lo que usan las corrutinas.
    IEnumerator GenerateWorldCoroutine()
    {
        for (int x = 0; x < worldSizeInChunks; x++)
        {
            for (int z = 0; z < worldSizeInChunks; z++)
            {
                Vector3 chunkPosition = new Vector3(x * Chunk.chunkSize, 0, z * Chunk.chunkSize);
                Chunk newChunk = Instantiate(chunkPrefab, chunkPosition, Quaternion.identity, this.transform).GetComponent<Chunk>();
                chunks.Add(chunkPosition, newChunk);

                // --- ¡LA LÍNEA CLAVE! ---
                // Pausa la ejecución aquí y le devuelve el control a Unity.
                // Reanudará en el siguiente fotograma desde este mismo punto.
                yield return null;
            }
        }
    }
}