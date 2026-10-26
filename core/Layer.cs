using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Layer : IDisposable
{
    public string Name { get; set; }
    public Color[,] Pixels { get; set; }

    private bool isVisible = true;
    public bool IsVisible 
    { 
        get => isVisible; 
        set { if (isVisible != value) { isVisible = value; IsDirty = true; } } 
    }

    // PERFORMANS: GPU Doku Önbelleği
    public bool IsDirty { get; set; } = true;
    private Texture2D cachedTexture;
    private bool hasTexture = false;
    private Color[] flatBuffer;

    public Layer(string name, int width, int height)
    {
        Name = name;
        Pixels = new Color[width, height];
        flatBuffer = new Color[width * height];
    }

    public Texture2D GetOrUpdateTexture()
    {
        int w = Pixels.GetLength(0);
        int h = Pixels.GetLength(1);

        if (!hasTexture)
        {
            Image img = Raylib.GenImageColor(w, h, new Color(0, 0, 0, 0));
            cachedTexture = Raylib.LoadTextureFromImage(img);
            Raylib.SetTextureFilter(cachedTexture, TextureFilter.Point);
            Raylib.UnloadImage(img);
            hasTexture = true;
            IsDirty = true;
        }

        if (IsDirty)
        {
            for (int y = 0; y < h; y++)
            {
                int rowOffset = y * w;
                for (int x = 0; x < w; x++)
                {
                    flatBuffer[rowOffset + x] = Pixels[x, y];
                }
            }
            Raylib.UpdateTexture(cachedTexture, flatBuffer);
            IsDirty = false;
        }

        return cachedTexture;
    }

    public Layer Clone()
    {
        int w = Pixels.GetLength(0);
        int h = Pixels.GetLength(1);
        Layer clone = new Layer(Name, w, h);
        clone.isVisible = this.isVisible;
        Array.Copy(Pixels, clone.Pixels, Pixels.Length);
        clone.IsDirty = true;
        return clone;
    }

    public void Dispose()
    {
        if (hasTexture)
        {
            Raylib.UnloadTexture(cachedTexture);
            hasTexture = false;
        }
    }
}