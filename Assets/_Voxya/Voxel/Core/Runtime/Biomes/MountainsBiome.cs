using UnityEngine;

namespace Voxya.Voxel.Core
{
    public class MountainsBiome : IBiome
    {
        public string Id => "mountains";
        public float HeightMul => 1.35f;
        public float BaseScaleMeters => 40f;
        public int FbmOctaves => 6;
        public float FbmLacunarity => 2.1f;
        public float FbmGain => 0.47f;
        public float WarpScaleMeters => 90f;
        public float WarpStrengthMeters => 14f;
        public int SmoothIterations => 1;
        public float SmoothFactor => 0.4f;
        public BlockType TopBlock(Vector3 worldMeters) => (worldMeters.y > 16f) ? BlockType.Snow : BlockType.Stone;
    }
}