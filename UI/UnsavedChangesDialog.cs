// === DOSYA: \UI\UnsavedChangesDialog.cs ===
using System;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public enum UnsavedDialogResult
{
    Save,
    Discard,
    Cancel
}

public class UnsavedChangesDialog
{
    public bool IsOpen { get; private set; } = false;
    private string projectTitle = "";
    private Action<UnsavedDialogResult>? resultCallback;
    private Rectangle windowRect = new Rectangle(0, 0, 430, 175);

    public void Open(string title, Action<UnsavedDialogResult> onResult)
    {
        projectTitle = title;
        resultCallback = onResult;
        windowRect.X = (Raylib.GetScreenWidth() - windowRect.Width) / 2f;
        windowRect.Y = (Raylib.GetScreenHeight() - windowRect.Height) / 2f;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        resultCallback = null;
    }

    private void TriggerResult(UnsavedDialogResult res)
    {
        var cb = resultCallback;
        Close();
        cb?.Invoke(res);
    }

    public bool Update(Vector2 mousePos)
    {
        if (!IsOpen) return false;

        windowRect.X = (Raylib.GetScreenWidth() - windowRect.Width) / 2f;
        windowRect.Y = (Raylib.GetScreenHeight() - windowRect.Height) / 2f;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(windowRect.X + 20, windowRect.Y + 120, 105, 34)))
            { TriggerResult(UnsavedDialogResult.Save); return true; }

            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(windowRect.X + 135, windowRect.Y + 120, 165, 34)))
            { TriggerResult(UnsavedDialogResult.Discard); return true; }

            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(windowRect.X + 310, windowRect.Y + 120, 100, 34)))
            { TriggerResult(UnsavedDialogResult.Cancel); return true; }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) TriggerResult(UnsavedDialogResult.Cancel);
        else if (Raylib.IsKeyPressed(KeyboardKey.Enter)) TriggerResult(UnsavedDialogResult.Save);

        return true;
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Vector2 mousePos = Raylib.GetMousePosition();
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 160));
        Raylib.DrawRectangleRec(windowRect, new Color(32, 32, 36, 255));
        Raylib.DrawRectangleLinesEx(windowRect, 1, Color.SkyBlue);

        Raylib.DrawRectangle((int)windowRect.X, (int)windowRect.Y, (int)windowRect.Width, 32, new Color(45, 45, 52, 255));
        Raylib.DrawText("Kaydedilmemis Degisiklikler", (int)windowRect.X + 12, (int)windowRect.Y + 8, 15, Color.Yellow);

        Raylib.DrawText($"'{projectTitle}' projesinde kaydedilmemis", (int)windowRect.X + 20, (int)windowRect.Y + 52, 14, Color.RayWhite);
        Raylib.DrawText("degisiklikler var. Kapatmadan once kaydedilsin mi?", (int)windowRect.X + 20, (int)windowRect.Y + 74, 14, Color.LightGray);

        UIWidgets.DrawColorButton(new Rectangle(windowRect.X + 20, windowRect.Y + 120, 105, 34), "Kaydet", new Color(45, 115, 70, 255), mousePos, 13, Color.LightGray);
        UIWidgets.DrawColorButton(new Rectangle(windowRect.X + 135, windowRect.Y + 120, 165, 34), "Kaydetmeden Kapat", new Color(135, 45, 45, 255), mousePos, 13, Color.LightGray);
        UIWidgets.DrawColorButton(new Rectangle(windowRect.X + 310, windowRect.Y + 120, 100, 34), "Iptal", new Color(65, 65, 75, 255), mousePos, 13, Color.LightGray);
    }
}