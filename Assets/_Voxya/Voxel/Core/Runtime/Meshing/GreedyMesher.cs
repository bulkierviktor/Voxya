using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Greedy Meshing con winding correcto y plano de la cara en q (sin offset +1 bloque)
    public class GreedyMesher : IMesher
    {
        public MeshData BuildMesh(VoxelChunkData c, VoxelWorldConfig cfg)
        {
            var md = new MeshData();
            GreedyAxis(md, c, cfg, axis: 0); // X
            GreedyAxis(md, c, cfg, axis: 1); // Y
            GreedyAxis(md, c, cfg, axis: 2); // Z
            return md;
        }

        private void GreedyAxis(MeshData md, VoxelChunkData c, VoxelWorldConfig cfg, int axis)
        {
            int N = c.Size;
            int H = c.Height;
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            int[] dims = { N, H, N };
            var mask = new BlockType[dims[u] * dims[v]];

            int i, j, k, n, m;

            int[] q = { 0, 0, 0 };
            q[axis] = 1;

            for (q[axis] = -1; q[axis] < dims[axis];)
            {
                int idx = 0;
                for (j = 0; j < dims[v]; j++)
                {
                    for (i = 0; i < dims[u]; i++)
                    {
                        int[] p = { 0, 0, 0 };
                        p[axis] = q[axis];
                        p[u] = i; p[v] = j;

                        BlockType va = Sample(c, p[0], p[1], p[2]);
                        BlockType vb = Sample(c, p[0] + q[0], p[1] + q[1], p[2] + q[2]);

                        // positivo => cara hacia +axis (frente); negativo => cara hacia -axis (backFace)
                        mask[idx++] = (va != BlockType.Air && vb == BlockType.Air) ? va :
                                      (vb != BlockType.Air && va == BlockType.Air) ? (BlockType)(-(int)vb) :
                                      BlockType.Air;
                    }
                }

                q[axis]++;

                idx = 0;
                for (j = 0; j < dims[v]; j++)
                {
                    for (i = 0; i < dims[u];)
                    {
                        BlockType bt = mask[idx];
                        if (bt == BlockType.Air) { i++; idx++; continue; }

                        // Ancho (u)
                        for (n = 1; i + n < dims[u] && mask[idx + n] == bt; n++) { }

                        // Alto (v)
                        bool done = false;
                        for (m = 1; j + m < dims[v]; m++)
                        {
                            for (k = 0; k < n; k++)
                            {
                                if (mask[idx + k + m * dims[u]] != bt) { done = true; break; }
                            }
                            if (done) break;
                        }

                        // Consumir bloque
                        for (int jj = 0; jj < m; jj++)
                            for (int ii = 0; ii < n; ii++)
                                mask[idx + ii + jj * dims[u]] = BlockType.Air;

                        int[] p0 = { 0, 0, 0 };
                        p0[axis] = q[axis]; // el plano está en q (sin offset extra)
                        p0[u] = i; p0[v] = j;

                        bool backFace = ((int)bt) < 0;
                        BlockType mat = backFace ? (BlockType)(-(int)bt) : bt;

                        AppendQuad(md, cfg, p0[0], p0[1], p0[2], n, m, axis, backFace, mat);

                        i += n; idx += n;
                    }
                }
            }
        }

        private static BlockType Sample(VoxelChunkData c, int x, int y, int z)
        {
            if (x < 0 || x >= c.Size || y < 0 || y >= c.Height || z < 0 || z >= c.Size)
                return BlockType.Air;
            return c.Get(x, y, z);
        }

        // Construcción del quad: plano en q, du/dv en ejes u/v; sin offset +1 bloque
        private static void AppendQuad(MeshData md, VoxelWorldConfig cfg,
                                       int px, int py, int pz,
                                       int width, int height,
                                       int axis, bool backFace, BlockType mat)
        {
            float s = cfg.BlockSizeMeters;

            // origen del plano en coordenadas mundo
            Vector3 origin = new Vector3(px * s, py * s, pz * s);

            // du en eje u, dv en eje v
            Vector3 du = Vector3.zero;
            Vector3 dv = Vector3.zero;
            switch ((axis + 1) % 3) // u
            {
                case 0: du.x = width * s; break;
                case 1: du.y = width * s; break;
                case 2: du.z = width * s; break;
            }
            switch ((axis + 2) % 3) // v
            {
                case 0: dv.x = height * s; break;
                case 1: dv.y = height * s; break;
                case 2: dv.z = height * s; break;
            }

            // normal hacia +axis o -axis
            Vector3 normal = Vector3.zero;
            switch (axis)
            {
                case 0: normal.x = backFace ? -1f : 1f; break;
                case 1: normal.y = backFace ? -1f : 1f; break;
                case 2: normal.z = backFace ? -1f : 1f; break;
            }

            // vértices (orden p0, p1, p2, p3)
            Vector3 p0 = origin;
            Vector3 p1 = origin + du;
            Vector3 p2 = origin + du + dv;
            Vector3 p3 = origin + dv;

            int vbase = md.vertices.Count;
            md.vertices.Add(p0);
            md.vertices.Add(p1);
            md.vertices.Add(p2);
            md.vertices.Add(p3);

            // winding CCW para cara frontal; invertido si backFace
            if (!backFace)
            {
                md.triangles.Add(vbase + 0);
                md.triangles.Add(vbase + 1);
                md.triangles.Add(vbase + 2);
                md.triangles.Add(vbase + 0);
                md.triangles.Add(vbase + 2);
                md.triangles.Add(vbase + 3);
            }
            else
            {
                md.triangles.Add(vbase + 0);
                md.triangles.Add(vbase + 2);
                md.triangles.Add(vbase + 1);
                md.triangles.Add(vbase + 0);
                md.triangles.Add(vbase + 3);
                md.triangles.Add(vbase + 2);
            }

            // normales planas
            md.normals.Add(normal); md.normals.Add(normal); md.normals.Add(normal); md.normals.Add(normal);

            // UVs (atlas 2x3)
            Vector2 uv = mat switch
            {
                BlockType.Grass => new Vector2(0, 2),
                BlockType.Dirt => new Vector2(1, 1),
                BlockType.Stone => new Vector2(1, 0),
                BlockType.Sand => new Vector2(0, 1),
                BlockType.Snow => new Vector2(0, 0),
                _ => new Vector2(1, 2) // Extra
            };
            const float invW = 0.5f, invH = 1f / 3f;
            float ux = uv.x * invW, uy = uv.y * invH;

            md.uvs.Add(new Vector2(ux, uy));
            md.uvs.Add(new Vector2(ux + invW, uy));
            md.uvs.Add(new Vector2(ux + invW, uy + invH));
            md.uvs.Add(new Vector2(ux, uy + invH));
        }
    }
}