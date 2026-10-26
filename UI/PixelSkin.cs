using System;
using System.IO;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public static class PixelSkin
{
    public static Color WindowBg      => AppSettings.IsLightTheme ? new Color(205, 205, 212, 255) : new Color(30, 30, 34, 255);    
    public static Color PanelBg       => AppSettings.IsLightTheme ? new Color(65, 65, 75, 255)     : new Color(24, 24, 28, 255);    
    public static Color BorderOutline => AppSettings.IsLightTheme ? new Color(35, 35, 42, 255)     : new Color(10, 10, 12, 255);    
    public static Color InsetDark     => AppSettings.IsLightTheme ? new Color(45, 45, 54, 255)     : new Color(18, 18, 22, 255);    

    public static Texture2D CustomButton;
    public static NPatchInfo ButtonPatch;
    public static bool IsCustomLoaded = false;

    public static void LoadSkin()
    {
        string fileName = "Button1.png";
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string fullPath = Path.Combine(exeDir, fileName);

        string targetPath = File.Exists(fileName) ? fileName : (File.Exists(fullPath) ? fullPath : "");

        if (!string.IsNullOrEmpty(targetPath))
        {
            CustomButton = Raylib.LoadTexture(targetPath);
            Raylib.SetTextureFilter(CustomButton, TextureFilter.Point);

            ButtonPatch = new NPatchInfo
            {
                Source = new Rectangle(0, 0, 28, 9),
                Left = 3, Top = 3, Right = 3, Bottom = 3,
                Layout = NPatchLayout.NinePatch
            };
            IsCustomLoaded = true;
        }
    }

    public static void UnloadSkin()
    {
        if (IsCustomLoaded)
        {
            Raylib.UnloadTexture(CustomButton);
            IsCustomLoaded = false;
        }
    }

    public static void DrawButton(Rectangle rect, bool isHover, bool isSelected)
    {
        if (isSelected) 
        {
            rect.Y += 2;
            rect.Height -= 2; 
        }
        else if (isHover)
        {
            rect.Y += 1;
            rect.Height -= 1;
        }

        if (IsCustomLoaded)
        {
            Color tint = isSelected ? new Color(120, 160, 255, 255) : (isHover ? Color.White : new Color(210, 210, 210, 255));
            Raylib.DrawTextureNPatch(CustomButton, ButtonPatch, rect, Vector2.Zero, 0f, tint);
        }
        else
        {
            Color bg = isSelected ? new Color(55, 95, 145, 255) : (isHover ? new Color(60, 60, 68, 255) : new Color(45, 45, 52, 255));
            Raylib.DrawRectangleRec(rect, bg);
            Raylib.DrawRectangleLinesEx(rect, 2, BorderOutline);
            
            // Üst kenar ince parlaklık (3D Bevel Hissi)
            if (!isSelected)
                Raylib.DrawLine((int)rect.X + 2, (int)rect.Y + 2, (int)(rect.X + rect.Width - 2), (int)rect.Y + 2, new Color(255, 255, 255, 30));
            else 
                Raylib.DrawRectangle((int)rect.X, (int)rect.Y, (int)rect.Width, 3, new Color(0, 0, 0, 80));
        }
    }

    public static void DrawInsetBox(Rectangle rect)
    {
        Raylib.DrawRectangleRec(rect, InsetDark);
        Raylib.DrawRectangleLinesEx(rect, 2, BorderOutline);
    }

    public static void DrawPanel(Rectangle rect)
    {
        Raylib.DrawRectangleRec(rect, PanelBg);
        Raylib.DrawRectangleLinesEx(rect, 2, BorderOutline);
    }
}