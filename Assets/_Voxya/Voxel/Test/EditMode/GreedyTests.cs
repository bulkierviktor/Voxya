using NUnit.Framework;
using Voxya.Voxel.Core;

public class GreedyTests
{
    [Test]
    public void Greedy_GeneratesQuads()
    {
        var cfg = UnityEngine.ScriptableObject.CreateInstance<VoxelWorldConfig>();
        var chunk = new VoxelChunkData(cfg.ChunkSize, cfg.ChunkHeight);
        // Crea un cubo 2x2x2 sólido
        for (int x = 0; x < 2; x++)
        for (int y = 0; y < 2; y++)
        for (int z = 0; z < 2; z++)
            chunk.Set(x, y, z, BlockType.Stone);

        var md = new GreedyMesher().BuildMesh(chunk, cfg);
        Assert.Greater(md.triangles.Count, 0);
    }
}