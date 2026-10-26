using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class LayerPanel
{
    public float Height { get; } = 86f; 
    public void ToggleHidden() { IsHidden = !IsHidden; }
    public bool IsHidden { get; private set; } = false; 
    
    private float scrollX = 0f;
    private bool isDraggingScrollbar = false;
    private float scrollDragStartMouseX = 0f;
    private float scrollDragStartScrollX = 0f;

    private int draggingLayerIndex = -1;
    private float dragOffsetX = 0f;

    private Rectangle GetPanelBounds(int screenWidth, int screenHeight)
    {
        float leftMargin = 145f;  
        float rightMargin = 180f; 
        float panelWidth = screenWidth - leftMargin - rightMargin;
        float panelY = IsHidden ? screenHeight : screenHeight - Height; 
        return new Rectangle(leftMargin, panelY, panelWidth, Height);
    }

    public bool Update(Canvas canvas, HistoryManager history, Vector2 mousePos, int screenWidth, int screenHeight, Action onLayerChanged)
    {
        Rectangle panelRect = GetPanelBounds(screenWidth, screenHeight);
        
        Rectangle toggleBtn = new Rectangle(panelRect.X + 10, panelRect.Y - 18, 44, 18);
        if (Raylib.CheckCollisionPointRec(mousePos, toggleBtn) && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            IsHidden = !IsHidden;
            return true; 
        }

        if (IsHidden) return false;

        if (!Raylib.CheckCollisionPointRec(mousePos, panelRect)) 
        {
            if (draggingLayerIndex != -1 && Raylib.IsMouseButtonReleased(MouseButton.Left)) draggingLayerIndex = -1;
            return false;
        }

        int boxSize = 58; 
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + 10;

        int itemCount = canvas.Layers.Count + 1; 
        float totalWidth = itemCount * boxSize + (itemCount - 1) * spacing + 24;
        float maxScroll = Math.Max(0, totalWidth - panelRect.Width);

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) { scrollX = Math.Clamp(scrollX - wheel * 40f, 0, maxScroll); }

        if (maxScroll > 0)
        {
            float trackX = panelRect.X + 12;
            float trackY = panelRect.Y + Height - 12;
            float trackWidth = panelRect.Width - 24;
            float thumbWidth = Math.Max(40f, (panelRect.Width / totalWidth) * trackWidth);
            float thumbX = trackX + (scrollX / maxScroll) * (trackWidth - thumbWidth);
            Rectangle thumbRect = new Rectangle(thumbX, trackY - 2, thumbWidth, 10);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, thumbRect))
            {
                isDraggingScrollbar = true; scrollDragStartMouseX = mousePos.X; scrollDragStartScrollX = scrollX;
            }

            if (isDraggingScrollbar)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    float deltaX = mousePos.X - scrollDragStartMouseX;
                    scrollX = Math.Clamp(scrollDragStartScrollX + (deltaX / Math.Max(1f, trackWidth - thumbWidth)) * maxScroll, 0, maxScroll);
                }
                else isDraggingScrollbar = false;
                return true;
            }
        }

        if (draggingLayerIndex != -1 && Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            int dropIndex = (int)Math.Round((mousePos.X - startX) / (boxSize + spacing));
            dropIndex = Math.Clamp(dropIndex, 0, canvas.Layers.Count - 1);

            if (dropIndex != draggingLayerIndex)
            {
                history.SaveState(canvas);
                var layer = canvas.Layers[draggingLayerIndex];
                canvas.Layers.RemoveAt(draggingLayerIndex);
                canvas.Layers.Insert(dropIndex, layer);
                canvas.ActiveLayerIndex = dropIndex;
                onLayerChanged();
            }
            draggingLayerIndex = -1;
            return true;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !isDraggingScrollbar)
        {
            for (int i = 0; i <= canvas.Layers.Count; i++)
            {
                Rectangle boxRect = new Rectangle(startX + i * (boxSize + spacing), startY, boxSize, boxSize);
                
                if (boxRect.X + boxSize > panelRect.X && boxRect.X < panelRect.X + panelRect.Width && Raylib.CheckCollisionPointRec(mousePos, boxRect))
                {
                    if (i == canvas.Layers.Count)
                    {
                        history.SaveState(canvas);
                        canvas.AddLayer();
                        scrollX = Math.Max(0, (canvas.Layers.Count + 1) * (boxSize + spacing) + 24 - panelRect.Width);
                        onLayerChanged();
                    }
                    else
                    {
                        Rectangle eyeRect = new Rectangle(boxRect.X + boxSize - 22, boxRect.Y + boxSize - 18, 20, 16);
                        Rectangle delBtn = new Rectangle(boxRect.X + boxSize - 18, boxRect.Y + 2, 16, 16);

                        if (Raylib.CheckCollisionPointRec(mousePos, eyeRect))
                        {
                            canvas.Layers[i].IsVisible = !canvas.Layers[i].IsVisible; 
                        }
                        else if (Raylib.CheckCollisionPointRec(mousePos, delBtn) && canvas.Layers.Count > 1)
                        {
                            history.SaveState(canvas);
                            canvas.Layers.RemoveAt(i);
                            if (canvas.ActiveLayerIndex >= canvas.Layers.Count) canvas.ActiveLayerIndex = canvas.Layers.Count - 1;
                            onLayerChanged();
                            return true;
                        }
                        else
                        {
                            draggingLayerIndex = i;
                            dragOffsetX = mousePos.X - boxRect.X;
                            canvas.ActiveLayerIndex = i; 
                            onLayerChanged();
                        }
                    }
                    return true;
                }
            }
        }

        return Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonDown(MouseButton.Left);
    }

    public void Draw(Canvas canvas, int screenWidth, int screenHeight)
    {
        Rectangle panelRect = GetPanelBounds(screenWidth, screenHeight);

        Rectangle toggleBtn = new Rectangle(panelRect.X + 10, panelRect.Y - 18, 44, 18);
        Raylib.DrawRectangleRec(toggleBtn, new Color(24, 24, 28, 240));
        Raylib.DrawRectangleLinesEx(toggleBtn, 1, new Color(70, 70, 80, 255));

        int cx = (int)toggleBtn.X + 22;
        int cy = (int)toggleBtn.Y + 9;

        if (IsHidden)
        {
            for (int j = 0; j < 2; j++) { Raylib.DrawLine(cx - 4, cy + 1 + j, cx, cy - 3 + j, Color.LightGray); Raylib.DrawLine(cx, cy - 3 + j, cx + 4, cy + 1 + j, Color.LightGray); }
            return;
        }
        else
        {
            for (int j = 0; j < 2; j++) { Raylib.DrawLine(cx - 4, cy - 2 + j, cx, cy + 2 + j, Color.LightGray); Raylib.DrawLine(cx, cy + 2 + j, cx + 4, cy - 2 + j, Color.LightGray); }
        }

        // Görsel İyileştirme: Alt katman paneli arka planı ve üst sınır çizgisi
        Raylib.DrawRectangleRec(panelRect, new Color(24, 24, 28, 230));
        Raylib.DrawLine((int)panelRect.X, (int)panelRect.Y, (int)(panelRect.X + panelRect.Width), (int)panelRect.Y, new Color(55, 55, 65, 255));

        int boxSize = 58;
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + 10;
        Vector2 mousePos = Raylib.GetMousePosition();

        Raylib.BeginScissorMode((int)panelRect.X, (int)panelRect.Y, (int)panelRect.Width, (int)Height);

        for (int i = 0; i <= canvas.Layers.Count; i++)
        {
            if (i == draggingLayerIndex) continue;
            Rectangle boxRect = new Rectangle(startX + i * (boxSize + spacing), startY, boxSize, boxSize);

            if (i == canvas.Layers.Count)
            {
                PixelSkin.DrawButton(boxRect, Raylib.CheckCollisionPointRec(mousePos, boxRect), false);
                int fontSize = 32;
                int plusWidth = Raylib.MeasureText("+", fontSize);
                Raylib.DrawText("+", (int)boxRect.X + (boxSize - plusWidth) / 2, (int)boxRect.Y + (boxSize - fontSize) / 2, fontSize, Color.White);
            }
            else DrawLayerBox(canvas, i, boxRect, mousePos);
        }

        if (draggingLayerIndex != -1 && draggingLayerIndex < canvas.Layers.Count)
        {
            Rectangle dragRect = new Rectangle(mousePos.X - dragOffsetX, startY, boxSize, boxSize);
            int dropIndex = Math.Clamp((int)Math.Round((mousePos.X - startX) / (boxSize + spacing)), 0, canvas.Layers.Count - 1);
            Raylib.DrawRectangle((int)(startX + dropIndex * (boxSize + spacing) - (spacing / 2f)) - 2, (int)startY, 4, boxSize, Color.Yellow);
            DrawLayerBox(canvas, draggingLayerIndex, dragRect, mousePos, isDragging: true);
        }
        
        Raylib.EndScissorMode();

        int itemCount = canvas.Layers.Count + 1;
        float totalWidth = itemCount * boxSize + (itemCount - 1) * spacing + 24;
        float maxScroll = Math.Max(0, totalWidth - panelRect.Width);
        if (maxScroll > 0)
        {
            float trackX = panelRect.X + 12;
            float trackY = panelRect.Y + Height - 12;
            float trackWidth = panelRect.Width - 24;
            // Kritik Hata Düzeltmesi: totalWidth üzerinden oranlama yapıldı
            float thumbWidth = Math.Max(40f, (panelRect.Width / totalWidth) * trackWidth);
            Raylib.DrawRectangle((int)trackX, (int)trackY, (int)trackWidth, 5, new Color(15, 15, 15, 180));
            Raylib.DrawRectangle((int)(trackX + (scrollX / maxScroll) * (trackWidth - thumbWidth)), (int)trackY, (int)thumbWidth, 5, isDraggingScrollbar ? Color.SkyBlue : Color.Gray);
        }
    }

    private void DrawLayerBox(Canvas canvas, int index, Rectangle boxRect, Vector2 mousePos, bool isDragging = false)
    {
        int boxSize = (int)boxRect.Width;
        bool isActive = (index == canvas.ActiveLayerIndex);
        bool vis = canvas.Layers[index].IsVisible;

        for (int cX = 0; cX < boxSize; cX += 4)
            for (int cY = 0; cY < boxSize; cY += 4)
                Raylib.DrawRectangle((int)boxRect.X + cX, (int)boxRect.Y + cY, Math.Min(4, boxSize - cX), Math.Min(4, boxSize - cY), 
                    ((cX / 4 + cY / 4) % 2 == 0) ? new Color(170, 170, 170, 255) : new Color(130, 130, 130, 255));

        float scale = (float)boxSize / Math.Max(canvas.Width, canvas.Height);
        int thumbX = (int)boxRect.X + (boxSize - (int)(canvas.Width * scale)) / 2;
        int thumbY = (int)boxRect.Y + (boxSize - (int)(canvas.Height * scale)) / 2;

        var pixels = canvas.Layers[index].Pixels;
        for (int x = 0; x < canvas.Width; x++)
            for (int y = 0; y < canvas.Height; y++)
                if (pixels[x, y].A > 0)
                    Raylib.DrawRectangle((int)(thumbX + x * scale), (int)(thumbY + y * scale), Math.Max(1, (int)Math.Ceiling(scale)), Math.Max(1, (int)Math.Ceiling(scale)), pixels[x, y]);

        if (!vis) Raylib.DrawRectangleRec(boxRect, new Color(0, 0, 0, 150));

        Raylib.DrawRectangle((int)boxRect.X, (int)boxRect.Y, boxSize, 18, new Color(0, 0, 0, 160));
        Raylib.DrawText($"Kat {index + 1}", (int)boxRect.X + 5, (int)boxRect.Y + 4, 10, isActive ? Color.Yellow : Color.RayWhite);

        int eyeCx = (int)boxRect.X + boxSize - 12;
        int eyeCy = (int)boxRect.Y + boxSize - 10;
        if (vis)
        {
            Raylib.DrawEllipse(eyeCx, eyeCy, 6, 4, Color.White);
            Raylib.DrawCircle(eyeCx, eyeCy, 2, Color.Black);
        }
        else
        {
            Raylib.DrawLine(eyeCx - 5, eyeCy - 4, eyeCx + 5, eyeCy + 4, Color.Red);
            Raylib.DrawLine(eyeCx - 5, eyeCy + 4, eyeCx + 5, eyeCy - 4, Color.Red);
        }

        Color borderColor = isDragging ? Color.Yellow : (isActive ? Color.SkyBlue : new Color(30, 30, 30, 255));
        Raylib.DrawRectangleLinesEx(boxRect, isActive || isDragging ? 2 : 1, borderColor);

        bool isHover = Raylib.CheckCollisionPointRec(mousePos, boxRect);
        if (isHover && !isDragging && canvas.Layers.Count > 1)
        {
            Rectangle delBtn = new Rectangle(boxRect.X + boxSize - 18, boxRect.Y + 2, 16, 16);
            bool delHover = Raylib.CheckCollisionPointRec(mousePos, delBtn);
            Raylib.DrawRectangleRec(delBtn, delHover ? new Color(220, 50, 50, 255) : new Color(150, 30, 30, 200));
            Raylib.DrawRectangleLinesEx(delBtn, 1, Color.White);
            Raylib.DrawText("x", (int)delBtn.X + 5, (int)delBtn.Y + 2, 12, Color.White);
        }

        if (isDragging) Raylib.DrawRectangleRec(boxRect, new Color(255, 255, 255, 50));
    }
}