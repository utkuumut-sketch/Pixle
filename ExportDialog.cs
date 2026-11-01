using System;
using System.Numerics;
using System.IO;
using Raylib_cs;
using PixelArtApp.IO;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class ExportDialog
{
    public bool IsOpen { get; private set; } = false;
    private Rectangle windowRect = new Rectangle(0, 0, 480, 270); 

    public string OutputPath { get; set; } = "";
    public int ScaleMultiplier { get; set; } = 1;
    public bool ExportAllFrames { get; set; } = false; 
    public bool SheetVertical { get; set; } = false; 
    
    private Canvas? currentCanvas;
    
    private Color bgMain = new Color(50, 50, 50, 255);
    private Color bgTitle = new Color(35, 35, 35, 255);
    private Color borderLine = new Color(20, 20, 20, 255);
    private Color bgInput = new Color(30, 30, 30, 255);
    private Color btnNormal = new Color(75, 75, 75, 255);
    private Color btnHover = new Color(90, 90, 90, 255);
    private Color textLight = new Color(230, 230, 230, 255);
    private Color textDark = new Color(170, 170, 170, 255);

    public void Open(string defaultFileName, Canvas canvas)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;
        
        currentCanvas = canvas;
        
        // EĞER İLK DEFA AÇILIYORSA VEYA BOŞSA, DOĞRUDAN MASAÜSTÜNÜ (DESKTOP) HEDEFLE
        if (string.IsNullOrEmpty(OutputPath))
        {
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string safeName = defaultFileName.EndsWith(".png") ? defaultFileName : defaultFileName + ".png";
            OutputPath = Path.Combine(desktopPath, safeName);
        }

        IsOpen = true;
    }

    public void Close() { IsOpen = false; }

    public bool Update(Vector2 mousePos, Action<string, int, bool, bool> onExport)
    {
        if (!IsOpen) return false;

        float startX = windowRect.X + 110;
        float startY = windowRect.Y + 45;
        
        // Yol kutusunun tamamı ve [...] butonu tıklanabilir alan
        Rectangle pathBox = new Rectangle(startX, startY, windowRect.Width - 165, 24);
        Rectangle browseBtn = new Rectangle(windowRect.X + windowRect.Width - 45, startY, 30, 24);
        
        Rectangle resizeMinus = new Rectangle(startX, startY + 35, 24, 24);
        Rectangle resizePlus = new Rectangle(startX + 140, startY + 35, 24, 24);
        
        Rectangle framesBtn = new Rectangle(startX, startY + 70, 164, 24);
        Rectangle layoutBtn = new Rectangle(startX, startY + 105, 164, 24);

        Rectangle exportBtn = new Rectangle(windowRect.X + windowRect.Width - 190, windowRect.Y + windowRect.Height - 40, 80, 26);
        Rectangle cancelBtn = new Rectangle(windowRect.X + windowRect.Width - 100, windowRect.Y + windowRect.Height - 40, 80, 26);
        Rectangle xBtn = new Rectangle(windowRect.X + windowRect.Width - 28, windowRect.Y + 4, 24, 24);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            // Kutunun neresine tıklanırsa tıklansın pencere açılır
            if (Raylib.CheckCollisionPointRec(mousePos, browseBtn) || Raylib.CheckCollisionPointRec(mousePos, pathBox))
            {
                string? newPath = Exporter.GetSavePathDialog(OutputPath);
                if (!string.IsNullOrEmpty(newPath)) OutputPath = newPath;
            }
            
            if (Raylib.CheckCollisionPointRec(mousePos, resizeMinus)) ScaleMultiplier = Math.Max(1, ScaleMultiplier - 1);
            if (Raylib.CheckCollisionPointRec(mousePos, resizePlus)) ScaleMultiplier = Math.Min(20, ScaleMultiplier + 1);

            if (Raylib.CheckCollisionPointRec(mousePos, framesBtn)) ExportAllFrames = !ExportAllFrames;
            
            if (ExportAllFrames && Raylib.CheckCollisionPointRec(mousePos, layoutBtn)) SheetVertical = !SheetVertical;

            if (Raylib.CheckCollisionPointRec(mousePos, cancelBtn) || Raylib.CheckCollisionPointRec(mousePos, xBtn)) Close();
            
            if (Raylib.CheckCollisionPointRec(mousePos, exportBtn))
            {
                onExport(OutputPath, ScaleMultiplier, ExportAllFrames, SheetVertical);
                Close();
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Close();
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            onExport(OutputPath, ScaleMultiplier, ExportAllFrames, SheetVertical);
            Close();
        }

        return true; 
    }

    public void Draw()
    {
        if (!IsOpen || currentCanvas == null) return;
        Vector2 mousePos = Raylib.GetMousePosition();

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 150));
        
        Raylib.DrawRectangleRec(windowRect, bgMain);
        Raylib.DrawRectangleLinesEx(windowRect, 1, borderLine);
        
        Rectangle titleBar = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 28);
        Raylib.DrawRectangleRec(titleBar, bgTitle);
        Raylib.DrawLine((int)windowRect.X, (int)windowRect.Y + 28, (int)(windowRect.X + windowRect.Width), (int)windowRect.Y + 28, borderLine);
        Raylib.DrawText("Export File", (int)windowRect.X + 10, (int)windowRect.Y + 7, 14, textLight);
        
        Rectangle xBtn = new Rectangle(windowRect.X + windowRect.Width - 28, windowRect.Y + 2, 24, 24);
        Raylib.DrawText("X", (int)xBtn.X + 8, (int)xBtn.Y + 5, 14, Raylib.CheckCollisionPointRec(mousePos, xBtn) ? Color.Red : textDark);

        float labelX = windowRect.X + 15;
        float startX = windowRect.X + 110;
        float startY = windowRect.Y + 45;

        // 1. Output File
        Raylib.DrawText("Output File:", (int)labelX, (int)startY + 5, 13, textLight);
        
        Rectangle pathBox = new Rectangle(startX, startY, windowRect.Width - 165, 24);
        bool isPathHover = Raylib.CheckCollisionPointRec(mousePos, pathBox);
        Raylib.DrawRectangleRec(pathBox, isPathHover ? btnHover : bgInput);
        Raylib.DrawRectangleLinesEx(pathBox, 1, isPathHover ? Color.SkyBlue : borderLine);
        
        string displayPath = OutputPath.Length > 40 ? "..." + OutputPath.Substring(OutputPath.Length - 40) : OutputPath;
        Raylib.DrawText(displayPath, (int)pathBox.X + 6, (int)pathBox.Y + 6, 12, textLight);
        
        DrawAsepriteBtn(new Rectangle(windowRect.X + windowRect.Width - 45, startY, 30, 24), "...", mousePos);

        // 2. Resize
        Raylib.DrawText("Resize:", (int)labelX, (int)startY + 40, 13, textLight);
        DrawAsepriteBtn(new Rectangle(startX, startY + 35, 24, 24), "-", mousePos);
        
        Rectangle scaleBox = new Rectangle(startX + 30, startY + 35, 105, 24);
        Raylib.DrawRectangleRec(scaleBox, bgInput);
        Raylib.DrawRectangleLinesEx(scaleBox, 1, borderLine);
        string scaleText = $"{ScaleMultiplier * 100}% ({currentCanvas.Width * ScaleMultiplier}x{currentCanvas.Height * ScaleMultiplier})";
        int sw = Raylib.MeasureText(scaleText, 12);
        Raylib.DrawText(scaleText, (int)(scaleBox.X + (scaleBox.Width - sw)/2), (int)scaleBox.Y + 6, 12, textLight);
        
        DrawAsepriteBtn(new Rectangle(startX + 140, startY + 35, 24, 24), "+", mousePos);

        // 3. Layers
        Raylib.DrawText("Layers:", (int)labelX, (int)startY + 75 + 35, 13, textLight);
        Rectangle layerBox = new Rectangle(startX, startY + 70 + 35, 164, 24);
        Raylib.DrawRectangleRec(layerBox, bgInput);
        Raylib.DrawRectangleLinesEx(layerBox, 1, borderLine);
        Raylib.DrawText("Visible layers", (int)layerBox.X + 8, (int)layerBox.Y + 6, 12, textDark);

        // 4. Frames
        Raylib.DrawText("Frames:", (int)labelX, (int)startY + 75, 13, textLight);
        DrawDropdownSim(new Rectangle(startX, startY + 70, 164, 24), ExportAllFrames ? "All frames" : "Current frame", mousePos);

        // 5. Layout 
        if (ExportAllFrames)
        {
            Raylib.DrawText("Layout:", (int)labelX, (int)startY + 110, 13, textLight);
            DrawDropdownSim(new Rectangle(startX, startY + 105, 164, 24), SheetVertical ? "Vertical Strip" : "Horizontal Strip", mousePos);
        }

        Raylib.DrawLine((int)windowRect.X, (int)(windowRect.Y + windowRect.Height - 55), (int)(windowRect.X + windowRect.Width), (int)(windowRect.Y + windowRect.Height - 55), borderLine);
        
        DrawAsepriteBtn(new Rectangle(windowRect.X + windowRect.Width - 190, windowRect.Y + windowRect.Height - 40, 80, 26), "Export", mousePos);
        DrawAsepriteBtn(new Rectangle(windowRect.X + windowRect.Width - 100, windowRect.Y + windowRect.Height - 40, 80, 26), "Cancel", mousePos);
    }

    private void DrawAsepriteBtn(Rectangle rect, string text, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Raylib.DrawRectangleRec(rect, hover ? btnHover : btnNormal);
        Raylib.DrawRectangleLinesEx(rect, 1, borderLine);
        
        int tw = Raylib.MeasureText(text, 13);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)(rect.Y + (rect.Height - 13) / 2), 13, textLight);
    }

    private void DrawDropdownSim(Rectangle rect, string text, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Raylib.DrawRectangleRec(rect, hover ? btnHover : btnNormal);
        Raylib.DrawRectangleLinesEx(rect, 1, borderLine);
        Raylib.DrawText(text, (int)rect.X + 8, (int)rect.Y + 6, 12, textLight);
        Raylib.DrawText("v", (int)(rect.X + rect.Width - 15), (int)rect.Y + 6, 12, textDark);
    }
}