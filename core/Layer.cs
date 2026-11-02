// === DOSYA: \Core\Layer.cs ===
using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Layer : IDisposable
{
    private sealed class PixelBuffer
    {
        public readonly Color[,] Data;
        public int RefCount;

        public PixelBuffer(int width, int height)
        {
            Data = new Color[width, height];
            RefCount = 1;
        }

        public PixelBuffer(Color[,] copiedData)
        {
            Data = copiedData;
            RefCount = 1;
        }
    }

    public readonly struct ReadOnlyPixelGrid
    {
        private readonly Color[,] data;
        internal ReadOnlyPixelGrid(Color[,] data) => this.data = data;
        public Color this[int x, int y] => data[x, y];
    }

    public string Name { get; set; }
    public int Width { get; }
    public int Height { get; }

    private PixelBuffer buffer;
    private bool isDisposed = false;

    public ReadOnlyPixelGrid Pixels => new ReadOnlyPixelGrid(buffer.Data);

    private bool isVisible = true;
    public bool IsVisible
    {
        get => isVisible;
        set
        {
            if (isVisible != value)
            {
                isVisible = value;
                IsDirty = true;
                VisibilityChanged = true;
            }
        }
    }

    private byte opacity = 255;
    public byte Opacity
    {
        get => opacity;
        set
        {
            if (opacity != value)
            {
                opacity = value;
                IsDirty = true;
                VisibilityChanged = true;
            }
        }
    }

    public bool IsDirty { get; set; } = true;
    internal bool VisibilityChanged { get; set; } = true;

    private Texture2D cachedTexture;
    private bool hasTexture = false;
    private Color[]? flatBuffer;

    public Layer(string name, int width, int height)
    {
        Name = name;
        Width = width;
        Height = height;
        buffer = new PixelBuffer(width, height);
    }

    private Layer(string name, PixelBuffer sharedBuffer, bool visible, byte layerOpacity)
    {
        Name = name;
        Width = sharedBuffer.Data.GetLength(0);
        Height = sharedBuffer.Data.GetLength(1);
        buffer = sharedBuffer;
        buffer.RefCount++;
        isVisible = visible;
        opacity = layerOpacity;
        IsDirty = true;
        VisibilityChanged = true;
    }

    public Color GetPixel(int x, int y)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return Canvas.TransparentColor;
        return buffer.Data[x, y];
    }

    public void SetPixel(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        EnsureUniquePixels();
        buffer.Data[x, y] = color;
        IsDirty = true;
    }

    public void SetPixelDirect(int x, int y, Color color)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        EnsureUniquePixels();
        buffer.Data[x, y] = color;
        IsDirty = true;
    }

    public void Fill(Color color)
    {
        EnsureUniquePixels();
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                buffer.Data[x, y] = color;
        IsDirty = true;
    }

    public void EnsureUniquePixels()
    {
        if (buffer.RefCount > 1)
        {
            Color[,] newPixels = new Color[Width, Height];
            Array.Copy(buffer.Data, newPixels, buffer.Data.Length);
            buffer.RefCount--;
            buffer = new PixelBuffer(newPixels);
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
            flatBuffer ??= new Color[Width * Height];

            var data = buffer.Data;
            for (int y = 0; y < Height; y++)
            {
                int rowOffset = y * Width;
                for (int x = 0; x < Width; x++)
                {
                    flatBuffer[rowOffset + x] = data[x, y];
                }
            }
            Raylib.UpdateTexture(cachedTexture, flatBuffer);
            IsDirty = false;
        }

        return cachedTexture;
    }

    public Layer Clone()
    {
        return new Layer(Name, this.buffer, this.isVisible, this.opacity);
    }

    public void Dispose()
    {
        if (!isDisposed)
        {
            if (buffer.RefCount > 1)
                buffer.RefCount--;
            isDisposed = true;
        }

        if (hasTexture)
        {
            Raylib.UnloadTexture(cachedTexture);
            hasTexture = false;
        }
        flatBuffer = null;
    }
}