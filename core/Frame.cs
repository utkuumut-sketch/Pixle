// === DOSYA: \Core\Frame.cs ===
using System;
using System.Collections.Generic;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Frame : IDisposable
{
    public List<Layer> Layers { get; set; } = new();
    private int activeLayerIndex = 0;

    public int ActiveLayerIndex
    {
        get => Layers.Count == 0 ? 0 : Math.Clamp(activeLayerIndex, 0, Layers.Count - 1);
        set => activeLayerIndex = Layers.Count == 0 ? 0 : Math.Clamp(value, 0, Layers.Count - 1);
    }

    public bool IsVisible { get; set; } = true;

    public bool IsThumbnailDirty { get; set; } = true;
    private Texture2D thumbTexture;
    private bool hasThumb = false;
    private Color[]? flatThumb;

    public Texture2D GetOrUpdateThumbnail(int w, int h)
    {
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].IsDirty || Layers[i].VisibilityChanged)
            {
                IsThumbnailDirty = true;
                Layers[i].IsDirty = false;
                Layers[i].VisibilityChanged = false;
            }
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
                if (!Layers[l].IsVisible || Layers[l].Opacity == 0) continue;
                var p = Layers[l].Pixels;
                byte layerOpacity = Layers[l].Opacity;

                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 0; x < w; x++)
                    {
                        Color topColor = p[x, y];
                        if (topColor.A == 0) continue;

                        if (layerOpacity < 255)
                        {
                            topColor.A = (byte)((topColor.A * layerOpacity) / 255);
                            if (topColor.A == 0) continue;
                        }

                        int idx = row + x;
                        flatThumb[idx] = Canvas.BlendOver(flatThumb[idx], topColor);
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
        var clone = new Frame
        {
            ActiveLayerIndex = this.ActiveLayerIndex,
            IsVisible = this.IsVisible
        };
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