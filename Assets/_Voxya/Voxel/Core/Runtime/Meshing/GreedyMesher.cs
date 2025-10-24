using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Greedy meshing (Lysenko) correcto:
    // - Slices 0..dims[d], comparando a = x - q y b = x.
    // - b != Air => cara "front" en plano x (normal +d)
    // - a != Air => cara "back"  en plano x-1 (normal -d)
    // - u y v = ejes restantes, du y dv se construyen en esos ejes (w y h).
    public class GreedyMesher : IMesher
    {
        public MeshData BuildMesh(VoxelChunkData c, VoxelWorldConfig cfg)
        {
            var md = new MeshData();
            GreedyAxis(md, c, cfg, 0); // X
            GreedyAxis(md, c, cfg, 1); // Y
            GreedyAxis(md, c, cfg, 2); // Z
            return md;
        }

        private void GreedyAxis(MeshData md, VoxelChunkData c, VoxelWorldConfig cfg, int d)
        {
            int N = c.Size;
            int H = c.Height;
            int[] dims = { N, H, N };

            int u = (d + 1) % 3; // eje horizontal de la máscara
            int v = (d + 2) % 3; // eje vertical de la máscara

            var mask = new BlockType[dims[u] * dims[v]];

            int[] x = { 0, 0, 0 };
            int[] q = { 0, 0, 0 };
            q[d] = 1;

            // Recorre slices 0..dims[d]
            for (x[d] = 0; x[d] <= dims[d]; x[d]++)
            {
                // Construir máscara
                int n = 0;
                for (x[v] = 0; x[v] < dims[v]; x[v]++)
                {
                    for (x[u] = 0; x[u] < dims[u]; x[u]++)
                    {
                        BlockType a = (x[d] > 0)
                            ? Sample(c, x[0] - q[0], x[1] - q[1], x[2] - q[2])
                            : BlockType.Air;
                        BlockType b = (x[d] < dims[d])
                            ? Sample(c, x[0], x[1], x[2])
                            : BlockType.Air;

                        if ((a != BlockType.Air) == (b != BlockType.Air))
                            mask[n++] = BlockType.Air;
                        else
                            mask[n++] = (b != BlockType.Air) ? b : (BlockType)(-(int)a);
                    }
                }

                // Consumir la máscara en rectángulos máximos
                n = 0;
                for (int j = 0; j < dims[v]; j++)
                {
                    for (int i = 0; i < dims[u];)
                    {
                        BlockType bt = mask[n];
                        if (bt == BlockType.Air) { i++; n++; continue; }

                        // Ancho en u
                        int w = 1;
                        while (i + w < dims[u] && mask[n + w] == bt) w++;

                        // Alto en v
                        int h = 1;
                        bool stop = false;
                        while (j + h < dims[v] && !stop)
                        {
                            for (int k = 0; k < w; k++)
                            {
                                if (mask[n + k + h * dims[u]] != bt) { stop = true; break; }
                            }
                            if (!stop) h++;
                        }

                        // Limpia el rectángulo consumido
                        for (int jj = 0; jj < h; jj++)
                            for (int ii = 0; ii < w; ii++)
                                mask[n + ii + jj * dims[u]] = BlockType.Air;

                        bool backFace = ((int)bt) < 0;
                        BlockType mat = backFace ? (BlockType)(-(int)bt) : bt;

                        // Plano correcto: front en x[d], back en x[d]-1
                        int plane = x[d] - (backFace ? 1 : 0);

                        AppendQuad(md, cfg, d, plane, u, v, i, j, w, h, backFace, mat);

                        i += w;
                        n += w;
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

        // Emite un quad en plano 'plane' del eje d; rectángulo (i..i+w, j..j+h) en ejes u y v.
        private static void AppendQuad(MeshData md, VoxelWorldConfig cfg,
                                       int d, int plane, int u, int v,
                                       int i, int j, int w, int h,
                                       bool backFace, BlockType mat)
        {
            float s = cfg.BlockSizeMeters;

            // pos[d]=plane; pos[u]=i; pos[v]=j
            int[] pos = { 0, 0, 0 };
            pos[d] = plane; pos[u] = i; pos[v] = j;

            Vector3 p = new Vector3(pos[0] * s, pos[1] * s, pos[2] * s);

            Vector3 du = Vector3.zero, dv = Vector3.zero;
            // du en eje u (w celdas)
            if (u == 0) du.x = w * s; else if (u == 1) du.y = w * s; else du.z = w * s;
            // dv en eje v (h celdas)
            if (v == 0) dv.x = h * s; else if (v == 1) dv.y = h * s; else dv.z = h * s;

            Vector3 v0 = p;
            Vector3 v1 = p + du;
            Vector3 v2 = p + du + dv;
            Vector3 v3 = p + dv;

            int vb = md.vertices.Count;
            md.vertices.Add(v0); md.vertices.Add(v1); md.vertices.Add(v2); md.vertices.Add(v3);

            // Normal ±d
            Vector3 nrm = Vector3.zero;
            if (d == 0) nrm.x = backFace ? -1f : 1f;
            else if (d == 1) nrm.y = backFace ? -1f : 1f;
            else nrm.z = backFace ? -1f : 1f;

            // Triángulos (front CCW, back invertido)
            if (!backFace)
            {
                md.triangles.Add(vb + 0); md.triangles.Add(vb + 1); md.triangles.Add(vb + 2);
                md.triangles.Add(vb + 0); md.triangles.Add(vb + 2); md.triangles.Add(vb + 3);
            }
            else
            {
                md.triangles.Add(vb + 0); md.triangles.Add(vb + 2); md.triangles.Add(vb + 1);
                md.triangles.Add(vb + 0); md.triangles.Add(vb + 3); md.triangles.Add(vb + 2);
            }

            md.normals.Add(nrm); md.normals.Add(nrm); md.normals.Add(nrm); md.normals.Add(nrm);

            // UVs (atlas 2x3)
            Vector2 cell = mat switch
            {
                BlockType.Grass => new Vector2(0, 2),
                BlockType.Dirt => new Vector2(1, 1),
                BlockType.Stone => new Vector2(1, 0),
                BlockType.Sand => new Vector2(0, 1),
                BlockType.Snow => new Vector2(0, 0),
                _ => new Vector2(1, 2)
            };
            const float invW = 0.5f, invH = 1f / 3f;
            float ux = cell.x * invW, uy = cell.y * invH;

            md.uvs.Add(new Vector2(ux, uy));
            md.uvs.Add(new Vector2(ux + invW, uy));
            md.uvs.Add(new Vector2(ux + invW, uy + invH));
            md.uvs.Add(new Vector2(ux, uy + invH));
        }
    }
}