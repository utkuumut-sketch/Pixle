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
        float currentX = 10f;
        foreach (var cat in categories)
        {
            int textW = Raylib.MeasureText(cat.Title, 15);
            float btnW = textW + 24;
            cat.HeaderBounds = new Rectangle(currentX, 4, btnW, Height - 8);
            currentX += btnW + 5;
        }
    }

    private float CalculateDropdownWidth(MenuCategory cat)
    {
        int maxW = 160; // Minimum genişlik
        foreach (var item in cat.Items)
        {
            int w = Raylib.MeasureText(item.Text, 14);
            if (w + 40 > maxW) maxW = w + 40; // 40 piksel padding
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
                float dropY = Height;
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
            var cat = categories[openCategoryIndex];
            Rectangle dropArea = new Rectangle(cat.HeaderBounds.X, Height, CalculateDropdownWidth(cat), cat.Items.Count * 28f);
            if (Raylib.CheckCollisionPointRec(mousePos, dropArea)) return true;
        }

        return mouseOverBar;
    }

    public void Draw(int screenWidth)
    {
        Vector2 mousePos = Raylib.GetMousePosition();
        Raylib.DrawRectangle(0, 0, screenWidth, (int)Height, new Color(25, 25, 25, 255));
        Raylib.DrawLine(0, (int)Height, screenWidth, (int)Height, Color.DarkGray);

        for (int i = 0; i < categories.Count; i++)
        {
            var cat = categories[i];
            bool isOpen = (openCategoryIndex == i);
            bool isHovered = Raylib.CheckCollisionPointRec(mousePos, cat.HeaderBounds);

            Color bg = isOpen ? new Color(60, 60, 60, 255) : isHovered ? new Color(45, 45, 45, 255) : Color.Blank;
            if (bg.A > 0) Raylib.DrawRectangleRec(cat.HeaderBounds, bg);

            int textW = Raylib.MeasureText(cat.Title, 15);
            Raylib.DrawText(cat.Title, (int)(cat.HeaderBounds.X + (cat.HeaderBounds.Width - textW) / 2), (int)(cat.HeaderBounds.Y + (cat.HeaderBounds.Height - 15) / 2), 15, Color.RayWhite);
        }

        if (openCategoryIndex != -1)
        {
            var cat = categories[openCategoryIndex];
            float dropY = Height;
            float dropW = CalculateDropdownWidth(cat);
            float itemH = 28f;
            float totalH = cat.Items.Count * itemH;

            Raylib.DrawRectangle((int)cat.HeaderBounds.X, (int)dropY, (int)dropW, (int)totalH, new Color(35, 35, 35, 255));
            Raylib.DrawRectangleLines((int)cat.HeaderBounds.X, (int)dropY, (int)dropW, (int)totalH, Color.DarkGray);

            for (int j = 0; j < cat.Items.Count; j++)
            {
                Rectangle itemRect = new Rectangle(cat.HeaderBounds.X, dropY + (j * itemH), dropW, itemH);
                bool itemHovered = Raylib.CheckCollisionPointRec(mousePos, itemRect);
                if (itemHovered) Raylib.DrawRectangleRec(itemRect, new Color(55, 75, 115, 255));
                Raylib.DrawText(cat.Items[j].Text, (int)itemRect.X + 10, (int)itemRect.Y + 6, 14, Color.RayWhite);
            }
        }
    }
}