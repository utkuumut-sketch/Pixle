using System;
using System.Collections.Generic;

namespace PixelArtApp.Core;

public class HistoryManager
{
    private LinkedList<(List<Frame> frames, int activeFrame)> undoList = new();
    private Stack<(List<Frame> frames, int activeFrame)> redoStack = new();

    public void SaveState(Canvas canvas)
    {
        var snapshot = new List<Frame>();
        foreach (var f in canvas.Frames) snapshot.Add(f.Clone());

        undoList.AddLast((snapshot, canvas.ActiveFrameIndex));
        
        // Kritik Hata Düzeltmesi: MaxUndoSteps sınırı aşılırsa en eski kaydı sil
        while (undoList.Count > AppSettings.MaxUndoSteps && undoList.Count > 1)
        {
            undoList.RemoveFirst();
        }

        redoStack.Clear(); 
    }

    public void Undo(Canvas canvas)
    {
        if (undoList.Count == 0) return;
        
        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        redoStack.Push((currentSnapshot, canvas.ActiveFrameIndex));

        var previous = undoList.Last!.Value;
        undoList.RemoveLast();

        canvas.Frames.Clear();
        foreach (var f in previous.frames) canvas.Frames.Add(f.Clone());
        canvas.ActiveFrameIndex = Math.Clamp(previous.activeFrame, 0, canvas.Frames.Count - 1);
    }

    public void Redo(Canvas canvas)
    {
        if (redoStack.Count == 0) return;

        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        undoList.AddLast((currentSnapshot, canvas.ActiveFrameIndex));

        var next = redoStack.Pop();
        canvas.Frames.Clear();
        foreach (var f in next.frames) canvas.Frames.Add(f.Clone());
        canvas.ActiveFrameIndex = Math.Clamp(next.activeFrame, 0, canvas.Frames.Count - 1);
    }
}