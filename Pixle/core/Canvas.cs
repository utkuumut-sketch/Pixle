using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Canvas
{
    public int Width { get; }
    public int Height { get; }
    public int PixelScale { get; }
    public Vector2 Offset { get; set; }
    public bool ShowGrid { get; set; } = true;
    public static readonly Color TransparentColor = new Color(0, 0, 0, 0);

    // YENİ: Animasyon Kareleri (Frames)
    public List<Frame> Frames { get; private set; } = new();
    public int ActiveFrameIndex { get; set; } = 0;

    // Eskiden var olan tüm sistemlerin bozulmadan çalışması için yönlendiriciler
    public List<Layer> Layers => Frames[ActiveFrameIndex].Layers;
    public int ActiveLayerIndex 
    { 
        get => Frames[ActiveFrameIndex].ActiveLayerIndex; 
        set => Frames[ActiveFrameIndex].ActiveLayerIndex = value; 
    }

    public Canvas(int width, int height, int scale, Vector2 offset)
    {
        Width = width;
        Height = height;
        PixelScale = scale;
        Offset = offset;
        
        // İlk açılışta 1. Kare (Frame) ve içine 1. Katmanı (Layer) ekle
        Frames.Add(new Frame());
        Frames[0].Layers.Add(new Layer("Katman 1", width, height));
    }

    public void AddLayer()
    {
        Layers.Add(new Layer($"Katman {Layers.Count + 1}", Width, Height));
        ActiveLayerIndex = Layers.Count - 1; 
    }

    // YENİ: Animasyon Karesi Ekleme (Bir önceki kareyi kopyalarak devam eder)
    public void AddFrame()
    {
        var newFrame = new Frame();
        foreach (var l in Layers)
        {
            newFrame.Layers.Add(l.Clone()); // Son çizimi kopyala
        }
        newFrame.ActiveLayerIndex = ActiveLayerIndex;
        Frames.Add(newFrame);
        ActiveFrameIndex = Frames.Count - 1;
    }

    public Color GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        return Layers[ActiveLayerIndex].Pixels[x, y];
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        Layers[ActiveLayerIndex].Pixels[x, y] = color;
    }

    public Color GetMergedPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        Color final = TransparentColor;
        
        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            Color c = Layers[i].Pixels[x, y];
            if (c.A > 0) final = c; 
        }
        return final;
    }

    public void Clear(Color color)
    {
        var p = Layers[ActiveLayerIndex].Pixels;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                p[x, y] = color;
    }

    public bool IsMouseInside(Vector2 worldPos)
    {
        return worldPos.X >= Offset.X && worldPos.X < Offset.X + Width * PixelScale &&
               worldPos.Y >= Offset.Y && worldPos.Y < Offset.Y + Height * PixelScale;
    }

    public (int x, int y) ScreenToGrid(Vector2 worldPos)
    {
        int gx = (int)((worldPos.X - Offset.X) / PixelScale);
        int gy = (int)((worldPos.Y - Offset.Y) / PixelScale);
        return (gx, gy);
    }

    public void Draw()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Color bg = ((x + y) % 2 == 0) ? new Color(200, 200, 200, 255) : new Color(160, 160, 160, 255);
                Raylib.DrawRectangle((int)Offset.X + x * PixelScale, (int)Offset.Y + y * PixelScale, PixelScale, PixelScale, bg);
            }
        }

        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            var p = Layers[i].Pixels;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (p[x, y].A > 0)
                        Raylib.DrawRectangle((int)Offset.X + x * PixelScale, (int)Offset.Y + y * PixelScale, PixelScale, PixelScale, p[x, y]);
        }

        if (ShowGrid)
        {
            for (int x = 0; x <= Width; x++)
                Raylib.DrawLine((int)Offset.X + x * PixelScale, (int)Offset.Y, (int)Offset.X + x * PixelScale, (int)Offset.Y + Height * PixelScale, new Color(0, 0, 0, 50));
            for (int y = 0; y <= Height; y++)
                Raylib.DrawLine((int)Offset.X, (int)Offset.Y + y * PixelScale, (int)Offset.X + Width * PixelScale, (int)Offset.Y + y * PixelScale, new Color(0, 0, 0, 50));
        }
    }
}