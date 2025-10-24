namespace Voxya.Voxel.Core
{
    // Convierte voxeles en MeshData
    public interface IMesher
    {
        MeshData BuildMesh(VoxelChunkData chunk, VoxelWorldConfig cfg);
    }
}