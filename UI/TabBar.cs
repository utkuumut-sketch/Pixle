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
    private const int TabWidth = 145;
    private const int TabSpacing = 4;
    private const float NavAreaWidth = 92f;

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

        Raylib.DrawRectangle(0, (int)TopOffset, screenWidth, (int)Height, new Color((byte)24, (byte)25, (byte)32, (byte)255));
        int bottomLineY = (int)(TopOffset + Height);
        Raylib.DrawLine(0, bottomLineY, screenWidth, bottomLineY, PixelSkin.PanelBorderDark);

        Vector2 mousePos = Raylib.GetMousePosition();
        bool isHomeActive = (activeTabIndex == -1);

        Rectangle homeRect = new Rectangle(6, TopOffset + 5, 85, Height - 5);
        if (!isHomeActive)
        {
            bool hover = Raylib.CheckCollisionPointRec(mousePos, homeRect);
            Raylib.DrawRectangleRec(homeRect, hover ? new Color((byte)42, (byte)44, (byte)54, (byte)255) : new Color((byte)30, (byte)31, (byte)38, (byte)255));
            Raylib.DrawRectangleLinesEx(homeRect, 1, PixelSkin.PanelBorderDark);
            Raylib.DrawText("Home", (int)homeRect.X + 24, (int)homeRect.Y + 9, 13, PixelSkin.TextMuted);
        }
        else
        {
            Rectangle aRect = new Rectangle(homeRect.X, TopOffset + 3, homeRect.Width, Height - 2);
            Raylib.DrawRectangleRec(aRect, new Color((byte)52, (byte)85, (byte)135, (byte)255));
            Raylib.DrawRectangle((int)aRect.X, (int)aRect.Y, (int)aRect.Width, 2, PixelSkin.AccentGold);
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), PixelSkin.AccentCyan);
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), PixelSkin.AccentCyan);
            Raylib.DrawText("Home", (int)aRect.X + 24, (int)aRect.Y + 11, 13, PixelSkin.AccentGold);
        }

        Raylib.BeginScissorMode((int)HomeEndX, (int)TopOffset, (int)viewportWidth, (int)Height + 2);

        for (int i = 0; i < tabs.Count; i++)
        {
            if (i == activeTabIndex) continue;

            float tabX = HomeEndX + i * (TabWidth + TabSpacing) - scrollX;
            if (tabX + TabWidth < HomeEndX || tabX > HomeEndX + viewportWidth) continue;

            Rectangle tRect = new Rectangle(tabX, TopOffset + 6, TabWidth, Height - 6);
            bool hover = Raylib.CheckCollisionPointRec(mousePos, tRect);

            Raylib.DrawRectangleRec(tRect, hover ? new Color((byte)42, (byte)44, (byte)54, (byte)255) : new Color((byte)30, (byte)31, (byte)38, (byte)255));
            Raylib.DrawRectangleLinesEx(tRect, 1, PixelSkin.PanelBorderDark);

            string title = tabs[i].GetDisplayTitle();
            if (title.Length > 15) title = title[..15] + "..";
            Raylib.DrawText(title, (int)tRect.X + 10, (int)tRect.Y + 8, 12, tabs[i].IsDirty ? PixelSkin.AccentGold : PixelSkin.TextMuted);

            Rectangle closeRect = new Rectangle(tRect.X + TabWidth - 25, tRect.Y + 5, 19, 19);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRec(closeRect, new Color((byte)185, (byte)50, (byte)50, (byte)255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 2, 13, closeHover ? Color.White : PixelSkin.TextMuted);
        }

        if (activeTabIndex >= 0 && activeTabIndex < tabs.Count)
        {
            float activeX = HomeEndX + activeTabIndex * (TabWidth + TabSpacing) - scrollX;
            Rectangle aRect = new Rectangle(activeX, TopOffset + 3, TabWidth, Height - 2);

            Raylib.DrawRectangleRec(aRect, PixelSkin.PanelBg);
            Raylib.DrawRectangle((int)aRect.X, (int)aRect.Y, (int)aRect.Width, 2, PixelSkin.AccentGold);
            Raylib.DrawLine((int)aRect.X, (int)aRect.Y, (int)aRect.X, (int)(aRect.Y + aRect.Height), PixelSkin.PanelBorderLight);
            Raylib.DrawLine((int)(aRect.X + aRect.Width), (int)aRect.Y, (int)(aRect.X + aRect.Width), (int)(aRect.Y + aRect.Height), PixelSkin.PanelBorderDark);

            string title = tabs[activeTabIndex].GetDisplayTitle();
            if (title.Length > 15) title = title[..15] + "..";
            Raylib.DrawText(title, (int)aRect.X + 10, (int)aRect.Y + 11, 12, tabs[activeTabIndex].IsDirty ? PixelSkin.AccentGold : Color.White);

            Rectangle closeRect = new Rectangle(aRect.X + TabWidth - 25, aRect.Y + 8, 19, 19);
            bool closeHover = Raylib.CheckCollisionPointRec(mousePos, closeRect);
            if (closeHover) Raylib.DrawRectangleRec(closeRect, new Color((byte)195, (byte)55, (byte)55, (byte)255));
            Raylib.DrawText("x", (int)closeRect.X + 6, (int)closeRect.Y + 2, 13, closeHover ? Color.White : PixelSkin.TextPrimary);
        }

        Raylib.EndScissorMode();

        float navStartX = HomeEndX + Math.Min(totalContentW, viewportWidth) + 4f;
        if (maxScroll > 0f)
        {
            Rectangle leftBtn = new Rectangle(navStartX, TopOffset + 6, 24, Height - 7);
            Rectangle rightBtn = new Rectangle(navStartX + 26, TopOffset + 6, 24, Height - 7);
            Rectangle plusRect = new Rectangle(navStartX + 54, TopOffset + 6, 28, Height - 7);

            PixelSkin.DrawButton(leftBtn, Raylib.CheckCollisionPointRec(mousePos, leftBtn), false);
            Raylib.DrawText("<", (int)leftBtn.X + 8, (int)leftBtn.Y + 6, 14, scrollX > 0 ? Color.White : PixelSkin.TextMuted);

            PixelSkin.DrawButton(rightBtn, Raylib.CheckCollisionPointRec(mousePos, rightBtn), false);
            Raylib.DrawText(">", (int)rightBtn.X + 8, (int)rightBtn.Y + 6, 14, scrollX < maxScroll ? Color.White : PixelSkin.TextMuted);

            PixelSkin.DrawButton(plusRect, Raylib.CheckCollisionPointRec(mousePos, plusRect), false);
            Raylib.DrawText("+", (int)plusRect.X + 9, (int)plusRect.Y + 4, 16, PixelSkin.AccentGold);
        }
        else
        {
            Rectangle plusRect = new Rectangle(navStartX, TopOffset + 6, 28, Height - 7);
            PixelSkin.DrawButton(plusRect, Raylib.CheckCollisionPointRec(mousePos, plusRect), false);
            Raylib.DrawText("+", (int)plusRect.X + 9, (int)plusRect.Y + 4, 16, PixelSkin.AccentGold);
        }
    }
}
