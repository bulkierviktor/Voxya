using UnityEngine;

namespace Voxya.Voxel.Core
{
    public class PlainsBiome : IBiome
    {
        public string Id => "plains";
        public float HeightMul => 0.65f;
        public float BaseScaleMeters => 50f;
        public int FbmOctaves => 5;
        public float FbmLacunarity => 2.0f;
        public float FbmGain => 0.5f;
        public float WarpScaleMeters => 120f;
        public float WarpStrengthMeters => 8f;
        public int SmoothIterations => 3;
        public float SmoothFactor => 0.6f;
        public BlockType TopBlock(Vector3 worldMeters) => BlockType.Grass;
    }
}