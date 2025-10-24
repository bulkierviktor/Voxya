using NUnit.Framework;
using UnityEngine;
using Voxya.Voxel.Core;

// Alias para evitar colisiones con otros BlockType del proyecto
using CoreBlock = Voxya.Voxel.Core.BlockType;

public class GreedyTests
{
    [Test]
    public void Greedy_GeneratesQuads()
    {
        var cfg = ScriptableObject.CreateInstance<VoxelWorldConfig>();
        // Si quieres fijar parámetros para el test:
        // cfg.ChunkSize = 16;
        // cfg.ChunkHeight = 32;

        var chunk = new VoxelChunkData(cfg.ChunkSize, cfg.ChunkHeight);

        // Crea un cubo 2x2x2 sólido con el enum del módulo (CoreBlock)
        for (int x = 0; x < 2; x++)
            for (int y = 0; y < 2; y++)
                for (int z = 0; z < 2; z++)
                    chunk.Set(x, y, z, CoreBlock.Stone);

        var md = new GreedyMesher().BuildMesh(chunk, cfg);
        Assert.Greater(md.triangles.Count, 0);
    }
}