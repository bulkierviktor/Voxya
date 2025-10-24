namespace Voxya.Voxel.Core
{
    public class NoCompression : ICompressor
    {
        public byte[] Compress(byte[] input) => input;
        public byte[] Decompress(byte[] input) => input;
    }
}