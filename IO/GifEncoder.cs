using System;
using System.Collections.Generic;
using System.IO;
using Raylib_cs;

namespace PixelArtApp.IO;

/// <summary>
/// Harici kutuphane gerektirmeyen, seffaflik ve sonsuz dongu (Netscape 2.0) destekli GIF89a kodlayici.
/// </summary>
public static class GifEncoder
{
    private const int TransparentIndex = 255;

    public static bool SaveAnimatedGif(string path, int width, int height, IReadOnlyList<Color[]> frames, float fps)
    {
        if (frames == null || frames.Count == 0 || width <= 0 || height <= 0 || width > 4096 || height > 4096)
            return false;

        int delayHundredths = Math.Clamp((int)Math.Round(100f / Math.Max(1f, fps)), 2, 1000);

        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter bw = new BinaryWriter(fs);

        // 1. GIF89a Basligi
        bw.Write(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' });

        // 2. Logical Screen Descriptor
        bw.Write((ushort)width);
        bw.Write((ushort)height);
        bw.Write((byte)0xF7); // Global renk tablosu aktif (256 renk)
        bw.Write((byte)TransparentIndex);
        bw.Write((byte)0);

        // Varsayilan Global Renk Tablosu (256 * 3 bayt)
        for (int i = 0; i < 256; i++)
        {
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((byte)0);
        }

        // 3. Netscape 2.0 Sonsuz Dongu (Loop) Uzantisi
        bw.Write((byte)0x21);
        bw.Write((byte)0xFF);
        bw.Write((byte)11);
        bw.Write(new byte[] { (byte)'N', (byte)'E', (byte)'T', (byte)'S', (byte)'C', (byte)'A', (byte)'P', (byte)'E', (byte)'2', (byte)'.', (byte)'0' });
        bw.Write((byte)3);
        bw.Write((byte)1);
        bw.Write((ushort)0); // 0 = Sonsuz dongu
        bw.Write((byte)0);

        byte[] indexedPixels = new byte[width * height];
        byte[] localPalette = new byte[256 * 3];

        // 4. Kareleri Kodla
        for (int f = 0; f < frames.Count; f++)
        {
            Color[] framePixels = frames[f];
            QuantizeFrame(framePixels, indexedPixels, localPalette);

            // Graphic Control Extension
            bw.Write((byte)0x21);
            bw.Write((byte)0xF9);
            bw.Write((byte)4);
            bw.Write((byte)0x09); // Disposal = 2 (Arka plana temizle), Seffaf renk aktif = 1
            bw.Write((ushort)delayHundredths);
            bw.Write((byte)TransparentIndex);
            bw.Write((byte)0);

            // Image Descriptor
            bw.Write((byte)0x2C);
            bw.Write((ushort)0);
            bw.Write((ushort)0);
            bw.Write((ushort)width);
            bw.Write((ushort)height);
            bw.Write((byte)0x87); // Yerel renk tablosu aktif (256 renk)

            // Local Color Table
            bw.Write(localPalette);

            // LZW Sikistirilmis Piksel Verisi
            WriteLzwImageData(bw, indexedPixels);
        }

        // 5. GIF Sonlandirici
        bw.Write((byte)0x3B);
        return true;
    }

    private static void QuantizeFrame(Color[] pixels, byte[] indexedPixels, byte[] paletteBytes)
    {
        Array.Clear(paletteBytes, 0, paletteBytes.Length);

        // Frekans tablosu: 0..254 arasi opak renkler, 255 seffaf
        Dictionary<int, int> colorCounts = new();
        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if (c.A < 128) continue;
            int key = (c.R << 16) | (c.G << 8) | c.B;
            colorCounts[key] = colorCounts.TryGetValue(key, out int cnt) ? cnt + 1 : 1;
        }

        List<int> paletteColors = new(Math.Min(255, colorCounts.Count));
        if (colorCounts.Count <= 255)
        {
            paletteColors.AddRange(colorCounts.Keys);
        }
        else
        {
            var sorted = new List<KeyValuePair<int, int>>(colorCounts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; i < 255; i++)
                paletteColors.Add(sorted[i].Key);
        }

        Dictionary<int, byte> exactMap = new(paletteColors.Count);
        for (int i = 0; i < paletteColors.Count; i++)
        {
            int rgb = paletteColors[i];
            paletteBytes[i * 3] = (byte)((rgb >> 16) & 0xFF);
            paletteBytes[i * 3 + 1] = (byte)((rgb >> 8) & 0xFF);
            paletteBytes[i * 3 + 2] = (byte)(rgb & 0xFF);
            exactMap[rgb] = (byte)i;
        }

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if (c.A < 128)
            {
                indexedPixels[i] = TransparentIndex;
                continue;
            }

            int key = (c.R << 16) | (c.G << 8) | c.B;
            if (exactMap.TryGetValue(key, out byte idx))
            {
                indexedPixels[i] = idx;
            }
            else
            {
                byte bestIdx = 0;
                int bestDist = int.MaxValue;
                for (int p = 0; p < paletteColors.Count; p++)
                {
                    int prgb = paletteColors[p];
                    int dr = c.R - ((prgb >> 16) & 0xFF);
                    int dg = c.G - ((prgb >> 8) & 0xFF);
                    int db = c.B - (prgb & 0xFF);
                    int dist = dr * dr + dg * dg + db * db;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIdx = (byte)p;
                    }
                }
                exactMap[key] = bestIdx;
                indexedPixels[i] = bestIdx;
            }
        }
    }

    private static void WriteLzwImageData(BinaryWriter bw, byte[] indexedPixels)
    {
        const int minCodeSize = 8;
        const int clearCode = 1 << minCodeSize; // 256
        const int eoiCode = clearCode + 1;      // 257

        bw.Write((byte)minCodeSize);

        using MemoryStream bitStream = new();
        int bitBuffer = 0;
        int bitCount = 0;

        void WriteCode(int code, int bits)
        {
            bitBuffer |= (code << bitCount);
            bitCount += bits;
            while (bitCount >= 8)
            {
                bitStream.WriteByte((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitCount -= 8;
            }
        }

        int codeSize = minCodeSize + 1; // 9 bit ile basla
        int nextCode = eoiCode + 1;     // 258
        Dictionary<int, int> table = new(4096);

        WriteCode(clearCode, codeSize);

        if (indexedPixels.Length > 0)
        {
            int prefix = indexedPixels[0];

            for (int i = 1; i < indexedPixels.Length; i++)
            {
                int k = indexedPixels[i];
                int combinedKey = (prefix << 8) | k;

                if (table.TryGetValue(combinedKey, out int existingCode))
                {
                    prefix = existingCode;
                }
                else
                {
                    WriteCode(prefix, codeSize);

                    if (nextCode < 4096)
                    {
                        table[combinedKey] = nextCode;
                        if (nextCode == (1 << codeSize) && codeSize < 12)
                        {
                            codeSize++;
                        }
                        nextCode++;
                    }
                    else
                    {
                        WriteCode(clearCode, codeSize);
                        table.Clear();
                        codeSize = minCodeSize + 1;
                        nextCode = eoiCode + 1;
                    }

                    prefix = k;
                }
            }

            WriteCode(prefix, codeSize);
        }

        WriteCode(eoiCode, codeSize);

        if (bitCount > 0)
        {
            bitStream.WriteByte((byte)(bitBuffer & 0xFF));
        }

        byte[] compressed = bitStream.ToArray();
        int offset = 0;
        while (offset < compressed.Length)
        {
            int chunk = Math.Min(255, compressed.Length - offset);
            bw.Write((byte)chunk);
            bw.Write(compressed, offset, chunk);
            offset += chunk;
        }

        bw.Write((byte)0); // Block terminator
    }
}
