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

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        resultCallback = null;
    }

    public bool Update(Vector2 mousePos)
    {
        if (!IsOpen) return false;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        Rectangle btnSave = new Rectangle(windowRect.X + 20, windowRect.Y + 120, 105, 34);
        Rectangle btnDiscard = new Rectangle(windowRect.X + 135, windowRect.Y + 120, 165, 34);
        Rectangle btnCancel = new Rectangle(windowRect.X + 310, windowRect.Y + 120, 100, 34);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, btnSave))
            {
                var cb = resultCallback;
                Close();
                cb?.Invoke(UnsavedDialogResult.Save);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, btnDiscard))
            {
                var cb = resultCallback;
                Close();
                cb?.Invoke(UnsavedDialogResult.Discard);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, btnCancel))
            {
                var cb = resultCallback;
                Close();
                cb?.Invoke(UnsavedDialogResult.Cancel);
                return true;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            var cb = resultCallback;
            Close();
            cb?.Invoke(UnsavedDialogResult.Cancel);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            var cb = resultCallback;
            Close();
            cb?.Invoke(UnsavedDialogResult.Save);
        }

        return true;
    }

    public void Draw()
    {
        if (!IsOpen) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mousePos = Raylib.GetMousePosition();

        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 160));
        Raylib.DrawRectangleRec(windowRect, new Color(32, 32, 36, 255));
        Raylib.DrawRectangleLinesEx(windowRect, 1, Color.SkyBlue);

        Raylib.DrawRectangle((int)windowRect.X, (int)windowRect.Y, (int)windowRect.Width, 32, new Color(45, 45, 52, 255));
        Raylib.DrawText("Kaydedilmemis Degisiklikler", (int)windowRect.X + 12, (int)windowRect.Y + 8, 15, Color.Yellow);

        Raylib.DrawText($"'{projectTitle}' projesinde kaydedilmemis", (int)windowRect.X + 20, (int)windowRect.Y + 52, 14, Color.RayWhite);
        Raylib.DrawText("degisiklikler var. Kapatmadan once kaydedilsin mi?", (int)windowRect.X + 20, (int)windowRect.Y + 74, 14, Color.LightGray);

        DrawBtn(new Rectangle(windowRect.X + 20, windowRect.Y + 120, 105, 34), "Kaydet", new Color(45, 115, 70, 255), mousePos);
        DrawBtn(new Rectangle(windowRect.X + 135, windowRect.Y + 120, 165, 34), "Kaydetmeden Kapat", new Color(135, 45, 45, 255), mousePos);
        DrawBtn(new Rectangle(windowRect.X + 310, windowRect.Y + 120, 100, 34), "Iptal", new Color(65, 65, 75, 255), mousePos);
    }

    private void DrawBtn(Rectangle rect, string text, Color baseCol, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Color col = hover
            ? new Color((byte)Math.Min(255, baseCol.R + 25), (byte)Math.Min(255, baseCol.G + 25), (byte)Math.Min(255, baseCol.B + 25), (byte)255)
            : baseCol;

        Raylib.DrawRectangleRec(rect, col);
        Raylib.DrawRectangleLinesEx(rect, 1, Color.LightGray);
        int tw = Raylib.MeasureText(text, 13);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 10, 13, Color.White);
    }
}