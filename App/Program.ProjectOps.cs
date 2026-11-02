// === DOSYA: \App\Program.ProjectOps.cs ===
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
    static void SwitchToTab(int newIndex)
    {
        if (ActiveTab != null)
        {
            ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
            ActiveTab.Fps = animationPanel.Fps;
        }

        activeTabIndex = newIndex;

        if (ActiveTab != null)
        {
            targetZoom = ZoomLevels[ActiveTab.ZoomIndex];
            animationPanel.Fps = ActiveTab.Fps;
            isGridVisible = ActiveTab.Canvas.ShowGrid;
        }
    }

    static void CreateNewTab(int width, int height, Color bgColor, string? customTitle = null)
    {
        string title = customTitle ?? $"Sprite-{untitledCounter++}";
        ProjectTab newTab = new ProjectTab(title, width, height, bgColor, isGridVisible);
        newTab.IsDirty = false;
        tabs.Add(newTab);
        SwitchToTab(tabs.Count - 1);
        CenterCamera(newTab);
        ShowStatus($"Yeni proje olusturuldu: {width}x{height}");
    }

    static bool SaveTabProject(ProjectTab tab, bool saveAs)
    {
        tab.Fps = animationPanel.Fps;
        string? targetPath = tab.FilePath;

        if (saveAs || string.IsNullOrWhiteSpace(targetPath) || !targetPath.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
        {
            targetPath = Exporter.SaveLoomDialog(tab.FilePath, tab.Title);
            if (string.IsNullOrWhiteSpace(targetPath))
            {
                ShowStatus("Kaydetme iptal edildi.");
                return false;
            }
        }

        if (LoomFileManager.SaveProject(tab, palette, targetPath, out string errorMsg))
        {
            homeScreen.AddRecentFile(tab.FilePath!);
            ShowStatus($"Kaydedildi: {Path.GetFileName(tab.FilePath)}");
            return true;
        }
        else
        {
            ShowStatus(errorMsg);
            return false;
        }
    }

    static void RequestCloseTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;
        ProjectTab tab = tabs[index];

        if (!tab.IsDirty)
        {
            ForceCloseTab(index);
            return;
        }

        SwitchToTab(index);
        unsavedDialog.Open(tab.Title, result =>
        {
            if (result == UnsavedDialogResult.Save)
            {
                if (SaveTabProject(tab, saveAs: false))
                {
                    int currentIdx = tabs.IndexOf(tab);
                    if (currentIdx >= 0) ForceCloseTab(currentIdx);
                }
            }
            else if (result == UnsavedDialogResult.Discard)
            {
                int currentIdx = tabs.IndexOf(tab);
                if (currentIdx >= 0) ForceCloseTab(currentIdx);
            }
        });
    }

    static void ForceCloseTab(int index)
    {
        if (index < 0 || index >= tabs.Count) return;

        bool wasActive = (index == activeTabIndex);
        if (ActiveTab != null && !wasActive)
        {
            ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
            ActiveTab.Fps = animationPanel.Fps;
        }

        tabs[index].Selection.CancelOrCommit(tabs[index].Canvas);
        tabs[index].History.ClearAll();
        tabs[index].Canvas.Dispose();
        tabs.RemoveAt(index);

        if (tabs.Count == 0)
        {
            activeTabIndex = -1;
        }
        else if (activeTabIndex > index)
        {
            activeTabIndex--;
        }
        else if (activeTabIndex >= tabs.Count)
        {
            activeTabIndex = tabs.Count - 1;
        }

        if (ActiveTab != null)
        {
            targetZoom = ZoomLevels[ActiveTab.ZoomIndex];
            animationPanel.Fps = ActiveTab.Fps;
            isGridVisible = ActiveTab.Canvas.ShowGrid;
        }
    }

    static void RequestExitApp()
    {
        if (unsavedDialog.IsOpen) return;

        int dirtyIndex = tabs.FindIndex(t => t.IsDirty);
        if (dirtyIndex == -1)
        {
            shouldExit = true;
            return;
        }

        SwitchToTab(dirtyIndex);
        ProjectTab dirtyTab = tabs[dirtyIndex];

        unsavedDialog.Open(dirtyTab.Title, result =>
        {
            if (result == UnsavedDialogResult.Save)
            {
                if (SaveTabProject(dirtyTab, saveAs: false))
                {
                    int idx = tabs.IndexOf(dirtyTab);
                    if (idx >= 0) ForceCloseTab(idx);
                    RequestExitApp();
                }
            }
            else if (result == UnsavedDialogResult.Discard)
            {
                int idx = tabs.IndexOf(dirtyTab);
                if (idx >= 0) ForceCloseTab(idx);
                RequestExitApp();
            }
        });
    }

    static void OpenProjectWithDialog()
    {
        string? path = Exporter.OpenProjectOrImageDialog();
        if (!string.IsNullOrEmpty(path)) LoadProjectFromFile(path);
    }

    static void LoadProjectFromFile(string path)
    {
        if (!File.Exists(path))
        {
            ShowStatus("Dosya bulunamadi!");
            return;
        }

        int existingIdx = tabs.FindIndex(t => !string.IsNullOrEmpty(t.FilePath) && string.Equals(t.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (existingIdx >= 0)
        {
            SwitchToTab(existingIdx);
            ShowStatus($"Zaten acik: {Path.GetFileName(path)}");
            return;
        }

        if (path.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
        {
            ProjectTab? loadedTab = LoomFileManager.LoadProject(path, out List<Color>? loadedPalette, out string errorMsg);
            if (loadedTab == null)
            {
                ShowStatus(errorMsg);
                return;
            }

            if (loadedPalette != null && loadedPalette.Count > 0)
                palette.SetColors(loadedPalette);

            tabs.Add(loadedTab);
            SwitchToTab(tabs.Count - 1);
            CenterCamera(loadedTab);
            homeScreen.AddRecentFile(path);
            ShowStatus($"Proje acildi: {Path.GetFileName(path)}");
            return;
        }

        Image img = Raylib.LoadImage(path);
        try
        {
            if (img.Width <= 0 || img.Height <= 0)
            {
                ShowStatus("Gorsel dosyasi okunamadi veya bozuk!");
                return;
            }

            if (img.Width > 2048 || img.Height > 2048)
            {
                ShowStatus($"Gorsel boyutu cok buyuk (Maks: 2048x2048): {img.Width}x{img.Height}");
                return;
            }

            int w = img.Width;
            int h = img.Height;
            string fileName = Path.GetFileNameWithoutExtension(path);
            ProjectTab newTab = new ProjectTab(fileName, w, h, Canvas.TransparentColor, isGridVisible) { FilePath = path };

            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    newTab.Canvas.SetPixel(x, y, Raylib.GetImageColor(img, x, y));

            newTab.IsDirty = false;

            tabs.Add(newTab);
            SwitchToTab(tabs.Count - 1);
            CenterCamera(newTab);
            homeScreen.AddRecentFile(path);
            ShowStatus($"Gorsel ice aktarildi: {Path.GetFileName(path)} ({w}x{h})");
        }
        finally
        {
            if (img.Width > 0 && img.Height > 0)
            {
                Raylib.UnloadImage(img);
            }
        }
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
        if (ActiveTab != null) ActiveTab.IsDirty = true;
    }

    static void ClearCanvas()
    {
        if (ActiveTab == null) return;
        ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
        ActiveTab.History.SaveState(ActiveTab.Canvas);
        ActiveTab.Canvas.Clear(Canvas.TransparentColor);
        ShowStatus("Tuval temizlendi.");
    }

    static void ShowStatus(string msg) { statusMessage = msg; messageTimer = 3.5f; }

    static void CenterCamera(ProjectTab tab)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        float centerAreaX = (LeftPanelWidth + (sw - RightPanelWidth)) / 2f;
        float centerAreaY = (WorkspaceTopY + animationPanel.GetVisibleHeight() + (sh - layerPanel.GetVisibleHeight())) / 2f;

        Camera2D cam = new Camera2D
        {
            Target = new Vector2((tab.Canvas.Width * tab.Canvas.PixelScale) / 2f, (tab.Canvas.Height * tab.Canvas.PixelScale) / 2f),
            Offset = new Vector2(centerAreaX, centerAreaY),
            Rotation = 0.0f,
            Zoom = ZoomLevels[tab.ZoomIndex]
        };
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
}