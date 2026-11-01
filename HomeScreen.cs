using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public class HomeScreen
{
    private readonly List<string> recentFiles = new();
    private readonly string recentFileStorePath = "recent_projects.txt";

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

        Rectangle newBtn = new Rectangle(80, topY + 120, 240, 52);
        Rectangle openBtn = new Rectangle(80, topY + 188, 240, 52);

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

            // Sağdaki Son Projeler Listesi
            float recentX = 390;
            float recentY = topY + 120;
            float recentW = sw - recentX - 80;

            for (int i = 0; i < recentFiles.Count; i++)
            {
                Rectangle itemRect = new Rectangle(recentX, recentY + i * 44, recentW, 38);
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
        Raylib.DrawRectangle(0, (int)topY, sw, sh - (int)topY, new Color(24, 24, 28, 255));

        // Başlık
        Raylib.DrawText("PIXEL ART STUDIO", 80, (int)topY + 45, 28, Color.RayWhite);
        Raylib.DrawText("v1.0 - Aseprite & Photoshop StilCalisma Alani", 80, (int)topY + 80, 13, Color.SkyBlue);

        Vector2 mousePos = Raylib.GetMousePosition();

        // SOL BÖLÜM: Yeni Proje & Dosya Aç
        Rectangle newBtn = new Rectangle(80, topY + 120, 240, 52);
        Rectangle openBtn = new Rectangle(80, topY + 188, 240, 52);

        DrawHomeButton(newBtn, "+  Yeni Proje Olustur", "Boyut ve arka plan secerek basla", new Color(45, 95, 150, 255), mousePos);
        DrawHomeButton(openBtn, "   Dosya Ac / Ice Aktar", "PNG gorselini yeni sekmede ac", new Color(50, 50, 60, 255), mousePos);

        // Kısayol Bilgi Kutusu
       Rectangle infoBox = new Rectangle(80, topY + 270, 240, 170);
Raylib.DrawRectangleRec(infoBox, new Color(30, 30, 35, 255));
Raylib.DrawRectangleLinesEx(infoBox, 1, new Color(55, 55, 65, 255));
Raylib.DrawText("Hizli Kisayollar:", (int)infoBox.X + 12, (int)infoBox.Y + 12, 13, Color.Yellow);
Raylib.DrawText("Ctrl + N : Yeni Proje", (int)infoBox.X + 12, (int)infoBox.Y + 36, 12, Color.LightGray);
Raylib.DrawText("Ctrl + O : Proje / Gorsel Ac", (int)infoBox.X + 12, (int)infoBox.Y + 56, 12, Color.LightGray);
Raylib.DrawText("Ctrl + S : Projeyi Kaydet (.loom)", (int)infoBox.X + 12, (int)infoBox.Y + 76, 12, Color.LightGray);
Raylib.DrawText("Ctrl+Shift+S : Farkli Kaydet", (int)infoBox.X + 12, (int)infoBox.Y + 96, 12, Color.LightGray);
Raylib.DrawText("Ctrl + E : PNG Disa Aktar", (int)infoBox.X + 12, (int)infoBox.Y + 116, 12, Color.LightGray);
Raylib.DrawText("Ctrl + W : Sekmeyi Kapat", (int)infoBox.X + 12, (int)infoBox.Y + 136, 12, Color.LightGray);
        

        // SAĞ BÖLÜM: Son Açılan Projeler (Recent Projects)
        float recentX = 390;
        float recentY = topY + 120;
        float recentW = Math.Max(300, sw - recentX - 80);

        Raylib.DrawText("SON ACILAN PROJELER (RECENT)", (int)recentX, (int)recentY - 30, 15, Color.SkyBlue);
        Raylib.DrawLine((int)recentX, (int)recentY - 10, (int)(recentX + recentW), (int)recentY - 10, Color.DarkGray);

        if (recentFiles.Count == 0)
        {
            Raylib.DrawRectangle((int)recentX, (int)recentY, (int)recentW, 120, new Color(30, 30, 35, 255));
            Raylib.DrawText("Henuz acilmis veya kaydedilmis bir proje yok.", (int)recentX + 20, (int)recentY + 50, 14, Color.Gray);
        }
        else
        {
            for (int i = 0; i < recentFiles.Count; i++)
            {
                Rectangle itemRect = new Rectangle(recentX, recentY + i * 44, recentW, 38);
                bool hover = Raylib.CheckCollisionPointRec(mousePos, itemRect);

                Raylib.DrawRectangleRec(itemRect, hover ? new Color(45, 55, 70, 255) : new Color(32, 32, 38, 255));
                Raylib.DrawRectangleLinesEx(itemRect, 1, hover ? Color.SkyBlue : new Color(50, 50, 58, 255));

                string fileName = Path.GetFileName(recentFiles[i]);
                Raylib.DrawText(fileName, (int)itemRect.X + 14, (int)itemRect.Y + 6, 14, Color.White);
                Raylib.DrawText(recentFiles[i], (int)itemRect.X + 14, (int)itemRect.Y + 22, 10, Color.Gray);
            }
        }
    }

    private void DrawHomeButton(Rectangle rect, string title, string subtitle, Color baseCol, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Color col = hover
            ? new Color((byte)Math.Min(255, baseCol.R + 25), (byte)Math.Min(255, baseCol.G + 25), (byte)Math.Min(255, baseCol.B + 25), (byte)255)
            : baseCol;

        Raylib.DrawRectangleRec(rect, col);
        Raylib.DrawRectangleLinesEx(rect, 1, hover ? Color.SkyBlue : Color.Gray);
        Raylib.DrawText(title, (int)rect.X + 14, (int)rect.Y + 10, 15, Color.White);
        Raylib.DrawText(subtitle, (int)rect.X + 14, (int)rect.Y + 30, 11, Color.LightGray);
    }
}