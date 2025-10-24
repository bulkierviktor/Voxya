using UnityEngine;

namespace Voxya.Voxel.Core
{
    public class DesertBiome : IBiome
    {
        public string Id => "desert";
        public float HeightMul => 0.8f;
        public float BaseScaleMeters => 65f;
        public int FbmOctaves => 5;
        public float FbmLacunarity => 2.0f;
        public float FbmGain => 0.55f;
        public float WarpScaleMeters => 160f;
        public float WarpStrengthMeters => 10f;
        public int SmoothIterations => 2;
        public float SmoothFactor => 0.55f;
        public BlockType TopBlock(Vector3 worldMeters) => BlockType.Sand;
    }
}