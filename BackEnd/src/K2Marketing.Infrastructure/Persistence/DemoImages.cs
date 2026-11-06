using System.IO.Compression;
using System.Text;

namespace K2Marketing.Infrastructure.Persistence;

/// <summary>Gera PNGs simples (gradiente + faixas) para os criativos de demonstração, sem dependências externas.</summary>
public static class DemoImages
{
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static byte[] Gradient(int width, int height, (byte R, byte G, byte B) from, (byte R, byte G, byte B) to, int variant)
    {
        var raw = new byte[(width * 3 + 1) * height];
        var i = 0;
        for (var y = 0; y < height; y++)
        {
            raw[i++] = 0; // filtro: nenhum
            for (var x = 0; x < width; x++)
            {
                var t = (x + y * (variant % 3 + 1)) / (double)(width + height * (variant % 3 + 1));
                var stripe = (variant % 2 == 0 ? (x + y) / 24 % 7 == 0 : y > height * 0.68 && y < height * 0.8) ? 0.82 : 1.0;
                // Círculo decorativo.
                var cx = width * (0.25 + variant % 4 * 0.15);
                var cy = height * 0.4;
                var inCircle = (x - cx) * (x - cx) + (y - cy) * (y - cy) < height * height * 0.06;
                var light = inCircle ? 1.18 : 1.0;
                raw[i++] = Clamp((from.R + (to.R - from.R) * t) * stripe * light);
                raw[i++] = Clamp((from.G + (to.G - from.G) * t) * stripe * light);
                raw[i++] = Clamp((from.B + (to.B - from.B) * t) * stripe * light);
            }
        }

        using var ms = new MemoryStream();
        ms.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        var ihdr = new byte[13];
        WriteBigEndian(ihdr, 0, (uint)width);
        WriteBigEndian(ihdr, 4, (uint)height);
        ihdr[8] = 8;  // bits por canal
        ihdr[9] = 2;  // RGB
        WriteChunk(ms, "IHDR", ihdr);
        using (var compressed = new MemoryStream())
        {
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true)) z.Write(raw);
            WriteChunk(ms, "IDAT", compressed.ToArray());
        }
        WriteChunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static byte Clamp(double v) => (byte)Math.Clamp((int)v, 0, 255);

    private static void WriteChunk(Stream s, string type, byte[] data)
    {
        var len = new byte[4];
        WriteBigEndian(len, 0, (uint)data.Length);
        s.Write(len);
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        var crc = new byte[4];
        WriteBigEndian(crc, 0, Crc(typeBytes, data));
        s.Write(crc);
    }

    private static void WriteBigEndian(byte[] b, int offset, uint v)
    {
        b[offset] = (byte)(v >> 24);
        b[offset + 1] = (byte)(v >> 16);
        b[offset + 2] = (byte)(v >> 8);
        b[offset + 3] = (byte)v;
    }

    private static uint Crc(byte[] type, byte[] data)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in type) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        foreach (var b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        return c ^ 0xFFFFFFFFu;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }
}
