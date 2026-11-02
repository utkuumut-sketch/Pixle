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

    private float scrollX = 0f;
    private int lastActiveTabIndex = -2;
    private int lastTabCount = 0;

    private const float HomeEndX = 96f;
    private const int TabWidth = 140;
    private const int TabSpacing = 4;
    private const float NavAreaWidth = 92f; // '<', '>' ve '+' butonları için sağda ayrılan güvenli alan

    private void EnsureActiveTabVisible(int activeTabIndex, int tabCount, float viewportWidth)
    {
        float totalContentW = tabCount * (TabWidth + TabSpacing);
        float maxScroll = Math.Max(0f, totalContentW - viewportWidth);

        if (activeTabIndex >= 0 && activeTabIndex < tabCount)
        {
            float tabStart = activeTabIndex * (TabWidth + TabSpacing);
            float tabEnd = tabStart + TabWidth;

            if (tabStart < scrollX)
                scrollX = tabStart;
            else if (tabEnd > scrollX + viewportWidth)
                scrollX = tabEnd - viewportWidth;
        }

        scrollX = Math.Clamp(scrollX, 0f, maxScroll);
    }

    public bool Update(Vector2 mousePos, List<ProjectTab> tabs, int activeTabIndex, Action<int> onSelectTab, Action<int> onCloseTab, Action onNewTabClick)
    {
        int screenWidth = Raylib.GetScreenWidth();
        float viewportWidth = Math.Max(TabWidth, screenWidth - HomeEndX - NavAreaWidth);
        float totalContentW = tabs.Count * (TabWidth + TabSpacing);
        float maxScroll = Math.Max(0f, totalContentW - viewportWidth);

        if (activeTabIndex != lastActiveTabIndex || tabs.Count != lastTabCount)
        {
            EnsureActiveTabVisible(activeTabIndex, tabs.Count, viewportWidth);
            lastActiveTabIndex = activeTabIndex;
            lastTabCount = tabs.Count;
        }
        else
        {
            scrollX = Math.Clamp(scrollX, 0f, maxScroll);
        }

        if (mousePos.Y < TopOffset || mousePos.Y > TopOffset + Height) return false;

        // Fare tekerleği ile sekmeleri yatay kaydır
        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0 && maxScroll > 0f)
        {
            scrollX = Math.Clamp(scrollX - wheel * 60f, 0f, maxScroll);
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            Rectangle homeRect = new Rectangle(6, TopOffset, 85, Height);
            if (Raylib.CheckCollisionPointRec(mousePos, homeRect))
            {
                onSelectTab(-1);
                return true;
            }

            // Sağ taraftaki kaydırma ('<', '>') ve yeni sekme ('+') butonları
            float navStartX = HomeEndX + Math.Min(totalContentW, viewportWidth) + 4f;
            if (maxScroll > 0f)
            {
                Rectangle leftBtn = new Rectangle(navStartX, TopOffset + 6, 24, Height - 10);
                Rectangle rightBtn = new Rectangle(navStartX + 26, TopOffset + 6, 24, Height - 10);
                Rectangle plusRectOverflow = new Rectangle(navStartX + 54, TopOffset + 6, 28, Height - 10);

                if (Raylib.CheckCollisionPointRec(mousePos, leftBtn))
                {
                    scrollX = Math.Clamp(scrollX - (TabWidth + TabSpacing), 0f, maxScroll);
                    return true;
                }
                if (Raylib.CheckCollisionPointRec(mousePos, rightBtn))
                {
                    scrollX = Math.Clamp(scrollX + (TabWidth + TabSpacing), 0f, maxScroll);
                    return true;
                }
                if (Raylib.CheckCollisionPointRec(mousePos, plusRectOverflow))
                {
                    onNewTabClick();
                    return true;
                }
            }
            else
            {
                Rectangle plusRect = new Rectangle(navStartX, TopOffset + 6, 28, Height - 10);
                if (Raylib.CheckCollisionPointRec(mousePos, plusRect))
                {
                    onNewTabClick();
                    return true;
                }
            }

            // Sekme görünür alanı içindeki tıklamalar
            Rectangle viewportRect = new Rectangle(HomeEndX, TopOffset, viewportWidth, Height);
            if (Raylib.CheckCollisionPointRec(mousePos, viewportRect))
            {
                for (int i = 0; i < tabs.Count; i++)
                {
                    float tabX = HomeEndX + i * (TabWidth + TabSpacing) - scrollX;
                    Rectangle tabRect = new Rectangle(tabX, TopOffset, TabWidth, Height);
                    Rectangle closeRect = new Rectangle(tabRect.X + TabWidth - 26, tabRect.Y + 10, 20, 20);

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
                }
            }
        }

        return true;
    }

    public void Draw(List<ProjectTab> tabs, int activeTabIndex, int screenWidth)
    {
        float viewportWidth = Math.Max(TabWidth, screenWidth - HomeEndX - NavAreaWidth);
        float totalContentW = tabs.Count * (TabWidth + TabSpacing);
        float maxScroll = Math.Max(0f, totalContentW - viewportWidth);

        if (activeTabIndex != lastActiveTabIndex || tabs.Count != lastTabCount)
        {
            EnsureActiveTabVisible(activeTabIndex, tabs.Count, viewportWidth);
            lastActiveTabIndex = activeTabIndex;
            lastTabCount = tabs.Count;
        }

        Raylib.DrawRectangle(0, (int)TopOffset, screenWidth, (int)Height, new Color(20, 20, 24, 255));
        int bottomLineY = (int)(TopOffset + Height);
        Raylib.DrawLine(0, bottomLineY, screenWidth, bottomLineY, Color.DarkGray);

        Vector2 mousePos = Raylib.GetMousePosition();
        bool isHomeActive = (activeTabIndex == -1);

        Rectangle homeRect = new Rectangle(6, TopOffset + 6, 85, Height - 6);
        if (!isHomeActive)
        {
            bool hover = Raylib.CheckCollisionPointRec(mousePos, homeRect);
            Raylib.DrawRectangleRec(homeRect, hover ? new Color(38, 38, 44, 255) : new Color(28, 28, 32, 255));
            Raylib.DrawRectangleLinesEx(homeRect, 1, Color.DarkGray);
            Raylib.DrawText("Home", (int)homeRect.X + 24, (int)homeRect.Y + 8, 14, Color.Gray);
        }
        else
        {
            Rectangle aRect = new Rectangle(homeRect.X, TopOffset + 3, homeRect.Width, Height - 2);
            Raylib.DrawRectangleRec(aRect, new Color(45, 75, 115, 255));
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)aRect.Y, Color.SkyBlue);
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), Color.SkyBlue);
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), Color.SkyBlue);
            Raylib.DrawText("Home", (int)aRect.X + 24, (int)aRect.Y + 11, 14, Color.Yellow);
        }

        // Sekmeleri görünür alan içinde kırparak çiz
        Raylib.BeginScissorMode((int)HomeEndX, (int)TopOffset, (int)viewportWidth, (int)Height + 2);

        for (int i = 0; i < tabs.Count; i++)
        {
            if (i == activeTabIndex) continue;

            float tabX = HomeEndX + i * (TabWidth + TabSpacing) - scrollX;
            if (tabX + TabWidth < HomeEndX || tabX > HomeEndX + viewportWidth) continue;

            Rectangle tRect = new Rectangle(tabX, TopOffset + 6, TabWidth, Height - 6);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, tRect);

            Raylib.DrawRectangleRec(tRect, hover ? new Color(38, 38, 44, 255) : new Color(28, 28, 32, 255));
            Raylib.DrawRectangleLinesEx(tRect, 1, Color.DarkGray);

            string title = tabs[i].GetDisplayTitle();
            if (title.Length > 14) title = title[..14] + "..";
            Raylib.DrawText(title, (int)tRect.X + 10, (int)tRect.Y + 8, 13, tabs[i].IsDirty ? Color.Yellow : Color.Gray);

            Rectangle closeRect = new Rectangle(tRect.X + TabWidth - 26, tRect.Y + 4, 20, 20);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRounded(closeRect, 0.2f, 4, new Color(180, 45, 45, 255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 2, 14, closeHover ? Color.White : Color.Gray);
        }

        if (activeTabIndex >= 0 && activeTabIndex < tabs.Count)
        {
            float activeX = HomeEndX + activeTabIndex * (TabWidth + TabSpacing) - scrollX;
            Rectangle aRect = new Rectangle(activeX, TopOffset + 3, TabWidth, Height - 2);

            Raylib.DrawRectangleRec(aRect, new Color(48, 48, 55, 255));
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)aRect.Y, Color.SkyBlue);
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), Color.SkyBlue);
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), Color.SkyBlue);

            string title = tabs[activeTabIndex].GetDisplayTitle();
            if (title.Length > 14) title = title[..14] + "..";
            Raylib.DrawText(title, (int)aRect.X + 10, (int)aRect.Y + 11, 13, tabs[activeTabIndex].IsDirty ? Color.Yellow : Color.White);

            Rectangle closeRect = new Rectangle(aRect.X + TabWidth - 26, aRect.Y + 7, 20, 20);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRounded(closeRect, 0.2f, 4, new Color(180, 45, 45, 255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 3, 14, closeHover ? Color.White : Color.Gray);
        }

        Raylib.EndScissorMode();

        // Navigasyon ve '+' butonları
        float navStartX = HomeEndX + Math.Min(totalContentW, viewportWidth) + 4f;
        if (maxScroll > 0f)
        {
            Rectangle leftBtn = new Rectangle(navStartX, TopOffset + 6, 24, Height - 7);
            Rectangle rightBtn = new Rectangle(navStartX + 26, TopOffset + 6, 24, Height - 7);
            Rectangle plusRect = new Rectangle(navStartX + 54, TopOffset + 6, 28, Height - 7);

            bool lHover = Raylib.CheckCollisionPointRec(mousePos, leftBtn);
            Raylib.DrawRectangleRec(leftBtn, lHover ? new Color(55, 55, 65, 255) : new Color(35, 35, 40, 255));
            Raylib.DrawRectangleLinesEx(leftBtn, 1, Color.DarkGray);
            Raylib.DrawText("<", (int)leftBtn.X + 8, (int)leftBtn.Y + 6, 14, scrollX > 0 ? Color.White : Color.DarkGray);

            bool rHover = Raylib.CheckCollisionPointRec(mousePos, rightBtn);
            Raylib.DrawRectangleRec(rightBtn, rHover ? new Color(55, 55, 65, 255) : new Color(35, 35, 40, 255));
            Raylib.DrawRectangleLinesEx(rightBtn, 1, Color.DarkGray);
            Raylib.DrawText(">", (int)rightBtn.X + 8, (int)rightBtn.Y + 6, 14, scrollX < maxScroll ? Color.White : Color.DarkGray);

            bool pHover = Raylib.CheckCollisionPointRec(mousePos, plusRect);
            Raylib.DrawRectangleRec(plusRect, pHover ? new Color(55, 55, 65, 255) : new Color(35, 35, 40, 255));
            Raylib.DrawRectangleLinesEx(plusRect, 1, Color.DarkGray);
            Raylib.DrawText("+", (int)plusRect.X + 8, (int)plusRect.Y + 3, 18, Color.LightGray);
        }
        else
        {
            Rectangle plusRect = new Rectangle(navStartX, TopOffset + 6, 28, Height - 7);
            bool plusHover = Raylib.CheckCollisionPointRec(mousePos, plusRect);
            Raylib.DrawRectangleRec(plusRect, plusHover ? new Color(55, 55, 65, 255) : new Color(35, 35, 40, 255));
            Raylib.DrawRectangleLinesEx(plusRect, 1, Color.DarkGray);
            Raylib.DrawText("+", (int)plusRect.X + 8, (int)plusRect.Y + 3, 18, Color.LightGray);
        }
    }
}