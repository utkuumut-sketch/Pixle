using System;
using System.Collections.Generic;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Frame : IDisposable
{
    public List<Layer> Layers { get; set; } = new();
    public int ActiveLayerIndex { get; set; } = 0;
    public bool IsVisible { get; set; } = true;

    // PERFORMANS: Animasyon Karesi Birleşik Önizleme Önbelleği
    public bool IsThumbnailDirty { get; set; } = true;
    private Texture2D thumbTexture;
    private bool hasThumb = false;
    private Color[]? flatThumb;

    public Texture2D GetOrUpdateThumbnail(int w, int h)
    {
        foreach (var l in Layers)
        {
            if (l.IsDirty) IsThumbnailDirty = true;
        }

        if (!hasThumb)
        {
            Image img = Raylib.GenImageColor(w, h, new Color(0, 0, 0, 0));
            thumbTexture = Raylib.LoadTextureFromImage(img);
            Raylib.SetTextureFilter(thumbTexture, TextureFilter.Point);
            Raylib.UnloadImage(img);
            flatThumb = new Color[w * h];
            hasThumb = true;
            IsThumbnailDirty = true;
        }

        if (IsThumbnailDirty && flatThumb != null)
        {
            Array.Clear(flatThumb, 0, flatThumb.Length);
            for (int l = 0; l < Layers.Count; l++)
            {
                if (!Layers[l].IsVisible) continue;
                var p = Layers[l].Pixels;
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        if (p[x, y].A > 0) flatThumb[row + x] = p[x, y];
                    }
                }
            }
            Raylib.UpdateTexture(thumbTexture, flatThumb);
            IsThumbnailDirty = false;
        }

        return thumbTexture;
    }

    public Frame Clone()
    {
        var clone = new Frame();
        clone.ActiveLayerIndex = ActiveLayerIndex;
        clone.IsVisible = IsVisible;
        foreach (var l in Layers)
        {
            clone.Layers.Add(l.Clone());
        }
        return clone;
    }

    public void Dispose()
    {
        if (hasThumb)
        {
            Raylib.UnloadTexture(thumbTexture);
            hasThumb = false;
        }
        foreach (var l in Layers) l.Dispose();
    }
}