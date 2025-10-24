using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Greedy meshing (Lysenko) con plano correcto por cara:
    // - Front (a!=Air, b==Air) en q+1
    // - Back  (a==Air, b!=Air) en q
    // y con winding/normales consistentes.
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

        private void GreedyAxis(MeshData md, VoxelChunkData c, VoxelWorldConfig cfg, int axis)
        {
            int N = c.Size;
            int H = c.Height;
            int[] dims = { N, H, N };

            int u = (axis + 1) % 3;
            int v = (axis + 2) % 3;

            var mask = new BlockType[dims[u] * dims[v]];
            int[] q = { 0, 0, 0 };
            q[axis] = 1;

            for (int xq = -1; xq < dims[axis];)
            {
                // Construye máscara entre el slice xq y xq+1
                int idx = 0;
                for (int j = 0; j < dims[v]; j++)
                {
                    for (int i = 0; i < dims[u]; i++)
                    {
                        int[] p = { 0, 0, 0 };
                        p[axis] = xq; p[u] = i; p[v] = j;

                        BlockType a = Sample(c, p[0], p[1], p[2]);
                        BlockType b = Sample(c, p[0] + q[0], p[1] + q[1], p[2] + q[2]);

                        // >0 = front (+axis), <0 = back (-axis)
                        mask[idx++] =
                            (a != BlockType.Air && b == BlockType.Air) ? a :
                            (a == BlockType.Air && b != BlockType.Air) ? (BlockType)(-(int)b) :
                            BlockType.Air;
                    }
                }

                // Avanza el slice; las caras "front" van en xq (ya incrementado), "back" en xq-1
                xq++;

                // Consume rectángulos máximos en la máscara
                idx = 0;
                for (int j = 0; j < dims[v]; j++)
                {
                    for (int i = 0; i < dims[u];)
                    {
                        BlockType bt = mask[idx];
                        if (bt == BlockType.Air) { i++; idx++; continue; }

                        int w;
                        for (w = 1; i + w < dims[u] && mask[idx + w] == bt; w++) { }

                        int h;
                        bool stop = false;
                        for (h = 1; j + h < dims[v]; h++)
                        {
                            for (int k = 0; k < w; k++)
                                if (mask[idx + k + h * dims[u]] != bt) { stop = true; break; }
                            if (stop) break;
                        }

                        // Limpia la máscara del rectángulo consumido
                        for (int jj = 0; jj < h; jj++)
                            for (int ii = 0; ii < w; ii++)
                                mask[idx + ii + jj * dims[u]] = BlockType.Air;

                        bool backFace = ((int)bt) < 0;
                        BlockType mat = backFace ? (BlockType)(-(int)bt) : bt;

                        // Plano correcto según front/back
                        int qPlane = xq - (backFace ? 1 : 0);

                        AppendQuad(md, cfg, axis, qPlane, i, j, w, h, backFace, mat);

                        i += w;
                        idx += w;
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

        // Emite un quad en el plano 'qPlane' del 'axis', con rectángulo (i..i+w, j..j+h) en ejes u/v.
        private static void AppendQuad(MeshData md, VoxelWorldConfig cfg,
                                       int axis, int qPlane, int i, int j, int w, int h,
                                       bool backFace, BlockType mat)
        {
            float s = cfg.BlockSizeMeters;

            // axis X: plano x = q; u=z, v=y  -> p=(q,j,i), du=(0,0,w), dv=(0,h,0)
            // axis Y: plano y = q; u=x, v=z  -> p=(i,q,j), du=(w,0,0), dv=(0,0,h)
            // axis Z: plano z = q; u=x, v=y  -> p=(i,j,q), du=(w,0,0), dv=(0,h,0)
            Vector3 p, du, dv, nrm;
            switch (axis)
            {
                case 0:
                    p = new Vector3(qPlane, j, i);
                    du = new Vector3(0, 0, w);
                    dv = new Vector3(0, h, 0);
                    nrm = backFace ? Vector3.left : Vector3.right;
                    break;
                case 1:
                    p = new Vector3(i, qPlane, j);
                    du = new Vector3(w, 0, 0);
                    dv = new Vector3(0, 0, h);
                    nrm = backFace ? Vector3.down : Vector3.up;
                    break;
                default:
                    p = new Vector3(i, j, qPlane);
                    du = new Vector3(w, 0, 0);
                    dv = new Vector3(0, h, 0);
                    nrm = backFace ? Vector3.back : Vector3.forward;
                    break;
            }

            p *= s; du *= s; dv *= s;

            Vector3 v0 = p;
            Vector3 v1 = p + du;
            Vector3 v2 = p + du + dv;
            Vector3 v3 = p + dv;

            int vb = md.vertices.Count;
            md.vertices.Add(v0); md.vertices.Add(v1); md.vertices.Add(v2); md.vertices.Add(v3);

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