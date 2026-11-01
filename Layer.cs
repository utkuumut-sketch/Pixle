using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Layer : IDisposable
{
    public string Name { get; set; }
    public int Width { get; }
    public int Height { get; }

    // CoW GUVENLIGI: Dizi disariya dogrudan acilmaz, yazma islemleri metotlarla kontrol edilir.
    private Color[,] pixels;
    private bool isPixelsShared = false;

    // Sadece okuma (Export, Thumbnail, vb.) yapan yerler icin salt-okunur erisim
    // Layer.cs icinde:
public Color[,] Pixels => pixels; // Disaridan new Color[,] atanmasini engeller, EnsureUniquePixels() olmadanSetPixel kullanilmasini saglar.

    private bool isVisible = true;
    public bool IsVisible 
    { 
        get => isVisible; 
        set { if (isVisible != value) { isVisible = value; IsDirty = true; } } 
    }

    public bool IsDirty { get; set; } = true;
    private Texture2D cachedTexture;
    private bool hasTexture = false;
    private Color[]? flatBuffer;

    public Layer(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
        pixels = new Color[width, height];
    }

    // RAM OPTIMIZASYONU: Clone() sirasinda gereksiz Color[,] tahsisatini onleyen ozel kurucu
    private Layer(string name, Color[,] sharedPixels, bool visible)
    {
        Name = name;
        Width = sharedPixels.GetLength(0);
        Height = sharedPixels.GetLength(1);
        pixels = sharedPixels;
        isVisible = visible;
        isPixelsShared = true;
        IsDirty = true;
    }

    public Color GetPixel(int x, int y) => pixels[x, y];

    public void SetPixel(int x, int y, Color color)
    {
        EnsureUniquePixels();
        pixels[x, y] = color;
        IsDirty = true;
    }

    // Dosyadan ilk yukleme gibi CoW paylasimi olmayan toplu yazimlar icin
    public void SetPixelDirect(int x, int y, Color color)
    {
        pixels[x, y] = color;
    }

    public void Fill(Color color)
    {
        EnsureUniquePixels();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                pixels[x, y] = color;
        IsDirty = true;
    }

    public void EnsureUniquePixels()
    {
        if (isPixelsShared)
        {
            Color[,] newPixels = new Color[Width, Height];
            Array.Copy(pixels, newPixels, pixels.Length);
            pixels = newPixels;
            isPixelsShared = false;
        }
    }

    public Texture2D GetOrUpdateTexture()
    {
        if (!hasTexture)
        {
            Image img = Raylib.GenImageColor(Width, Height, new Color(0, 0, 0, 0));
            cachedTexture = Raylib.LoadTextureFromImage(img);
            Raylib.SetTextureFilter(cachedTexture, TextureFilter.Point);
            Raylib.UnloadImage(img);
            hasTexture = true;
            IsDirty = true;
        }

        if (IsDirty)
        {
            if (flatBuffer == null)
                flatBuffer = new Color[Width * Height];

            for (int y = 0; y < Height; y++)
            {
                int rowOffset = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    flatBuffer[rowOffset + x] = pixels[x, y];
                }
            }
            Raylib.UpdateTexture(cachedTexture, flatBuffer);
            IsDirty = false;
        }

        return cachedTexture;
    }

    public Layer Clone()
    {
        this.isPixelsShared = true;
        return new Layer(Name, this.pixels, this.isVisible);
    }

    public void Dispose()
    {
        if (hasTexture)
        {
            Raylib.UnloadTexture(cachedTexture);
            hasTexture = false;
        }
        flatBuffer = null; 
    }
}