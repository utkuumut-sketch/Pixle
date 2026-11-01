using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.IO;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.IO;

public static class Exporter
{
    // .LOOM PROJE KAYDETME DIYALOGU (Ctrl+S / Ctrl+Shift+S icin)
    public static string? SaveLoomDialog(string? currentFullPath, string defaultTitle)
    {
        string? selectedPath = null;

        Thread staThread = new Thread(() =>
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Projeyi Kaydet (.loom)";
            sfd.Filter = "Loom Project (*.loom)|*.loom";
            sfd.DefaultExt = "loom";
            sfd.AddExtension = true;

            try
            {
                if (!string.IsNullOrEmpty(currentFullPath) && currentFullPath.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.InitialDirectory = Path.GetDirectoryName(currentFullPath);
                    sfd.FileName = Path.GetFileName(currentFullPath);
                }
                else
                {
                    sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    sfd.FileName = defaultTitle.EndsWith(".loom", StringComparison.OrdinalIgnoreCase)
                        ? defaultTitle
                        : defaultTitle + ".loom";
                }
            }
            catch { }

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                selectedPath = sfd.FileName;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        return selectedPath;
    }

    // .LOOM PROJE VEYA GORSEL ACMA DIYALOGU (Ctrl+O icin)
    public static string? OpenProjectOrImageDialog()
    {
        string? selectedPath = null;
        Thread staThread = new Thread(() =>
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Proje veya Gorsel Ac";
            ofd.Filter = "Loom Project (*.loom)|*.loom|Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|Desteklenen Tum Dosyalar (*.loom;*.png;*.jpg;*.bmp)|*.loom;*.png;*.jpg;*.bmp";
            ofd.FilterIndex = 1; // Varsayilan olarak Loom Project (*.loom) secili gelir
            if (ofd.ShowDialog() == DialogResult.OK) selectedPath = ofd.FileName;
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return selectedPath;
    }

    // Aseprite [...] butonuna basildiginda PNG/JPG yolu almak icin (Mevcut Export ozelligi korundu)
    public static string? GetSavePathDialog(string currentFullPath)
    {
        string? selectedPath = null;

        Thread staThread = new Thread(() =>
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Nereye Kaydedilsin? (PNG / Sprite Sheet Export)";
            sfd.Filter = "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Tum Dosyalar (*.*)|*.*";
            sfd.DefaultExt = "png";
            
            try 
            {
                if (!string.IsNullOrEmpty(currentFullPath))
                {
                    sfd.InitialDirectory = Path.GetDirectoryName(currentFullPath);
                    sfd.FileName = Path.GetFileName(currentFullPath);
                }
            } 
            catch { }

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                selectedPath = sfd.FileName;
            }
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();

        return selectedPath;
    }

    // Export butonuna basildiginda belirtilen yola PNG/JPG kaydetmek icin (Mevcut Export ozelligi korundu)
    public static bool SaveExport(Canvas canvas, string path, int scale, bool exportAllFrames, bool sheetVertical)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            List<Frame> framesToExport = new();
            if (exportAllFrames)
            {
                foreach (var f in canvas.Frames) if (f.IsVisible) framesToExport.Add(f);
            }
            else
            {
                framesToExport.Add(canvas.Frames[canvas.ActiveFrameIndex]);
            }

            if (framesToExport.Count == 0) return false;

            int baseW = canvas.Width;
            int baseH = canvas.Height;
            int imgW = exportAllFrames ? (sheetVertical ? baseW : baseW * framesToExport.Count) : baseW;
            int imgH = exportAllFrames ? (sheetVertical ? baseH * framesToExport.Count : baseH) : baseH;

            int finalW = imgW * scale;
            int finalH = imgH * scale;

            Color bgColor = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ? Color.White : Canvas.TransparentColor;
            Image img = Raylib.GenImageColor(finalW, finalH, bgColor);

            for (int i = 0; i < framesToExport.Count; i++)
            {
                int offsetX = (exportAllFrames && !sheetVertical) ? i * baseW * scale : 0;
                int offsetY = (exportAllFrames && sheetVertical) ? i * baseH * scale : 0;

                var frame = framesToExport[i];

                for (int x = 0; x < baseW; x++)
                {
                    for (int y = 0; y < baseH; y++)
                    {
                        Color pixelColor = Canvas.TransparentColor;
                        foreach (var l in frame.Layers)
                        {
                            if (l.IsVisible && l.Pixels[x, y].A > 0)
                                pixelColor = l.Pixels[x, y];
                        }

                        if (pixelColor.A > 0)
                        {
                            if (scale == 1) Raylib.ImageDrawPixel(ref img, offsetX + x, offsetY + y, pixelColor);
                            else Raylib.ImageDrawRectangle(ref img, offsetX + x * scale, offsetY + y * scale, scale, scale, pixelColor);
                        }
                    }
                }
            }

            Raylib.ExportImage(img, path);
            Raylib.UnloadImage(img);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Geriye donuk uyumluluk icin korundu
    public static string? OpenImageDialog() => OpenProjectOrImageDialog();
}