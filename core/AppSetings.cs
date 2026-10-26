using System;
using Raylib_cs;

namespace PixelArtApp.Core;

public static class AppSettings
{
    public static int MaxUndoSteps { get; set; } = 50;
    public static bool IsLightTheme { get; set; } = false;
    
    // YENİ AYARLAR
    public static bool EyedropperAutoSwitchPen { get; set; } = true;
    public static bool ShowPanelBackdrops { get; set; } = true;

    public static KeyboardKey KeyPen { get; set; } = KeyboardKey.One;
    public static KeyboardKey KeyEraser { get; set; } = KeyboardKey.Two;
    public static KeyboardKey KeyBucket { get; set; } = KeyboardKey.Three;
    public static KeyboardKey KeySmartBucket { get; set; } = KeyboardKey.Four;
    public static KeyboardKey KeyEyedropper { get; set; } = KeyboardKey.Five;
    public static KeyboardKey KeyLine { get; set; } = KeyboardKey.L;
    public static KeyboardKey KeyShape { get; set; } = KeyboardKey.U;
    public static KeyboardKey KeySelectMove { get; set; } = KeyboardKey.M;
    public static KeyboardKey KeyToggleLayers { get; set; } = KeyboardKey.Up;

    public static KeyboardKey[] KeyColors { get; set; } = new KeyboardKey[20];

    public static void ResetToDefaults()
    {
        MaxUndoSteps = 50;
        IsLightTheme = false;
        EyedropperAutoSwitchPen = true;
        ShowPanelBackdrops = true;

        KeyPen = KeyboardKey.One;
        KeyEraser = KeyboardKey.Two;
        KeyBucket = KeyboardKey.Three;
        KeySmartBucket = KeyboardKey.Four;
        KeyEyedropper = KeyboardKey.Five;
        KeyLine = KeyboardKey.L;
        KeyShape = KeyboardKey.U;
        KeySelectMove = KeyboardKey.M;
        KeyToggleLayers = KeyboardKey.Up;
        
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