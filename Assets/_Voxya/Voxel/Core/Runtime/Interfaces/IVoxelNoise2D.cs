using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Contrato genérico para fuentes de ruido 2D seedables
    public interface IVoxelNoise2D
    {
        void SetSeed(int seed);
        // Devuelve [0,1]
        float Sample01(float x, float z);
        // fbm con octavas
        float Fbm01(float x, float z, int octaves, float lacunarity, float gain);
    }
}