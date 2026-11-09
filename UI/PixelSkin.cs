using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public static class PixelSkin
{
    private static Color C(int r, int g, int b, int a = 255) => new Color((byte)r, (byte)g, (byte)b, (byte)a);

    // Aseprite esintili modern koyu/acik piksel studyo temasi
    public static Color WindowBg         => AppSettings.IsLightTheme ? C(214, 216, 222) : C(18, 19, 23);
    public static Color PanelBg          => AppSettings.IsLightTheme ? C(232, 234, 238) : C(28, 30, 36);
    public static Color ElevatedBg       => AppSettings.IsLightTheme ? C(246, 247, 250) : C(39, 42, 50);
    public static Color InsetDark        => AppSettings.IsLightTheme ? C(192, 196, 204) : C(14, 15, 19);
    public static Color BorderOutline    => AppSettings.IsLightTheme ? C(128, 134, 145) : C(12, 12, 16);
    public static Color BevelLight       => AppSettings.IsLightTheme ? C(255, 255, 255, 200) : C(62, 67, 80, 210);
    public static Color BevelShadow      => AppSettings.IsLightTheme ? C(165, 170, 180, 210) : C(16, 17, 22, 230);
    public static Color PanelBorderLight => BevelLight;
    public static Color PanelBorderDark  => BorderOutline;

    public static Color AccentPrimary    => C(95, 175, 132);
    public static Color AccentHover      => C(122, 204, 158);
    public static Color AccentBg         => AppSettings.IsLightTheme ? C(195, 232, 210) : C(36, 68, 54);
    public static Color AccentGold       => C(248, 208, 88);
    public static Color AccentCyan       => C(102, 204, 248);

    public static Color TextPrimary      => AppSettings.IsLightTheme ? C(24, 26, 30) : C(228, 230, 236);
    public static Color TextMuted        => AppSettings.IsLightTheme ? C(102, 108, 118) : C(138, 144, 158);

    public static void LoadSkin()
    {
        PixelIcons.LoadIcons();
    }

    public static void UnloadSkin()
    {
        PixelIcons.UnloadIcons();
    }

    /// <summary>
    /// Aseprite tarzinda 3B egimli (bevel) piksel buton cizer.
    /// </summary>
    public static void DrawButton(Rectangle rect, bool isHover, bool isSelected)
    {
        int x = (int)MathF.Round(rect.X);
        int y = (int)MathF.Round(rect.Y);
        int w = (int)MathF.Round(rect.Width);
        int h = (int)MathF.Round(rect.Height);

        Color bg = isSelected
            ? AccentBg
            : (isHover ? ElevatedBg : PanelBg);

        // Dis siyah/koyu piksel cerceve
        Raylib.DrawRectangle(x, y, w, h, BorderOutline);
        // Ic dolgu
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, h - 2, bg);

        if (isSelected)
        {
            // Gomulu (basili) gorunum + parlak vurgu cercevesi
            Raylib.DrawRectangle(x + 1, y + 1, w - 2, 2, C(15, 28, 22, 220));
            Raylib.DrawRectangleLinesEx(new Rectangle(x + 1, y + 1, w - 2, h - 2), 1f, AccentPrimary);
        }
        else
        {
            // Ust ve sol parlak kenar (Bevel highlight)
            Color topHighlight = isHover ? C(90, 98, 116) : BevelLight;
            Raylib.DrawRectangle(x + 1, y + 1, w - 2, 1, topHighlight);
            Raylib.DrawRectangle(x + 1, y + 2, 1, h - 4, topHighlight);

            // Alt ve sag koyu golge (Bevel shadow)
            Raylib.DrawRectangle(x + 1, y + h - 2, w - 2, 1, BevelShadow);
            Raylib.DrawRectangle(x + w - 2, y + 2, 1, h - 3, BevelShadow);
        }
    }

    public static void DrawInsetBox(Rectangle rect)
    {
        int x = (int)MathF.Round(rect.X);
        int y = (int)MathF.Round(rect.Y);
        int w = (int)MathF.Round(rect.Width);
        int h = (int)MathF.Round(rect.Height);

        Raylib.DrawRectangle(x, y, w, h, BorderOutline);
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, h - 2, InsetDark);
        // Ust gomulu golge, alt hafif parilti
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 1, C(8, 8, 12, 200));
        Raylib.DrawRectangle(x + 1, y + h - 1, w - 2, 1, BevelLight);
    }

    public static void DrawPanel(Rectangle rect)
    {
        int x = (int)MathF.Round(rect.X);
        int y = (int)MathF.Round(rect.Y);
        int w = (int)MathF.Round(rect.Width);
        int h = (int)MathF.Round(rect.Height);

        Raylib.DrawRectangle(x, y, w, h, PanelBg);
        Raylib.DrawRectangle(x + 1, y + 1, w - 2, 1, BevelLight);
        Raylib.DrawRectangleLinesEx(rect, 1f, BorderOutline);
    }

    public static void DrawTooltip(Vector2 anchorPos, string title, string? shortcut = null)
    {
        int titleW = Raylib.MeasureText(title, 12);
        int keyW = !string.IsNullOrEmpty(shortcut) ? Raylib.MeasureText(shortcut, 11) + 12 : 0;
        int totalW = titleW + keyW + 18;
        int totalH = 24;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float tx = Math.Clamp(anchorPos.X, 6f, Math.Max(6f, sw - totalW - 6f));
        float ty = Math.Clamp(anchorPos.Y, 6f, Math.Max(6f, sh - totalH - 6f));

        Rectangle rect = new Rectangle(tx, ty, totalW, totalH);
        Raylib.DrawRectangle((int)rect.X + 2, (int)rect.Y + 2, totalW, totalH, C(0, 0, 0, 140));
        Raylib.DrawRectangleRec(rect, C(20, 22, 28, 250));
        Raylib.DrawRectangleLinesEx(rect, 1f, AccentPrimary);

        Raylib.DrawText(title, (int)rect.X + 8, (int)rect.Y + 6, 12, Color.White);
        if (!string.IsNullOrEmpty(shortcut))
        {
            Rectangle badge = new Rectangle(rect.X + titleW + 12, rect.Y + 4, keyW - 4, 16);
            Raylib.DrawRectangleRec(badge, C(40, 46, 58, 255));
            Raylib.DrawRectangleLinesEx(badge, 1f, C(80, 90, 110, 255));
            Raylib.DrawText(shortcut, (int)badge.X + 4, (int)badge.Y + 3, 11, AccentGold);
        }
    }

    public static void DrawDialogWindow(Rectangle rect, string title, Vector2 mousePos, out Rectangle closeBtnRect)
    {
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), C(0, 0, 0, 165));

        Raylib.DrawRectangle((int)rect.X + 5, (int)rect.Y + 5, (int)rect.Width, (int)rect.Height, C(0, 0, 0, 120));

        DrawPanel(rect);
        Raylib.DrawRectangleLinesEx(new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), 1f, BevelLight);

        Rectangle titleBar = new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, 30);
        Raylib.DrawRectangleRec(titleBar, ElevatedBg);
        Raylib.DrawRectangle((int)titleBar.X, (int)titleBar.Y, (int)titleBar.Width, 1, BevelLight);
        Raylib.DrawLine((int)rect.X, (int)rect.Y + 31, (int)(rect.X + rect.Width), (int)rect.Y + 31, BorderOutline);

        PixelIcons.DrawIcon("app_logo", rect.X + 8, rect.Y + 8, 16f);
        Raylib.DrawText(title, (int)rect.X + 30, (int)rect.Y + 9, 14, TextPrimary);

        closeBtnRect = new Rectangle(rect.X + rect.Width - 28, rect.Y + 5, 22, 22);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeBtnRect);

        if (closeHover)
        {
            Raylib.DrawRectangleRec(closeBtnRect, C(195, 55, 60));
            Raylib.DrawRectangleLinesEx(closeBtnRect, 1f, BorderOutline);
        }

        Raylib.DrawText("X", (int)closeBtnRect.X + 7, (int)closeBtnRect.Y + 5, 14, closeHover ? Color.White : TextMuted);
    }
}
