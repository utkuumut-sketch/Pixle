// === DOSYA: \App\Program.cs ===
using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;
using PixelArtApp.Core.Palettes;
using PixelArtApp.UI;

namespace PixelArtApp;

partial class Program
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
    static UnsavedChangesDialog unsavedDialog = null!;
    static ToolPanel toolPanel = null!;
    static ColorPickerDialog colorPicker = null!;
    static LayerPanel layerPanel = null!;
    static AnimationPanel animationPanel = null!;

    static int LeftPanelWidth
    {
        get => AppSettings.LeftPanelWidth;
        set => AppSettings.LeftPanelWidth = value;
    }
    static int RightPanelWidth
    {
        get => AppSettings.RightPanelWidth;
        set => AppSettings.RightPanelWidth = value;
    }

    static bool isDraggingLeftPanel = false;
    static bool isDraggingRightPanel = false;
    static bool isHoveringLeftEdge = false;
    static bool isHoveringRightEdge = false;

    static bool isDrawingWithLeft = false;
    static bool isDrawingWithRight = false;

    const float WorkspaceTopY = 72f;

    static readonly float[] ZoomLevels = new float[]
    { 0.5f, 0.75f, 1.0f, 1.25f, 1.5f, 1.75f, 2.0f, 2.5f, 3.0f, 4.0f, 5.0f, 6.0f, 8.0f };

    static string statusMessage = "Hazir";
    static float messageTimer = 0f;

    static (int x, int y)? lastGridPos = null;
    static (int x, int y)? dragStartPos = null;
    static (int x, int y)? curveStartPos = null;
    static (int x, int y)? curveEndPos = null;
    static bool isCurveAwaitingControl = false;
    static bool isGridVisible = false;
    static bool isTileWrapEnabled = false;

    static bool sizeInputActive = false;
    static string sizeInputText = "1";
    static float targetZoom = 1.0f;
    static ReferenceImageManager refManager = null!;

    static ProjectTab? ActiveTab => (activeTabIndex >= 0 && activeTabIndex < tabs.Count) ? tabs[activeTabIndex] : null;

    [STAThread]
    static void Main()
    {
        Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
        Raylib.InitWindow(1080, 740, "Pixel Art Studio");
        Raylib.SetWindowMinSize(900, 580);

        Raylib.SetTargetFPS(AppSettings.TargetFPS);
        Raylib.SetExitKey(KeyboardKey.Null);
        PixelSkin.LoadSkin();

        InitializeUIComponents();
        SetupTopMenu();

        while (!shouldExit)
        {
            if (Raylib.WindowShouldClose())
            {
                RequestExitApp();
            }

            Update();
            Draw();
        }

        foreach (var t in tabs)
        {
            t.History.ClearAll();
            t.Canvas.Dispose();
        }
        refManager.ClearReference();
        colorPicker.Unload();
        PixelSkin.UnloadSkin();
        Raylib.CloseWindow();
    }

    static void InitializeUIComponents()
    {
        toolManager = new ToolManager();
        topMenu = new TopMenu();
        tabBar = new TabBar();
        homeScreen = new HomeScreen();
        newProjectDialog = new NewProjectDialog();
        settingsDialog = new SettingsDialog();
        exportDialog = new ExportDialog();
        unsavedDialog = new UnsavedChangesDialog();
        toolPanel = new ToolPanel(WorkspaceTopY);
        layerPanel = new LayerPanel();
        animationPanel = new AnimationPanel();
        palette = new Palette(new Vector2(1080 - RightPanelWidth + 20, WorkspaceTopY + 35));
        colorPicker = new ColorPickerDialog();
        refManager = new ReferenceImageManager();
    }

    static void SetupTopMenu()
    {
        topMenu.AddCategory("Dosya", items =>
        {
            items.Add(new MenuItem("Yeni Proje (Ctrl+N)", () => newProjectDialog.Open()));
            items.Add(new MenuItem("Ac... (Ctrl+O)", OpenProjectWithDialog));
            items.Add(new MenuItem("Kaydet (Ctrl+S)", () => { if (ActiveTab != null) SaveTabProject(ActiveTab, saveAs: false); }));
            items.Add(new MenuItem("Farkli Kaydet... (Ctrl+Shift+S)", () => { if (ActiveTab != null) SaveTabProject(ActiveTab, saveAs: true); }));
            items.Add(new MenuItem("PNG / GIF / Sprite Sheet Disa Aktar (Ctrl+E)", () =>
            {
                if (ActiveTab != null) exportDialog.Open(ActiveTab.Title, ActiveTab.Canvas);
            }));
            items.Add(new MenuItem("Sekmeyi Kapat (Ctrl+W)", () => { if (activeTabIndex >= 0) RequestCloseTab(activeTabIndex); }));
            items.Add(new MenuItem("Cikis", RequestExitApp));
        });

        topMenu.AddCategory("Duzenle", items =>
        {
            items.Add(new MenuItem("Geri Al (Ctrl+Z)", () => { if (ActiveTab != null) { ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas); ActiveTab.History.Undo(ActiveTab.Canvas); } }));
            items.Add(new MenuItem("Ileri Al (Ctrl+Y)", () => { if (ActiveTab != null) { ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas); ActiveTab.History.Redo(ActiveTab.Canvas); } }));
            items.Add(new MenuItem("Kopyala (Ctrl+C)", () => ActiveTab?.Selection.Copy(ActiveTab.Canvas)));
            items.Add(new MenuItem("Kes (Ctrl+X)", () => { if (ActiveTab != null) ActiveTab.Selection.Cut(ActiveTab.Canvas, ActiveTab.History); }));
            items.Add(new MenuItem("Yapistir (Ctrl+V)", () => { if (ActiveTab != null) { SelectTool(ToolType.SelectMove); ActiveTab.Selection.Paste(ActiveTab.Canvas, ActiveTab.History); } }));
            items.Add(new MenuItem("Cogalt (Ctrl+D)", () => { if (ActiveTab != null) ActiveTab.Selection.Duplicate(ActiveTab.Canvas, ActiveTab.History); }));
            items.Add(new MenuItem("Aktif Katmani Birlestir", () =>
            {
                if (ActiveTab != null)
                {
                    ActiveTab.Selection.CancelOrCommit(ActiveTab.Canvas);
                    ActiveTab.History.SaveState(ActiveTab.Canvas);
                    ActiveTab.Canvas.MergeActiveLayer(out string msg);
                    ActiveTab.IsDirty = true;
                    ShowStatus(msg);
                }
            }));
            items.Add(new MenuItem("Aktif Katmani Ayir (Split / Unmerge)", () =>
            {
                if (ActiveTab != null)
                {
                    ActiveTab.History.SaveState(ActiveTab.Canvas);
                    ActiveTab.Canvas.SplitActiveLayer(ActiveTab.Selection, out string msg);
                    ActiveTab.IsDirty = true;
                    ShowStatus(msg);
                }
            }));
            items.Add(new MenuItem("Temizle", ClearCanvas));
            items.Add(new MenuItem("Ayarlar...", () => settingsDialog.Open()));
        });

        topMenu.AddCategory("Gorunum", items =>
        {
            items.Add(new MenuItem("Izgara Ac/Kapat", ToggleGrid));
            items.Add(new MenuItem("Karo Izgarasi (8x8 / 16x16 / 32x32) (G)", CycleTileGrid));
            items.Add(new MenuItem("Tile / Doseme Modu Ac/Kapat (T)", ToggleTileWrap));
            items.Add(new MenuItem("Alfa Kilidi Ac/Kapat (A)", ToggleAlphaLock));
            items.Add(new MenuItem("Sogan Zari (Onion Skin) Ac/Kapat", () =>
            {
                AppSettings.EnableOnionSkin = !AppSettings.EnableOnionSkin;
                ShowStatus(AppSettings.EnableOnionSkin ? "Onion Skin Acildi" : "Onion Skin Kapatildi");
            }));
            items.Add(new MenuItem("Animasyon Paneli Gizle/Ac (Yukari Ok)", () => animationPanel.ToggleHidden()));
            items.Add(new MenuItem("Katman Paneli Gizle/Ac (Asagi Ok)", () => layerPanel.ToggleHidden()));
            items.Add(new MenuItem("Referans Bolumunu Gizle/Ac", () =>
            {
                AppSettings.ShowReferencePanel = !AppSettings.ShowReferencePanel;
                ShowStatus(AppSettings.ShowReferencePanel ? "Referans bolumu acildi" : "Referans bolumu gizlendi");
            }));
            items.Add(new MenuItem("Pixel-Perfect Modu Ac/Kapat (P)", () =>
            {
                toolManager.PixelPerfect = !toolManager.PixelPerfect;
                ShowStatus($"Pixel-Perfect: {(toolManager.PixelPerfect ? "Acik" : "Kapali")}");
            }));
            items.Add(new MenuItem("Simetri Modu Degistir (S)", () =>
            {
                toolManager.CycleSymmetry();
                ShowStatus($"Simetri Modu: {ToolManager.SymmetryLabel(toolManager.Symmetry)}");
            }));
            items.Add(new MenuItem("Kamerayi Sifirla", () => { if (ActiveTab != null) CenterCamera(ActiveTab); }));
            items.Add(new MenuItem("Panellerin Yerini Sifirla", () =>
            {
                LeftPanelWidth = 92;
                RightPanelWidth = 180;
                toolPanel.Width = 92f;
                ShowStatus("Panellerin genisligi sifirlandi.");
            }));
            items.Add(new MenuItem("Ana Ekran (Home)", () => SwitchToTab(-1)));
        });

        topMenu.AddCategory("Paletler", items =>
        {
            for (int i = 0; i < PalettePresets.BuiltIn.Count; i++)
            {
                int presetIdx = i;
                string pName = PalettePresets.BuiltIn[i].Name;
                items.Add(new MenuItem($"Hazir Palet: {pName}", () =>
                {
                    palette.ApplyPreset(presetIdx);
                    if (ActiveTab != null) ActiveTab.IsDirty = true;
                    ShowStatus($"Palet yuklendi: {pName}");
                }));
            }
            items.Add(new MenuItem("Palet Dosyasi Ice Aktar (.hex / .pal / .gpl)...", ImportPaletteFromFile));
            items.Add(new MenuItem("Mevcut Paleti .HEX Olarak Disa Aktar...", ExportPaletteToFile));
        });

        topMenu.Layout();
    }
}
