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
    private Rectangle windowRect = new Rectangle(0, 0, 480, 305);

    public string OutputPath { get; set; } = "";
    public int ScaleMultiplier { get; set; } = 1;
    public bool ExportAllFrames { get; set; } = false;
    public bool SheetVertical { get; set; } = false;

    private Canvas? currentCanvas;

    private bool IsGifSelected => OutputPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
    private bool IsJpgSelected => OutputPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || OutputPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

    public void Open(string defaultFileName, Canvas canvas)
    {
        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        currentCanvas = canvas;

        string directory = (!string.IsNullOrEmpty(OutputPath) && !string.IsNullOrEmpty(Path.GetDirectoryName(OutputPath)))
            ? Path.GetDirectoryName(OutputPath)!
            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string ext = (!string.IsNullOrEmpty(OutputPath) && Path.HasExtension(OutputPath))
            ? Path.GetExtension(OutputPath)
            : ".png";
        string baseTitle = Path.GetFileNameWithoutExtension(defaultFileName);
        OutputPath = Path.Combine(directory, baseTitle + ext);

        IsOpen = true;
    }

    private void ChangeExtension(string newExt)
    {
        if (string.IsNullOrWhiteSpace(OutputPath)) return;
        OutputPath = Path.ChangeExtension(OutputPath, newExt);
    }

    public void Close() => IsOpen = false;

    public bool Update(Vector2 mousePos, Action<string, int, bool, bool> onExport)
    {
        if (!IsOpen) return false;

        windowRect.X = (Raylib.GetScreenWidth() - windowRect.Width) / 2f;
        windowRect.Y = (Raylib.GetScreenHeight() - windowRect.Height) / 2f;

        float startX = windowRect.X + 115;
        float startY = windowRect.Y + 46;

        Rectangle pathBox = new Rectangle(startX, startY, windowRect.Width - 170, 26);
        Rectangle browseBtn = new Rectangle(windowRect.X + windowRect.Width - 48, startY, 32, 26);

        Rectangle fmtPngBtn = new Rectangle(startX, startY + 36, 70, 26);
        Rectangle fmtGifBtn = new Rectangle(startX + 76, startY + 36, 130, 26);
        Rectangle fmtJpgBtn = new Rectangle(startX + 212, startY + 36, 70, 26);

        Rectangle resizeMinus = new Rectangle(startX, startY + 72, 28, 26);
        Rectangle resizePlus = new Rectangle(startX + 142, startY + 72, 28, 26);

        Rectangle framesBtn = new Rectangle(startX, startY + 108, 190, 26);
        Rectangle layoutBtn = new Rectangle(startX, startY + 144, 190, 26);

        Rectangle exportBtn = new Rectangle(windowRect.X + windowRect.Width - 200, windowRect.Y + windowRect.Height - 44, 88, 30);
        Rectangle cancelBtn = new Rectangle(windowRect.X + windowRect.Width - 102, windowRect.Y + windowRect.Height - 44, 84, 30);
        Rectangle xBtn = new Rectangle(windowRect.X + windowRect.Width - 30, windowRect.Y + 5, 24, 24);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, browseBtn) || Raylib.CheckCollisionPointRec(mousePos, pathBox))
            {
                string? newPath = Exporter.GetSavePathDialog(OutputPath);
                if (!string.IsNullOrEmpty(newPath)) OutputPath = newPath;
            }

            if (Raylib.CheckCollisionPointRec(mousePos, fmtPngBtn)) ChangeExtension(".png");
            if (Raylib.CheckCollisionPointRec(mousePos, fmtGifBtn)) { ChangeExtension(".gif"); ExportAllFrames = true; }
            if (Raylib.CheckCollisionPointRec(mousePos, fmtJpgBtn)) ChangeExtension(".jpg");

            if (Raylib.CheckCollisionPointRec(mousePos, resizeMinus)) ScaleMultiplier = Math.Max(1, ScaleMultiplier - 1);
            if (Raylib.CheckCollisionPointRec(mousePos, resizePlus)) ScaleMultiplier = Math.Min(20, ScaleMultiplier + 1);

            if (!IsGifSelected)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, framesBtn)) ExportAllFrames = !ExportAllFrames;
                if (ExportAllFrames && Raylib.CheckCollisionPointRec(mousePos, layoutBtn)) SheetVertical = !SheetVertical;
            }

            if (Raylib.CheckCollisionPointRec(mousePos, cancelBtn) || Raylib.CheckCollisionPointRec(mousePos, xBtn)) Close();

            if (Raylib.CheckCollisionPointRec(mousePos, exportBtn))
            {
                onExport(OutputPath, ScaleMultiplier, ExportAllFrames || IsGifSelected, SheetVertical);
                Close();
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Close();
        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            onExport(OutputPath, ScaleMultiplier, ExportAllFrames || IsGifSelected, SheetVertical);
            Close();
        }

        return true;
    }

    public void Draw()
    {
        if (!IsOpen || currentCanvas == null) return;
        Vector2 mousePos = Raylib.GetMousePosition();

        PixelSkin.DrawDialogWindow(windowRect, "Disa Aktar (PNG / Animasyonlu GIF / Sprite Sheet)", mousePos, out _);

        float labelX = windowRect.X + 18;
        float startX = windowRect.X + 115;
        float startY = windowRect.Y + 46;

        Raylib.DrawText("Dosya Yolu:", (int)labelX, (int)startY + 6, 13, PixelSkin.TextMuted);
        Rectangle pathBox = new Rectangle(startX, startY, windowRect.Width - 170, 26);
        PixelSkin.DrawInsetBox(pathBox);

        string displayPath = OutputPath.Length > 38 ? "..." + OutputPath[^38..] : OutputPath;
        Raylib.DrawText(displayPath, (int)pathBox.X + 8, (int)pathBox.Y + 7, 12, PixelSkin.TextPrimary);

        Rectangle browseBtn = new Rectangle(windowRect.X + windowRect.Width - 48, startY, 32, 26);
        PixelSkin.DrawButton(browseBtn, Raylib.CheckCollisionPointRec(mousePos, browseBtn), false);
        Raylib.DrawText("...", (int)browseBtn.X + 9, (int)browseBtn.Y + 5, 14, Color.White);

        // Format Secimi
        Raylib.DrawText("Format:", (int)labelX, (int)startY + 42, 13, PixelSkin.TextMuted);
        Rectangle fmtPngBtn = new Rectangle(startX, startY + 36, 70, 26);
        Rectangle fmtGifBtn = new Rectangle(startX + 76, startY + 36, 130, 26);
        Rectangle fmtJpgBtn = new Rectangle(startX + 212, startY + 36, 70, 26);

        bool isGif = IsGifSelected;
        bool isJpg = IsJpgSelected;
        bool isPng = !isGif && !isJpg;

        PixelSkin.DrawButton(fmtPngBtn, Raylib.CheckCollisionPointRec(mousePos, fmtPngBtn), isPng);
        Raylib.DrawText("PNG", (int)fmtPngBtn.X + 22, (int)fmtPngBtn.Y + 7, 12, isPng ? Color.Yellow : Color.White);

        PixelSkin.DrawButton(fmtGifBtn, Raylib.CheckCollisionPointRec(mousePos, fmtGifBtn), isGif);
        Raylib.DrawText("Animasyon GIF", (int)fmtGifBtn.X + 18, (int)fmtGifBtn.Y + 7, 12, isGif ? Color.Yellow : Color.White);

        PixelSkin.DrawButton(fmtJpgBtn, Raylib.CheckCollisionPointRec(mousePos, fmtJpgBtn), isJpg);
        Raylib.DrawText("JPG", (int)fmtJpgBtn.X + 22, (int)fmtJpgBtn.Y + 7, 12, isJpg ? Color.Yellow : Color.White);

        // Olcek Secimi
        Raylib.DrawText("Olcek (Scale):", (int)labelX, (int)startY + 78, 13, PixelSkin.TextMuted);
        Rectangle minusBtn = new Rectangle(startX, startY + 72, 28, 26);
        Rectangle scaleBox = new Rectangle(startX + 32, startY + 72, 106, 26);
        Rectangle plusBtn = new Rectangle(startX + 142, startY + 72, 28, 26);

        PixelSkin.DrawButton(minusBtn, Raylib.CheckCollisionPointRec(mousePos, minusBtn), false);
        Raylib.DrawText("-", (int)minusBtn.X + 10, (int)minusBtn.Y + 6, 14, Color.White);

        PixelSkin.DrawInsetBox(scaleBox);
        string scaleText = $"{ScaleMultiplier * 100}% ({currentCanvas.Width * ScaleMultiplier}x{currentCanvas.Height * ScaleMultiplier})";
        int sw = Raylib.MeasureText(scaleText, 12);
        Raylib.DrawText(scaleText, (int)(scaleBox.X + (scaleBox.Width - sw) / 2), (int)scaleBox.Y + 7, 12, PixelSkin.AccentHover);

        PixelSkin.DrawButton(plusBtn, Raylib.CheckCollisionPointRec(mousePos, plusBtn), false);
        Raylib.DrawText("+", (int)plusBtn.X + 9, (int)plusBtn.Y + 6, 14, Color.White);

        Raylib.DrawText("Kareler:", (int)labelX, (int)startY + 114, 13, PixelSkin.TextMuted);
        if (isGif)
        {
            Raylib.DrawText($"Tum gorunur kareler hareketli GIF olarak kaydedilir ({currentCanvas.Frames.Count} kare).", (int)startX, (int)startY + 114, 11, Color.SkyBlue);
        }
        else
        {
            Rectangle framesBtn = new Rectangle(startX, startY + 108, 190, 26);
            PixelSkin.DrawButton(framesBtn, Raylib.CheckCollisionPointRec(mousePos, framesBtn), false);
            Raylib.DrawText(ExportAllFrames ? "Tum Kareler (Sprite Sheet)" : "Sadece Aktif Kare", (int)framesBtn.X + 10, (int)framesBtn.Y + 7, 12, Color.White);

            if (ExportAllFrames)
            {
                Raylib.DrawText("Dizilim:", (int)labelX, (int)startY + 150, 13, PixelSkin.TextMuted);
                Rectangle layoutBtn = new Rectangle(startX, startY + 144, 190, 26);
                PixelSkin.DrawButton(layoutBtn, Raylib.CheckCollisionPointRec(mousePos, layoutBtn), false);
                Raylib.DrawText(SheetVertical ? "Dikey Serit (Vertical)" : "Yatay Serit (Horizontal)", (int)layoutBtn.X + 10, (int)layoutBtn.Y + 7, 12, Color.White);
            }
        }

        Raylib.DrawLine((int)windowRect.X, (int)(windowRect.Y + windowRect.Height - 56), (int)(windowRect.X + windowRect.Width), (int)(windowRect.Y + windowRect.Height - 56), PixelSkin.BorderOutline);

        UIWidgets.DrawColorButton(new Rectangle(windowRect.X + windowRect.Width - 200, windowRect.Y + windowRect.Height - 44, 88, 30), "Aktar", PixelSkin.AccentPrimary, mousePos);
        UIWidgets.DrawColorButton(new Rectangle(windowRect.X + windowRect.Width - 102, windowRect.Y + windowRect.Height - 44, 84, 30), "Iptal", new Color(110, 45, 50, 255), mousePos);
    }
}
