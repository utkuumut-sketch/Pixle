using System;
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

    public List<Frame> Frames { get; private set; } = new();
    public int ActiveFrameIndex { get; set; } = 0;

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
        
        Frames.Add(new Frame());
        Frames[0].Layers.Add(new Layer("Katman 1", width, height));
    }

    public void AddLayer()
    {
        Layers.Add(new Layer($"Katman {Layers.Count + 1}", Width, Height));
        ActiveLayerIndex = Layers.Count - 1; 
    }

    public void AddFrame()
    {
        var newFrame = new Frame();
        foreach (var l in Layers)
        {
            newFrame.Layers.Add(l.Clone());
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
        int gx = (int)Math.Floor((worldPos.X - Offset.X) / PixelScale);
        int gy = (int)Math.Floor((worldPos.Y - Offset.Y) / PixelScale);
        return (gx, gy);
    }

    public void Draw()
    {
        int totalW = Width * PixelScale;
        int totalH = Height * PixelScale;

        // Görsel İyileştirme: Tuval Altı Gölge (Drop Shadow)
        Raylib.DrawRectangle((int)Offset.X + 6, (int)Offset.Y + 6, totalW, totalH, new Color(0, 0, 0, 110));

        // Şeffaflık Damalı Arka Plan
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Color bg = ((x + y) % 2 == 0) ? new Color(200, 200, 200, 255) : new Color(160, 160, 160, 255);
                Raylib.DrawRectangle((int)Offset.X + x * PixelScale, (int)Offset.Y + y * PixelScale, PixelScale, PixelScale, bg);
            }
        }

        // Katman Pikselleri
        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            var p = Layers[i].Pixels;
            for (int x = 0; x < Width; x++)
                for (int y = 0; y < Height; y++)
                    if (p[x, y].A > 0)
                        Raylib.DrawRectangle((int)Offset.X + x * PixelScale, (int)Offset.Y + y * PixelScale, PixelScale, PixelScale, p[x, y]);
        }

        // Piksel Izgarası
        if (ShowGrid)
        {
            for (int x = 0; x <= Width; x++)
                Raylib.DrawLine((int)Offset.X + x * PixelScale, (int)Offset.Y, (int)Offset.X + x * PixelScale, (int)Offset.Y + totalH, new Color(0, 0, 0, 45));
            for (int y = 0; y <= Height; y++)
                Raylib.DrawLine((int)Offset.X, (int)Offset.Y + y * PixelScale, (int)Offset.X + totalW, (int)Offset.Y + y * PixelScale, new Color(0, 0, 0, 45));
        }

        // Tuval Dış Çerçevesi
        Raylib.DrawRectangleLinesEx(new Rectangle(Offset.X - 1, Offset.Y - 1, totalW + 2, totalH + 2), 1f, new Color(90, 90, 100, 255));
    }
}