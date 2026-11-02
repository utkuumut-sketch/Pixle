// === DOSYA: \Core\ProjectTab.cs ===
using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public class ProjectTab
{
    public string Title { get; set; }
    public string? FilePath { get; set; }

    private bool isDirty = false;
    public bool IsDirty
    {
        get
        {
            if (!isDirty && HasStructuralOrLayerChanges())
            {
                isDirty = true;
            }
            return isDirty;
        }
        set
        {
            isDirty = value;
            if (!value)
            {
                CaptureSavedSignature();
            }
        }
    }

    private float fps = 8f;
    public float Fps
    {
        get => fps;
        set
        {
            if (Math.Abs(fps - value) > 0.001f)
            {
                fps = value;
                isDirty = true;
            }
        }
    }

    public Canvas Canvas { get; set; }
    public HistoryManager History { get; set; }
    public SelectionManager Selection { get; set; }
    public Camera2D Camera { get; set; }
    public int ZoomIndex { get; set; } = 2;

    private int savedStateHash = 0;

    public ProjectTab(string title, int width, int height, Color bgColor, bool showGrid)
    {
        Title = title;
        int scale = width switch { <= 16 => 24, <= 32 => 14, <= 64 => 8, _ => 4 };
        Canvas = new Canvas(width, height, scale, System.Numerics.Vector2.Zero);
        Canvas.ShowGrid = showGrid;
        if (bgColor.A > 0) Canvas.Clear(bgColor);

        History = new HistoryManager();
        History.OnStateChanged = () => isDirty = true;

        Selection = new SelectionManager();
        Selection.EnsureSize(width, height);

        MarkSaved();
    }

    public void MarkSaved()
    {
        isDirty = false;
        CaptureSavedSignature();
    }

    private void CaptureSavedSignature()
    {
        savedStateHash = ComputeStructuralSignature();
    }

    private bool HasStructuralOrLayerChanges()
    {
        if (Selection != null && Selection.IsFloating) return true;
        return ComputeStructuralSignature() != savedStateHash;
    }

    private int ComputeStructuralSignature()
    {
        if (Canvas == null) return 0;

        HashCode hc = new();
        hc.Add(Canvas.ShowGrid);
        hc.Add(Canvas.Frames.Count);

        for (int f = 0; f < Canvas.Frames.Count; f++)
        {
            var frame = Canvas.Frames[f];
            hc.Add(frame.IsVisible);
            hc.Add(frame.Layers.Count);

            for (int l = 0; l < frame.Layers.Count; l++)
            {
                var layer = frame.Layers[l];
                hc.Add(layer.Name);
                hc.Add(layer.IsVisible);
                hc.Add(layer.Opacity);
            }
        }

        return hc.ToHashCode();
    }

    public string GetDisplayTitle() => IsDirty ? $"{Title}*" : Title;
}