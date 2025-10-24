using System.Collections.Generic;
using UnityEngine;

namespace Voxya.Voxel.Unity
{
    // Pool simple de GameObjects (chunks) y Meshes para evitar GC
    internal static class ObjectPools
    {
        private static readonly Stack<GameObject> chunkPool = new();
        private static readonly Stack<Mesh> meshPool = new();

        public static GameObject RentChunk(GameObject prefab)
        {
            if (chunkPool.Count > 0)
            {
                var go = chunkPool.Pop();
                go.SetActive(true);
                return go;
            }
            return Object.Instantiate(prefab);
        }

        public static void ReturnChunk(GameObject go)
        {
            go.SetActive(false);
            chunkPool.Push(go);
        }

        public static Mesh RentMesh()
        {
            if (meshPool.Count > 0) return meshPool.Pop();
            var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            return m;
        }

        public static void ReturnMesh(Mesh m)
        {
            m.Clear();
            meshPool.Push(m);
        }
    }
}