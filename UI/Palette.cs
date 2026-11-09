using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core.Palettes;

namespace PixelArtApp.UI;

public class Palette
{
    public Color CurrentColor { get; set; } = Color.Black;
    public int SelectedColorIndex { get; private set; } = 0;
    public int CurrentPresetIndex { get; private set; } = 0;
    public string CurrentPresetName { get; private set; } = "Varsayilan (20)";
    public Vector2 Offset { get; set; }

    private readonly List<Color> colors = new();
    private readonly int boxSize = 23;
    private readonly int spacing = 5;
    private readonly int columns = 5;

    private float scrollY = 0f;
    private bool isDraggingScrollbar = false;
    private float scrollDragStartMouseY = 0f;
    private float scrollDragStartScrollY = 0f;

    private readonly float viewportWidth = 148f;
    public float ViewportHeight { get; set; } = 242f;

    public Rectangle ActiveColorBoxBounds => new Rectangle(Offset.X, Offset.Y, 44, 44);
    private Rectangle PresetPrevBtnBounds => new Rectangle(Offset.X, Offset.Y + 49, 20, 20);
    private Rectangle PresetNameBtnBounds => new Rectangle(Offset.X + 22, Offset.Y + 49, 104, 20);
    private Rectangle PresetNextBtnBounds => new Rectangle(Offset.X + 128, Offset.Y + 49, 20, 20);
    private Rectangle ViewportBounds => new Rectangle(Offset.X, Offset.Y + 73, viewportWidth, ViewportHeight);

    public Palette(Vector2 offset)
    {
        Offset = offset;
        ApplyPreset(0);
    }

    public void ApplyPreset(int presetIndex)
    {
        if (PalettePresets.BuiltIn.Count == 0) return;
        CurrentPresetIndex = ((presetIndex % PalettePresets.BuiltIn.Count) + PalettePresets.BuiltIn.Count) % PalettePresets.BuiltIn.Count;
        var preset = PalettePresets.BuiltIn[CurrentPresetIndex];
        CurrentPresetName = preset.Name;

        colors.Clear();
        colors.AddRange(preset.Colors);
        if (colors.Count > 0)
        {
            CurrentColor = colors[0];
            SelectedColorIndex = 0;
        }
        else
        {
            SelectedColorIndex = -1;
        }
        scrollY = 0f;
    }

    public void CyclePreset(int dir)
    {
        ApplyPreset(CurrentPresetIndex + dir);
    }

    public void LoadCustomPalette(IEnumerable<Color> loadedColors, string name)
    {
        colors.Clear();
        colors.AddRange(loadedColors);
        CurrentPresetName = string.IsNullOrWhiteSpace(name) ? $"Ozel ({colors.Count})" : name;
        if (colors.Count > 0)
        {
            CurrentColor = colors[0];
            SelectedColorIndex = 0;
        }
        else
        {
            SelectedColorIndex = -1;
        }
        scrollY = 0f;
    }

    public void AddColor(Color newColor)
    {
        int existingIdx = colors.FindIndex(c => ColorsEqual(c, newColor));
        if (existingIdx < 0)
        {
            colors.Add(newColor);
            SelectedColorIndex = colors.Count - 1;
        }
        else
        {
            SelectedColorIndex = existingIdx;
        }
        CurrentColor = newColor;

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - ViewportHeight);
        scrollY = maxScroll;
    }

    public void SetColor(Color newColor)
    {
        CurrentColor = newColor;
        SelectedColorIndex = colors.FindIndex(c => ColorsEqual(c, newColor));
    }

    public bool SelectColor(int index)
    {
        if (index >= 0 && index < colors.Count)
        {
            CurrentColor = colors[index];
            SelectedColorIndex = index;
            return true;
        }
        return false;
    }

    public IReadOnlyList<Color> GetColors() => colors;

    public void SetColors(IEnumerable<Color> loadedColors)
    {
        colors.Clear();
        colors.AddRange(loadedColors);
        if (colors.Count > 0)
        {
            CurrentColor = colors[0];
            SelectedColorIndex = 0;
        }
        else
        {
            SelectedColorIndex = -1;
        }
        scrollY = 0f;
    }

    private static bool ColorsEqual(Color a, Color b) =>
        a.R == b.R && a.G == b.G && a.B == b.B && a.A == b.A;

    /// <summary>
    /// Rengin algilanan parlakligina (luminance) gore okunabilir Siyah veya Beyaz yazi rengi dondurur.
    /// </summary>
    private static Color GetContrastTextColor(Color bg)
    {
        if (bg.A < 110) return Color.White;
        int lum = (bg.R * 299 + bg.G * 587 + bg.B * 114) / 1000;
        return lum >= 145 ? new Color((byte)16, (byte)18, (byte)24, (byte)255) : Color.White;
    }

    private float GetTotalContentHeight()
    {
        // +1: Sonda yer alan "+ renk ekle" kutucuğu için
        int totalItems = colors.Count + 1;
        int totalRows = (int)Math.Ceiling(totalItems / (float)columns);
        return totalRows * (boxSize + spacing) + 8;
    }

    public bool Update(
        Vector2 mousePos,
        Action onOpenColorPicker,
        Action<string>? onPresetChanged = null,
        Action? onRequestImportPalette = null)
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, ActiveColorBoxBounds))
            {
                onOpenColorPicker();
                return true;
            }

            if (Raylib.CheckCollisionPointRec(mousePos, PresetPrevBtnBounds))
            {
                CyclePreset(-1);
                onPresetChanged?.Invoke(CurrentPresetName);
                return true;
            }

            if (Raylib.CheckCollisionPointRec(mousePos, PresetNameBtnBounds) ||
                Raylib.CheckCollisionPointRec(mousePos, PresetNextBtnBounds))
            {
                CyclePreset(1);
                onPresetChanged?.Invoke(CurrentPresetName);
                return true;
            }
        }
        else if (Raylib.IsMouseButtonPressed(MouseButton.Right) &&
                 Raylib.CheckCollisionPointRec(mousePos, PresetNameBtnBounds))
        {
            onRequestImportPalette?.Invoke();
            return true;
        }

        Rectangle vp = ViewportBounds;
        bool isInsideViewport = Raylib.CheckCollisionPointRec(mousePos, vp);

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - ViewportHeight);
        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        if (isInsideViewport)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                scrollY -= wheel * (boxSize + spacing);
                scrollY = Math.Clamp(scrollY, 0, maxScroll);
            }
        }

        if (maxScroll > 0)
        {
            float trackX = vp.X + vp.Width - 8;
            float trackHeight = vp.Height;
            float thumbHeight = Math.Max(24f, (vp.Height / totalHeight) * trackHeight);
            float thumbY = vp.Y + (scrollY / maxScroll) * (trackHeight - thumbHeight);
            Rectangle thumbRect = new Rectangle(trackX, thumbY, 6, thumbHeight);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, thumbRect))
            {
                isDraggingScrollbar = true;
                scrollDragStartMouseY = mousePos.Y;
                scrollDragStartScrollY = scrollY;
            }

            if (isDraggingScrollbar)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    float deltaY = mousePos.Y - scrollDragStartMouseY;
                    float scrollRatio = deltaY / (trackHeight - thumbHeight);
                    scrollY = Math.Clamp(scrollDragStartScrollY + scrollRatio * maxScroll, 0, maxScroll);
                }
                else
                {
                    isDraggingScrollbar = false;
                }
                return true;
            }
        }

        if (isInsideViewport && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            float startX = vp.X + 7;
            float startY = vp.Y + 6 - scrollY;

            for (int i = 0; i <= colors.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;

                Rectangle boxRect = new Rectangle(
                    startX + col * (boxSize + spacing),
                    startY + row * (boxSize + spacing),
                    boxSize,
                    boxSize
                );

                if (Raylib.CheckCollisionPointRec(mousePos, boxRect))
                {
                    if (i == colors.Count)
                    {
                        onOpenColorPicker();
                    }
                    else
                    {
                        CurrentColor = colors[i];
                        SelectedColorIndex = i;
                    }
                    return true;
                }
            }
        }

        return isInsideViewport;
    }

    public void Draw()
    {
        Vector2 mousePos = Raylib.GetMousePosition();

        // Seçili renk indeksini güncel tut
        if (SelectedColorIndex < 0 || SelectedColorIndex >= colors.Count || !ColorsEqual(colors[SelectedColorIndex], CurrentColor))
        {
            SelectedColorIndex = colors.FindIndex(c => ColorsEqual(c, CurrentColor));
        }

        Raylib.DrawText("RENK & PALET", (int)Offset.X, (int)Offset.Y - 19, 11, PixelSkin.AccentCyan);

        Rectangle activeBox = ActiveColorBoxBounds;
        bool activeHover = Raylib.CheckCollisionPointRec(mousePos, activeBox);

        // Yarı saydam renkler için dama zemini
        int halfW = (int)(activeBox.Width / 2);
        int halfH = (int)(activeBox.Height / 2);
        Raylib.DrawRectangle((int)activeBox.X, (int)activeBox.Y, halfW, halfH, new Color((byte)170, (byte)170, (byte)170, (byte)255));
        Raylib.DrawRectangle((int)activeBox.X + halfW, (int)activeBox.Y, halfW, halfH, new Color((byte)115, (byte)115, (byte)115, (byte)255));
        Raylib.DrawRectangle((int)activeBox.X, (int)activeBox.Y + halfH, halfW, halfH, new Color((byte)115, (byte)115, (byte)115, (byte)255));
        Raylib.DrawRectangle((int)activeBox.X + halfW, (int)activeBox.Y + halfH, halfW, halfH, new Color((byte)170, (byte)170, (byte)170, (byte)255));

        Raylib.DrawRectangleRec(activeBox, CurrentColor);

        // Aktif renk kutucuğunun tam ortasında sadece sıra numarasını göster ("SECILI" yazısı kaldırıldı)
        if (SelectedColorIndex >= 0)
        {
            Color activeContrast = GetContrastTextColor(CurrentColor);
            Color activeShadow = activeContrast.R > 128
                ? new Color((byte)0, (byte)0, (byte)0, (byte)185)
                : new Color((byte)255, (byte)255, (byte)255, (byte)185);
            string numStr = $"{SelectedColorIndex + 1}";
            int numW = Raylib.MeasureText(numStr, 14);
            int tx = (int)(activeBox.X + (activeBox.Width - numW) / 2f);
            int ty = (int)(activeBox.Y + (activeBox.Height - 14) / 2f);
            Raylib.DrawText(numStr, tx + 1, ty + 1, 14, activeShadow);
            Raylib.DrawText(numStr, tx, ty, 14, activeContrast);
        }

        Raylib.DrawRectangleLinesEx(activeBox, 2f, activeHover ? PixelSkin.AccentGold : Color.RayWhite);
        Raylib.DrawRectangleLinesEx(new Rectangle(activeBox.X - 1, activeBox.Y - 1, activeBox.Width + 2, activeBox.Height + 2), 1f, PixelSkin.PanelBorderDark);

        string hex = $"#{CurrentColor.R:X2}{CurrentColor.G:X2}{CurrentColor.B:X2}";
        Raylib.DrawText(hex, (int)Offset.X + 51, (int)Offset.Y + 6, 14, PixelSkin.AccentGold);
        Raylib.DrawText($"R:{CurrentColor.R} G:{CurrentColor.G} B:{CurrentColor.B}", (int)Offset.X + 51, (int)Offset.Y + 25, 10, PixelSkin.TextMuted);

        // Sadeleştirilmiş Palet Seçici Çubuğu (< Palet Adı >)
        Rectangle prevBtn = PresetPrevBtnBounds;
        Rectangle nameBtn = PresetNameBtnBounds;
        Rectangle nextBtn = PresetNextBtnBounds;

        PixelSkin.DrawButton(prevBtn, Raylib.CheckCollisionPointRec(mousePos, prevBtn), false);
        Raylib.DrawText("<", (int)prevBtn.X + 7, (int)prevBtn.Y + 5, 10, PixelSkin.TextPrimary);

        PixelSkin.DrawButton(nameBtn, Raylib.CheckCollisionPointRec(mousePos, nameBtn), false);
        string shortPreset = CurrentPresetName.Length > 16 ? CurrentPresetName[..16] : CurrentPresetName;
        int pnw = Raylib.MeasureText(shortPreset, 10);
        Raylib.DrawText(shortPreset, (int)(nameBtn.X + (nameBtn.Width - pnw) / 2f), (int)nameBtn.Y + 5, 10, PixelSkin.AccentGold);

        PixelSkin.DrawButton(nextBtn, Raylib.CheckCollisionPointRec(mousePos, nextBtn), false);
        Raylib.DrawText(">", (int)nextBtn.X + 7, (int)nextBtn.Y + 5, 10, PixelSkin.TextPrimary);

        Rectangle vp = ViewportBounds;
        PixelSkin.DrawInsetBox(vp);

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - ViewportHeight);
        scrollY = Math.Clamp(scrollY, 0, maxScroll);

        Raylib.BeginScissorMode((int)vp.X + 2, (int)vp.Y + 2, (int)vp.Width - 4, (int)vp.Height - 4);

        float startX = vp.X + 7;
        float startY = vp.Y + 6 - scrollY;

        for (int i = 0; i <= colors.Count; i++)
        {
            int col = i % columns;
            int row = i / columns;

            Rectangle boxRect = new Rectangle(
                startX + col * (boxSize + spacing),
                startY + row * (boxSize + spacing),
                boxSize,
                boxSize
            );

            bool swatchHover = Raylib.CheckCollisionPointRec(mousePos, boxRect);

            if (i == colors.Count)
            {
                // Sade "+" Yeni Renk Ekleme Kutucuğu
                PixelSkin.DrawButton(boxRect, swatchHover, false);
                int pw = Raylib.MeasureText("+", 14);
                Raylib.DrawText("+", (int)(boxRect.X + (boxRect.Width - pw) / 2f), (int)(boxRect.Y + (boxRect.Height - 14) / 2f), 14, swatchHover ? PixelSkin.AccentGold : PixelSkin.TextMuted);
                continue;
            }

            bool isSelected = (i == SelectedColorIndex) || (SelectedColorIndex < 0 && ColorsEqual(colors[i], CurrentColor));

            Raylib.DrawRectangleRec(boxRect, colors[i]);
            Raylib.DrawLine((int)boxRect.X + 1, (int)boxRect.Y + 1, (int)(boxRect.X + boxRect.Width - 1), (int)boxRect.Y + 1, new Color((byte)255, (byte)255, (byte)255, (byte)55));
            Raylib.DrawRectangleLinesEx(boxRect, 1f, PixelSkin.PanelBorderDark);

            if (isSelected)
            {
                Raylib.DrawRectangleLinesEx(new Rectangle(boxRect.X - 1, boxRect.Y - 1, boxRect.Width + 2, boxRect.Height + 2), 2f, PixelSkin.AccentGold);

                // Seçili renk kutucuğunun tam ortasında sıra numarasını göster
                string numTxt = $"{i + 1}";
                int numW = Raylib.MeasureText(numTxt, 10);
                int tx = (int)(boxRect.X + (boxRect.Width - numW) / 2f);
                int ty = (int)(boxRect.Y + (boxRect.Height - 10) / 2f);

                Color fg = GetContrastTextColor(colors[i]);
                Color shadow = fg.R > 128 ? new Color((byte)0, (byte)0, (byte)0, (byte)190) : new Color((byte)255, (byte)255, (byte)255, (byte)190);
                Raylib.DrawText(numTxt, tx + 1, ty + 1, 10, shadow);
                Raylib.DrawText(numTxt, tx, ty, 10, fg);
            }
            else if (swatchHover)
            {
                Raylib.DrawRectangleLinesEx(boxRect, 1f, Color.White);
                string numTxt = $"{i + 1}";
                int numW = Raylib.MeasureText(numTxt, 9);
                int tx = (int)(boxRect.X + (boxRect.Width - numW) / 2f);
                int ty = (int)(boxRect.Y + (boxRect.Height - 9) / 2f);
                Raylib.DrawText(numTxt, tx, ty, 9, GetContrastTextColor(colors[i]));
            }
        }

        Raylib.EndScissorMode();

        if (maxScroll > 0)
        {
            float trackX = vp.X + vp.Width - 8;
            float trackHeight = vp.Height - 4;
            float thumbHeight = Math.Max(24f, (vp.Height / totalHeight) * trackHeight);
            float thumbY = vp.Y + 2 + (scrollY / maxScroll) * (trackHeight - thumbHeight);

            Raylib.DrawRectangle((int)trackX, (int)vp.Y + 2, 6, (int)trackHeight, new Color((byte)14, (byte)14, (byte)18, (byte)220));
            Color thumbColor = isDraggingScrollbar ? PixelSkin.AccentCyan : new Color((byte)95, (byte)97, (byte)115, (byte)255);
            Raylib.DrawRectangle((int)trackX, (int)thumbY, 6, (int)thumbHeight, thumbColor);
        }
    }
}
