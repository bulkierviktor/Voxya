using System.Threading.Tasks;

namespace Voxya.Voxel.Core
{
    public interface IChunkStorage
    {
        Task<bool> TryLoad(ChunkCoord coord, VoxelWorldConfig cfg, VoxelChunkData into);
        Task Save(ChunkCoord coord, VoxelWorldConfig cfg, VoxelChunkData from);
    }
}