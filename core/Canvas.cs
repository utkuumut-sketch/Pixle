using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Canvas : IDisposable
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

    // PERFORMANS: Damalı Arka Plan (Checkerboard) Önbelleği
    private Texture2D checkerTexture;
    private bool hasChecker = false;

    public Canvas(int width, int height, int scale, Vector2 offset)
    {
        Width = width;
        Height = height;
        PixelScale = scale;
        Offset = offset;
        
        Frames.Add(new Frame());
        Frames[0].Layers.Add(new Layer("Katman 1", width, height));
    }

    private void InitCheckerTexture()
    {
        if (hasChecker) return;
        Image img = Raylib.GenImageColor(Width, Height, Color.White);
        Color c1 = new Color(200, 200, 200, 255);
        Color c2 = new Color(160, 160, 160, 255);

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Raylib.ImageDrawPixel(ref img, x, y, ((x + y) % 2 == 0) ? c1 : c2);
            }
        }
        checkerTexture = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureFilter(checkerTexture, TextureFilter.Point);
        Raylib.UnloadImage(img);
        hasChecker = true;
    }

    public void AddLayer()
    {
        Layers.Add(new Layer($"Katman {Layers.Count + 1}", Width, Height));
        ActiveLayerIndex = Layers.Count - 1; 
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
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

    public void ReplaceFrames(List<Frame> newFrames, int activeFrameIndex)
    {
        if (newFrames == null || newFrames.Count == 0) return;

        foreach (var f in Frames) f.Dispose();
        Frames.Clear();

        Frames.AddRange(newFrames);
        ActiveFrameIndex = Math.Clamp(activeFrameIndex, 0, Frames.Count - 1);
    }
    
    public Color GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        return Layers[ActiveLayerIndex].Pixels[x, y];
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        var layer = Layers[ActiveLayerIndex];
        layer.Pixels[x, y] = color;
        layer.IsDirty = true;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
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
        var layer = Layers[ActiveLayerIndex];
        var p = layer.Pixels;
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                p[x, y] = color;

        layer.IsDirty = true;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
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
        InitCheckerTexture();

        int totalW = Width * PixelScale;
        int totalH = Height * PixelScale;
        Rectangle srcRect = new Rectangle(0, 0, Width, Height);
        Rectangle destRect = new Rectangle(Offset.X, Offset.Y, totalW, totalH);

        // Tuval Altı Gölge
        Raylib.DrawRectangle((int)Offset.X + 6, (int)Offset.Y + 6, totalW, totalH, new Color(0, 0, 0, 110));

        // 1. Tek Çağrıda Damalı Arka Plan Çizimi (65.536 DrawRectangle yerine 1 DrawTexturePro!)
        Raylib.DrawTexturePro(checkerTexture, srcRect, destRect, Vector2.Zero, 0f, Color.White);

        // 2. Katmanları GPU Dokusu Olarak Çiz
        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            Texture2D layerTex = Layers[i].GetOrUpdateTexture();
            Raylib.DrawTexturePro(layerTex, srcRect, destRect, Vector2.Zero, 0f, Color.White);
        }

        // 3. Piksel Izgarası
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

    public void Dispose()
    {
        if (hasChecker)
        {
            Raylib.UnloadTexture(checkerTexture);
            hasChecker = false;
        }
        foreach (var f in Frames) f.Dispose();
    }
}