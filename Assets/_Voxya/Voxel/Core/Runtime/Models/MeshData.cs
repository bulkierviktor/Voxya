using System.Collections.Generic;
using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Datos de malla sin objetos temporales al aplicar
    public sealed class MeshData
    {
        public readonly List<Vector3> vertices = new(4096);
        public readonly List<int> triangles = new(8192);
        public readonly List<Vector3> normals = new(4096);
        public readonly List<Vector2> uvs = new(4096);

        public void Clear()
        {
            vertices.Clear();
            triangles.Clear();
            normals.Clear();
            uvs.Clear();
        }
    }
}