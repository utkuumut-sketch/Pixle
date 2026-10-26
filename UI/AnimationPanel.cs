using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class AnimationPanel
{
    public float Height { get; } = 96f; 
    private float scrollX = 0f;
    
    public bool IsPlaying { get; private set; } = false;
    private float playTimer = 0f;
    
    public float Fps { get; set; } = 8f; 
    public int ThumbnailSize { get; set; } = 64; 
    public bool IsSettingsOpen { get; private set; } = false;

    private int draggingFrameIndex = -1;
    private float dragOffsetX = 0f;

    private Rectangle GetPanelBounds(int screenWidth, int screenHeight)
    {
        float leftMargin = 145f;  
        float rightMargin = 180f; 
        float panelWidth = screenWidth - leftMargin - rightMargin;
        float panelY = 72f; 
        return new Rectangle(leftMargin, panelY, panelWidth, Height);
    }

    public bool Update(Canvas canvas, HistoryManager history, Vector2 mousePos, int screenWidth, int screenHeight, Action onFrameChanged)
    {
        if (IsPlaying)
        {
            playTimer += Raylib.GetFrameTime();
            if (playTimer >= 1f / Fps)
            {
                playTimer = 0f;
                int startIdx = canvas.ActiveFrameIndex;
                int nextIdx = startIdx;
                bool foundVisible = false;

                for (int i = 0; i < canvas.Frames.Count; i++)
                {
                    nextIdx = (nextIdx + 1) % canvas.Frames.Count;
                    if (canvas.Frames[nextIdx].IsVisible)
                    {
                        foundVisible = true;
                        break;
                    }
                }

                if (foundVisible)
                {
                    canvas.ActiveFrameIndex = nextIdx;
                    onFrameChanged();
                }
                else
                {
                    IsPlaying = false; 
                }
            }
        }

        Rectangle panelRect = GetPanelBounds(screenWidth, screenHeight);
        Rectangle playBtn = new Rectangle(panelRect.X + panelRect.Width - 120, panelRect.Y + 16, 50, 64);
        Rectangle settingsBtn = new Rectangle(panelRect.X + panelRect.Width - 65, panelRect.Y + 16, 50, 64);

        if (IsSettingsOpen)
        {
            Rectangle setRect = new Rectangle(settingsBtn.X - 100, settingsBtn.Y + 70, 150, 140);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(mousePos, setRect))
                {
                    Rectangle fpsMinus = new Rectangle(setRect.X + 70, setRect.Y + 30, 24, 24);
                    Rectangle fpsPlus = new Rectangle(setRect.X + 115, setRect.Y + 30, 24, 24);
                    Rectangle sizeMinus = new Rectangle(setRect.X + 70, setRect.Y + 65, 24, 24);
                    Rectangle sizePlus = new Rectangle(setRect.X + 115, setRect.Y + 65, 24, 24);
                    Rectangle resetBtn = new Rectangle(setRect.X + 10, setRect.Y + 100, 130, 28);

                    if (Raylib.CheckCollisionPointRec(mousePos, fpsMinus)) Fps = Math.Max(1, Fps - 1);
                    if (Raylib.CheckCollisionPointRec(mousePos, fpsPlus)) Fps = Math.Min(60, Fps + 1);
                    if (Raylib.CheckCollisionPointRec(mousePos, sizeMinus)) ThumbnailSize = Math.Max(32, ThumbnailSize - 2);
                    if (Raylib.CheckCollisionPointRec(mousePos, sizePlus)) ThumbnailSize = Math.Min(84, ThumbnailSize + 2);
                    
                    if (Raylib.CheckCollisionPointRec(mousePos, resetBtn)) { Fps = 8; ThumbnailSize = 64; }
                    return true;
                }
                else if (!Raylib.CheckCollisionPointRec(mousePos, settingsBtn))
                {
                    IsSettingsOpen = false;
                }
            }
        }

        if (!Raylib.CheckCollisionPointRec(mousePos, panelRect)) 
        {
            if (draggingFrameIndex != -1 && Raylib.IsMouseButtonReleased(MouseButton.Left)) draggingFrameIndex = -1;
            return false;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, playBtn)) { IsPlaying = !IsPlaying; return true; }
            if (Raylib.CheckCollisionPointRec(mousePos, settingsBtn)) { IsSettingsOpen = !IsSettingsOpen; return true; }
        }

        if (IsPlaying) return true;

        int boxSize = ThumbnailSize; 
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + (Height - boxSize) / 2; 
        float listAvailableWidth = panelRect.Width - 135; 

        int itemCount = canvas.Frames.Count + 1; 
        float totalWidth = itemCount * boxSize + (itemCount - 1) * spacing + 24;
        float maxScroll = Math.Max(0, totalWidth - listAvailableWidth);

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) { scrollX = Math.Clamp(scrollX - wheel * 40f, 0, maxScroll); }

        if (draggingFrameIndex != -1 && Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            int dropIndex = (int)Math.Round((mousePos.X - startX) / (boxSize + spacing));
            dropIndex = Math.Clamp(dropIndex, 0, canvas.Frames.Count - 1);

            if (dropIndex != draggingFrameIndex)
            {
                history.SaveState(canvas);
                var frame = canvas.Frames[draggingFrameIndex];
                canvas.Frames.RemoveAt(draggingFrameIndex);
                canvas.Frames.Insert(dropIndex, frame);
                canvas.ActiveFrameIndex = dropIndex;
                onFrameChanged();
            }
            draggingFrameIndex = -1;
            return true;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (int i = 0; i <= canvas.Frames.Count; i++)
            {
                Rectangle boxRect = new Rectangle(startX + i * (boxSize + spacing), startY, boxSize, boxSize);
                if (boxRect.X + boxSize > panelRect.X && boxRect.X < panelRect.X + listAvailableWidth && Raylib.CheckCollisionPointRec(mousePos, boxRect))
                {
                    if (i == canvas.Frames.Count)
                    {
                        history.SaveState(canvas);
                        canvas.AddFrame();
                        scrollX = Math.Max(0, (canvas.Frames.Count + 1) * (boxSize + spacing) + 24 - listAvailableWidth);
                        onFrameChanged();
                    }
                    else
                    {
                        Rectangle eyeRect = new Rectangle(boxRect.X + boxSize - 22, boxRect.Y + boxSize - 18, 20, 16);
                        Rectangle delBtn = new Rectangle(boxRect.X + boxSize - 18, boxRect.Y + 2, 16, 16);

                        if (Raylib.CheckCollisionPointRec(mousePos, eyeRect))
                        {
                            canvas.Frames[i].IsVisible = !canvas.Frames[i].IsVisible;
                            onFrameChanged();
                        }
                        else if (Raylib.CheckCollisionPointRec(mousePos, delBtn) && canvas.Frames.Count > 1)
                        {
                            history.SaveState(canvas);
                            canvas.Frames.RemoveAt(i);
                            if (canvas.ActiveFrameIndex >= canvas.Frames.Count) canvas.ActiveFrameIndex = canvas.Frames.Count - 1;
                            onFrameChanged();
                            return true;
                        }
                        else
                        {
                            draggingFrameIndex = i;
                            dragOffsetX = mousePos.X - boxRect.X;
                            canvas.ActiveFrameIndex = i;
                            onFrameChanged();
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
        
        // Görsel İyileştirme: Yarı şeffaf üst panel arka planı ve alt sınır çizgisi
        Raylib.DrawRectangleRec(panelRect, new Color(24, 24, 28, 230));
        Raylib.DrawLine((int)panelRect.X, (int)(panelRect.Y + Height), (int)(panelRect.X + panelRect.Width), (int)(panelRect.Y + Height), new Color(55, 55, 65, 255));

        int boxSize = ThumbnailSize;
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + (Height - boxSize) / 2;
        float listAvailableWidth = panelRect.Width - 135;

        Vector2 mousePos = Raylib.GetMousePosition();

        Raylib.BeginScissorMode((int)panelRect.X, (int)panelRect.Y, (int)listAvailableWidth, (int)Height);

        for (int i = 0; i <= canvas.Frames.Count; i++)
        {
            if (i == draggingFrameIndex) continue; 
            Rectangle boxRect = new Rectangle(startX + i * (boxSize + spacing), startY, boxSize, boxSize);

            if (i == canvas.Frames.Count)
            {
                PixelSkin.DrawButton(boxRect, Raylib.CheckCollisionPointRec(mousePos, boxRect), false);
                int plusW = Raylib.MeasureText("+", 32);
                Raylib.DrawText("+", (int)boxRect.X + (boxSize - plusW) / 2, (int)boxRect.Y + (boxSize - 32) / 2, 32, Color.White);
            }
            else DrawFrameBox(canvas, i, boxRect, mousePos);
        }

        if (draggingFrameIndex != -1 && draggingFrameIndex < canvas.Frames.Count)
        {
            Rectangle dragRect = new Rectangle(mousePos.X - dragOffsetX, startY, boxSize, boxSize);
            int dropIndex = Math.Clamp((int)Math.Round((mousePos.X - startX) / (boxSize + spacing)), 0, canvas.Frames.Count - 1);
            Raylib.DrawRectangle((int)(startX + dropIndex * (boxSize + spacing) - (spacing / 2f)) - 2, (int)startY, 4, boxSize, Color.Yellow);
            DrawFrameBox(canvas, draggingFrameIndex, dragRect, mousePos, isDragging: true);
        }

        Raylib.EndScissorMode();

        Rectangle playBtn = new Rectangle(panelRect.X + panelRect.Width - 120, panelRect.Y + 16, 50, 64);
        Rectangle settingsBtn = new Rectangle(panelRect.X + panelRect.Width - 65, panelRect.Y + 16, 50, 64);

        bool hoverPlay = Raylib.CheckCollisionPointRec(mousePos, playBtn);
        bool hoverSet = Raylib.CheckCollisionPointRec(mousePos, settingsBtn);
        
        PixelSkin.DrawButton(playBtn, hoverPlay, IsPlaying);
        PixelSkin.DrawButton(settingsBtn, hoverSet, IsSettingsOpen);

        int iconOffsetPlay = IsPlaying ? 2 : (hoverPlay ? 1 : 0);
        int iconOffsetSet = IsSettingsOpen ? 2 : (hoverSet ? 1 : 0);

        if (IsPlaying) 
            Raylib.DrawRectangle((int)playBtn.X + 16, (int)playBtn.Y + 23 + iconOffsetPlay, 18, 18, Color.Yellow); 
        else 
            Raylib.DrawTriangle(new Vector2(playBtn.X + 18, playBtn.Y + 19 + iconOffsetPlay), new Vector2(playBtn.X + 18, playBtn.Y + 45 + iconOffsetPlay), new Vector2(playBtn.X + 38, playBtn.Y + 32 + iconOffsetPlay), Color.White);

        Raylib.DrawCircleLines((int)settingsBtn.X + 25, (int)settingsBtn.Y + 32 + iconOffsetSet, 8, Color.LightGray);
        Raylib.DrawCircleLines((int)settingsBtn.X + 25, (int)settingsBtn.Y + 32 + iconOffsetSet, 4, Color.White);

        if (IsSettingsOpen)
        {
            Rectangle setRect = new Rectangle(settingsBtn.X - 100, settingsBtn.Y + 70, 150, 140);
            PixelSkin.DrawPanel(setRect);
            
            Raylib.DrawRectangle((int)setRect.X, (int)setRect.Y, (int)setRect.Width, 24, new Color(45, 45, 52, 255));
            Raylib.DrawText("Kare Ayarlari", (int)setRect.X + 10, (int)setRect.Y + 6, 12, Color.RayWhite);

            Raylib.DrawText("Hiz (FPS):", (int)setRect.X + 10, (int)setRect.Y + 35, 12, Color.LightGray);
            DrawSmallBtn(new Rectangle(setRect.X + 70, setRect.Y + 30, 24, 24), "-", mousePos);
            Raylib.DrawText(Fps.ToString(), (int)setRect.X + 98, (int)setRect.Y + 36, 12, Color.Yellow);
            DrawSmallBtn(new Rectangle(setRect.X + 115, setRect.Y + 30, 24, 24), "+", mousePos);

            Raylib.DrawText("Boyut:", (int)setRect.X + 10, (int)setRect.Y + 70, 12, Color.LightGray);
            DrawSmallBtn(new Rectangle(setRect.X + 70, setRect.Y + 65, 24, 24), "-", mousePos);
            Raylib.DrawText(ThumbnailSize.ToString(), (int)setRect.X + 96, (int)setRect.Y + 71, 12, Color.Yellow);
            DrawSmallBtn(new Rectangle(setRect.X + 115, setRect.Y + 65, 24, 24), "+", mousePos);

            Rectangle resetBtn = new Rectangle(setRect.X + 10, setRect.Y + 100, 130, 28);
            PixelSkin.DrawButton(resetBtn, Raylib.CheckCollisionPointRec(mousePos, resetBtn), false);
            Raylib.DrawText("Varsayilana Don", (int)resetBtn.X + 16, (int)resetBtn.Y + 8, 12, Color.White);
        }
    }

    private void DrawSmallBtn(Rectangle rect, string text, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        PixelSkin.DrawButton(rect, hover, false);
        int tw = Raylib.MeasureText(text, 14);
        int offset = hover ? 1 : 0; 
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 5 + offset, 14, Color.White);
    }

    private void DrawFrameBox(Canvas canvas, int index, Rectangle boxRect, Vector2 mousePos, bool isDragging = false)
    {
        int boxSize = (int)boxRect.Width;
        bool isActive = (index == canvas.ActiveFrameIndex);
        bool vis = canvas.Frames[index].IsVisible;

        for (int cX = 0; cX < boxSize; cX += 4)
            for (int cY = 0; cY < boxSize; cY += 4)
                Raylib.DrawRectangle((int)boxRect.X + cX, (int)boxRect.Y + cY, Math.Min(4, boxSize - cX), Math.Min(4, boxSize - cY), 
                    ((cX / 4 + cY / 4) % 2 == 0) ? new Color(170, 170, 170, 255) : new Color(130, 130, 130, 255));

        float scale = (float)boxSize / Math.Max(canvas.Width, canvas.Height);
        int thumbX = (int)boxRect.X + (boxSize - (int)(canvas.Width * scale)) / 2;
        int thumbY = (int)boxRect.Y + (boxSize - (int)(canvas.Height * scale)) / 2;

        var frame = canvas.Frames[index];
        for (int l = 0; l < frame.Layers.Count; l++)
        {
            if (!frame.Layers[l].IsVisible) continue;
            var pixels = frame.Layers[l].Pixels;
            for (int x = 0; x < canvas.Width; x++)
                for (int y = 0; y < canvas.Height; y++)
                    if (pixels[x, y].A > 0)
                        Raylib.DrawRectangle((int)(thumbX + x * scale), (int)(thumbY + y * scale), Math.Max(1, (int)Math.Ceiling(scale)), Math.Max(1, (int)Math.Ceiling(scale)), pixels[x, y]);
        }

        if (!vis) Raylib.DrawRectangleRec(boxRect, new Color(0, 0, 0, 160));

        Raylib.DrawRectangle((int)boxRect.X, (int)boxRect.Y, boxSize, 18, new Color(0, 0, 0, 160));
        Raylib.DrawText($"Kare {index + 1}", (int)boxRect.X + 6, (int)boxRect.Y + 4, 10, isActive ? Color.Yellow : Color.RayWhite);

        int eyeCx = (int)boxRect.X + boxSize - 12;
        int eyeCy = (int)boxRect.Y + boxSize - 10;
        if (vis) { Raylib.DrawEllipse(eyeCx, eyeCy, 6, 4, Color.White); Raylib.DrawCircle(eyeCx, eyeCy, 2, Color.Black); }
        else { Raylib.DrawLine(eyeCx - 5, eyeCy - 4, eyeCx + 5, eyeCy + 4, Color.Red); Raylib.DrawLine(eyeCx - 5, eyeCy + 4, eyeCx + 5, eyeCy - 4, Color.Red); }
        
        Color borderColor = isDragging ? Color.Yellow : (isActive ? Color.Yellow : new Color(30, 30, 30, 255));
        Raylib.DrawRectangleLinesEx(boxRect, isActive || isDragging ? 2 : 1, borderColor);

        bool isHover = Raylib.CheckCollisionPointRec(mousePos, boxRect);
        if (isHover && !isDragging && canvas.Frames.Count > 1)
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