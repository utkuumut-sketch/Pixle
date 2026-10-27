using System.Collections.Generic;

namespace PixelArtApp.IO;

public class LoomProjectData
{
    // Gelecekte format degistiginde eski dosyalari taniyip donusturebilmek icin surum numarasi
    public int FormatVersion { get; set; } = 1;
    
    public int Width { get; set; }
    public int Height { get; set; }
    public float Fps { get; set; } = 8f;
    public int ActiveFrameIndex { get; set; } = 0;
    public bool ShowGrid { get; set; } = true;

    // Renk paleti (#RRGGBBAA formatinda hex kodlari olarak tutulur)
    public List<string> PaletteHex { get; set; } = new();

    // Animasyon kareleri
    public List<LoomFrameData> Frames { get; set; } = new();
}

public class LoomFrameData
{
    public int ActiveLayerIndex { get; set; } = 0;
    public bool IsVisible { get; set; } = true;
    public List<LoomLayerData> Layers { get; set; } = new();
}

public class LoomLayerData
{
    public string Name { get; set; } = "Katman 1";
    public bool IsVisible { get; set; } = true;
    
    // Ileride katman opakligi eklendiginde dosya formatini bozmamak icin simdiden tanimliyoruz (0-255)
    public byte Opacity { get; set; } = 255;

    // ZIP icindeki ikili (binary) piksel dosyasinin yolu (Ornek: "pixels/f0_l0.bin")
    public string DataFile { get; set; } = "";
}