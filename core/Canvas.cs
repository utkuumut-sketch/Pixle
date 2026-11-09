// === DOSYA: \Core\Canvas.cs ===
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.Core;

public class Canvas : IDisposable
{
    public int Width { get; }
    public int Height { get; }
    public int PixelScale { get; }
    public Vector2 Offset { get; set; }
    public bool ShowGrid { get; set; } = true;
    public bool TileWrapMode { get; set; } = false;
    public static readonly Color TransparentColor = new Color(0, 0, 0, 0);

    public List<Frame> Frames { get; private set; } = new();
    private int activeFrameIndex = 0;

    public int ActiveFrameIndex
    {
        get => Frames.Count == 0 ? 0 : Math.Clamp(activeFrameIndex, 0, Frames.Count - 1);
        set => activeFrameIndex = Frames.Count == 0 ? 0 : Math.Clamp(value, 0, Frames.Count - 1);
    }

    public List<Layer> Layers => Frames[ActiveFrameIndex].Layers;

    public int ActiveLayerIndex
    {
        get
        {
            var layers = Layers;
            return layers.Count == 0 ? 0 : Math.Clamp(Frames[ActiveFrameIndex].ActiveLayerIndex, 0, layers.Count - 1);
        }
        set
        {
            var layers = Layers;
            Frames[ActiveFrameIndex].ActiveLayerIndex = layers.Count == 0 ? 0 : Math.Clamp(value, 0, layers.Count - 1);
        }
    }

    public bool ActiveLayerAlphaLock
    {
        get => Layers.Count > 0 && Layers[ActiveLayerIndex].AlphaLock;
        set
        {
            if (Layers.Count > 0)
                Layers[ActiveLayerIndex].AlphaLock = value;
        }
    }

    private Texture2D checkerTexture;
    private bool hasChecker = false;
    private int lastCheckerContrast = -1;

    public Canvas(int width, int height, int scale, Vector2 offset)
    {
        Width = width;
        Height = height;
        PixelScale = scale;
        Offset = offset;

        Frames.Add(new Frame());
        Frames[0].Layers.Add(new Layer("Katman 1", width, height));
    }

    public int WrapX(int x) => ((x % Width) + Width) % Width;
    public int WrapY(int y) => ((y % Height) + Height) % Height;
    public (int x, int y) WrapCoord(int x, int y) => (WrapX(x), WrapY(y));

    public static Color BlendOver(Color bottom, Color top)
    {
        if (top.A == 0) return bottom;
        if (top.A == 255 || bottom.A == 0) return top;

        float srcA = top.A / 255f;
        float dstA = bottom.A / 255f;
        float outA = srcA + dstA * (1f - srcA);

        if (outA <= 0.0001f) return TransparentColor;

        byte r = (byte)Math.Clamp((top.R * srcA + bottom.R * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte g = (byte)Math.Clamp((top.G * srcA + bottom.G * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte b = (byte)Math.Clamp((top.B * srcA + bottom.B * dstA * (1f - srcA)) / outA, 0f, 255f);
        byte a = (byte)Math.Clamp(outA * 255f, 0f, 255f);

        return new Color(r, g, b, a);
    }

    private void InitCheckerTexture()
    {
        if (hasChecker && lastCheckerContrast == AppSettings.CheckerContrast) return;

        if (hasChecker)
        {
            Raylib.UnloadTexture(checkerTexture);
            hasChecker = false;
        }

        Image img = Raylib.GenImageColor(Width, Height, Color.White);
        Color c1 = AppSettings.CheckerContrast switch
        {
            0 => new Color(185, 185, 185, 255),
            2 => new Color(225, 225, 225, 255),
            _ => new Color(200, 200, 200, 255)
        };
        Color c2 = AppSettings.CheckerContrast switch
        {
            0 => new Color(170, 170, 170, 255),
            2 => new Color(135, 135, 135, 255),
            _ => new Color(160, 160, 160, 255)
        };

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                Raylib.ImageDrawPixel(ref img, x, y, ((x + y) % 2 == 0) ? c1 : c2);
            }
        }
        checkerTexture = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureFilter(checkerTexture, TextureFilter.Point);
        Raylib.UnloadImage(img);
        hasChecker = true;
        lastCheckerContrast = AppSettings.CheckerContrast;
    }

    private string GenerateUniqueLayerName(string basePrefix = "Katman")
    {
        int nextNum = Layers.Count + 1;
        while (Layers.Exists(l => string.Equals(l.Name, $"{basePrefix} {nextNum}", StringComparison.OrdinalIgnoreCase)))
        {
            nextNum++;
        }
        return $"{basePrefix} {nextNum}";
    }

    public void AddLayer()
    {
        Layers.Add(new Layer(GenerateUniqueLayerName(), Width, Height));
        ActiveLayerIndex = Layers.Count - 1;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public void DuplicateActiveLayer()
    {
        if (Layers.Count == 0) return;
        Layer current = Layers[ActiveLayerIndex];
        Layer copy = current.Clone();
        copy.Name = $"{current.Name} Kopya";
        int insertIdx = ActiveLayerIndex + 1;
        Layers.Insert(insertIdx, copy);
        ActiveLayerIndex = insertIdx;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public bool MergeLayersAt(int indexA, int indexB, out string message)
    {
        message = "";
        if (Layers.Count < 2 || indexA == indexB ||
            indexA < 0 || indexA >= Layers.Count ||
            indexB < 0 || indexB >= Layers.Count)
        {
            message = "Birlestirmek icin en az 2 gecerli katman gerekli.";
            return false;
        }

        int lowIdx = Math.Min(indexA, indexB);
        int highIdx = Math.Max(indexA, indexB);
        Layer bottomLayer = Layers[lowIdx];
        Layer topLayer = Layers[highIdx];

        List<Layer> combinedSources = new();
        if (bottomLayer.HasMergedSources && bottomLayer.MergedSources != null)
        {
            foreach (var s in bottomLayer.MergedSources) combinedSources.Add(s.Clone());
        }
        else combinedSources.Add(bottomLayer.Clone());

        if (topLayer.HasMergedSources && topLayer.MergedSources != null)
        {
            foreach (var s in topLayer.MergedSources) combinedSources.Add(s.Clone());
        }
        else combinedSources.Add(topLayer.Clone());

        Layer merged = new Layer(bottomLayer.Name, Width, Height)
        {
            IsVisible = true,
            Opacity = 255,
            AlphaLock = bottomLayer.AlphaLock && topLayer.AlphaLock
        };

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                Color cBot = bottomLayer.GetPixel(x, y);
                if (cBot.A > 0 && bottomLayer.Opacity < 255)
                    cBot.A = (byte)((cBot.A * bottomLayer.Opacity) / 255);

                Color cTop = topLayer.GetPixel(x, y);
                if (cTop.A > 0 && topLayer.Opacity < 255)
                    cTop.A = (byte)((cTop.A * topLayer.Opacity) / 255);

                Color final = BlendOver(cBot, cTop);
                if (final.A > 0)
                    merged.SetPixel(x, y, final, ignoreAlphaLock: true);
            }
        }

        merged.SetMergedSources(combinedSources);
        foreach (var s in combinedSources) s.Dispose();

        string topName = topLayer.Name;
        string botName = bottomLayer.Name;

        topLayer.Dispose();
        bottomLayer.Dispose();
        Layers.RemoveAt(highIdx);
        Layers[lowIdx] = merged;
        ActiveLayerIndex = lowIdx;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;

        message = $"'{topName}' ve '{botName}' birlestirildi (Ayir ile geri acilabilir).";
        return true;
    }

    public bool MergeActiveLayer(out string message)
    {
        if (Layers.Count < 2)
        {
            message = "Birlestirmek icin en az 2 katman olmali.";
            return false;
        }

        int targetIdx = ActiveLayerIndex > 0 ? ActiveLayerIndex - 1 : ActiveLayerIndex + 1;
        return MergeLayersAt(ActiveLayerIndex, targetIdx, out message);
    }

    public bool MergeAllVisibleLayers(out string message)
    {
        List<int> visIndices = new();
        for (int i = 0; i < Layers.Count; i++)
        {
            if (Layers[i].IsVisible) visIndices.Add(i);
        }

        if (visIndices.Count < 2)
        {
            message = "Birlestirmek icin en az 2 gorunur katman olmali.";
            return false;
        }

        List<Layer> sources = new();
        Layer merged = new Layer(Layers[visIndices[0]].Name, Width, Height);

        foreach (int idx in visIndices)
        {
            Layer l = Layers[idx];
            if (l.HasMergedSources && l.MergedSources != null)
            {
                foreach (var s in l.MergedSources) sources.Add(s.Clone());
            }
            else sources.Add(l.Clone());

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Color c = l.GetPixel(x, y);
                    if (c.A > 0)
                    {
                        if (l.Opacity < 255) c.A = (byte)((c.A * l.Opacity) / 255);
                        if (c.A > 0)
                        {
                            Color cur = merged.GetPixel(x, y);
                            merged.SetPixel(x, y, BlendOver(cur, c), ignoreAlphaLock: true);
                        }
                    }
                }
            }
        }

        merged.SetMergedSources(sources);
        foreach (var s in sources) s.Dispose();

        int insertPos = visIndices[0];
        for (int i = visIndices.Count - 1; i >= 0; i--)
        {
            int removeIdx = visIndices[i];
            Layers[removeIdx].Dispose();
            Layers.RemoveAt(removeIdx);
        }

        insertPos = Math.Clamp(insertPos, 0, Layers.Count);
        Layers.Insert(insertPos, merged);
        ActiveLayerIndex = insertPos;
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;

        message = $"{visIndices.Count} gorunur katman tek katmanda birlestirildi.";
        return true;
    }

    public bool SplitActiveLayer(SelectionManager? selection, out string message)
    {
        message = "";
        if (Layers.Count == 0) return false;

        int actIdx = ActiveLayerIndex;
        Layer active = Layers[actIdx];

        // 1. Durum: Aktif bir seçim varsa, seçili pikselleri yeni katmana ayır
        if (selection != null && selection.HasSelection)
        {
            if (selection.ExtractSelectionToNewLayer(this, out Layer? selLayer) && selLayer != null)
            {
                selLayer.Name = $"{active.Name} Parca";
                int newIdx = actIdx + 1;
                Layers.Insert(newIdx, selLayer);
                ActiveLayerIndex = newIdx;
                Frames[ActiveFrameIndex].IsThumbnailDirty = true;
                message = "Secili alan yeni katmana ayrildi.";
                return true;
            }
        }

        // 2. Durum: Bu katman daha önce birleştirilmiş (Merge edilmiş) bir katmansa, eski katmanlarına geri ayır!
        if (active.HasMergedSources && active.MergedSources != null)
        {
            var sources = active.MergedSources;
            List<Layer> restored = new(sources.Count);
            foreach (var src in sources)
            {
                restored.Add(src.Clone());
            }

            // Birleştirildikten sonra üzerine çizilmiş yeni pikseller varsa en üstteki ayrılan katmana aktar
            Layer topRestored = restored[^1];
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    Color cur = active.GetPixel(x, y);
                    Color origComp = TransparentColor;
                    foreach (var rLayer in restored)
                    {
                        Color rc = rLayer.GetPixel(x, y);
                        if (rc.A > 0)
                        {
                            if (rLayer.Opacity < 255) rc.A = (byte)((rc.A * rLayer.Opacity) / 255);
                            origComp = BlendOver(origComp, rc);
                        }
                    }

                    if (cur.R != origComp.R || cur.G != origComp.G || cur.B != origComp.B || cur.A != origComp.A)
                    {
                        if (cur.A == 0)
                        {
                            foreach (var rLayer in restored)
                                rLayer.SetPixel(x, y, TransparentColor, ignoreAlphaLock: true);
                        }
                        else
                        {
                            topRestored.SetPixel(x, y, cur, ignoreAlphaLock: true);
                        }
                    }
                }
            }

            active.Dispose();
            Layers.RemoveAt(actIdx);
            for (int i = 0; i < restored.Count; i++)
            {
                Layers.Insert(actIdx + i, restored[i]);
            }
            ActiveLayerIndex = actIdx + restored.Count - 1;
            Frames[ActiveFrameIndex].IsThumbnailDirty = true;
            message = $"Birlestirilmis katman {restored.Count} ayri katmana geri ayrildi.";
            return true;
        }

        // 3. Durum: Katmandaki bağımsız çizim adalarını (Connected Components) veya farklı renk gruplarını ayır
        int[,] compGrid = new int[Width, Height];
        List<List<(int x, int y)>> islands = new();
        ReadOnlySpan<(int dx, int dy)> dirs8 = stackalloc (int, int)[]
        {
            (-1, -1), (0, -1), (1, -1),
            (-1,  0),          (1,  0),
            (-1,  1), (0,  1), (1,  1)
        };

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (compGrid[x, y] != 0 || active.GetPixel(x, y).A == 0) continue;

                int compId = islands.Count + 1;
                var pts = new List<(int x, int y)>();
                Queue<(int x, int y)> q = new();
                q.Enqueue((x, y));
                compGrid[x, y] = compId;

                while (q.Count > 0)
                {
                    var (cx, cy) = q.Dequeue();
                    pts.Add((cx, cy));

                    foreach (var (dx, dy) in dirs8)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx >= 0 && nx < Width && ny >= 0 && ny < Height &&
                            compGrid[nx, ny] == 0 && active.GetPixel(nx, ny).A > 0)
                        {
                            compGrid[nx, ny] = compId;
                            q.Enqueue((nx, ny));
                        }
                    }
                }
                islands.Add(pts);
            }
        }

        if (islands.Count >= 2)
        {
            // En büyük parçayı mevcut katmanda bırak, diğer bağımsız parçaları yeni katmana ayır
            int largestIdx = 0;
            for (int i = 1; i < islands.Count; i++)
            {
                if (islands[i].Count > islands[largestIdx].Count)
                    largestIdx = i;
            }

            Layer backupBeforeSplit = active.Clone();
            Layer splitLayer = new Layer($"{active.Name} Parca", Width, Height);

            for (int i = 0; i < islands.Count; i++)
            {
                if (i == largestIdx) continue;
                foreach (var (px, py) in islands[i])
                {
                    splitLayer.SetPixel(px, py, active.GetPixel(px, py), ignoreAlphaLock: true);
                    active.SetPixel(px, py, TransparentColor, ignoreAlphaLock: true);
                }
            }

            backupBeforeSplit.Dispose();
            int insertIdx = actIdx + 1;
            Layers.Insert(insertIdx, splitLayer);
            ActiveLayerIndex = insertIdx;
            Frames[ActiveFrameIndex].IsThumbnailDirty = true;
            message = $"Katmandaki bagimsiz cizim parcalari yeni katmana ayrildi.";
            return true;
        }

        // Tek bir bağlı parça varsa ama içinde birden fazla renk varsa en baskın olmayan rengi yeni katmana ayır
        if (islands.Count == 1)
        {
            Dictionary<(byte R, byte G, byte B, byte A), List<(int x, int y)>> colorGroups = new();
            foreach (var (px, py) in islands[0])
            {
                Color c = active.GetPixel(px, py);
                var key = (c.R, c.G, c.B, c.A);
                if (!colorGroups.TryGetValue(key, out var list))
                {
                    list = new List<(int x, int y)>();
                    colorGroups[key] = list;
                }
                list.Add((px, py));
            }

            if (colorGroups.Count >= 2)
            {
                (byte R, byte G, byte B, byte A) mainColorKey = default;
                int maxCount = -1;
                foreach (var kv in colorGroups)
                {
                    if (kv.Value.Count > maxCount)
                    {
                        maxCount = kv.Value.Count;
                        mainColorKey = kv.Key;
                    }
                }

                Layer splitColorLayer = new Layer($"{active.Name} Renk", Width, Height);
                foreach (var kv in colorGroups)
                {
                    if (kv.Key == mainColorKey) continue;
                    foreach (var (px, py) in kv.Value)
                    {
                        splitColorLayer.SetPixel(px, py, active.GetPixel(px, py), ignoreAlphaLock: true);
                        active.SetPixel(px, py, TransparentColor, ignoreAlphaLock: true);
                    }
                }

                int insertIdx = actIdx + 1;
                Layers.Insert(insertIdx, splitColorLayer);
                ActiveLayerIndex = insertIdx;
                Frames[ActiveFrameIndex].IsThumbnailDirty = true;
                message = "Katmandaki farkli renk tonlari yeni katmana ayrildi.";
                return true;
            }
        }

        message = "Bu katmanda ayrilacak birlestirme gecmisi, secim veya bagimsiz parca yok.";
        return false;
    }

    public void SetLayerVisibility(int layerIndex, bool isVisible)
    {
        if (layerIndex < 0 || layerIndex >= Layers.Count) return;
        if (Layers[layerIndex].IsVisible != isVisible)
        {
            Layers[layerIndex].IsVisible = isVisible;
            Frames[ActiveFrameIndex].IsThumbnailDirty = true;
        }
    }

    public void AddFrame()
    {
        var newFrame = new Frame();
        foreach (var l in Layers)
        {
            newFrame.Layers.Add(l.Clone());
        }
        newFrame.ActiveLayerIndex = ActiveLayerIndex;
        Frames.Add(newFrame);
        ActiveFrameIndex = Frames.Count - 1;
    }

    public void ReplaceFrames(List<Frame> newFrames, int activeFrameIdx)
    {
        if (newFrames == null || newFrames.Count == 0) return;

        foreach (var f in Frames) f.Dispose();
        Frames.Clear();

        Frames.AddRange(newFrames);
        ActiveFrameIndex = activeFrameIdx;
    }

    public Color GetPixel(int x, int y)
    {
        if (Layers.Count == 0) return TransparentColor;
        if (TileWrapMode) (x, y) = WrapCoord(x, y);
        else if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        return Layers[ActiveLayerIndex].GetPixel(x, y);
    }

    public void SetPixel(int x, int y, Color color, bool ignoreAlphaLock = false)
    {
        if (Layers.Count == 0) return;
        if (TileWrapMode) (x, y) = WrapCoord(x, y);
        else if (x < 0 || x >= Width || y < 0 || y >= Height) return;
        Layers[ActiveLayerIndex].SetPixel(x, y, color, ignoreAlphaLock);
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public Color GetMergedPixel(int x, int y)
    {
        if (TileWrapMode) (x, y) = WrapCoord(x, y);
        else if (x < 0 || x >= Width || y < 0 || y >= Height) return TransparentColor;
        Color final = TransparentColor;

        for (int i = 0; i < Layers.Count; i++)
        {
            if (!Layers[i].IsVisible || Layers[i].Opacity == 0) continue;
            Color c = Layers[i].GetPixel(x, y);
            if (c.A > 0)
            {
                if (Layers[i].Opacity < 255)
                {
                    c.A = (byte)((c.A * Layers[i].Opacity) / 255);
                    if (c.A == 0) continue;
                }
                final = BlendOver(final, c);
            }
        }
        return final;
    }

    public void Clear(Color color)
    {
        if (Layers.Count == 0) return;
        Layers[ActiveLayerIndex].Fill(color);
        Frames[ActiveFrameIndex].IsThumbnailDirty = true;
    }

    public bool IsMouseInside(Vector2 worldPos)
    {
        int totalW = Width * PixelScale;
        int totalH = Height * PixelScale;
        if (TileWrapMode)
        {
            return worldPos.X >= Offset.X - totalW && worldPos.X < Offset.X + 2 * totalW &&
                   worldPos.Y >= Offset.Y - totalH && worldPos.Y < Offset.Y + 2 * totalH;
        }
        return worldPos.X >= Offset.X && worldPos.X < Offset.X + totalW &&
               worldPos.Y >= Offset.Y && worldPos.Y < Offset.Y + totalH;
    }

    public (int x, int y) ScreenToGrid(Vector2 worldPos)
    {
        int gx = (int)Math.Floor((worldPos.X - Offset.X) / PixelScale);
        int gy = (int)Math.Floor((worldPos.Y - Offset.Y) / PixelScale);
        return (gx, gy);
    }

    public void Draw()
    {
        InitCheckerTexture();

        int totalW = Width * PixelScale;
        int totalH = Height * PixelScale;
        Rectangle srcRect = new Rectangle(0, 0, Width, Height);
        Rectangle destRect = new Rectangle(Offset.X, Offset.Y, totalW, totalH);

        Texture2D? mergedFrameTex = Frames.Count > 0 ? Frames[ActiveFrameIndex].GetOrUpdateThumbnail(Width, Height) : null;

        // TILE / DOSEME (WRAP) MODU: 8 komşu karoyu merkez tuvalin etrafında canlı göster
        if (TileWrapMode && mergedFrameTex.HasValue)
        {
            Color neighborCheckerTint = new Color((byte)145, (byte)150, (byte)162, (byte)175);
            Color neighborSpriteTint = new Color((byte)230, (byte)235, (byte)245, (byte)185);
            Color neighborBorderCol = new Color((byte)70, (byte)135, (byte)175, (byte)120);

            for (int ty = -1; ty <= 1; ty++)
            {
                for (int tx = -1; tx <= 1; tx++)
                {
                    if (tx == 0 && ty == 0) continue;
                    Rectangle nRect = new Rectangle(Offset.X + tx * totalW, Offset.Y + ty * totalH, totalW, totalH);
                    Raylib.DrawTexturePro(checkerTexture, srcRect, nRect, Vector2.Zero, 0f, neighborCheckerTint);
                    Raylib.DrawTexturePro(mergedFrameTex.Value, srcRect, nRect, Vector2.Zero, 0f, neighborSpriteTint);
                    Raylib.DrawRectangleLinesEx(nRect, 1f, neighborBorderCol);
                }
            }
        }

        Raylib.DrawRectangle((int)Offset.X + 6, (int)Offset.Y + 6, totalW, totalH, new Color(0, 0, 0, 110));
        Raylib.DrawTexturePro(checkerTexture, srcRect, destRect, Vector2.Zero, 0f, Color.White);

        if (AppSettings.EnableOnionSkin && Frames.Count > 1)
        {
            byte prevAlpha = (byte)Math.Clamp(AppSettings.OnionSkinOpacity, 15, 230);
            byte nextAlpha = (byte)Math.Clamp((int)(AppSettings.OnionSkinOpacity * 0.6f), 10, 200);

            if (ActiveFrameIndex > 0 && Frames[ActiveFrameIndex - 1].IsVisible)
            {
                Texture2D prevTex = Frames[ActiveFrameIndex - 1].GetOrUpdateThumbnail(Width, Height);
                Raylib.DrawTexturePro(prevTex, srcRect, destRect, Vector2.Zero, 0f, new Color((byte)255, (byte)255, (byte)255, prevAlpha));
            }

            if (ActiveFrameIndex < Frames.Count - 1 && Frames[ActiveFrameIndex + 1].IsVisible)
            {
                Texture2D nextTex = Frames[ActiveFrameIndex + 1].GetOrUpdateThumbnail(Width, Height);
                Raylib.DrawTexturePro(nextTex, srcRect, destRect, Vector2.Zero, 0f, new Color((byte)255, (byte)255, (byte)255, nextAlpha));
            }
        }

        if (mergedFrameTex.HasValue)
        {
            Raylib.DrawTexturePro(mergedFrameTex.Value, srcRect, destRect, Vector2.Zero, 0f, Color.White);
        }

        int tileStep = AppSettings.TileGridSize;

        if (ShowGrid)
        {
            Color gridCol = new Color((byte)0, (byte)0, (byte)0, (byte)Math.Clamp(AppSettings.GridOpacity, 10, 200));
            for (int x = 0; x <= Width; x++)
            {
                // Karo ızgarası çizgileriyle 1x1 piksel ızgarasının üst üste binip hare (moiré) yapmasını önle
                if (tileStep > 0 && x > 0 && x < Width && x % tileStep == 0) continue;
                Raylib.DrawLine((int)Offset.X + x * PixelScale, (int)Offset.Y, (int)Offset.X + x * PixelScale, (int)Offset.Y + totalH, gridCol);
            }
            for (int y = 0; y <= Height; y++)
            {
                if (tileStep > 0 && y > 0 && y < Height && y % tileStep == 0) continue;
                Raylib.DrawLine((int)Offset.X, (int)Offset.Y + y * PixelScale, (int)Offset.X + totalW, (int)Offset.Y + y * PixelScale, gridCol);
            }
        }

        // KARO IZGARASI (Tile Grid: 8x8, 16x16, 32x32)
        if (tileStep > 0)
        {
            Color tileLineShadow = new Color((byte)0, (byte)0, (byte)0, (byte)140);
            Color tileLineMain = new Color((byte)64, (byte)215, (byte)255, (byte)195);

            for (int x = tileStep; x < Width; x += tileStep)
            {
                int lx = (int)Offset.X + x * PixelScale;
                Raylib.DrawLine(lx + 1, (int)Offset.Y, lx + 1, (int)Offset.Y + totalH, tileLineShadow);
                Raylib.DrawLine(lx, (int)Offset.Y, lx, (int)Offset.Y + totalH, tileLineMain);
            }
            for (int y = tileStep; y < Height; y += tileStep)
            {
                int ly = (int)Offset.Y + y * PixelScale;
                Raylib.DrawLine((int)Offset.X, ly + 1, (int)Offset.X + totalW, ly + 1, tileLineShadow);
                Raylib.DrawLine((int)Offset.X, ly, (int)Offset.X + totalW, ly, tileLineMain);
            }
        }

        Color mainBorder = TileWrapMode ? new Color((byte)95, (byte)215, (byte)255, (byte)230) : new Color((byte)90, (byte)90, (byte)100, (byte)255);
        Raylib.DrawRectangleLinesEx(new Rectangle(Offset.X - 1, Offset.Y - 1, totalW + 2, totalH + 2), TileWrapMode ? 1.5f : 1f, mainBorder);
    }

    public void Dispose()
    {
        if (hasChecker)
        {
            Raylib.UnloadTexture(checkerTexture);
            hasChecker = false;
        }
        foreach (var f in Frames) f.Dispose();
    }
}
