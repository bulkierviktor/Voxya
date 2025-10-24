using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Describe cómo obtener altura base y modulación del bioma
    public interface IBiome
    {
        string Id { get; }
        // Factor multiplicador de altura (1 = sin cambios)
        float HeightMul { get; }
        // Config de ruido base del bioma (escala metros)
        float BaseScaleMeters { get; }
        int FbmOctaves { get; }
        float FbmLacunarity { get; }
        float FbmGain { get; }
        // Domain warp ligero (metros)
        float WarpScaleMeters { get; }
        float WarpStrengthMeters { get; }
        // Suavizado local (0..1)
        int SmoothIterations { get; }
        float SmoothFactor { get; }
        // Elegir bloque superior (grass, sand, snow...) — simple por ahora
        BlockType TopBlock(Vector3 worldMeters);
    }
}