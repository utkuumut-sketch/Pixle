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

    private Texture2D checkerTexture;
    private bool hasChecker = false;
    private int lastCheckerContrast = -1;

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
        if (hasChecker && lastCheckerContrast == AppSettings.CheckerContrast) return;

        if (hasChecker)
        {
            Raylib.UnloadTexture(checkerTexture);
            hasChecker = false;
        }

        Image img = Raylib.GenImageColor(Width, Height, Color.White);
        Color c1 = AppSettings.CheckerContrast switch
        {
            0 => new Color(185, 185, 185, 255),
            2 => new Color(225, 225, 225, 255),
            _ => new Color(200, 200, 200, 255)
        };
        Color c2 = AppSettings.CheckerContrast switch
        {
            0 => new Color(170, 170, 170, 255),
            2 => new Color(135, 135, 135, 255),
            _ => new Color(160, 160, 160, 255)
        };

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
        lastCheckerContrast = AppSettings.CheckerContrast;
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
        return Layers[ActiveLayerIndex].GetPixel(x, y);
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        Layers[ActiveLayerIndex].SetPixel(x, y, color);
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public Color GetMergedPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        Color final = TransparentColor;
        
        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            Color c = Layers[i].GetPixel(x, y);
            if (c.A > 0) final = c; 
        }
        return final;
    }

    public void Clear(Color color)
    {
        Layers[ActiveLayerIndex].Fill(color);
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

        Raylib.DrawRectangle((int)Offset.X + 6, (int)Offset.Y + 6, totalW, totalH, new Color(0, 0, 0, 110));
        Raylib.DrawTexturePro(checkerTexture, srcRect, destRect, Vector2.Zero, 0f, Color.White);

        if (AppSettings.EnableOnionSkin && Frames.Count > 1)
        {
            byte prevAlpha = (byte)Math.Clamp(AppSettings.OnionSkinOpacity, 15, 230);
            byte nextAlpha = (byte)Math.Clamp((int)(AppSettings.OnionSkinOpacity * 0.6f), 10, 200);

            if (ActiveFrameIndex > 0 && Frames[ActiveFrameIndex - 1].IsVisible)
            {
                Texture2D prevTex = Frames[ActiveFrameIndex - 1].GetOrUpdateThumbnail(Width, Height);
                Raylib.DrawTexturePro(prevTex, srcRect, destRect, Vector2.Zero, 0f, new Color((byte)255, (byte)255, (byte)255, prevAlpha)); 
            }

            if (ActiveFrameIndex < Frames.Count - 1 && Frames[ActiveFrameIndex + 1].IsVisible)
            {
                Texture2D nextTex = Frames[ActiveFrameIndex + 1].GetOrUpdateThumbnail(Width, Height);
                Raylib.DrawTexturePro(nextTex, srcRect, destRect, Vector2.Zero, 0f, new Color((byte)255, (byte)255, (byte)255, nextAlpha));
            }
        }

        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible) continue;
            Texture2D layerTex = Layers[i].GetOrUpdateTexture();
            Raylib.DrawTexturePro(layerTex, srcRect, destRect, Vector2.Zero, 0f, Color.White);
        }

        if (ShowGrid)
        {
            Color gridCol = new Color((byte)0, (byte)0, (byte)0, (byte)Math.Clamp(AppSettings.GridOpacity, 10, 200));
            for (int x = 0; x <= Width; x++)
                Raylib.DrawLine((int)Offset.X + x * PixelScale, (int)Offset.Y, (int)Offset.X + x * PixelScale, (int)Offset.Y + totalH, gridCol);
            for (int y = 0; y <= Height; y++)
                Raylib.DrawLine((int)Offset.X, (int)Offset.Y + y * PixelScale, (int)Offset.X + totalW, (int)Offset.Y + y * PixelScale, gridCol);
        }

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