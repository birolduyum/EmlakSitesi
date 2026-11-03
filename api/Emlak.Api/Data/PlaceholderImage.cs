using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Emlak.Api.Data;

/// <summary>
/// Örnek veri için kodla üretilen yer tutucu PNG: indekse göre düz arka plan rengi ve ortada büyük fotoğraf numarası.
/// Dış kütüphane ve ikili dosya gerekmez; telifli görsel yoktur.
/// </summary>
public static class PlaceholderImage
{
    private const int GlyphWidth = 5;
    private const int GlyphHeight = 7;

    // 5x7 rakam yazı tipi: her satır 5 bitlik bir maske (en soldaki bit en yüksek).
    private static readonly byte[][] Glyphs =
    [
        [0b01110, 0b10001, 0b10011, 0b10101, 0b11001, 0b10001, 0b01110], // 0
        [0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110], // 1
        [0b01110, 0b10001, 0b00001, 0b00010, 0b00100, 0b01000, 0b11111], // 2
        [0b11111, 0b00010, 0b00100, 0b00010, 0b00001, 0b10001, 0b01110], // 3
        [0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010], // 4
        [0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110], // 5
        [0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110], // 6
        [0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000], // 7
        [0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110], // 8
        [0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100], // 9
    ];

    private static readonly byte[] Signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly uint[] CrcTable = BuildCrcTable();

    /// <summary>İndekse özgü renkte, ortasında <paramref name="index"/> numarası yazılı width x height PNG döner.</summary>
    public static byte[] Create(int index, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var (r, g, b) = BackgroundColor(index);
        var stride = 1 + width * 3; // her satırın başında süzgeç bayt'ı (0 = yok)
        var raw = new byte[stride * height];
        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            for (var x = 0; x < width; x++)
            {
                var p = row + 1 + x * 3;
                raw[p] = r;
                raw[p + 1] = g;
                raw[p + 2] = b;
            }
        }

        DrawNumber(raw, stride, width, height, Math.Abs(index).ToString());

        using var output = new MemoryStream();
        output.Write(Signature);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr[..4], width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.Slice(4, 4), height);
        ihdr[8] = 8; // bit derinliği
        ihdr[9] = 2; // renk türü: RGB
        WriteChunk(output, "IHDR", ihdr);

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    // Rakamlar beyaz kalır; arka plan koyu tutulur (açıklık 0.38) ki okunaklı olsun.
    private static (byte R, byte G, byte B) BackgroundColor(int index)
    {
        var hue = Math.Abs(index * 47 % 360);
        const double s = 0.55, l = 0.38;
        var c = (1 - Math.Abs(2 * l - 1)) * s;
        var x = c * (1 - Math.Abs(hue / 60.0 % 2 - 1));
        var m = l - c / 2;
        var (r1, g1, b1) = hue switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        return ((byte)Math.Round((r1 + m) * 255), (byte)Math.Round((g1 + m) * 255), (byte)Math.Round((b1 + m) * 255));
    }

    private static void DrawNumber(byte[] raw, int stride, int width, int height, string text)
    {
        // Ölçek, numaranın görüntünün yaklaşık yarı yüksekliğini kaplayacağı ve taşmayacağı biçimde seçilir.
        var gap = 1;
        var cells = text.Length * GlyphWidth + (text.Length - 1) * gap;
        var scale = Math.Max(1, Math.Min(height / 2 / GlyphHeight, width * 3 / 4 / cells));
        var totalW = cells * scale;
        var totalH = GlyphHeight * scale;
        var left = (width - totalW) / 2;
        var top = (height - totalH) / 2;

        for (var i = 0; i < text.Length; i++)
        {
            var glyph = Glyphs[text[i] - '0'];
            var originX = left + i * (GlyphWidth + gap) * scale;
            for (var gy = 0; gy < GlyphHeight; gy++)
            {
                for (var gx = 0; gx < GlyphWidth; gx++)
                {
                    if ((glyph[gy] >> (GlyphWidth - 1 - gx) & 1) == 0) { continue; }
                    FillRect(raw, stride, width, height, originX + gx * scale, top + gy * scale, scale);
                }
            }
        }
    }

    private static void FillRect(byte[] raw, int stride, int width, int height, int x0, int y0, int size)
    {
        for (var y = Math.Max(0, y0); y < Math.Min(height, y0 + size); y++)
        {
            for (var x = Math.Max(0, x0); x < Math.Min(width, x0 + size); x++)
            {
                var p = y * stride + 1 + x * 3;
                raw[p] = raw[p + 1] = raw[p + 2] = 255;
            }
        }
    }

    private static void WriteChunk(Stream output, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> word = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(word, data.Length);
        output.Write(word);

        var typeBytes = Encoding.ASCII.GetBytes(type);
        output.Write(typeBytes);
        output.Write(data);

        var crc = 0xFFFFFFFFu;
        crc = UpdateCrc(crc, typeBytes);
        crc = UpdateCrc(crc, data);
        BinaryPrimitives.WriteUInt32BigEndian(word, crc ^ 0xFFFFFFFFu);
        output.Write(word);
    }

    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var b in bytes)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
