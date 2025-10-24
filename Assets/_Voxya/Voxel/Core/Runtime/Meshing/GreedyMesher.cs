using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Implementación compacta de Greedy Meshing para bloques cúbicos
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

        // Algoritmo: por cada eje, barrer slices 2D, fusionar rectángulos de caras contiguas con mismo material
        private void GreedyAxis(MeshData md, VoxelChunkData c, VoxelWorldConfig cfg, int axis)
        {
            int N = c.Size;
            int H = c.Height;
            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            int[] dims = { N, H, N };
            int duX = 0, duY = 0, duZ = 0;
            int dvX = 0, dvY = 0, dvZ = 0;

            var mask = new BlockType[dims[u] * dims[v]];

            int x, y, z, i, j, k, n, m;

            int[] q = { 0, 0, 0 };
            q[axis] = 1;

            for (q[axis] = -1; q[axis] < dims[axis];)
            {
                int idx = 0;
                for (j = 0; j < dims[v]; j++)
                {
                    for (i = 0; i < dims[u]; i++)
                    {
                        BlockType a, b;

                        int[] p = { 0, 0, 0 };
                        p[axis] = q[axis];
                        p[u] = i; p[v] = j;

                        BlockType va = Sample(c, p[0], p[1], p[2]);
                        BlockType vb = Sample(c, p[0] + q[0], p[1] + q[1], p[2] + q[2]);

                        a = va; b = vb;
                        mask[idx++] = (a != BlockType.Air && b == BlockType.Air) ? a :
                                      (b != BlockType.Air && a == BlockType.Air) ? (BlockType)(-(int)b) :
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

                        // Calcular ancho
                        for (n = 1; i + n < dims[u] && mask[idx + n] == bt; n++) { }

                        // Calcular alto
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
                        {
                            for (int ii = 0; ii < n; ii++)
                                mask[idx + ii + jj * dims[u]] = BlockType.Air;
                        }

                        // Construir quad
                        duX = duY = duZ = 0; dvX = dvY = dvZ = 0;
                        if (u == 0) duX = n; else if (u == 1) duY = n; else duZ = n;
                        if (v == 0) dvX = m; else if (v == 1) dvY = m; else dvZ = m;

                        int[] p0 = { 0, 0, 0 };
                        p0[axis] = q[axis];
                        p0[u] = i; p0[v] = j;

                        bool backFace = ((int)bt) < 0;
                        BlockType mat = backFace ? (BlockType)(-(int)bt) : bt;

                        AppendQuad(md, cfg, p0[0], p0[1], p0[2],
                                   duX, duY, duZ,
                                   dvX, dvY, dvZ,
                                   axis, backFace, mat);

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

        private static void AppendQuad(MeshData md, VoxelWorldConfig cfg, int x, int y, int z,
                                       int duX, int duY, int duZ,
                                       int dvX, int dvY, int dvZ,
                                       int axis, bool backFace, BlockType mat)
        {
            // Convertir a metros
            Vector3 origin = new(x * cfg.BlockSizeMeters, y * cfg.BlockSizeMeters, z * cfg.BlockSizeMeters);
            Vector3 du = new(duX * cfg.BlockSizeMeters, duY * cfg.BlockSizeMeters, duZ * cfg.BlockSizeMeters);
            Vector3 dv = new(dvX * cfg.BlockSizeMeters, dvY * cfg.BlockSizeMeters, dvZ * cfg.BlockSizeMeters);

            int vbase = md.vertices.Count;

            if (!backFace)
            {
                md.vertices.Add(origin);
                md.vertices.Add(origin + du);
                md.vertices.Add(origin + du + dv);
                md.vertices.Add(origin + dv);
            }
            else
            {
                md.vertices.Add(origin);
                md.vertices.Add(origin + dv);
                md.vertices.Add(origin + du + dv);
                md.vertices.Add(origin + du);
            }

            // Triángulos
            md.triangles.Add(vbase + 0);
            md.triangles.Add(vbase + 1);
            md.triangles.Add(vbase + 2);
            md.triangles.Add(vbase + 0);
            md.triangles.Add(vbase + 2);
            md.triangles.Add(vbase + 3);

            // Normales planas
            Vector3 n = axis switch
            {
                0 => (backFace ? Vector3.left : Vector3.right),
                1 => (backFace ? Vector3.down : Vector3.up),
                _ => (backFace ? Vector3.back : Vector3.forward),
            };
            md.normals.Add(n); md.normals.Add(n); md.normals.Add(n); md.normals.Add(n);

            // UVs muy simples por material (atlas 2x3 ejemplo)
            Vector2 uv = mat switch
            {
                BlockType.Grass => new Vector2(0, 2),
                BlockType.Dirt => new Vector2(1, 1),
                BlockType.Stone => new Vector2(1, 0),
                BlockType.Sand => new Vector2(0, 1),
                BlockType.Snow => new Vector2(0, 0),
                _ => new Vector2(1, 1)
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