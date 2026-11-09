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
            ? new Color((byte)Math.Min(255, baseCol.R + 28), (byte)Math.Min(255, baseCol.G + 28), (byte)Math.Min(255, baseCol.B + 28), baseCol.A)
            : baseCol;

        Raylib.DrawRectangleRounded(rect, 0.25f, 6, col);
        Raylib.DrawRectangleRoundedLines(rect, 0.25f, 6, borderCol ?? (hover ? Color.White : PixelSkin.BorderOutline));

        int tw = Raylib.MeasureText(text, fontSize);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)(rect.Y + (rect.Height - fontSize) / 2), fontSize, Color.White);

        return hover && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public static void DrawToggleBtn(Rectangle rect, bool state, string label)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);

        Color bg = state
            ? (hover ? new Color((byte)42, (byte)86, (byte)68, (byte)255) : new Color((byte)34, (byte)72, (byte)56, (byte)255))
            : (hover ? new Color((byte)50, (byte)53, (byte)64, (byte)255) : new Color((byte)38, (byte)40, (byte)48, (byte)255));

        Color border = state ? PixelSkin.AccentPrimary : (hover ? PixelSkin.TextMuted : PixelSkin.BorderOutline);

        Raylib.DrawRectangleRounded(rect, 0.3f, 6, bg);
        Raylib.DrawRectangleRoundedLines(rect, 0.3f, 6, border);

        // Modern kayan anahtar (switch) gostergesi
        float swW = 26f;
        float swH = 14f;
        float swX = rect.X + 8f;
        float swY = rect.Y + (rect.Height - swH) / 2f;
        Rectangle track = new Rectangle(swX, swY, swW, swH);
        Raylib.DrawRectangleRounded(track, 0.5f, 6, state ? new Color((byte)22, (byte)48, (byte)36, (byte)255) : new Color((byte)22, (byte)24, (byte)30, (byte)255));

        float knobSize = 10f;
        float knobX = state ? (swX + swW - knobSize - 2f) : (swX + 2f);
        Rectangle knob = new Rectangle(knobX, swY + 2f, knobSize, knobSize);
        Raylib.DrawRectangleRounded(knob, 0.5f, 4, state ? PixelSkin.AccentHover : PixelSkin.TextMuted);

        int tw = Raylib.MeasureText(label, 11);
        float textAreaX = swX + swW + 6f;
        float textAreaW = rect.Width - (swW + 16f);
        Raylib.DrawText(
            label,
            (int)(textAreaX + Math.Max(0, (textAreaW - tw) / 2f)),
            (int)(rect.Y + (rect.Height - 11) / 2f),
            11,
            state ? Color.White : PixelSkin.TextPrimary
        );
    }

    public static void DrawStepper(float x, float y, string valueText, Vector2 mousePos, bool enabled)
    {
        Rectangle minusRect = new Rectangle(x, y, 28, 26);
        Rectangle valRect = new Rectangle(x + 31, y, 68, 26);
        Rectangle plusRect = new Rectangle(x + 102, y, 28, 26);

        PixelSkin.DrawInsetBox(valRect);

        if (enabled)
        {
            PixelSkin.DrawButton(minusRect, Raylib.CheckCollisionPointRec(mousePos, minusRect), false);
            PixelSkin.DrawButton(plusRect, Raylib.CheckCollisionPointRec(mousePos, plusRect), false);
        }
        else
        {
            Raylib.DrawRectangleRounded(minusRect, 0.25f, 6, PixelSkin.InsetDark);
            Raylib.DrawRectangleRounded(plusRect, 0.25f, 6, PixelSkin.InsetDark);
        }

        Color symbolCol = enabled ? Color.White : Color.DarkGray;
        Color valCol = enabled ? PixelSkin.AccentGold : Color.DarkGray;

        Raylib.DrawText("-", (int)minusRect.X + 10, (int)minusRect.Y + 6, 15, symbolCol);
        int tw = Raylib.MeasureText(valueText, 12);
        Raylib.DrawText(valueText, (int)(valRect.X + (valRect.Width - tw) / 2), (int)y + 7, 12, valCol);
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
