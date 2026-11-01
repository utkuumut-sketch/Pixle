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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Verilen ProjectTab ve Palette verilerini .loom (ZIP + JSON + Binary) formatinda kaydeder.
    /// </summary>
    public static bool SaveProject(ProjectTab tab, Palette palette, string filePath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
        {
            errorMessage = "Gecersiz dosya yolu.";
            return false;
        }

        // Uzanti .loom degilse otomatik tamamla
        if (!filePath.EndsWith(".loom", StringComparison.OrdinalIgnoreCase))
        {
            filePath += ".loom";
        }

        // Atomik kayit icin gecici dosya yolu (Mevcut dosyayi bozmamak icin)
        string tempPath = filePath + ".tmp";

        try
        {
            // Eger gecici bir seçili alan (floating selection) varsa kaydetmeden once tuvale yerlestir
            tab.Selection.CancelOrCommit(tab.Canvas);

            Canvas canvas = tab.Canvas;

            // 1. JSON Metaveri modelini hazirla
            LoomProjectData projectData = new()
            {
                FormatVersion = CurrentFormatVersion,
                Width = canvas.Width,
                Height = canvas.Height,
                Fps = tab.Fps,
                ActiveFrameIndex = canvas.ActiveFrameIndex,
                ShowGrid = canvas.ShowGrid
            };

            // Palet renklerini HEX (#RRGGBBAA) olarak kaydet
            foreach (var col in palette.GetColors())
            {
                projectData.PaletteHex.Add($"{col.R:X2}{col.G:X2}{col.B:X2}{col.A:X2}");
            }

            if (File.Exists(tempPath))
                File.Delete(tempPath);

            // 2. ZIP arsivini olustur ve icine hem binary pikselleri hem JSON metaveriyi yaz
            using (FileStream fs = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (ZipArchive archive = new(fs, ZipArchiveMode.Create))
            {
                int w = canvas.Width;
                int h = canvas.Height;
                byte[] pixelBuffer = new byte[w * h * 4]; // Her piksel 4 byte (R, G, B, A)

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
                            Opacity = 255,
                            DataFile = binEntryName
                        });

                        // 2 Boyutlu Color[,] dizisini 1 boyutlu byte[] dizisine cevir
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

                        // Binary piksel dosyasini ZIP icine sikistirarak yaz
                        ZipArchiveEntry binEntry = archive.CreateEntry(binEntryName, CompressionLevel.Optimal);
                        using (Stream entryStream = binEntry.Open())
                        {
                            entryStream.Write(pixelBuffer, 0, pixelBuffer.Length);
                        }
                    }

                    projectData.Frames.Add(frameData);
                }

                // 3. project.json dosyasini ZIP icine yaz
                ZipArchiveEntry jsonEntry = archive.CreateEntry("project.json", CompressionLevel.Optimal);
                using (Stream jsonStream = jsonEntry.Open())
                {
                    JsonSerializer.Serialize(jsonStream, projectData, JsonOptions);
                }
            }

            // 4. Yazma %100 basarili olduysa gecici dosyayi asil dosyanin uzerine tasi
            File.Move(tempPath, filePath, overwrite: true);

            // Sekme bilgilerini guncelle
            tab.FilePath = filePath;
            tab.Title = Path.GetFileNameWithoutExtension(filePath);
            tab.IsDirty = false;

            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Kaydetme hatasi: {ex.Message}";
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
            return false;
        }
    }

    /// <summary>
    /// .loom dosyasini okur, dogrular ve basarili olursa yeni bir ProjectTab dondurur.
    /// Basarisiz olursa null dondurur ve mevcut acik projelere hicbir zarar vermez.
    /// </summary>
    public static ProjectTab? LoadProject(string filePath, out List<Color>? loadedPalette, out string errorMessage)
    {
        loadedPalette = null;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            errorMessage = "Dosya bulunamadi.";
            return null;
        }

        try
        {
            using FileStream fs = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using ZipArchive archive = new(fs, ZipArchiveMode.Read);

            // 1. project.json kontrolu ve okunmasi
            ZipArchiveEntry? jsonEntry = archive.GetEntry("project.json");
            if (jsonEntry == null)
            {
                errorMessage = "Bozuk .loom dosyasi: 'project.json' bulunamadi.";
                return null;
            }

            LoomProjectData? projectData;
            using (Stream jsonStream = jsonEntry.Open())
            {
                projectData = JsonSerializer.Deserialize<LoomProjectData>(jsonStream, JsonOptions);
            }

            if (projectData == null)
            {
                errorMessage = "Bozuk .loom dosyasi: Proje verisi okunamadi.";
                return null;
            }

            // 2. Surum ve boyut dogrulamasi
            if (projectData.FormatVersion <= 0 || projectData.FormatVersion > CurrentFormatVersion)
            {
                errorMessage = $"Desteklenmeyen .loom surumu: v{projectData.FormatVersion}";
                return null;
            }

            if (projectData.Width <= 0 || projectData.Width > 2048 || projectData.Height <= 0 || projectData.Height > 2048)
            {
                errorMessage = $"Gecersiz tuval boyutu: {projectData.Width}x{projectData.Height}";
                return null;
            }

            if (projectData.Frames == null || projectData.Frames.Count == 0)
            {
                errorMessage = "Bozuk .loom dosyasi: Hic animasyon karesi (frame) yok.";
                return null;
            }

            int w = projectData.Width;
            int h = projectData.Height;
            int expectedByteCount = w * h * 4;
            byte[] pixelBuffer = new byte[expectedByteCount];

            // 3. Once tum kare ve katmanlari bellekte olusturup dogrula (Hata cikarsa yarim sekme kalmasin)
            List<Frame> rebuiltFrames = new();

            for (int fIdx = 0; fIdx < projectData.Frames.Count; fIdx++)
            {
                LoomFrameData fData = projectData.Frames[fIdx];
                if (fData.Layers == null || fData.Layers.Count == 0)
                {
                    foreach (var rf in rebuiltFrames) rf.Dispose();
                    errorMessage = $"Bozuk .loom dosyasi: Kare {fIdx + 1} icinde katman yok.";
                    return null;
                }

                Frame newFrame = new()
                {
                    IsVisible = fData.IsVisible
                };

                for (int lIdx = 0; lIdx < fData.Layers.Count; lIdx++)
                {
                    LoomLayerData lData = fData.Layers[lIdx];
                    ZipArchiveEntry? binEntry = archive.GetEntry(lData.DataFile);

                    if (binEntry == null)
                    {
                        foreach (var rf in rebuiltFrames) rf.Dispose();
                        newFrame.Dispose();
                        errorMessage = $"Eksik piksel verisi: {lData.DataFile}";
                        return null;
                    }

                    // Binary akistan tam olarak expectedByteCount kadar oku
                    using (Stream binStream = binEntry.Open())
                    {
                        int totalRead = 0;
                        while (totalRead < expectedByteCount)
                        {
                            int read = binStream.Read(pixelBuffer, totalRead, expectedByteCount - totalRead);
                            if (read == 0) break;
                            totalRead += read;
                        }

                        if (totalRead != expectedByteCount)
                        {
                            foreach (var rf in rebuiltFrames) rf.Dispose();
                            newFrame.Dispose();
                            errorMessage = $"Bozuk piksel verisi boyutu: {lData.DataFile}";
                            return null;
                        }
                    }

                    string layerName = string.IsNullOrWhiteSpace(lData.Name) ? $"Katman {lIdx + 1}" : lData.Name;
                    Layer newLayer = new(layerName, w, h)
                    {
                        IsVisible = lData.IsVisible
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
                            newLayer.Pixels[x, y] = new Color(r, g, b, a);
                        }
                    }
                    newLayer.IsDirty = true;
                    newFrame.Layers.Add(newLayer);
                }

                newFrame.ActiveLayerIndex = Math.Clamp(fData.ActiveLayerIndex, 0, newFrame.Layers.Count - 1);
                newFrame.IsThumbnailDirty = true;
                rebuiltFrames.Add(newFrame);
            }

            // 4. Palet renklerini coz (Varsa)
            if (projectData.PaletteHex != null && projectData.PaletteHex.Count > 0)
            {
                List<Color> cols = new();
                foreach (string hex in projectData.PaletteHex)
                {
                    if (TryParseHexColor(hex, out Color parsedCol))
                        cols.Add(parsedCol);
                }
                if (cols.Count > 0)
                    loadedPalette = cols;
            }

            // 5. Yeni ProjectTab nesnesini olustur ve dogrulanmis kareleri icine yerlestir
            string title = Path.GetFileNameWithoutExtension(filePath);
            ProjectTab newTab = new(title, w, h, Canvas.TransparentColor, projectData.ShowGrid)
            {
                FilePath = filePath,
                Fps = Math.Clamp(projectData.Fps, 1f, 60f),
                IsDirty = false
            };

            // Varsayilan olusan ilk bos kareyi temizle ve dosyadan gelen kareleri ekle
            foreach (var defaultFrame in newTab.Canvas.Frames)
                defaultFrame.Dispose();
            newTab.Canvas.Frames.Clear();

            newTab.Canvas.Frames.AddRange(rebuiltFrames);
            newTab.Canvas.ActiveFrameIndex = Math.Clamp(projectData.ActiveFrameIndex, 0, newTab.Canvas.Frames.Count - 1);
            newTab.IsDirty = false;

            return newTab;
        }
        catch (InvalidDataException)
        {
            errorMessage = "Secilen dosya gecerli bir .loom (arsiv) dosyasi degil veya bozulmus.";
            return null;
        }
        catch (Exception ex)
        {
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