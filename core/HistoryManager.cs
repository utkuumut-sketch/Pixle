// === DOSYA: \Core\HistoryManager.cs ===
using System;
using System.Collections.Generic;

namespace PixelArtApp.Core;

public class HistoryManager
{
    private readonly LinkedList<(List<Frame> frames, int activeFrame, long byteCost)> undoList = new();
    private readonly LinkedList<(List<Frame> frames, int activeFrame, long byteCost)> redoList = new();

    private long totalUndoBytes = 0;
    private long totalRedoBytes = 0;
    private const long MaxHistoryBytes = 256L * 1024L * 1024L; // 256 MB üst bellek limiti

    public Action? OnStateChanged { get; set; }

    private void DisposeFrameList(List<Frame> list)
    {
        foreach (var f in list) f.Dispose();
    }

    private static (List<Frame> frames, long byteCost) CreateSnapshot(Canvas canvas)
    {
        var snapshot = new List<Frame>(canvas.Frames.Count);
        long cost = 0;
        foreach (var f in canvas.Frames)
        {
            snapshot.Add(f.Clone());
            cost += (long)f.Layers.Count * canvas.Width * canvas.Height * 4L;
        }
        return (snapshot, cost);
    }

    private static void RestoreSnapshotToCanvas(Canvas canvas, List<Frame> sourceFrames, int activeFrame)
    {
        foreach (var f in canvas.Frames) f.Dispose();
        canvas.Frames.Clear();

        foreach (var f in sourceFrames)
        {
            var clonedFrame = f.Clone();
            clonedFrame.IsThumbnailDirty = true;
            foreach (var l in clonedFrame.Layers)
            {
                l.IsDirty = true;
            }
            canvas.Frames.Add(clonedFrame);
        }

        canvas.ActiveFrameIndex = Math.Clamp(activeFrame, 0, Math.Max(0, canvas.Frames.Count - 1));
    }

    private void TrimUndoHistory()
    {
        int maxSteps = Math.Max(1, AppSettings.MaxUndoSteps);
        while (undoList.Count > 0 && (undoList.Count > maxSteps || totalUndoBytes > MaxHistoryBytes))
        {
            var oldest = undoList.First!.Value;
            totalUndoBytes -= oldest.byteCost;
            DisposeFrameList(oldest.frames);
            undoList.RemoveFirst();
        }
    }

    private void TrimRedoHistory()
    {
        int maxSteps = Math.Max(1, AppSettings.MaxUndoSteps);
        while (redoList.Count > 0 && (redoList.Count > maxSteps || totalRedoBytes > MaxHistoryBytes))
        {
            var oldest = redoList.First!.Value;
            totalRedoBytes -= oldest.byteCost;
            DisposeFrameList(oldest.frames);
            redoList.RemoveFirst();
        }
    }

    private void ClearRedo()
    {
        foreach (var item in redoList)
        {
            DisposeFrameList(item.frames);
        }
        redoList.Clear();
        totalRedoBytes = 0;
    }

    public void SaveState(Canvas canvas)
    {
        var (snapshot, cost) = CreateSnapshot(canvas);
        undoList.AddLast((snapshot, canvas.ActiveFrameIndex, cost));
        totalUndoBytes += cost;

        TrimUndoHistory();
        ClearRedo();

        OnStateChanged?.Invoke();
    }

    public void Undo(Canvas canvas)
    {
        if (undoList.Count == 0) return;

        var (currentSnapshot, currentCost) = CreateSnapshot(canvas);
        redoList.AddLast((currentSnapshot, canvas.ActiveFrameIndex, currentCost));
        totalRedoBytes += currentCost;
        TrimRedoHistory();

        var previous = undoList.Last!.Value;
        undoList.RemoveLast();
        totalUndoBytes -= previous.byteCost;

        RestoreSnapshotToCanvas(canvas, previous.frames, previous.activeFrame);
        DisposeFrameList(previous.frames);

        OnStateChanged?.Invoke();
    }

    public void Redo(Canvas canvas)
    {
        if (redoList.Count == 0) return;

        var (currentSnapshot, currentCost) = CreateSnapshot(canvas);
        undoList.AddLast((currentSnapshot, canvas.ActiveFrameIndex, currentCost));
        totalUndoBytes += currentCost;
        TrimUndoHistory();

        var next = redoList.Last!.Value;
        redoList.RemoveLast();
        totalRedoBytes -= next.byteCost;

        RestoreSnapshotToCanvas(canvas, next.frames, next.activeFrame);
        DisposeFrameList(next.frames);

        OnStateChanged?.Invoke();
    }

    public void ClearAll()
    {
        foreach (var item in undoList) DisposeFrameList(item.frames);
        undoList.Clear();
        totalUndoBytes = 0;

        ClearRedo();
    }
}