using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class TabBar
{
    public float TopOffset { get; } = 36f;
    public float Height { get; } = 36f; 

    public bool Update(Vector2 mousePos, List<ProjectTab> tabs, int activeTabIndex, Action<int> onSelectTab, Action<int> onCloseTab, Action onNewTabClick)
    {
        if (mousePos.Y < TopOffset || mousePos.Y > TopOffset + Height) return false;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // 1. Home Sekmesi Hitbox'ı
            Rectangle homeRect = new Rectangle(6, TopOffset, 85, Height);
            if (Raylib.CheckCollisionPointRec(mousePos, homeRect))
            {
                onSelectTab(-1);
                return true;
            }

            // 2. Proje Sekmeleri Hitbox'ları
            float curX = 96;
            int tabW = 140;

            for (int i = 0; i < tabs.Count; i++)
            {
                Rectangle tabRect = new Rectangle(curX, TopOffset, tabW, Height);
                Rectangle closeRect = new Rectangle(tabRect.X + tabW - 26, tabRect.Y + 10, 20, 20);

                if (Raylib.CheckCollisionPointRec(mousePos, closeRect))
                {
                    onCloseTab(i);
                    return true;
                }
                else if (Raylib.CheckCollisionPointRec(mousePos, tabRect))
                {
                    onSelectTab(i);
                    return true;
                }
                curX += tabW + 4;
            }

            // 3. Hızlı "+" (Yeni Proje) Sekme Butonu
            Rectangle plusRect = new Rectangle(curX, TopOffset + 6, 28, Height - 10);
            if (Raylib.CheckCollisionPointRec(mousePos, plusRect))
            {
                onNewTabClick();
                return true;
            }
        }

        return true;
    }

    public void Draw(List<ProjectTab> tabs, int activeTabIndex, int screenWidth)
    {
        // 1. Arka Plan ve Ana Ayırıcı Alt Çizgi
        Raylib.DrawRectangle(0, (int)TopOffset, screenWidth, (int)Height, new Color(20, 20, 24, 255));
        int bottomLineY = (int)(TopOffset + Height);
        Raylib.DrawLine(0, bottomLineY, screenWidth, bottomLineY, Color.DarkGray);

        Vector2 mousePos = Raylib.GetMousePosition();
        bool isHomeActive = (activeTabIndex == -1);

        // Geometri Hazırlığı
        Rectangle homeRect = new Rectangle(6, TopOffset + 6, 85, Height - 6);
        List<Rectangle> tabRects = new();
        float curX = 96;
        int tabW = 140;

        for (int i = 0; i < tabs.Count; i++)
        {
            tabRects.Add(new Rectangle(curX, TopOffset + 6, tabW, Height - 6));
            curX += tabW + 4;
        }
        Rectangle plusRect = new Rectangle(curX, TopOffset + 6, 28, Height - 7);

        // ==========================================
        // ÖNCE İNAKTİF (ARKADA KALAN) SEKMELERİ ÇİZ
        // ==========================================

        if (!isHomeActive)
        {
            bool hover = Raylib.CheckCollisionPointRec(mousePos, homeRect);
            Raylib.DrawRectangleRec(homeRect, hover ? new Color(38, 38, 44, 255) : new Color(28, 28, 32, 255));
            Raylib.DrawRectangleLinesEx(homeRect, 1, Color.DarkGray);
            Raylib.DrawText("Home", (int)homeRect.X + 24, (int)homeRect.Y + 8, 14, Color.Gray);
        }

        for (int i = 0; i < tabs.Count; i++)
        {
            if (i == activeTabIndex) continue; // Aktif olanı sonra çizeceğiz

            Rectangle tRect = tabRects[i];
            bool hover = Raylib.CheckCollisionPointRec(mousePos, tRect);
            
            Raylib.DrawRectangleRec(tRect, hover ? new Color(38, 38, 44, 255) : new Color(28, 28, 32, 255));
            Raylib.DrawRectangleLinesEx(tRect, 1, Color.DarkGray);
            
            string title = tabs[i].Title;
            if (title.Length > 14) title = title[..14] + "..";
            Raylib.DrawText(title, (int)tRect.X + 10, (int)tRect.Y + 8, 13, Color.Gray);
            
            Rectangle closeRect = new Rectangle(tRect.X + tabW - 26, tRect.Y + 4, 20, 20);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRounded(closeRect, 0.2f, 4, new Color(180, 45, 45, 255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 2, 14, closeHover ? Color.White : Color.Gray);
        }

        // Yeni Sekme (+) Butonu
        bool plusHover = Raylib.CheckCollisionPointRec(mousePos, plusRect);
        Raylib.DrawRectangleRec(plusRect, plusHover ? new Color(55, 55, 65, 255) : new Color(35, 35, 40, 255));
        Raylib.DrawRectangleLinesEx(plusRect, 1, Color.DarkGray);
        Raylib.DrawText("+", (int)plusRect.X + 8, (int)plusRect.Y + 3, 18, Color.LightGray);

        // ==========================================
        // SONRA AKTİF (ÖNDE OLAN) SEKMEYİ ÇİZ
        // ==========================================
        // Aktif sekme biraz daha yukarıdan başlar ve alt çizgiyi örtecek kadar uzundur.
        
        if (isHomeActive)
        {
            Rectangle aRect = new Rectangle(homeRect.X, TopOffset + 3, homeRect.Width, Height - 2); 
            
            Raylib.DrawRectangleRec(aRect, new Color(45, 75, 115, 255)); // Aktif Arkaplan
            
            // Alt kenar HARİÇ çerçeve çizgileri çizilir (Bu sayede alt kısımla birleşir)
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)aRect.Y, Color.SkyBlue); // Üst
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), Color.SkyBlue); // Sol
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), Color.SkyBlue); // Sağ
            
            Raylib.DrawText("Home", (int)aRect.X + 24, (int)aRect.Y + 11, 14, Color.Yellow);
        }
        else if (activeTabIndex >= 0 && activeTabIndex < tabs.Count)
        {
            Rectangle baseRect = tabRects[activeTabIndex];
            Rectangle aRect = new Rectangle(baseRect.X, TopOffset + 3, baseRect.Width, Height - 2);
            
            Raylib.DrawRectangleRec(aRect, new Color(48, 48, 55, 255)); // Aktif Arkaplan
            
            // Alt kenar HARİÇ çerçeve çizgileri çizilir
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)aRect.Y, Color.SkyBlue); // Üst
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), Color.SkyBlue); // Sol
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), Color.SkyBlue); // Sağ
            
            string title = tabs[activeTabIndex].Title;
            if (title.Length > 14) title = title[..14] + "..";
            Raylib.DrawText(title, (int)aRect.X + 10, (int)aRect.Y + 11, 13, Color.White);
            
            // X Butonu (Aktif Sekme için)
            Rectangle closeRect = new Rectangle(aRect.X + tabW - 26, aRect.Y + 7, 20, 20);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRounded(closeRect, 0.2f, 4, new Color(180, 45, 45, 255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 3, 14, closeHover ? Color.White : Color.Gray);
        }
    }
}