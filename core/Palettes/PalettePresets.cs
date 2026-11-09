// === DOSYA: \Core\Palettes\PalettePresets.cs ===
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Raylib_cs;

namespace PixelArtApp.Core.Palettes;

public sealed record PalettePreset(string Name, Color[] Colors);

/// <summary>
/// Lospec uyumlu hazir piksel sanat paletleri (PICO-8, Game Boy, Sweetie 16, Endesga 32 vb.)
/// ve disaridan .hex / .pal / .gpl palet dosyasi okuma/yazma destegi.
/// </summary>
public static class PalettePresets
{
    private static Color Hex(string hex)
    {
        string s = hex.Trim().TrimStart('#');
        if (s.Length == 6 &&
            byte.TryParse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
            byte.TryParse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
            byte.TryParse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
        {
            return new Color(r, g, b, (byte)255);
        }
        return Color.Black;
    }

    public static readonly IReadOnlyList<PalettePreset> BuiltIn = new List<PalettePreset>
    {
        new PalettePreset("Varsayilan (20)", new Color[]
        {
            Color.Black, Color.DarkGray, Color.Gray, Color.LightGray, Color.White,
            Color.Red, Color.Maroon, Color.Orange, Color.Gold, Color.Yellow,
            Color.DarkGreen, Color.Green, Color.Lime, Color.DarkBlue, Color.Blue,
            Color.SkyBlue, Color.Purple, Color.Violet, Color.Beige, Color.Brown
        }),

        // PICO-8 Resmi 16 Renk Paleti
        new PalettePreset("PICO-8 (16)", new Color[]
        {
            Hex("000000"), Hex("1D2B53"), Hex("7E2553"), Hex("008751"),
            Hex("AB5236"), Hex("5F574F"), Hex("C2C3C7"), Hex("FFF1E8"),
            Hex("FF004D"), Hex("FFA300"), Hex("FFEC27"), Hex("00E436"),
            Hex("29ADFF"), Hex("83769C"), Hex("FF77A8"), Hex("FFCCAA")
        }),

        // Nintendo Game Boy DMG-01 Klasik 4 Yeşil Tonu + Cep (Pocket) 4 Tonu
        new PalettePreset("Game Boy DMG (4)", new Color[]
        {
            Hex("0F380F"), Hex("306230"), Hex("8BAC0F"), Hex("9BBC0F")
        }),

        // Lospec En Popüler 16 Renk Paleti: Sweetie 16 (GrafxKid)
        new PalettePreset("Sweetie 16", new Color[]
        {
            Hex("1A1C2C"), Hex("5D275D"), Hex("B13E53"), Hex("EF7D57"),
            Hex("FFCD75"), Hex("A7F070"), Hex("38B764"), Hex("257179"),
            Hex("29366F"), Hex("3B5DC9"), Hex("41A6F6"), Hex("73EFF7"),
            Hex("F4F4F4"), Hex("94B0C2"), Hex("566C86"), Hex("333C57")
        }),

        // Lospec Efsanevi 32 Renk Paleti: Endesga 32 (ENDESGA)
        new PalettePreset("Endesga 32", new Color[]
        {
            Hex("BE4A2F"), Hex("D77643"), Hex("EAD4AA"), Hex("E4A672"),
            Hex("B86F50"), Hex("733E39"), Hex("3E2731"), Hex("A22633"),
            Hex("E43B44"), Hex("F77622"), Hex("FEAE34"), Hex("FEE761"),
            Hex("63C74D"), Hex("3E8948"), Hex("265C42"), Hex("193C3E"),
            Hex("124E89"), Hex("0099DB"), Hex("2CE8F5"), Hex("FFFFFF"),
            Hex("C0CBDC"), Hex("8B9BB4"), Hex("5A6988"), Hex("3A4466"),
            Hex("262B44"), Hex("181425"), Hex("FF0044"), Hex("68386C"),
            Hex("B55088"), Hex("F6757A"), Hex("E8B796"), Hex("C28569")
        }),

        // Cyberpunk Synthwave 16
        new PalettePreset("Cyberpunk (16)", new Color[]
        {
            Hex("090A14"), Hex("151729"), Hex("272744"), Hex("493D6B"),
            Hex("7A487A"), Hex("B8538A"), Hex("F26193"), Hex("FF8EB3"),
            Hex("FF2A6D"), Hex("FF6E27"), Hex("FFD319"), Hex("F9F871"),
            Hex("05FFA1"), Hex("01FFC3"), Hex("00D9F5"), Hex("005678")
        }),

        // Yumuşak Pastel 16
        new PalettePreset("Pastel Ruya (16)", new Color[]
        {
            Hex("2D2A32"), Hex("564D65"), Hex("8D818C"), Hex("C4B7CB"),
            Hex("F7EDF0"), Hex("FFCAD4"), Hex("F4978E"), Hex("F08080"),
            Hex("F8AD9D"), Hex("FBC4AB"), Hex("FFE5D9"), Hex("D8E2DC"),
            Hex("B8E0D2"), Hex("95D5B2"), Hex("A2D2FF"), Hex("BDB2FF")
        }),

        // Retro Monokrom / 1-Bit + Gri Tonlama (8)
        new PalettePreset("Retro Mono (8)", new Color[]
        {
            Hex("10121C"), Hex("2C3040"), Hex("4E546B"), Hex("747D96"),
            Hex("9FA8C2"), Hex("C6CFE6"), Hex("E4ECFA"), Hex("FFFFFF")
        })
    };

    /// <summary>
    /// Lospec .hex, JASC .pal veya GIMP .gpl formatindaki palet dosyalarini okur.
    /// </summary>
    public static List<Color> LoadFromFile(string path, out string paletteName)
    {
        paletteName = Path.GetFileNameWithoutExtension(path);
        var result = new List<Color>();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return result;

        string ext = Path.GetExtension(path).ToLowerInvariant();
        string[] lines = File.ReadAllLines(path);

        if (ext == ".pal" && lines.Length > 0 && lines[0].Trim().StartsWith("JASC-PAL", StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 3; i < lines.Length; i++)
            {
                string[] parts = lines[i].Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    byte.TryParse(parts[0], out byte r) &&
                    byte.TryParse(parts[1], out byte g) &&
                    byte.TryParse(parts[2], out byte b))
                {
                    result.Add(new Color(r, g, b, (byte)255));
                }
            }
            return result;
        }

        if (ext == ".gpl" || (lines.Length > 0 && lines[0].Trim().StartsWith("GIMP Palette", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("GIMP", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Columns", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase))
                {
                    string n = line.Substring(5).Trim();
                    if (!string.IsNullOrEmpty(n)) paletteName = n;
                    continue;
                }

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3 &&
                    byte.TryParse(parts[0], out byte r) &&
                    byte.TryParse(parts[1], out byte g) &&
                    byte.TryParse(parts[2], out byte b))
                {
                    result.Add(new Color(r, g, b, (byte)255));
                }
            }
            return result;
        }

        // Varsayilan: Lospec .hex veya .txt (her satirda RRGGBB veya #RRGGBB)
        foreach (string raw in lines)
        {
            string line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("//"))
                continue;

            string token = line.Split(new[] { ' ', '\t', ',' }, StringSplitOptions.RemoveEmptyEntries)[0].TrimStart('#');
            if (token.Length == 8) token = token.Substring(2); // AARRGGBB -> RRGGBB
            if (token.Length == 6 &&
                byte.TryParse(token.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) &&
                byte.TryParse(token.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) &&
                byte.TryParse(token.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
            {
                result.Add(new Color(r, g, b, (byte)255));
            }
        }

        return result;
    }

    public static bool SaveAsHexFile(string path, IReadOnlyList<Color> colors)
    {
        if (string.IsNullOrWhiteSpace(path) || colors == null || colors.Count == 0)
            return false;

        try
        {
            using var sw = new StreamWriter(path, false);
            for (int i = 0; i < colors.Count; i++)
            {
                Color c = colors[i];
                sw.WriteLine($"{c.R:X2}{c.G:X2}{c.B:X2}".ToLowerInvariant());
            }
            return true;
        }
        catch
        {
            return false;
        }
    }
}
