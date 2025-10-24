using System;
using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Almacén contiguo orientado a caché para voxeles (byte por bloque)
    public sealed class VoxelChunkData
    {
        public readonly int Size;     // X,Z
        public readonly int Height;   // Y
        public readonly byte[] V;     // Voxel buffer (Size * Height * Size)

        public VoxelChunkData(int size, int height)
        {
            Size = size;
            Height = height;
            V = new byte[size * height * size];
        }

        public void Clear(byte val = 0) => Array.Fill(V, val);

        // Indexación (x,y,z) -> índice lineal
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public int Idx(int x, int y, int z) => (y * Size + z) * Size + x;

        public BlockType Get(int x, int y, int z) => (BlockType)V[Idx(x, y, z)];
        public void Set(int x, int y, int z, BlockType bt) => V[Idx(x, y, z)] = (byte)bt;
    }
}