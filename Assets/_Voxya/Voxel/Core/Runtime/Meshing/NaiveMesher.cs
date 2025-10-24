using System.Collections.Generic;
using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Mesher naive (genera una quad por cara expuesta de cada vóxel no-Air).
    // Úsalo para depurar: si esto se ve “volumétrico” correcto, el bug está en el GreedyMesher.
    public class NaiveMesher : IMesher
    {
        static readonly Vector3Int[] NeighborDir = {
            new( 1,  0,  0), new(-1,  0,  0),
            new( 0,  1,  0), new( 0, -1,  0),
            new( 0,  0,  1), new( 0,  0, -1),
        };
        static readonly Vector3[] FaceNormal = {
            Vector3.right, Vector3.left,
            Vector3.up,    Vector3.down,
            Vector3.forward, Vector3.back
        };

        public MeshData BuildMesh(VoxelChunkData c, VoxelWorldConfig cfg)
        {
            var md = new MeshData();
            float s = cfg.BlockSizeMeters;

            for (int x = 0; x < c.Size; x++)
            for (int y = 0; y < c.Height; y++)
            for (int z = 0; z < c.Size; z++)
            {
                BlockType bt = c.Get(x, y, z);
                if (bt == BlockType.Air) continue;

                for (int f = 0; f < 6; f++)
                {
                    var n = NeighborDir[f];
                    int nx = x + n.x, ny = y + n.y, nz = z + n.z;
                    BlockType nb = (nx < 0 || nx >= c.Size || ny < 0 || ny >= c.Height || nz < 0 || nz >= c.Size)
                        ? BlockType.Air
                        : c.Get(nx, ny, nz);

                    if (nb != BlockType.Air) continue; // cara no expuesta

                    // Construir quad en la cara f del voxel (x,y,z)
                    AddFace(md, cfg, x, y, z, f, bt);
                }
            }

            return md;
        }

        private static void AddFace(MeshData md, VoxelWorldConfig cfg, int x, int y, int z, int face, BlockType mat)
        {
            float s = cfg.BlockSizeMeters;
            Vector3 basePos = new Vector3(x * s, y * s, z * s);

            // Vértices de un cubo unidad y cara seleccionada
            // Cada cara en CCW mirando hacia la normal
            Vector3[] quad = new Vector3[4];
            switch (face)
            {
                case 0: // +X
                    quad[0] = basePos + new Vector3(s, 0, 0);
                    quad[1] = basePos + new Vector3(s, 0, s);
                    quad[2] = basePos + new Vector3(s, s, s);
                    quad[3] = basePos + new Vector3(s, s, 0);
                    break;
                case 1: // -X
                    quad[0] = basePos + new Vector3(0, 0, s);
                    quad[1] = basePos + new Vector3(0, 0, 0);
                    quad[2] = basePos + new Vector3(0, s, 0);
                    quad[3] = basePos + new Vector3(0, s, s);
                    break;
                case 2: // +Y
                    quad[0] = basePos + new Vector3(0, s, 0);
                    quad[1] = basePos + new Vector3(s, s, 0);
                    quad[2] = basePos + new Vector3(s, s, s);
                    quad[3] = basePos + new Vector3(0, s, s);
                    break;
                case 3: // -Y
                    quad[0] = basePos + new Vector3(0, 0, s);
                    quad[1] = basePos + new Vector3(s, 0, s);
                    quad[2] = basePos + new Vector3(s, 0, 0);
                    quad[3] = basePos + new Vector3(0, 0, 0);
                    break;
                case 4: // +Z
                    quad[0] = basePos + new Vector3(0, 0, s);
                    quad[1] = basePos + new Vector3(0, s, s);
                    quad[2] = basePos + new Vector3(s, s, s);
                    quad[3] = basePos + new Vector3(s, 0, s);
                    break;
                case 5: // -Z
                    quad[0] = basePos + new Vector3(s, 0, 0);
                    quad[1] = basePos + new Vector3(s, s, 0);
                    quad[2] = basePos + new Vector3(0, s, 0);
                    quad[3] = basePos + new Vector3(0, 0, 0);
                    break;
            }

            int vbase = md.vertices.Count;
            md.vertices.AddRange(quad);

            // Triángulos CCW
            md.triangles.Add(vbase + 0);
            md.triangles.Add(vbase + 1);
            md.triangles.Add(vbase + 2);
            md.triangles.Add(vbase + 0);
            md.triangles.Add(vbase + 2);
            md.triangles.Add(vbase + 3);

            // Normales planas
            Vector3 nrm = FaceNormal[face];
            md.normals.Add(nrm); md.normals.Add(nrm); md.normals.Add(nrm); md.normals.Add(nrm);

            // UVs (atlas 2x3)
            Vector2 uv = mat switch
            {
                BlockType.Grass => new Vector2(0, 2),
                BlockType.Dirt  => new Vector2(1, 1),
                BlockType.Stone => new Vector2(1, 0),
                BlockType.Sand  => new Vector2(0, 1),
                BlockType.Snow  => new Vector2(0, 0),
                _               => new Vector2(1, 2)
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