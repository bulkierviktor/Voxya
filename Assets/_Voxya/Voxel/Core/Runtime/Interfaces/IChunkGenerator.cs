namespace Voxya.Voxel.Core
{
    // Rellena un buffer de voxeles de un chunk con datos sólidos/aire
    public interface IChunkGenerator
    {
        void GenerateChunkVoxels(ChunkCoord coord, VoxelChunkData chunk, VoxelWorldConfig cfg, IBiomeProvider biomeProvider, IVoxelNoise2D noise);
    }
}