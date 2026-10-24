using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.Core;

public enum SelectToolMode
{
    Rectangle,
    Lasso,
    MagicWand
}

public class SelectionManager
{
    public bool HasSelection { get; private set; } = false;
    public bool IsFloating { get; private set; } = false;
    public bool IsCreatingSelection { get; private set; } = false;
    public bool IsDraggingFloating { get; private set; } = false;

    private bool[,] mask = new bool[1, 1];
    private Color[,] floatingPixels = new Color[1, 1];

    public Rectangle Bounds { get; private set; }
    public Vector2 Offset { get; private set; }

    private (int x, int y) createStartPos;
    private (int x, int y) createCurrentPos;
    public List<(int x, int y)> LassoPath { get; private set; } = new();

    private (int x, int y) dragStartGrid;
    private Vector2 initialOffset;

    private Color[,]? clipboardPixels;
    private bool[,]? clipboardMask;
    private int clipWidth, clipHeight;

    public void EnsureSize(int width, int height)
    {
        if (mask.GetLength(0) != width || mask.GetLength(1) != height)
        {
            mask = new bool[width, height];
            floatingPixels = new Color[width, height];
        }
    }

    public bool IsPointInCurrentSelection(int gx, int gy)
    {
        if (!HasSelection) return false;
        int srcX = gx - (int)Offset.X;
        int srcY = gy - (int)Offset.Y;
        if (srcX < 0 || srcX >= mask.GetLength(0) || srcY < 0 || srcY >= mask.GetLength(1)) return false;
        return mask[srcX, srcY];
    }

    public Rectangle GetCurrentBounds()
    {
        return new Rectangle(Bounds.X + Offset.X, Bounds.Y + Offset.Y, Bounds.Width, Bounds.Height);
    }

    public void StartSelection(Canvas canvas, int gx, int gy, SelectToolMode mode, bool isAdd = false, bool isSubtract = false)
    {
        EnsureSize(canvas.Width, canvas.Height);

        // Eğer yüzen pikseller taşınıyorsa önce onları tuvale yapıştır
        if (IsFloating)
        {
            CancelOrCommit(canvas);
        }

        if (mode == SelectToolMode.MagicWand)
        {
            DoMagicWand(canvas, gx, gy, isAdd, isSubtract);
        }
        else
        {
            if (!isAdd && !isSubtract)
            {
                CancelOrCommit(canvas);
            }

            IsCreatingSelection = true;
            createStartPos = (gx, gy);
            createCurrentPos = (gx, gy);
            LassoPath.Clear();
            LassoPath.Add((gx, gy));
        }
    }

    public void UpdateSelectionCreation(int gx, int gy, SelectToolMode mode)
    {
        if (!IsCreatingSelection) return;
        createCurrentPos = (gx, gy);

        if (mode == SelectToolMode.Lasso)
        {
            var last = LassoPath[^1];
            if (last.x != gx || last.y != gy)
            {
                var line = ToolManager.GetLinePixels(last.x, last.y, gx, gy);
                foreach (var pt in line)
                {
                    if (LassoPath[^1] != pt)
                        LassoPath.Add(pt);
                }
            }
        }
    }

    public void EndSelectionCreation(Canvas canvas, SelectToolMode mode, bool isAdd = false, bool isSubtract = false)
    {
        if (!IsCreatingSelection) return;
        IsCreatingSelection = false;

        int w = canvas.Width;
        int h = canvas.Height;
        bool[,] tempMask = new bool[w, h];

        if (mode == SelectToolMode.Rectangle)
        {
            int minX = Math.Clamp(Math.Min(createStartPos.x, createCurrentPos.x), 0, w - 1);
            int maxX = Math.Clamp(Math.Max(createStartPos.x, createCurrentPos.x), 0, w - 1);
            int minY = Math.Clamp(Math.Min(createStartPos.y, createCurrentPos.y), 0, h - 1);
            int maxY = Math.Clamp(Math.Max(createStartPos.y, createCurrentPos.y), 0, h - 1);

            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tempMask[x, y] = true;
                }
            }
        }
        else if (mode == SelectToolMode.Lasso)
        {
            if (LassoPath.Count < 3)
            {
                int cx = Math.Clamp(createStartPos.x, 0, w - 1);
                int cy = Math.Clamp(createStartPos.y, 0, h - 1);
                tempMask[cx, cy] = true;
            }
            else
            {
                var closingLine = ToolManager.GetLinePixels(LassoPath[^1].x, LassoPath[^1].y, LassoPath[0].x, LassoPath[0].y);
                foreach (var pt in closingLine) LassoPath.Add(pt);

                for (int x = 0; x < w; x++)
                {
                    for (int y = 0; y < h; y++)
                    {
                        if (IsPointInPolygon(x, y, LassoPath))
                        {
                            tempMask[x, y] = true;
                        }
                    }
                }
                foreach (var (px, py) in LassoPath)
                {
                    if (px >= 0 && px < w && py >= 0 && py < h)
                        tempMask[px, py] = true;
                }
            }
        }

        // Maske Birleştirme (Ekleme / Çıkarma / Yeni)
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (isAdd)
                {
                    if (tempMask[x, y]) mask[x, y] = true;
                }
                else if (isSubtract)
                {
                    if (tempMask[x, y]) mask[x, y] = false;
                }
                else
                {
                    mask[x, y] = tempMask[x, y];
                }
            }
        }

        Offset = Vector2.Zero;
        RecalculateBounds(w, h);
    }

    private void DoMagicWand(Canvas canvas, int startX, int startY, bool isAdd, bool isSubtract)
    {
        int w = canvas.Width;
        int h = canvas.Height;
        if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

        Color target = canvas.GetPixel(startX, startY);

        if (target.A == 0)
        {
            if (!isAdd && !isSubtract)
            {
                CancelOrCommit(canvas);
            }
            return;
        }

        bool[,] newSelection = new bool[w, h];
        Queue<(int x, int y)> q = new();
        q.Enqueue((startX, startY));
        bool[,] visited = new bool[w, h];
        visited[startX, startY] = true;

        ReadOnlySpan<(int dx, int dy)> dirs = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            newSelection[x, y] = true;

            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    Color c = canvas.GetPixel(nx, ny);
                    if (c.R == target.R && c.G == target.G && c.B == target.B && c.A == target.A)
                    {
                        q.Enqueue((nx, ny));
                    }
                }
            }
        }

        if (!isAdd && !isSubtract)
        {
            Array.Clear(mask, 0, mask.Length);
        }

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (newSelection[x, y])
                {
                    if (isSubtract)
                    {
                        mask[x, y] = false;
                    }
                    else
                    {
                        mask[x, y] = true;
                    }
                }
            }
        }

        Offset = Vector2.Zero;
        RecalculateBounds(w, h);
    }

    private bool IsPointInPolygon(int x, int y, List<(int x, int y)> poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            if (((poly[i].y > y) != (poly[j].y > y)) &&
                (x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (float)(poly[j].y - poly[i].y) + poly[i].x))
            {
                inside = !inside;
            }
        }
        return inside;
    }

   private void RecalculateBounds(int w, int h)
    {
        int minX = w, maxX = -1, minY = h, maxY = -1;
        bool any = false;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mask[x, y])
                {
                    any = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        HasSelection = any;
        if (any)
        {
            Bounds = new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
        else
        {
            Bounds = new Rectangle(0, 0, 0, 0);
        }
    }

    public void StartMove(Canvas canvas, HistoryManager history, int gx, int gy)
    {
        if (!HasSelection) return;

        if (!IsFloating)
        {
            history.SaveState(canvas);
            LiftSelection(canvas);
        }

        IsDraggingFloating = true;
        dragStartGrid = (gx, gy);
        initialOffset = Offset;
    }

    public void UpdateMove(int gx, int gy)
    {
        if (!IsDraggingFloating) return;
        int dx = gx - dragStartGrid.x;
        int dy = gy - dragStartGrid.y;
        Offset = new Vector2(initialOffset.X + dx, initialOffset.Y + dy);
    }

    public void EndMove()
    {
        IsDraggingFloating = false;
    }

    private void LiftSelection(Canvas canvas)
    {
        if (IsFloating) return;
        int w = canvas.Width;
        int h = canvas.Height;
        EnsureSize(w, h);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mask[x, y])
                {
                    floatingPixels[x, y] = canvas.GetPixel(x, y);
                    canvas.SetPixel(x, y, Canvas.TransparentColor);
                }
                else
                {
                    floatingPixels[x, y] = Canvas.TransparentColor;
                }
            }
        }
        IsFloating = true;
    }

    public void CancelOrCommit(Canvas canvas)
    {
        if (!HasSelection && !IsFloating) return;

        if (IsFloating)
        {
            int w = canvas.Width;
            int h = canvas.Height;
            int ox = (int)Offset.X;
            int oy = (int)Offset.Y;

            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mask[x, y])
                    {
                        Color c = floatingPixels[x, y];
                        if (c.A > 0)
                        {
                            int tx = x + ox;
                            int ty = y + oy;
                            if (tx >= 0 && tx < w && ty >= 0 && ty < h)
                            {
                                canvas.SetPixel(tx, ty, c);
                            }
                        }
                    }
                }
            }
        }

        IsFloating = false;
        HasSelection = false;
        IsDraggingFloating = false;
        IsCreatingSelection = false;
        Offset = Vector2.Zero;
        Array.Clear(mask, 0, mask.Length);
    }

    public void DeleteSelection(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;

        if (!IsFloating)
        {
            history.SaveState(canvas);
            int w = canvas.Width;
            int h = canvas.Height;
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mask[x, y])
                    {
                        canvas.SetPixel(x, y, Canvas.TransparentColor);
                    }
                }
            }
        }

        IsFloating = false;
        HasSelection = false;
        IsDraggingFloating = false;
        Offset = Vector2.Zero;
        Array.Clear(mask, 0, mask.Length);
    }

    public void Copy(Canvas canvas)
    {
        if (!HasSelection) return;

        int bx = (int)Bounds.X;
        int by = (int)Bounds.Y;
        clipWidth = (int)Bounds.Width;
        clipHeight = (int)Bounds.Height;

        clipboardPixels = new Color[clipWidth, clipHeight];
        clipboardMask = new bool[clipWidth, clipHeight];

        for (int x = 0; x < clipWidth; x++)
        {
            for (int y = 0; y < clipHeight; y++)
            {
                int sx = bx + x;
                int sy = by + y;
                if (sx >= 0 && sx < mask.GetLength(0) && sy >= 0 && sy < mask.GetLength(1) && mask[sx, sy])
                {
                    clipboardMask[x, y] = true;
                    clipboardPixels[x, y] = IsFloating ? floatingPixels[sx, sy] : canvas.GetPixel(sx, sy);
                }
            }
        }
    }

    public void Cut(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        Copy(canvas);
        DeleteSelection(canvas, history);
    }

    public void Paste(Canvas canvas, HistoryManager history)
    {
        if (clipboardPixels == null || clipboardMask == null) return;

        CancelOrCommit(canvas);
        history.SaveState(canvas);

        int w = canvas.Width;
        int h = canvas.Height;
        EnsureSize(w, h);
        Array.Clear(mask, 0, mask.Length);
        Array.Clear(floatingPixels, 0, floatingPixels.Length);

        for (int x = 0; x < clipWidth && x < w; x++)
        {
            for (int y = 0; y < clipHeight && y < h; y++)
            {
                if (clipboardMask[x, y])
                {
                    mask[x, y] = true;
                    floatingPixels[x, y] = clipboardPixels[x, y];
                }
            }
        }

        IsFloating = true;
        Offset = new Vector2(1, 1);
        RecalculateBounds(w, h);
    }

    public void Duplicate(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        Copy(canvas);
        Paste(canvas, history);
    }

    public void DrawWorldOverlay(Canvas canvas)
    {
        int scale = canvas.PixelScale;

        if (IsCreatingSelection)
        {
            int minX = Math.Min(createStartPos.x, createCurrentPos.x);
            int maxX = Math.Max(createStartPos.x, createCurrentPos.x);
            int minY = Math.Min(createStartPos.y, createCurrentPos.y);
            int maxY = Math.Max(createStartPos.y, createCurrentPos.y);

            if (LassoPath.Count > 1)
            {
                foreach (var (px, py) in LassoPath)
                {
                    Raylib.DrawRectangle((int)canvas.Offset.X + px * scale, (int)canvas.Offset.Y + py * scale, scale, scale, new Color(0, 180, 255, 110));
                }
            }
            else
            {
                Rectangle previewRect = new Rectangle(
                    canvas.Offset.X + minX * scale,
                    canvas.Offset.Y + minY * scale,
                    (maxX - minX + 1) * scale,
                    (maxY - minY + 1) * scale
                );
                Raylib.DrawRectangleRec(previewRect, new Color(0, 160, 255, 50));
                Raylib.DrawRectangleLinesEx(previewRect, 1.5f, Color.SkyBlue);
            }
            return;
        }

        if (!HasSelection) return;

        int w = canvas.Width;
        int h = canvas.Height;
        int ox = (int)Offset.X;
        int oy = (int)Offset.Y;

        if (IsFloating)
        {
            for (int x = 0; x < w; x++)
            {
                for (int y = 0; y < h; y++)
                {
                    if (mask[x, y])
                    {
                        Color c = floatingPixels[x, y];
                        if (c.A > 0)
                        {
                            int tx = x + ox;
                            int ty = y + oy;
                            Raylib.DrawRectangle(
                                (int)canvas.Offset.X + tx * scale,
                                (int)canvas.Offset.Y + ty * scale,
                                scale,
                                scale,
                                c
                            );
                        }
                    }
                }
            }
        }

        bool animPhase = ((int)(Raylib.GetTime() * 6) % 2) == 0;
        Color borderCol1 = animPhase ? Color.White : Color.Black;
        Color borderCol2 = animPhase ? Color.Black : Color.Yellow;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (!mask[x, y]) continue;

                int rx = (int)canvas.Offset.X + (x + ox) * scale;
                int ry = (int)canvas.Offset.Y + (y + oy) * scale;

                Raylib.DrawRectangle(rx, ry, scale, scale, new Color(0, 140, 255, 30));

                if (y == 0 || !mask[x, y - 1])
                    Raylib.DrawLine(rx, ry, rx + scale, ry, borderCol1);
                if (y == h - 1 || !mask[x, y + 1])
                    Raylib.DrawLine(rx, ry + scale, rx + scale, ry + scale, borderCol2);
                if (x == 0 || !mask[x - 1, y])
                    Raylib.DrawLine(rx, ry, rx, ry + scale, borderCol1);
                if (x == w - 1 || !mask[x + 1, y])
                    Raylib.DrawLine(rx + scale, ry, rx + scale, ry + scale, borderCol2);
            }
        }
    }

    public bool CheckAndDrawFloatingMenu(Canvas canvas, Camera2D camera, Vector2 mouseScreenPos, HistoryManager history, Action<string> onStatus)
    {
        if (!HasSelection || IsCreatingSelection || IsDraggingFloating) return false;

        Rectangle curBounds = GetCurrentBounds();
        Vector2 bottomCenterWorld = new Vector2(
            canvas.Offset.X + (curBounds.X + curBounds.Width / 2f) * canvas.PixelScale,
            canvas.Offset.Y + (curBounds.Y + curBounds.Height) * canvas.PixelScale
        );

        Vector2 screenAnchor = Raylib.GetWorldToScreen2D(bottomCenterWorld, camera);

        int menuWidth = 285;
        int menuHeight = 32;
        float menuX = Math.Clamp(screenAnchor.X - menuWidth / 2f, 150, Raylib.GetScreenWidth() - 190 - menuWidth);
        float menuY = Math.Clamp(screenAnchor.Y + 12, 45, Raylib.GetScreenHeight() - 45);

        Rectangle menuRect = new Rectangle(menuX, menuY, menuWidth, menuHeight);

        Raylib.DrawRectangleRec(menuRect, new Color(28, 28, 32, 245));
        Raylib.DrawRectangleLinesEx(menuRect, 1, Color.SkyBlue);

        string[] labels = { "Kopyala", "Kes", "Cogalt", "Sil", "X" };
        int[] widths = { 62, 45, 58, 42, 28 };
        Color[] colors = {
            new Color(50, 80, 130, 255),
            new Color(120, 80, 40, 255),
            new Color(45, 110, 75, 255),
            new Color(130, 45, 45, 255),
            new Color(70, 70, 70, 255)
        };

        float curX = menuX + 6;
        bool interacted = Raylib.CheckCollisionPointRec(mouseScreenPos, menuRect);

        for (int i = 0; i < labels.Length; i++)
        {
            Rectangle btnRect = new Rectangle(curX, menuY + 4, widths[i], 24);
            bool hover = Raylib.CheckCollisionPointRec(mouseScreenPos, btnRect);

            Color bg = hover
                ? new Color((byte)Math.Min(255, colors[i].R + 30), (byte)Math.Min(255, colors[i].G + 30), (byte)Math.Min(255, colors[i].B + 30), (byte)255)
                : colors[i];

            Raylib.DrawRectangleRec(btnRect, bg);
            Raylib.DrawRectangleLinesEx(btnRect, 1, Color.Gray);

            int tw = Raylib.MeasureText(labels[i], 12);
            Raylib.DrawText(labels[i], (int)(btnRect.X + (btnRect.Width - tw) / 2), (int)btnRect.Y + 6, 12, Color.White);

            if (hover && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                switch (i)
                {
                    case 0:
                        Copy(canvas);
                        onStatus("Secim kopyalandi (Ctrl+C)");
                        break;
                    case 1:
                        Cut(canvas, history);
                        onStatus("Secim kesildi (Ctrl+X)");
                        break;
                    case 2:
                        Duplicate(canvas, history);
                        onStatus("Secim cogaltildi (Ctrl+D)");
                        break;
                    case 3:
                        DeleteSelection(canvas, history);
                        onStatus("Secim silindi (Del)");
                        break;
                    case 4:
                        CancelOrCommit(canvas);
                        onStatus("Secim tamamlandi / kapatildi");
                        break;
                }
                return true;
            }

            curX += widths[i] + 6;
        }

        return interacted && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
}