namespace Voxya.Voxel.Core
{
    // Punto de extensión para LZ4 u otras compresiones
    public interface ICompressor
    {
        byte[] Compress(byte[] input);
        byte[] Decompress(byte[] input);
    }
}