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
                string? dir = !string.IsNullOrEmpty(currentFullPath) ? Path.GetDirectoryName(currentFullPath) : null;
                sfd.InitialDirectory = (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    ? dir
                    : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (!string.IsNullOrEmpty(currentFullPath) && currentFullPath.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
                {
                    sfd.FileName = Path.GetFileName(currentFullPath);
                }
                else
                {
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
            ofd.Filter = "Desteklenen Tum Dosyalar (*.loom;*.png;*.jpg;*.jpeg;*.bmp)|*.loom;*.png;*.jpg;*.jpeg;*.bmp|Loom Project (*.loom)|*.loom|Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";
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
            sfd.Title = "Nereye Kaydedilsin? (PNG / GIF / Sprite Sheet Export)";
            sfd.Filter = "PNG Image (*.png)|*.png|Animated GIF (*.gif)|*.gif|JPEG Image (*.jpg)|*.jpg|Tum Dosyalar (*.*)|*.*";
            sfd.AddExtension = true;

            if (currentFullPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase))
            {
                sfd.FilterIndex = 2;
                sfd.DefaultExt = "gif";
            }
            else if (currentFullPath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || currentFullPath.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                sfd.FilterIndex = 3;
                sfd.DefaultExt = "jpg";
            }
            else
            {
                sfd.FilterIndex = 1;
                sfd.DefaultExt = "png";
            }

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

    public static bool SaveExport(Canvas canvas, string path, int scale, bool exportAllFrames, bool sheetVertical, float fps = 8f)
    {
        if (string.IsNullOrWhiteSpace(path) || canvas == null || scale <= 0 || scale > 64)
            return false;

        bool isGif = path.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);

        List<Frame> framesToExport = new();
        if (exportAllFrames || isGif)
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

        if (isGif)
        {
            long gifWLong = baseW * scale;
            long gifHLong = baseH * scale;
            long gifFrameBytes = gifWLong * gifHLong * 4L;
            if (gifWLong <= 0 || gifHLong <= 0 || gifWLong > 4096 || gifHLong > 4096 || gifFrameBytes * framesToExport.Count > MaxExportBytes)
                return false;

            int gifW = (int)gifWLong;
            int gifH = (int)gifHLong;
            List<Color[]> gifFrames = new(framesToExport.Count);

            for (int i = 0; i < framesToExport.Count; i++)
            {
                var frame = framesToExport[i];
                Color[] framePixels = new Color[gifW * gifH];

                for (int y = 0; y < (int)baseH; y++)
                {
                    for (int x = 0; x < (int)baseW; x++)
                    {
                        Color pixelColor = Canvas.TransparentColor;
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

                        if (scale == 1)
                        {
                            framePixels[y * gifW + x] = pixelColor;
                        }
                        else
                        {
                            int startPx = x * scale;
                            int startPy = y * scale;
                            for (int sy = 0; sy < scale; sy++)
                            {
                                int rowOffset = (startPy + sy) * gifW + startPx;
                                for (int sx = 0; sx < scale; sx++)
                                {
                                    framePixels[rowOffset + sx] = pixelColor;
                                }
                            }
                        }
                    }
                }

                gifFrames.Add(framePixels);
            }

            try
            {
                return GifEncoder.SaveAnimatedGif(path, gifW, gifH, gifFrames, fps);
            }
            catch
            {
                return false;
            }
        }

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

    public static string? OpenImageDialog()
    {
        string? selectedPath = null;
        Thread staThread = new Thread(() =>
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Referans Gorsel Sec";
            ofd.Filter = "Gorsel Dosyalari (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|Tum Dosyalar (*.*)|*.*";
            ofd.FilterIndex = 1;
            if (ofd.ShowDialog() == DialogResult.OK) selectedPath = ofd.FileName;
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return selectedPath;
    }

    public static string? OpenPaletteDialog()
    {
        string? selectedPath = null;
        Thread staThread = new Thread(() =>
        {
            using OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Lospec / Palet Dosyasi Ice Aktar (.hex, .pal, .gpl)";
            ofd.Filter = "Palet Dosyalari (*.hex;*.pal;*.gpl;*.txt)|*.hex;*.pal;*.gpl;*.txt|Lospec HEX (*.hex)|*.hex|JASC Palette (*.pal)|*.pal|GIMP Palette (*.gpl)|*.gpl|Tum Dosyalar (*.*)|*.*";
            ofd.FilterIndex = 1;
            if (ofd.ShowDialog() == DialogResult.OK) selectedPath = ofd.FileName;
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return selectedPath;
    }

    public static string? SavePaletteDialog(string defaultName = "palet.hex")
    {
        string? selectedPath = null;
        Thread staThread = new Thread(() =>
        {
            using SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Paleti Disa Aktar (.hex)";
            sfd.Filter = "Lospec HEX Palette (*.hex)|*.hex";
            sfd.DefaultExt = "hex";
            sfd.AddExtension = true;
            sfd.FileName = defaultName.EndsWith(".hex", StringComparison.OrdinalIgnoreCase) ? defaultName : defaultName + ".hex";
            if (sfd.ShowDialog() == DialogResult.OK) selectedPath = sfd.FileName;
        });

        staThread.SetApartmentState(ApartmentState.STA);
        staThread.Start();
        staThread.Join();
        return selectedPath;
    }
}
