using UnityEngine;

namespace Voxya.Voxel.Core
{
    // OpenSimplex2D simplificado y determinista (suficiente para terreno)
    // Nota: implementación compacta enfocada a 2D; si necesitas 3D, separar clase.
    public class OpenSimplex2D : IVoxelNoise2D
    {
        private short[] perm;

        public void SetSeed(int seed)
        {
            var rnd = new System.Random(seed);
            perm = new short[256];
            for (int i = 0; i < 256; i++) perm[i] = (short)i;
            for (int i = 255; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                (perm[i], perm[j]) = (perm[j], perm[i]);
            }
        }

        public float Sample01(float x, float z)
        {
            // Skewing/Unskewing constantes
            const float STRETCH_CONSTANT = -0.211324865405187f; // (1/Mathf.Sqrt(2+1)-1)/2
            const float SQUISH_CONSTANT = 0.366025403784439f;   // (Mathf.Sqrt(2+1)-1)/2

            float stretchOffset = (x + z) * STRETCH_CONSTANT;
            float xs = x + stretchOffset;
            float zs = z + stretchOffset;

            int xsb = Mathf.FloorToInt(xs);
            int zsb = Mathf.FloorToInt(zs);

            float squishOffset = (xsb + zsb) * SQUISH_CONSTANT;
            float dx0 = x - (xsb + squishOffset);
            float dz0 = z - (zsb + squishOffset);

            float value = 0f;

            // Primera contribución
            float attn0 = 2 - dx0 * dx0 - dz0 * dz0;
            if (attn0 > 0)
            {
                attn0 *= attn0;
                value += attn0 * attn0 * Extrapolate(xsb, zsb, dx0, dz0);
            }

            // Determinar segunda esquina
            float dx1 = dx0 - 1 - SQUISH_CONSTANT;
            float dz1 = dz0 - 0 - SQUISH_CONSTANT;
            float attn1 = 2 - dx1 * dx1 - dz1 * dz1;
            if (attn1 > 0)
            {
                attn1 *= attn1;
                value += attn1 * attn1 * Extrapolate(xsb + 1, zsb + 0, dx1, dz1);
            }

            float dx2 = dx0 - 0 - SQUISH_CONSTANT;
            float dz2 = dz0 - 1 - SQUISH_CONSTANT;
            float attn2 = 2 - dx2 * dx2 - dz2 * dz2;
            if (attn2 > 0)
            {
                attn2 *= attn2;
                value += attn2 * attn2 * Extrapolate(xsb + 0, zsb + 1, dx2, dz2);
            }

            // Normalizar aproximadamente a [0,1]
            return Mathf.Clamp01(0.5f + value * 0.25f);
        }

        public float Fbm01(float x, float z, int octaves, float lacunarity, float gain)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Sample01(x * freq, z * freq);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return (norm > 0f) ? sum / norm : 0f;
        }

        private float Extrapolate(int xsb, int zsb, float dx, float dz)
        {
            int index = perm[(xsb & 0xFF)];
            int index2 = perm[(index + zsb) & 0xFF] & 0x0E;
            // Gradientes 2D
            float gx = Gradients2D[index2];
            float gz = Gradients2D[index2 + 1];
            return gx * dx + gz * dz;
        }

        private static readonly float[] Gradients2D = {
            5, 2,  2, 5,  -5, 2,  -2, 5,
            5, -2, 2, -5, -5, -2, -2, -5
        };
    }
}