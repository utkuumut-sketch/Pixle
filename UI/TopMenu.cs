using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public class MenuItem
{
    public string Text { get; }
    public Action Action { get; }
    public MenuItem(string text, Action action) { Text = text; Action = action; }
}

public class MenuCategory
{
    public string Title { get; }
    public Rectangle HeaderBounds { get; set; }
    public List<MenuItem> Items { get; } = new();
    public MenuCategory(string title) { Title = title; }
}

public class TopMenu
{
    public float Height { get; } = 36f;
    private readonly List<MenuCategory> categories = new();
    private int openCategoryIndex = -1;

    public void AddCategory(string title, Action<List<MenuItem>> buildItems)
    {
        var category = new MenuCategory(title);
        buildItems(category.Items);
        categories.Add(category);
    }

    public void Layout()
    {
        float currentX = 38f;
        foreach (var cat in categories)
        {
            int textW = Raylib.MeasureText(cat.Title, 14);
            float btnW = textW + 24;
            cat.HeaderBounds = new Rectangle(currentX, 5, btnW, Height - 10);
            currentX += btnW + 6;
        }
    }

    private float CalculateDropdownWidth(MenuCategory cat)
    {
        int maxW = 190;
        foreach (var item in cat.Items)
        {
            int w = Raylib.MeasureText(item.Text, 13);
            if (w + 48 > maxW) maxW = w + 48;
        }
        return maxW;
    }

    public bool Update(Vector2 mousePos)
    {
        bool mouseOverBar = mousePos.Y < Height;
        bool isAnyMenuOpen = openCategoryIndex != -1;

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (int i = 0; i < categories.Count; i++)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, categories[i].HeaderBounds))
                {
                    openCategoryIndex = (openCategoryIndex == i) ? -1 : i;
                    return true;
                }
            }

            if (isAnyMenuOpen)
            {
                var cat = categories[openCategoryIndex];
                float dropY = Height + 2;
                float dropW = CalculateDropdownWidth(cat);
                float itemH = 28f;

                for (int j = 0; j < cat.Items.Count; j++)
                {
                    Rectangle itemRect = new Rectangle(cat.HeaderBounds.X, dropY + (j * itemH), dropW, itemH);
                    if (Raylib.CheckCollisionPointRec(mousePos, itemRect))
                    {
                        var action = cat.Items[j].Action;
                        openCategoryIndex = -1;
                        action?.Invoke();
                        return true;
                    }
                }
                openCategoryIndex = -1;
                return true;
            }
        }

        if (isAnyMenuOpen)
        {
            for (int i = 0; i < categories.Count; i++)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, categories[i].HeaderBounds))
                {
                    openCategoryIndex = i;
                    break;
                }
            }

            var cat = categories[openCategoryIndex];
            Rectangle dropArea = new Rectangle(cat.HeaderBounds.X, Height, CalculateDropdownWidth(cat), cat.Items.Count * 28f + 8);
            if (Raylib.CheckCollisionPointRec(mousePos, dropArea)) return true;
        }

        return mouseOverBar;
    }

    public void Draw(int screenWidth)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        Raylib.DrawRectangle(0, 0, screenWidth, (int)Height, PixelSkin.InsetDark);
        Raylib.DrawRectangle(0, (int)Height - 2, screenWidth, 1, PixelSkin.BevelShadow);
        Raylib.DrawLine(0, (int)Height, screenWidth, (int)Height, PixelSkin.BorderOutline);

        // Sol Ust Uygulama İkonu
        PixelIcons.DrawIcon("app_logo", 11, 10, 16f);

        for (int i = 0; i < categories.Count; i++)
        {
            var cat = categories[i];
            bool isOpen = (openCategoryIndex == i);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, cat.HeaderBounds);

            if (isOpen || isHovered)
            {
                PixelSkin.DrawButton(cat.HeaderBounds, isHovered, isOpen);
            }

            int textW = Raylib.MeasureText(cat.Title, 14);
            Raylib.DrawText(
                cat.Title,
                (int)(cat.HeaderBounds.X + (cat.HeaderBounds.Width - textW) / 2),
                (int)(cat.HeaderBounds.Y + (cat.HeaderBounds.Height - 14) / 2),
                14,
                isOpen ? PixelSkin.AccentGold : PixelSkin.TextPrimary
            );
        }

        if (openCategoryIndex != -1)
        {
            var cat = categories[openCategoryIndex];
            float dropY = Height + 2;
            float dropW = CalculateDropdownWidth(cat);
            float itemH = 28f;
            float totalH = cat.Items.Count * itemH + 8;

            Rectangle dropRect = new Rectangle(cat.HeaderBounds.X, dropY, dropW, totalH);
            Raylib.DrawRectangle((int)dropRect.X + 4, (int)dropRect.Y + 4, (int)dropRect.Width, (int)dropRect.Height, new Color((byte)0, (byte)0, (byte)0, (byte)130));
            PixelSkin.DrawPanel(dropRect);

            for (int j = 0; j < cat.Items.Count; j++)
            {
                Rectangle itemRect = new Rectangle(cat.HeaderBounds.X + 4, dropY + 4 + (j * itemH), dropW - 8, itemH);
                bool itemHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect);
                if (itemHovered)
                {
                    Raylib.DrawRectangleRec(itemRect, PixelSkin.AccentBg);
                    Raylib.DrawRectangleLinesEx(itemRect, 1f, PixelSkin.AccentPrimary);
                }

                string rawText = cat.Items[j].Text;
                int parenIdx = rawText.LastIndexOf('(');
                if (parenIdx > 0 && rawText.EndsWith(')'))
                {
                    string mainLabel = rawText[..parenIdx].TrimEnd();
                    string shortcutLabel = rawText[(parenIdx + 1)..^1];
                    Raylib.DrawText(mainLabel, (int)itemRect.X + 10, (int)itemRect.Y + 7, 13, itemHovered ? Color.White : PixelSkin.TextPrimary);
                    int sw = Raylib.MeasureText(shortcutLabel, 11);
                    Raylib.DrawText(shortcutLabel, (int)(itemRect.X + itemRect.Width - sw - 10), (int)itemRect.Y + 8, 11, itemHovered ? PixelSkin.AccentGold : PixelSkin.TextMuted);
                }
                else
                {
                    Raylib.DrawText(rawText, (int)itemRect.X + 10, (int)itemRect.Y + 7, 13, itemHovered ? Color.White : PixelSkin.TextPrimary);
                }
            }
        }
    }
}
