using Raylib_cs;

namespace PixelArtApp.Core;

public class ProjectTab
{
    public string Title { get; set; }
    public string? FilePath { get; set; }
    
    // YENI: Projenin kaydedilmemis degisiklik icerip icermedigini takip eder
    public bool IsDirty { get; set; } = false;
    
    // YENI: Her projenin kendi animasyon FPS degeri
    public float Fps { get; set; } = 8f;

    public Canvas Canvas { get; set; }
    public HistoryManager History { get; set; }
    public SelectionManager Selection { get; set; }
    public Camera2D Camera { get; set; }
    public int ZoomIndex { get; set; } = 2;

    public ProjectTab(string title, int width, int height, Color bgColor, bool showGrid)
    {
        Title = title;
        int scale = width switch { <= 16 => 24, <= 32 => 14, <= 64 => 8, _ => 4 };
        Canvas = new Canvas(width, height, scale, System.Numerics.Vector2.Zero);
        Canvas.ShowGrid = showGrid;
        if (bgColor.A > 0) Canvas.Clear(bgColor);
        
        History = new HistoryManager();
        // History uzerinde her islem yapildiginda bu sekmeyi "degisti" (IsDirty = true) olarak isaretle
        History.OnStateChanged = () => IsDirty = true;

        Selection = new SelectionManager();
        Selection.EnsureSize(width, height);
    }

    // Sekme cubugunda (TabBar) gosterilecek baslik (Kaydedilmemisse sonuna * ekler)
    public string GetDisplayTitle() => IsDirty ? $"{Title}*" : Title;
}