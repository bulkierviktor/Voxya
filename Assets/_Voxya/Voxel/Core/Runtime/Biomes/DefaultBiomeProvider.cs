using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Elegimos biomas por bandas latitudinales con ruido de mezcla (simple y extensible)
    public class DefaultBiomeProvider : IBiomeProvider
    {
        private readonly IVoxelNoise2D selector;
        private readonly float scale; // metros

        private readonly IBiome plains = new PlainsBiome();
        private readonly IBiome desert = new DesertBiome();
        private readonly IBiome mountains = new MountainsBiome();

        public DefaultBiomeProvider(int seed, float scaleMeters = 600f)
        {
            selector = new OpenSimplex2D();
            selector.SetSeed(seed * 73856093 ^ 0x9E3779B);
            scale = Mathf.Max(50f, scaleMeters);
        }

        public IBiome GetBiomeAt(float worldXMeters, float worldZMeters)
        {
            float t = selector.Fbm01(worldXMeters / scale, worldZMeters / scale, 3, 2f, 0.5f);
            // Mezcla simple: 0..0.33 desierto, 0.33..0.66 pradera, 0.66..1 montañas
            if (t < 0.33f) return desert;
            if (t < 0.66f) return plains;
            return mountains;
        }
    }
}