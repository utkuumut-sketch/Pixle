// === DOSYA: \UI\SettingsDialog.cs ===
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class SettingsDialog
{
    public bool IsOpen { get; private set; } = false;
    private Rectangle windowRect = new Rectangle(0, 0, 780, 560);
    private int activeCategory = 0;
    private bool justOpened = false;

    private int rebindingIndex = -1;
    private float scrollY = 0f;
    private float perfScrollY = 0f;
    private string searchQuery = "";
    private bool isSearchActive = false;
    private string conflictWarning = "";
    private float conflictTimer = 0f;

    private const float LeftSidebarW = 195f;
    private const float HeaderH = 32f;
    private const float FooterH = 52f;
    private const int GeneralRowH = 54;
    private const int PerfRowCount = 13;
    private const int PerfRowHeight = 44;
    private const int ShortcutRowH = 38;

    private readonly (string Title, string Sub, string Icon)[] categories =
    {
        ("Genel & Araclar", "Cizim, geri alma ve paneller", "tool_pen"),
        ("Gorunum & Perf.", "Tema, animasyon ve FPS", "assist_grid"),
        ("Kisayollar", "Klavye tus atamalari", "sel_wand")
    };

    private static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };

    private sealed record ShortcutMap(string Name, Func<KeyboardKey> GetKey, Action<KeyboardKey> SetKey);
    private readonly List<ShortcutMap> allShortcuts = new();

    public SettingsDialog()
    {
        allShortcuts.Add(new("Kalem Araci", () => AppSettings.KeyPen, k => AppSettings.KeyPen = k));
        allShortcuts.Add(new("Silgi Araci", () => AppSettings.KeyEraser, k => AppSettings.KeyEraser = k));
        allShortcuts.Add(new("Dither (Noktali Firca)", () => AppSettings.KeyDither, k => AppSettings.KeyDither = k));
        allShortcuts.Add(new("Tonlama (Shading) Fircasi", () => AppSettings.KeyShading, k => AppSettings.KeyShading = k));
        allShortcuts.Add(new("Kova Araci", () => AppSettings.KeyBucket, k => AppSettings.KeyBucket = k));
        allShortcuts.Add(new("Akilli Kova", () => AppSettings.KeySmartBucket, k => AppSettings.KeySmartBucket = k));
        allShortcuts.Add(new("Damlalik", () => AppSettings.KeyEyedropper, k => AppSettings.KeyEyedropper = k));
        allShortcuts.Add(new("Bulaniklastir (Blur)", () => AppSettings.KeyBlur, k => AppSettings.KeyBlur = k));
        allShortcuts.Add(new("Karistir (Jumble)", () => AppSettings.KeyJumble, k => AppSettings.KeyJumble = k));
        allShortcuts.Add(new("Cizgi Araci", () => AppSettings.KeyLine, k => AppSettings.KeyLine = k));
        allShortcuts.Add(new("Egri Araci (3 Nokta Bezier)", () => AppSettings.KeyCurve, k => AppSettings.KeyCurve = k));
        allShortcuts.Add(new("Sekiller", () => AppSettings.KeyShape, k => AppSettings.KeyShape = k));
        allShortcuts.Add(new("Secim / Tasi", () => AppSettings.KeySelectMove, k => AppSettings.KeySelectMove = k));
        allShortcuts.Add(new("Animasyon Paneli Gizle", () => AppSettings.KeyToggleAnimPanel, k => AppSettings.KeyToggleAnimPanel = k));
        allShortcuts.Add(new("Katman Paneli Gizle", () => AppSettings.KeyToggleLayers, k => AppSettings.KeyToggleLayers = k));
        allShortcuts.Add(new("Pixel-Perfect Modu", () => AppSettings.KeyPixelPerfect, k => AppSettings.KeyPixelPerfect = k));
        allShortcuts.Add(new("Simetri Modu Gecisi", () => AppSettings.KeySymmetry, k => AppSettings.KeySymmetry = k));
        allShortcuts.Add(new("Alfa Kilidi (Alpha Lock)", () => AppSettings.KeyAlphaLock, k => AppSettings.KeyAlphaLock = k));
        allShortcuts.Add(new("Tile / Doseme Modu (Wrap)", () => AppSettings.KeyTileWrap, k => AppSettings.KeyTileWrap = k));
        allShortcuts.Add(new("Karo Izgarasi Boyutu Gecisi", () => AppSettings.KeyTileGrid, k => AppSettings.KeyTileGrid = k));

        for (int i = 0; i < 20; i++)
        {
            int index = i;
            allShortcuts.Add(new($"Palet Renk {i + 1}", () => AppSettings.KeyColors[index], k => AppSettings.KeyColors[index] = k));
        }
    }

    private Rectangle GetPerfListBounds()
    {
        float contentX = windowRect.X + LeftSidebarW + 18f;
        float listTop = windowRect.Y + HeaderH + 92f;
        float listBottom = windowRect.Y + windowRect.Height - FooterH - 10f;
        return new Rectangle(contentX, listTop, windowRect.Width - LeftSidebarW - 36f, listBottom - listTop);
    }

    private Rectangle GetShortcutListBounds()
    {
        float contentX = windowRect.X + LeftSidebarW + 18f;
        float listTop = windowRect.Y + HeaderH + 92f;
        float listBottom = windowRect.Y + windowRect.Height - FooterH - 10f;
        return new Rectangle(contentX, listTop, windowRect.Width - LeftSidebarW - 36f, listBottom - listTop);
    }

    public void Open()
    {
        IsOpen = true;
        justOpened = true;
        rebindingIndex = -1;
        isSearchActive = false;
        searchQuery = "";
        scrollY = 0f;
        perfScrollY = 0f;
        conflictWarning = "";
        conflictTimer = 0f;
        while (Raylib.GetKeyPressed() > 0) { }
    }

    public void Close()
    {
        IsOpen = false;
        rebindingIndex = -1;
        isSearchActive = false;
    }

    private static bool CheckStepperClick(Vector2 mousePos, float x, float y, out int dir)
    {
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(x, y, 28, 26))) { dir = -1; return true; }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(x + 102, y, 28, 26))) { dir = 1; return true; }
        dir = 0;
        return false;
    }

    public bool Update(Vector2 mousePos, Action onToggleGrid, bool isGridVisible = true)
    {
        if (!IsOpen) return false;

        if (conflictTimer > 0f)
        {
            conflictTimer -= Raylib.GetFrameTime();
            if (conflictTimer <= 0f) conflictWarning = "";
        }

        windowRect.X = MathF.Round((Raylib.GetScreenWidth() - windowRect.Width) / 2f);
        windowRect.Y = MathF.Round((Raylib.GetScreenHeight() - windowRect.Height) / 2f);

        if (justOpened) { justOpened = false; return true; }

        if (rebindingIndex >= 0)
        {
            int key = Raylib.GetKeyPressed();
            if (key > 0)
            {
                if ((KeyboardKey)key == KeyboardKey.Escape)
                {
                    rebindingIndex = -1;
                    return true;
                }

                KeyboardKey k = ((KeyboardKey)key == KeyboardKey.Backspace || (KeyboardKey)key == KeyboardKey.Delete)
                    ? KeyboardKey.Null
                    : (KeyboardKey)key;
                var filtered = GetFilteredShortcuts();
                if (rebindingIndex < filtered.Count)
                {
                    var targetShortcut = filtered[rebindingIndex];
                    if (k != KeyboardKey.Null)
                    {
                        foreach (var sc in allShortcuts)
                        {
                            if (!ReferenceEquals(sc, targetShortcut) && sc.GetKey() == k)
                            {
                                sc.SetKey(KeyboardKey.Null);
                                conflictWarning = $"'{AppSettings.KeyToString(k)}' tusu '{sc.Name}' kisayolundan kaldirildi.";
                                conflictTimer = 4.0f;
                            }
                        }
                    }
                    targetShortcut.SetKey(k);
                    AppSettings.MarkAsCustomPreset();
                }
                rebindingIndex = -1;
            }
            return true;
        }

        float contentX = windowRect.X + LeftSidebarW + 18f;
        float contentY = windowRect.Y + HeaderH + 14f;
        float contentW = windowRect.Width - LeftSidebarW - 36f;

        if (isSearchActive && activeCategory == 2)
        {
            int charKey = Raylib.GetCharPressed();
            while (charKey > 0)
            {
                if (charKey >= 32 && charKey <= 125 && searchQuery.Length < 25)
                {
                    searchQuery += (char)charKey;
                    scrollY = 0f;
                }
                charKey = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && searchQuery.Length > 0)
            {
                searchQuery = searchQuery[..^1];
                scrollY = 0f;
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (int i = 0; i < categories.Length; i++)
            {
                Rectangle catRect = new Rectangle(windowRect.X + 8, windowRect.Y + HeaderH + 14 + (i * 54), LeftSidebarW - 16, 48);
                if (Raylib.CheckCollisionPointRec(mousePos, catRect))
                {
                    activeCategory = i;
                    rebindingIndex = -1;
                    isSearchActive = false;
                }
            }

            Rectangle closeBtn = new Rectangle(windowRect.X + windowRect.Width - 28, windowRect.Y + 5, 22, 22);
            Rectangle saveBtn = new Rectangle(windowRect.X + windowRect.Width - 132, windowRect.Y + windowRect.Height - 40, 114, 30);
            Rectangle resetBtn = new Rectangle(windowRect.X + LeftSidebarW + 18, windowRect.Y + windowRect.Height - 40, 148, 30);

            if (Raylib.CheckCollisionPointRec(mousePos, closeBtn) || Raylib.CheckCollisionPointRec(mousePos, saveBtn))
            {
                Close();
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, resetBtn))
            {
                AppSettings.ResetToDefaults();
                searchQuery = "";
                conflictWarning = "Tum ayarlar varsayilana sifirlandi.";
                conflictTimer = 2.5f;
            }

            isSearchActive = false;

            if (activeCategory == 0) UpdateGeneralTabClicks(mousePos, contentX, contentY, contentW);
            else if (activeCategory == 1) UpdatePerfTabClicks(mousePos, contentX, contentY, contentW, onToggleGrid, isGridVisible);
            else if (activeCategory == 2) UpdateShortcutsTabClicks(mousePos, contentX, contentY, contentW);
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            if (activeCategory == 1)
            {
                Rectangle listBounds = GetPerfListBounds();
                float maxPerfScroll = Math.Max(0f, (PerfRowCount * PerfRowHeight + 8f) - listBounds.Height);
                perfScrollY = Math.Clamp(perfScrollY - (wheel * 34f), 0f, maxPerfScroll);
            }
            else if (activeCategory == 2)
            {
                Rectangle listBounds = GetShortcutListBounds();
                float maxScroll = Math.Max(0f, (GetFilteredShortcuts().Count * ShortcutRowH + 8f) - listBounds.Height);
                scrollY = Math.Clamp(scrollY - (wheel * 34f), 0f, maxScroll);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (isSearchActive) { isSearchActive = false; searchQuery = ""; } else Close();
        }
        return true;
    }

    private void UpdateGeneralTabClicks(Vector2 mousePos, float contentX, float contentY, float contentW)
    {
        float ctrlX = contentX + contentW - 144f;
        float row0Y = contentY + 42f;

        if (CheckStepperClick(mousePos, ctrlX, row0Y + 13f, out int d1))
        {
            AppSettings.MaxUndoSteps = Math.Clamp(AppSettings.MaxUndoSteps + d1 * 5, 5, 200);
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(ctrlX, row0Y + GeneralRowH + 13f, 130, 28)))
        {
            AppSettings.EyedropperAutoSwitchPen = !AppSettings.EyedropperAutoSwitchPen;
            AppSettings.MarkAsCustomPreset();
        }
        if (CheckStepperClick(mousePos, ctrlX, row0Y + GeneralRowH * 2 + 13f, out int d2))
        {
            AppSettings.BlurIntensity = Math.Clamp(AppSettings.BlurIntensity + d2, 1, 5);
            AppSettings.MarkAsCustomPreset();
        }
        if (CheckStepperClick(mousePos, ctrlX, row0Y + GeneralRowH * 3 + 13f, out int d3))
        {
            AppSettings.JumbleIntensity = Math.Clamp(AppSettings.JumbleIntensity + d3 * 10, 10, 100);
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(ctrlX, row0Y + GeneralRowH * 4 + 13f, 130, 28)))
        {
            AppSettings.EnablePanelResize = !AppSettings.EnablePanelResize;
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(ctrlX, row0Y + GeneralRowH * 5 + 13f, 130, 28)))
        {
            AppSettings.ShowReferencePanel = !AppSettings.ShowReferencePanel;
            AppSettings.MarkAsCustomPreset();
        }
    }

    private void UpdatePerfTabClicks(Vector2 mousePos, float contentX, float contentY, float contentW, Action onToggleGrid, bool isGridVisible)
    {
        PerformancePreset[] presets = { PerformancePreset.Varsayilan, PerformancePreset.Kalite, PerformancePreset.Performans, PerformancePreset.Ozel };
        float pillStartX = contentX + 120f;
        float pillW = (contentW - 120f - 18f) / 4f;
        for (int p = 0; p < presets.Length; p++)
        {
            if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(pillStartX + p * (pillW + 6f), contentY + 38f, pillW, 28f)))
            {
                AppSettings.ApplyPreset(presets[p]);
                return;
            }
        }

        Rectangle listBounds = GetPerfListBounds();
        if (!Raylib.CheckCollisionPointRec(mousePos, listBounds)) return;

        float y0 = listBounds.Y + 4f - perfScrollY;
        float bx = listBounds.X + listBounds.Width - 150f;

        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + 7f, 130, 26)))
        {
            AppSettings.EnableUIAnimations = !AppSettings.EnableUIAnimations;
            AppSettings.MarkAsCustomPreset();
        }
        if (AppSettings.EnableUIAnimations && CheckStepperClick(mousePos, bx, y0 + PerfRowHeight + 7f, out int dAnim))
        {
            AppSettings.UIAnimationSpeed = Math.Clamp(AppSettings.UIAnimationSpeed + dAnim * 2f, 4f, 24f);
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 2 + 7f, 130, 26)))
        {
            AppSettings.SmoothCameraZoom = !AppSettings.SmoothCameraZoom;
            AppSettings.MarkAsCustomPreset();
        }
        if (CheckStepperClick(mousePos, bx, y0 + PerfRowHeight * 3 + 7f, out int dFps))
        {
            int idx = Array.IndexOf(FpsOptions, AppSettings.TargetFPS);
            if (idx < 0) idx = FpsOptions.Length - 1;
            AppSettings.TargetFPS = FpsOptions[Math.Clamp(idx + dFps, 0, FpsOptions.Length - 1)];
            Raylib.SetTargetFPS(AppSettings.TargetFPS);
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 4 + 7f, 130, 26)))
        {
            AppSettings.ShowBrushPreview = !AppSettings.ShowBrushPreview;
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 5 + 7f, 130, 26)))
        {
            AppSettings.ShowPanelBackdrops = !AppSettings.ShowPanelBackdrops;
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 6 + 7f, 130, 26)))
        {
            AppSettings.IsLightTheme = !AppSettings.IsLightTheme;
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 7 + 7f, 130, 26)))
        {
            AppSettings.CheckerContrast = (AppSettings.CheckerContrast + 1) % 3;
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 8 + 7f, 130, 26)))
        {
            onToggleGrid();
            AppSettings.MarkAsCustomPreset();
        }
        if (isGridVisible && CheckStepperClick(mousePos, bx, y0 + PerfRowHeight * 9 + 7f, out int dGrid))
        {
            AppSettings.GridOpacity = Math.Clamp(AppSettings.GridOpacity + dGrid * 15, 15, 165);
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 10 + 7f, 130, 26)))
        {
            AppSettings.CycleTileGridSize();
            AppSettings.MarkAsCustomPreset();
        }
        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(bx, y0 + PerfRowHeight * 11 + 7f, 130, 26)))
        {
            AppSettings.EnableOnionSkin = !AppSettings.EnableOnionSkin;
            AppSettings.MarkAsCustomPreset();
        }
        if (AppSettings.EnableOnionSkin && CheckStepperClick(mousePos, bx, y0 + PerfRowHeight * 12 + 7f, out int dOnion))
        {
            AppSettings.OnionSkinOpacity = Math.Clamp(AppSettings.OnionSkinOpacity + dOnion * 25, 25, 225);
            AppSettings.MarkAsCustomPreset();
        }
    }

    private void UpdateShortcutsTabClicks(Vector2 mousePos, float contentX, float contentY, float contentW)
    {
        Rectangle searchBox = new Rectangle(contentX, contentY + 36f, contentW - 88f, 30f);
        Rectangle clearBtn = new Rectangle(contentX + contentW - 80f, contentY + 36f, 80f, 30f);

        if (Raylib.CheckCollisionPointRec(mousePos, searchBox))
        {
            isSearchActive = true;
            return;
        }
        if (!string.IsNullOrEmpty(searchQuery) && Raylib.CheckCollisionPointRec(mousePos, clearBtn))
        {
            searchQuery = "";
            scrollY = 0f;
            return;
        }

        Rectangle listBounds = GetShortcutListBounds();
        if (!Raylib.CheckCollisionPointRec(mousePos, listBounds)) return;

        var filtered = GetFilteredShortcuts();
        for (int i = 0; i < filtered.Count; i++)
        {
            float rowY = listBounds.Y + 4f + (i * ShortcutRowH) - scrollY;
            if (rowY + ShortcutRowH < listBounds.Y || rowY > listBounds.Y + listBounds.Height) continue;

            Rectangle keyBtn = new Rectangle(listBounds.X + listBounds.Width - 156f, rowY + 5f, 138f, 26f);
            if (Raylib.CheckCollisionPointRec(mousePos, keyBtn))
            {
                rebindingIndex = i;
                break;
            }
        }
    }

    private List<ShortcutMap> GetFilteredShortcuts()
    {
        if (string.IsNullOrWhiteSpace(searchQuery)) return allShortcuts;
        return allShortcuts.FindAll(s => s.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase));
    }

    public void Draw(bool isGridVisible)
    {
        if (!IsOpen) return;

        Vector2 mousePos = Raylib.GetMousePosition();
        PixelSkin.DrawDialogWindow(windowRect, "Studyosu Ayarlari (Settings)", mousePos, out _);

        // Sol Kategori Menüsü
        Rectangle sidebarRect = new Rectangle(windowRect.X + 1, windowRect.Y + HeaderH, LeftSidebarW, windowRect.Height - HeaderH - 1);
        Raylib.DrawRectangleRec(sidebarRect, PixelSkin.InsetDark);
        Raylib.DrawLine((int)(windowRect.X + LeftSidebarW), (int)(windowRect.Y + HeaderH), (int)(windowRect.X + LeftSidebarW), (int)(windowRect.Y + windowRect.Height - 1), PixelSkin.BorderOutline);

        for (int i = 0; i < categories.Length; i++)
        {
            Rectangle catRect = new Rectangle(windowRect.X + 8, windowRect.Y + HeaderH + 14 + (i * 54), LeftSidebarW - 16, 48);
            bool isSel = (activeCategory == i);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, catRect);

            Color cardBg = isSel
                ? PixelSkin.AccentBg
                : (hover ? PixelSkin.ElevatedBg : PixelSkin.PanelBg);

            Raylib.DrawRectangleRounded(catRect, 0.18f, 6, cardBg);
            Raylib.DrawRectangleRoundedLines(catRect, 0.18f, 6, isSel ? PixelSkin.AccentPrimary : PixelSkin.BorderOutline);

            if (isSel)
            {
                Raylib.DrawRectangle((int)catRect.X + 2, (int)catRect.Y + 8, 3, (int)catRect.Height - 16, PixelSkin.AccentGold);
            }

            PixelIcons.DrawIcon(categories[i].Icon, catRect.X + 10, catRect.Y + 16, 16f);
            Raylib.DrawText(categories[i].Title, (int)catRect.X + 34, (int)catRect.Y + 9, 13, isSel ? Color.White : PixelSkin.TextPrimary);
            Raylib.DrawText(categories[i].Sub, (int)catRect.X + 34, (int)catRect.Y + 27, 10, isSel ? PixelSkin.AccentGold : PixelSkin.TextMuted);
        }

        //Sol alt bilgi kartı
        Rectangle tipCard = new Rectangle(windowRect.X + 10, windowRect.Y + windowRect.Height - 95, LeftSidebarW - 20, 82);
        PixelSkin.DrawPanel(tipCard);
        Raylib.DrawText("IPUCU", (int)tipCard.X + 10, (int)tipCard.Y + 8, 10, PixelSkin.AccentGold);
        Raylib.DrawText("Kisayol atarken silmek", (int)tipCard.X + 10, (int)tipCard.Y + 24, 10, PixelSkin.TextMuted);
        Raylib.DrawText("icin Backspace/Delete,", (int)tipCard.X + 10, (int)tipCard.Y + 38, 10, PixelSkin.TextMuted);
        Raylib.DrawText("iptal icin ESC kullanin.", (int)tipCard.X + 10, (int)tipCard.Y + 52, 10, PixelSkin.TextMuted);

        float contentX = windowRect.X + LeftSidebarW + 18f;
        float contentY = windowRect.Y + HeaderH + 14f;
        float contentW = windowRect.Width - LeftSidebarW - 36f;

        if (activeCategory == 0) DrawGeneralTab(contentX, contentY, contentW, mousePos);
        else if (activeCategory == 1) DrawPerfTab(contentX, contentY, contentW, mousePos, isGridVisible);
        else if (activeCategory == 2) DrawShortcutsTab(contentX, contentY, contentW, mousePos);

        // Alt Çubuk (Footer)
        int footerY = (int)(windowRect.Y + windowRect.Height - FooterH);
        Raylib.DrawRectangle((int)(windowRect.X + LeftSidebarW + 1), footerY, (int)(windowRect.Width - LeftSidebarW - 2), (int)FooterH - 1, PixelSkin.ElevatedBg);
        Raylib.DrawLine((int)(windowRect.X + LeftSidebarW), footerY, (int)(windowRect.X + windowRect.Width - 1), footerY, PixelSkin.BorderOutline);

        if (!string.IsNullOrEmpty(conflictWarning))
        {
            Raylib.DrawText(conflictWarning, (int)(windowRect.X + LeftSidebarW + 180), footerY + 19, 11, PixelSkin.AccentGold);
        }

        UIWidgets.DrawColorButton(
            new Rectangle(windowRect.X + LeftSidebarW + 18, windowRect.Y + windowRect.Height - 40, 148, 30),
            "Varsayilana Sifirla",
            new Color((byte)110, (byte)42, (byte)48, (byte)255),
            mousePos,
            12
        );
        UIWidgets.DrawColorButton(
            new Rectangle(windowRect.X + windowRect.Width - 132, windowRect.Y + windowRect.Height - 40, 114, 30),
            "Kaydet & Kapat",
            new Color((byte)44, (byte)124, (byte)86, (byte)255),
            mousePos,
            12,
            PixelSkin.AccentHover
        );
    }

    private static void DrawSettingCard(float x, float y, float w, float h, string title, string subtitle, bool enabled = true)
    {
        Rectangle card = new Rectangle(x, y, w, h - 6f);
        Raylib.DrawRectangleRounded(card, 0.14f, 6, PixelSkin.ElevatedBg);
        Raylib.DrawRectangleRoundedLines(card, 0.14f, 6, PixelSkin.BorderOutline);

        Raylib.DrawText(title, (int)x + 14, (int)y + 9, 13, enabled ? PixelSkin.TextPrimary : PixelSkin.TextMuted);
        Raylib.DrawText(subtitle, (int)x + 14, (int)y + 27, 10, PixelSkin.TextMuted);
    }

    private void DrawGeneralTab(float contentX, float contentY, float contentW, Vector2 mousePos)
    {
        Raylib.DrawText("GENEL VE ARAC AYARLARI", (int)contentX, (int)contentY, 15, PixelSkin.AccentGold);
        Raylib.DrawText("Cizim araclarinin davranislarini ve calisma alani duzenini ozellestirin", (int)contentX, (int)contentY + 18, 11, PixelSkin.TextMuted);
        Raylib.DrawLine((int)contentX, (int)contentY + 34, (int)(contentX + contentW), (int)contentY + 34, PixelSkin.PanelBorderLight);

        float row0Y = contentY + 42f;
        float ctrlX = contentX + contentW - 144f;

        DrawSettingCard(contentX, row0Y, contentW, GeneralRowH,
            "Geri Alma (Undo) Basamak Limiti", "Bellekte tutulacak maksimum islem gecmisi adimi");
        UIWidgets.DrawStepper(ctrlX, row0Y + 11f, AppSettings.MaxUndoSteps.ToString(), mousePos, true);

        DrawSettingCard(contentX, row0Y + GeneralRowH, contentW, GeneralRowH,
            "Damlalik Sonrasi Otomatik Kalem", "Renk sectikten sonra otomatik olarak Kalem aracina doner");
        UIWidgets.DrawToggleBtn(new Rectangle(ctrlX, row0Y + GeneralRowH + 10f, 130, 28), AppSettings.EyedropperAutoSwitchPen, AppSettings.EyedropperAutoSwitchPen ? "Acik" : "Kapali");

        DrawSettingCard(contentX, row0Y + GeneralRowH * 2, contentW, GeneralRowH,
            "Bulaniklastirma (Blur) Yogunlugu", "Blur fircasinin komsu pikselleri yumusatma siddeti");
        UIWidgets.DrawStepper(ctrlX, row0Y + GeneralRowH * 2 + 11f, $"{AppSettings.BlurIntensity} / 5", mousePos, true);

        DrawSettingCard(contentX, row0Y + GeneralRowH * 3, contentW, GeneralRowH,
            "Karistirma (Jumble) Siddeti", "Jumble fircasinin pikselleri yer degistirme orani");
        UIWidgets.DrawStepper(ctrlX, row0Y + GeneralRowH * 3 + 11f, $"%{AppSettings.JumbleIntensity}", mousePos, true);

        DrawSettingCard(contentX, row0Y + GeneralRowH * 4, contentW, GeneralRowH,
            "Panelleri Serbest Boyutlandir", "Sol ve sag panellerin kenarlarindan surukleyerek genisligini ayarla");
        UIWidgets.DrawToggleBtn(new Rectangle(ctrlX, row0Y + GeneralRowH * 4 + 10f, 130, 28), AppSettings.EnablePanelResize, AppSettings.EnablePanelResize ? "Acik" : "Kapali");

        DrawSettingCard(contentX, row0Y + GeneralRowH * 5, contentW, GeneralRowH,
            "Referans Gorsel Bolumu", "Sag paneldeki referans resim yukleme ve onizleme alanini goster");
        UIWidgets.DrawToggleBtn(new Rectangle(ctrlX, row0Y + GeneralRowH * 5 + 10f, 130, 28), AppSettings.ShowReferencePanel, AppSettings.ShowReferencePanel ? "Gorunur" : "Gizli");
    }

    private void DrawPerfTab(float contentX, float contentY, float contentW, Vector2 mousePos, bool isGridVisible)
    {
        Raylib.DrawText("GORUNUM, TEMA VE PERFORMANS", (int)contentX, (int)contentY, 15, PixelSkin.AccentGold);
        Raylib.DrawText("Arayuz akiciligini, izgara gorunumunu ve kare hizi limitini yonetin", (int)contentX, (int)contentY + 18, 11, PixelSkin.TextMuted);

        Raylib.DrawText("Hizli Profil:", (int)contentX, (int)contentY + 45, 12, PixelSkin.AccentCyan);
        (string Label, PerformancePreset Preset)[] presetItems =
        {
            ("Varsayilan", PerformancePreset.Varsayilan),
            ("Kalite", PerformancePreset.Kalite),
            ("Performans", PerformancePreset.Performans),
            ("Ozel", PerformancePreset.Ozel)
        };

        float pillStartX = contentX + 120f;
        float pillW = (contentW - 120f - 18f) / 4f;
        for (int p = 0; p < presetItems.Length; p++)
        {
            Rectangle pRect = new Rectangle(pillStartX + p * (pillW + 6f), contentY + 38f, pillW, 28f);
            bool isCurrent = AppSettings.CurrentPreset == presetItems[p].Preset;
            bool hover = Raylib.CheckCollisionPointRec(mousePos, pRect);

            PixelSkin.DrawButton(pRect, hover, isCurrent);
            int tw = Raylib.MeasureText(presetItems[p].Label, 12);
            Raylib.DrawText(
                presetItems[p].Label,
                (int)(pRect.X + (pRect.Width - tw) / 2),
                (int)pRect.Y + 8,
                12,
                isCurrent ? PixelSkin.AccentGold : PixelSkin.TextPrimary
            );
        }

        Rectangle listBounds = GetPerfListBounds();
        PixelSkin.DrawInsetBox(listBounds);

        Raylib.BeginScissorMode((int)listBounds.X + 2, (int)listBounds.Y + 2, (int)listBounds.Width - 4, (int)listBounds.Height - 4);
        float y0 = listBounds.Y + 6f - perfScrollY;
        float rowX = listBounds.X + 8f;
        float rowW = listBounds.Width - 22f;
        float bx = listBounds.X + listBounds.Width - 150f;

        void DrawPerfRow(int idx, string title, bool enabled = true)
        {
            float ry = y0 + idx * PerfRowHeight;
            Rectangle rRect = new Rectangle(rowX, ry, rowW, PerfRowHeight - 4f);
            Raylib.DrawRectangleRounded(rRect, 0.15f, 4, idx % 2 == 0 ? PixelSkin.PanelBg : PixelSkin.ElevatedBg);
            Raylib.DrawText(title, (int)rowX + 12, (int)ry + 13, 13, enabled ? PixelSkin.TextPrimary : PixelSkin.TextMuted);
        }

        DrawPerfRow(0, "Panel Kayma Animasyonlari");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + 7f, 130, 26), AppSettings.EnableUIAnimations, AppSettings.EnableUIAnimations ? "Acik" : "Kapali");

        DrawPerfRow(1, "Panel Animasyon Hizi", AppSettings.EnableUIAnimations);
        UIWidgets.DrawStepper(bx, y0 + PerfRowHeight + 7f, $"{(int)AppSettings.UIAnimationSpeed}x", mousePos, AppSettings.EnableUIAnimations);

        DrawPerfRow(2, "Yumusak Kamera Zoom (Lerp)");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + PerfRowHeight * 2 + 7f, 130, 26), AppSettings.SmoothCameraZoom, AppSettings.SmoothCameraZoom ? "Akici" : "Keskin");

        DrawPerfRow(3, "Hedef Kare Hizi (FPS)");
        UIWidgets.DrawStepper(bx, y0 + PerfRowHeight * 3 + 7f, AppSettings.TargetFPS == 0 ? "Sinirsiz" : $"{AppSettings.TargetFPS} FPS", mousePos, true);

        DrawPerfRow(4, "Firca Onizleme Cercevesi (Ghost)");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + PerfRowHeight * 4 + 7f, 130, 26), AppSettings.ShowBrushPreview, AppSettings.ShowBrushPreview ? "Acik" : "Kapali");

        DrawPerfRow(5, "Panel Arka Plan Golgesi");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + PerfRowHeight * 5 + 7f, 130, 26), AppSettings.ShowPanelBackdrops, AppSettings.ShowPanelBackdrops ? "Acik" : "Kapali");

        DrawPerfRow(6, "Studyo Arayuz Temasi");
        Rectangle themeBtn = new Rectangle(bx, y0 + PerfRowHeight * 6 + 7f, 130, 26);
        PixelSkin.DrawButton(themeBtn, Raylib.CheckCollisionPointRec(mousePos, themeBtn), false);
        string themeTxt = AppSettings.IsLightTheme ? "Acik (Light)" : "Koyu (Dark)";
        Raylib.DrawText(themeTxt, (int)(themeBtn.X + (themeBtn.Width - Raylib.MeasureText(themeTxt, 12)) / 2), (int)themeBtn.Y + 7, 12, PixelSkin.AccentCyan);

        DrawPerfRow(7, "Damali Zemin Kontrasti");
        Rectangle chkBtn = new Rectangle(bx, y0 + PerfRowHeight * 7 + 7f, 130, 26);
        PixelSkin.DrawButton(chkBtn, Raylib.CheckCollisionPointRec(mousePos, chkBtn), false);
        string chkTxt = AppSettings.CheckerContrast switch { 0 => "Dusuk", 2 => "Yuksek", _ => "Orta" };
        Raylib.DrawText(chkTxt, (int)(chkBtn.X + (chkBtn.Width - Raylib.MeasureText(chkTxt, 12)) / 2), (int)chkBtn.Y + 7, 12, PixelSkin.AccentGold);

        DrawPerfRow(8, "Piksel Izgarasi (Grid)");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + PerfRowHeight * 8 + 7f, 130, 26), isGridVisible, isGridVisible ? "Acik" : "Kapali");

        DrawPerfRow(9, "Piksel Izgarasi Opakligi", isGridVisible);
        UIWidgets.DrawStepper(bx, y0 + PerfRowHeight * 9 + 7f, $"%{(int)Math.Round(AppSettings.GridOpacity / 255f * 100)}", mousePos, isGridVisible);

        DrawPerfRow(10, "Karo Izgarasi (Tile Grid: 8x8 / 16x16 / 32x32)");
        Rectangle tileGridBtn = new Rectangle(bx, y0 + PerfRowHeight * 10 + 7f, 130, 26);
        PixelSkin.DrawButton(tileGridBtn, Raylib.CheckCollisionPointRec(mousePos, tileGridBtn), AppSettings.TileGridSize > 0);
        string tgTxt = AppSettings.TileGridLabel(AppSettings.TileGridSize);
        Raylib.DrawText(tgTxt, (int)(tileGridBtn.X + (tileGridBtn.Width - Raylib.MeasureText(tgTxt, 12)) / 2), (int)tileGridBtn.Y + 7, 12, AppSettings.TileGridSize > 0 ? PixelSkin.AccentGold : PixelSkin.TextPrimary);

        DrawPerfRow(11, "Sogan Zari (Onion Skin)");
        UIWidgets.DrawToggleBtn(new Rectangle(bx, y0 + PerfRowHeight * 11 + 7f, 130, 26), AppSettings.EnableOnionSkin, AppSettings.EnableOnionSkin ? "Acik" : "Kapali");

        DrawPerfRow(12, "Sogan Zari Opakligi", AppSettings.EnableOnionSkin);
        UIWidgets.DrawStepper(bx, y0 + PerfRowHeight * 12 + 7f, $"%{(int)Math.Round(AppSettings.OnionSkinOpacity / 255f * 100)}", mousePos, AppSettings.EnableOnionSkin);

        Raylib.EndScissorMode();

        float totalContentH = PerfRowCount * PerfRowHeight + 8f;
        float maxPerfScroll = Math.Max(0f, totalContentH - listBounds.Height);
        if (maxPerfScroll > 0)
        {
            float thumbH = Math.Max(24f, (listBounds.Height / totalContentH) * (listBounds.Height - 8f));
            float thumbY = listBounds.Y + 4f + (perfScrollY / maxPerfScroll) * (listBounds.Height - 8f - thumbH);
            Raylib.DrawRectangleRounded(new Rectangle(listBounds.X + listBounds.Width - 9f, thumbY, 5f, thumbH), 0.5f, 4, PixelSkin.AccentPrimary);
        }
    }

    private void DrawShortcutsTab(float contentX, float contentY, float contentW, Vector2 mousePos)
    {
        Raylib.DrawText("KLAVYE KISAYOLLARI", (int)contentX, (int)contentY, 15, PixelSkin.AccentGold);
        Raylib.DrawText("Degistirmek istediginiz kisayolun uzerine tiklayip yeni tusa basin", (int)contentX, (int)contentY + 18, 11, PixelSkin.TextMuted);

        Rectangle searchBox = new Rectangle(contentX, contentY + 36f, contentW - 88f, 30f);
        PixelSkin.DrawInsetBox(searchBox);
        if (isSearchActive)
            Raylib.DrawRectangleLinesEx(searchBox, 1f, PixelSkin.AccentPrimary);

        string displaySearch = searchQuery + (isSearchActive && (int)(Raylib.GetTime() * 2) % 2 == 0 ? "|" : "");
        if (string.IsNullOrEmpty(searchQuery) && !isSearchActive)
            Raylib.DrawText("Kisayol veya arac ara...", (int)searchBox.X + 10, (int)searchBox.Y + 9, 12, PixelSkin.TextMuted);
        else
            Raylib.DrawText(displaySearch, (int)searchBox.X + 10, (int)searchBox.Y + 9, 12, Color.White);

        Rectangle clearBtn = new Rectangle(contentX + contentW - 80f, contentY + 36f, 80f, 30f);
        PixelSkin.DrawButton(clearBtn, Raylib.CheckCollisionPointRec(mousePos, clearBtn), false);
        Raylib.DrawText("Temizle", (int)clearBtn.X + 16, (int)clearBtn.Y + 9, 12, string.IsNullOrEmpty(searchQuery) ? PixelSkin.TextMuted : Color.White);

        Rectangle listBounds = GetShortcutListBounds();
        PixelSkin.DrawInsetBox(listBounds);

        var filtered = GetFilteredShortcuts();
        Raylib.BeginScissorMode((int)listBounds.X + 2, (int)listBounds.Y + 2, (int)listBounds.Width - 4, (int)listBounds.Height - 4);

        for (int i = 0; i < filtered.Count; i++)
        {
            float rowY = listBounds.Y + 4f + (i * ShortcutRowH) - scrollY;
            if (rowY + ShortcutRowH < listBounds.Y || rowY > listBounds.Y + listBounds.Height) continue;

            Rectangle rowRect = new Rectangle(listBounds.X + 8f, rowY, listBounds.Width - 22f, ShortcutRowH - 4f);
            Raylib.DrawRectangleRounded(rowRect, 0.15f, 4, i % 2 == 0 ? PixelSkin.PanelBg : PixelSkin.ElevatedBg);

            Raylib.DrawText(filtered[i].Name, (int)rowRect.X + 12, (int)rowY + 11, 13, PixelSkin.TextPrimary);

            Rectangle keyBtn = new Rectangle(listBounds.X + listBounds.Width - 156f, rowY + 4f, 138f, 26f);
            bool isWaiting = (rebindingIndex == i);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, keyBtn);

            if (isWaiting)
            {
                Raylib.DrawRectangleRounded(keyBtn, 0.25f, 6, new Color((byte)155, (byte)92, (byte)28, (byte)255));
                Raylib.DrawRectangleRoundedLines(keyBtn, 0.25f, 6, PixelSkin.AccentGold);
            }
            else
            {
                PixelSkin.DrawButton(keyBtn, hover, false);
            }

            string keyLabel = isWaiting ? "Tusa Bas (ESC)" : AppSettings.KeyToString(filtered[i].GetKey());
            int kw = Raylib.MeasureText(keyLabel, 12);
            Color keyCol = isWaiting
                ? Color.White
                : (filtered[i].GetKey() == KeyboardKey.Null ? PixelSkin.TextMuted : PixelSkin.AccentGold);
            Raylib.DrawText(keyLabel, (int)(keyBtn.X + (keyBtn.Width - kw) / 2), (int)keyBtn.Y + 7, 12, keyCol);
        }

        Raylib.EndScissorMode();

        float totalH = filtered.Count * ShortcutRowH + 8f;
        float maxScroll = Math.Max(0f, totalH - listBounds.Height);
        if (maxScroll > 0)
        {
            float thumbH = Math.Max(24f, (listBounds.Height / totalH) * (listBounds.Height - 8f));
            float thumbY = listBounds.Y + 4f + (scrollY / maxScroll) * (listBounds.Height - 8f - thumbH);
            Raylib.DrawRectangleRounded(new Rectangle(listBounds.X + listBounds.Width - 9f, thumbY, 5f, thumbH), 0.5f, 4, PixelSkin.AccentPrimary);
        }
    }
}
