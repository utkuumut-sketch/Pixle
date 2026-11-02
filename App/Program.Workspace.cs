// === DOSYA: \App\Program.Workspace.cs ===
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;
using PixelArtApp.UI;
using PixelArtApp.IO;

namespace PixelArtApp;

partial class Program
{
    static void Update()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mouseScreenPos = Raylib.GetMousePosition();

        if (Raylib.IsKeyPressed(KeyboardKey.F11)) Raylib.ToggleFullscreen();

        palette.Offset = new Vector2(sw - RightPanelWidth + 20, WorkspaceTopY + 35);
        toolPanel.Width = LeftPanelWidth;

        if (UpdateActiveDialogs(mouseScreenPos)) return;
        if (UpdatePanelResize(mouseScreenPos, sw, sh)) return;
        if (HandleGlobalFileShortcuts()) return;

        bool topMenuIntercepted = topMenu.Update(mouseScreenPos);
        bool tabBarIntercepted = !topMenuIntercepted && tabBar.Update(
            mouseScreenPos, tabs, activeTabIndex,
            SwitchToTab, RequestCloseTab, () => newProjectDialog.Open()
        );

        if (ActiveTab == null)
        {
            if (!topMenuIntercepted && !tabBarIntercepted)
                homeScreen.Update(mouseScreenPos, sw, sh, WorkspaceTopY, () => newProjectDialog.Open(), OpenProjectWithDialog, LoadProjectFromFile);
            return;
        }

        Canvas canvas = ActiveTab.Canvas;
        SelectionManager selection = ActiveTab.Selection;
        HistoryManager history = ActiveTab.History;
        Camera2D camera = ActiveTab.Camera;

        UpdateCameraZoom(ref camera);
        if (UpdateBrushSizeInput(mouseScreenPos, sw)) return;

        if (Raylib.IsKeyPressed(AppSettings.KeyToggleAnimPanel)) animationPanel.ToggleHidden();
        if (Raylib.IsKeyPressed(AppSettings.KeyToggleLayers)) layerPanel.ToggleHidden();

        if (selection.HasSelection && !selection.IsCreatingSelection && !selection.IsDraggingFloating)
        {
            if (selection.IsDraggingDial) return;
            Rectangle menuRect = selection.GetFloatingMenuBounds(canvas, camera);
            if (Raylib.CheckCollisionPointRec(mouseScreenPos, menuRect) &&
                (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonDown(MouseButton.Left)))
                return;
        }

        bool animPanelIntercepted = false;
        if (!topMenuIntercepted && !tabBarIntercepted)
        {
            float prevFps = animationPanel.Fps;
            animPanelIntercepted = animationPanel.Update(canvas, history, mouseScreenPos, sw, sh, () => { selection.CancelOrCommit(canvas); ShowStatus($"Aktif Kare: {canvas.ActiveFrameIndex + 1}"); });
            if (Math.Abs(animationPanel.Fps - prevFps) > 0.01f)
            {
                ActiveTab.Fps = animationPanel.Fps;
                ActiveTab.IsDirty = true;
            }
        }

        bool toolPanelIntercepted = toolPanel.Update(
            mouseScreenPos, SelectTool, shape => toolManager.ActiveShape = shape,
            selMode => { selection.CancelOrCommit(canvas); toolManager.ActiveSelectionMode = selMode; }
        );

        bool paletteIntercepted = palette.Update(mouseScreenPos, () => colorPicker.Open(palette.CurrentColor));
        bool layerPanelIntercepted = layerPanel.Update(canvas, history, mouseScreenPos, sw, sh, () => { selection.CancelOrCommit(canvas); ActiveTab.IsDirty = true; ShowStatus($"Aktif Katman: {canvas.Layers[canvas.ActiveLayerIndex].Name}"); });

        bool insideDrawingArea = !topMenuIntercepted && !tabBarIntercepted && !animPanelIntercepted &&
                                 !toolPanelIntercepted && !paletteIntercepted && !layerPanelIntercepted &&
                                 mouseScreenPos.X >= LeftPanelWidth && mouseScreenPos.X < (sw - RightPanelWidth) &&
                                 mouseScreenPos.Y >= WorkspaceTopY;

        bool isCtrlDown = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);

        UpdateCanvasCameraNavigation(mouseScreenPos, insideDrawingArea, isCtrlDown, ref camera);
        HandleWorkspaceShortcuts(canvas, selection, history, isCtrlDown, topMenuIntercepted || tabBarIntercepted || paletteIntercepted || toolPanelIntercepted);
        HandleCanvasDrawingAndSelection(canvas, selection, history, camera, mouseScreenPos, insideDrawingArea, isCtrlDown);

        if (messageTimer > 0)
        {
            messageTimer -= Raylib.GetFrameTime();
            if (messageTimer <= 0) statusMessage = "Hazir";
        }
    }

    static bool UpdateActiveDialogs(Vector2 mouseScreenPos)
    {
        if (unsavedDialog.IsOpen) { unsavedDialog.Update(mouseScreenPos); return true; }
        if (newProjectDialog.IsOpen) { newProjectDialog.Update(mouseScreenPos, (w, h, bg) => CreateNewTab(w, h, bg)); return true; }
        if (exportDialog.IsOpen)
        {
            exportDialog.Update(mouseScreenPos, (path, scale, allFrames, vertical) =>
            {
                if (ActiveTab != null)
                {
                    if (Exporter.SaveExport(ActiveTab.Canvas, path, scale, allFrames, vertical))
                    {
                        homeScreen.AddRecentFile(path);
                        ShowStatus($"Disa aktarildi: {Path.GetFileName(path)}");
                    }
                    else ShowStatus("Disa aktarma basarisiz oldu veya iptal edildi.");
                }
            });
            return true;
        }
        if (settingsDialog.IsOpen) { settingsDialog.Update(mouseScreenPos, ToggleGrid, isGridVisible); return true; }
        if (colorPicker.IsOpen)
        {
            colorPicker.Update(mouseScreenPos, (newColor, addToPalette) =>
            {
                if (addToPalette)
                {
                    palette.AddColor(newColor);
                    if (ActiveTab != null) ActiveTab.IsDirty = true;
                    ShowStatus("Ozel renk palete eklendi!");
                }
                else
                {
                    palette.SetColor(newColor);
                    ShowStatus("Yeni renk secildi.");
                }
            });
            return true;
        }
        return false;
    }

    static bool UpdatePanelResize(Vector2 mouseScreenPos, int sw, int sh)
    {
        if (ActiveTab != null && AppSettings.EnablePanelResize)
        {
            int maxLeftAllowed = Math.Max(110, Math.Min(sw / 2 - 80, sw - RightPanelWidth - 160));
            int maxRightAllowed = Math.Max(165, Math.Min(sw / 2 - 80, sw - LeftPanelWidth - 160));

            LeftPanelWidth = Math.Clamp(LeftPanelWidth, 110, maxLeftAllowed);
            RightPanelWidth = Math.Clamp(RightPanelWidth, 165, maxRightAllowed);
            toolPanel.Width = LeftPanelWidth;

            Rectangle leftDragZone = new Rectangle(LeftPanelWidth - 4, WorkspaceTopY, 8, sh - WorkspaceTopY);
            Rectangle rightDragZone = new Rectangle(sw - RightPanelWidth - 4, WorkspaceTopY, 8, sh - WorkspaceTopY);

            isHoveringLeftEdge = Raylib.CheckCollisionPointRec(mouseScreenPos, leftDragZone);
            isHoveringRightEdge = Raylib.CheckCollisionPointRec(mouseScreenPos, rightDragZone);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (isHoveringLeftEdge) isDraggingLeftPanel = true;
                else if (isHoveringRightEdge) isDraggingRightPanel = true;
            }

            if (isDraggingLeftPanel)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    LeftPanelWidth = Math.Clamp((int)mouseScreenPos.X, 110, maxLeftAllowed);
                    toolPanel.Width = LeftPanelWidth;
                }
                else isDraggingLeftPanel = false;
                return true;
            }

            if (isDraggingRightPanel)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                    RightPanelWidth = Math.Clamp(sw - (int)mouseScreenPos.X, 165, maxRightAllowed);
                else isDraggingRightPanel = false;
                return true;
            }
        }
        else
        {
            isHoveringLeftEdge = false;
            isHoveringRightEdge = false;
        }
        return false;
    }

    static bool HandleGlobalFileShortcuts()
    {
        bool isCtrlDown = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        bool isShiftDown = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

        if (!isCtrlDown) return false;

        if (Raylib.IsKeyPressed(KeyboardKey.N)) { newProjectDialog.Open(); return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.O)) { OpenProjectWithDialog(); return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.W) && activeTabIndex >= 0) { RequestCloseTab(activeTabIndex); return true; }
        if (Raylib.IsKeyPressed(KeyboardKey.S))
        {
            if (ActiveTab != null) SaveTabProject(ActiveTab, saveAs: isShiftDown);
            return true;
        }
        if (Raylib.IsKeyPressed(KeyboardKey.E))
        {
            if (ActiveTab != null) exportDialog.Open(ActiveTab.Title, ActiveTab.Canvas);
            return true;
        }
        return false;
    }

    static void UpdateCameraZoom(ref Camera2D camera)
    {
        if (ActiveTab == null) return;
        targetZoom = ZoomLevels[ActiveTab.ZoomIndex];

        if (!AppSettings.SmoothCameraZoom || Math.Abs(camera.Zoom - targetZoom) <= 0.005f)
        {
            if (camera.Zoom != targetZoom)
            {
                camera.Zoom = targetZoom;
                ActiveTab.Camera = camera;
            }
        }
        else
        {
            camera.Zoom = Raymath.Lerp(camera.Zoom, targetZoom, Raylib.GetFrameTime() * 14f);
            ActiveTab.Camera = camera;
        }
    }

    static bool UpdateBrushSizeInput(Vector2 mouseScreenPos, int sw)
    {
        int rightPanelX = sw - RightPanelWidth;
        Rectangle sizeBoxRect = new Rectangle(rightPanelX + 75, 518, 45, 22);
        Rectangle btnMinusRect = new Rectangle(rightPanelX + 52, 518, 20, 22);
        Rectangle btnPlusRect = new Rectangle(rightPanelX + 123, 518, 20, 22);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mouseScreenPos, sizeBoxRect))
            {
                sizeInputActive = true;
                int curSize = toolManager.ActiveTool == ToolType.Eraser ? toolManager.EraserSize : toolManager.PenSize;
                sizeInputText = curSize.ToString();
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mouseScreenPos, btnMinusRect))
            {
                if (toolManager.ActiveTool == ToolType.Eraser) toolManager.EraserSize = Math.Clamp(toolManager.EraserSize - 1, 1, 50);
                else toolManager.PenSize = Math.Clamp(toolManager.PenSize - 1, 1, 50);
                sizeInputActive = false;
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mouseScreenPos, btnPlusRect))
            {
                if (toolManager.ActiveTool == ToolType.Eraser) toolManager.EraserSize = Math.Clamp(toolManager.EraserSize + 1, 1, 50);
                else toolManager.PenSize = Math.Clamp(toolManager.PenSize + 1, 1, 50);
                sizeInputActive = false;
                return true;
            }
            if (sizeInputActive) ApplyTypedSize();
        }

        if (sizeInputActive)
        {
            int key = Raylib.GetCharPressed();
            while (key > 0)
            {
                if (key >= '0' && key <= '9' && sizeInputText.Length < 2) sizeInputText += (char)key;
                key = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && sizeInputText.Length > 0)
                sizeInputText = sizeInputText[..^1];
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Escape))
                ApplyTypedSize();
            return true;
        }
        return false;
    }

    static void UpdateCanvasCameraNavigation(Vector2 mouseScreenPos, bool insideDrawingArea, bool isCtrlDown, ref Camera2D camera)
    {
        if (ActiveTab == null) return;

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0 && insideDrawingArea)
        {
            if (isCtrlDown)
            {
                if (toolManager.ActiveTool == ToolType.Eraser)
                {
                    toolManager.EraserSize = Math.Clamp(toolManager.EraserSize + (int)Math.Sign(wheel), 1, 50);
                    ShowStatus($"Silgi Boyutu: {toolManager.EraserSize}px");
                }
                else
                {
                    toolManager.PenSize = Math.Clamp(toolManager.PenSize + (int)Math.Sign(wheel), 1, 50);
                    ShowStatus($"Firca Boyutu: {toolManager.PenSize}px");
                }
            }
            else
            {
                int prevIndex = ActiveTab.ZoomIndex;
                if (wheel > 0 && ActiveTab.ZoomIndex < ZoomLevels.Length - 1) ActiveTab.ZoomIndex++;
                else if (wheel < 0 && ActiveTab.ZoomIndex > 0) ActiveTab.ZoomIndex--;

                if (prevIndex != ActiveTab.ZoomIndex)
                {
                    Vector2 mouseWorldPos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
                    camera.Offset = mouseScreenPos;
                    camera.Target = mouseWorldPos;
                    targetZoom = ZoomLevels[ActiveTab.ZoomIndex];
                    ActiveTab.Camera = camera;
                }
            }
        }

        if (Raylib.IsMouseButtonDown(MouseButton.Middle))
        {
            Vector2 delta = Vector2.Multiply(Raylib.GetMouseDelta(), -1.0f / camera.Zoom);
            camera.Target = Vector2.Add(camera.Target, delta);
            ActiveTab.Camera = camera;
        }
    }

    static void HandleWorkspaceShortcuts(Canvas canvas, SelectionManager selection, HistoryManager history, bool isCtrlDown, bool uiIntercepted)
    {
        if (!isCtrlDown && !uiIntercepted && !sizeInputActive && !selection.isRotateInputActive)
        {
            for (int i = 0; i < AppSettings.KeyColors.Length; i++)
            {
                if (AppSettings.KeyColors[i] != KeyboardKey.Null && Raylib.IsKeyPressed(AppSettings.KeyColors[i]))
                    if (palette.SelectColor(i)) ShowStatus($"Renk {i + 1} secildi.");
            }

            if (Raylib.IsKeyPressed(AppSettings.KeyPen)) SelectTool(ToolType.Pen);
            if (Raylib.IsKeyPressed(AppSettings.KeyEraser)) SelectTool(ToolType.Eraser);
            if (Raylib.IsKeyPressed(AppSettings.KeyBucket)) SelectTool(ToolType.Bucket);
            if (Raylib.IsKeyPressed(AppSettings.KeySmartBucket)) SelectTool(ToolType.SmartBucket);
            if (Raylib.IsKeyPressed(AppSettings.KeyEyedropper)) SelectTool(ToolType.Eyedropper);
            if (Raylib.IsKeyPressed(AppSettings.KeyBlur)) SelectTool(ToolType.Blur);
            if (Raylib.IsKeyPressed(AppSettings.KeyJumble)) SelectTool(ToolType.Jumble);
            if (Raylib.IsKeyPressed(AppSettings.KeyLine)) SelectTool(ToolType.Line);
            if (Raylib.IsKeyPressed(AppSettings.KeyShape)) SelectTool(ToolType.Shape);
            if (Raylib.IsKeyPressed(AppSettings.KeySelectMove)) SelectTool(ToolType.SelectMove);
        }

        if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.Z)) { selection.CancelOrCommit(canvas); history.Undo(canvas); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.Y)) { selection.CancelOrCommit(canvas); history.Redo(canvas); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.C) && selection.HasSelection) { selection.Copy(canvas); ShowStatus("Secim kopyalandi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.X) && selection.HasSelection) { selection.Cut(canvas, history); ShowStatus("Secim kesildi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.V)) { SelectTool(ToolType.SelectMove); selection.Paste(canvas, history); ShowStatus("Yapistirildi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.D) && selection.HasSelection) { selection.Duplicate(canvas, history); ShowStatus("Secim cogaltildi"); }

        if ((Raylib.IsKeyPressed(KeyboardKey.Delete) || Raylib.IsKeyPressed(KeyboardKey.Backspace)) && selection.HasSelection && !selection.isRotateInputActive)
        {
            selection.DeleteSelection(canvas, history);
            ShowStatus("Secim silindi");
        }
        if ((Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter)) && selection.HasSelection)
        {
            selection.CancelOrCommit(canvas);
            ShowStatus("Secim kapatildi");
        }
    }

    static void HandleCanvasDrawingAndSelection(Canvas canvas, SelectionManager selection, HistoryManager history, Camera2D camera, Vector2 mouseScreenPos, bool insideDrawingArea, bool isCtrlDown)
    {
        Vector2 worldMousePos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
        var (gx, gy) = canvas.ScreenToGrid(worldMousePos);

        if (!Raylib.IsMouseButtonDown(MouseButton.Left) && !Raylib.IsMouseButtonDown(MouseButton.Right))
            lastGridPos = null;

        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
        {
            dragStartPos = null;
            isDrawingWithLeft = false;
        }
        if (Raylib.IsMouseButtonReleased(MouseButton.Right))
            isDrawingWithRight = false;

        if (toolManager.ActiveTool == ToolType.SelectMove)
        {
            bool isAdd = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
            bool isSubtract = isCtrlDown || Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);

            if (insideDrawingArea && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (!isAdd && !isSubtract && selection.HasSelection && selection.IsPointInCurrentSelection(gx, gy))
                    selection.StartMove(canvas, history, gx, gy);
                else if (canvas.IsMouseInside(worldMousePos))
                    selection.StartSelection(canvas, gx, gy, toolManager.ActiveSelectionMode, isAdd, isSubtract);
                else
                    selection.CancelOrCommit(canvas);
            }

            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                if (selection.IsDraggingFloating) selection.UpdateMove(gx, gy);
                else if (selection.IsCreatingSelection) selection.UpdateSelectionCreation(gx, gy, toolManager.ActiveSelectionMode);
            }
            if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                if (selection.IsDraggingFloating) selection.EndMove();
                else if (selection.IsCreatingSelection) selection.EndSelectionCreation(canvas, toolManager.ActiveSelectionMode, isAdd, isSubtract);
            }
            return;
        }

        if (!insideDrawingArea || !canvas.IsMouseInside(worldMousePos)) return;

        if (toolManager.ActiveTool == ToolType.Eyedropper || Raylib.IsKeyDown(KeyboardKey.LeftAlt))
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                Color pickedColor = canvas.GetMergedPixel(gx, gy);
                if (pickedColor.A > 0) { palette.SetColor(pickedColor); ShowStatus($"Renk secildi: R:{pickedColor.R}"); }
                if (toolManager.ActiveTool == ToolType.Eyedropper && AppSettings.EyedropperAutoSwitchPen)
                    toolManager.ActiveTool = ToolType.Pen;
            }
        }
        else if (toolManager.ActiveTool == ToolType.Bucket || toolManager.ActiveTool == ToolType.SmartBucket)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                history.SaveState(canvas);
                toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor);
            }
        }
        else if (toolManager.ActiveTool == ToolType.Line || toolManager.ActiveTool == ToolType.Shape)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                dragStartPos = (gx, gy);
            }
            else if (Raylib.IsMouseButtonReleased(MouseButton.Left) && dragStartPos.HasValue)
            {
                history.SaveState(canvas);
                List<(int x, int y)> shapePixels = (toolManager.ActiveTool == ToolType.Line)
                    ? ToolManager.GetLinePixels(dragStartPos.Value.x, dragStartPos.Value.y, gx, gy)
                    : ToolManager.GetShapePixels(dragStartPos.Value.x, dragStartPos.Value.y, gx, gy, toolManager.ActiveShape);
                foreach (var (px, py) in shapePixels) canvas.SetPixel(px, py, palette.CurrentColor);
                dragStartPos = null;
            }
        }
        else
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { isDrawingWithLeft = true; history.SaveState(canvas); }
            else if (Raylib.IsMouseButtonPressed(MouseButton.Right)) { isDrawingWithRight = true; history.SaveState(canvas); }

            if (isDrawingWithLeft && Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                if (lastGridPos.HasValue)
                {
                    if (!((toolManager.ActiveTool == ToolType.Blur || toolManager.ActiveTool == ToolType.Jumble) && lastGridPos.Value == (gx, gy)))
                        toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, palette.CurrentColor);
                }
                else toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor);
                lastGridPos = (gx, gy);
            }
            else if (isDrawingWithRight && Raylib.IsMouseButtonDown(MouseButton.Right))
            {
                if (lastGridPos.HasValue) toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, Canvas.TransparentColor, forceEraser: true);
                else toolManager.ApplyTool(canvas, gx, gy, Canvas.TransparentColor, forceEraser: true);
                lastGridPos = (gx, gy);
            }
        }
    }

    static void Draw()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mouseScreenPos = Raylib.GetMousePosition();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(PixelSkin.WindowBg);

        if (ActiveTab == null)
        {
            homeScreen.Draw(sw, sh, WorkspaceTopY);
        }
        else
        {
            DrawActiveWorkspace(sw, sh, mouseScreenPos);
        }

        tabBar.Draw(tabs, activeTabIndex, sw);
        topMenu.Draw(sw);

        colorPicker.Draw();
        newProjectDialog.Draw();
        settingsDialog.Draw(isGridVisible);
        exportDialog.Draw();
        unsavedDialog.Draw();

        Raylib.EndDrawing();
    }

    static void DrawActiveWorkspace(int sw, int sh, Vector2 mouseScreenPos)
    {
        Canvas canvas = ActiveTab!.Canvas;
        SelectionManager selection = ActiveTab.Selection;
        HistoryManager history = ActiveTab.History;
        Camera2D camera = ActiveTab.Camera;

        Raylib.BeginMode2D(camera);
        canvas.Draw();

        Vector2 worldMousePos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
        var (gx, gy) = canvas.ScreenToGrid(worldMousePos);

        if (dragStartPos.HasValue && (toolManager.ActiveTool == ToolType.Line || toolManager.ActiveTool == ToolType.Shape))
        {
            List<(int x, int y)> previewPixels = (toolManager.ActiveTool == ToolType.Line)
                ? ToolManager.GetLinePixels(dragStartPos.Value.x, dragStartPos.Value.y, gx, gy)
                : ToolManager.GetShapePixels(dragStartPos.Value.x, dragStartPos.Value.y, gx, gy, toolManager.ActiveShape);
            Color previewColor = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)180);
            foreach (var (px, py) in previewPixels)
                if (px >= 0 && px < canvas.Width && py >= 0 && py < canvas.Height)
                    Raylib.DrawRectangle((int)canvas.Offset.X + px * canvas.PixelScale, (int)canvas.Offset.Y + py * canvas.PixelScale, canvas.PixelScale, canvas.PixelScale, previewColor);
        }

        DrawBrushPreview(canvas, camera, worldMousePos, gx, gy);

        selection.DrawWorldOverlay(canvas);
        Raylib.EndMode2D();
        selection.CheckAndDrawFloatingMenu(canvas, camera, mouseScreenPos, history, ShowStatus);

        layerPanel.Draw(canvas, sw, sh);
        animationPanel.Draw(canvas, sw, sh);
        toolPanel.Draw(toolManager.ActiveTool, toolManager.ActiveShape, toolManager.ActiveSelectionMode, sh);

        DrawRightSidebar(canvas, sw, sh, mouseScreenPos);
    }

    static void DrawBrushPreview(Canvas canvas, Camera2D camera, Vector2 worldMousePos, int gx, int gy)
    {
        bool isBrushTool = toolManager.ActiveTool == ToolType.Pen ||
                           toolManager.ActiveTool == ToolType.Eraser ||
                           toolManager.ActiveTool == ToolType.Blur ||
                           toolManager.ActiveTool == ToolType.Jumble;

        if (!AppSettings.ShowBrushPreview || !canvas.IsMouseInside(worldMousePos) || !isBrushTool) return;

        int size = toolManager.ActiveTool == ToolType.Eraser ? toolManager.EraserSize : toolManager.PenSize;
        if (toolManager.ActiveTool == ToolType.Jumble && size < 2) size = 2;

        int offset = size / 2;
        Rectangle brushPreviewRect = new Rectangle(
            canvas.Offset.X + (gx - offset) * canvas.PixelScale,
            canvas.Offset.Y + (gy - offset) * canvas.PixelScale,
            size * canvas.PixelScale,
            size * canvas.PixelScale
        );

        float pulse = (float)(Math.Sin(Raylib.GetTime() * 8.0) * 0.5 + 0.5);
        byte alphaPulse = (byte)(150 + pulse * 105);

        if (toolManager.ActiveTool == ToolType.Pen)
            Raylib.DrawRectangleRec(brushPreviewRect, new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)85));

        float lineThickness = Math.Max(1f, 1.5f / camera.Zoom);
        Color outlineCol = toolManager.ActiveTool switch
        {
            ToolType.Eraser => new Color((byte)255, (byte)100, (byte)100, alphaPulse),
            ToolType.Blur => new Color((byte)100, (byte)200, (byte)255, alphaPulse),
            ToolType.Jumble => new Color((byte)255, (byte)200, (byte)80, alphaPulse),
            _ => new Color((byte)255, (byte)255, (byte)255, alphaPulse)
        };

        Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness * 1.6f, new Color((byte)0, (byte)0, (byte)0, (byte)200));
        Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness, outlineCol);
    }

    static void DrawRightSidebar(Canvas canvas, int sw, int sh, Vector2 mouseScreenPos)
    {
        int rightPanelX = sw - RightPanelWidth;
        PixelSkin.DrawPanel(new Rectangle(rightPanelX, WorkspaceTopY, RightPanelWidth, sh - WorkspaceTopY));
        palette.Draw();

        PixelSkin.DrawInsetBox(new Rectangle(rightPanelX + 10, 430, RightPanelWidth - 20, 145));

        Raylib.DrawText($"Tuval: {canvas.Width}x{canvas.Height}", rightPanelX + 20, 442, 13, Color.LightGray);
        string toolDesc = toolManager.ActiveTool switch
        {
            ToolType.Shape => $"Sekil ({toolManager.ActiveShape})",
            ToolType.SelectMove => $"Sec ({toolManager.ActiveSelectionMode})",
            ToolType.Blur => "Bulanik (Blur)",
            ToolType.Jumble => "Karistir (Jumble)",
            _ => toolManager.ActiveTool.ToString()
        };
        Raylib.DrawText($"Arac: {toolDesc}", rightPanelX + 20, 466, 12, Color.Yellow);
        Raylib.DrawText($"Zoom: {ZoomLevels[ActiveTab!.ZoomIndex]}x", rightPanelX + 20, 490, 13, Color.LightGray);
        Raylib.DrawText("Boyut:", rightPanelX + 16, 523, 12, Color.LightGray);

        Rectangle btnMinus = new Rectangle(rightPanelX + 52, 518, 20, 22);
        Rectangle sizeBox = new Rectangle(rightPanelX + 75, 518, 45, 22);
        Rectangle btnPlus = new Rectangle(rightPanelX + 123, 518, 20, 22);

        PixelSkin.DrawButton(btnMinus, Raylib.CheckCollisionPointRec(mouseScreenPos, btnMinus), false);
        Raylib.DrawText("-", (int)btnMinus.X + 7, (int)btnMinus.Y + 4, 14, Color.White);

        PixelSkin.DrawInsetBox(sizeBox);
        if (sizeInputActive)
        {
            Raylib.DrawRectangleLinesEx(sizeBox, 1, Color.Yellow);
            string blink = ((int)(Raylib.GetTime() * 3) % 2 == 0) ? "|" : "";
            Raylib.DrawText(sizeInputText + blink, (int)sizeBox.X + 8, (int)sizeBox.Y + 5, 12, Color.Yellow);
        }
        else
        {
            int currentToolSize = toolManager.ActiveTool == ToolType.Eraser ? toolManager.EraserSize : toolManager.PenSize;
            Raylib.DrawText($"{currentToolSize}px", (int)sizeBox.X + 8, (int)sizeBox.Y + 5, 12, Color.RayWhite);
        }

        PixelSkin.DrawButton(btnPlus, Raylib.CheckCollisionPointRec(mouseScreenPos, btnPlus), false);
        Raylib.DrawText("+", (int)btnPlus.X + 6, (int)btnPlus.Y + 4, 14, Color.White);

        if (statusMessage != "Hazir") Raylib.DrawText(statusMessage, rightPanelX + 20, 550, 11, Color.Green);

        if (isHoveringLeftEdge || isDraggingLeftPanel)
            Raylib.DrawRectangle(LeftPanelWidth - 2, (int)WorkspaceTopY, 4, sh - (int)WorkspaceTopY, isDraggingLeftPanel ? Color.Yellow : Color.SkyBlue);

        if (isHoveringRightEdge || isDraggingRightPanel)
            Raylib.DrawRectangle(sw - RightPanelWidth - 2, (int)WorkspaceTopY, 4, sh - (int)WorkspaceTopY, isDraggingRightPanel ? Color.Yellow : Color.SkyBlue);
    }
}