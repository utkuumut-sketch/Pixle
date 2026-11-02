// === DOSYA: \IO\Exporter.cs ===
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
    private const long MaxExportDimension = 16384;
    private const long MaxExportBytes = 256L * 1024L * 1024L;

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

    public static string? OpenProjectOrImageDialog()
    {
        string? selectedPath = null;
        Thread staThread = new Thread(() =>
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Proje veya Gorsel Ac";
            ofd.Filter = "Loom Project (*.loom)|*.loom|Image Files (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp|Desteklenen Tum Dosyalar (*.loom;*.png;*.jpg;*.bmp)|*.loom;*.png;*.jpg;*.bmp";
            ofd.FilterIndex = 1;
            if (ofd.ShowDialog() == DialogResult.OK) selectedPath = ofd.FileName;
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return selectedPath;
    }

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

    public static bool SaveExport(Canvas canvas, string path, int scale, bool exportAllFrames, bool sheetVertical)
    {
        if (string.IsNullOrWhiteSpace(path) || canvas == null || scale <= 0 || scale > 64)
            return false;

        List<Frame> framesToExport = new();
        if (exportAllFrames)
        {
            foreach (var f in canvas.Frames)
                if (f.IsVisible) framesToExport.Add(f);
        }
        else if (canvas.Frames.Count > 0)
        {
            framesToExport.Add(canvas.Frames[canvas.ActiveFrameIndex]);
        }

        if (framesToExport.Count == 0) return false;

        long baseW = canvas.Width;
        long baseH = canvas.Height;
        if (baseW <= 0 || baseH <= 0) return false;

        long imgW = exportAllFrames ? (sheetVertical ? baseW : baseW * framesToExport.Count) : baseW;
        long imgH = exportAllFrames ? (sheetVertical ? baseH * framesToExport.Count : baseH) : baseH;

        long finalWLong = imgW * scale;
        long finalHLong = imgH * scale;
        long totalBytes = finalWLong * finalHLong * 4L;

        if (finalWLong <= 0 || finalHLong <= 0 ||
            finalWLong > MaxExportDimension || finalHLong > MaxExportDimension ||
            totalBytes > MaxExportBytes)
        {
            return false;
        }

        int finalW = (int)finalWLong;
        int finalH = (int)finalHLong;

        bool isJpg = path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        Color bgColor = isJpg ? Color.White : Canvas.TransparentColor;

        Image img = default;
        bool imageCreated = false;

        try
        {
            img = Raylib.GenImageColor(finalW, finalH, bgColor);
            imageCreated = true;

            for (int i = 0; i < framesToExport.Count; i++)
            {
                int offsetX = (exportAllFrames && !sheetVertical) ? i * (int)baseW * scale : 0;
                int offsetY = (exportAllFrames && sheetVertical) ? i * (int)baseH * scale : 0;

                var frame = framesToExport[i];

                for (int x = 0; x < (int)baseW; x++)
                {
                    for (int y = 0; y < (int)baseH; y++)
                    {
                        Color pixelColor = bgColor;
                        foreach (var l in frame.Layers)
                        {
                            if (!l.IsVisible || l.Opacity == 0) continue;
                            Color c = l.Pixels[x, y];
                            if (c.A > 0)
                            {
                                if (l.Opacity < 255)
                                {
                                    c.A = (byte)((c.A * l.Opacity) / 255);
                                    if (c.A == 0) continue;
                                }
                                pixelColor = Canvas.BlendOver(pixelColor, c);
                            }
                        }

                        if (pixelColor.A > 0)
                        {
                            if (scale == 1)
                                Raylib.ImageDrawPixel(ref img, offsetX + x, offsetY + y, pixelColor);
                            else
                                Raylib.ImageDrawRectangle(ref img, offsetX + x * scale, offsetY + y * scale, scale, scale, pixelColor);
                        }
                    }
                }
            }

            return Raylib.ExportImage(img, path);
        }
        catch
        {
            return false;
        }
        finally
        {
            if (imageCreated)
            {
                Raylib.UnloadImage(img);
            }
        }
    }

    public static string? OpenImageDialog() => OpenProjectOrImageDialog();
}