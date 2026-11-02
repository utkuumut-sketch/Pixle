// === DOSYA: \Core\Selection\SelectionManager.cs ===
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

public partial class SelectionManager
{
    public bool HasSelection { get; private set; } = false;
    public bool IsFloating { get; private set; } = false;
    public bool IsCreatingSelection { get; private set; } = false;
    public bool IsDraggingFloating { get; private set; } = false;

    private bool[,] mask = new bool[1, 1];
    private Color[,] floatingPixels = new Color[1, 1];

    public Rectangle Bounds { get; private set; }
    public Vector2 Offset { get; private set; }

    private int floatingMenuState = 0; // 0: Ana Menü, 1: Yansıt, 2: Döndür
    private string rotateAngleText = "0";
    public bool isRotateInputActive = false;
    private float currentRotationAngle = 0f;
    public bool IsDraggingDial { get; private set; } = false;

    private (int x, int y) createStartPos;
    private (int x, int y) createCurrentPos;
    public List<(int x, int y)> LassoPath { get; private set; } = new();

    private (int x, int y) dragStartGrid;
    private Vector2 initialOffset;

    private Color[,]? clipboardPixels;
    private bool[,]? clipboardMask;
    private int clipWidth, clipHeight;
    private Vector2 clipOrigin;

    public void EnsureSize(int width, int height) { }

    public bool IsPointInCurrentSelection(int gx, int gy)
    {
        if (!HasSelection) return false;
        int localX = gx - (int)Math.Round(Bounds.X + Offset.X);
        int localY = gy - (int)Math.Round(Bounds.Y + Offset.Y);
        if (localX < 0 || localX >= mask.GetLength(0) || localY < 0 || localY >= mask.GetLength(1)) return false;
        return mask[localX, localY];
    }

    public Rectangle GetCurrentBounds() => new Rectangle(Bounds.X + Offset.X, Bounds.Y + Offset.Y, Bounds.Width, Bounds.Height);

    private bool[,] RasterizeCurrentMaskToCanvas(int w, int h)
    {
        bool[,] canvasMask = new bool[w, h];
        if (!HasSelection) return canvasMask;

        int ox = (int)Math.Round(Bounds.X + Offset.X);
        int oy = (int)Math.Round(Bounds.Y + Offset.Y);
        int mw = mask.GetLength(0);
        int mh = mask.GetLength(1);

        for (int x = 0; x < mw; x++)
        {
            int cx = ox + x;
            if (cx < 0 || cx >= w) continue;
            for (int y = 0; y < mh; y++)
            {
                int cy = oy + y;
                if (cy < 0 || cy >= h) continue;
                if (mask[x, y]) canvasMask[cx, cy] = true;
            }
        }
        return canvasMask;
    }

    private void BuildTightMaskFromCanvas(bool[,] canvasMask, int searchMinX, int searchMaxX, int searchMinY, int searchMaxY)
    {
        int w = canvasMask.GetLength(0);
        int h = canvasMask.GetLength(1);

        searchMinX = Math.Clamp(searchMinX, 0, w - 1);
        searchMaxX = Math.Clamp(searchMaxX, 0, w - 1);
        searchMinY = Math.Clamp(searchMinY, 0, h - 1);
        searchMaxY = Math.Clamp(searchMaxY, 0, h - 1);

        int minX = w, maxX = -1, minY = h, maxY = -1;
        bool any = false;

        for (int x = searchMinX; x <= searchMaxX; x++)
        {
            for (int y = searchMinY; y <= searchMaxY; y++)
            {
                if (canvasMask[x, y])
                {
                    any = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        Offset = Vector2.Zero;
        if (!any)
        {
            ResetSelectionState();
            return;
        }

        int bw = maxX - minX + 1;
        int bh = maxY - minY + 1;
        mask = new bool[bw, bh];
        floatingPixels = new Color[bw, bh];

        for (int x = 0; x < bw; x++)
            for (int y = 0; y < bh; y++)
                mask[x, y] = canvasMask[minX + x, minY + y];

        Bounds = new Rectangle(minX, minY, bw, bh);
        HasSelection = true;
    }

    public void StartSelection(Canvas canvas, int gx, int gy, SelectToolMode mode, bool isAdd = false, bool isSubtract = false)
    {
        if (IsFloating) CancelOrCommit(canvas);

        if (mode == SelectToolMode.MagicWand)
        {
            DoMagicWand(canvas, gx, gy, isAdd, isSubtract);
        }
        else
        {
            if (!isAdd && !isSubtract) CancelOrCommit(canvas);

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
                foreach (var pt in ToolManager.GetLinePixels(last.x, last.y, gx, gy))
                    if (LassoPath[^1] != pt) LassoPath.Add(pt);
            }
        }
    }

    public void EndSelectionCreation(Canvas canvas, SelectToolMode mode, bool isAdd = false, bool isSubtract = false)
    {
        if (!IsCreatingSelection) return;
        IsCreatingSelection = false;

        int w = canvas.Width;
        int h = canvas.Height;
        bool[,] baseMask = (isAdd || isSubtract) ? RasterizeCurrentMaskToCanvas(w, h) : new bool[w, h];
        int opMinX = 0, opMaxX = w - 1, opMinY = 0, opMaxY = h - 1;

        if (mode == SelectToolMode.Rectangle)
        {
            opMinX = Math.Clamp(Math.Min(createStartPos.x, createCurrentPos.x), 0, w - 1);
            opMaxX = Math.Clamp(Math.Max(createStartPos.x, createCurrentPos.x), 0, w - 1);
            opMinY = Math.Clamp(Math.Min(createStartPos.y, createCurrentPos.y), 0, h - 1);
            opMaxY = Math.Clamp(Math.Max(createStartPos.y, createCurrentPos.y), 0, h - 1);

            for (int x = opMinX; x <= opMaxX; x++)
                for (int y = opMinY; y <= opMaxY; y++)
                    baseMask[x, y] = !isSubtract;
        }
        else if (mode == SelectToolMode.Lasso)
        {
            if (LassoPath.Count < 3)
            {
                LassoPath.Clear();
                if (!isAdd && !isSubtract)
                {
                    ResetSelectionState();
                }
                return;
            }

            foreach (var pt in ToolManager.GetLinePixels(LassoPath[^1].x, LassoPath[^1].y, LassoPath[0].x, LassoPath[0].y))
            {
                if (LassoPath[^1] != pt) LassoPath.Add(pt);
            }

            int polyMinX = w - 1, polyMaxX = 0, polyMinY = h - 1, polyMaxY = 0;
            foreach (var (px, py) in LassoPath)
            {
                if (px < polyMinX) polyMinX = px;
                if (px > polyMaxX) polyMaxX = px;
                if (py < polyMinY) polyMinY = py;
                if (py > polyMaxY) polyMaxY = py;
            }

            opMinX = Math.Clamp(polyMinX, 0, w - 1);
            opMaxX = Math.Clamp(polyMaxX, 0, w - 1);
            opMinY = Math.Clamp(polyMinY, 0, h - 1);
            opMaxY = Math.Clamp(polyMaxY, 0, h - 1);

            bool[,] lassoMask = new bool[w, h];
            for (int x = opMinX; x <= opMaxX; x++)
                for (int y = opMinY; y <= opMaxY; y++)
                    if (IsPointInPolygon(x, y, LassoPath))
                        lassoMask[x, y] = true;

            foreach (var (px, py) in LassoPath)
                if (px >= 0 && px < w && py >= 0 && py < h)
                    lassoMask[px, py] = true;

            for (int x = opMinX; x <= opMaxX; x++)
            {
                for (int y = opMinY; y <= opMaxY; y++)
                {
                    if (lassoMask[x, y])
                    {
                        baseMask[x, y] = !isSubtract;
                    }
                }
            }
        }

        UpdateTightBoundsWithExisting(baseMask, opMinX, opMaxX, opMinY, opMaxY, isAdd || isSubtract);
    }

    private void DoMagicWand(Canvas canvas, int startX, int startY, bool isAdd, bool isSubtract)
    {
        int w = canvas.Width;
        int h = canvas.Height;
        if (startX < 0 || startX >= w || startY < 0 || startY >= h) return;

        Color target = canvas.GetMergedPixel(startX, startY);
        bool[,] baseMask = (isAdd || isSubtract) ? RasterizeCurrentMaskToCanvas(w, h) : new bool[w, h];
        Queue<(int x, int y)> q = new();
        q.Enqueue((startX, startY));
        bool[,] visited = new bool[w, h];
        visited[startX, startY] = true;

        int minX = startX, maxX = startX, minY = startY, maxY = startY;
        ReadOnlySpan<(int dx, int dy)> dirs = stackalloc (int, int)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            baseMask[x, y] = !isSubtract;

            if (x < minX) minX = x;
            if (x > maxX) maxX = x;
            if (y < minY) minY = y;
            if (y > maxY) maxY = y;

            foreach (var (dx, dy) in dirs)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && nx < w && ny >= 0 && ny < h && !visited[nx, ny])
                {
                    visited[nx, ny] = true;
                    Color c = canvas.GetMergedPixel(nx, ny);
                    if (c.R == target.R && c.G == target.G && c.B == target.B && c.A == target.A)
                        q.Enqueue((nx, ny));
                }
            }
        }

        UpdateTightBoundsWithExisting(baseMask, minX, maxX, minY, maxY, isAdd || isSubtract);
    }

    private void UpdateTightBoundsWithExisting(bool[,] baseMask, int minX, int maxX, int minY, int maxY, bool mergeBounds)
    {
        if (mergeBounds && HasSelection)
        {
            BuildTightMaskFromCanvas(baseMask, 0, baseMask.GetLength(0) - 1, 0, baseMask.GetLength(1) - 1);
        }
        else
        {
            BuildTightMaskFromCanvas(baseMask, minX, maxX, minY, maxY);
        }
    }

    private static bool IsPointInPolygon(int x, int y, List<(int x, int y)> poly)
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

    public void StartMove(Canvas canvas, HistoryManager history, int gx, int gy)
    {
        if (!HasSelection) return;

        if (floatingMenuState == 2)
        {
            ClearRotationBackup();
            floatingMenuState = 0;
        }

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
        Offset = new Vector2(initialOffset.X + (gx - dragStartGrid.x), initialOffset.Y + (gy - dragStartGrid.y));
    }

    public void EndMove() => IsDraggingFloating = false;

    private void LiftSelection(Canvas canvas)
    {
        if (IsFloating || !HasSelection) return;

        int bx = (int)Bounds.X, by = (int)Bounds.Y;
        int bw = mask.GetLength(0), bh = mask.GetLength(1);
        floatingPixels = new Color[bw, bh];

        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                if (mask[x, y])
                {
                    floatingPixels[x, y] = canvas.GetPixel(bx + x, by + y);
                    canvas.SetPixel(bx + x, by + y, Canvas.TransparentColor);
                }
                else
                {
                    floatingPixels[x, y] = Canvas.TransparentColor;
                }
            }
        }
        IsFloating = true;
    }

    private void ResetSelectionState()
    {
        IsFloating = false;
        HasSelection = false;
        IsDraggingFloating = false;
        IsCreatingSelection = false;
        IsDraggingDial = false;
        floatingMenuState = 0;
        isRotateInputActive = false;
        ClearRotationBackup();
        Offset = Vector2.Zero;
        Bounds = new Rectangle(0, 0, 0, 0);
        mask = new bool[1, 1];
        floatingPixels = new Color[1, 1];
    }

    public void CancelOrCommit(Canvas canvas)
    {
        if (!HasSelection && !IsFloating) return;

        if (IsFloating)
        {
            int w = canvas.Width, h = canvas.Height;
            int destStartX = (int)Math.Round(Bounds.X + Offset.X);
            int destStartY = (int)Math.Round(Bounds.Y + Offset.Y);
            int bw = mask.GetLength(0), bh = mask.GetLength(1);

            for (int x = 0; x < bw; x++)
            {
                for (int y = 0; y < bh; y++)
                {
                    if (mask[x, y] && floatingPixels[x, y].A > 0)
                    {
                        int tx = destStartX + x, ty = destStartY + y;
                        if (tx >= 0 && tx < w && ty >= 0 && ty < h)
                            canvas.SetPixel(tx, ty, floatingPixels[x, y]);
                    }
                }
            }
        }

        ResetSelectionState();
    }

    public void DeleteSelection(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;

        if (!IsFloating)
        {
            history.SaveState(canvas);
            int destStartX = (int)Math.Round(Bounds.X + Offset.X);
            int destStartY = (int)Math.Round(Bounds.Y + Offset.Y);
            int bw = mask.GetLength(0), bh = mask.GetLength(1);

            for (int x = 0; x < bw; x++)
                for (int y = 0; y < bh; y++)
                    if (mask[x, y])
                        canvas.SetPixel(destStartX + x, destStartY + y, Canvas.TransparentColor);
        }

        ResetSelectionState();
    }

    public void Copy(Canvas canvas)
    {
        if (!HasSelection) return;

        int bx = (int)Bounds.X, by = (int)Bounds.Y;
        clipWidth = mask.GetLength(0);
        clipHeight = mask.GetLength(1);
        clipOrigin = new Vector2(bx + Offset.X, by + Offset.Y);

        clipboardPixels = new Color[clipWidth, clipHeight];
        clipboardMask = new bool[clipWidth, clipHeight];

        for (int x = 0; x < clipWidth; x++)
        {
            for (int y = 0; y < clipHeight; y++)
            {
                if (mask[x, y])
                {
                    clipboardMask[x, y] = true;
                    clipboardPixels[x, y] = IsFloating ? floatingPixels[x, y] : canvas.GetPixel(bx + x, by + y);
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

        int startX = Math.Clamp((int)clipOrigin.X, 0, Math.Max(0, canvas.Width - clipWidth));
        int startY = Math.Clamp((int)clipOrigin.Y, 0, Math.Max(0, canvas.Height - clipHeight));

        mask = new bool[clipWidth, clipHeight];
        floatingPixels = new Color[clipWidth, clipHeight];
        Array.Copy(clipboardMask, mask, clipboardMask.Length);
        Array.Copy(clipboardPixels, floatingPixels, clipboardPixels.Length);

        Bounds = new Rectangle(startX, startY, clipWidth, clipHeight);
        Offset = new Vector2(1, 1);
        IsFloating = true;
        HasSelection = true;
    }

    public void Duplicate(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        Copy(canvas);
        Paste(canvas, history);
    }
}