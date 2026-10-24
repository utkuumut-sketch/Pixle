using System;
using System.Collections.Generic;

namespace PixelArtApp.Core;

public class HistoryManager
{
    private Stack<(List<Frame> frames, int activeFrame)> undoStack = new();
    private Stack<(List<Frame> frames, int activeFrame)> redoStack = new();

    public void SaveState(Canvas canvas)
    {
        var snapshot = new List<Frame>();
        foreach (var f in canvas.Frames) snapshot.Add(f.Clone());

        undoStack.Push((snapshot, canvas.ActiveFrameIndex));
        redoStack.Clear(); 
    }

    public void Undo(Canvas canvas)
    {
        if (undoStack.Count == 0) return;
        
        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        redoStack.Push((currentSnapshot, canvas.ActiveFrameIndex));

        var previous = undoStack.Pop();
        canvas.Frames.Clear();
        foreach (var f in previous.frames) canvas.Frames.Add(f.Clone());
        canvas.ActiveFrameIndex = previous.activeFrame;
    }

    public void Redo(Canvas canvas)
    {
        if (redoStack.Count == 0) return;

        var currentSnapshot = new List<Frame>();
        foreach (var f in canvas.Frames) currentSnapshot.Add(f.Clone());
        undoStack.Push((currentSnapshot, canvas.ActiveFrameIndex));

        var next = redoStack.Pop();
        canvas.Frames.Clear();
        foreach (var f in next.frames) canvas.Frames.Add(f.Clone());
        canvas.ActiveFrameIndex = next.activeFrame;
    }
}