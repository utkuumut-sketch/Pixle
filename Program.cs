using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;
using PixelArtApp.UI;
using PixelArtApp.IO;

namespace PixelArtApp;

class Program
{
    static List<ProjectTab> tabs = new();
    static int activeTabIndex = -1;
    static int untitledCounter = 1;
    static bool shouldExit = false;

    static Palette palette = null!;
    static ToolManager toolManager = null!;
    static TopMenu topMenu = null!;
    static TabBar tabBar = null!;
    static HomeScreen homeScreen = null!;
    static NewProjectDialog newProjectDialog = null!;
    static SettingsDialog settingsDialog = null!;
    static ExportDialog exportDialog = null!;
    static ToolPanel toolPanel = null!;
    static ColorPickerDialog colorPicker = null!;
    static LayerPanel layerPanel = null!;
    static AnimationPanel animationPanel = null!;

    const int RightPanelWidth = 180;
    const float WorkspaceTopY = 72f;

    static readonly float[] ZoomLevels = new float[] 
    { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f, 2.5f, 3.0f, 4.0f, 5.0f, 6.0f, 8.0f };

    static string statusMessage = "Hazir";
    static float messageTimer = 0f;

    static (int x, int y)? lastGridPos = null;
    static (int x, int y)? dragStartPos = null;
    static bool isGridVisible = false;

    static bool sizeInputActive = false;
    static string sizeInputText = "1";
    static float targetZoom = 1.0f;

    static ProjectTab? ActiveTab => (activeTabIndex >= 0 && activeTabIndex < tabs.Count) ? tabs[activeTabIndex] : null;

    [STAThread]
    static void Main()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1080, 740, "Pixel Art Studio");
        Raylib.SetTargetFPS(60);
        Raylib.SetExitKey(KeyboardKey.Null);
        PixelSkin.LoadSkin();

        toolManager = new ToolManager();
        topMenu = new TopMenu();
        tabBar = new TabBar();
        homeScreen = new HomeScreen();
        newProjectDialog = new NewProjectDialog();
        settingsDialog = new SettingsDialog();
        exportDialog = new ExportDialog();
        toolPanel = new ToolPanel(WorkspaceTopY);
        layerPanel = new LayerPanel();
        animationPanel = new AnimationPanel();
        palette = new Palette(new Vector2(1080 - RightPanelWidth + 20, WorkspaceTopY + 35));
        colorPicker = new ColorPickerDialog();

        topMenu.AddCategory("Dosya", items =>
        {
            items.Add(new MenuItem("Yeni Proje (Ctrl+N)", () => newProjectDialog.Open()));
            items.Add(new MenuItem("Ac/Ice Aktar (Ctrl+O)", OpenProjectWithDialog));
            items.Add(new MenuItem("Disa Aktar (Ctrl+S)", () => { 
                if (ActiveTab != null) exportDialog.Open(ActiveTab.Title, ActiveTab.Canvas); 
            }));
            items.Add(new MenuItem("Sekmeyi Kapat (Ctrl+W)", () => { if (activeTabIndex >= 0) CloseTab(activeTabIndex); }));
            items.Add(new MenuItem("Cikis", () => shouldExit = true));
        });

        topMenu.AddCategory("Duzenle", items =>
        {
            items.Add(new MenuItem("Geri Al (Ctrl+Z)", () => { if (ActiveTab != null) { ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas); ActiveTab.History.Undo(ActiveTab.Canvas); } }));
            items.Add(new MenuItem("Ileri Al (Ctrl+Y)", () => { if (ActiveTab != null) { ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas); ActiveTab.History.Redo(ActiveTab.Canvas); } }));
            items.Add(new MenuItem("Kopyala (Ctrl+C)", () => ActiveTab?.Selection.Copy(ActiveTab.Canvas)));
            items.Add(new MenuItem("Kes (Ctrl+X)", () => { if (ActiveTab != null) ActiveTab.Selection.Cut(ActiveTab.Canvas, ActiveTab.History); }));
            items.Add(new MenuItem("Yapistir (Ctrl+V)", () => { if (ActiveTab != null) { toolManager.ActiveTool = ToolType.SelectMove; ActiveTab.Selection.Paste(ActiveTab.Canvas, ActiveTab.History); } }));
            items.Add(new MenuItem("Cogalt (Ctrl+D)", () => { if (ActiveTab != null) ActiveTab.Selection.Duplicate(ActiveTab.Canvas, ActiveTab.History); }));
            items.Add(new MenuItem("Temizle", ClearCanvas));
            items.Add(new MenuItem("Ayarlar...", () => settingsDialog.Open()));
        });

        topMenu.AddCategory("Gorunum", items =>
        {
            items.Add(new MenuItem("Izgara Ac/Kapat", ToggleGrid));
            items.Add(new MenuItem("Kamerayi Sifirla", () => { if (ActiveTab != null) CenterCamera(ActiveTab); }));
            items.Add(new MenuItem("Ana Ekran (Home)", () => activeTabIndex = -1));
        });

        topMenu.Layout();

        while (!Raylib.WindowShouldClose() && !shouldExit)
        {
            Update();
            Draw();
        }

        colorPicker.Unload();
        PixelSkin.UnloadSkin();
        Raylib.CloseWindow();
    }

    static void CreateNewTab(int width, int height, Color bgColor, string? customTitle = null)
    {
        string title = customTitle ?? $"Sprite-{untitledCounter++}";
        ProjectTab newTab = new ProjectTab(title, width, height, bgColor, isGridVisible);
        tabs.Add(newTab);
        activeTabIndex = tabs.Count - 1;
        CenterCamera(newTab);
        ShowStatus($"Yeni proje olusturuldu: {width}x{height}");
    }

    static void CloseTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        tabs[index].Selection.CancelOrCommit(tabs[index].Canvas);
        tabs.RemoveAt(index);
        if (tabs.Count == 0) activeTabIndex = -1;
        else if (activeTabIndex >= tabs.Count) activeTabIndex = tabs.Count - 1;
    }

    static void OpenProjectWithDialog()
    {
        string? path = Exporter.OpenImageDialog();
        if (!string.IsNullOrEmpty(path)) LoadProjectFromFile(path);
    }

    static void LoadProjectFromFile(string path)
    {
        if (!File.Exists(path)) return;
        Image img = Raylib.LoadImage(path);
        if (img.Width <= 0 || img.Height <= 0) return;

        int w = Math.Min(img.Width, 256);
        int h = Math.Min(img.Height, 256);
        string fileName = Path.GetFileNameWithoutExtension(path);
        ProjectTab newTab = new ProjectTab(fileName, w, h, Canvas.TransparentColor, isGridVisible) { FilePath = path };

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                Color c = Raylib.GetImageColor(img, x, y);
                newTab.Canvas.SetPixel(x, y, c);
            }
        }
        Raylib.UnloadImage(img);

        tabs.Add(newTab);
        activeTabIndex = tabs.Count - 1;
        CenterCamera(newTab);
        homeScreen.AddRecentFile(path);
        ShowStatus($"Acildi: {Path.GetFileName(path)}");
    }

    static void SelectTool(ToolType newTool)
    {
        if (ActiveTab != null && toolManager.ActiveTool == ToolType.SelectMove && newTool != ToolType.SelectMove)
            ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
        toolManager.ActiveTool = newTool;
        sizeInputActive = false;
    }

    static void ToggleGrid()
    {
        isGridVisible = !isGridVisible;
        foreach (var t in tabs) t.Canvas.ShowGrid = isGridVisible;
    }

    static void ClearCanvas()
    {
        if (ActiveTab == null) return;
        ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
        ActiveTab.History.SaveState(ActiveTab.Canvas);
        ActiveTab.Canvas.Clear(Canvas.TransparentColor);
        ShowStatus("Tuval temizlendi.");
    }

    static void ShowStatus(string msg) { statusMessage = msg; messageTimer = 3.0f; }

    static void CenterCamera(ProjectTab tab)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float centerAreaX = (toolPanel.Width + (sw - RightPanelWidth)) / 2f;
        float centerAreaY = (WorkspaceTopY + animationPanel.Height + (sh - layerPanel.Height)) / 2f;
        Camera2D cam = new Camera2D();
        cam.Target = new Vector2((tab.Canvas.Width * tab.Canvas.PixelScale) / 2f, (tab.Canvas.Height * tab.Canvas.PixelScale) / 2f);
        cam.Offset = new Vector2(centerAreaX, centerAreaY);
        cam.Rotation = 0.0f;
        cam.Zoom = ZoomLevels[tab.ZoomIndex];
        targetZoom = cam.Zoom;
        tab.Camera = cam;
    }

    static void ApplyTypedSize()
    {
        if (int.TryParse(sizeInputText, out int val))
        {
            val = Math.Clamp(val, 1, 50);
            if (toolManager.ActiveTool == ToolType.Eraser) toolManager.EraserSize = val;
            else toolManager.PenSize = val;
            ShowStatus($"Boyut ayarlandi: {val}px");
        }
        sizeInputActive = false;
    }

    static void Update()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mouseScreenPos = Raylib.GetMousePosition();

        if (Raylib.IsKeyPressed(KeyboardKey.F11)) Raylib.ToggleFullscreen();

        palette.Offset = new Vector2(sw - RightPanelWidth + 20, WorkspaceTopY + 35);

        if (newProjectDialog.IsOpen)
        {
            newProjectDialog.Update(mouseScreenPos, (w, h, bg) => CreateNewTab(w, h, bg));
            return;
        }

        if (exportDialog.IsOpen)
        {
            exportDialog.Update(mouseScreenPos, (path, scale, allFrames, vertical) =>
            {
                var tab = ActiveTab;
                if (tab != null)
                {
                    bool success = Exporter.SaveExport(tab.Canvas, path, scale, allFrames, vertical);
                    if (success)
                    {
                        tab.FilePath = path;
                        tab.Title = Path.GetFileNameWithoutExtension(path);
                        homeScreen.AddRecentFile(path);
                        ShowStatus($"Basariyla aktarildi: {Path.GetFileName(path)}");
                    }
                    else ShowStatus("Disa aktarma basarisiz oldu veya iptal edildi.");
                }
            });
            return;
        }

        if (settingsDialog.IsOpen)
        {
            settingsDialog.Update(mouseScreenPos, ToggleGrid);
            return;
        }

        if (colorPicker.IsOpen)
        {
            colorPicker.Update(mouseScreenPos, (newColor, addToPalette) =>
            {
                if (addToPalette) { palette.AddColor(newColor); ShowStatus("Ozel renk palete eklendi!"); }
                else { palette.SetColor(newColor); ShowStatus("Yeni renk secildi."); }
            });
            return;
        }

        bool isCtrlDown = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);

        if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.N)) { newProjectDialog.Open(); return; }
        if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.O)) { OpenProjectWithDialog(); return; }
        if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.W) && activeTabIndex >= 0) { CloseTab(activeTabIndex); return; }

        bool topMenuIntercepted = topMenu.Update(mouseScreenPos);

        bool tabBarIntercepted = false;
        if (!topMenuIntercepted)
        {
            tabBarIntercepted = tabBar.Update(
                mouseScreenPos, tabs, activeTabIndex,
                newIndex => { ActiveTab?.Selection.CancelOrCommit(ActiveTab.Canvas); activeTabIndex = newIndex; if (ActiveTab != null) targetZoom = ZoomLevels[ActiveTab.ZoomIndex]; },
                CloseTab, () => newProjectDialog.Open()
            );
        }

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

        targetZoom = ZoomLevels[ActiveTab.ZoomIndex];
        if (Math.Abs(camera.Zoom - targetZoom) > 0.005f)
        {
            camera.Zoom = Raymath.Lerp(camera.Zoom, targetZoom, Raylib.GetFrameTime() * 14f);
            ActiveTab.Camera = camera;
        }
        else if (camera.Zoom != targetZoom)
        {
            camera.Zoom = targetZoom;
            ActiveTab.Camera = camera;
        }

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
                return;
            }
            else if (Raylib.CheckCollisionPointRec(mouseScreenPos, btnMinusRect))
            {
                if (toolManager.ActiveTool == ToolType.Eraser)
                    toolManager.EraserSize = Math.Clamp(toolManager.EraserSize - 1, 1, 50);
                else
                    toolManager.PenSize = Math.Clamp(toolManager.PenSize - 1, 1, 50);
                sizeInputActive = false;
                return;
            }
            else if (Raylib.CheckCollisionPointRec(mouseScreenPos, btnPlusRect))
            {
                if (toolManager.ActiveTool == ToolType.Eraser)
                    toolManager.EraserSize = Math.Clamp(toolManager.EraserSize + 1, 1, 50);
                else
                    toolManager.PenSize = Math.Clamp(toolManager.PenSize + 1, 1, 50);
                sizeInputActive = false;
                return;
            }
            else if (sizeInputActive)
            {
                ApplyTypedSize();
            }
        }

        if (sizeInputActive)
        {
            int key = Raylib.GetCharPressed();
            while (key > 0)
            {
                if (key >= '0' && key <= '9' && sizeInputText.Length < 2)
                {
                    sizeInputText += (char)key;
                }
                key = Raylib.GetCharPressed();
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && sizeInputText.Length > 0)
            {
                sizeInputText = sizeInputText[..^1];
            }
            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.KpEnter) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                ApplyTypedSize();
            }
            return;
        }

        if (Raylib.IsKeyPressed(AppSettings.KeyToggleLayers)) layerPanel.ToggleHidden();

        if (selection.HasSelection && !selection.IsCreatingSelection && !selection.IsDraggingFloating)
        {
            Rectangle curBounds = selection.GetCurrentBounds();
            Vector2 bottomCenterWorld = new Vector2(
                canvas.Offset.X + (curBounds.X + curBounds.Width / 2f) * canvas.PixelScale,
                canvas.Offset.Y + (curBounds.Y + curBounds.Height) * canvas.PixelScale
            );
            Vector2 screenAnchor = Raylib.GetWorldToScreen2D(bottomCenterWorld, camera);
            Rectangle menuRect = new Rectangle(Math.Clamp(screenAnchor.X - 142.5f, 150, sw - 190 - 285), Math.Clamp(screenAnchor.Y + 12, 45, sh - 45), 285, 32);

            if (Raylib.CheckCollisionPointRec(mouseScreenPos, menuRect))
                if (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonDown(MouseButton.Left)) return;
        }

        bool animPanelIntercepted = false;
        if (!topMenuIntercepted && !tabBarIntercepted)
           animPanelIntercepted = animationPanel.Update(canvas, history, mouseScreenPos, sw, sh, () => { selection.CancelOrCommit(canvas); ShowStatus($"Aktif Kare: {canvas.ActiveFrameIndex + 1}"); });

        bool toolPanelIntercepted = toolPanel.Update(
            mouseScreenPos, SelectTool, shape => toolManager.ActiveShape = shape,
            selMode => { selection.CancelOrCommit(canvas); toolManager.ActiveSelectionMode = selMode; }
        );

        bool paletteIntercepted = palette.Update(mouseScreenPos, () => colorPicker.Open(palette.CurrentColor));
        bool layerPanelIntercepted = layerPanel.Update(canvas, history, mouseScreenPos, sw, sh, () => { selection.CancelOrCommit(canvas); ShowStatus($"Aktif Katman: {canvas.Layers[canvas.ActiveLayerIndex].Name}"); });

        bool insideDrawingArea = !topMenuIntercepted && !tabBarIntercepted && !animPanelIntercepted &&
                                 !toolPanelIntercepted && !paletteIntercepted && !layerPanelIntercepted &&
                                 mouseScreenPos.X >= toolPanel.Width && mouseScreenPos.X < (sw - RightPanelWidth) &&
                                 mouseScreenPos.Y >= WorkspaceTopY;

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
                    ShowStatus($"Kalem Boyutu: {toolManager.PenSize}px");
                }
            }
            else
            {
                int prevIndex = ActiveTab.ZoomIndex;
                if (wheel > 0 && ActiveTab.ZoomIndex < ZoomLevels.Length - 1) ActiveTab.ZoomIndex++;
                else if (wheel < 0 && ActiveTab.ZoomIndex > 0) ActiveTab.ZoomIndex--;

                if (prevIndex != ActiveTab.ZoomIndex)
                {
                    camera.Offset = mouseScreenPos;
                    camera.Target = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
                    targetZoom = ZoomLevels[ActiveTab.ZoomIndex];
                    ActiveTab.Camera = camera;
                }
            }
        }

        if (Raylib.IsMouseButtonDown(MouseButton.Middle))
        {
            Vector2 delta = Raylib.GetMouseDelta();
            delta = Vector2.Multiply(delta, -1.0f / camera.Zoom);
            camera.Target = Vector2.Add(camera.Target, delta);
            ActiveTab.Camera = camera;
        }

        if (!isCtrlDown && !topMenuIntercepted && !tabBarIntercepted && !paletteIntercepted && !toolPanelIntercepted)
        {
            for (int i = 0; i < AppSettings.KeyColors.Length; i++)
            {
                if (AppSettings.KeyColors[i] != KeyboardKey.Null && Raylib.IsKeyPressed(AppSettings.KeyColors[i]))
                {
                    if (palette.SelectColor(i)) ShowStatus($"Renk {i + 1} secildi.");
                }
            }

            if (Raylib.IsKeyPressed(AppSettings.KeyPen)) SelectTool(ToolType.Pen);
            if (Raylib.IsKeyPressed(AppSettings.KeyEraser)) SelectTool(ToolType.Eraser);
            if (Raylib.IsKeyPressed(AppSettings.KeyBucket)) SelectTool(ToolType.Bucket);
            if (Raylib.IsKeyPressed(AppSettings.KeySmartBucket)) SelectTool(ToolType.SmartBucket);
            if (Raylib.IsKeyPressed(AppSettings.KeyEyedropper)) SelectTool(ToolType.Eyedropper);
            if (Raylib.IsKeyPressed(AppSettings.KeyLine)) SelectTool(ToolType.Line);
            if (Raylib.IsKeyPressed(AppSettings.KeyShape)) SelectTool(ToolType.Shape);
            if (Raylib.IsKeyPressed(AppSettings.KeySelectMove)) SelectTool(ToolType.SelectMove);
        }

        if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.Z)) { selection.CancelOrCommit(canvas); history.Undo(canvas); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.Y)) { selection.CancelOrCommit(canvas); history.Redo(canvas); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.S)) { if (ActiveTab != null) exportDialog.Open(ActiveTab.Title, ActiveTab.Canvas); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.C) && selection.HasSelection) { selection.Copy(canvas); ShowStatus("Secim kopyalandi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.X) && selection.HasSelection) { selection.Cut(canvas, history); ShowStatus("Secim kesildi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.V)) { SelectTool(ToolType.SelectMove); selection.Paste(canvas, history); ShowStatus("Yapistirildi"); }
        else if (isCtrlDown && Raylib.IsKeyPressed(KeyboardKey.D) && selection.HasSelection) { selection.Duplicate(canvas, history); ShowStatus("Secim cogaltildi"); }

        if ((Raylib.IsKeyPressed(KeyboardKey.Delete) || Raylib.IsKeyPressed(KeyboardKey.Backspace)) && selection.HasSelection) { selection.DeleteSelection(canvas, history); ShowStatus("Secim silindi"); }
        if ((Raylib.IsKeyPressed(KeyboardKey.Escape) || Raylib.IsKeyPressed(KeyboardKey.Enter)) && selection.HasSelection) { selection.CancelOrCommit(canvas); ShowStatus("Secim kapatildi"); }

        Vector2 worldMousePos = Raylib.GetScreenToWorld2D(mouseScreenPos, camera);
        var (gx, gy) = canvas.ScreenToGrid(worldMousePos);

        if (!Raylib.IsMouseButtonDown(MouseButton.Left) && !Raylib.IsMouseButtonDown(MouseButton.Right)) lastGridPos = null;

        if (toolManager.ActiveTool == ToolType.SelectMove)
        {
            bool isShift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
            bool isAlt = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
            bool isAdd = isShift;
            bool isSubtract = isCtrlDown || isAlt;

            if (insideDrawingArea && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (!isAdd && !isSubtract && selection.HasSelection && selection.IsPointInCurrentSelection(gx, gy)) selection.StartMove(canvas, history, gx, gy);
                else if (canvas.IsMouseInside(worldMousePos)) selection.StartSelection(canvas, gx, gy, toolManager.ActiveSelectionMode, isAdd, isSubtract);
                else selection.CancelOrCommit(canvas);
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
        }
        else if (insideDrawingArea && canvas.IsMouseInside(worldMousePos))
        {
            if (toolManager.ActiveTool == ToolType.Eyedropper || Raylib.IsKeyDown(KeyboardKey.LeftAlt))
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left))
                {
                    Color pickedColor = canvas.GetMergedPixel(gx, gy);
                    if (pickedColor.A > 0) { palette.SetColor(pickedColor); ShowStatus($"Renk secildi: R:{pickedColor.R}"); }
                    if (toolManager.ActiveTool == ToolType.Eyedropper) toolManager.ActiveTool = ToolType.Pen;
                }
            }
            else if (toolManager.ActiveTool == ToolType.Bucket || toolManager.ActiveTool == ToolType.SmartBucket)
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) { history.SaveState(canvas); toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor); }
            }
            else if (toolManager.ActiveTool == ToolType.Line || toolManager.ActiveTool == ToolType.Shape)
            {
                if (Raylib.IsMouseButtonPressed(MouseButton.Left)) dragStartPos = (gx, gy);
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
                if (Raylib.IsMouseButtonPressed(MouseButton.Left) || Raylib.IsMouseButtonPressed(MouseButton.Right)) history.SaveState(canvas);
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    if (lastGridPos.HasValue) toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, palette.CurrentColor);
                    else toolManager.ApplyTool(canvas, gx, gy, palette.CurrentColor);
                    lastGridPos = (gx, gy);
                }
                else if (Raylib.IsMouseButtonDown(MouseButton.Right))
                {
                    // Hata Düzeltmesi: Sağ tık her zaman silgi boyutunu kullanarak siler
                    if (lastGridPos.HasValue) toolManager.DrawLine(canvas, lastGridPos.Value.x, lastGridPos.Value.y, gx, gy, Canvas.TransparentColor, forceEraser: true);
                    else toolManager.ApplyTool(canvas, gx, gy, Canvas.TransparentColor, forceEraser: true);
                    lastGridPos = (gx, gy);
                }
            }
        }
        else if (Raylib.IsMouseButtonReleased(MouseButton.Left)) dragStartPos = null;

        if (messageTimer > 0)
        {
            messageTimer -= Raylib.GetFrameTime();
            if (messageTimer <= 0) statusMessage = "Hazir";
        }
    }

    static void Draw()
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        Vector2 mouseScreenPos = Raylib.GetMousePosition();

        Raylib.BeginDrawing();
        Raylib.ClearBackground(PixelSkin.WindowBg);

        if (ActiveTab == null) homeScreen.Draw(sw, sh, WorkspaceTopY);
        else
        {
            Canvas canvas = ActiveTab.Canvas;
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

            if (canvas.IsMouseInside(worldMousePos) && (toolManager.ActiveTool == ToolType.Pen || toolManager.ActiveTool == ToolType.Eraser))
            {
                int size = toolManager.ActiveTool == ToolType.Pen ? toolManager.PenSize : toolManager.EraserSize;
                int offset = size / 2;
                int startX = gx - offset;
                int startY = gy - offset;

                Rectangle brushPreviewRect = new Rectangle(
                    canvas.Offset.X + startX * canvas.PixelScale,
                    canvas.Offset.Y + startY * canvas.PixelScale,
                    size * canvas.PixelScale,
                    size * canvas.PixelScale
                );

                float pulse = (float)(Math.Sin(Raylib.GetTime() * 8.0) * 0.5 + 0.5);
                byte alphaPulse = (byte)(150 + pulse * 105);

                if (toolManager.ActiveTool == ToolType.Pen)
                {
                    Color ghostColor = new Color(palette.CurrentColor.R, palette.CurrentColor.G, palette.CurrentColor.B, (byte)85);
                    Raylib.DrawRectangleRec(brushPreviewRect, ghostColor);
                }

                float lineThickness = Math.Max(1f, 1.5f / camera.Zoom);
                Color outlineCol = toolManager.ActiveTool == ToolType.Eraser 
                    ? new Color((byte)255, (byte)100, (byte)100, alphaPulse) 
                    : new Color((byte)255, (byte)255, (byte)255, alphaPulse);

                Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness * 1.6f, new Color((byte)0, (byte)0, (byte)0, (byte)200));
                Raylib.DrawRectangleLinesEx(brushPreviewRect, lineThickness, outlineCol);
            }

            selection.DrawWorldOverlay(canvas);
            Raylib.EndMode2D();
            selection.CheckAndDrawFloatingMenu(canvas, camera, mouseScreenPos, history, ShowStatus);

            // Z-Order Düzeltmesi: Katman paneli altta, animasyon paneli (açılır ayarlar menüsüyle birlikte) üstte çizilir
            layerPanel.Draw(canvas, sw, sh);
            animationPanel.Draw(canvas, sw, sh);
            toolPanel.Draw(toolManager.ActiveTool, toolManager.ActiveShape, toolManager.ActiveSelectionMode, sh);

            int rightPanelX = sw - RightPanelWidth;
            Rectangle rightPanelRect = new Rectangle(rightPanelX, WorkspaceTopY, RightPanelWidth, sh - WorkspaceTopY);
            PixelSkin.DrawPanel(rightPanelRect);

            palette.Draw();

            Rectangle infoBox = new Rectangle(rightPanelX + 10, 430, RightPanelWidth - 20, 145);
            PixelSkin.DrawInsetBox(infoBox);

            Raylib.DrawText($"Tuval: {canvas.Width}x{canvas.Height}", rightPanelX + 20, 442, 13, Color.LightGray);
            string toolDesc = toolManager.ActiveTool switch
            {
                ToolType.Shape => $"Sekil ({toolManager.ActiveShape})",
                ToolType.SelectMove => $"Sec ({toolManager.ActiveSelectionMode})",
                _ => toolManager.ActiveTool.ToString()
            };
            Raylib.DrawText($"Arac: {toolDesc}", rightPanelX + 20, 466, 12, Color.Yellow);
            Raylib.DrawText($"Zoom: {ZoomLevels[ActiveTab.ZoomIndex]}x", rightPanelX + 20, 490, 13, Color.LightGray);

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
        }

        tabBar.Draw(tabs, activeTabIndex, sw);
        topMenu.Draw(sw);

        colorPicker.Draw();
        newProjectDialog.Draw();
        settingsDialog.Draw(isGridVisible);
        exportDialog.Draw();

        Raylib.EndDrawing();
    }
}