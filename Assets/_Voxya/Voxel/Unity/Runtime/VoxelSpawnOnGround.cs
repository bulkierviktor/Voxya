using System.Collections;
using UnityEngine;

// Coloca al Player sobre el terreno voxel al iniciar, esperando a que existan colliders.
[DisallowMultipleComponent]
public class VoxelSpawnOnGround : MonoBehaviour
{
    [Header("B�squeda de suelo")]
    [Tooltip("Altura desde la que lanzamos el raycast hacia abajo al empezar.")]
    public float startHeight = 250f;
    [Tooltip("Altura extra para dejar al jugador un poco por encima del suelo.")]
    public float extraUp = 2f;
    [Tooltip("M�scara de capas que consideramos como suelo (deja ~0 para 'todas').")]
    public LayerMask groundMask = ~0;

    [Header("Control")]
    [Tooltip("Tiempo m�ximo esperando a que existan colliders bajo el jugador (segundos).")]
    public float maxWaitSeconds = 5f;
    [Tooltip("Intervalo entre intentos de raycast mientras esperamos terreno (segundos).")]
    public float checkInterval = 0.05f;

    void Start()
    {
        StartCoroutine(PlaceOnGroundRoutine());
    }

    private IEnumerator PlaceOnGroundRoutine()
    {
        // Desactiva CC temporalmente para evitar que caiga mientras buscamos suelo
        var cc = GetComponent<CharacterController>();
        bool hadCC = cc != null && cc.enabled;
        if (cc != null) cc.enabled = false;

        // Tambi�n intenta �silenciar� cualquier rigidbody si lo hubiera (no deber�a con CC)
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.linearVelocity = Vector3.zero; }

        float deadline = Time.realtimeSinceStartup + Mathf.Max(0.1f, maxWaitSeconds);
        Vector3 origin = transform.position;
        origin.y = startHeight;

        bool placed = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            if (Physics.Raycast(origin, Vector3.down, out var hit, startHeight * 2f, groundMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * extraUp;
                placed = true;
                break;
            }
            yield return new WaitForSeconds(checkInterval);
        }

        if (!placed)
        {
            // Segundo intento: subimos m�s y reintentamos unos frames
            origin.y = startHeight * 2f;
            for (int i = 0; i < 60; i++)
            {
                if (Physics.Raycast(origin, Vector3.down, out var hit, origin.y * 2f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    transform.position = hit.point + Vector3.up * extraUp;
                    placed = true;
                    break;
                }
                yield return null;
            }
        }

        if (!placed)
        {
            UnityEngine.Debug.LogWarning("[VoxelSpawnOnGround] No se detect� suelo tras el tiempo de espera. �ColliderDistance demasiado bajo? �ChunkPrefab sin MeshCollider? Dejando posici�n actual.");
        }

        // Reactiva CC y/o RB
        if (cc != null) cc.enabled = hadCC;
        if (rb != null) rb.isKinematic = true; // mantenlo kinematic si hay CC

        yield break;
    }
}