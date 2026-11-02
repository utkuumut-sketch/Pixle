// === DOSYA: \Core\Canvas.cs ===
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
    private int activeFrameIndex = 0;

    public int ActiveFrameIndex
    {
        get => Frames.Count == 0 ? 0 : Math.Clamp(activeFrameIndex, 0, Frames.Count - 1);
        set => activeFrameIndex = Frames.Count == 0 ? 0 : Math.Clamp(value, 0, Frames.Count - 1);
    }

    public List<Layer> Layers => Frames[ActiveFrameIndex].Layers;

    public int ActiveLayerIndex
    {
        get
        {
            var layers = Layers;
            return layers.Count == 0 ? 0 : Math.Clamp(Frames[ActiveFrameIndex].ActiveLayerIndex, 0, layers.Count - 1);
        }
        set
        {
            var layers = Layers;
            Frames[ActiveFrameIndex].ActiveLayerIndex = layers.Count == 0 ? 0 : Math.Clamp(value, 0, layers.Count - 1);
        }
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

    public static Color BlendOver(Color bottom, Color top)
    {
        if (top.A == 0) return bottom;
        if (top.A == 255 || bottom.A == 0) return top;

        float srcA = top.A / 255f;
        float dstA = bottom.A / 255f;
        float outA = srcA + dstA * (1f - srcA);

        if (outA <= 0.0001f) return TransparentColor;

        byte r = (byte)Math.Clamp((top.R * srcA + bottom.R * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte g = (byte)Math.Clamp((top.G * srcA + bottom.G * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte b = (byte)Math.Clamp((top.B * srcA + bottom.B * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte a = (byte)Math.Clamp(outA * 255f, 0f, 255f);

        return new Color(r, g, b, a);
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

    public void SetLayerVisibility(int layerIndex, bool isVisible)
    {
        if (layerIndex < 0 || layerIndex >= Layers.Count) return;
        if (Layers[layerIndex].IsVisible != isVisible)
        {
            Layers[layerIndex].IsVisible = isVisible;
            Frames[ActiveFrameIndex].IsThumbnailDirty = true;
        }
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

    public void ReplaceFrames(List<Frame> newFrames, int activeFrameIdx)
    {
        if (newFrames == null || newFrames.Count == 0) return;

        foreach (var f in Frames) f.Dispose();
        Frames.Clear();

        Frames.AddRange(newFrames);
        ActiveFrameIndex = activeFrameIdx;
    }

    public Color GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || Layers.Count == 0) return TransparentColor;
        return Layers[ActiveLayerIndex].GetPixel(x, y);
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height || Layers.Count == 0) return;
        Layers[ActiveLayerIndex].SetPixel(x, y, color);
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public Color GetMergedPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        Color final = TransparentColor;

        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible || Layers[i].Opacity == 0) continue;
            Color c = Layers[i].GetPixel(x, y);
            if (c.A > 0)
            {
                if (Layers[i].Opacity < 255)
                {
                    c.A = (byte)((c.A * Layers[i].Opacity) / 255);
                    if (c.A == 0) continue;
                }
                final = BlendOver(final, c);
            }
        }
        return final;
    }

    public void Clear(Color color)
    {
        if (Layers.Count == 0) return;
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

        if (Frames.Count > 0)
        {
            Texture2D mergedFrameTex = Frames[ActiveFrameIndex].GetOrUpdateThumbnail(Width, Height);
            Raylib.DrawTexturePro(mergedFrameTex, srcRect, destRect, Vector2.Zero, 0f, Color.White);
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