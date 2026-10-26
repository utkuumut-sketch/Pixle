using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class SettingsDialog
{
    public bool IsOpen { get; private set; } = false;
    private Rectangle windowRect = new Rectangle(0, 0, 700, 520);
    private int activeCategory = 0;
    private bool justOpened = false;

    private int rebindingIndex = -1; 
    private float scrollY = 0f;
    private string searchQuery = "";
    private bool isSearchActive = false;
    private readonly string[] categories = { "Genel", "Gorunum", "Kisayollar" };

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
        allShortcuts.Add(new ShortcutMap("Cizgi Araci", () => AppSettings.KeyLine, k => AppSettings.KeyLine = k));
        allShortcuts.Add(new ShortcutMap("Sekiller", () => AppSettings.KeyShape, k => AppSettings.KeyShape = k));
        allShortcuts.Add(new ShortcutMap("Secim / Tasi", () => AppSettings.KeySelectMove, k => AppSettings.KeySelectMove = k));
        allShortcuts.Add(new ShortcutMap("Katmanlari Gizle", () => AppSettings.KeyToggleLayers, k => AppSettings.KeyToggleLayers = k));

        for (int i = 0; i < 20; i++)
        {
            int index = i;
            allShortcuts.Add(new ShortcutMap($"Palet Renk {i + 1}", () => AppSettings.KeyColors[index], k => AppSettings.KeyColors[index] = k));
        }
    }

    public void Open()
    {
        IsOpen = true; justOpened = true; rebindingIndex = -1; isSearchActive = false; searchQuery = ""; scrollY = 0f;
        while (Raylib.GetKeyPressed() > 0) { } 
    }

    public void Close() { IsOpen = false; rebindingIndex = -1; isSearchActive = false; }

    public bool Update(Vector2 mousePos, Action onToggleGrid)
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
                if (rebindingIndex < filtered.Count) filtered[rebindingIndex].SetKey(k);
                rebindingIndex = -1;
            }
            return true;
        }

        float contentX = windowRect.X + 160;
        float contentY = windowRect.Y + 50;

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
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(windowRect.X, catY + (i * 38), 140, 38)))
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

            if (activeCategory == 0)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 210, contentY + 35, 28, 26))) AppSettings.MaxUndoSteps = Math.Max(5, AppSettings.MaxUndoSteps - 5);
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 285, contentY + 35, 28, 26))) AppSettings.MaxUndoSteps = Math.Min(200, AppSettings.MaxUndoSteps + 5);
                
                // YENİ: Damlalık Otomatik Kaleme Geçiş Butonu
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 210, contentY + 75, 130, 28)))
                    AppSettings.EyedropperAutoSwitchPen = !AppSettings.EyedropperAutoSwitchPen;
            }
            else if (activeCategory == 1)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 210, contentY + 35, 130, 28))) AppSettings.IsLightTheme = !AppSettings.IsLightTheme;
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 210, contentY + 75, 130, 28))) onToggleGrid();
                
                // YENİ: Panel Gölge / Şeffaf Arka Plan Butonu
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 210, contentY + 115, 130, 28)))
                    AppSettings.ShowPanelBackdrops = !AppSettings.ShowPanelBackdrops;
            }
            else if (activeCategory == 2)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX, contentY + 35, 250, 30))) isSearchActive = true;
                
                var filtered = GetFilteredShortcuts();
                float listStartY = contentY + 80;
                for (int i = 0; i < filtered.Count; i++)
                {
                    float rowY = listStartY + (i * 32) - scrollY;
                    if (rowY < listStartY || rowY > windowRect.Y + windowRect.Height - 70) continue;
                    if (Raylib.CheckCollisionPointRec(mousePos, new Rectangle(contentX + 220, rowY, 140, 24))) { rebindingIndex = i; break; }
                }
            }
        }

        if (activeCategory == 2)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                float maxScroll = Math.Max(0, (GetFilteredShortcuts().Count * 32) - 300);
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

        float leftPanelW = 140;
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
            Raylib.DrawText(categories[i], (int)catRect.X + 16, (int)catRect.Y + 12, 13, isSel ? Color.White : Color.LightGray);
        }

        float contentX = windowRect.X + leftPanelW + 20;
        float contentY = windowRect.Y + 50;

        if (activeCategory == 0)
        {
            Raylib.DrawText("Genel Ayarlar", (int)contentX, (int)contentY, 16, Color.SkyBlue);
            Raylib.DrawLine((int)contentX, (int)contentY + 22, (int)(windowRect.X + windowRect.Width - 20), (int)contentY + 22, Color.DarkGray);
            
            Raylib.DrawText("Geri Alma (Undo) Limiti:", (int)contentX, (int)contentY + 42, 13, Color.LightGray);
            Raylib.DrawRectangleLines((int)contentX + 210, (int)contentY + 35, 28, 26, Color.Gray);
            Raylib.DrawText("-", (int)contentX + 220, (int)contentY + 41, 15, Color.White);
            Raylib.DrawText(AppSettings.MaxUndoSteps.ToString(), (int)contentX + 250, (int)contentY + 41, 14, Color.Yellow);
            Raylib.DrawRectangleLines((int)contentX + 285, (int)contentY + 35, 28, 26, Color.Gray);
            Raylib.DrawText("+", (int)contentX + 294, (int)contentY + 41, 15, Color.White);

            // YENİ: Damlalık Otomatik Kaleme Geçiş Seçeneği
            Raylib.DrawText("Damlalik Sonrasi Kaleme Gec:", (int)contentX, (int)contentY + 82, 13, Color.LightGray);
            Rectangle eyeBtn = new Rectangle(contentX + 210, contentY + 75, 130, 28);
            Raylib.DrawRectangleRec(eyeBtn, AppSettings.EyedropperAutoSwitchPen ? new Color(45, 100, 65, 255) : new Color(95, 45, 45, 255));
            Raylib.DrawRectangleLinesEx(eyeBtn, 1, Color.Gray);
            string eyeTxt = AppSettings.EyedropperAutoSwitchPen ? "Acik (Otomatik)" : "Kapali (Kalir)";
            int etw = Raylib.MeasureText(eyeTxt, 12);
            Raylib.DrawText(eyeTxt, (int)(eyeBtn.X + (eyeBtn.Width - etw) / 2), (int)eyeBtn.Y + 8, 12, Color.White);
        }
        else if (activeCategory == 1)
        {
            Raylib.DrawText("Gorunum ve Tema", (int)contentX, (int)contentY, 16, Color.SkyBlue);
            Raylib.DrawLine((int)contentX, (int)contentY + 22, (int)(windowRect.X + windowRect.Width - 20), (int)contentY + 22, Color.DarkGray);
            
            Raylib.DrawText("Arayuz Temasi:", (int)contentX, (int)contentY + 42, 13, Color.LightGray);
            Raylib.DrawRectangleRec(new Rectangle(contentX + 210, contentY + 35, 130, 28), new Color(45, 65, 95, 255));
            Raylib.DrawRectangleLines((int)contentX + 210, (int)contentY + 35, 130, 28, Color.SkyBlue);
            Raylib.DrawText(AppSettings.IsLightTheme ? "Acik (Light)" : "Koyu (Dark)", (int)contentX + 224, (int)contentY + 42, 13, Color.White);
            
            Raylib.DrawText("Piksel Izgarasi:", (int)contentX, (int)contentY + 82, 13, Color.LightGray);
            Raylib.DrawRectangleRec(new Rectangle(contentX + 210, contentY + 75, 130, 28), isGridVisible ? new Color(45, 100, 65, 255) : new Color(95, 45, 45, 255));
            Raylib.DrawRectangleLines((int)contentX + 210, (int)contentY + 75, 130, 28, Color.Gray);
            Raylib.DrawText(isGridVisible ? "Acik" : "Kapali", (int)contentX + 252, (int)contentY + 82, 13, Color.White);

            // YENİ: Katman ve Animasyon Paneli Gölge / Şeffaflık Efekti Seçeneği
            Raylib.DrawText("Panel Seffaf Arka Plan / Golge:", (int)contentX, (int)contentY + 122, 13, Color.LightGray);
            Rectangle bdBtn = new Rectangle(contentX + 210, contentY + 115, 130, 28);
            Raylib.DrawRectangleRec(bdBtn, AppSettings.ShowPanelBackdrops ? new Color(45, 100, 65, 255) : new Color(95, 45, 45, 255));
            Raylib.DrawRectangleLinesEx(bdBtn, 1, Color.Gray);
            Raylib.DrawText(AppSettings.ShowPanelBackdrops ? "Acik" : "Kapali", (int)bdBtn.X + 42, (int)bdBtn.Y + 8, 13, Color.White);
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
                Rectangle keyBtn = new Rectangle(contentX + 220, rowY, 140, 24);
                bool isWaiting = (rebindingIndex == i);
                bool hover = Raylib.CheckCollisionPointRec(mousePos, keyBtn);

                Raylib.DrawRectangleRec(keyBtn, isWaiting ? new Color(160, 90, 30, 255) : (hover ? new Color(65, 65, 75, 255) : new Color(48, 48, 55, 255)));
                Raylib.DrawRectangleLinesEx(keyBtn, 1, isWaiting ? Color.Yellow : Color.Gray);
                string keyLabel = isWaiting ? "Tusa Bas (Iptal: ESC)" : AppSettings.KeyToString(filtered[i].GetKey());
                int kw = Raylib.MeasureText(keyLabel, 12);
                Raylib.DrawText(keyLabel, (int)keyBtn.X + (140 - kw) / 2, (int)keyBtn.Y + 6, 12, isWaiting ? Color.White : (filtered[i].GetKey() == KeyboardKey.Null ? Color.Gray : Color.Yellow));
            }
            Raylib.EndScissorMode();

            float maxScroll = Math.Max(0, (filtered.Count * 32) - 300);
            if (maxScroll > 0)
            {
                float scrollThumbH = Math.Max(20, 300f / (filtered.Count * 32) * 300f);
                float scrollYPos = listStartY + (scrollY / maxScroll) * (300f - scrollThumbH);
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
}