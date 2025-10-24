using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Implementación de Greedy Meshing con winding corregido para evitar “huecos”
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

                        // Nota: negativo en mask => cara hacia “dentro” (backFace)
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

                        // Ancho
                        for (n = 1; i + n < dims[u] && mask[idx + n] == bt; n++) { }

                        // Alto
                        bool done = false;
                        for (m = 1; j + m < dims[v]; m++)
                        {
                            for (k = 0; k < n; k++)
                            {
                                if (mask[idx + k + m * dims[u]] != bt) { done = true; break; }
                            }
                            if (done) break;
                        }

                        // Marcar consumido
                        for (int jj = 0; jj < m; jj++)
                            for (int ii = 0; ii < n; ii++)
                                mask[idx + ii + jj * dims[u]] = BlockType.Air;

                        int[] p0 = { 0, 0, 0 };
                        p0[axis] = q[axis];
                        p0[u] = i; p0[v] = j;

                        bool backFace = ((int)bt) < 0;
                        BlockType mat = backFace ? (BlockType)(-(int)bt) : bt;

                        AppendQuad(md, cfg, p0[0], p0[1], p0[2],
                                   n, m, axis, backFace, mat);

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

        // Winding fijo + inversión de triángulos si backFace
        private static void AppendQuad(MeshData md, VoxelWorldConfig cfg,
                                       int px, int py, int pz,
                                       int width, int height,
                                       int axis, bool backFace, BlockType mat)
        {
            // Construcción en coordenadas de vóxel
            Vector3 origin = new(px * cfg.BlockSizeMeters, py * cfg.BlockSizeMeters, pz * cfg.BlockSizeMeters);

            // Ejes locales u/v según axis
            Vector3 u = axis switch
            {
                0 => new Vector3(0, 0, 1), // X: barrido en Z
                1 => new Vector3(1, 0, 0), // Y: barrido en X
                _ => new Vector3(1, 0, 0)  // Z: barrido en X
            };
            Vector3 v = axis switch
            {
                0 => new Vector3(0, 1, 0), // X: barrido en Y
                1 => new Vector3(0, 0, 1), // Y: barrido en Z
                _ => new Vector3(0, 1, 0)  // Z: barrido en Y
            };

            // Escalar u/v por tamaño del rectángulo y por tamaño de bloque
            u *= width * cfg.BlockSizeMeters;
            v *= height * cfg.BlockSizeMeters;

            // Para caras “positivas” desplazamos una unidad en el eje principal
            Vector3 normal = axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward
            };
            if (backFace) normal = -normal;

            // Desplazar el plano al lado correcto
            Vector3 planeOffset = (axis switch
            {
                0 => new Vector3(backFace ? 0 : cfg.BlockSizeMeters, 0, 0),
                1 => new Vector3(0, backFace ? 0 : cfg.BlockSizeMeters, 0),
                _ => new Vector3(0, 0, backFace ? 0 : cfg.BlockSizeMeters),
            });

            Vector3 p0 = origin + planeOffset;
            Vector3 p1 = p0 + u;
            Vector3 p2 = p0 + u + v;
            Vector3 p3 = p0 + v;

            int vbase = md.vertices.Count;
            md.vertices.Add(p0);
            md.vertices.Add(p1);
            md.vertices.Add(p2);
            md.vertices.Add(p3);

            // Winding: CCW = cara frontal. Si es backFace, invertimos
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

            // Normales planas
            md.normals.Add(normal); md.normals.Add(normal); md.normals.Add(normal); md.normals.Add(normal);

            // UVs (atlas 2x3)
            Vector2 uv = mat switch
            {
                BlockType.Grass => new Vector2(0, 2),
                BlockType.Dirt => new Vector2(1, 1),
                BlockType.Stone => new Vector2(1, 0),
                BlockType.Sand => new Vector2(0, 1),
                BlockType.Snow => new Vector2(0, 0),
                _ => new Vector2(1, 2)
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