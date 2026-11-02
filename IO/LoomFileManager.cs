// === DOSYA: \IO\LoomFileManager.cs ===
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using Raylib_cs;
using PixelArtApp.Core;
using PixelArtApp.UI;

namespace PixelArtApp.IO;

public static class LoomFileManager
{
    public const int CurrentFormatVersion = 1;

    private const int MaxCanvasDimension = 2048;
    private const int MaxFrames = 512;
    private const int MaxLayersPerFrame = 64;
    private const int MaxLayerNameLength = 64;
    private const long MaxJsonBytes = 4L * 1024L * 1024L; // 4 MB
    private const long MaxTotalProjectPixelBytes = 256L * 1024L * 1024L; // 256 MB

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static bool SaveProject(ProjectTab tab, Palette palette, string filePath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Gecersiz dosya yolu.";
            return false;
        }

        if (!filePath.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
        {
            filePath += ".loom";
        }

        string tempPath = filePath + ".tmp";

        try
        {
            tab.Selection.CancelOrCommit(tab.Canvas);
            Canvas canvas = tab.Canvas;

            LoomProjectData projectData = new()
            {
                FormatVersion = CurrentFormatVersion,
                Width = canvas.Width,
                Height = canvas.Height,
                Fps = tab.Fps,
                ActiveFrameIndex = canvas.ActiveFrameIndex,
                ShowGrid = canvas.ShowGrid
            };

            foreach (var col in palette.GetColors())
            {
                projectData.PaletteHex.Add($"{col.R:X2}{col.G:X2}{col.B:X2}{col.A:X2}");
            }

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            using (FileStream fs = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (ZipArchive archive = new(fs, ZipArchiveMode.Create))
            {
                int w = canvas.Width;
                int h = canvas.Height;
                byte[] pixelBuffer = new byte[w * h * 4];

                for (int fIdx = 0; fIdx < canvas.Frames.Count; fIdx++)
                {
                    Frame frame = canvas.Frames[fIdx];
                    LoomFrameData frameData = new()
                    {
                        ActiveLayerIndex = frame.ActiveLayerIndex,
                        IsVisible = frame.IsVisible
                    };

                    for (int lIdx = 0; lIdx < frame.Layers.Count; lIdx++)
                    {
                        Layer layer = frame.Layers[lIdx];
                        string binEntryName = $"pixels/f{fIdx}_l{lIdx}.bin";

                        frameData.Layers.Add(new LoomLayerData
                        {
                            Name = layer.Name,
                            IsVisible = layer.IsVisible,
                            Opacity = layer.Opacity,
                            DataFile = binEntryName
                        });

                        int offset = 0;
                        for (int y = 0; y < h; y++)
                        {
                            for (int x = 0; x < w; x++)
                            {
                                Color c = layer.Pixels[x, y];
                                pixelBuffer[offset++] = c.R;
                                pixelBuffer[offset++] = c.G;
                                pixelBuffer[offset++] = c.B;
                                pixelBuffer[offset++] = c.A;
                            }
                        }

                        ZipArchiveEntry binEntry = archive.CreateEntry(binEntryName, CompressionLevel.Optimal);
                        using (Stream entryStream = binEntry.Open())
                        {
                            entryStream.Write(pixelBuffer, 0, pixelBuffer.Length);
                        }
                    }

                    projectData.Frames.Add(frameData);
                }

                ZipArchiveEntry jsonEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
                using (Stream jsonStream = jsonEntry.Open())
                {
                    JsonSerializer.Serialize(jsonStream, projectData, JsonOptions);
                }
            }

            File.Move(tempPath, filePath, overwrite: true);

            tab.FilePath = filePath;
            tab.Title = Path.GetFileNameWithoutExtension(filePath);
            tab.MarkSaved();

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Kaydetme hatasi: {ex.Message}";
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }

    public static ProjectTab? LoadProject(string filePath, out List<Color>? loadedPalette, out string errorMessage)
    {
        loadedPalette = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = "Dosya bulunamadi.";
            return null;
        }

        List<Frame> rebuiltFrames = new();
        Frame? currentFrame = null;

        void CleanupAllocatedFrames()
        {
            currentFrame?.Dispose();
            currentFrame = null;
            foreach (var rf in rebuiltFrames) rf.Dispose();
            rebuiltFrames.Clear();
        }

        try
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipArchive archive = new(fs, ZipArchiveMode.Read);

            ZipArchiveEntry? jsonEntry = archive.GetEntry("project.json");
            if (jsonEntry == null)
            {
                errorMessage = "Bozuk .loom dosyasi: 'project.json' bulunamadi.";
                return null;
            }

            if (jsonEntry.Length <= 0 || jsonEntry.Length > MaxJsonBytes)
            {
                errorMessage = "Bozuk .loom dosyasi: 'project.json' boyutu gecersiz veya cok buyuk.";
                return null;
            }

            LoomProjectData? projectData;
            using (Stream jsonStream = jsonEntry.Open())
            using (MemoryStream ms = new())
            {
                byte[] chunk = new byte[8192];
                long totalJsonRead = 0;
                int r;
                while ((r = jsonStream.Read(chunk, 0, chunk.Length)) > 0)
                {
                    totalJsonRead += r;
                    if (totalJsonRead > MaxJsonBytes)
                    {
                        errorMessage = "Bozuk .loom dosyasi: JSON icerigi siniri asti.";
                        return null;
                    }
                    ms.Write(chunk, 0, r);
                }
                ms.Position = 0;
                projectData = JsonSerializer.Deserialize<LoomProjectData>(ms, JsonOptions);
            }

            if (projectData == null)
            {
                errorMessage = "Bozuk .loom dosyasi: Proje verisi okunamadi.";
                return null;
            }

            if (projectData.FormatVersion <= 0 || projectData.FormatVersion > CurrentFormatVersion)
            {
                errorMessage = $"Desteklenmeyen .loom surumu: v{projectData.FormatVersion}";
                return null;
            }

            if (projectData.Width <= 0 || projectData.Width > MaxCanvasDimension ||
                projectData.Height <= 0 || projectData.Height > MaxCanvasDimension)
            {
                errorMessage = $"Gecersiz tuval boyutu: {projectData.Width}x{projectData.Height}";
                return null;
            }

            if (projectData.Frames == null || projectData.Frames.Count == 0 || projectData.Frames.Count > MaxFrames)
            {
                errorMessage = $"Gecersiz animasyon karesi sayisi: {projectData.Frames?.Count ?? 0} (Maks: {MaxFrames}).";
                return null;
            }

            int w = projectData.Width;
            int h = projectData.Height;
            int expectedByteCount = w * h * 4;
            long totalAccumulatedBytes = 0;
            byte[] pixelBuffer = new byte[expectedByteCount];

            for (int fIdx = 0; fIdx < projectData.Frames.Count; fIdx++)
            {
                LoomFrameData? fData = projectData.Frames[fIdx];
                if (fData == null || fData.Layers == null || fData.Layers.Count == 0 || fData.Layers.Count > MaxLayersPerFrame)
                {
                    CleanupAllocatedFrames();
                    errorMessage = $"Gecersiz katman sayisi (Kare {fIdx + 1}).";
                    return null;
                }

                if (fData.ActiveLayerIndex < 0 || fData.ActiveLayerIndex >= fData.Layers.Count)
                {
                    CleanupAllocatedFrames();
                    errorMessage = $"Kare {fIdx + 1} icinde gecersiz aktif katman indeksi.";
                    return null;
                }

                currentFrame = new Frame
                {
                    IsVisible = fData.IsVisible
                };

                for (int lIdx = 0; lIdx < fData.Layers.Count; lIdx++)
                {
                    LoomLayerData? lData = fData.Layers[lIdx];

                    if (lData == null ||
                        string.IsNullOrWhiteSpace(lData.DataFile) ||
                        lData.DataFile.Contains("..") ||
                        Path.IsPathRooted(lData.DataFile))
                    {
                        CleanupAllocatedFrames();
                        errorMessage = $"Kare {fIdx + 1}, Katman {lIdx + 1} bilgisi gecersiz.";
                        return null;
                    }

                    totalAccumulatedBytes += expectedByteCount;
                    if (totalAccumulatedBytes > MaxTotalProjectPixelBytes)
                    {
                        CleanupAllocatedFrames();
                        errorMessage = "Proje bellek sinirini (256 MB) asiyor.";
                        return null;
                    }

                    ZipArchiveEntry? binEntry = archive.GetEntry(lData.DataFile);
                    if (binEntry == null)
                    {
                        CleanupAllocatedFrames();
                        errorMessage = $"Eksik piksel verisi: {lData.DataFile}";
                        return null;
                    }

                    if (binEntry.Length != expectedByteCount)
                    {
                        CleanupAllocatedFrames();
                        errorMessage = $"Bozuk piksel verisi boyutu: {lData.DataFile}";
                        return null;
                    }

                    using (Stream binStream = binEntry.Open())
                    {
                        int totalRead = 0;
                        while (totalRead < expectedByteCount)
                        {
                            int read = binStream.Read(pixelBuffer, totalRead, expectedByteCount - totalRead);
                            if (read == 0) break;
                            totalRead += read;
                        }

                        if (totalRead != expectedByteCount || binStream.ReadByte() != -1)
                        {
                            CleanupAllocatedFrames();
                            errorMessage = $"Bozuk binary piksel akisi: {lData.DataFile}";
                            return null;
                        }
                    }

                    string rawName = string.IsNullOrWhiteSpace(lData.Name) ? $"Katman {lIdx + 1}" : lData.Name.Trim();
                    if (rawName.Length > MaxLayerNameLength) rawName = rawName[..MaxLayerNameLength];

                    Layer newLayer = new(rawName, w, h)
                    {
                        IsVisible = lData.IsVisible,
                        Opacity = lData.Opacity
                    };

                    int offset = 0;
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            byte r = pixelBuffer[offset++];
                            byte g = pixelBuffer[offset++];
                            byte b = pixelBuffer[offset++];
                            byte a = pixelBuffer[offset++];
                            newLayer.SetPixelDirect(x, y, new Color(r, g, b, a));
                        }
                    }
                    newLayer.IsDirty = true;
                    currentFrame.Layers.Add(newLayer);
                }

                currentFrame.ActiveLayerIndex = fData.ActiveLayerIndex;
                currentFrame.IsThumbnailDirty = true;
                rebuiltFrames.Add(currentFrame);
                currentFrame = null;
            }

            if (projectData.PaletteHex != null && projectData.PaletteHex.Count > 0)
            {
                List<Color> cols = new();
                int maxPalette = Math.Min(projectData.PaletteHex.Count, 256);
                for (int i = 0; i < maxPalette; i++)
                {
                    if (TryParseHexColor(projectData.PaletteHex[i], out Color parsedCol))
                        cols.Add(parsedCol);
                }
                if (cols.Count > 0)
                    loadedPalette = cols;
            }

            string title = Path.GetFileNameWithoutExtension(filePath);
            ProjectTab newTab = new(title, w, h, Canvas.TransparentColor, projectData.ShowGrid)
            {
                FilePath = filePath,
                Fps = float.IsFinite(projectData.Fps) ? Math.Clamp(projectData.Fps, 1f, 60f) : 8f
            };

            newTab.Canvas.ReplaceFrames(rebuiltFrames, projectData.ActiveFrameIndex);
            rebuiltFrames.Clear();
            newTab.MarkSaved();

            return newTab;
        }
        catch (InvalidDataException)
        {
            CleanupAllocatedFrames();
            errorMessage = "Secilen dosya gecerli bir .loom (arsiv) dosyasi degil veya bozulmus.";
            return null;
        }
        catch (Exception ex)
        {
            CleanupAllocatedFrames();
            errorMessage = $"Dosya acilirken hata olustu: {ex.Message}";
            return null;
        }
    }

    private static bool TryParseHexColor(string hex, out Color color)
    {
        color = Color.Black;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        hex = hex.Trim().TrimStart('#');

        if (hex.Length == 6 && uint.TryParse(hex, NumberStyles.HexNumber, null, out uint rgb))
        {
            color = new Color((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), (byte)255);
            return true;
        }
        if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, null, out uint rgba))
        {
            color = new Color((byte)((rgba >> 24) & 0xFF), (byte)((rgba >> 16) & 0xFF), (byte)((rgba >> 8) & 0xFF), (byte)(rgba & 0xFF));
            return true;
        }
        return false;
    }
}