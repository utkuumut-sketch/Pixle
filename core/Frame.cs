using System.Collections.Generic;

namespace PixelArtApp.Core;

public class Frame
{
    public List<Layer> Layers { get; set; } = new();
    public int ActiveLayerIndex { get; set; } = 0;
    public bool IsVisible { get; set; } = true; // YENİ: Animasyonda atlanıp atlanmayacağı

    public Frame Clone()
    {
        var clone = new Frame();
        clone.ActiveLayerIndex = ActiveLayerIndex;
        clone.IsVisible = IsVisible; // Kopyalarken durumu koru
        foreach (var l in Layers)
        {
            clone.Layers.Add(l.Clone());
        }
        return clone;
    }
}