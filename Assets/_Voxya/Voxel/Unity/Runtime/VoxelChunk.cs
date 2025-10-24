using UnityEngine;
using Voxya.Voxel.Core;

namespace Voxya.Voxel.Unity
{
    // Componente que sostiene la malla del chunk y su collider
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
    public class VoxelChunk : MonoBehaviour
    {
        private MeshFilter mf;
        private MeshCollider mc;

        void Awake()
        {
            mf = GetComponent<MeshFilter>();
            mc = GetComponent<MeshCollider>();
        }

        // Aplica una MeshData en el main thread usando Mesh del pool
        public void ApplyMesh(MeshData data, bool enableCollider)
        {
            var m = ObjectPools.RentMesh();
            m.SetVertices(data.vertices);
            m.SetTriangles(data.triangles, 0, true);
            m.SetNormals(data.normals);
            m.SetUVs(0, data.uvs);
            m.RecalculateBounds();

            // Devuelve el mesh previo al pool
            if (mf.sharedMesh != null) ObjectPools.ReturnMesh(mf.sharedMesh);
            mf.sharedMesh = m;

            if (mc != null)
            {
                mc.enabled = enableCollider;
                mc.sharedMesh = enableCollider ? m : null;
            }
        }
    }
}