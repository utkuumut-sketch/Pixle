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

    // --- YÜZEN MENÜ VE CANLI DÖNDÜRME ÇARKI DEĞİŞKENLERİ ---
    private int floatingMenuState = 0; // 0: Ana Menü, 1: Yansıt, 2: Döndür
    private string rotateAngleText = "0";
    public bool isRotateInputActive = false;
    private float currentRotationAngle = 0f;
    public bool IsDraggingDial { get; private set; } = false;

    // Canlı döndürme sırasında piksellerin bozulmaması için orijinal kopyayı tutar
    private bool[,]? rotBackupMask;
    private Color[,]? rotBackupPixels;
    private Rectangle rotBackupBounds;
    private Vector2 rotBackupOffset;
    // --------------------------------------------------------

    private (int x, int y) createStartPos;
    private (int x, int y) createCurrentPos;
    public List<(int x, int y)> LassoPath { get; private set; } = new();

    private (int x, int y) dragStartGrid;
    private Vector2 initialOffset;

    private Color[,]? clipboardPixels;
    private bool[,]? clipboardMask;
    private int clipWidth, clipHeight;
    private Vector2 clipOrigin;

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

        // Eğer döndürme menüsü açıksa ve kullanıcı tuvalden seçimi tutarsa, döndürmeyi onayla ve kapat
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
        IsDraggingDial = false;
        floatingMenuState = 0;
        isRotateInputActive = false;
        ClearRotationBackup();
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
        IsDraggingDial = false;
        floatingMenuState = 0;
        ClearRotationBackup();
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
        clipOrigin = new Vector2(bx + Offset.X, by + Offset.Y);

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

        int startX = Math.Clamp((int)clipOrigin.X, 0, Math.Max(0, w - clipWidth));
        int startY = Math.Clamp((int)clipOrigin.Y, 0, Math.Max(0, h - clipHeight));

        for (int x = 0; x < clipWidth; x++)
        {
            for (int y = 0; y < clipHeight; y++)
            {
                int tx = startX + x;
                int ty = startY + y;
                if (tx < w && ty < h && clipboardMask[x, y])
                {
                    mask[tx, ty] = true;
                    floatingPixels[tx, ty] = clipboardPixels[x, y];
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

    // --- DÖNÜŞTÜRME (TRANSFORM), YANSITMA VE CANLI ÇARK SİSTEMİ ---

    public void FlipHorizontal(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        if (!IsFloating) { history.SaveState(canvas); LiftSelection(canvas); }

        int w = canvas.Width;
        int h = canvas.Height;
        bool[,] newMask = new bool[w, h];
        Color[,] newFloating = new Color[w, h];

        int bx = (int)Bounds.X;
        int bw = (int)Bounds.Width;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mask[x, y])
                {
                    int newX = bx + (bw - 1 - (x - bx));
                    if (newX >= 0 && newX < w)
                    {
                        newMask[newX, y] = true;
                        newFloating[newX, y] = floatingPixels[x, y];
                    }
                }
            }
        }
        mask = newMask;
        floatingPixels = newFloating;
    }

    public void FlipVertical(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        if (!IsFloating) { history.SaveState(canvas); LiftSelection(canvas); }

        int w = canvas.Width;
        int h = canvas.Height;
        bool[,] newMask = new bool[w, h];
        Color[,] newFloating = new Color[w, h];

        int by = (int)Bounds.Y;
        int bh = (int)Bounds.Height;

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                if (mask[x, y])
                {
                    int newY = by + (bh - 1 - (y - by));
                    if (newY >= 0 && newY < h)
                    {
                        newMask[x, newY] = true;
                        newFloating[x, newY] = floatingPixels[x, y];
                    }
                }
            }
        }
        mask = newMask;
        floatingPixels = newFloating;
    }

    private void BeginRotationSession(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        if (!IsFloating)
        {
            history.SaveState(canvas);
            LiftSelection(canvas);
        }

        int w = mask.GetLength(0);
        int h = mask.GetLength(1);
        rotBackupMask = new bool[w, h];
        rotBackupPixels = new Color[w, h];
        Array.Copy(mask, rotBackupMask, mask.Length);
        Array.Copy(floatingPixels, rotBackupPixels, floatingPixels.Length);
        rotBackupBounds = Bounds;
        rotBackupOffset = Offset;

        currentRotationAngle = 0f;
        rotateAngleText = "0";
    }

    private void RestoreRotationBackup(int w, int h)
    {
        if (rotBackupMask == null || rotBackupPixels == null) return;
        Array.Copy(rotBackupMask, mask, mask.Length);
        Array.Copy(rotBackupPixels, floatingPixels, floatingPixels.Length);
        Bounds = rotBackupBounds;
        Offset = rotBackupOffset;
        RecalculateBounds(w, h);
    }

    private void ClearRotationBackup()
    {
        rotBackupMask = null;
        rotBackupPixels = null;
        IsDraggingDial = false;
    }

    private void ApplyLiveRotation(Canvas canvas, float angleDegrees)
    {
        if (rotBackupMask == null || rotBackupPixels == null) return;

        int w = canvas.Width;
        int h = canvas.Height;

        if (MathF.Abs(angleDegrees) < 0.1f)
        {
            RestoreRotationBackup(w, h);
            return;
        }

        float rad = -angleDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        int bw = (int)rotBackupBounds.Width;
        int bh = (int)rotBackupBounds.Height;
        int bx = (int)rotBackupBounds.X;
        int by = (int)rotBackupBounds.Y;

        float cx = bx + bw / 2f;
        float cy = by + bh / 2f;

        Vector2[] corners = {
            new Vector2(bx - cx, by - cy),
            new Vector2(bx + bw - cx, by - cy),
            new Vector2(bx + bw - cx, by + bh - cy),
            new Vector2(bx - cx, by + bh - cy)
        };

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var p in corners)
        {
            float rx = p.X * cos - p.Y * sin;
            float ry = p.X * sin + p.Y * cos;
            if (rx < minX) minX = rx; if (rx > maxX) maxX = rx;
            if (ry < minY) minY = ry; if (ry > maxY) maxY = ry;
        }

        int newW = (int)MathF.Ceiling(maxX - minX);
        int newH = (int)MathF.Ceiling(maxY - minY);
        if (newW <= 0 || newH <= 0 || newW > 2048 || newH > 2048) return;

        Array.Clear(mask, 0, mask.Length);
        Array.Clear(floatingPixels, 0, floatingPixels.Length);

        float newCx = newW / 2f;
        float newCy = newH / 2f;

        int startX = (int)MathF.Round(cx - newCx);
        int startY = (int)MathF.Round(cy - newCy);

        for (int x = 0; x < newW; x++)
        {
            for (int y = 0; y < newH; y++)
            {
                float dx = (x + 0.5f) - newCx;
                float dy = (y + 0.5f) - newCy;

                int sx = (int)MathF.Floor(dx * cos + dy * sin + cx);
                int sy = (int)MathF.Floor(-dx * sin + dy * cos + cy);

                if (sx >= 0 && sx < w && sy >= 0 && sy < h && rotBackupMask[sx, sy])
                {
                    int tx = startX + x;
                    int ty = startY + y;
                    if (tx >= 0 && tx < w && ty >= 0 && ty < h)
                    {
                        mask[tx, ty] = true;
                        floatingPixels[tx, ty] = rotBackupPixels[sx, sy];
                    }
                }
            }
        }

        RecalculateBounds(w, h);
        Offset = rotBackupOffset;
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

    public Rectangle GetFloatingMenuBounds(Canvas canvas, Camera2D camera)
    {
        Rectangle anchorBounds = (floatingMenuState == 2 && rotBackupMask != null)
            ? new Rectangle(rotBackupBounds.X + rotBackupOffset.X, rotBackupBounds.Y + rotBackupOffset.Y, rotBackupBounds.Width, rotBackupBounds.Height)
            : GetCurrentBounds();

        Vector2 bottomCenterWorld = new Vector2(
            canvas.Offset.X + (anchorBounds.X + anchorBounds.Width / 2f) * canvas.PixelScale,
            canvas.Offset.Y + (anchorBounds.Y + anchorBounds.Height) * canvas.PixelScale
        );

        Vector2 screenAnchor = Raylib.GetWorldToScreen2D(bottomCenterWorld, camera);

        int menuWidth = floatingMenuState == 0 ? 385 : (floatingMenuState == 1 ? 165 : 375);
        int menuHeight = floatingMenuState == 2 ? 48 : 32;

        float menuX = Math.Clamp(screenAnchor.X - menuWidth / 2f, 150, Raylib.GetScreenWidth() - 190 - menuWidth);
        float menuY = Math.Clamp(screenAnchor.Y + 12, 45, Raylib.GetScreenHeight() - 60);

        return new Rectangle(menuX, menuY, menuWidth, menuHeight);
    }

    public bool CheckAndDrawFloatingMenu(Canvas canvas, Camera2D camera, Vector2 mouseScreenPos, HistoryManager history, Action<string> onStatus)
    {
        if (!HasSelection || IsCreatingSelection || IsDraggingFloating) return false;

        Rectangle menuRect = GetFloatingMenuBounds(canvas, camera);
        float menuX = menuRect.X;
        float menuY = menuRect.Y;

        Raylib.DrawRectangleRec(menuRect, new Color(28, 28, 32, 245));
        Raylib.DrawRectangleLinesEx(menuRect, 1, Color.SkyBlue);

        float curX = menuX + 6;
        bool interacted = Raylib.CheckCollisionPointRec(mouseScreenPos, menuRect);
        bool click = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (floatingMenuState == 0) // 0: ANA MENÜ
        {
            string[] labels = { "Kopyala", "Kes", "Cogalt", "Sil", "Yansit", "Dondur", "X" };
            int[] widths = { 58, 40, 52, 38, 55, 60, 28 };
            Color[] colors = {
                new Color(50, 80, 130, 255), new Color(120, 80, 40, 255),
                new Color(45, 110, 75, 255), new Color(130, 45, 45, 255),
                new Color(60, 60, 80, 255), new Color(60, 60, 80, 255),
                new Color(70, 70, 70, 255)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle btnRect = new Rectangle(curX, menuY + 4, widths[i], 24);
                bool hover = Raylib.CheckCollisionPointRec(mouseScreenPos, btnRect);
                Raylib.DrawRectangleRec(btnRect, hover ? new Color((byte)Math.Min(255, colors[i].R + 30), (byte)Math.Min(255, colors[i].G + 30), (byte)Math.Min(255, colors[i].B + 30), (byte)255) : colors[i]);
                Raylib.DrawRectangleLinesEx(btnRect, 1, Color.Gray);
                int tw = Raylib.MeasureText(labels[i], 12);
                Raylib.DrawText(labels[i], (int)(btnRect.X + (btnRect.Width - tw) / 2), (int)btnRect.Y + 6, 12, Color.White);

                if (hover && click)
                {
                    if (i == 0) { Copy(canvas); onStatus("Secim kopyalandi (Ctrl+C)"); }
                    else if (i == 1) { Cut(canvas, history); onStatus("Secim kesildi (Ctrl+X)"); }
                    else if (i == 2) { Duplicate(canvas, history); onStatus("Secim cogaltildi (Ctrl+D)"); }
                    else if (i == 3) { DeleteSelection(canvas, history); onStatus("Secim silindi (Del)"); }
                    else if (i == 4) floatingMenuState = 1;
                    else if (i == 5) { BeginRotationSession(canvas, history); floatingMenuState = 2; }
                    else if (i == 6) { CancelOrCommit(canvas); onStatus("Secim tamamlandi"); }
                    return true;
                }
                curX += widths[i] + 6;
            }
        }
        else if (floatingMenuState == 1) // 1: YANSIT MENÜSÜ
        {
            string[] labels = { "Yatay", "Dikey", "Geri" };
            int[] widths = { 50, 50, 40 };

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle btnRect = new Rectangle(curX, menuY + 4, widths[i], 24);
                bool hover = Raylib.CheckCollisionPointRec(mouseScreenPos, btnRect);
                Raylib.DrawRectangleRec(btnRect, hover ? new Color(90, 90, 110, 255) : new Color(60, 60, 80, 255));
                Raylib.DrawRectangleLinesEx(btnRect, 1, Color.Gray);
                int tw = Raylib.MeasureText(labels[i], 12);
                Raylib.DrawText(labels[i], (int)(btnRect.X + (btnRect.Width - tw) / 2), (int)btnRect.Y + 6, 12, Color.White);

                if (hover && click)
                {
                    if (i == 0) { FlipHorizontal(canvas, history); onStatus("Yatay yansitildi."); floatingMenuState = 0; }
                    else if (i == 1) { FlipVertical(canvas, history); onStatus("Dikey yansitildi."); floatingMenuState = 0; }
                    else if (i == 2) floatingMenuState = 0;
                    return true;
                }
                curX += widths[i] + 6;
            }
        }
        else if (floatingMenuState == 2) // 2: CANLI ÇARKLI DÖNDÜRME MENÜSÜ
        {
            // 1. DÖNDÜRME ÇARKI (DIAL)
            Vector2 dialCenter = new Vector2(curX + 20, menuY + 24);
            float dialRadius = 18f;
            bool hoverDial = Vector2.Distance(mouseScreenPos, dialCenter) <= dialRadius + 4;

            if (click && hoverDial)
            {
                IsDraggingDial = true;
                isRotateInputActive = false;
            }

            if (IsDraggingDial)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    float dx = mouseScreenPos.X - dialCenter.X;
                    float dy = mouseScreenPos.Y - dialCenter.Y;
                    float deg = MathF.Atan2(dy, dx) * (180f / MathF.PI) + 90f;
                    if (deg > 180f) deg -= 360f;
                    if (deg < -180f) deg += 360f;

                    // Shift basılıysa 15 derecelik açılara kilitle
                    if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift))
                        deg = MathF.Round(deg / 15f) * 15f;
                    else
                        deg = MathF.Round(deg);

                    if (MathF.Abs(deg - currentRotationAngle) >= 0.5f)
                    {
                        currentRotationAngle = deg;
                        rotateAngleText = ((int)currentRotationAngle).ToString();
                        ApplyLiveRotation(canvas, currentRotationAngle);
                    }
                }
                else
                {
                    IsDraggingDial = false;
                }
            }

            // Çarkın Çizimi
            Raylib.DrawCircleV(dialCenter, dialRadius, new Color(40, 40, 48, 255));
            Raylib.DrawCircleLines((int)dialCenter.X, (int)dialCenter.Y, dialRadius, IsDraggingDial || hoverDial ? Color.Yellow : Color.SkyBlue);

            // Çark üzerindeki 0, 90, 180, -90 çentikleri
            for (int a = 0; a < 360; a += 90)
            {
                float r = a * (MathF.PI / 180f);
                Vector2 p1 = new Vector2(dialCenter.X + MathF.Cos(r) * (dialRadius - 3), dialCenter.Y + MathF.Sin(r) * (dialRadius - 3));
                Vector2 p2 = new Vector2(dialCenter.X + MathF.Cos(r) * dialRadius, dialCenter.Y + MathF.Sin(r) * dialRadius);
                Raylib.DrawLineV(p1, p2, Color.Gray);
            }

            // Çarkın İbresi ve Tutamacı
            float radAngle = (currentRotationAngle - 90f) * (MathF.PI / 180f);
            Vector2 knobPos = new Vector2(
                dialCenter.X + MathF.Cos(radAngle) * (dialRadius - 4),
                dialCenter.Y + MathF.Sin(radAngle) * (dialRadius - 4)
            );
            Raylib.DrawLineEx(dialCenter, knobPos, 2f, Color.Yellow);
            Raylib.DrawCircleV(knobPos, 3.5f, Color.Red);
            Raylib.DrawCircleV(dialCenter, 2f, Color.White);

            curX += 46;

            // 2. AÇI METİN KUTUSU (Elle yazarken anında döndürür)
            Rectangle inputRect = new Rectangle(curX, menuY + 12, 46, 24);
            bool inputHover = Raylib.CheckCollisionPointRec(mouseScreenPos, inputRect);
            if (click) isRotateInputActive = inputHover;

            if (isRotateInputActive)
            {
                bool textChanged = false;
                int key = Raylib.GetCharPressed();
                while (key > 0)
                {
                    if ((char.IsDigit((char)key) || key == '-') && rotateAngleText.Length < 5)
                    {
                        if (key == '-')
                        {
                            if (rotateAngleText.StartsWith("-")) rotateAngleText = rotateAngleText[1..];
                            else rotateAngleText = "-" + rotateAngleText;
                            textChanged = true;
                        }
                        else
                        {
                            if (rotateAngleText == "0") rotateAngleText = ((char)key).ToString();
                            else rotateAngleText += (char)key;
                            textChanged = true;
                        }
                    }
                    key = Raylib.GetCharPressed();
                }
                if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && rotateAngleText.Length > 0)
                {
                    rotateAngleText = rotateAngleText[..^1];
                    textChanged = true;
                }

                if (textChanged)
                {
                    if (float.TryParse(rotateAngleText, out float parsedAngle))
                    {
                        currentRotationAngle = Math.Clamp(parsedAngle, -360f, 360f);
                        ApplyLiveRotation(canvas, currentRotationAngle);
                    }
                    else if (string.IsNullOrEmpty(rotateAngleText) || rotateAngleText == "-")
                    {
                        currentRotationAngle = 0f;
                        ApplyLiveRotation(canvas, 0f);
                    }
                }
            }

            Raylib.DrawRectangleRec(inputRect, isRotateInputActive ? new Color(15, 15, 20, 255) : new Color(40, 40, 45, 255));
            Raylib.DrawRectangleLinesEx(inputRect, 1, isRotateInputActive ? Color.Yellow : Color.Gray);
            string displayAngle = rotateAngleText + (isRotateInputActive && (int)(Raylib.GetTime() * 3) % 2 == 0 ? "|" : "°");
            Raylib.DrawText(displayAngle, (int)inputRect.X + 5, (int)inputRect.Y + 6, 12, isRotateInputActive ? Color.Yellow : Color.White);

            curX += 52;

            // 3. HAZIR AÇI VE ONAY BUTONLARI
            string[] labels = { "-90", "90", "180", "Onayla", "Iptal" };
            int[] widths = { 34, 30, 34, 52, 42 };

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle btnRect = new Rectangle(curX, menuY + 12, widths[i], 24);
                bool hover = Raylib.CheckCollisionPointRec(mouseScreenPos, btnRect);
                Color bg = i == 3 ? new Color(45, 110, 75, 255) : (i == 4 ? new Color(120, 45, 45, 255) : new Color(60, 60, 80, 255));
                if (hover) bg = new Color((byte)Math.Min(255, bg.R + 30), (byte)Math.Min(255, bg.G + 30), (byte)Math.Min(255, bg.B + 30), (byte)255);

                Raylib.DrawRectangleRec(btnRect, bg);
                Raylib.DrawRectangleLinesEx(btnRect, 1, Color.Gray);
                int tw = Raylib.MeasureText(labels[i], 12);
                Raylib.DrawText(labels[i], (int)(btnRect.X + (btnRect.Width - tw) / 2), (int)btnRect.Y + 6, 12, Color.White);

                if (hover && click)
                {
                    if (i == 0) { currentRotationAngle = -90f; rotateAngleText = "-90"; ApplyLiveRotation(canvas, -90f); }
                    else if (i == 1) { currentRotationAngle = 90f; rotateAngleText = "90"; ApplyLiveRotation(canvas, 90f); }
                    else if (i == 2) { currentRotationAngle = 180f; rotateAngleText = "180"; ApplyLiveRotation(canvas, 180f); }
                    else if (i == 3) // Onayla
                    {
                        ClearRotationBackup();
                        floatingMenuState = 0;
                        isRotateInputActive = false;
                        onStatus($"{currentRotationAngle}° donduruldu.");
                    }
                    else if (i == 4) // İptal (Eski haline geri döndür)
                    {
                        RestoreRotationBackup(canvas.Width, canvas.Height);
                        ClearRotationBackup();
                        floatingMenuState = 0;
                        isRotateInputActive = false;
                        onStatus("Dondurme iptal edildi.");
                    }
                    return true;
                }
                curX += widths[i] + 5;
            }
        }

        return (interacted && click) || IsDraggingDial;
    }
}