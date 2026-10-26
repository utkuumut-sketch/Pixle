using System;
using System.Collections.Generic;

namespace PixelArtApp.Core;

public class HistoryManager
{
    private LinkedList<(List<Frame> frames, int activeFrame)> undoList = new();
    private Stack<(List<Frame> frames, int activeFrame)> redoStack = new();

    private void DisposeFrameList(List<Frame> list)
    {
        foreach (var f in list) f.Dispose();
    }

    public void SaveState(Canvas canvas)
    {
        var snapshot = new List<Frame>();
        foreach (var f in canvas.Frames) snapshot.Add(f.Clone());

        undoList.AddLast((snapshot, canvas.ActiveFrameIndex));
        
        while (undoList.Count > AppSettings.MaxUndoSteps && undoList.Count > 1)
        {
            DisposeFrameList(undoList.First!.Value.frames);
            undoList.RemoveFirst();
        }

        while (redoStack.Count > 0)
        {
            var popped = redoStack.Pop();
            DisposeFrameList(popped.frames);
        }
    }

    public void Undo(Canvas canvas)
    {
        if (undoList.Count == 0) return;
        
        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        redoStack.Push((currentSnapshot, canvas.ActiveFrameIndex));

        var previous = undoList.Last!.Value;
        undoList.RemoveLast();

        DisposeFrameList(canvas.Frames);
        canvas.Frames.Clear();
        foreach (var f in previous.frames) canvas.Frames.Add(f.Clone());
        DisposeFrameList(previous.frames);

        canvas.ActiveFrameIndex = Math.Clamp(previous.activeFrame, 0, canvas.Frames.Count - 1);
    }

    public void Redo(Canvas canvas)
    {
        if (redoStack.Count == 0) return;

        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        undoList.AddLast((currentSnapshot, canvas.ActiveFrameIndex));

        var next = redoStack.Pop();

        DisposeFrameList(canvas.Frames);
        canvas.Frames.Clear();
        foreach (var f in next.frames) canvas.Frames.Add(f.Clone());
        DisposeFrameList(next.frames);

        canvas.ActiveFrameIndex = Math.Clamp(next.activeFrame, 0, canvas.Frames.Count - 1);
    }

    public void ClearAll()
    {
        foreach (var item in undoList) DisposeFrameList(item.frames);
        undoList.Clear();
        while (redoStack.Count > 0) DisposeFrameList(redoStack.Pop().frames);
    }
}