using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Elige bioma para una coordenada del mundo
    public interface IBiomeProvider
    {
        IBiome GetBiomeAt(float worldXMeters, float worldZMeters);
    }
}