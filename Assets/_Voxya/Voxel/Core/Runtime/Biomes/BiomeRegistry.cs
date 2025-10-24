using System.Collections.Generic;

namespace Voxya.Voxel.Core
{
    // Registro simple en memoria. Se podría mover a ScriptableObject si lo prefieres.
    public static class BiomeRegistry
    {
        private static readonly Dictionary<string, IBiome> map = new();

        static BiomeRegistry()
        {
            Register(new PlainsBiome());
            Register(new DesertBiome());
            Register(new MountainsBiome());
        }

        public static void Register(IBiome biome) => map[biome.Id] = biome;

        public static IEnumerable<IBiome> All()
        {
            foreach (var b in map.Values) yield return b;
        }
    }
}