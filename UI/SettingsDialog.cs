using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class SettingsDialog
{
    public bool IsOpen { get; private set; } = false;
    private Rectangle windowRect = new Rectangle(0, 0, 740, 540);
    private int activeCategory = 0;
    private bool justOpened = false;

    private int rebindingIndex = -1; 
    private float scrollY = 0f;
    private float perfScrollY = 0f;
    private string searchQuery = "";
    private bool isSearchActive = false;
    private readonly string[] categories = { "Genel", "Gorunum & Perf.", "Kisayollar" };

    // 0 = Sinirsiz FPS
    private static readonly int[] FpsOptions = { 30, 60, 120, 144, 0 };
    private const int PerfRowCount = 12;
    private const int PerfRowHeight = 34;

    class ShortcutMap
    {
        public string Name;
        public Func<KeyboardKey> GetKey;
        public Action<KeyboardKey> SetKey;
        public ShortcutMap(string n, Func<KeyboardKey> get, Action<KeyboardKey> set) { Name = n; GetKey = get; SetKey = set; }
    }
    
    private List<ShortcutMap> allShortcuts = new();

    public SettingsDialog() { InitializeShortcuts(); }

    private void InitializeShortcuts()
    {
        allShortcuts.Add(new ShortcutMap("Kalem Araci", () => AppSettings.KeyPen, k => AppSettings.KeyPen = k));
        allShortcuts.Add(new ShortcutMap("Silgi Araci", () => AppSettings.KeyEraser, k => AppSettings.KeyEraser = k));
        allShortcuts.Add(new ShortcutMap("Kova Araci", () => AppSettings.KeyBucket, k => AppSettings.KeyBucket = k));
        allShortcuts.Add(new ShortcutMap("Akilli Kova", () => AppSettings.KeySmartBucket, k => AppSettings.KeySmartBucket = k));
        allShortcuts.Add(new ShortcutMap("Damlalik", () => AppSettings.KeyEyedropper, k => AppSettings.KeyEyedropper = k));
        allShortcuts.Add(new ShortcutMap("Bulaniklastir (Blur)", () => AppSettings.KeyBlur, k => AppSettings.KeyBlur = k));
        allShortcuts.Add(new ShortcutMap("Karistir (Jumble)", () => AppSettings.KeyJumble, k => AppSettings.KeyJumble = k));
        allShortcuts.Add(new ShortcutMap("Cizgi Araci", () => AppSettings.KeyLine, k => AppSettings.KeyLine = k));
        allShortcuts.Add(new ShortcutMap("Sekiller", () => AppSettings.KeyShape, k => AppSettings.KeyShape = k));
        allShortcuts.Add(new ShortcutMap("Secim / Tasi", () => AppSettings.KeySelectMove, k => AppSettings.KeySelectMove = k));
        allShortcuts.Add(new ShortcutMap("Animasyon Paneli Gizle", () => AppSettings.KeyToggleAnimPanel, k => AppSettings.KeyToggleAnimPanel = k));
        allShortcuts.Add(new ShortcutMap("Katman Paneli Gizle", () => AppSettings.KeyToggleLayers, k => AppSettings.KeyToggleLayers = k));

        for (int i = 0; i < 20; i++)
        {
            int index = i;
            allShortcuts.Add(new ShortcutMap($"Palet Renk {i + 1}", () => AppSettings.KeyColors[index], k => AppSettings.KeyColors[index] = k));
        }
    }

    // GORUNUM & PERFORMANS LISTESI ICIN ORTAK CIZIM VE TIKLAMA SINIRI
    private Rectangle GetPerfListBounds()
    {
        float leftPanelW = 150f;
        float contentX = windowRect.X + leftPanelW + 20f;
        float contentY = windowRect.Y + 45f;
        float listTop = contentY + 68f;
        float listBottom = windowRect.Y + windowRect.Height - 55f;
        return new Rectangle(contentX, listTop, windowRect.Width - leftPanelW - 25f, listBottom - listTop);
    }

    public void Open()
    {
        IsOpen = true; justOpened = true; rebindingIndex = -1; isSearchActive = false; searchQuery = ""; scrollY = 0f; perfScrollY = 0f;
        while (Raylib.GetKeyPressed() > 0) { } 
    }

    public void Close() { IsOpen = false; rebindingIndex = -1; isSearchActive = false; }

    public bool Update(Vector2 mousePos, Action onToggleGrid, bool isGridVisible = true)
    {
        if (!IsOpen) return false;

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        if (justOpened) { justOpened = false; return true; }

        if (rebindingIndex >= 0)
        {
            int key = Raylib.GetKeyPressed();
            if (key > 0)
            {
                KeyboardKey k = (KeyboardKey)key;
                if (k == KeyboardKey.Escape) k = KeyboardKey.Null; 
                var filtered = GetFilteredShortcuts();
                if (rebindingIndex < filtered.Count)
                {
                    filtered[rebindingIndex].SetKey(k);
                    AppSettings.MarkAsCustomPreset();
                }
                rebindingIndex = -1;
            }
            return true;
        }

        float contentX = windowRect.X + 170;
        float contentY = windowRect.Y + 45;

        if (isSearchActive && activeCategory == 2)
        {
            int charKey = Raylib.GetCharPressed();
            while (charKey > 0)
            {
                if ((charKey >= 32) && (charKey <= 125) && searchQuery.Length < 25) searchQuery += (char)charKey;
                charKey = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && searchQuery.Length > 0) searchQuery = searchQuery[..^1];
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            float catY = windowRect.Y + 40;
            for (int i = 0; i < categories.Length; i++)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(windowRect.X, catY + (i * 38), 150, 38)))
                {
                    activeCategory = i; rebindingIndex = -1; isSearchActive = false;
                }
            }

            Rectangle closeBtn = new Rectangle(windowRect.X + windowRect.Width - 30, windowRect.Y + 5, 24, 24);
            Rectangle saveBtn = new Rectangle(windowRect.X + windowRect.Width - 110, windowRect.Y + windowRect.Height - 40, 90, 30);
            Rectangle resetBtn = new Rectangle(windowRect.X + windowRect.Width - 250, windowRect.Y + windowRect.Height - 40, 130, 30);

            if (Raylib.CheckCollisionPointRec(mousePos, closeBtn) || Raylib.CheckCollisionPointRec(mousePos, saveBtn)) { Close(); return true; }
            if (Raylib.CheckCollisionPointRec(mousePos, resetBtn)) { AppSettings.ResetToDefaults(); searchQuery = ""; }

            isSearchActive = false;

            if (activeCategory == 0) // GENEL (Tum degisiklikler profili Ozel olarak isaretler)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, contentY + 35, 28, 26)))
                {
                    AppSettings.MaxUndoSteps = Math.Max(5, AppSettings.MaxUndoSteps - 5);
                    AppSettings.MarkAsCustomPreset();
                }
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, contentY + 35, 28, 26)))
                {
                    AppSettings.MaxUndoSteps = Math.Min(200, AppSettings.MaxUndoSteps + 5);
                    AppSettings.MarkAsCustomPreset();
                }
                
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, contentY + 75, 130, 28)))
                {
                    AppSettings.EyedropperAutoSwitchPen = !AppSettings.EyedropperAutoSwitchPen;
                    AppSettings.MarkAsCustomPreset();
                }

                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, contentY + 115, 28, 26)))
                {
                    AppSettings.BlurIntensity = Math.Max(1, AppSettings.BlurIntensity - 1);
                    AppSettings.MarkAsCustomPreset();
                }
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, contentY + 115, 28, 26)))
                {
                    AppSettings.BlurIntensity = Math.Min(5, AppSettings.BlurIntensity + 1);
                    AppSettings.MarkAsCustomPreset();
                }

                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, contentY + 155, 28, 26)))
                {
                    AppSettings.JumbleIntensity = Math.Max(10, AppSettings.JumbleIntensity - 10);
                    AppSettings.MarkAsCustomPreset();
                }
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, contentY + 155, 28, 26)))
                {
                    AppSettings.JumbleIntensity = Math.Min(100, AppSettings.JumbleIntensity + 10);
                    AppSettings.MarkAsCustomPreset();
                }

                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, contentY + 195, 130, 28)))
                {
                    AppSettings.EnablePanelResize = !AppSettings.EnablePanelResize;
                    AppSettings.MarkAsCustomPreset();
                }
            }
            else if (activeCategory == 1) // GORUNUM VE PERFORMANS
            {
                PerformancePreset[] presets = { PerformancePreset.Varsayilan, PerformancePreset.Kalite, PerformancePreset.Performans, PerformancePreset.Ozel };
                for (int p = 0; p < presets.Length; p++)
                {
                    Rectangle pRect = new Rectangle(contentX + 145 + p * 98, contentY + 32, 92, 26);
                    if (Raylib.CheckCollisionPointRec(mousePos, pRect))
                    {
                        AppSettings.ApplyPreset(presets[p]);
                        return true;
                    }
                }

                Rectangle listBounds = GetPerfListBounds();
                if (Raylib.CheckCollisionPointRec(mousePos, listBounds))
                {
                    float y0 = listBounds.Y + 6f - perfScrollY;

                    // Satir 0: Panel Kayma Animasyonlari
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0, 130, 26)))
                    {
                        AppSettings.EnableUIAnimations = !AppSettings.EnableUIAnimations;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 1: Animasyon Hizi
                    if (AppSettings.EnableUIAnimations)
                    {
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight, 28, 26)))
                        {
                            AppSettings.UIAnimationSpeed = Math.Max(4f, AppSettings.UIAnimationSpeed - 2f);
                            AppSettings.MarkAsCustomPreset();
                        }
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, y0 + PerfRowHeight, 28, 26)))
                        {
                            AppSettings.UIAnimationSpeed = Math.Min(24f, AppSettings.UIAnimationSpeed + 2f);
                            AppSettings.MarkAsCustomPreset();
                        }
                    }
                    // Satir 2: Yumusak Kamera Zoom
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 2, 130, 26)))
                    {
                        AppSettings.SmoothCameraZoom = !AppSettings.SmoothCameraZoom;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 3: Hedef FPS Limiti
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 3, 28, 26)))
                    {
                        int idx = Array.IndexOf(FpsOptions, AppSettings.TargetFPS);
                        if (idx < 0) idx = FpsOptions.Length - 1;
                        AppSettings.TargetFPS = FpsOptions[Math.Max(0, idx - 1)];
                        Raylib.SetTargetFPS(AppSettings.TargetFPS);
                        AppSettings.MarkAsCustomPreset();
                    }
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, y0 + PerfRowHeight * 3, 28, 26)))
                    {
                        int idx = Array.IndexOf(FpsOptions, AppSettings.TargetFPS);
                        if (idx < 0) idx = FpsOptions.Length - 1;
                        AppSettings.TargetFPS = FpsOptions[Math.Min(FpsOptions.Length - 1, idx + 1)];
                        Raylib.SetTargetFPS(AppSettings.TargetFPS);
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 4: Firca Onizleme (Ghost)
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 4, 130, 26)))
                    {
                        AppSettings.ShowBrushPreview = !AppSettings.ShowBrushPreview;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 5: Panel Seffaf Arka Plan / Golge
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 5, 130, 26)))
                    {
                        AppSettings.ShowPanelBackdrops = !AppSettings.ShowPanelBackdrops;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 6: Arayuz Temasi
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 6, 130, 26)))
                    {
                        AppSettings.IsLightTheme = !AppSettings.IsLightTheme;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 7: Damali Zemin Kontrasti
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 7, 130, 26)))
                    {
                        AppSettings.CheckerContrast = (AppSettings.CheckerContrast + 1) % 3;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 8: Piksel Izgarasi
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 8, 130, 26)))
                    {
                        onToggleGrid();
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 9: Izgara Opakligi
                    if (isGridVisible)
                    {
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 9, 28, 26)))
                        {
                            AppSettings.GridOpacity = Math.Max(15, AppSettings.GridOpacity - 15);
                            AppSettings.MarkAsCustomPreset();
                        }
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, y0 + PerfRowHeight * 9, 28, 26)))
                        {
                            AppSettings.GridOpacity = Math.Min(165, AppSettings.GridOpacity + 15);
                            AppSettings.MarkAsCustomPreset();
                        }
                    }
                    // Satir 10: Sogan Zari (Onion Skin)
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 10, 130, 26)))
                    {
                        AppSettings.EnableOnionSkin = !AppSettings.EnableOnionSkin;
                        AppSettings.MarkAsCustomPreset();
                    }
                    // Satir 11: Sogan Zari Opakligi
                    if (AppSettings.EnableOnionSkin)
                    {
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, y0 + PerfRowHeight * 11, 28, 26)))
                        {
                            AppSettings.OnionSkinOpacity = Math.Max(25, AppSettings.OnionSkinOpacity - 25);
                            AppSettings.MarkAsCustomPreset();
                        }
                        if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 342, y0 + PerfRowHeight * 11, 28, 26)))
                        {
                            AppSettings.OnionSkinOpacity = Math.Min(225, AppSettings.OnionSkinOpacity + 25);
                            AppSettings.MarkAsCustomPreset();
                        }
                    }
                }
            }
            else if (activeCategory == 2) // KISAYOLLAR
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX, contentY + 35, 250, 30))) isSearchActive = true;
                
                var filtered = GetFilteredShortcuts();
                float listStartY = contentY + 80;
                for (int i = 0; i < filtered.Count; i++)
                {
                    float rowY = listStartY + (i * 32) - scrollY;
                    if (rowY < listStartY || rowY > windowRect.Y + windowRect.Height - 70) continue;
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 240, rowY, 140, 24))) { rebindingIndex = i; break; }
                }
            }
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0)
        {
            if (activeCategory == 1)
            {
                Rectangle listBounds = GetPerfListBounds();
                float totalContentH = PerfRowCount * PerfRowHeight + 12f;
                float maxPerfScroll = Math.Max(0f, totalContentH - listBounds.Height);
                perfScrollY = Math.Clamp(perfScrollY - (wheel * 30f), 0f, maxPerfScroll);
            }
            else if (activeCategory == 2)
            {
                float maxScroll = Math.Max(0, (GetFilteredShortcuts().Count * 32) - 310);
                scrollY = Math.Clamp(scrollY - (wheel * 30f), 0, maxScroll);
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (isSearchActive) { isSearchActive = false; searchQuery = ""; } else Close();
        }
        return true;
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
        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 160));
        Raylib.DrawRectangleRec(windowRect, new Color(32, 32, 36, 255));
        Raylib.DrawRectangleLinesEx(windowRect, 1, Color.SkyBlue);
        Raylib.DrawRectangle((int)windowRect.X, (int)windowRect.Y, (int)windowRect.Width, 32, new Color(45, 45, 52, 255));
        Raylib.DrawText("Ayarlar (Settings)", (int)windowRect.X + 12, (int)windowRect.Y + 9, 14, Color.RayWhite);

        Rectangle closeBtn = new Rectangle(windowRect.X + windowRect.Width - 30, windowRect.Y + 5, 24, 24);
        bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeBtn);
        if (closeHover) Raylib.DrawRectangleRec(closeBtn, new Color(200, 50, 50, 255));
        Raylib.DrawText("X", (int)closeBtn.X + 8, (int)closeBtn.Y + 5, 14, closeHover ? Color.White : Color.Gray);

        float leftPanelW = 150;
        Raylib.DrawRectangle((int)windowRect.X, (int)windowRect.Y + 32, (int)leftPanelW, (int)windowRect.Height - 32, new Color(26, 26, 30, 255));
        Raylib.DrawLine((int)(windowRect.X + leftPanelW), (int)windowRect.Y + 32, (int)(windowRect.X + leftPanelW), (int)(windowRect.Y + windowRect.Height), Color.DarkGray);

        float catY = windowRect.Y + 40;
        for (int i = 0; i < categories.Length; i++)
        {
            Rectangle catRect = new Rectangle(windowRect.X, catY + (i * 38), leftPanelW, 38);
            bool isSel = (activeCategory == i);
            bool isHover = Raylib.CheckCollisionPointRec(mousePos, catRect);
            if (isSel) { Raylib.DrawRectangleRec(catRect, new Color(45, 75, 115, 255)); Raylib.DrawRectangle((int)catRect.X, (int)catRect.Y, 4, (int)catRect.Height, Color.SkyBlue); }
            else if (isHover) Raylib.DrawRectangleRec(catRect, new Color(38, 38, 44, 255));
            Raylib.DrawText(categories[i], (int)catRect.X + 14, (int)catRect.Y + 12, 13, isSel ? Color.White : Color.LightGray);
        }

        float contentX = windowRect.X + leftPanelW + 20;
        float contentY = windowRect.Y + 45;

        if (activeCategory == 0)
        {
            Raylib.DrawText("Genel ve Arac Ayarlari", (int)contentX, (int)contentY, 16, Color.SkyBlue);
            Raylib.DrawLine((int)contentX, (int)contentY + 22, (int)(windowRect.X + windowRect.Width - 20), (int)contentY + 22, Color.DarkGray);
            
            Raylib.DrawText("Geri Alma (Undo) Limiti:", (int)contentX, (int)contentY + 42, 13, Color.LightGray);
            DrawStepper(contentX + 240, contentY + 35, AppSettings.MaxUndoSteps.ToString(), mousePos, true);

            Raylib.DrawText("Damlalik Sonrasi Kaleme Gec:", (int)contentX, (int)contentY + 82, 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, contentY + 75, 130, 28), AppSettings.EyedropperAutoSwitchPen, AppSettings.EyedropperAutoSwitchPen ? "Acik (Otomatik)" : "Kapali (Kalir)");

            Raylib.DrawText("Bulaniklastirma (Blur) Gucu:", (int)contentX, (int)contentY + 122, 13, Color.LightGray);
            DrawStepper(contentX + 240, contentY + 115, $"{AppSettings.BlurIntensity} / 5", mousePos, true);

            Raylib.DrawText("Karistirma (Jumble) Gucu:", (int)contentX, (int)contentY + 162, 13, Color.LightGray);
            DrawStepper(contentX + 240, contentY + 155, $"%{AppSettings.JumbleIntensity}", mousePos, true);

            Raylib.DrawText("Panelleri Serbest Boyutlandir:", (int)contentX, (int)contentY + 202, 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, contentY + 195, 130, 28), AppSettings.EnablePanelResize, AppSettings.EnablePanelResize ? "Acik" : "Kapali");
        }
        else if (activeCategory == 1)
        {
            Raylib.DrawText("Gorunum, Animasyon ve Performans", (int)contentX, (int)contentY, 16, Color.SkyBlue);
            Raylib.DrawLine((int)contentX, (int)contentY + 22, (int)(windowRect.X + windowRect.Width - 20), (int)contentY + 22, Color.DarkGray);

            Raylib.DrawText("Ayar Profili:", (int)contentX, (int)contentY + 38, 13, Color.Yellow);
            (string Label, PerformancePreset Preset)[] presetItems = {
                ("Varsayilan", PerformancePreset.Varsayilan),
                ("Kalite", PerformancePreset.Kalite),
                ("Performans", PerformancePreset.Performans),
                ("Ozel", PerformancePreset.Ozel)
            };

            for (int p = 0; p < presetItems.Length; p++)
            {
                Rectangle pRect = new Rectangle(contentX + 145 + p * 98, contentY + 32, 92, 26);
                bool isCurrent = AppSettings.CurrentPreset == presetItems[p].Preset;
                bool hover = Raylib.CheckCollisionPointRec(mousePos, pRect);

                Color bg = isCurrent
                    ? (presetItems[p].Preset == PerformancePreset.Ozel ? new Color(145, 95, 35, 255) : new Color(45, 95, 150, 255))
                    : (hover ? new Color(60, 60, 70, 255) : new Color(42, 42, 48, 255));

                Raylib.DrawRectangleRec(pRect, bg);
                Raylib.DrawRectangleLinesEx(pRect, 1, isCurrent ? Color.Yellow : Color.Gray);
                int tw = Raylib.MeasureText(presetItems[p].Label, 12);
                Raylib.DrawText(presetItems[p].Label, (int)(pRect.X + (pRect.Width - tw) / 2), (int)pRect.Y + 7, 12, isCurrent ? Color.Yellow : Color.White);
            }

            Rectangle listBounds = GetPerfListBounds();
            Raylib.DrawLine((int)contentX, (int)listBounds.Y - 2, (int)(windowRect.X + windowRect.Width - 20), (int)listBounds.Y - 2, new Color(55, 55, 65, 255));

            Raylib.BeginScissorMode((int)listBounds.X, (int)listBounds.Y, (int)listBounds.Width, (int)listBounds.Height);

            float y0 = listBounds.Y + 6f - perfScrollY;

            // 0. Panel Kayma Animasyonlari
            Raylib.DrawText("Panel Kayma Animasyonlari:", (int)contentX, (int)(y0 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0, 130, 26), AppSettings.EnableUIAnimations, AppSettings.EnableUIAnimations ? "Acik (Kayarak)" : "Kapali (Aninda)");

            // 1. Panel Animasyon Hizi
            Color animSpeedLabelCol = AppSettings.EnableUIAnimations ? Color.LightGray : Color.DarkGray;
            Raylib.DrawText("Panel Animasyon Hizi:", (int)contentX, (int)(y0 + PerfRowHeight + 6), 13, animSpeedLabelCol);
            DrawStepper(contentX + 240, y0 + PerfRowHeight, $"{(int)AppSettings.UIAnimationSpeed}x", mousePos, AppSettings.EnableUIAnimations);

            // 2. Yumusak Kamera Zoom
            Raylib.DrawText("Yumusak Kamera Zoom (Lerp):", (int)contentX, (int)(y0 + PerfRowHeight * 2 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0 + PerfRowHeight * 2, 130, 26), AppSettings.SmoothCameraZoom, AppSettings.SmoothCameraZoom ? "Acik (Akici)" : "Kapali (Keskin)");

            // 3. Hedef Kare Hizi (FPS)
            Raylib.DrawText("Uygulama Hedef FPS:", (int)contentX, (int)(y0 + PerfRowHeight * 3 + 6), 13, Color.LightGray);
            string fpsLabel = AppSettings.TargetFPS == 0 ? "Sinirsiz" : $"{AppSettings.TargetFPS} FPS";
            DrawStepper(contentX + 240, y0 + PerfRowHeight * 3, fpsLabel, mousePos, true);

            // 4. Firca Onizleme (Ghost)
            Raylib.DrawText("Firca Onizleme (Ghost):", (int)contentX, (int)(y0 + PerfRowHeight * 4 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0 + PerfRowHeight * 4, 130, 26), AppSettings.ShowBrushPreview, AppSettings.ShowBrushPreview ? "Acik" : "Kapali");

            // 5. Panel Seffaf Arka Plan / Golge
            Raylib.DrawText("Panel Seffaf Arka Plan / Golge:", (int)contentX, (int)(y0 + PerfRowHeight * 5 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0 + PerfRowHeight * 5, 130, 26), AppSettings.ShowPanelBackdrops, AppSettings.ShowPanelBackdrops ? "Acik" : "Kapali");

            // 6. Arayuz Temasi
            Raylib.DrawText("Arayuz Temasi:", (int)contentX, (int)(y0 + PerfRowHeight * 6 + 6), 13, Color.LightGray);
            Rectangle themeBtn = new Rectangle(contentX + 240, y0 + PerfRowHeight * 6, 130, 26);
            Raylib.DrawRectangleRec(themeBtn, new Color(45, 65, 95, 255));
            Raylib.DrawRectangleLinesEx(themeBtn, 1, Color.SkyBlue);
            string themeTxt = AppSettings.IsLightTheme ? "Acik (Light)" : "Koyu (Dark)";
            Raylib.DrawText(themeTxt, (int)(themeBtn.X + (themeBtn.Width - Raylib.MeasureText(themeTxt, 12)) / 2), (int)themeBtn.Y + 7, 12, Color.White);

            // 7. Damali Zemin Kontrasti
            Raylib.DrawText("Damali Zemin Kontrasti:", (int)contentX, (int)(y0 + PerfRowHeight * 7 + 6), 13, Color.LightGray);
            Rectangle chkBtn = new Rectangle(contentX + 240, y0 + PerfRowHeight * 7, 130, 26);
            Raylib.DrawRectangleRec(chkBtn, new Color(55, 55, 68, 255));
            Raylib.DrawRectangleLinesEx(chkBtn, 1, Color.Gray);
            string chkTxt = AppSettings.CheckerContrast switch { 0 => "Dusuk", 2 => "Yuksek", _ => "Orta" };
            Raylib.DrawText(chkTxt, (int)(chkBtn.X + (chkBtn.Width - Raylib.MeasureText(chkTxt, 12)) / 2), (int)chkBtn.Y + 7, 12, Color.Yellow);

            // 8. Piksel Izgarasi
            Raylib.DrawText("Piksel Izgarasi:", (int)contentX, (int)(y0 + PerfRowHeight * 8 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0 + PerfRowHeight * 8, 130, 26), isGridVisible, isGridVisible ? "Acik" : "Kapali");

            // 9. Izgara Opakligi
            Color gridOpLabelCol = isGridVisible ? Color.LightGray : Color.DarkGray;
            Raylib.DrawText("Izgara Opakligi:", (int)contentX, (int)(y0 + PerfRowHeight * 9 + 6), 13, gridOpLabelCol);
            DrawStepper(contentX + 240, y0 + PerfRowHeight * 9, $"%{(int)Math.Round(AppSettings.GridOpacity / 255f * 100)}", mousePos, isGridVisible);

            // 10. Sogan Zari (Onion Skin)
            Raylib.DrawText("Sogan Zari (Onion Skin):", (int)contentX, (int)(y0 + PerfRowHeight * 10 + 6), 13, Color.LightGray);
            DrawToggleBtn(new Rectangle(contentX + 240, y0 + PerfRowHeight * 10, 130, 26), AppSettings.EnableOnionSkin, AppSettings.EnableOnionSkin ? "Acik" : "Kapali");

            // 11. Sogan Zari Opakligi
            Color onionOpLabelCol = AppSettings.EnableOnionSkin ? Color.LightGray : Color.DarkGray;
            Raylib.DrawText("Sogan Zari Opakligi:", (int)contentX, (int)(y0 + PerfRowHeight * 11 + 6), 13, onionOpLabelCol);
            DrawStepper(contentX + 240, y0 + PerfRowHeight * 11, $"%{(int)Math.Round(AppSettings.OnionSkinOpacity / 255f * 100)}", mousePos, AppSettings.EnableOnionSkin);

            Raylib.EndScissorMode();

            // Kaydirma Cubugu (Scrollbar)
            float totalContentH = PerfRowCount * PerfRowHeight + 12f;
            float maxPerfScroll = Math.Max(0f, totalContentH - listBounds.Height);
            if (maxPerfScroll > 0)
            {
                float thumbH = Math.Max(24f, (listBounds.Height / totalContentH) * listBounds.Height);
                float thumbY = listBounds.Y + (perfScrollY / maxPerfScroll) * (listBounds.Height - thumbH);
                Raylib.DrawRectangle((int)(windowRect.X + windowRect.Width - 22), (int)thumbY, 6, (int)thumbH, Color.Gray);
            }
        }
        else if (activeCategory == 2)
        {
            Raylib.DrawText("Klavye Kisayollari", (int)contentX, (int)contentY, 16, Color.SkyBlue);
            
            Rectangle searchBox = new Rectangle(contentX, contentY + 35, 250, 30);
            Raylib.DrawRectangleRec(searchBox, new Color(18, 18, 22, 255));
            Raylib.DrawRectangleLinesEx(searchBox, isSearchActive ? 2 : 1, isSearchActive ? Color.SkyBlue : Color.Gray);
            string displaySearch = searchQuery;
            if (isSearchActive && (int)(Raylib.GetTime() * 2) % 2 == 0) displaySearch += "|";
            if (searchQuery == "" && !isSearchActive) Raylib.DrawText("Arama yap...", (int)searchBox.X + 8, (int)searchBox.Y + 8, 13, Color.DarkGray);
            else Raylib.DrawText(displaySearch, (int)searchBox.X + 8, (int)searchBox.Y + 8, 13, Color.White);

            float listStartY = contentY + 80;
            Raylib.DrawLine((int)contentX, (int)listStartY - 5, (int)(windowRect.X + windowRect.Width - 20), (int)listStartY - 5, Color.DarkGray);

            var filtered = GetFilteredShortcuts();
            Raylib.BeginScissorMode((int)contentX, (int)listStartY, (int)(windowRect.Width - leftPanelW - 20), (int)(windowRect.Height - 140));
            for (int i = 0; i < filtered.Count; i++)
            {
                float rowY = listStartY + (i * 32) - scrollY;
                if (rowY < listStartY - 32 || rowY > windowRect.Y + windowRect.Height - 50) continue;

                Raylib.DrawText(filtered[i].Name, (int)contentX, (int)rowY + 5, 13, Color.LightGray);
                Rectangle keyBtn = new Rectangle(contentX + 240, rowY, 140, 24);
                bool isWaiting = (rebindingIndex == i);
                bool hover = Raylib.CheckCollisionPointRec(mousePos, keyBtn);

                Raylib.DrawRectangleRec(keyBtn, isWaiting ? new Color(160, 90, 30, 255) : (hover ? new Color(65, 65, 75, 255) : new Color(48, 48, 55, 255)));
                Raylib.DrawRectangleLinesEx(keyBtn, 1, isWaiting ? Color.Yellow : Color.Gray);
                string keyLabel = isWaiting ? "Tusa Bas (Iptal: ESC)" : AppSettings.KeyToString(filtered[i].GetKey());
                int kw = Raylib.MeasureText(keyLabel, 12);
                Raylib.DrawText(keyLabel, (int)keyBtn.X + (140 - kw) / 2, (int)keyBtn.Y + 6, 12, isWaiting ? Color.White : (filtered[i].GetKey() == KeyboardKey.Null ? Color.Gray : Color.Yellow));
            }
            Raylib.EndScissorMode();

            float maxScroll = Math.Max(0, (filtered.Count * 32) - 310);
            if (maxScroll > 0)
            {
                float scrollThumbH = Math.Max(20, 310f / (filtered.Count * 32) * 310f);
                float scrollYPos = listStartY + (scrollY / maxScroll) * (310f - scrollThumbH);
                Raylib.DrawRectangle((int)(windowRect.X + windowRect.Width - 25), (int)scrollYPos, 6, (int)scrollThumbH, Color.Gray);
            }
        }

        Raylib.DrawLine((int)(windowRect.X + leftPanelW), (int)(windowRect.Y + windowRect.Height - 50), (int)(windowRect.X + windowRect.Width), (int)(windowRect.Y + windowRect.Height - 50), Color.DarkGray);
        
        Rectangle resetBtn = new Rectangle(windowRect.X + windowRect.Width - 250, windowRect.Y + windowRect.Height - 40, 130, 30);
        bool resetHover = Raylib.CheckCollisionPointRec(mousePos, resetBtn);
        Raylib.DrawRectangleRec(resetBtn, resetHover ? new Color(120, 40, 40, 255) : new Color(90, 35, 35, 255));
        Raylib.DrawRectangleLinesEx(resetBtn, 1, Color.LightGray);
        Raylib.DrawText("Varsayilana Sifirla", (int)resetBtn.X + 8, (int)resetBtn.Y + 8, 12, Color.White);

        Rectangle saveBtn = new Rectangle(windowRect.X + windowRect.Width - 110, windowRect.Y + windowRect.Height - 40, 90, 30);
        bool saveHover = Raylib.CheckCollisionPointRec(mousePos, saveBtn);
        Raylib.DrawRectangleRec(saveBtn, saveHover ? new Color(55, 130, 80, 255) : new Color(45, 115, 70, 255));
        Raylib.DrawRectangleLinesEx(saveBtn, 1, Color.LightGray);
        Raylib.DrawText("Tamam", (int)saveBtn.X + 24, (int)saveBtn.Y + 8, 14, Color.White);
    }

    private void DrawToggleBtn(Rectangle rect, bool state, string label)
    {
        Raylib.DrawRectangleRec(rect, state ? new Color(45, 100, 65, 255) : new Color(95, 45, 45, 255));
        Raylib.DrawRectangleLinesEx(rect, 1, Color.Gray);
        int tw = Raylib.MeasureText(label, 12);
        Raylib.DrawText(label, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 7, 12, Color.White);
    }

    private void DrawStepper(float x, float y, string valueText, Vector2 mousePos, bool enabled)
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
}