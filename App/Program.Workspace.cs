// === DOSYA: \App\Program.Workspace.cs ===
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;
using PixelArtApp.Core.Palettes;
using PixelArtApp.UI;
using PixelArtApp.IO;

namespace PixelArtApp;

partial class Program
{
    static readonly int[] BrushQuickSizes = new int[] { 1, 2, 4, 8 };

    static void Update()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mouseScreenPos = Raylib.GetMousePosition();

        if (Raylib.IsKeyPressed(KeyboardKey.F11)) Raylib.ToggleFullscreen();

        palette.Offset = new Vector2(sw - RightPanelWidth + 16, WorkspaceTopY + 32);
        palette.ViewportHeight = ComputePaletteViewportHeight(sh);
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
        if (UpdateBrushSizeInput(mouseScreenPos, sw, sh)) return;

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
            mouseScreenPos,
            SelectTool,
            shape => toolManager.ActiveShape = shape,
            selMode => { selection.CancelOrCommit(canvas); toolManager.ActiveSelectionMode = selMode; },
            TogglePixelPerfect,
            CycleSymmetryMode,
            toolManager.ActiveTool,
            dither =>
            {
                toolManager.ActiveDither = dither;
                ShowStatus($"Dither Deseni: {ToolManager.DitherLabel(dither)}");
            },
            shading =>
            {
                toolManager.ActiveShadingMode = shading;
                ShowStatus($"Tonlama Modu: {ToolManager.ShadingModeLabel(shading)}");
            },
            ToggleAlphaLock,
            ToggleTileWrap
        );

        bool paletteIntercepted = palette.Update(
            mouseScreenPos,
            () => colorPicker.Open(palette.CurrentColor),
            presetName =>
            {
                if (ActiveTab != null) ActiveTab.IsDirty = true;
                ShowStatus($"Palet: {presetName}");
            },
            ImportPaletteFromFile
        );
        Rectangle refBounds = GetRightSidebarRefBounds(sw, sh);
        bool refIntercepted = refManager.UpdateUI(mouseScreenPos, refBounds);
        bool layerPanelIntercepted = layerPanel.Update(
            canvas,
            history,
            mouseScreenPos,
            sw,
            sh,
            () =>
            {
                ActiveTab.IsDirty = true;
                ShowStatus($"Aktif Katman: {canvas.Layers[canvas.ActiveLayerIndex].Name}");
            },
            selection,
            ShowStatus
        );

        bool insideDrawingArea = !topMenuIntercepted && !tabBarIntercepted && !animPanelIntercepted &&
                                 !toolPanelIntercepted && !paletteIntercepted && !refIntercepted && !layerPanelIntercepted &&
                                 mouseScreenPos.X >= LeftPanelWidth && mouseScreenPos.X < (sw - RightPanelWidth) &&
                                 mouseScreenPos.Y >= WorkspaceTopY;

        bool isCtrlDown = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);

        UpdateCanvasCameraNavigation(mouseScreenPos, insideDrawingArea, isCtrlDown, ref camera);
        HandleWorkspaceShortcuts(canvas, selection, history, isCtrlDown, topMenuIntercepted || tabBarIntercepted || paletteIntercepted || toolPanelIntercepted || refIntercepted || layerPanelIntercepted);
        HandleCanvasDrawingAndSelection(canvas, selection, history, camera, mouseScreenPos, insideDrawingArea, isCtrlDown);

        if (messageTimer > 0)
        {
            messageTimer -= Raylib.GetFrameTime();
            if (messageTimer <= 0) statusMessage = "Hazir";
        }
    }

    static void TogglePixelPerfect()
    {
        toolManager.PixelPerfect = !toolManager.PixelPerfect;
        ShowStatus($"Pixel-Perfect: {(toolManager.PixelPerfect ? "Acik" : "Kapali")}");
    }

    static void CycleSymmetryMode()
    {
        toolManager.CycleSymmetry();
        ShowStatus($"Simetri Modu: {ToolManager.SymmetryLabel(toolManager.Symmetry)}");
    }

    static void ToggleAlphaLock()
    {
        if (ActiveTab == null) return;
        Canvas canvas = ActiveTab.Canvas;
        canvas.ActiveLayerAlphaLock = !canvas.ActiveLayerAlphaLock;
        ActiveTab.IsDirty = true;
        ShowStatus($"Alfa Kilidi ({canvas.Layers[canvas.ActiveLayerIndex].Name}): {(canvas.ActiveLayerAlphaLock ? "Acik" : "Kapali")}");
    }

    static void ToggleTileWrap()
    {
        isTileWrapEnabled = !isTileWrapEnabled;
        foreach (var t in tabs) t.Canvas.TileWrapMode = isTileWrapEnabled;
        ShowStatus($"Tile / Doseme Modu: {(isTileWrapEnabled ? "Acik (3x3)" : "Kapali")}");
    }

    static void CycleTileGrid()
    {
        AppSettings.CycleTileGridSize();
        ShowStatus($"Karo Izgarasi: {AppSettings.TileGridLabel(AppSettings.TileGridSize)}");
    }

    static void ImportPaletteFromFile()
    {
        string? path = Exporter.OpenPaletteDialog();
        if (string.IsNullOrWhiteSpace(path)) return;

        var loaded = PalettePresets.LoadFromFile(path, out string palName);
        if (loaded.Count > 0)
        {
            palette.LoadCustomPalette(loaded, palName);
            if (ActiveTab != null) ActiveTab.IsDirty = true;
            ShowStatus($"Palet ice aktarildi: {palName} ({loaded.Count} renk)");
        }
        else
        {
            ShowStatus("Palet dosyasi okunamadi veya gosterilecek renk bulunamadi.");
        }
    }

    static void ExportPaletteToFile()
    {
        string? path = Exporter.SavePaletteDialog(palette.CurrentPresetName);
        if (string.IsNullOrWhiteSpace(path)) return;

        if (PalettePresets.SaveAsHexFile(path, palette.GetColors()))
            ShowStatus($"Palet kaydedildi: {Path.GetFileName(path)}");
        else
            ShowStatus("Palet kaydedilemedi.");
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
                    ActiveTab.Fps = animationPanel.Fps;
                    if (Exporter.SaveExport(ActiveTab.Canvas, path, scale, allFrames, vertical, ActiveTab.Fps))
                    {
                        if (!path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
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
        const int MinCanvasCenterWidth = 260;
        int maxLeftAllowed = Math.Max(52, Math.Min(sw / 2 - 120, sw - RightPanelWidth - MinCanvasCenterWidth));
        int maxRightAllowed = Math.Max(165, Math.Min(sw / 2 - 120, sw - LeftPanelWidth - MinCanvasCenterWidth));

        LeftPanelWidth = Math.Clamp(LeftPanelWidth, 52, maxLeftAllowed);
        RightPanelWidth = Math.Clamp(RightPanelWidth, 165, maxRightAllowed);
        toolPanel.Width = LeftPanelWidth;

        if (ActiveTab != null && AppSettings.EnablePanelResize)
        {
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
                    LeftPanelWidth = Math.Clamp((int)mouseScreenPos.X, 52, maxLeftAllowed);
                    toolPanel.Width = LeftPanelWidth;
                }
                else isDraggingLeftPanel = false;
                return true;
            }

            if (isDraggingRightPanel)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    RightPanelWidth = Math.Clamp(sw - (int)mouseScreenPos.X, 165, maxRightAllowed);
                }
                else isDraggingRightPanel = false;
                return true;
            }
        }
        else
        {
            isHoveringLeftEdge = false;
            isHoveringRightEdge = false;
            isDraggingLeftPanel = false;
            isDraggingRightPanel = false;
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

    static float ComputePaletteViewportHeight(int sh)
    {
        if (!AppSettings.ShowReferencePanel)
            return Math.Clamp(sh - 378f, 54f, 252f);

        bool showingFlatPreview = refManager != null && refManager.Texture.HasValue && refManager.ShowInSidePanel;
        if (showingFlatPreview)
            return Math.Clamp(sh - 568f, 54f, 177f);

        return Math.Clamp(sh - 498f, 54f, 242f);
    }

    static float GetRightSidebarInfoBoxY(int sh)
    {
        float vpH = ComputePaletteViewportHeight(sh);
        return WorkspaceTopY + 32f + 78f + vpH + 4f;
    }

    static Rectangle GetRightSidebarRefBounds(int sw, int sh)
    {
        int rightPanelX = sw - RightPanelWidth;
        if (!AppSettings.ShowReferencePanel)
        {
            return new Rectangle(rightPanelX + 10, sh - 26f, RightPanelWidth - 20, 22f);
        }

        float infoBoxY = GetRightSidebarInfoBoxY(sh);
        float refY = infoBoxY + 150f;
        float refH = Math.Max(110f, sh - refY - 8f);
        return new Rectangle(rightPanelX + 10, refY, RightPanelWidth - 20, refH);
    }

    static bool UpdateBrushSizeInput(Vector2 mouseScreenPos, int sw, int sh)
    {
        int rightPanelX = sw - RightPanelWidth;
        float infoBoxY = GetRightSidebarInfoBoxY(sh);
        float btnY = infoBoxY + 86f;
        Rectangle sizeBoxRect = new Rectangle(rightPanelX + 75, btnY, 45, 22);
        Rectangle btnMinusRect = new Rectangle(rightPanelX + 52, btnY, 20, 22);
        Rectangle btnPlusRect = new Rectangle(rightPanelX + 123, btnY, 20, 22);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // Hızlı fırça boyutu önayarları (1px, 2px, 4px, 8px)
            float presetStartX = rightPanelX + 18f;
            float presetY = infoBoxY + 114f;
            for (int i = 0; i < BrushQuickSizes.Length; i++)
            {
                Rectangle pRect = new Rectangle(presetStartX + i * 36f, presetY, 32f, 20f);
                if (Raylib.CheckCollisionPointRec(mouseScreenPos, pRect))
                {
                    if (toolManager.ActiveTool == ToolType.Eraser) toolManager.EraserSize = BrushQuickSizes[i];
                    else toolManager.PenSize = BrushQuickSizes[i];
                    sizeInputActive = false;
                    return true;
                }
            }

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
            if (Raylib.IsKeyPressed(AppSettings.KeyDither)) SelectTool(ToolType.Dither);
            if (Raylib.IsKeyPressed(AppSettings.KeyShading)) SelectTool(ToolType.Shading);
            if (Raylib.IsKeyPressed(AppSettings.KeyBucket)) SelectTool(ToolType.Bucket);
            if (Raylib.IsKeyPressed(AppSettings.KeySmartBucket)) SelectTool(ToolType.SmartBucket);
            if (Raylib.IsKeyPressed(AppSettings.KeyEyedropper)) SelectTool(ToolType.Eyedropper);
            if (Raylib.IsKeyPressed(AppSettings.KeyBlur)) SelectTool(ToolType.Blur);
            if (Raylib.IsKeyPressed(AppSettings.KeyJumble)) SelectTool(ToolType.Jumble);
            if (Raylib.IsKeyPressed(AppSettings.KeyLine)) SelectTool(ToolType.Line);
            if (Raylib.IsKeyPressed(AppSettings.KeyCurve)) SelectTool(ToolType.Curve);
            if (Raylib.IsKeyPressed(AppSettings.KeyShape)) SelectTool(ToolType.Shape);
            if (Raylib.IsKeyPressed(AppSettings.KeySelectMove)) SelectTool(ToolType.SelectMove);
            if (Raylib.IsKeyPressed(AppSettings.KeyPixelPerfect)) TogglePixelPerfect();
            if (Raylib.IsKeyPressed(AppSettings.KeySymmetry)) CycleSymmetryMode();
            if (Raylib.IsKeyPressed(AppSettings.KeyAlphaLock)) ToggleAlphaLock();
            if (Raylib.IsKeyPressed(AppSettings.KeyTileWrap)) ToggleTileWrap();
            if (Raylib.IsKeyPressed(AppSettings.KeyTileGrid)) CycleTileGrid();
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
        if ((Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter)) && selection.HasSelection)
        {
            if (selection.IsInRotationMode)
            {
                if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
                {
                    selection.ConfirmRotationSession();
                    ShowStatus("Dondurme onaylandi");
                }
                else
                {
                    selection.CancelRotationSession();
                    ShowStatus("Dondurme iptal edildi");
                }
            }
            else
            {
                selection.CancelOrCommit(canvas);
                ShowStatus("Secim kapatildi");
            }
        }
    }

    static void HandleCanvasDrawingAndSelection(Canvas canvas, SelectionManager selection, HistoryManager history, Camera2D camera, Vector2 mouseScreenPos, bool insideDrawingArea, bool isCtrlDown)
    {
        if (animationPanel.IsPlaying)
        {
            if (insideDrawingArea && (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonPressed(MouseButton.Right)))
                ShowStatus("Cizim yapmak icin animasyonu durdurun.");
            return;
        }

        Vector2 worldMousePos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
        var (gx, gy) = canvas.ScreenToGrid(worldMousePos);

        if (!Raylib.IsMouseButtonDown(MouseButton.Left) && !Raylib.IsMouseButtonDown(MouseButton.Right))
            lastGridPos = null;

        if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            isDrawingWithLeft = false;
        if (Raylib.IsMouseButtonReleased(MouseButton.Right))
            isDrawingWithRight = false;

        // 3 Noktalı Bezier Eğri (Curve) Aracı
        if (toolManager.ActiveTool == ToolType.Curve)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Right) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                ResetCurveState();
                ShowStatus("Egri cizimi iptal edildi.");
                return;
            }

            if (isCurveAwaitingControl && curveStartPos.HasValue && curveEndPos.HasValue)
            {
                if ((insideDrawingArea && Raylib.IsMouseButtonPressed(MouseButton.Left)) ||
                    Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter))
                {
                    history.SaveState(canvas);
                    int ctrlX = canvas.TileWrapMode ? gx : Math.Clamp(gx, -canvas.Width, canvas.Width * 2);
                    int ctrlY = canvas.TileWrapMode ? gy : Math.Clamp(gy, -canvas.Height, canvas.Height * 2);

                    var curvePixels = ToolManager.GetBezierCurvePixels(
                        curveStartPos.Value.x, curveStartPos.Value.y,
                        ctrlX, ctrlY,
                        curveEndPos.Value.x, curveEndPos.Value.y,
                        cleanCorners: toolManager.PenSize == 1
                    );

                    foreach (var (px, py) in curvePixels)
                        toolManager.PlotStrokePoint(canvas, px, py, palette.CurrentColor, true);

                    ActiveTab!.IsDirty = true;
                    ResetCurveState();
                    ShowStatus("Egri tamamlandi.");
                }
                return;
            }

            if (insideDrawingArea && canvas.IsMouseInside(worldMousePos) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                curveStartPos = (gx, gy);
                return;
            }

            if (curveStartPos.HasValue && Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                int endX = canvas.TileWrapMode ? gx : Math.Clamp(gx, 0, canvas.Width - 1);
                int endY = canvas.TileWrapMode ? gy : Math.Clamp(gy, 0, canvas.Height - 1);

                if (endX == curveStartPos.Value.x && endY == curveStartPos.Value.y)
                {
                    history.SaveState(canvas);
                    toolManager.PlotStrokePoint(canvas, endX, endY, palette.CurrentColor, true);
                    ActiveTab!.IsDirty = true;
                    ResetCurveState();
                }
                else
                {
                    curveEndPos = (endX, endY);
                    isCurveAwaitingControl = true;
                    ShowStatus("Egri: Kavis vermek icin fareyi hareket ettirin ve Sol Tik ile onaylayin (Sag Tik: Iptal)");
                }
            }
            return;
        }
        else if (curveStartPos.HasValue || isCurveAwaitingControl)
        {
            ResetCurveState();
        }

        if (toolManager.ActiveTool == ToolType.Line || toolManager.ActiveTool == ToolType.Shape)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Right) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                dragStartPos = null;
                return;
            }

            if (insideDrawingArea && canvas.IsMouseInside(worldMousePos) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                dragStartPos = (gx, gy);
                return;
            }

            if (dragStartPos.HasValue && Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                history.SaveState(canvas);
                int endX = canvas.TileWrapMode ? gx : Math.Clamp(gx, 0, canvas.Width - 1);
                int endY = canvas.TileWrapMode ? gy : Math.Clamp(gy, 0, canvas.Height - 1);
                List<(int x, int y)> shapePixels = (toolManager.ActiveTool == ToolType.Line)
                    ? ToolManager.GetLinePixels(dragStartPos.Value.x, dragStartPos.Value.y, endX, endY)
                    : ToolManager.GetShapePixels(dragStartPos.Value.x, dragStartPos.Value.y, endX, endY, toolManager.ActiveShape);

                bool useBrushSize = toolManager.ActiveTool == ToolType.Line ||
                                    toolManager.ActiveShape == ShapeType.RectangleOutline ||
                                    toolManager.ActiveShape == ShapeType.CircleOutline ||
                                    toolManager.ActiveShape == ShapeType.TriangleOutline;

                foreach (var (px, py) in shapePixels)
                    toolManager.PlotStrokePoint(canvas, px, py, palette.CurrentColor, useBrushSize);

                ActiveTab!.IsDirty = true;
                dragStartPos = null;
            }
            return;
        }
        else
        {
            dragStartPos = null;
        }

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
                var (wx, wy) = canvas.TileWrapMode ? canvas.WrapCoord(gx, gy) : (gx, gy);
                Color pickedColor = canvas.GetMergedPixel(wx, wy);
                if (pickedColor.A > 0)
                {
                    palette.SetColor(pickedColor);
                    ShowStatus($"Renk secildi: #{pickedColor.R:X2}{pickedColor.G:X2}{pickedColor.B:X2}");
                }
                if (toolManager.ActiveTool == ToolType.Eyedropper && AppSettings.EyedropperAutoSwitchPen)
                    SelectTool(ToolType.Pen);
            }
        }
        else if (toolManager.ActiveTool == ToolType.Bucket || toolManager.ActiveTool == ToolType.SmartBucket)
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                var (wx, wy) = canvas.TileWrapMode ? canvas.WrapCoord(gx, gy) : (gx, gy);
                history.SaveState(canvas);
                toolManager.ApplyTool(canvas, wx, wy, palette.CurrentColor);
                ActiveTab!.IsDirty = true;
            }
        }
        else
        {
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                isDrawingWithLeft = true;
                history.SaveState(canvas);
                toolManager.BeginStroke();
            }
            else if (Raylib.IsMouseButtonPressed(MouseButton.Right))
            {
                isDrawingWithRight = true;
                history.SaveState(canvas);
                toolManager.BeginStroke();
            }

            IReadOnlyList<Color>? paletteColors = toolManager.ActiveTool == ToolType.Shading ? palette.GetColors() : null;

            if (isDrawingWithLeft && Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                if (lastGridPos.HasValue)
                {
                    if (!((toolManager.ActiveTool == ToolType.Blur || toolManager.ActiveTool == ToolType.Jumble) && lastGridPos.Value == (gx, gy)))
                        toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, palette.CurrentColor, forceEraser: false, darkenShading: false, paletteColors: paletteColors);
                }
                else toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor, forceEraser: false, darkenShading: false, paletteColors: paletteColors);
                lastGridPos = (gx, gy);
                ActiveTab!.IsDirty = true;
            }
            else if (isDrawingWithRight && Raylib.IsMouseButtonDown(MouseButton.Right))
            {
                if (toolManager.ActiveTool == ToolType.Shading)
                {
                    if (lastGridPos.HasValue)
                        toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, palette.CurrentColor, forceEraser: false, darkenShading: true, paletteColors: paletteColors);
                    else
                        toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor, forceEraser: false, darkenShading: true, paletteColors: paletteColors);
                }
                else
                {
                    if (lastGridPos.HasValue) toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, Canvas.TransparentColor, forceEraser: true);
                    else toolManager.ApplyTool(canvas, gx, gy, Canvas.TransparentColor, forceEraser: true);
                }
                lastGridPos = (gx, gy);
                ActiveTab!.IsDirty = true;
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
        topMenu.Draw(sw, statusMessage);

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
        refManager.DrawOnCanvas(canvas);
        DrawSymmetryAxes(canvas, camera);

        Vector2 worldMousePos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
        var (gx, gy) = canvas.ScreenToGrid(worldMousePos);

        int curveCtrlX = 0, curveCtrlY = 0;
        bool drawCurveGuidesScreenSpace = false;

        // Eğri (Bezier Curve) Canlı Önizlemesi
        if (toolManager.ActiveTool == ToolType.Curve && curveStartPos.HasValue)
        {
            List<(int x, int y)> curvePreviewPixels;
            if (isCurveAwaitingControl && curveEndPos.HasValue)
            {
                curveCtrlX = canvas.TileWrapMode ? gx : Math.Clamp(gx, -canvas.Width, canvas.Width * 2);
                curveCtrlY = canvas.TileWrapMode ? gy : Math.Clamp(gy, -canvas.Height, canvas.Height * 2);
                curvePreviewPixels = ToolManager.GetBezierCurvePixels(
                    curveStartPos.Value.x, curveStartPos.Value.y,
                    curveCtrlX, curveCtrlY,
                    curveEndPos.Value.x, curveEndPos.Value.y,
                    cleanCorners: toolManager.PenSize == 1
                );
                drawCurveGuidesScreenSpace = true;
            }
            else
            {
                int endX = canvas.TileWrapMode ? gx : Math.Clamp(gx, 0, canvas.Width - 1);
                int endY = canvas.TileWrapMode ? gy : Math.Clamp(gy, 0, canvas.Height - 1);
                curvePreviewPixels = ToolManager.GetLinePixels(curveStartPos.Value.x, curveStartPos.Value.y, endX, endY);
            }

            DrawShapeOrCurvePreviewPixels(canvas, curvePreviewPixels, Math.Max(1, toolManager.PenSize));
        }

        if (dragStartPos.HasValue && (toolManager.ActiveTool == ToolType.Line || toolManager.ActiveTool == ToolType.Shape))
        {
            int endX = canvas.TileWrapMode ? gx : Math.Clamp(gx, 0, canvas.Width - 1);
            int endY = canvas.TileWrapMode ? gy : Math.Clamp(gy, 0, canvas.Height - 1);
            List<(int x, int y)> previewPixels = (toolManager.ActiveTool == ToolType.Line)
                ? ToolManager.GetLinePixels(dragStartPos.Value.x, dragStartPos.Value.y, endX, endY)
                : ToolManager.GetShapePixels(dragStartPos.Value.x, dragStartPos.Value.y, endX, endY, toolManager.ActiveShape);

            bool useBrushSize = toolManager.ActiveTool == ToolType.Line ||
                                toolManager.ActiveShape == ShapeType.RectangleOutline ||
                                toolManager.ActiveShape == ShapeType.CircleOutline ||
                                toolManager.ActiveShape == ShapeType.TriangleOutline;
            int stampSize = useBrushSize ? Math.Max(1, toolManager.PenSize) : 1;

            DrawShapeOrCurvePreviewPixels(canvas, previewPixels, stampSize);
        }

        DrawBrushPreview(canvas, camera, worldMousePos, gx, gy);

        selection.DrawWorldOverlay(canvas);
        Raylib.EndMode2D();

        // Bezier kılavuz kollarını ve kontrol noktasını ekran uzayında (Screen-Space) sabit kalınlıkta çiz
        if (drawCurveGuidesScreenSpace && curveStartPos.HasValue && curveEndPos.HasValue)
        {
            Vector2 p0World = new Vector2(canvas.Offset.X + (curveStartPos.Value.x + 0.5f) * canvas.PixelScale, canvas.Offset.Y + (curveStartPos.Value.y + 0.5f) * canvas.PixelScale);
            Vector2 p1World = new Vector2(canvas.Offset.X + (curveCtrlX + 0.5f) * canvas.PixelScale, canvas.Offset.Y + (curveCtrlY + 0.5f) * canvas.PixelScale);
            Vector2 p2World = new Vector2(canvas.Offset.X + (curveEndPos.Value.x + 0.5f) * canvas.PixelScale, canvas.Offset.Y + (curveEndPos.Value.y + 0.5f) * canvas.PixelScale);

            Vector2 p0Screen = Raylib.GetWorldToScreen2D(p0World, camera);
            Vector2 p1Screen = Raylib.GetWorldToScreen2D(p1World, camera);
            Vector2 p2Screen = Raylib.GetWorldToScreen2D(p2World, camera);

            Color guideCol = new Color((byte)255, (byte)205, (byte)75, (byte)185);
            Raylib.DrawLineEx(p0Screen, p1Screen, 1.5f, guideCol);
            Raylib.DrawLineEx(p2Screen, p1Screen, 1.5f, guideCol);
            Raylib.DrawCircleV(p1Screen, 5.5f, Color.Black);
            Raylib.DrawCircleV(p1Screen, 4f, PixelSkin.AccentGold);
        }

        selection.CheckAndDrawFloatingMenu(canvas, camera, mouseScreenPos, history, ShowStatus);

        layerPanel.Draw(canvas, sw, sh);
        animationPanel.Draw(canvas, sw, sh);
        DrawRightSidebar(canvas, sw, sh, mouseScreenPos);

        // Sol araç çubuğunu en son çiz ki üzerine gelindiğinde açılan Aseprite ipucu balonları üstte kalsın
        toolPanel.Draw(
            toolManager.ActiveTool,
            toolManager.ActiveShape,
            toolManager.ActiveSelectionMode,
            toolManager.PixelPerfect,
            toolManager.Symmetry,
            sh,
            toolManager.ActiveDither,
            toolManager.ActiveShadingMode,
            canvas.ActiveLayerAlphaLock,
            canvas.TileWrapMode
        );
    }

    static void DrawShapeOrCurvePreviewPixels(Canvas canvas, List<(int x, int y)> previewPixels, int stampSize)
    {
        int stampOffset = stampSize / 2;
        Color previewColor = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)185);
        Color neighborPreviewColor = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)115);
        int totalW = canvas.Width * canvas.PixelScale;
        int totalH = canvas.Height * canvas.PixelScale;

        HashSet<(int, int)> drawnPreview = new();
        foreach (var (px, py) in previewPixels)
        {
            for (int dx = 0; dx < stampSize; dx++)
            {
                for (int dy = 0; dy < stampSize; dy++)
                {
                    foreach (var (spx, spy) in toolManager.GetSymmetricPoints(canvas, px - stampOffset + dx, py - stampOffset + dy))
                    {
                        if (spx >= 0 && spx < canvas.Width && spy >= 0 && spy < canvas.Height && drawnPreview.Add((spx, spy)))
                        {
                            int rx = (int)canvas.Offset.X + spx * canvas.PixelScale;
                            int ry = (int)canvas.Offset.Y + spy * canvas.PixelScale;
                            Raylib.DrawRectangle(rx, ry, canvas.PixelScale, canvas.PixelScale, previewColor);

                            if (canvas.TileWrapMode)
                            {
                                for (int ty = -1; ty <= 1; ty++)
                                {
                                    for (int tx = -1; tx <= 1; tx++)
                                    {
                                        if (tx == 0 && ty == 0) continue;
                                        Raylib.DrawRectangle(rx + tx * totalW, ry + ty * totalH, canvas.PixelScale, canvas.PixelScale, neighborPreviewColor);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    static void DrawSymmetryAxes(Canvas canvas, Camera2D camera)
    {
        if (toolManager.Symmetry == SymmetryMode.None) return;

        int totalW = canvas.Width * canvas.PixelScale;
        int totalH = canvas.Height * canvas.PixelScale;
        float thickness = Math.Max(1f, 1.5f / camera.Zoom);
        Color axisColor = new Color((byte)0, (byte)210, (byte)255, (byte)190);

        if (toolManager.Symmetry == SymmetryMode.Horizontal || toolManager.Symmetry == SymmetryMode.Both)
        {
            float midX = canvas.Offset.X + totalW / 2f;
            Raylib.DrawLineEx(new Vector2(midX, canvas.Offset.Y - 4), new Vector2(midX, canvas.Offset.Y + totalH + 4), thickness, axisColor);
        }

        if (toolManager.Symmetry == SymmetryMode.Vertical || toolManager.Symmetry == SymmetryMode.Both)
        {
            float midY = canvas.Offset.Y + totalH / 2f;
            Raylib.DrawLineEx(new Vector2(canvas.Offset.X - 4, midY), new Vector2(canvas.Offset.X + totalW + 4, midY), thickness, axisColor);
        }
    }

    static void DrawBrushPreview(Canvas canvas, Camera2D camera, Vector2 worldMousePos, int gx, int gy)
    {
        bool isBrushTool = toolManager.ActiveTool == ToolType.Pen ||
                           toolManager.ActiveTool == ToolType.Eraser ||
                           toolManager.ActiveTool == ToolType.Dither ||
                           toolManager.ActiveTool == ToolType.Shading ||
                           toolManager.ActiveTool == ToolType.Blur ||
                           toolManager.ActiveTool == ToolType.Jumble;

        if (!AppSettings.ShowBrushPreview || !canvas.IsMouseInside(worldMousePos) || !isBrushTool) return;

        int size = toolManager.ActiveTool == ToolType.Eraser ? toolManager.EraserSize : toolManager.PenSize;
        if (toolManager.ActiveTool == ToolType.Jumble && size < 2) size = 2;

        int offset = size / 2;
        float pulse = (float)(Math.Sin(Raylib.GetTime() * 8.0) * 0.5 + 0.5);
        byte alphaPulse = (byte)(150 + pulse * 105);
        float lineThickness = Math.Max(1f, 1.5f / camera.Zoom);
        int totalW = canvas.Width * canvas.PixelScale;
        int totalH = canvas.Height * canvas.PixelScale;

        Color outlineCol = toolManager.ActiveTool switch
        {
            ToolType.Eraser => new Color((byte)255, (byte)100, (byte)100, alphaPulse),
            ToolType.Dither => new Color((byte)130, (byte)245, (byte)190, alphaPulse),
            ToolType.Shading => new Color((byte)255, (byte)215, (byte)85, alphaPulse),
            ToolType.Blur => new Color((byte)100, (byte)200, (byte)255, alphaPulse),
            ToolType.Jumble => new Color((byte)255, (byte)200, (byte)80, alphaPulse),
            _ => new Color((byte)255, (byte)255, (byte)255, alphaPulse)
        };

        foreach (var (sx, sy) in toolManager.GetSymmetricPoints(canvas, gx, gy))
        {
            Rectangle brushPreviewRect = new Rectangle(
                canvas.Offset.X + (sx - offset) * canvas.PixelScale,
                canvas.Offset.Y + (sy - offset) * canvas.PixelScale,
                size * canvas.PixelScale,
                size * canvas.PixelScale
            );

            if (toolManager.ActiveTool == ToolType.Pen)
            {
                Color penGhost = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)95);
                if (canvas.TileWrapMode)
                {
                    for (int dx = 0; dx < size; dx++)
                    {
                        for (int dy = 0; dy < size; dy++)
                        {
                            var (wx, wy) = canvas.WrapCoord(sx - offset + dx, sy - offset + dy);
                            int rx = (int)canvas.Offset.X + wx * canvas.PixelScale;
                            int ry = (int)canvas.Offset.Y + wy * canvas.PixelScale;
                            for (int ty = -1; ty <= 1; ty++)
                                for (int tx = -1; tx <= 1; tx++)
                                    Raylib.DrawRectangle(rx + tx * totalW, ry + ty * totalH, canvas.PixelScale, canvas.PixelScale, penGhost);
                        }
                    }
                }
                else
                {
                    Raylib.DrawRectangleRec(brushPreviewRect, penGhost);
                }
            }
            else if (toolManager.ActiveTool == ToolType.Dither)
            {
                Color ditherPreviewCol = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)135);
                for (int dx = 0; dx < size; dx++)
                {
                    for (int dy = 0; dy < size; dy++)
                    {
                        int px = sx - offset + dx;
                        int py = sy - offset + dy;
                        if (canvas.TileWrapMode) (px, py) = canvas.WrapCoord(px, py);
                        if (px >= 0 && px < canvas.Width && py >= 0 && py < canvas.Height &&
                            ToolManager.ShouldPaintDither(px, py, toolManager.ActiveDither))
                        {
                            int rx = (int)canvas.Offset.X + px * canvas.PixelScale;
                            int ry = (int)canvas.Offset.Y + py * canvas.PixelScale;
                            Raylib.DrawRectangle(rx, ry, canvas.PixelScale, canvas.PixelScale, ditherPreviewCol);
                            if (canvas.TileWrapMode)
                            {
                                for (int ty = -1; ty <= 1; ty++)
                                    for (int tx = -1; tx <= 1; tx++)
                                        if (tx != 0 || ty != 0)
                                            Raylib.DrawRectangle(rx + tx * totalW, ry + ty * totalH, canvas.PixelScale, canvas.PixelScale, ditherPreviewCol);
                            }
                        }
                    }
                }
            }
            else if (toolManager.ActiveTool == ToolType.Shading)
            {
                Raylib.DrawRectangleRec(brushPreviewRect, new Color((byte)255, (byte)225, (byte)110, (byte)45));
            }

            Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness * 1.6f, new Color((byte)0, (byte)0, (byte)0, (byte)200));
            Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness, outlineCol);

            // Tile-Wrap açıkken imleç çevre karolardaysa gerçek imleç konumunda da çerçeveyi göster
            if (canvas.TileWrapMode && (sx != gx || sy != gy))
            {
                Rectangle cursorTileRect = new Rectangle(
                    canvas.Offset.X + (gx - offset) * canvas.PixelScale,
                    canvas.Offset.Y + (gy - offset) * canvas.PixelScale,
                    size * canvas.PixelScale,
                    size * canvas.PixelScale
                );
                Raylib.DrawRectangleLinesEx(cursorTileRect, lineThickness * 1.6f, new Color((byte)0, (byte)0, (byte)0, (byte)200));
                Raylib.DrawRectangleLinesEx(cursorTileRect, lineThickness, outlineCol);
            }
        }
    }

    static void DrawRightSidebar(Canvas canvas, int sw, int sh, Vector2 mouseScreenPos)
    {
        int rightPanelX = sw - RightPanelWidth;
        float infoBoxY = GetRightSidebarInfoBoxY(sh);
        int iy = (int)infoBoxY;

        PixelSkin.DrawPanel(new Rectangle(rightPanelX, WorkspaceTopY, RightPanelWidth, sh - WorkspaceTopY));
        palette.Draw();

        PixelSkin.DrawInsetBox(new Rectangle(rightPanelX + 10, infoBoxY, RightPanelWidth - 20, 145));

        Raylib.DrawText($"Tuval: {canvas.Width}x{canvas.Height} px", rightPanelX + 18, iy + 10, 12, PixelSkin.TextPrimary);
        string toolDesc = toolManager.ActiveTool switch
        {
            ToolType.Pen => "Kalem",
            ToolType.Eraser => "Silgi",
            ToolType.Dither => $"Dither ({ToolManager.DitherLabel(toolManager.ActiveDither)})",
            ToolType.Shading => $"Tonlama ({ToolManager.ShadingModeLabel(toolManager.ActiveShadingMode)})",
            ToolType.Bucket => "Kova",
            ToolType.SmartBucket => "Akilli Kova",
            ToolType.Eyedropper => "Damlalik",
            ToolType.Line => "Cizgi",
            ToolType.Curve => "Egri (Bezier)",
            ToolType.Shape => $"Sekil ({toolManager.ActiveShape})",
            ToolType.SelectMove => $"Sec ({toolManager.ActiveSelectionMode})",
            ToolType.Blur => "Bulanik (Blur)",
            ToolType.Jumble => "Karistir (Jumble)",
            _ => toolManager.ActiveTool.ToString()
        };
        Raylib.DrawText($"Arac: {toolDesc}", rightPanelX + 18, iy + 32, 12, PixelSkin.AccentGold);
        Raylib.DrawText($"Zoom: {ZoomLevels[ActiveTab!.ZoomIndex]}x", rightPanelX + 18, iy + 54, 12, PixelSkin.TextMuted);
        Raylib.DrawText("Firca:", rightPanelX + 16, iy + 91, 11, PixelSkin.TextPrimary);

        float btnY = infoBoxY + 86f;
        Rectangle btnMinus = new Rectangle(rightPanelX + 52, btnY, 20, 22);
        Rectangle sizeBox = new Rectangle(rightPanelX + 75, btnY, 45, 22);
        Rectangle btnPlus = new Rectangle(rightPanelX + 123, btnY, 20, 22);

        PixelSkin.DrawButton(btnMinus, Raylib.CheckCollisionPointRec(mouseScreenPos, btnMinus), false);
        Raylib.DrawText("-", (int)btnMinus.X + 7, (int)btnMinus.Y + 4, 14, Color.White);

        int currentToolSize = toolManager.ActiveTool == ToolType.Eraser ? toolManager.EraserSize : toolManager.PenSize;

        PixelSkin.DrawInsetBox(sizeBox);
        if (sizeInputActive)
        {
            Raylib.DrawRectangleLinesEx(sizeBox, 1, PixelSkin.AccentGold);
            string blink = ((int)(Raylib.GetTime() * 3) % 2 == 0) ? "|" : "";
            Raylib.DrawText(sizeInputText + blink, (int)sizeBox.X + 8, (int)sizeBox.Y + 5, 12, PixelSkin.AccentGold);
        }
        else
        {
            Raylib.DrawText($"{currentToolSize}px", (int)sizeBox.X + 8, (int)sizeBox.Y + 5, 12, Color.RayWhite);
        }

        PixelSkin.DrawButton(btnPlus, Raylib.CheckCollisionPointRec(mouseScreenPos, btnPlus), false);
        Raylib.DrawText("+", (int)btnPlus.X + 6, (int)btnPlus.Y + 4, 14, Color.White);

        // Hızlı fırça boyutu butonları (1px, 2px, 4px, 8px)
        float presetStartX = rightPanelX + 18f;
        float presetY = infoBoxY + 114f;
        for (int i = 0; i < BrushQuickSizes.Length; i++)
        {
            int pSize = BrushQuickSizes[i];
            Rectangle pRect = new Rectangle(presetStartX + i * 36f, presetY, 32f, 20f);
            bool pHover = Raylib.CheckCollisionPointRec(mouseScreenPos, pRect);
            bool pSelected = currentToolSize == pSize;
            PixelSkin.DrawButton(pRect, pHover, pSelected);
            string lbl = $"{pSize}p";
            int lw = Raylib.MeasureText(lbl, 10);
            Raylib.DrawText(lbl, (int)(pRect.X + (pRect.Width - lw) / 2f), (int)pRect.Y + 5, 10, pSelected ? Color.White : PixelSkin.TextPrimary);
        }

        Rectangle refBounds = GetRightSidebarRefBounds(sw, sh);
        refManager.DrawUI(refBounds, mouseScreenPos);

        if (isHoveringLeftEdge || isDraggingLeftPanel)
            Raylib.DrawRectangle(LeftPanelWidth - 2, (int)WorkspaceTopY, 4, sh - (int)WorkspaceTopY, isDraggingLeftPanel ? PixelSkin.AccentGold : PixelSkin.AccentCyan);

        if (isHoveringRightEdge || isDraggingRightPanel)
            Raylib.DrawRectangle(sw - RightPanelWidth - 2, (int)WorkspaceTopY, 4, sh - (int)WorkspaceTopY, isDraggingRightPanel ? PixelSkin.AccentGold : PixelSkin.AccentCyan);
    }
}
