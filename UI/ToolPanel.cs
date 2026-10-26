using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class ToolPanel
{
    public float Width { get; } = 145f;
    public float TopOffset { get; }

    private readonly List<(string Label, ToolType Tool)> tools = new()
    {
        ("Kalem (1)", ToolType.Pen),
        ("Silgi (2)", ToolType.Eraser),
        ("Kova (3)", ToolType.Bucket),
        ("Akilli Kova (4)", ToolType.SmartBucket),
        ("Damlalik (5)", ToolType.Eyedropper),
        ("Cizgi (L)", ToolType.Line),
        ("Sekiller (U)", ToolType.Shape),
        ("Sec / Tasi (M)", ToolType.SelectMove)
    };

    private readonly List<(string Label, ShapeType Shape)> shapes = new()
    {
        ("Kare (Bos)", ShapeType.RectangleOutline),
        ("Kare (Dolu)", ShapeType.RectangleFilled),
        ("Cember (Bos)", ShapeType.CircleOutline),
        ("Cember (Dolu)", ShapeType.CircleFilled),
        ("Ucgen (Bos)", ShapeType.TriangleOutline),
        ("Ucgen (Dolu)", ShapeType.TriangleFilled)
    };

    private readonly List<(string Label, SelectToolMode Mode)> selectionModes = new()
    {
        ("Kare Secim", SelectToolMode.Rectangle),
        ("Serbest Cizim", SelectToolMode.Lasso),
        ("Sihirli Degnek", SelectToolMode.MagicWand)
    };

    private ToolType currentActiveTool = ToolType.Pen;

    public ToolPanel(float topOffset)
    {
        TopOffset = topOffset;
    }

    public bool Update(Vector2 mousePos, Action<ToolType> onSelectTool, Action<ShapeType> onSelectShape, Action<SelectToolMode> onSelectMode)
    {
        float startY = TopOffset + 32;
        int btnHeight = 30;
        int spacing = 5;

        if (mousePos.X <= Width && mousePos.Y >= TopOffset)
        {
            for (int i = 0; i < tools.Count; i++)
            {
                Rectangle btnRect = new Rectangle(10, startY + i * (btnHeight + spacing), Width - 20, btnHeight);
                if (Raylib.CheckCollisionPointRec(mousePos, btnRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    currentActiveTool = tools[i].Tool;
                    onSelectTool(tools[i].Tool);
                    return true;
                }
            }
        }

        Rectangle flyoutBounds = GetFlyoutBounds();
        if (flyoutBounds.Width > 0 && Raylib.CheckCollisionPointRec(mousePos, flyoutBounds))
        {
            float itemStartY = flyoutBounds.Y + 24;
            int itemHeight = 24;

            if (currentActiveTool == ToolType.Shape)
            {
                for (int i = 0; i < shapes.Count; i++)
                {
                    Rectangle subRect = new Rectangle(flyoutBounds.X + 6, itemStartY + i * (itemHeight + 3), flyoutBounds.Width - 12, itemHeight);
                    if (Raylib.CheckCollisionPointRec(mousePos, subRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        onSelectShape(shapes[i].Shape);
                        return true;
                    }
                }
            }
            else if (currentActiveTool == ToolType.SelectMove)
            {
                for (int i = 0; i < selectionModes.Count; i++)
                {
                    Rectangle subRect = new Rectangle(flyoutBounds.X + 6, itemStartY + i * (itemHeight + 3), flyoutBounds.Width - 12, itemHeight);
                    if (Raylib.CheckCollisionPointRec(mousePos, subRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                    {
                        onSelectMode(selectionModes[i].Mode);
                        return true;
                    }
                }
            }

            return Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonDown(MouseButton.Left);
        }

        return mousePos.X <= Width && mousePos.Y >= TopOffset && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    private Rectangle GetFlyoutBounds()
    {
        float startY = TopOffset + 32;
        int btnHeight = 30;
        int spacing = 5;

        int activeIndex = tools.FindIndex(t => t.Tool == currentActiveTool);
        if (activeIndex < 0) return new Rectangle(0, 0, 0, 0);

        float flyoutY = startY + activeIndex * (btnHeight + spacing);
        float flyoutX = Width + 4;

        if (currentActiveTool == ToolType.Shape)
            return new Rectangle(flyoutX, flyoutY, 130, 28 + shapes.Count * 27 + 6);
        else if (currentActiveTool == ToolType.SelectMove)
            return new Rectangle(flyoutX, flyoutY, 130, 28 + selectionModes.Count * 27 + 6);

        return new Rectangle(0, 0, 0, 0);
    }

    public void Draw(ToolType activeTool, ShapeType activeShape, SelectToolMode activeSelMode, int screenHeight)
    {
        currentActiveTool = activeTool;

        // Sol Panel Gövdesi
        Rectangle panelRect = new Rectangle(0, TopOffset, Width, screenHeight - TopOffset);
        PixelSkin.DrawPanel(panelRect);

        Raylib.DrawText("ARACLAR", 16, (int)TopOffset + 10, 13, Color.White);

        float startY = TopOffset + 32;
        int btnHeight = 30;
        int spacing = 5;
        Vector2 mousePos = Raylib.GetMousePosition();

        for (int i = 0; i < tools.Count; i++)
        {
            Rectangle btnRect = new Rectangle(10, startY + i * (btnHeight + spacing), Width - 20, btnHeight);
            bool isSelected = tools[i].Tool == activeTool;
            bool isHover = Raylib.CheckCollisionPointRec(mousePos, btnRect);

            // 3D Bevel Buton Çizimi
            PixelSkin.DrawButton(btnRect, isHover, isSelected);

            string label = tools[i].Label;
            int textOffsetY = isSelected ? 9 : 8; // Basıldığında hafif aşağı çökme efekti
            int textOffsetX = isSelected ? 11 : 10;

            if (tools[i].Tool == ToolType.Shape || tools[i].Tool == ToolType.SelectMove)
            {
                Raylib.DrawText(">", (int)btnRect.X + (int)btnRect.Width - 14, (int)btnRect.Y + textOffsetY, 12, isSelected ? Color.Yellow : Color.LightGray);
            }

            Raylib.DrawText(label, (int)btnRect.X + textOffsetX, (int)btnRect.Y + textOffsetY, 12, isSelected ? Color.Yellow : Color.RayWhite);
        }

        // Açılan Yan Menü (Flyout)
        Rectangle flyout = GetFlyoutBounds();
        if (flyout.Width > 0)
        {
            PixelSkin.DrawPanel(flyout);

            float itemStartY = flyout.Y + 24;
            int itemHeight = 24;

            if (activeTool == ToolType.Shape)
            {
                Raylib.DrawText("Sekil Sec:", (int)flyout.X + 8, (int)flyout.Y + 6, 11, Color.White);

                for (int i = 0; i < shapes.Count; i++)
                {
                    Rectangle subRect = new Rectangle(flyout.X + 6, itemStartY + i * (itemHeight + 3), flyout.Width - 12, itemHeight);
                    bool isSelected = shapes[i].Shape == activeShape;
                    bool isHover = Raylib.CheckCollisionPointRec(mousePos, subRect);

                    PixelSkin.DrawButton(subRect, isHover, isSelected);
                    Raylib.DrawText(shapes[i].Label, (int)subRect.X + 6, (int)subRect.Y + 6, 11, isSelected ? Color.Yellow : Color.RayWhite);
                }
            }
            else if (activeTool == ToolType.SelectMove)
            {
                Raylib.DrawText("Secim Modu:", (int)flyout.X + 8, (int)flyout.Y + 6, 11, Color.White);

                for (int i = 0; i < selectionModes.Count; i++)
                {
                    Rectangle subRect = new Rectangle(flyout.X + 6, itemStartY + i * (itemHeight + 3), flyout.Width - 12, itemHeight);
                    bool isSelected = selectionModes[i].Mode == activeSelMode;
                    bool isHover = Raylib.CheckCollisionPointRec(mousePos, subRect);

                    PixelSkin.DrawButton(subRect, isHover, isSelected);
                    Raylib.DrawText(selectionModes[i].Label, (int)subRect.X + 6, (int)subRect.Y + 6, 11, isSelected ? Color.Yellow : Color.RayWhite);
                }
            }
        }
    }
}