using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Coordenadas de chunk (X,Z). Inmutables y comparables
    public readonly struct ChunkCoord
    {
        public readonly int x;
        public readonly int z;
        public ChunkCoord(int x, int z) { this.x = x; this.z = z; }

        public Vector3 ToWorldPositionMeters(VoxelWorldConfig cfg)
        {
            float sizeMeters = cfg.ChunkSize * cfg.BlockSizeMeters;
            return new Vector3(x * sizeMeters, 0f, z * sizeMeters);
        }

        public override string ToString() => $"({x},{z})";
    }
}