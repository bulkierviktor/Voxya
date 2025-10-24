using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace Voxya.Voxel.Core
{
    // Guarda/lee chunks en binario simple (.vxb) en persistentDataPath
    public class BinaryChunkStorage : IChunkStorage
    {
        private readonly string root;
        private readonly ICompressor comp;

        public BinaryChunkStorage(string folderName = "VoxyaChunks", ICompressor comp = null)
        {
            root = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(root);
            this.comp = comp ?? new NoCompression();
        }

        private string PathFor(ChunkCoord c) => Path.Combine(root, $"c_{c.x}_{c.z}.vxb");

        public async Task<bool> TryLoad(ChunkCoord coord, VoxelWorldConfig cfg, VoxelChunkData into)
        {
            string p = PathFor(coord);
            if (!File.Exists(p)) return false;
            byte[] data = await File.ReadAllBytesAsync(p);
            data = comp.Decompress(data);

            using var br = new BinaryReader(new MemoryStream(data));
            int size = br.ReadInt32();
            int height = br.ReadInt32();
            if (size != into.Size || height != into.Height) return false;
            br.Read(into.V, 0, into.V.Length);
            return true;
        }

        public async Task Save(ChunkCoord coord, VoxelWorldConfig cfg, VoxelChunkData from)
        {
            using var ms = new MemoryStream();
            using (var bw = new BinaryWriter(ms))
            {
                bw.Write(from.Size);
                bw.Write(from.Height);
                bw.Write(from.V);
            }
            byte[] data = comp.Compress(ms.ToArray());
            await File.WriteAllBytesAsync(PathFor(coord), data);
        }
    }
}