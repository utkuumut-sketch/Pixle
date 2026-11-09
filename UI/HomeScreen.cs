using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public class HomeScreen
{
    private readonly List<string> recentFiles = new();
    private readonly string recentFileStorePath = Path.Combine(AppContext.BaseDirectory, "recent_projects.txt");

    public HomeScreen()
    {
        LoadRecentFiles();
    }

    public void AddRecentFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        recentFiles.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        recentFiles.Insert(0, path);
        if (recentFiles.Count > 10) recentFiles.RemoveAt(recentFiles.Count - 1);
        SaveRecentFiles();
    }

    private void LoadRecentFiles()
    {
        try
        {
            if (File.Exists(recentFileStorePath))
            {
                var lines = File.ReadAllLines(recentFileStorePath);
                foreach (var l in lines)
                {
                    if (!string.IsNullOrWhiteSpace(l) && File.Exists(l))
                        recentFiles.Add(l);
                }
            }
        }
        catch { }
    }

    private void SaveRecentFiles()
    {
        try
        {
            File.WriteAllLines(recentFileStorePath, recentFiles);
        }
        catch { }
    }

    public void Update(Vector2 mousePos, int sw, int sh, float topY, Action onNewProject, Action onOpenDialog, Action<string> onOpenRecent)
    {
        if (mousePos.Y < topY) return;

        Rectangle newBtn = new Rectangle(80, topY + 125, 290, 56);
        Rectangle openBtn = new Rectangle(80, topY + 193, 290, 56);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, newBtn))
            {
                onNewProject();
                return;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, openBtn))
            {
                onOpenDialog();
                return;
            }

            float recentX = 410;
            float recentY = topY + 125;
            float recentW = Math.Max(280, sw - recentX - 70);

            for (int i = 0; i < recentFiles.Count; i++)
            {
                Rectangle itemRect = new Rectangle(recentX, recentY + i * 46, recentW, 40);
                if (Raylib.CheckCollisionPointRec(mousePos, itemRect))
                {
                    if (File.Exists(recentFiles[i]))
                    {
                        onOpenRecent(recentFiles[i]);
                    }
                    return;
                }
            }
        }
    }

    public void Draw(int sw, int sh, float topY)
    {
        Raylib.DrawRectangle(0, (int)topY, sw, sh - (int)topY, PixelSkin.WindowBg);

        // Başlık ve 48x48 piksel logo
        PixelIcons.DrawIcon("app_logo", 80, (int)topY + 38, 48f);
        Raylib.DrawText("PIXEL ART STUDIO", 142, (int)topY + 42, 28, Color.RayWhite);
        Raylib.DrawText("Aseprite & Photoshop Stil Piksel Sanati ve Animasyon Atolyesi", 144, (int)topY + 74, 13, PixelSkin.AccentCyan);

        Vector2 mousePos = Raylib.GetMousePosition();

        Rectangle newBtn = new Rectangle(80, topY + 125, 290, 56);
        Rectangle openBtn = new Rectangle(80, topY + 193, 290, 56);

        DrawHomeButton(newBtn, "tool_pen", "+ Yeni Proje Olustur", "Tuval boyutu ve zemin sec", true, mousePos);
        DrawHomeButton(openBtn, "tool_smartbucket", "Dosya Ac / Ice Aktar", ".loom veya PNG dosyasi ac", false, mousePos);

        // Kısayol Bilgi Kutusu
        Rectangle infoBox = new Rectangle(80, topY + 268, 290, 215);
        PixelSkin.DrawInsetBox(infoBox);
        Raylib.DrawText("STUDYO KISAYOLLARI", (int)infoBox.X + 14, (int)infoBox.Y + 12, 12, PixelSkin.AccentGold);
        Raylib.DrawLine((int)infoBox.X + 12, (int)infoBox.Y + 28, (int)(infoBox.X + infoBox.Width - 12), (int)infoBox.Y + 28, PixelSkin.PanelBorderLight);

        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 38, "Ctrl + N", "Yeni Proje");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 58, "Ctrl + O", "Proje / Gorsel Ac");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 78, "Ctrl + S", "Projeyi Kaydet (.loom)");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 98, "Ctrl + E", "PNG / GIF / Sheet Aktar");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 118, "P / S / A / T / G", "Asistanlar & Karo");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 138, "1..5, D,H,C,L,U,M", "Cizim Araclari");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 158, "Alt + Tik", "Hizli Damlalik");
        DrawShortcutRow(infoBox.X + 14, infoBox.Y + 178, "Orta Tus", "Tuvali Kaydir (Pan)");

        // SAĞ BÖLÜM: Son Açılan Projeler (Recent Projects)
        float recentX = 410;
        float recentY = topY + 125;
        float recentW = Math.Max(280, sw - recentX - 70);

        Raylib.DrawText("SON ACILAN PROJELER (RECENT)", (int)recentX, (int)recentY - 26, 14, PixelSkin.AccentGold);
        Raylib.DrawLine((int)recentX, (int)recentY - 8, (int)(recentX + recentW), (int)recentY - 8, PixelSkin.PanelBorderLight);

        if (recentFiles.Count == 0)
        {
            Rectangle emptyBox = new Rectangle(recentX, recentY, recentW, 130);
            PixelSkin.DrawInsetBox(emptyBox);
            PixelIcons.DrawIcon("tool_pen", recentX + 20, recentY + 46, 32f);
            Raylib.DrawText("Henuz acilmis veya kaydedilmis bir proje yok.", (int)recentX + 64, (int)recentY + 46, 13, PixelSkin.TextPrimary);
            Raylib.DrawText("Sol taraftan '+ Yeni Proje Olustur' ile baslayabilirsin.", (int)recentX + 64, (int)recentY + 68, 12, PixelSkin.TextMuted);
        }
        else
        {
            for (int i = 0; i < recentFiles.Count; i++)
            {
                Rectangle itemRect = new Rectangle(recentX, recentY + i * 46, recentW, 40);
                bool hover = Raylib.CheckCollisionPointRec(mousePos, itemRect);

                PixelSkin.DrawButton(itemRect, hover, false);
                PixelIcons.DrawIcon("app_logo", itemRect.X + 10, itemRect.Y + 12, 16f);

                string fileName = Path.GetFileName(recentFiles[i]);
                Raylib.DrawText(fileName, (int)itemRect.X + 34, (int)itemRect.Y + 6, 14, hover ? PixelSkin.AccentGold : Color.White);
                Raylib.DrawText(recentFiles[i], (int)itemRect.X + 34, (int)itemRect.Y + 23, 10, PixelSkin.TextMuted);
            }
        }
    }

    private void DrawShortcutRow(float x, float y, string key, string desc)
    {
        Raylib.DrawText(key, (int)x, (int)y, 11, PixelSkin.AccentCyan);
        Raylib.DrawText(desc, (int)x + 102, (int)y, 11, PixelSkin.TextPrimary);
    }

    private void DrawHomeButton(Rectangle rect, string iconName, string title, string subtitle, bool primary, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        PixelSkin.DrawButton(rect, hover, primary);

        PixelIcons.DrawIcon(iconName, rect.X + 12, rect.Y + 12, 32f);
        Raylib.DrawText(title, (int)rect.X + 54, (int)rect.Y + 11, 14, Color.White);
        Raylib.DrawText(subtitle, (int)rect.X + 54, (int)rect.Y + 31, 11, primary ? new Color((byte)220, (byte)242, (byte)230, (byte)255) : PixelSkin.TextMuted);
    }
}
