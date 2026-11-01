using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public enum PerformancePreset
{
    Varsayilan,
    Kalite,
    Performans,
    Ozel
}

public static class AppSettings
{
    public static PerformancePreset CurrentPreset { get; set; } = PerformancePreset.Varsayilan;

    public static int MaxUndoSteps { get; set; } = 50;
    public static bool IsLightTheme { get; set; } = false;
    
    // MEVCUT VE YENI OZELLESTIRME AYARLARI
    public static bool EyedropperAutoSwitchPen { get; set; } = true;
    public static bool ShowPanelBackdrops { get; set; } = true;
    public static bool EnableOnionSkin { get; set; } = false;
    public static bool EnablePanelResize { get; set; } = false;
    public static int LeftPanelWidth { get; set; } = 145;
    public static int RightPanelWidth { get; set; } = 180;

    // GORUNUM VE PERFORMANS / ANIMASYON AYARLARI
    public static bool EnableUIAnimations { get; set; } = true;
    public static float UIAnimationSpeed { get; set; } = 12f; // 4f (Yavas) - 24f (Cok Hizli)
    public static bool SmoothCameraZoom { get; set; } = true;
    
    // 0 = Sinirsiz FPS (Raylib'de SetTargetFPS(0) limitsiz calistirir)
    public static int TargetFPS { get; set; } = 0; 

    public static bool ShowBrushPreview { get; set; } = true;
    public static int OnionSkinOpacity { get; set; } = 100; // 25 - 225 arasi
    public static int GridOpacity { get; set; } = 45;       // 15 - 165 arasi
    public static int CheckerContrast { get; set; } = 1;    // 0: Dusuk, 1: Orta, 2: Yuksek (Standart: 1)
    public static int BlurIntensity { get; set; } = 2;      // 1 - 5 arasi
    public static int JumbleIntensity { get; set; } = 50;   // %10 - %100 arasi

    // KISAYOLLAR
    public static KeyboardKey KeyPen { get; set; } = KeyboardKey.One;
    public static KeyboardKey KeyEraser { get; set; } = KeyboardKey.Two;
    public static KeyboardKey KeyBucket { get; set; } = KeyboardKey.Three;
    public static KeyboardKey KeySmartBucket { get; set; } = KeyboardKey.Four;
    public static KeyboardKey KeyEyedropper { get; set; } = KeyboardKey.Five;
    public static KeyboardKey KeyBlur { get; set; } = KeyboardKey.R;
    public static KeyboardKey KeyJumble { get; set; } = KeyboardKey.J;
    public static KeyboardKey KeyLine { get; set; } = KeyboardKey.L;
    public static KeyboardKey KeyShape { get; set; } = KeyboardKey.U;
    public static KeyboardKey KeySelectMove { get; set; } = KeyboardKey.M;
    public static KeyboardKey KeyToggleAnimPanel { get; set; } = KeyboardKey.Up;
    public static KeyboardKey KeyToggleLayers { get; set; } = KeyboardKey.Down;

    public static KeyboardKey[] KeyColors { get; set; } = new KeyboardKey[20];

    public static void ApplyPreset(PerformancePreset preset)
    {
        CurrentPreset = preset;

        // NOT: Canvas arka plan rengi/doygunlugu (CheckerContrast), Izgara rengi ve Tema
        // hicbir performans modunda degistirilmez, standart kalir.
        // Ayrica hangi profil secilirse secilsin TargetFPS varsayilan olarak Sinirsiz (0) gelir.
        switch (preset)
        {
            case PerformancePreset.Varsayilan:
                EnableUIAnimations = true;
                UIAnimationSpeed = 12f;
                SmoothCameraZoom = true;
                ShowPanelBackdrops = true;
                ShowBrushPreview = true;
                TargetFPS = 0; // Sinirsiz
                break;

            case PerformancePreset.Kalite:
                EnableUIAnimations = true;
                UIAnimationSpeed = 16f;
                SmoothCameraZoom = true;
                ShowPanelBackdrops = true;
                ShowBrushPreview = true;
                TargetFPS = 0; // Sinirsiz
                break;

            case PerformancePreset.Performans:
                EnableUIAnimations = false;
                SmoothCameraZoom = false;
                ShowPanelBackdrops = false;
                ShowBrushPreview = false;
                TargetFPS = 0; // Sinirsiz
                break;
        }

        Raylib.SetTargetFPS(TargetFPS);
    }

    public static void MarkAsCustomPreset()
    {
        CurrentPreset = PerformancePreset.Ozel;
    }

    public static void ResetToDefaults()
    {
        LeftPanelWidth = 145;
        RightPanelWidth = 180;
        MaxUndoSteps = 50;
        IsLightTheme = false;
        EyedropperAutoSwitchPen = true;
        EnableOnionSkin = false;
        EnablePanelResize = false;

        CheckerContrast = 1;
        GridOpacity = 45;
        OnionSkinOpacity = 100;
        BlurIntensity = 2;
        JumbleIntensity = 50;

        ApplyPreset(PerformancePreset.Varsayilan);

        KeyPen = KeyboardKey.One;
        KeyEraser = KeyboardKey.Two;
        KeyBucket = KeyboardKey.Three;
        KeySmartBucket = KeyboardKey.Four;
        KeyEyedropper = KeyboardKey.Five;
        KeyBlur = KeyboardKey.R;
        KeyJumble = KeyboardKey.J;
        KeyLine = KeyboardKey.L;
        KeyShape = KeyboardKey.U;
        KeySelectMove = KeyboardKey.M;
        KeyToggleAnimPanel = KeyboardKey.Up;
        KeyToggleLayers = KeyboardKey.Down;
        
        for (int i = 0; i < KeyColors.Length; i++)
        {
            KeyColors[i] = KeyboardKey.Null;
        }
    }

    public static Color BgColor => IsLightTheme ? new Color(210, 210, 215, 255) : new Color(22, 22, 22, 255);
    public static Color PanelColor => IsLightTheme ? new Color(235, 235, 240, 255) : new Color(30, 30, 30, 255);

    public static string KeyToString(KeyboardKey key)
    {
        if (key == KeyboardKey.Null) return "Atanmadi";
        return key switch
        {
            KeyboardKey.One => "1", KeyboardKey.Two => "2", KeyboardKey.Three => "3",
            KeyboardKey.Four => "4", KeyboardKey.Five => "5", KeyboardKey.Six => "6",
            KeyboardKey.Seven => "7", KeyboardKey.Eight => "8", KeyboardKey.Nine => "9",
            KeyboardKey.Zero => "0", KeyboardKey.Up => "Yukari Ok", KeyboardKey.Down => "Asagi Ok",
            KeyboardKey.Left => "Sol Ok", KeyboardKey.Right => "Sag Ok",
            _ => key.ToString()
        };
    }
}