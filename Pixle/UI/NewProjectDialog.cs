using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public enum BgOption
{
    Transparent,
    White,
    Black
}

public class NewProjectDialog
{
    public bool IsOpen { get; private set; } = false;

    private string widthText = "32";
    private string heightText = "32";
    private int activeInput = 0; // 0: Yok, 1: Width, 2: Height
    private BgOption selectedBg = BgOption.Transparent;

    private Rectangle windowRect = new Rectangle(0, 0, 360, 310);

    public void Open()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;
        activeInput = 1;
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        activeInput = 0;
    }

    public bool Update(Vector2 mousePos, Action<int, int, Color> onConfirm)
    {
        if (!IsOpen) return false;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        Rectangle wBox = new Rectangle(windowRect.X + 110, windowRect.Y + 65, 90, 28);
        Rectangle hBox = new Rectangle(windowRect.X + 110, windowRect.Y + 103, 90, 28);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, wBox)) activeInput = 1;
            else if (Raylib.CheckCollisionPointRec(mousePos, hBox)) activeInput = 2;
            else activeInput = 0;

            // Hazır Boyut Şablonları
            int[] presets = { 16, 32, 64, 128 };
            for (int i = 0; i < presets.Length; i++)
            {
                Rectangle pRect = new Rectangle(windowRect.X + 215 + (i % 2) * 62, windowRect.Y + 65 + (i / 2) * 34, 56, 28);
                if (Raylib.CheckCollisionPointRec(mousePos, pRect))
                {
                    widthText = presets[i].ToString();
                    heightText = presets[i].ToString();
                }
            }

            // Background Seçenekleri
            for (int i = 0; i < 3; i++)
            {
                Rectangle bgRect = new Rectangle(windowRect.X + 25 + i * 105, windowRect.Y + 175, 95, 58);
                if (Raylib.CheckCollisionPointRec(mousePos, bgRect))
                {
                    selectedBg = (BgOption)i;
                }
            }

            // OK ve Cancel Butonları
            Rectangle okBtn = new Rectangle(windowRect.X + 65, windowRect.Y + 255, 105, 34);
            Rectangle cancelBtn = new Rectangle(windowRect.X + 190, windowRect.Y + 255, 105, 34);

            if (Raylib.CheckCollisionPointRec(mousePos, okBtn))
            {
                ConfirmCreation(onConfirm);
                return true;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, cancelBtn))
            {
                Close();
                return true;
            }
        }

        // Klavyeden Rakam Girişi
        if (activeInput != 0)
        {
            int key = Raylib.GetCharPressed();
            while (key > 0)
            {
                if (char.IsDigit((char)key))
                {
                    ref string target = ref (activeInput == 1 ? ref widthText : ref heightText);
                    if (target.Length < 3) target += (char)key;
                }
                key = Raylib.GetCharPressed();
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
            {
                ref string target = ref (activeInput == 1 ? ref widthText : ref heightText);
                if (target.Length > 0) target = target[..^1];
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Tab))
            {
                activeInput = activeInput == 1 ? 2 : 1;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            ConfirmCreation(onConfirm);
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            Close();
        }

        return true;
    }

    private void ConfirmCreation(Action<int, int, Color> onConfirm)
    {
        int w = int.TryParse(widthText, out int pw) ? Math.Clamp(pw, 1, 256) : 32;
        int h = int.TryParse(heightText, out int ph) ? Math.Clamp(ph, 1, 256) : 32;

        Color bg = selectedBg switch
        {
            BgOption.White => Color.White,
            BgOption.Black => Color.Black,
            _ => Canvas.TransparentColor
        };

        onConfirm(w, h, bg);
        Close();
    }

    public void Draw()
    {
        if (!IsOpen) return;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();

        // Karartılmış Arka Plan
        Raylib.DrawRectangle(0, 0, sw, sh, new Color(0, 0, 0, 150));

        // Pencere Gövdesi
        Raylib.DrawRectangleRec(windowRect, new Color(32, 32, 36, 255));
        Raylib.DrawRectangleLinesEx(windowRect, 1, Color.SkyBlue);

        // Başlık Çubuğu
        Raylib.DrawRectangle((int)windowRect.X, (int)windowRect.Y, (int)windowRect.Width, 32, new Color(45, 45, 52, 255));
        Raylib.DrawText("Yeni Proje (New Sprite)", (int)windowRect.X + 12, (int)windowRect.Y + 8, 15, Color.RayWhite);

        // SIZE Başlığı
        Raylib.DrawText("SIZE", (int)windowRect.X + 25, (int)windowRect.Y + 44, 13, Color.SkyBlue);
        Raylib.DrawLine((int)windowRect.X + 65, (int)windowRect.Y + 50, (int)(windowRect.X + windowRect.Width - 25), (int)windowRect.Y + 50, Color.DarkGray);

        // Width & Height Kutuları
        Raylib.DrawText("Width:", (int)windowRect.X + 35, (int)windowRect.Y + 72, 14, Color.LightGray);
        DrawInputBox(new Rectangle(windowRect.X + 110, windowRect.Y + 65, 90, 28), widthText + " px", activeInput == 1);

        Raylib.DrawText("Height:", (int)windowRect.X + 35, (int)windowRect.Y + 110, 14, Color.LightGray);
        DrawInputBox(new Rectangle(windowRect.X + 110, windowRect.Y + 103, 90, 28), heightText + " px", activeInput == 2);

        // Hızlı Şablon Butonları
        Vector2 mousePos = Raylib.GetMousePosition();
        int[] presets = { 16, 32, 64, 128 };
        for (int i = 0; i < presets.Length; i++)
        {
            Rectangle pRect = new Rectangle(windowRect.X + 215 + (i % 2) * 62, windowRect.Y + 65 + (i / 2) * 34, 56, 28);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, pRect);
            Raylib.DrawRectangleRec(pRect, hover ? new Color(65, 65, 75, 255) : new Color(45, 45, 50, 255));
            Raylib.DrawRectangleLinesEx(pRect, 1, Color.Gray);
            string pTxt = $"{presets[i]}x{presets[i]}";
            int tw = Raylib.MeasureText(pTxt, 11);
            Raylib.DrawText(pTxt, (int)(pRect.X + (pRect.Width - tw) / 2), (int)pRect.Y + 8, 11, Color.LightGray);
        }

        // BACKGROUND Başlığı
        Raylib.DrawText("BACKGROUND", (int)windowRect.X + 25, (int)windowRect.Y + 150, 13, Color.SkyBlue);
        Raylib.DrawLine((int)windowRect.X + 120, (int)windowRect.Y + 156, (int)(windowRect.X + windowRect.Width - 25), (int)windowRect.Y + 156, Color.DarkGray);

        string[] bgNames = { "Transparent", "White", "Black" };
        for (int i = 0; i < 3; i++)
        {
            Rectangle bgRect = new Rectangle(windowRect.X + 25 + i * 105, windowRect.Y + 175, 95, 58);
            bool isSel = selectedBg == (BgOption)i;
            bool hover = Raylib.CheckCollisionPointRec(mousePos, bgRect);

            Raylib.DrawRectangleRec(bgRect, isSel ? new Color(55, 85, 125, 255) : (hover ? new Color(48, 48, 55, 255) : new Color(38, 38, 42, 255)));
            Raylib.DrawRectangleLinesEx(bgRect, isSel ? 2 : 1, isSel ? Color.SkyBlue : Color.Gray);

            // İkon Çizimi
            int iconX = (int)bgRect.X + 35;
            int iconY = (int)bgRect.Y + 8;
            if (i == 0)
            {
                // Damalı Şeffaf İkon
                Raylib.DrawRectangle(iconX, iconY, 12, 12, Color.LightGray);
                Raylib.DrawRectangle(iconX + 12, iconY, 12, 12, Color.Gray);
                Raylib.DrawRectangle(iconX, iconY + 12, 12, 12, Color.Gray);
                Raylib.DrawRectangle(iconX + 12, iconY + 12, 12, 12, Color.LightGray);
            }
            else if (i == 1)
            {
                Raylib.DrawRectangle(iconX, iconY, 24, 24, Color.White);
            }
            else
            {
                Raylib.DrawRectangle(iconX, iconY, 24, 24, Color.Black);
            }
            Raylib.DrawRectangleLines(iconX, iconY, 24, 24, Color.DarkGray);

            int tw = Raylib.MeasureText(bgNames[i], 11);
            Raylib.DrawText(bgNames[i], (int)(bgRect.X + (bgRect.Width - tw) / 2), (int)bgRect.Y + 39, 11, isSel ? Color.Yellow : Color.RayWhite);
        }

        // OK & CANCEL Butonları
        DrawActionBtn(new Rectangle(windowRect.X + 65, windowRect.Y + 255, 105, 34), "OK", new Color(45, 115, 70, 255), mousePos);
        DrawActionBtn(new Rectangle(windowRect.X + 190, windowRect.Y + 255, 105, 34), "Cancel", new Color(120, 45, 45, 255), mousePos);
    }

    private void DrawInputBox(Rectangle rect, string text, bool active)
    {
        Raylib.DrawRectangleRec(rect, new Color(20, 20, 24, 255));
        Raylib.DrawRectangleLinesEx(rect, active ? 2 : 1, active ? Color.SkyBlue : Color.Gray);
        
        // Eğer kutu aktifse saniyede bir yanıp sönen imleç ekle
        string displayText = text;
        if (active) 
        {
            bool showCursor = (Raylib.GetTime() * 2) % 2 < 1;
            if (showCursor) displayText += "|";
        }
        else
        {
            displayText += " px";
        }

        Raylib.DrawText(displayText, (int)rect.X + 8, (int)rect.Y + 7, 14, active ? Color.Yellow : Color.RayWhite);
    }

    private void DrawActionBtn(Rectangle rect, string text, Color baseCol, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Color col = hover ? new Color((byte)Math.Min(255, baseCol.R + 25), (byte)Math.Min(255, baseCol.G + 25), (byte)Math.Min(255, baseCol.B + 25), (byte)255) : baseCol;
        Raylib.DrawRectangleRec(rect, col);
        Raylib.DrawRectangleLinesEx(rect, 1, Color.LightGray);
        int tw = Raylib.MeasureText(text, 14);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 10, 14, Color.White);
    }
}