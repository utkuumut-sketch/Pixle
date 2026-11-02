// === DOSYA: \UI\UIWidgets.cs ===
using System;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public static class UIWidgets
{
    public static bool DrawColorButton(Rectangle rect, string text, Color baseCol, Vector2 mousePos, int fontSize = 13, Color? borderCol = null)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Color col = hover
            ? new Color((byte)Math.Min(255, baseCol.R + 25), (byte)Math.Min(255, baseCol.G + 25), (byte)Math.Min(255, baseCol.B + 25), baseCol.A)
            : baseCol;

        Raylib.DrawRectangleRec(rect, col);
        Raylib.DrawRectangleLinesEx(rect, 1, borderCol ?? Color.Gray);

        int tw = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)(rect.Y + (rect.Height - fontSize) / 2), fontSize, Color.White);

        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public static void DrawToggleBtn(Rectangle rect, bool state, string label)
    {
        Raylib.DrawRectangleRec(rect, state ? new Color(45, 100, 65, 255) : new Color(95, 45, 45, 255));
        Raylib.DrawRectangleLinesEx(rect, 1, Color.Gray);
        int tw = Raylib.MeasureText(label, 12);
        Raylib.DrawText(label, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 7, 12, Color.White);
    }

    public static void DrawStepper(float x, float y, string valueText, Vector2 mousePos, bool enabled)
    {
        Rectangle minusRect = new Rectangle(x, y, 28, 26);
        Rectangle plusRect = new Rectangle(x + 102, y, 28, 26);

        Color btnBgMinus = !enabled
            ? new Color(28, 28, 32, 255)
            : (Raylib.CheckCollisionPointRec(mousePos, minusRect) ? new Color(65, 65, 75, 255) : new Color(45, 45, 52, 255));

        Color btnBgPlus = !enabled
            ? new Color(28, 28, 32, 255)
            : (Raylib.CheckCollisionPointRec(mousePos, plusRect) ? new Color(65, 65, 75, 255) : new Color(45, 45, 52, 255));

        Color borderCol = enabled ? Color.Gray : new Color(55, 55, 60, 255);
        Color symbolCol = enabled ? Color.White : Color.DarkGray;
        Color valCol = enabled ? Color.Yellow : Color.DarkGray;

        Raylib.DrawRectangleRec(minusRect, btnBgMinus);
        Raylib.DrawRectangleLinesEx(minusRect, 1, borderCol);
        Raylib.DrawText("-", (int)minusRect.X + 10, (int)minusRect.Y + 6, 15, symbolCol);

        int tw = Raylib.MeasureText(valueText, 13);
        Raylib.DrawText(valueText, (int)(x + 28 + (74 - tw) / 2), (int)y + 7, 13, valCol);

        Raylib.DrawRectangleRec(plusRect, btnBgPlus);
        Raylib.DrawRectangleLinesEx(plusRect, 1, borderCol);
        Raylib.DrawText("+", (int)plusRect.X + 9, (int)plusRect.Y + 6, 15, symbolCol);
    }

    public static void DrawCheckerboard(Rectangle rect, int cellSize, Color c1, Color c2)
    {
        int w = (int)rect.Width;
        int h = (int)rect.Height;
        for (int x = 0; x < w; x += cellSize)
        {
            for (int y = 0; y < h; y += cellSize)
            {
                Color c = ((x / cellSize + y / cellSize) % 2 == 0) ? c1 : c2;
                Raylib.DrawRectangle((int)rect.X + x, (int)rect.Y + y, Math.Min(cellSize, w - x), Math.Min(cellSize, h - y), c);
            }
        }
    }
}