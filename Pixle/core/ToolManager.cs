using System;
using System.Collections.Generic;
using Raylib_cs;

namespace PixelArtApp.Core;

public enum ToolType
{
    Pen,
    Eraser,
    Bucket,
    SmartBucket,
    Eyedropper,
    Line,
    Shape,
    SelectMove
}

public enum ShapeType
{
    RectangleOutline,
    RectangleFilled,
    CircleOutline,
    CircleFilled,
    TriangleOutline,
    TriangleFilled
}

public class ToolManager
{
    public int PenSize { get; set; } = 1;
    public int EraserSize { get; set; } = 1;

    public ToolType ActiveTool { get; set; } = ToolType.Pen;
    public ShapeType ActiveShape { get; set; } = ShapeType.RectangleOutline;
    public SelectToolMode ActiveSelectionMode { get; set; } = SelectToolMode.Rectangle;

    public void ApplyTool(Canvas canvas, int startX, int startY, Color color)
    {
        switch (ActiveTool)
        {
            case ToolType.Pen:
            case ToolType.Eraser:
                int size = ActiveTool == ToolType.Pen ? PenSize : EraserSize;
                int offset = size / 2;

                for (int i = 0; i < size; i++)
                {
                    for (int j = 0; j < size; j++)
                    {
                        int px = startX - offset + i;
                        int py = startY - offset + j;
                        
                        if (ActiveTool == ToolType.Pen)
                            canvas.SetPixel(px, py, color);
                        else
                            canvas.SetPixel(px, py, Canvas.TransparentColor);
                    }
                }
                break;

            case ToolType.Bucket:
                FloodFill(canvas, startX, startY, color);
                break;

            case ToolType.SmartBucket:
                ReplaceAllColor(canvas, startX, startY, color);
                break;
        }
    }

    public void ReplaceAllColor(Canvas canvas, int startX, int startY, Color newColor)
    {
        Color targetColor = canvas.GetPixel(startX, startY);
        if (ColorsEqual(targetColor, newColor)) return;

        for (int x = 0; x < canvas.Width; x++)
        {
            for (int y = 0; y < canvas.Height; y++)
            {
                if (ColorsEqual(canvas.GetPixel(x, y), targetColor))
                {
                    canvas.SetPixel(x, y, newColor);
                }
            }
        }
    }

    public void DrawLine(Canvas canvas, int x0, int y0, int x1, int y1, Color color)
    {
        // Fareyi hızlı sürüklerken oluşan çizgilerde de fırça boyutunu uygular
        int size = ActiveTool == ToolType.Pen ? PenSize : EraserSize;
        int offset = size / 2;

        foreach (var (x, y) in GetLinePixels(x0, y0, x1, y1))
        {
            // Sadece tek piksel değil, seçili boyuta göre blok halinde çizer
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    int px = x - offset + i;
                    int py = y - offset + j;

                    if (ActiveTool == ToolType.Eraser) 
                        canvas.SetPixel(px, py, Canvas.TransparentColor);
                    else 
                        canvas.SetPixel(px, py, color);
                }
            }
        }
    }

    public static List<(int x, int y)> GetLinePixels(int x0, int y0, int x1, int y1)
    {
        List<(int x, int y)> list = new();
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            list.Add((x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
        return list;
    }

    public static List<(int x, int y)> GetShapePixels(int x0, int y0, int x1, int y1, ShapeType shape)
    {
        List<(int x, int y)> pixels = new();
        int minX = Math.Min(x0, x1);
        int maxX = Math.Max(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxY = Math.Max(y0, y1);

        switch (shape)
        {
            case ShapeType.RectangleOutline:
                for (int x = minX; x <= maxX; x++) { pixels.Add((x, minY)); pixels.Add((x, maxY)); }
                for (int y = minY; y <= maxY; y++) { pixels.Add((minX, y)); pixels.Add((maxX, y)); }
                break;
            case ShapeType.RectangleFilled:
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        pixels.Add((x, y));
                break;
            case ShapeType.CircleOutline:
            case ShapeType.CircleFilled:
                int rx = Math.Max(1, (maxX - minX) / 2);
                int ry = Math.Max(1, (maxY - minY) / 2);
                int cx = (minX + maxX) / 2;
                int cy = (minY + maxY) / 2;

                if (shape == ShapeType.CircleFilled)
                {
                    for (int y = -ry; y <= ry; y++)
                        for (int x = -rx; x <= rx; x++)
                            if ((x * x * ry * ry) + (y * y * rx * rx) <= (rx * rx * ry * ry))
                                pixels.Add((cx + x, cy + y));
                }
                else
                {
                    int x = 0, y = ry;
                    long rx2 = (long)rx * rx, ry2 = (long)ry * ry;
                    long twoRx2 = 2 * rx2, twoRy2 = 2 * ry2;
                    long px = 0, py = twoRx2 * y;

                    void AddSymmetric(int sx, int sy)
                    {
                        pixels.Add((cx + sx, cy + sy)); pixels.Add((cx - sx, cy + sy));
                        pixels.Add((cx + sx, cy - sy)); pixels.Add((cx - sx, cy - sy));
                    }

                    long p = (long)Math.Round(ry2 - (rx2 * ry) + (0.25 * rx2));
                    while (px < py)
                    {
                        AddSymmetric(x, y);
                        x++;
                        px += twoRy2;
                        if (p < 0) p += ry2 + px;
                        else { y--; py -= twoRx2; p += ry2 + px - py; }
                    }

                    p = (long)Math.Round(ry2 * (x + 0.5) * (x + 0.5) + rx2 * (y - 1) * (y - 1) - rx2 * ry2);
                    while (y >= 0)
                    {
                        AddSymmetric(x, y);
                        y--;
                        py -= twoRx2;
                        if (p > 0) p += rx2 - py;
                        else { x++; px += twoRy2; p += rx2 - py + px; }
                    }
                }
                break;
            case ShapeType.TriangleOutline:
                int midTopX = (minX + maxX) / 2;
                pixels.AddRange(GetLinePixels(midTopX, minY, minX, maxY));
                pixels.AddRange(GetLinePixels(minX, maxY, maxX, maxY));
                pixels.AddRange(GetLinePixels(maxX, maxY, midTopX, minY));
                break;
            case ShapeType.TriangleFilled:
                int topX = (minX + maxX) / 2;
                for (int y = minY; y <= maxY; y++)
                {
                    float factor = (maxY == minY) ? 1.0f : (float)(y - minY) / (maxY - minY);
                    int curMinX = (int)(topX - (topX - minX) * factor);
                    int curMaxX = (int)(topX + (maxX - topX) * factor);
                    for (int x = curMinX; x <= curMaxX; x++) pixels.Add((x, y));
                }
                break;
        }
        return pixels;
    }

    private void FloodFill(Canvas canvas, int startX, int startY, Color newColor)
    {
        Color targetColor = canvas.GetPixel(startX, startY);
        if (ColorsEqual(targetColor, newColor)) return;

        Queue<(int x, int y)> queue = new();
        queue.Enqueue((startX, startY));

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || x >= canvas.Width || y < 0 || y >= canvas.Height) continue;
            if (!ColorsEqual(canvas.GetPixel(x, y), targetColor)) continue;

            canvas.SetPixel(x, y, newColor);
            queue.Enqueue((x + 1, y)); queue.Enqueue((x - 1, y));
            queue.Enqueue((x, y + 1)); queue.Enqueue((x, y - 1));
        }
    }

    public bool ColorsEqual(Color c1, Color c2)
    {
        return c1.R == c2.R && c1.G == c2.G && c1.B == c2.B && c1.A == c2.A;
    }
}