// === DOSYA: \UI\LayerPanel.cs ===
using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class LayerPanel
{
    public float Height { get; } = 86f;
    public bool IsHidden { get; private set; } = false;
    public float SlideProgress { get; private set; } = 0f; // 0f: Tam Acik, 1f: Tam Gizli (Asagi Kaymis)

    public int ThumbnailSize { get; set; } = 58;
    public bool IsSettingsOpen { get; private set; } = false;

    public void ToggleHidden()
    {
        IsHidden = !IsHidden;
        if (IsHidden) IsSettingsOpen = false;
    }

    public float GetVisibleHeight() => Height * (1f - SlideProgress);

    private float scrollX = 0f;
    private bool isDraggingScrollbar = false;
    private float scrollDragStartMouseX = 0f;
    private float scrollDragStartScrollX = 0f;

    private int draggingLayerIndex = -1;
    private float dragOffsetX = 0f;

    private const float RightControlsWidth = 172f;

    private Rectangle GetPanelBounds(int screenWidth, int screenHeight)
    {
        float leftMargin = AppSettings.LeftPanelWidth;
        float rightMargin = AppSettings.RightPanelWidth;
        float panelWidth = screenWidth - leftMargin - rightMargin;
        float panelY = screenHeight - Height + (Height * SlideProgress);
        return new Rectangle(leftMargin, panelY, panelWidth, Height);
    }

    private static Rectangle GetSettingsPopupRect(Rectangle settingsBtn, Rectangle panelRect)
    {
        float popW = 216f;
        float popH = 232f;
        float popX = Math.Clamp(settingsBtn.X + settingsBtn.Width - popW, panelRect.X + 6f, panelRect.X + panelRect.Width - popW - 6f);
        float popY = Math.Max(76f, panelRect.Y - popH - 6f);
        return new Rectangle(popX, popY, popW, popH);
    }

    public bool Update(
        Canvas canvas,
        HistoryManager history,
        Vector2 mousePos,
        int screenWidth,
        int screenHeight,
        Action onLayerChanged,
        SelectionManager? selection = null,
        Action<string>? onStatus = null)
    {
        // ASAGI / YUKARI KAYMA ANIMASYONU GUNCELLEMESI
        float targetSlide = IsHidden ? 1f : 0f;
        if (!AppSettings.EnableUIAnimations)
        {
            SlideProgress = targetSlide;
        }
        else if (Math.Abs(SlideProgress - targetSlide) > 0.001f)
        {
            SlideProgress = Raymath.Lerp(SlideProgress, targetSlide, Math.Min(1f, Raylib.GetFrameTime() * AppSettings.UIAnimationSpeed));
            if (Math.Abs(SlideProgress - targetSlide) < 0.005f) SlideProgress = targetSlide;
        }

        Rectangle panelRect = GetPanelBounds(screenWidth, screenHeight);

        Rectangle toggleBtn = new Rectangle(panelRect.X + 10, panelRect.Y - 18, 44, 18);
        if (Raylib.CheckCollisionPointRec(mousePos, toggleBtn) && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            ToggleHidden();
            return true;
        }

        if (IsHidden || SlideProgress > 0.1f)
        {
            draggingLayerIndex = -1;
            isDraggingScrollbar = false;
            return false;
        }

        float btnY = panelRect.Y + 13f;
        float btnH = 58f;
        Rectangle mergeBtn = new Rectangle(panelRect.X + panelRect.Width - 164f, btnY, 48f, btnH);
        Rectangle splitBtn = new Rectangle(panelRect.X + panelRect.Width - 111f, btnY, 48f, btnH);
        Rectangle settingsBtn = new Rectangle(panelRect.X + panelRect.Width - 58f, btnY, 46f, btnH);

        if (IsSettingsOpen)
        {
            Rectangle setRect = GetSettingsPopupRect(settingsBtn, panelRect);
            if (Raylib.CheckCollisionPointRec(mousePos, setRect))
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Layer activeLayer = canvas.Layers[canvas.ActiveLayerIndex];

                    Rectangle sizeMinus = new Rectangle(setRect.X + 112, setRect.Y + 32, 24, 22);
                    Rectangle sizePlus = new Rectangle(setRect.X + 178, setRect.Y + 32, 24, 22);

                    Rectangle opMinus = new Rectangle(setRect.X + 112, setRect.Y + 60, 24, 22);
                    Rectangle opPlus = new Rectangle(setRect.X + 178, setRect.Y + 60, 24, 22);

                    Rectangle alphaBtn = new Rectangle(setRect.X + 112, setRect.Y + 88, 90, 22);

                    Rectangle popMergeDownBtn = new Rectangle(setRect.X + 10, setRect.Y + 120, 95, 26);
                    Rectangle popMergeAllBtn = new Rectangle(setRect.X + 111, setRect.Y + 120, 95, 26);

                    Rectangle popSplitBtn = new Rectangle(setRect.X + 10, setRect.Y + 152, 95, 26);
                    Rectangle popDupBtn = new Rectangle(setRect.X + 111, setRect.Y + 152, 95, 26);

                    Rectangle resetBtn = new Rectangle(setRect.X + 10, setRect.Y + 190, 196, 28);

                    if (Raylib.CheckCollisionPointRec(mousePos, sizeMinus))
                        ThumbnailSize = Math.Max(38, ThumbnailSize - 2);
                    else if (Raylib.CheckCollisionPointRec(mousePos, sizePlus))
                        ThumbnailSize = Math.Min(74, ThumbnailSize + 2);
                    else if (Raylib.CheckCollisionPointRec(mousePos, opMinus))
                    {
                        int curPct = (int)Math.Round(activeLayer.Opacity / 255f * 100f);
                        int nextPct = Math.Clamp(curPct - 10, 10, 100);
                        activeLayer.Opacity = (byte)Math.Round(nextPct / 100f * 255f);
                        canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
                        onLayerChanged();
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, opPlus))
                    {
                        int curPct = (int)Math.Round(activeLayer.Opacity / 255f * 100f);
                        int nextPct = Math.Clamp(curPct + 10, 10, 100);
                        activeLayer.Opacity = (byte)Math.Round(nextPct / 100f * 255f);
                        canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
                        onLayerChanged();
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, alphaBtn))
                    {
                        activeLayer.AlphaLock = !activeLayer.AlphaLock;
                        onLayerChanged();
                        onStatus?.Invoke($"Alfa Kilidi ({activeLayer.Name}): {(activeLayer.AlphaLock ? "Acik" : "Kapali")}");
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, popMergeDownBtn))
                    {
                        selection?.CancelOrCommit(canvas);
                        history.SaveState(canvas);
                        canvas.MergeActiveLayer(out string msg);
                        onLayerChanged();
                        onStatus?.Invoke(msg);
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, popMergeAllBtn))
                    {
                        selection?.CancelOrCommit(canvas);
                        history.SaveState(canvas);
                        canvas.MergeAllVisibleLayers(out string msg);
                        onLayerChanged();
                        onStatus?.Invoke(msg);
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, popSplitBtn))
                    {
                        history.SaveState(canvas);
                        canvas.SplitActiveLayer(selection, out string msg);
                        onLayerChanged();
                        onStatus?.Invoke(msg);
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, popDupBtn))
                    {
                        selection?.CancelOrCommit(canvas);
                        history.SaveState(canvas);
                        canvas.DuplicateActiveLayer();
                        onLayerChanged();
                        onStatus?.Invoke("Aktif katman cogaltildi.");
                    }
                    else if (Raylib.CheckCollisionPointRec(mousePos, resetBtn))
                    {
                        ThumbnailSize = 58;
                        activeLayer.Opacity = 255;
                        canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
                        onLayerChanged();
                        onStatus?.Invoke("Katman gorunum ayarlari sifirlandi.");
                    }
                }
                return true;
            }
            else if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !Raylib.CheckCollisionPointRec(mousePos, settingsBtn))
            {
                IsSettingsOpen = false;
            }
        }

        int boxSize = ThumbnailSize;
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + (Height - boxSize) / 2f;
        float listAvailableWidth = Math.Max(80f, panelRect.Width - RightControlsWidth);

        int itemCount = canvas.Layers.Count + 1;
        float totalWidth = itemCount * boxSize + (itemCount - 1) * spacing + 24;
        float maxScroll = Math.Max(0, totalWidth - listAvailableWidth);
        scrollX = Math.Clamp(scrollX, 0, maxScroll);

        if (isDraggingScrollbar)
        {
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && maxScroll > 0)
            {
                float trackWidth = listAvailableWidth - 24;
                float thumbWidth = Math.Max(40f, (listAvailableWidth / totalWidth) * trackWidth);
                float deltaX = mousePos.X - scrollDragStartMouseX;
                scrollX = Math.Clamp(scrollDragStartScrollX + (deltaX / Math.Max(1f, trackWidth - thumbWidth)) * maxScroll, 0, maxScroll);
            }
            else isDraggingScrollbar = false;
            return true;
        }

        if (draggingLayerIndex != -1)
        {
            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                int dropIndex = (int)Math.Round((mousePos.X - startX) / (boxSize + spacing));
                dropIndex = Math.Clamp(dropIndex, 0, canvas.Layers.Count - 1);

                if (dropIndex != draggingLayerIndex && draggingLayerIndex < canvas.Layers.Count)
                {
                    bool isShiftMerge = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
                    history.SaveState(canvas);
                    if (isShiftMerge)
                    {
                        canvas.MergeLayersAt(draggingLayerIndex, dropIndex, out string msg);
                        onLayerChanged();
                        onStatus?.Invoke(msg);
                    }
                    else
                    {
                        var layer = canvas.Layers[draggingLayerIndex];
                        canvas.Layers.RemoveAt(draggingLayerIndex);
                        canvas.Layers.Insert(dropIndex, layer);
                        canvas.ActiveLayerIndex = dropIndex;
                        canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
                        onLayerChanged();
                    }
                }
                draggingLayerIndex = -1;
            }
            return true;
        }

        if (!Raylib.CheckCollisionPointRec(mousePos, panelRect))
        {
            return false;
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, mergeBtn))
            {
                selection?.CancelOrCommit(canvas);
                history.SaveState(canvas);
                canvas.MergeActiveLayer(out string msg);
                onLayerChanged();
                onStatus?.Invoke(msg);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, splitBtn))
            {
                history.SaveState(canvas);
                canvas.SplitActiveLayer(selection, out string msg);
                onLayerChanged();
                onStatus?.Invoke(msg);
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, settingsBtn))
            {
                IsSettingsOpen = !IsSettingsOpen;
                return true;
            }
        }

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0) { scrollX = Math.Clamp(scrollX - wheel * 40f, 0, maxScroll); }

        if (maxScroll > 0)
        {
            float trackX = panelRect.X + 12;
            float trackY = panelRect.Y + Height - 10;
            float trackWidth = listAvailableWidth - 24;
            float thumbWidth = Math.Max(40f, (listAvailableWidth / totalWidth) * trackWidth);
            float thumbX = trackX + (scrollX / maxScroll) * (trackWidth - thumbWidth);
            Rectangle thumbRect = new Rectangle(thumbX, trackY - 2, thumbWidth, 8);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, thumbRect))
            {
                isDraggingScrollbar = true;
                scrollDragStartMouseX = mousePos.X;
                scrollDragStartScrollX = scrollX;
                return true;
            }
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !isDraggingScrollbar)
        {
            for (int i = 0; i <= canvas.Layers.Count; i++)
            {
                Rectangle boxRect = new Rectangle(startX + i * (boxSize + spacing), startY, boxSize, boxSize);

                if (boxRect.X + boxSize > panelRect.X && boxRect.X < panelRect.X + listAvailableWidth && Raylib.CheckCollisionPointRec(mousePos, boxRect))
                {
                    if (i == canvas.Layers.Count)
                    {
                        history.SaveState(canvas);
                        canvas.AddLayer();
                        scrollX = Math.Max(0, (canvas.Layers.Count + 1) * (boxSize + spacing) + 24 - listAvailableWidth);
                        onLayerChanged();
                    }
                    else
                    {
                        Rectangle eyeRect = new Rectangle(boxRect.X + boxSize - 22, boxRect.Y + boxSize - 18, 20, 16);
                        Rectangle delBtn = new Rectangle(boxRect.X + boxSize - 18, boxRect.Y + 2, 16, 16);

                        if (Raylib.CheckCollisionPointRec(mousePos, eyeRect))
                        {
                            history.SaveState(canvas);
                            canvas.Layers[i].IsVisible = !canvas.Layers[i].IsVisible;
                            canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
                            onLayerChanged();
                        }
                        else if (Raylib.CheckCollisionPointRec(mousePos, delBtn) && canvas.Layers.Count > 1)
                        {
                            int prevActive = canvas.ActiveLayerIndex;
                            history.SaveState(canvas);
                            canvas.Layers[i].Dispose();
                            canvas.Layers.RemoveAt(i);

                            if (prevActive > i)
                                canvas.ActiveLayerIndex = prevActive - 1;
                            else if (prevActive >= canvas.Layers.Count)
                                canvas.ActiveLayerIndex = canvas.Layers.Count - 1;
                            else
                                canvas.ActiveLayerIndex = prevActive;

                            canvas.Frames[canvas.ActiveFrameIndex].IsThumbnailDirty = true;
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
        }
        else
        {
            for (int j = 0; j < 2; j++) { Raylib.DrawLine(cx - 4, cy - 2 + j, cx, cy + 2 + j, Color.LightGray); Raylib.DrawLine(cx, cy + 2 + j, cx + 4, cy - 2 + j, Color.LightGray); }
        }

        if (SlideProgress >= 0.99f) return;

        if (AppSettings.ShowPanelBackdrops)
        {
            Raylib.DrawRectangle((int)panelRect.X, (int)panelRect.Y - 4, (int)panelRect.Width, 4, new Color(0, 0, 0, 75));
            Raylib.DrawRectangleRec(panelRect, new Color(24, 24, 28, 225));
            Raylib.DrawLine((int)panelRect.X, (int)panelRect.Y, (int)(panelRect.X + panelRect.Width), (int)panelRect.Y, new Color(55, 55, 65, 255));
        }

        int boxSize = ThumbnailSize;
        int spacing = 10;
        float startX = panelRect.X + 12 - scrollX;
        float startY = panelRect.Y + (Height - boxSize) / 2f;
        float listAvailableWidth = Math.Max(80f, panelRect.Width - RightControlsWidth);
        Vector2 mousePos = Raylib.GetMousePosition();

        int visibleH = Math.Max(0, screenHeight - (int)panelRect.Y);
        if (visibleH <= 0) return;

        Raylib.BeginScissorMode((int)panelRect.X, (int)panelRect.Y, (int)listAvailableWidth, visibleH);

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
            bool isShiftMerge = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
            if (isShiftMerge && dropIndex != draggingLayerIndex)
            {
                Rectangle targetBox = new Rectangle(startX + dropIndex * (boxSize + spacing) - 2, startY - 2, boxSize + 4, boxSize + 4);
                Raylib.DrawRectangleLinesEx(targetBox, 2f, PixelSkin.AccentGold);
            }
            else
            {
                Raylib.DrawRectangle((int)(startX + dropIndex * (boxSize + spacing) - (spacing / 2f)) - 2, (int)startY, 4, boxSize, Color.Yellow);
            }
            DrawLayerBox(canvas, draggingLayerIndex, dragRect, mousePos, isDragging: true);
        }

        int itemCount = canvas.Layers.Count + 1;
        float totalWidth = itemCount * boxSize + (itemCount - 1) * spacing + 24;
        float maxScroll = Math.Max(0, totalWidth - listAvailableWidth);
        if (maxScroll > 0)
        {
            float trackX = panelRect.X + 12;
            float trackY = panelRect.Y + Height - 8;
            float trackWidth = listAvailableWidth - 24;
            float thumbWidth = Math.Max(40f, (listAvailableWidth / totalWidth) * trackWidth);
            Raylib.DrawRectangle((int)trackX, (int)trackY, (int)trackWidth, 4, new Color(15, 15, 15, 180));
            Raylib.DrawRectangle((int)(trackX + (scrollX / maxScroll) * (trackWidth - thumbWidth)), (int)trackY, (int)thumbWidth, 4, isDraggingScrollbar ? Color.SkyBlue : Color.Gray);
        }

        Raylib.EndScissorMode();

        // SAĞ KONTROL BUTONLARI (Birleştir, Ayır, Katman Ayarları)
        Raylib.BeginScissorMode((int)panelRect.X, (int)panelRect.Y, (int)panelRect.Width, visibleH);

        float btnY = panelRect.Y + 13f;
        float btnH = 58f;
        Rectangle mergeBtn = new Rectangle(panelRect.X + panelRect.Width - 164f, btnY, 48f, btnH);
        Rectangle splitBtn = new Rectangle(panelRect.X + panelRect.Width - 111f, btnY, 48f, btnH);
        Rectangle settingsBtn = new Rectangle(panelRect.X + panelRect.Width - 58f, btnY, 46f, btnH);

        bool hoverMerge = Raylib.CheckCollisionPointRec(mousePos, mergeBtn);
        bool hoverSplit = Raylib.CheckCollisionPointRec(mousePos, splitBtn);
        bool hoverSet = Raylib.CheckCollisionPointRec(mousePos, settingsBtn);

        bool canSplitMerged = canvas.Layers.Count > 0 && canvas.Layers[canvas.ActiveLayerIndex].HasMergedSources;

        PixelSkin.DrawButton(mergeBtn, hoverMerge, false);
        PixelSkin.DrawButton(splitBtn, hoverSplit, canSplitMerged);
        PixelSkin.DrawButton(settingsBtn, hoverSet, IsSettingsOpen);

        // Birleştir İkonu (İki katman üst üste + aşağı ok)
        int mCx = (int)(mergeBtn.X + mergeBtn.Width / 2f);
        int mCy = (int)mergeBtn.Y + 21;
        Raylib.DrawRectangleLines(mCx - 9, mCy - 9, 18, 6, Color.White);
        Raylib.DrawRectangle(mCx - 9, mCy + 2, 18, 6, PixelSkin.AccentCyan);
        Raylib.DrawLine(mCx, mCy - 2, mCx, mCy + 1, PixelSkin.AccentGold);
        int mTw = Raylib.MeasureText("Birlestir", 10);
        Raylib.DrawText("Birlestir", (int)(mergeBtn.X + (mergeBtn.Width - mTw) / 2f), (int)mergeBtn.Y + 41, 10, hoverMerge ? PixelSkin.AccentGold : PixelSkin.TextPrimary);

        // Ayır İkonu (İki katman birbirinden ayrılıyor)
        int sCx = (int)(splitBtn.X + splitBtn.Width / 2f);
        int sCy = (int)splitBtn.Y + 21;
        Raylib.DrawRectangle(sCx - 10, sCy - 9, 12, 6, PixelSkin.AccentGold);
        Raylib.DrawRectangle(sCx - 2, sCy + 2, 12, 6, Color.White);
        int sTw = Raylib.MeasureText("Ayir", 10);
        Raylib.DrawText("Ayir", (int)(splitBtn.X + (splitBtn.Width - sTw) / 2f), (int)splitBtn.Y + 41, 10, canSplitMerged || hoverSplit ? PixelSkin.AccentGold : PixelSkin.TextPrimary);

        // Ayarlar (Dişli Çark) İkonu - AnimationPanel ile birebir uyumlu
        int iconOffsetSet = IsSettingsOpen ? 2 : (hoverSet ? 1 : 0);
        int gCx = (int)(settingsBtn.X + settingsBtn.Width / 2f);
        int gCy = (int)(settingsBtn.Y + settingsBtn.Height / 2f) + iconOffsetSet;
        Raylib.DrawCircleLines(gCx, gCy, 8, Color.LightGray);
        Raylib.DrawCircleLines(gCx, gCy, 4, Color.White);

        Raylib.EndScissorMode();

        // KATMAN AYARLARI AÇILIR PENCERESİ (Scissor dışında çizilir ki üst tarafa doğru tam görünsün)
        if (IsSettingsOpen && SlideProgress < 0.05f)
        {
            Rectangle setRect = GetSettingsPopupRect(settingsBtn, panelRect);
            PixelSkin.DrawPanel(setRect);

            Layer activeLayer = canvas.Layers[canvas.ActiveLayerIndex];
            int opPct = (int)Math.Round(activeLayer.Opacity / 255f * 100f);

            Raylib.DrawRectangle((int)setRect.X, (int)setRect.Y, (int)setRect.Width, 24, new Color(45, 45, 52, 255));
            Raylib.DrawText("Katman Ayarlari", (int)setRect.X + 10, (int)setRect.Y + 6, 12, Color.RayWhite);

            // 1. Kutu Boyutu
            Raylib.DrawText("Kutu Boyutu:", (int)setRect.X + 10, (int)setRect.Y + 37, 12, Color.LightGray);
            DrawSmallBtn(new Rectangle(setRect.X + 112, setRect.Y + 32, 24, 22), "-", mousePos);
            string szTxt = ThumbnailSize.ToString();
            Raylib.DrawText(szTxt, (int)(setRect.X + 157 - Raylib.MeasureText(szTxt, 12) / 2f), (int)setRect.Y + 37, 12, Color.Yellow);
            DrawSmallBtn(new Rectangle(setRect.X + 178, setRect.Y + 32, 24, 22), "+", mousePos);

            // 2. Katman Opaklığı
            Raylib.DrawText("Opaklik:", (int)setRect.X + 10, (int)setRect.Y + 65, 12, Color.LightGray);
            DrawSmallBtn(new Rectangle(setRect.X + 112, setRect.Y + 60, 24, 22), "-", mousePos);
            string opTxt = $"%{opPct}";
            Raylib.DrawText(opTxt, (int)(setRect.X + 157 - Raylib.MeasureText(opTxt, 12) / 2f), (int)setRect.Y + 65, 12, Color.Yellow);
            DrawSmallBtn(new Rectangle(setRect.X + 178, setRect.Y + 60, 24, 22), "+", mousePos);

            // 3. Alfa Kilidi
            Raylib.DrawText("Alfa Kilidi:", (int)setRect.X + 10, (int)setRect.Y + 93, 12, Color.LightGray);
            Rectangle alphaBtn = new Rectangle(setRect.X + 112, setRect.Y + 88, 90, 22);
            PixelSkin.DrawButton(alphaBtn, Raylib.CheckCollisionPointRec(mousePos, alphaBtn), activeLayer.AlphaLock);
            string alTxt = activeLayer.AlphaLock ? "Acik" : "Kapali";
            Raylib.DrawText(alTxt, (int)(alphaBtn.X + (alphaBtn.Width - Raylib.MeasureText(alTxt, 11)) / 2f), (int)alphaBtn.Y + 6, 11, activeLayer.AlphaLock ? PixelSkin.AccentGold : Color.White);

            // 4. Birleştirme Butonları
            Rectangle popMergeDownBtn = new Rectangle(setRect.X + 10, setRect.Y + 120, 95, 26);
            Rectangle popMergeAllBtn = new Rectangle(setRect.X + 111, setRect.Y + 120, 95, 26);
            DrawActionBtn(popMergeDownBtn, "Altla Birlestir", mousePos);
            DrawActionBtn(popMergeAllBtn, "Tumu Birlestir", mousePos);

            // 5. Ayırma ve Çoğaltma Butonları
            Rectangle popSplitBtn = new Rectangle(setRect.X + 10, setRect.Y + 152, 95, 26);
            Rectangle popDupBtn = new Rectangle(setRect.X + 111, setRect.Y + 152, 95, 26);
            DrawActionBtn(popSplitBtn, "Katmani Ayir", mousePos, activeLayer.HasMergedSources);
            DrawActionBtn(popDupBtn, "Cogalt (Kopya)", mousePos);

            // 6. Varsayılana Dön
            Rectangle resetBtn = new Rectangle(setRect.X + 10, setRect.Y + 190, 196, 28);
            PixelSkin.DrawButton(resetBtn, Raylib.CheckCollisionPointRec(mousePos, resetBtn), false);
            int rTw = Raylib.MeasureText("Varsayilana Don", 12);
            Raylib.DrawText("Varsayilana Don", (int)(resetBtn.X + (resetBtn.Width - rTw) / 2f), (int)resetBtn.Y + 8, 12, Color.White);
        }
    }

    private static void DrawSmallBtn(Rectangle rect, string text, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        PixelSkin.DrawButton(rect, hover, false);
        int tw = Raylib.MeasureText(text, 14);
        int offset = hover ? 1 : 0;
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)rect.Y + 4 + offset, 14, Color.White);
    }

    private static void DrawActionBtn(Rectangle rect, string text, Vector2 mousePos, bool highlight = false)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        PixelSkin.DrawButton(rect, hover, highlight);
        int tw = Raylib.MeasureText(text, 11);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2f), (int)rect.Y + 7, 11, hover || highlight ? PixelSkin.AccentGold : Color.White);
    }

    private void DrawLayerBox(Canvas canvas, int index, Rectangle boxRect, Vector2 mousePos, bool isDragging = false)
    {
        int boxSize = (int)boxRect.Width;
        bool isActive = (index == canvas.ActiveLayerIndex);
        Layer layer = canvas.Layers[index];
        bool vis = layer.IsVisible;

        for (int cX = 0; cX < boxSize; cX += 8)
            for (int cY = 0; cY < boxSize; cY += 8)
                Raylib.DrawRectangle((int)boxRect.X + cX, (int)boxRect.Y + cY, Math.Min(8, boxSize - cX), Math.Min(8, boxSize - cY),
                    ((cX / 8 + cY / 8) % 2 == 0) ? new Color(170, 170, 170, 255) : new Color(130, 130, 130, 255));

        float scale = (float)boxSize / Math.Max(canvas.Width, canvas.Height);
        float drawW = canvas.Width * scale;
        float drawH = canvas.Height * scale;
        float thumbX = boxRect.X + (boxSize - drawW) / 2f;
        float thumbY = boxRect.Y + (boxSize - drawH) / 2f;

        Texture2D layerTex = layer.GetOrUpdateTexture();
        Raylib.DrawTexturePro(
            layerTex,
            new Rectangle(0, 0, canvas.Width, canvas.Height),
            new Rectangle(thumbX, thumbY, drawW, drawH),
            Vector2.Zero, 0f,
            new Color((byte)255, (byte)255, (byte)255, layer.Opacity)
        );

        if (!vis) Raylib.DrawRectangleRec(boxRect, new Color(0, 0, 0, 150));

        Raylib.DrawRectangle((int)boxRect.X, (int)boxRect.Y, boxSize, 18, new Color(0, 0, 0, 160));
        string lName = layer.Name;
        if (lName.Length > 7) lName = lName[..7];
        Raylib.DrawText(lName, (int)boxRect.X + 5, (int)boxRect.Y + 4, 10, isActive ? Color.Yellow : Color.RayWhite);

        // Birleştirilmiş (Merge edilmiş) katmansa sağ üstte ince gösterge noktası
        if (layer.HasMergedSources)
        {
            Raylib.DrawRectangle((int)boxRect.X + boxSize - 7, (int)boxRect.Y + 4, 4, 4, PixelSkin.AccentGold);
        }

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
