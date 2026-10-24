using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Layer
{
    public string Name { get; set; }
    public Color[,] Pixels { get; set; }
    public bool IsVisible { get; set; } = true;

    public Layer(string name, int width, int height)
    {
        Name = name;
        Pixels = new Color[width, height];
        
        // Katmanı tamamen şeffaf başlat
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Pixels[x, y] = new Color(0, 0, 0, 0); 
            }
        }
    }

    public Layer Clone()
    {
        int w = Pixels.GetLength(0);
        int h = Pixels.GetLength(1);
        Layer clone = new Layer(Name, w, h);
        clone.IsVisible = IsVisible;
        Array.Copy(Pixels, clone.Pixels, Pixels.Length);
        return clone;
    }
}