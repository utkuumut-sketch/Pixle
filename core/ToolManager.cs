using System;
using System.Collections.Generic;
using Raylib_cs;

namespace PixelArtApp.Core;

public enum ToolType
{
    Pen,
    Eraser,
    Dither,
    Shading,
    Bucket,
    SmartBucket,
    Eyedropper,
    Blur,
    Jumble,
    Line,
    Curve,
    Shape,
    SelectMove
}

public enum DitherPattern
{
    Checker50,       // %50 Dama
    Sparse25,        // %25 Seyrek Nokta
    Dense75,         // %75 Yogun Doku
    VerticalLines,   // Dikey Cizgili
    HorizontalLines, // Yatay Cizgili
    DiagonalLines    // Capraz Doku
}

public enum ShadingMode
{
    PaletteRamp,     // Palet Tonlama (Secili paletteki acik/koyu tonlara gecis)
    SmoothHsv        // Yumusak Tonlama (HSL/HSV isik ve golge gecisi)
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

public enum SymmetryMode
{
    None,
    Horizontal, // Sol <-> Sag aynalama (dikey eksen)
    Vertical,   // Ust <-> Alt aynalama (yatay eksen)
    Both
}

public class ToolManager
{
    public int PenSize { get; set; } = 1;
    public int EraserSize { get; set; } = 1;

    public ToolType ActiveTool { get; set; } = ToolType.Pen;
    public DitherPattern ActiveDither { get; set; } = DitherPattern.Checker50;
    public ShadingMode ActiveShadingMode { get; set; } = ShadingMode.PaletteRamp;
    public ShapeType ActiveShape { get; set; } = ShapeType.RectangleOutline;
    public SelectToolMode ActiveSelectionMode { get; set; } = SelectToolMode.Rectangle;

    // PIXEL PERFECT: 1px kalem/silgi darbelerindeki "L" seklindeki fazla pikselleri temizler
    public bool PixelPerfect { get; set; } = false;

    // SIMETRI: Cizimi tuval merkezine gore aynalar
    public SymmetryMode Symmetry { get; set; } = SymmetryMode.None;

    private readonly Random rng = new();

    // Pixel-perfect darbe gecmisi: her nokta icin (ana nokta + simetrik kopyalar) ve onceki renkleri
    private readonly List<List<(int x, int y, Color orig)>> ppStroke = new();
    private (int x, int y)? ppLast;

    // Shading fircasinda tek bir surukleme darbesinde ayni pikselin defalarca yanmasini onler
    private readonly HashSet<(int x, int y)> shadedInStroke = new();

    public static string SymmetryLabel(SymmetryMode mode) => mode switch
    {
        SymmetryMode.Horizontal => "Dikey Eksen",
        SymmetryMode.Vertical => "Yatay Eksen",
        SymmetryMode.Both => "4 Yonlu",
        _ => "Kapali"
    };

    public static string DitherLabel(DitherPattern pattern) => pattern switch
    {
        DitherPattern.Checker50 => "%50 Dama",
        DitherPattern.Sparse25 => "%25 Seyrek",
        DitherPattern.Dense75 => "%75 Yogun",
        DitherPattern.VerticalLines => "Dikey Cizgi",
        DitherPattern.HorizontalLines => "Yatay Cizgi",
        DitherPattern.DiagonalLines => "Capraz Doku",
        _ => "%50 Dama"
    };

    public static string ShadingModeLabel(ShadingMode mode) => mode switch
    {
        ShadingMode.PaletteRamp => "Palet Tonlama",
        ShadingMode.SmoothHsv => "Yumusak Tonlama",
        _ => "Palet Tonlama"
    };

    /// <summary>
    /// Dunya (tuval) koordinati (x, y)'nin secili dither desenine gore boyanip boyanmayacagini belirler.
    /// Tuval izgarasina kilitli oldugu icin fircayi gezdirdikce desen asla bozulmaz!
    /// </summary>
    public static bool ShouldPaintDither(int x, int y, DitherPattern pattern)
    {
        int mx = ((x % 2) + 2) % 2;
        int my = ((y % 2) + 2) % 2;

        return pattern switch
        {
            DitherPattern.Checker50 => ((mx + my) & 1) == 0,
            DitherPattern.Sparse25 => mx == 0 && my == 0,
            DitherPattern.Dense75 => !(mx == 1 && my == 1),
            DitherPattern.VerticalLines => mx == 0,
            DitherPattern.HorizontalLines => my == 0,
            DitherPattern.DiagonalLines => (((x - y) % 3 + 3) % 3) == 0,
            _ => ((mx + my) & 1) == 0
        };
    }

    public void CycleSymmetry()
    {
        Symmetry = Symmetry switch
        {
            SymmetryMode.None => SymmetryMode.Horizontal,
            SymmetryMode.Horizontal => SymmetryMode.Vertical,
            SymmetryMode.Vertical => SymmetryMode.Both,
            _ => SymmetryMode.None
        };
    }

    /// <summary>Yeni bir serbest cizim darbesi baslarken cagrilmali.</summary>
    public void BeginStroke()
    {
        ppStroke.Clear();
        ppLast = null;
        shadedInStroke.Clear();
    }

    /// <summary>Verilen noktayi ve aktif simetri moduna gore aynalanmis kopyalarini dondurur.</summary>
    public IEnumerable<(int x, int y)> GetSymmetricPoints(Canvas canvas, int x, int y)
    {
        if (canvas.TileWrapMode)
        {
            (x, y) = canvas.WrapCoord(x, y);
        }

        yield return (x, y);
        if (Symmetry == SymmetryMode.None) yield break;

        bool h = Symmetry == SymmetryMode.Horizontal || Symmetry == SymmetryMode.Both;
        bool v = Symmetry == SymmetryMode.Vertical || Symmetry == SymmetryMode.Both;
        int mx = canvas.Width - 1 - x;
        int my = canvas.Height - 1 - y;

        if (h && mx != x) yield return (mx, y);
        if (v && my != y) yield return (x, my);
        if (h && v && mx != x && my != y) yield return (mx, my);
    }

    /// <summary>Tek bir pikseli simetriye uyarak boyar.</summary>
    public void PlotSymmetric(Canvas canvas, int x, int y, Color color)
    {
        foreach (var (px, py) in GetSymmetricPoints(canvas, x, y))
            canvas.SetPixel(px, py, color);
    }

    /// <summary>Cizgi, egri ve sekil noktalarini firca kalinligina (PenSize) ve simetriye gore boyar.</summary>
    public void PlotStrokePoint(Canvas canvas, int x, int y, Color color, bool useBrushSize = true)
    {
        int size = useBrushSize ? Math.Max(1, PenSize) : 1;
        if (size <= 1)
        {
            PlotSymmetric(canvas, x, y, color);
        }
        else
        {
            Stamp(canvas, x, y, size, color);
        }
    }

    private void Stamp(Canvas canvas, int cx, int cy, int size, Color color)
    {
        int offset = size / 2;
        for (int i = 0; i < size; i++)
            for (int j = 0; j < size; j++)
                PlotSymmetric(canvas, cx - offset + i, cy - offset + j, color);
    }

    private void StampDither(Canvas canvas, int cx, int cy, int size, Color color)
    {
        int effSize = Math.Max(1, size);
        int offset = effSize / 2;
        for (int i = 0; i < effSize; i++)
        {
            for (int j = 0; j < effSize; j++)
            {
                foreach (var (px, py) in GetSymmetricPoints(canvas, cx - offset + i, cy - offset + j))
                {
                    if (ShouldPaintDither(px, py, ActiveDither))
                        canvas.SetPixel(px, py, color);
                }
            }
        }
    }

    private void StampShading(Canvas canvas, int cx, int cy, int size, bool darken, IReadOnlyList<Color>? paletteColors)
    {
        int effSize = Math.Max(1, size);
        int offset = effSize / 2;
        for (int i = 0; i < effSize; i++)
        {
            for (int j = 0; j < effSize; j++)
            {
                foreach (var (rawX, rawY) in GetSymmetricPoints(canvas, cx - offset + i, cy - offset + j))
                {
                    int px = rawX, py = rawY;
                    if (canvas.TileWrapMode)
                    {
                        (px, py) = canvas.WrapCoord(px, py);
                    }
                    else if (px < 0 || px >= canvas.Width || py < 0 || py >= canvas.Height)
                    {
                        continue;
                    }

                    if (!shadedInStroke.Add((px, py))) continue;

                    Color current = canvas.GetPixel(px, py);
                    if (current.A == 0) continue;

                    Color shaded = GetShadedColor(current, darken, paletteColors, ActiveShadingMode);
                    canvas.SetPixel(px, py, shaded);
                }
            }
        }
    }

    public static Color GetShadedColor(Color c, bool darken, IReadOnlyList<Color>? paletteColors, ShadingMode mode)
    {
        if (c.A == 0) return c;

        if (mode == ShadingMode.PaletteRamp && paletteColors != null && paletteColors.Count >= 2)
        {
            RgbToHsv(c, out float curH, out float curS, out float curV);
            int curLum = c.R * 299 + c.G * 587 + c.B * 114;
            int curIdx = -1;
            for (int i = 0; i < paletteColors.Count; i++)
            {
                if (paletteColors[i].R == c.R && paletteColors[i].G == c.G && paletteColors[i].B == c.B)
                {
                    curIdx = i;
                    break;
                }
            }

            int bestIdx = -1;
            double bestScore = double.MaxValue;

            for (int i = 0; i < paletteColors.Count; i++)
            {
                Color cand = paletteColors[i];
                if (cand.A == 0) continue;
                if (cand.R == c.R && cand.G == c.G && cand.B == c.B) continue;

                int candLum = cand.R * 299 + cand.G * 587 + cand.B * 114;
                int lumDiff = candLum - curLum;

                // Aciklastirma (Sol Tik) icin daha parlak, Koyulastirma (Sag Tik) icin daha koyu olmali
                if (!darken && lumDiff <= 1500) continue;
                if (darken && lumDiff >= -1500) continue;

                RgbToHsv(cand, out float candH, out float candS, out float candV);
                float hueDiff = Math.Abs(candH - curH);
                if (hueDiff > 180f) hueDiff = 360f - hueDiff;

                // Renkli piksellerin alakasiz bir renk tonuna (ornegin kirmizidan yesile/maviye) atlamasini engelle
                if (curS > 0.14f && candS > 0.14f && hueDiff > 48f) continue;
                if (curS > 0.18f && candS <= 0.14f)
                {
                    if (!darken && candV < 0.86f) continue;
                    if (darken && candV > 0.22f) continue;
                    hueDiff = 18f;
                }
                else if (curS <= 0.14f && candS > 0.28f)
                {
                    continue;
                }

                double dr = cand.R - c.R;
                double dg = cand.G - c.G;
                double db = cand.B - c.B;
                double rgbDist = Math.Sqrt(dr * dr + dg * dg + db * db);
                double absLumStep = Math.Abs(lumDiff) / 1000.0;

                // Palet sirasinda yan yana olan (ramp) renklere oncelik bonusu ver
                double adjacencyBonus = (curIdx >= 0 && Math.Abs(i - curIdx) == 1) ? 0.55 : 1.0;
                double score = (hueDiff * 2.8 + absLumStep * 1.15 + rgbDist * 1.0) * adjacencyBonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestIdx = i;
                }
            }

            if (bestIdx >= 0)
            {
                Color chosen = paletteColors[bestIdx];
                return new Color(chosen.R, chosen.G, chosen.B, c.A);
            }
        }

        // Smooth HSV Tonlama (veya palette uygun ton kalmadiginda yumusak gecis)
        RgbToHsv(c, out float h, out float s, out float v);
        if (!darken)
        {
            v = Math.Min(1f, v + 0.09f);
            if (v > 0.82f) s = Math.Max(0f, s - 0.06f);
        }
        else
        {
            v = Math.Max(0.04f, v - 0.09f);
            if (s > 0.05f) s = Math.Min(1f, s + 0.04f);
        }
        return HsvToRgb(h, s, v, c.A);
    }

    private static void RgbToHsv(Color color, out float h, out float s, out float v)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0) h = 0;
        else if (max == r) h = 60f * (((g - b) / delta) % 6f);
        else if (max == g) h = 60f * (((b - r) / delta) + 2f);
        else h = 60f * (((r - g) / delta) + 4f);

        if (h < 0) h += 360f;
    }

    private static Color HsvToRgb(float h, float s, float v, byte alpha)
    {
        float c = v * s;
        float x = c * (1f - Math.Abs((h / 60f % 2f) - 1f));
        float m = v - c;

        float r = 0, g = 0, b = 0;
        if (h < 60f) { r = c; g = x; b = 0; }
        else if (h < 120f) { r = x; g = c; b = 0; }
        else if (h < 180f) { r = 0; g = c; b = x; }
        else if (h < 240f) { r = 0; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return new Color(
            (byte)Math.Clamp(Math.Round((r + m) * 255f), 0, 255),
            (byte)Math.Clamp(Math.Round((g + m) * 255f), 0, 255),
            (byte)Math.Clamp(Math.Round((b + m) * 255f), 0, 255),
            alpha
        );
    }

    private void PixelPerfectPlot(Canvas canvas, int x, int y, Color color)
    {
        if (ppLast.HasValue && ppLast.Value == (x, y)) return;
        ppLast = (x, y);

        if (!canvas.TileWrapMode && (x < 0 || x >= canvas.Width || y < 0 || y >= canvas.Height))
        {
            ppStroke.Clear();
            return;
        }

        var writes = new List<(int x, int y, Color orig)>();
        foreach (var (px, py) in GetSymmetricPoints(canvas, x, y))
        {
            writes.Add((px, py, canvas.GetPixel(px, py)));
            canvas.SetPixel(px, py, color);
        }
        ppStroke.Add(writes);

        if (ppStroke.Count >= 3)
        {
            var a = ppStroke[^3][0];
            var b = ppStroke[^2][0];
            var c = ppStroke[^1][0];

            bool bAdjA = Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) == 1;
            bool bAdjC = Math.Abs(c.x - b.x) + Math.Abs(c.y - b.y) == 1;
            bool diagonal = Math.Abs(a.x - c.x) == 1 && Math.Abs(a.y - c.y) == 1;

            if (bAdjA && bAdjC && diagonal)
            {
                var middle = ppStroke[^2];
                for (int i = middle.Count - 1; i >= 0; i--)
                    canvas.SetPixel(middle[i].x, middle[i].y, middle[i].orig);
                ppStroke.RemoveAt(ppStroke.Count - 2);
            }
        }

        if (ppStroke.Count > 8) ppStroke.RemoveAt(0);
    }

    public void ApplyTool(
        Canvas canvas,
        int startX,
        int startY,
        Color color,
        bool forceEraser = false,
        bool darkenShading = false,
        IReadOnlyList<Color>? paletteColors = null)
    {
        if (ActiveTool == ToolType.Shading && !forceEraser)
        {
            StampShading(canvas, startX, startY, PenSize, darkenShading, paletteColors);
            return;
        }

        if (forceEraser || ActiveTool == ToolType.Pen || ActiveTool == ToolType.Eraser)
        {
            bool isErasing = forceEraser || ActiveTool == ToolType.Eraser;
            int size = isErasing ? EraserSize : PenSize;
            Color drawColor = isErasing ? Canvas.TransparentColor : color;

            if (PixelPerfect && size == 1) PixelPerfectPlot(canvas, startX, startY, drawColor);
            else Stamp(canvas, startX, startY, size, drawColor);
            return;
        }

        switch (ActiveTool)
        {
            case ToolType.Dither:
                StampDither(canvas, startX, startY, PenSize, color);
                break;

            case ToolType.Bucket:
                foreach (var (sx, sy) in new List<(int, int)>(GetSymmetricPoints(canvas, startX, startY)))
                    FloodFill(canvas, sx, sy, color);
                break;

            case ToolType.SmartBucket:
                ReplaceAllColor(canvas, startX, startY, color);
                break;

            case ToolType.Blur:
                foreach (var (sx, sy) in new List<(int, int)>(GetSymmetricPoints(canvas, startX, startY)))
                    ApplyBlur(canvas, sx, sy, PenSize);
                break;

            case ToolType.Jumble:
                foreach (var (sx, sy) in new List<(int, int)>(GetSymmetricPoints(canvas, startX, startY)))
                    ApplyJumble(canvas, sx, sy, PenSize);
                break;
        }
    }

    // ASEPRITE BLUR TOOL: Komsu piksellerin agirlikli ortalamasini alarak yumusatir
    public void ApplyBlur(Canvas canvas, int centerX, int centerY, int size)
    {
        int offset = size / 2;
        int minX = canvas.TileWrapMode ? (centerX - offset) : Math.Max(0, centerX - offset);
        int maxX = canvas.TileWrapMode ? (centerX - offset + size - 1) : Math.Min(canvas.Width - 1, centerX - offset + size - 1);
        int minY = canvas.TileWrapMode ? (centerY - offset) : Math.Max(0, centerY - offset);
        int maxY = canvas.TileWrapMode ? (centerY - offset + size - 1) : Math.Min(canvas.Height - 1, centerY - offset + size - 1);

        int regionW = maxX - minX + 1;
        int regionH = maxY - minY + 1;
        if (regionW <= 0 || regionH <= 0) return;

        int sampleMinX = canvas.TileWrapMode ? (minX - 1) : Math.Max(0, minX - 1);
        int sampleMaxX = canvas.TileWrapMode ? (maxX + 1) : Math.Min(canvas.Width - 1, maxX + 1);
        int sampleMinY = canvas.TileWrapMode ? (minY - 1) : Math.Max(0, minY - 1);
        int sampleMaxY = canvas.TileWrapMode ? (maxY + 1) : Math.Min(canvas.Height - 1, maxY + 1);

        int sampleW = sampleMaxX - sampleMinX + 1;
        int sampleH = sampleMaxY - sampleMinY + 1;
        Color[,] snapshot = new Color[sampleW, sampleH];

        for (int x = sampleMinX; x <= sampleMaxX; x++)
        {
            for (int y = sampleMinY; y <= sampleMaxY; y++)
            {
                snapshot[x - sampleMinX, y - sampleMinY] = canvas.GetPixel(x, y);
            }
        }

        int centerWeight = Math.Max(1, 6 - AppSettings.BlurIntensity);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                long sumR = 0, sumG = 0, sumB = 0, sumA = 0;
                long colorWeightSum = 0;
                int totalKernelWeight = 0;

                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < sampleMinX || nx > sampleMaxX || ny < sampleMinY || ny > sampleMaxY) continue;

                        int kWeight = (dx == 0 && dy == 0) ? centerWeight : 1;
                        Color c = snapshot[nx - sampleMinX, ny - sampleMinY];

                        sumA += c.A * kWeight;
                        totalKernelWeight += kWeight;

                        if (c.A > 0)
                        {
                            long cw = c.A * kWeight;
                            sumR += c.R * cw;
                            sumG += c.G * cw;
                            sumB += c.B * cw;
                            colorWeightSum += cw;
                        }
                    }
                }

                if (totalKernelWeight == 0) continue;
                byte newA = (byte)Math.Clamp(sumA / totalKernelWeight, 0, 255);

                if (newA == 0 || colorWeightSum == 0)
                {
                    canvas.SetPixel(x, y, Canvas.TransparentColor);
                }
                else
                {
                    byte newR = (byte)Math.Clamp(sumR / colorWeightSum, 0, 255);
                    byte newG = (byte)Math.Clamp(sumG / colorWeightSum, 0, 255);
                    byte newB = (byte)Math.Clamp(sumB / colorWeightSum, 0, 255);
                    canvas.SetPixel(x, y, new Color(newR, newG, newB, newA));
                }
            }
        }
    }

    // ASEPRITE JUMBLE TOOL: Firca alanindaki pikselleri rastgele komsulariyla karistirir
    public void ApplyJumble(Canvas canvas, int centerX, int centerY, int size)
    {
        int effSize = Math.Max(2, size);
        int offset = effSize / 2;
        int minX = canvas.TileWrapMode ? (centerX - offset) : Math.Max(0, centerX - offset);
        int maxX = canvas.TileWrapMode ? (centerX - offset + effSize - 1) : Math.Min(canvas.Width - 1, centerX - offset + effSize - 1);
        int minY = canvas.TileWrapMode ? (centerY - offset) : Math.Max(0, centerY - offset);
        int maxY = canvas.TileWrapMode ? (centerY - offset + effSize - 1) : Math.Min(canvas.Height - 1, centerY - offset + effSize - 1);

        List<(int x, int y)> coords = new();
        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                coords.Add((x, y));
            }
        }

        if (coords.Count <= 1) return;

        for (int i = coords.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (coords[i], coords[j]) = (coords[j], coords[i]);
        }

        int swapAttempts = Math.Max(1, (coords.Count * AppSettings.JumbleIntensity) / 200);
        for (int i = 0; i < swapAttempts; i++)
        {
            var (x1, y1) = coords[i % coords.Count];
            int nx = Math.Clamp(x1 + rng.Next(-1, 2), minX, maxX);
            int ny = Math.Clamp(y1 + rng.Next(-1, 2), minY, maxY);

            if (nx != x1 || ny != y1)
            {
                Color c1 = canvas.GetPixel(x1, y1);
                Color c2 = canvas.GetPixel(nx, ny);
                if (!ColorsEqual(c1, c2))
                {
                    canvas.SetPixel(x1, y1, c2);
                    canvas.SetPixel(nx, ny, c1);
                }
            }
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

    public void DrawLine(
        Canvas canvas,
        int x0,
        int y0,
        int x1,
        int y1,
        Color color,
        bool forceEraser = false,
        bool darkenShading = false,
        IReadOnlyList<Color>? paletteColors = null)
    {
        if (!forceEraser && (ActiveTool == ToolType.Blur || ActiveTool == ToolType.Jumble || ActiveTool == ToolType.Dither || ActiveTool == ToolType.Shading))
        {
            foreach (var (x, y) in GetLinePixels(x0, y0, x1, y1))
            {
                ApplyTool(canvas, x, y, color, false, darkenShading, paletteColors);
            }
            return;
        }

        bool isErasing = forceEraser || ActiveTool == ToolType.Eraser;
        int size = isErasing ? EraserSize : PenSize;
        Color drawColor = isErasing ? Canvas.TransparentColor : color;
        bool usePixelPerfect = PixelPerfect && size == 1;

        foreach (var (x, y) in GetLinePixels(x0, y0, x1, y1))
        {
            if (usePixelPerfect) PixelPerfectPlot(canvas, x, y, drawColor);
            else Stamp(canvas, x, y, size, drawColor);
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

    /// <summary>
    /// 3 noktali Kuadratik Bezier egrisinin piksellerini hesaplar.
    /// (x0, y0): Baslangic, (x1, y1): Kavis Tepe Noktasi (Imlec), (x2, y2): Bitis.
    /// </summary>
    public static List<(int x, int y)> GetBezierCurvePixels(int x0, int y0, int x1, int y1, int x2, int y2, bool cleanCorners = true)
    {
        // Egrinin t = 0.5 noktasinda tam olarak (x1, y1) imlec noktasindan gecmesi icin kontrol noktasi:
        double cx = 2.0 * x1 - 0.5 * (x0 + x2);
        double cy = 2.0 * y1 - 0.5 * (y0 + y2);

        double len1 = Math.Sqrt((cx - x0) * (cx - x0) + (cy - y0) * (cy - y0));
        double len2 = Math.Sqrt((x2 - cx) * (x2 - cx) + (y2 - cy) * (y2 - cy));
        int steps = Math.Clamp((int)Math.Ceiling((len1 + len2) * 3.0), 16, 4096);

        List<(int x, int y)> raw = new();
        int prevX = x0, prevY = y0;
        raw.Add((prevX, prevY));

        for (int i = 1; i <= steps; i++)
        {
            double t = (double)i / steps;
            double u = 1.0 - t;
            double bx = u * u * x0 + 2.0 * u * t * cx + t * t * x2;
            double by = u * u * y0 + 2.0 * u * t * cy + t * t * y2;

            int px = (int)Math.Round(bx);
            int py = (int)Math.Round(by);

            if (px != prevX || py != prevY)
            {
                if (Math.Abs(px - prevX) > 1 || Math.Abs(py - prevY) > 1)
                {
                    foreach (var segPt in GetLinePixels(prevX, prevY, px, py))
                    {
                        if (raw[^1] != segPt) raw.Add(segPt);
                    }
                }
                else
                {
                    raw.Add((px, py));
                }
                prevX = px;
                prevY = py;
            }
        }

        if (!cleanCorners || raw.Count < 3) return raw;

        // 1px piksel sanati egrilerinde olusan dik acili "L" basamak fazlaliklarini temizle
        List<(int x, int y)> cleaned = new(raw.Count) { raw[0] };
        for (int i = 1; i < raw.Count - 1; i++)
        {
            var a = cleaned[^1];
            var b = raw[i];
            var c = raw[i + 1];

            bool bAdjA = Math.Abs(a.x - b.x) + Math.Abs(a.y - b.y) == 1;
            bool bAdjC = Math.Abs(c.x - b.x) + Math.Abs(c.y - b.y) == 1;
            bool diagAC = Math.Abs(a.x - c.x) == 1 && Math.Abs(a.y - c.y) == 1;

            if (bAdjA && bAdjC && diagAC)
                continue;

            cleaned.Add(b);
        }
        cleaned.Add(raw[^1]);
        return cleaned;
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
                for (int x = minX; x <= maxX; x++) { pixels.Add((x, minY)); if (maxY != minY) pixels.Add((x, maxY)); }
                for (int y = minY + 1; y < maxY; y++) { pixels.Add((minX, y)); if (maxX != minX) pixels.Add((maxX, y)); }
                break;

            case ShapeType.RectangleFilled:
                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        pixels.Add((x, y));
                break;

            case ShapeType.CircleOutline:
            case ShapeType.CircleFilled:
            {
                double cx = (minX + maxX) * 0.5;
                double cy = (minY + maxY) * 0.5;
                double rx = (maxX - minX) * 0.5 + 0.5;
                double ry = (maxY - minY) * 0.5 + 0.5;

                bool InsideEllipse(int px, int py)
                {
                    double nx = (px - cx) / rx;
                    double ny = (py - cy) / ry;
                    return (nx * nx + ny * ny) <= 1.0;
                }

                for (int y = minY; y <= maxY; y++)
                {
                    for (int x = minX; x <= maxX; x++)
                    {
                        if (!InsideEllipse(x, y)) continue;
                        if (shape == ShapeType.CircleFilled)
                        {
                            pixels.Add((x, y));
                        }
                        else
                        {
                            if (!InsideEllipse(x - 1, y) || !InsideEllipse(x + 1, y) ||
                                !InsideEllipse(x, y - 1) || !InsideEllipse(x, y + 1))
                            {
                                pixels.Add((x, y));
                            }
                        }
                    }
                }
                break;
            }

            case ShapeType.TriangleOutline:
                int midTopX = (minX + maxX) / 2;
                pixels.AddRange(GetLinePixels(midTopX, minY, minX, maxY));
                pixels.AddRange(GetLinePixels(minX, maxY, maxX, maxY));
                pixels.AddRange(GetLinePixels(maxX, maxY, midTopX, minY));
                break;

            case ShapeType.TriangleFilled:
                double topX = (minX + maxX) * 0.5;
                for (int y = minY; y <= maxY; y++)
                {
                    double factor = (maxY == minY) ? 1.0 : (double)(y - minY) / (maxY - minY);
                    int curMinX = (int)Math.Round(topX - (topX - minX) * factor);
                    int curMaxX = (int)Math.Round(topX + (maxX - topX) * factor);
                    for (int x = curMinX; x <= curMaxX; x++) pixels.Add((x, y));
                }
                break;
        }
        return pixels;
    }

    private void FloodFill(Canvas canvas, int startX, int startY, Color newColor)
    {
        if (canvas.TileWrapMode)
            (startX, startY) = canvas.WrapCoord(startX, startY);
        else if (startX < 0 || startX >= canvas.Width || startY < 0 || startY >= canvas.Height)
            return;

        Color targetColor = canvas.GetPixel(startX, startY);
        if (ColorsEqual(targetColor, newColor)) return;
        if (canvas.ActiveLayerAlphaLock && targetColor.A == 0) return;

        bool[,] visited = new bool[canvas.Width, canvas.Height];
        Queue<(int x, int y)> queue = new();
        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;

        ReadOnlySpan<(int dx, int dy)> dirs = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (!ColorsEqual(canvas.GetPixel(x, y), targetColor)) continue;

            canvas.SetPixel(x, y, newColor);

            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (canvas.TileWrapMode)
                {
                    (nx, ny) = canvas.WrapCoord(nx, ny);
                }
                else if (nx < 0 || nx >= canvas.Width || ny < 0 || ny >= canvas.Height)
                {
                    continue;
                }

                if (!visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    queue.Enqueue((nx, ny));
                }
            }
        }
    }

    public bool ColorsEqual(Color c1, Color c2)
    {
        return c1.R == c2.R && c1.G == c2.G && c1.B == c2.B && c1.A == c2.A;
    }
}
