// === DOSYA: \Core\Selection\SelectionManager.Transform.cs ===
using System;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.Core;

public partial class SelectionManager
{
    private bool[,]? rotBackupMask;
    private Color[,]? rotBackupPixels;
    private Rectangle rotBackupBounds;
    private Vector2 rotBackupOffset;

    public void FlipHorizontal(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        if (!IsFloating) { history.SaveState(canvas); LiftSelection(canvas); }

        int bw = mask.GetLength(0), bh = mask.GetLength(1);
        bool[,] newMask = new bool[bw, bh];
        Color[,] newFloating = new Color[bw, bh];

        for (int x = 0; x < bw; x++)
        {
            int flippedX = bw - 1 - x;
            for (int y = 0; y < bh; y++)
            {
                newMask[flippedX, y] = mask[x, y];
                newFloating[flippedX, y] = floatingPixels[x, y];
            }
        }

        mask = newMask;
        floatingPixels = newFloating;
    }

    public void FlipVertical(Canvas canvas, HistoryManager history)
    {
        if (!HasSelection) return;
        if (!IsFloating) { history.SaveState(canvas); LiftSelection(canvas); }

        int bw = mask.GetLength(0), bh = mask.GetLength(1);
        bool[,] newMask = new bool[bw, bh];
        Color[,] newFloating = new Color[bw, bh];

        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                int flippedY = bh - 1 - y;
                newMask[x, flippedY] = mask[x, y];
                newFloating[x, flippedY] = floatingPixels[x, y];
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

        int bw = mask.GetLength(0), bh = mask.GetLength(1);
        rotBackupMask = new bool[bw, bh];
        rotBackupPixels = new Color[bw, bh];
        Array.Copy(mask, rotBackupMask, mask.Length);
        Array.Copy(floatingPixels, rotBackupPixels, floatingPixels.Length);
        rotBackupBounds = Bounds;
        rotBackupOffset = Offset;

        currentRotationAngle = 0f;
        rotateAngleText = "0";
    }

    private void RestoreRotationBackup()
    {
        if (rotBackupMask == null || rotBackupPixels == null) return;
        int bw = rotBackupMask.GetLength(0), bh = rotBackupMask.GetLength(1);
        mask = new bool[bw, bh];
        floatingPixels = new Color[bw, bh];
        Array.Copy(rotBackupMask, mask, rotBackupMask.Length);
        Array.Copy(rotBackupPixels, floatingPixels, rotBackupPixels.Length);
        Bounds = rotBackupBounds;
        Offset = rotBackupOffset;
        HasSelection = true;
    }

    private void ClearRotationBackup()
    {
        rotBackupMask = null;
        rotBackupPixels = null;
        IsDraggingDial = false;
    }

    private void ApplyLiveRotation(float angleDegrees)
    {
        if (rotBackupMask == null || rotBackupPixels == null) return;

        if (MathF.Abs(angleDegrees) < 0.1f)
        {
            RestoreRotationBackup();
            return;
        }

        float rad = angleDegrees * (MathF.PI / 180f);
        float cos = MathF.Cos(rad);
        float sin = MathF.Sin(rad);

        int srcW = rotBackupMask.GetLength(0);
        int srcH = rotBackupMask.GetLength(1);
        float srcCx = srcW / 2f, srcCy = srcH / 2f;

        int newW = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * cos) + MathF.Abs(srcH * sin)));
        int newH = Math.Max(1, (int)MathF.Ceiling(MathF.Abs(srcW * sin) + MathF.Abs(srcH * cos)));
        if (newW > 4096 || newH > 4096) return;

        bool[,] rotatedMask = new bool[newW, newH];
        Color[,] rotatedPixels = new Color[newW, newH];
        float dstCx = newW / 2f, dstCy = newH / 2f;

        for (int x = 0; x < newW; x++)
        {
            for (int y = 0; y < newH; y++)
            {
                float dx = (x + 0.5f) - dstCx;
                float dy = (y + 0.5f) - dstCy;

                int sx = (int)MathF.Floor(dx * cos + dy * sin + srcCx);
                int sy = (int)MathF.Floor(-dx * sin + dy * cos + srcCy);

                if (sx >= 0 && sx < srcW && sy >= 0 && sy < srcH && rotBackupMask[sx, sy])
                {
                    rotatedMask[x, y] = true;
                    rotatedPixels[x, y] = rotBackupPixels[sx, sy];
                }
            }
        }

        int newBx = (int)MathF.Round(rotBackupBounds.X + srcCx - dstCx);
        int newBy = (int)MathF.Round(rotBackupBounds.Y + srcCy - dstCy);

        mask = rotatedMask;
        floatingPixels = rotatedPixels;
        Bounds = new Rectangle(newBx, newBy, newW, newH);
        Offset = rotBackupOffset;
        HasSelection = true;
    }
}