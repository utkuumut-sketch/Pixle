using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

/// <summary>
/// Aseprite tarzinda dogrudan 32x32 tam cozunurluklu, 1px net konturlu piksel sanat ikonlari uretir.
/// </summary>
public static class PixelIcons
{
    private static bool isLoaded = false;
    private static readonly Dictionary<string, Texture2D> textures = new();
    private static Image windowIconImage;
    private static bool hasWindowIcon = false;

    private static Color C(int r, int g, int b, int a = 255) => new Color((byte)r, (byte)g, (byte)b, (byte)a);

    private static readonly Color Outline     = C(16, 18, 24);
    private static readonly Color Shadow      = C(0, 0, 0, 75);
    private static readonly Color White       = C(250, 252, 255);
    private static readonly Color SilverLight = C(218, 226, 238);
    private static readonly Color SilverMid   = C(158, 170, 188);
    private static readonly Color SilverDark  = C(98, 108, 126);

    private static readonly Color GoldLight   = C(255, 232, 136);
    private static readonly Color GoldMid     = C(248, 198, 76);
    private static readonly Color GoldDark    = C(204, 136, 38);

    private static readonly Color WoodLight   = C(244, 214, 176);
    private static readonly Color WoodDark    = C(198, 152, 108);

    private static readonly Color PinkLight   = C(255, 162, 192);
    private static readonly Color PinkMid     = C(242, 112, 154);
    private static readonly Color PinkDark    = C(192, 68, 108);

    private static readonly Color BlueLight   = C(142, 224, 255);
    private static readonly Color BlueMid     = C(72, 176, 248);
    private static readonly Color BlueDark    = C(38, 118, 198);

    private static readonly Color MintLight   = C(152, 244, 192);
    private static readonly Color MintMid     = C(82, 208, 142);
    private static readonly Color MintDark    = C(42, 148, 94);

    public static void LoadIcons()
    {
        if (isLoaded) return;

        Register("app_logo", DrawModernAppLogo);

        // 13 Ana Cizim Araci
        Register("tool_pen", DrawPenIcon);
        Register("tool_eraser", DrawEraserIcon);
        Register("tool_dither", DrawDitherToolIcon);
        Register("tool_shading", DrawShadingToolIcon);
        Register("tool_bucket", (ref Image img) => DrawBucketIcon(ref img, smart: false));
        Register("tool_smartbucket", (ref Image img) => DrawBucketIcon(ref img, smart: true));
        Register("tool_eyedropper", DrawEyedropperIcon);
        Register("tool_blur", DrawBlurIcon);
        Register("tool_jumble", DrawJumbleIcon);
        Register("tool_line", DrawLineIcon);
        Register("tool_curve", DrawCurveIcon);
        Register("tool_shape", DrawShapeToolIcon);
        Register("tool_select", DrawSelectToolIcon);

        // 2 Alt Shading İkonu
        Register("shading_palette", (ref Image img) => DrawShadingModeIcon(ref img, paletteMode: true));
        Register("shading_smooth", (ref Image img) => DrawShadingModeIcon(ref img, paletteMode: false));

        // 6 Alt Dither Desen İkonu
        Register("dither_50", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.Checker50));
        Register("dither_25", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.Sparse25));
        Register("dither_75", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.Dense75));
        Register("dither_vert", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.VerticalLines));
        Register("dither_horiz", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.HorizontalLines));
        Register("dither_diag", (ref Image img) => DrawDitherPatternIcon(ref img, DitherPattern.DiagonalLines));

        // 6 Alt Sekil İkonu
        Register("shape_rect_out", (ref Image img) => DrawRectShapeIcon(ref img, filled: false));
        Register("shape_rect_fill", (ref Image img) => DrawRectShapeIcon(ref img, filled: true));
        Register("shape_circle_out", (ref Image img) => DrawCircleShapeIcon(ref img, filled: false));
        Register("shape_circle_fill", (ref Image img) => DrawCircleShapeIcon(ref img, filled: true));
        Register("shape_tri_out", (ref Image img) => DrawTriangleShapeIcon(ref img, filled: false));
        Register("shape_tri_fill", (ref Image img) => DrawTriangleShapeIcon(ref img, filled: true));

        // 3 Alt Secim İkonu
        Register("sel_rect", DrawSelRectIcon);
        Register("sel_lasso", DrawSelLassoIcon);
        Register("sel_wand", DrawSelWandIcon);

        // Yardimci İkonlar
        Register("assist_pixelperfect", DrawPixelPerfectIcon);
        Register("assist_symmetry", DrawSymmetryIcon);
        Register("assist_alphalock", DrawAlphaLockIcon);
        Register("assist_tilewrap", DrawTileWrapIcon);
        Register("assist_grid", DrawGridIcon);
        Register("assist_onion", DrawOnionIcon);

        BuildWindowIcon();
        isLoaded = true;
    }

    private delegate void IconBuilder(ref Image img);

    private static void Register(string key, IconBuilder builder)
    {
        string customPngPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", $"{key}.png");
        if (File.Exists(customPngPath))
        {
            Texture2D customTex = Raylib.LoadTexture(customPngPath);
            Raylib.SetTextureFilter(customTex, TextureFilter.Point);
            textures[key] = customTex;
            return;
        }

        Image img = Raylib.GenImageColor(32, 32, C(0, 0, 0, 0));
        builder(ref img);

        Texture2D tex = Raylib.LoadTextureFromImage(img);
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        Raylib.UnloadImage(img);
        textures[key] = tex;
    }

    private static void P(ref Image img, int x, int y, Color col)
    {
        if (x >= 0 && x < 32 && y >= 0 && y < 32)
            Raylib.ImageDrawPixel(ref img, x, y, col);
    }

    private static void R(ref Image img, int x, int y, int w, int h, Color col)
    {
        Raylib.ImageDrawRectangle(ref img, x, y, w, h, col);
    }

    private static void Line(ref Image img, int x0, int y0, int x1, int y1, Color col)
    {
        foreach (var (px, py) in ToolManager.GetLinePixels(x0, y0, x1, y1))
            P(ref img, px, py, col);
    }

    private static void Sparkle(ref Image img, int cx, int cy, Color outer, Color center)
    {
        P(ref img, cx, cy - 2, outer);
        P(ref img, cx, cy - 1, outer);
        P(ref img, cx - 2, cy, outer);
        P(ref img, cx - 1, cy, outer);
        P(ref img, cx + 1, cy, outer);
        P(ref img, cx + 2, cy, outer);
        P(ref img, cx, cy + 1, outer);
        P(ref img, cx, cy + 2, outer);
        P(ref img, cx, cy, center);
    }

    // =========================================================================
    // 0. UYGULAMA LOGOSU: Modern, Sade ve Sik Piksel Sanati Amblemi
    // =========================================================================
    private static void DrawModernAppLogo(ref Image img)
    {
        // Yumusak alt golge
        for (int y = 4; y <= 27; y++)
            for (int x = 4; x <= 27; x++)
                if (!IsCornerCut(x - 4, y - 4, 24, 24))
                    P(ref img, x + 1, y + 1, Shadow);

        // Modern yuvarlatilmis koyu arduvaz kart govdesi (4..27 -> 24x24)
        Color cardTop = C(44, 50, 66);
        Color cardMid = C(32, 36, 48);
        Color cardBot = C(22, 25, 34);
        Color innerRim = C(68, 78, 102);

        for (int y = 4; y <= 27; y++)
        {
            for (int x = 4; x <= 27; x++)
            {
                int lx = x - 4, ly = y - 4;
                if (IsCornerCut(lx, ly, 24, 24)) continue;

                bool isBorder = lx == 0 || lx == 23 || ly == 0 || ly == 23 ||
                                IsCornerCut(lx - 1, ly, 24, 24) || IsCornerCut(lx + 1, ly, 24, 24) ||
                                IsCornerCut(lx, ly - 1, 24, 24) || IsCornerCut(lx, ly + 1, 24, 24);

                if (isBorder)
                {
                    P(ref img, x, y, Outline);
                }
                else
                {
                    Color fill = ly < 7 ? cardTop : (ly > 17 ? cardBot : cardMid);
                    if ((lx == 1 && ly >= 3 && ly <= 20) || (ly == 1 && lx >= 3 && lx <= 20))
                        fill = innerRim;
                    P(ref img, x, y, fill);
                }
            }
        }

        // Merkezde 2x2 modern renk prizmasi (Piksel Sanati Karosu)
        // Sol-Ust: Turkuaz / Mavi
        DrawMiniTile(ref img, 8, 8, 7, 7, BlueLight, BlueMid, BlueDark);
        // Sag-Ust: Mercan / Pembe
        DrawMiniTile(ref img, 17, 8, 7, 7, PinkLight, PinkMid, PinkDark);
        // Sol-Alt: Zumrut / Mint
        DrawMiniTile(ref img, 8, 17, 7, 7, MintLight, MintMid, MintDark);
        // Sag-Alt: Altin Sarisi (Hafif 1px disari kaydirilmis dinamik "aktif piksel" vurgusu)
        DrawMiniTile(ref img, 18, 18, 7, 7, GoldLight, GoldMid, GoldDark);

        // Merkez kesisim noktasinda parlak elmas isiltisi
        P(ref img, 15, 15, White);
        P(ref img, 16, 15, White);
        P(ref img, 15, 16, White);
        P(ref img, 16, 16, White);
    }

    private static void DrawMiniTile(ref Image img, int x, int y, int w, int h, Color light, Color mid, Color dark)
    {
        R(ref img, x - 1, y - 1, w + 2, h + 2, Outline);
        R(ref img, x, y, w, h, mid);
        R(ref img, x, y, w, 2, light);
        R(ref img, x, y, 2, h, light);
        R(ref img, x, y + h - 1, w, 1, dark);
        R(ref img, x + w - 1, y, 1, h, dark);
        P(ref img, x + 1, y + 1, White);
    }

    private static bool IsCornerCut(int lx, int ly, int w, int h)
    {
        if (lx < 0 || lx >= w || ly < 0 || ly >= h) return true;
        if ((lx == 0 || lx == w - 1) && (ly <= 1 || ly >= h - 2)) return true;
        if ((ly == 0 || ly == h - 1) && (lx <= 1 || lx >= w - 2)) return true;
        return false;
    }

    private static void BuildWindowIcon()
    {
        if (hasWindowIcon) return;
        windowIconImage = Raylib.GenImageColor(32, 32, C(0, 0, 0, 0));
        DrawModernAppLogo(ref windowIconImage);
        Raylib.SetWindowIcon(windowIconImage);
        hasWindowIcon = true;
    }

    // =========================================================================
    // 1. KALEM (Pen) - Net 45 derece piksel kalem
    // =========================================================================
    private static void DrawPenIcon(ref Image img)
    {
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                // s: sol-alt (uc) -> sag-ust (silgi) ekseni
                int s = (x - 5) + (26 - y);
                // d: kalem kalinlik ekseni
                int d = (x - 5) - (26 - y);

                if (s < 0 || s > 34) continue;

                int halfW = s < 8 ? (s / 2) : 4;
                if (s > 32 && Math.Abs(d) >= 4) continue;

                if (Math.Abs(d) == halfW + 1 || (s == 34 && Math.Abs(d) <= 3) || (s == 0 && d == 0))
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else if (Math.Abs(d) <= halfW)
                {
                    Color c;
                    if (s <= 3) c = Outline; // Grafit kursun ucu
                    else if (s <= 8) c = d < 0 ? WoodLight : WoodDark; // Ahsap koni
                    else if (s == 9) c = Outline;
                    else if (s <= 24) // Sari kalem govdesi
                    {
                        c = d <= -2 ? GoldLight : (d >= 2 ? GoldDark : GoldMid);
                    }
                    else if (s == 25 || s == 29) c = Outline;
                    else if (s <= 28) // Gumus metal halka
                    {
                        c = d < 0 ? SilverLight : (d > 1 ? SilverDark : SilverMid);
                    }
                    else // Pembe tepe silgisi
                    {
                        c = d <= -1 ? PinkLight : (d >= 2 ? PinkDark : PinkMid);
                    }
                    P(ref img, x, y, c);
                }
            }
        }
    }

    // =========================================================================
    // 2. SILGI (Eraser) - Iki renkli (mavi-pembe) net blok silgi
    // =========================================================================
    private static void DrawEraserIcon(ref Image img)
    {
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int s = (x - 6) + (24 - y);
                int d = (x - 6) - (24 - y);

                if (s < 0 || s > 28 || Math.Abs(d) > 7) continue;

                // Koseleri 1px yuvarlat
                if ((s == 0 || s == 28) && Math.Abs(d) >= 6) continue;

                bool isBorder = s == 0 || s == 28 || Math.Abs(d) == 7 ||
                                ((s == 1 || s == 27) && Math.Abs(d) == 6);

                if (isBorder)
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else if (s == 13)
                {
                    P(ref img, x, y, Outline);
                }
                else if (s < 13) // Sol-alt mavi kilif
                {
                    Color c = d <= -3 ? BlueLight : (d >= 3 ? BlueDark : BlueMid);
                    if (d == -5 && s >= 3 && s <= 10) c = White;
                    P(ref img, x, y, c);
                }
                else // Sag-ust pembe silgi kismi
                {
                    Color c = d <= -3 ? PinkLight : (d >= 3 ? PinkDark : PinkMid);
                    if (d == -5 && s >= 16 && s <= 24) c = White;
                    P(ref img, x, y, c);
                }
            }
        }

        // Alt kisimda minik silgi kirintilari
        R(ref img, 21, 25, 3, 2, PinkMid);
        R(ref img, 25, 25, 2, 2, PinkLight);
    }

    // =========================================================================
    // 3 & 4. BOYA KOVASI & AKILLI KOVA (Bucket & Smart Bucket)
    // =========================================================================
    private static void DrawBucketIcon(ref Image img, bool smart)
    {
        Color paintLight = smart ? MintLight : BlueLight;
        Color paintMid   = smart ? MintMid   : BlueMid;
        Color paintDark  = smart ? MintDark  : BlueDark;

        // Kova tel sapı (üst yay)
        Line(ref img, 13, 8, 17, 4, SilverLight);
        Line(ref img, 17, 4, 22, 4, SilverLight);
        Line(ref img, 22, 4, 25, 8, SilverMid);

        // Egik metal kova govdesi
        for (int y = 7; y <= 24; y++)
        {
            for (int x = 8; x <= 27; x++)
            {
                int u = (x - 18) + (y - 15);
                int v = (x - 18) - (y - 15);

                if (u < -8 || u > 8) continue;
                int halfW = 6 - (u + 8) / 6;
                if (Math.Abs(v) > halfW + 1) continue;

                if (u == -8 || u == 8 || Math.Abs(v) == halfW + 1)
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else
                {
                    Color c = v <= -2 ? SilverLight : (v >= 2 ? SilverDark : SilverMid);
                    if (v == -3 && u >= -5 && u <= 4) c = White;
                    P(ref img, x, y, c);
                }
            }
        }

        // Kovanin agzindan dokulen boya akisi ve damlasi
        for (int y = 13; y <= 27; y++)
        {
            int centerX = 9 - (y - 13) / 3;
            int w = y < 22 ? 3 : (y < 25 ? 2 : 1);
            for (int dx = -w; dx <= w; dx++)
            {
                int px = centerX + dx;
                if (dx == -w || dx == w || y == 13 || y == 27)
                    P(ref img, px, y, Outline);
                else
                    P(ref img, px, y, dx < 0 ? paintLight : (dx > 0 ? paintDark : paintMid));
            }
        }

        if (smart)
        {
            Sparkle(ref img, 6, 6, GoldMid, White);
            Sparkle(ref img, 26, 24, GoldMid, White);
        }
    }

    // =========================================================================
    // 5. DAMLALIK (Eyedropper)
    // =========================================================================
    private static void DrawEyedropperIcon(ref Image img)
    {
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                int s = (x - 5) + (26 - y);
                int d = (x - 5) - (26 - y);

                if (s < 0 || s > 34) continue;

                int halfW;
                if (s <= 4) halfW = s / 2;
                else if (s <= 20) halfW = 3;
                else if (s <= 23) halfW = 5; // Yaka halkasi
                else halfW = 4;              // Kaucuk tepe pompasi

                if (s >= 33 && Math.Abs(d) >= 3) continue;

                if (Math.Abs(d) == halfW + 1 || (s == 0 && d == 0) || (s == 34 && Math.Abs(d) <= 2))
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else if (Math.Abs(d) <= halfW)
                {
                    if (s == 20 || s == 23)
                    {
                        P(ref img, x, y, Outline);
                    }
                    else if (s > 23) // Pembe kaucuk pompa
                    {
                        Color c = d < 0 ? PinkLight : (d > 1 ? PinkDark : PinkMid);
                        if (d == -2 && s >= 26 && s <= 31) c = White;
                        P(ref img, x, y, c);
                    }
                    else if (s > 20) // Metal halka
                    {
                        P(ref img, x, y, d < 0 ? SilverLight : SilverMid);
                    }
                    else if (s <= 12) // Cam tup icindeki mavi sivi
                    {
                        Color c = d < 0 ? BlueLight : (d > 0 ? BlueDark : BlueMid);
                        if (d == -2) c = White;
                        P(ref img, x, y, c);
                    }
                    else // Cam tup ust bosluk
                    {
                        P(ref img, x, y, d < 0 ? White : SilverLight);
                    }
                }
            }
        }

        // Uctaki renk damlasi
        R(ref img, 4, 27, 2, 3, BlueMid);
        P(ref img, 4, 27, BlueLight);
    }

    // =========================================================================
    // 6. BULANIKLASTIR (Blur) - Simetrik parlak su damlasi
    // =========================================================================
    private static void DrawBlurIcon(ref Image img)
    {
        for (int y = 4; y <= 27; y++)
        {
            double t = (y - 4) / 23.0;
            double halfW = t < 0.62
                ? (1.0 + t * 14.5)
                : Math.Sqrt(Math.Max(0, 1.0 - Math.Pow((t - 0.62) / 0.38, 2))) * 10.0;

            int minX = (int)Math.Round(15.5 - halfW);
            int maxX = (int)Math.Round(15.5 + halfW);

            for (int x = minX; x <= maxX; x++)
            {
                bool isBorder = x == minX || x == maxX || y == 4 || y == 27;
                if (isBorder)
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else
                {
                    double nx = (x - 15.5) / Math.Max(1.0, halfW);
                    Color c = (nx < -0.25 && t < 0.75) ? BlueLight : (nx > 0.3 || t > 0.82 ? BlueDark : BlueMid);
                    P(ref img, x, y, c);
                }
            }
        }

        // Damla uzerindeki parlak beyaz isik yansimasi
        Line(ref img, 12, 11, 10, 16, White);
        Line(ref img, 13, 12, 11, 17, White);
        P(ref img, 11, 20, White);
    }

    // =========================================================================
    // 7. KARISTIR (Jumble) - 2x2 Renkli Piksel Karistirma Bloklari
    // =========================================================================
    private static void DrawJumbleIcon(ref Image img)
    {
        DrawTileBlock(ref img, 5, 5, 10, 10, GoldLight, GoldMid, GoldDark);
        DrawTileBlock(ref img, 17, 5, 10, 10, BlueLight, BlueMid, BlueDark);
        DrawTileBlock(ref img, 5, 17, 10, 10, PinkLight, PinkMid, PinkDark);
        DrawTileBlock(ref img, 17, 17, 10, 10, MintLight, MintMid, MintDark);
    }

    private static void DrawTileBlock(ref Image img, int x, int y, int w, int h, Color light, Color mid, Color dark)
    {
        R(ref img, x + 1, y + 1, w, h, Shadow);
        R(ref img, x, y, w, h, Outline);
        R(ref img, x + 1, y + 1, w - 2, h - 2, mid);
        R(ref img, x + 1, y + 1, w - 2, 2, light);
        R(ref img, x + 1, y + 1, 2, h - 2, light);
        R(ref img, x + 1, y + h - 3, w - 2, 2, dark);
        R(ref img, x + w - 3, y + 1, 2, h - 2, dark);
        R(ref img, x + 2, y + 2, 2, 2, White);
    }

    // =========================================================================
    // 8. CIZGI ARACI (Line) - Iki ucu tutamacli diyagonal cizgi
    // =========================================================================
    private static void DrawLineIcon(ref Image img)
    {
        // Kalin diyagonal cizgi (koyu kontur + beyaz ic)
        for (int d = -2; d <= 2; d++)
        {
            Color col = Math.Abs(d) == 2 ? Outline : (d <= 0 ? White : SilverLight);
            Line(ref img, 7 + d, 24, 24 + d, 7, col);
        }

        // Sol-alt ve sag-ust 7x7 kare tutamaclar
        DrawTileBlock(ref img, 4, 21, 7, 7, MintLight, MintMid, MintDark);
        DrawTileBlock(ref img, 21, 4, 7, 7, MintLight, MintMid, MintDark);
    }

    // =========================================================================
    // 9. SEKIL ARACI (Shapes) - Arkada mavi cerceve kare, onde altin dolu daire
    // =========================================================================
    private static void DrawShapeToolIcon(ref Image img)
    {
        // Arkadaki ici bos mavi kare (4, 4) -> (19, 19)
        R(ref img, 4, 4, 16, 16, Outline);
        R(ref img, 5, 5, 14, 14, BlueLight);
        R(ref img, 7, 7, 10, 10, Outline);
        R(ref img, 8, 8, 8, 8, C(0, 0, 0, 0));

        // Ondeki altin dolu daire (merkez: 20, 20, yaricap: 7.5)
        DrawFilledCircleInternal(ref img, 20.0, 20.0, 7.6, GoldLight, GoldMid, GoldDark);
    }

    // =========================================================================
    // 10. SEC VE TASI (Select & Move) - Kesikli secim cercevesi + imlec oku
    // =========================================================================
    private static void DrawSelectToolIcon(ref Image img)
    {
        // Kesikli secim kutusu (4, 4) -> (23, 22)
        DrawDashedBox(ref img, 4, 4, 20, 19);

        // Sag-alt kosede klasik beyaz piksel fare imleci oku
        int ox = 16, oy = 14;
        for (int dy = 0; dy <= 12; dy++)
        {
            int maxDx = dy <= 8 ? dy : (dy - 6);
            for (int dx = 0; dx <= maxDx; dx++)
            {
                bool border = dx == 0 || dy == 12 || (dy <= 8 && dx == dy) || (dy == 8 && dx >= 4) || (dy > 8 && dx == maxDx);
                P(ref img, ox + dx, oy + dy, border ? Outline : White);
            }
        }
    }

    private static void DrawDashedBox(ref Image img, int x, int y, int w, int h)
    {
        for (int i = 0; i < w; i++)
        {
            if ((i / 3) % 2 == 0)
            {
                R(ref img, x + i, y, 1, 2, White);
                P(ref img, x + i, y - 1, Outline);
                P(ref img, x + i, y + 2, Outline);

                R(ref img, x + i, y + h - 2, 1, 2, White);
                P(ref img, x + i, y + h - 3, Outline);
                P(ref img, x + i, y + h, Outline);
            }
        }

        for (int j = 0; j < h; j++)
        {
            if ((j / 3) % 2 == 0)
            {
                R(ref img, x, y + j, 2, 1, White);
                P(ref img, x - 1, y + j, Outline);
                P(ref img, x + 2, y + j, Outline);

                R(ref img, x + w - 2, y + j, 2, 1, White);
                P(ref img, x + w - 3, y + j, Outline);
                P(ref img, x + w, y + j, Outline);
            }
        }
    }

    // =========================================================================
    // ALT SEKIL IKONLARI (Kare, Cember, Ucgen - Cerceve & Dolu)
    // =========================================================================
    private static void DrawRectShapeIcon(ref Image img, bool filled)
    {
        R(ref img, 6, 7, 22, 20, Shadow);
        if (filled)
        {
            R(ref img, 5, 6, 22, 20, Outline);
            R(ref img, 6, 7, 20, 18, MintMid);
            R(ref img, 6, 7, 20, 2, MintLight);
            R(ref img, 6, 7, 2, 18, MintLight);
            R(ref img, 6, 23, 20, 2, MintDark);
            R(ref img, 24, 7, 2, 18, MintDark);
            R(ref img, 7, 8, 3, 2, White);
        }
        else
        {
            R(ref img, 5, 6, 22, 20, Outline);
            R(ref img, 6, 7, 20, 18, White);
            R(ref img, 8, 9, 16, 14, Outline);
            R(ref img, 9, 10, 14, 12, C(0, 0, 0, 0));
        }
    }

    private static void DrawCircleShapeIcon(ref Image img, bool filled)
    {
        if (filled)
        {
            DrawFilledCircleInternal(ref img, 15.5, 15.5, 10.8, MintLight, MintMid, MintDark);
        }
        else
        {
            double cx = 15.5, cy = 15.5;
            double rOuter = 11.2, rStrokeOuter = 10.2, rStrokeInner = 7.8, rInner = 6.8;

            for (int y = 3; y <= 28; y++)
            {
                for (int x = 3; x <= 28; x++)
                {
                    double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (dist <= rOuter && dist >= rInner)
                    {
                        if (dist > rStrokeOuter || dist < rStrokeInner)
                            P(ref img, x, y, Outline);
                        else
                            P(ref img, x, y, y < 16 ? White : SilverLight);
                    }
                }
            }
        }
    }

    private static void DrawFilledCircleInternal(ref Image img, double cx, double cy, double radius, Color light, Color mid, Color dark)
    {
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist <= radius + 0.5)
                {
                    if (dist > radius - 0.65)
                    {
                        P(ref img, x + 1, y + 1, Shadow);
                        P(ref img, x, y, Outline);
                    }
                    else
                    {
                        double nx = (x - cx) / radius;
                        double ny = (y - cy) / radius;
                        Color c = (nx + ny < -0.35) ? light : (nx + ny > 0.45 ? dark : mid);
                        if (nx < -0.35 && ny < -0.35 && dist > radius * 0.45 && dist < radius * 0.72) c = White;
                        P(ref img, x, y, c);
                    }
                }
            }
        }
    }

    private static void DrawTriangleShapeIcon(ref Image img, bool filled)
    {
        for (int y = 5; y <= 26; y++)
        {
            double t = (y - 5) / 21.0;
            int halfW = (int)Math.Round(t * 11.0);
            int minX = 16 - halfW;
            int maxX = 16 + halfW;

            for (int x = minX; x <= maxX; x++)
            {
                int edgeDist = Math.Min(x - minX, maxX - x);
                int bottomDist = 26 - y;

                if (filled)
                {
                    bool isBorder = edgeDist == 0 || bottomDist == 0 || y == 5;
                    if (isBorder)
                    {
                        P(ref img, x + 1, y + 1, Shadow);
                        P(ref img, x, y, Outline);
                    }
                    else
                    {
                        Color c = (x < 14 && y < 22) ? MintLight : (x > 18 || bottomDist <= 2 ? MintDark : MintMid);
                        P(ref img, x, y, c);
                    }
                }
                else
                {
                    // Ici bos ucgen: dis kontur + 2px beyaz duvar + ic kontur
                    if (edgeDist == 0 || bottomDist == 0 || y == 5)
                    {
                        P(ref img, x, y, Outline);
                    }
                    else if (edgeDist <= 2 || bottomDist <= 2)
                    {
                        P(ref img, x, y, y < 18 ? White : SilverLight);
                    }
                    else if (edgeDist == 3 || bottomDist == 3)
                    {
                        P(ref img, x, y, Outline);
                    }
                }
            }
        }
    }

    // =========================================================================
    // ALT SECIM IKONLARI (Kutu, Kement, Sihirli Degnek)
    // =========================================================================
    private static void DrawSelRectIcon(ref Image img)
    {
        DrawDashedBox(ref img, 5, 6, 22, 20);
    }

    private static void DrawSelLassoIcon(ref Image img)
    {
        double cx = 15.5, cy = 14.0, rx = 10.5, ry = 8.5;
        for (int deg = 0; deg < 360; deg += 4)
        {
            if ((deg / 24) % 2 != 0) continue;
            double rad = deg * Math.PI / 180.0;
            int px = (int)Math.Round(cx + Math.Cos(rad) * rx);
            int py = (int)Math.Round(cy + Math.Sin(rad) * ry);
            R(ref img, px - 1, py - 1, 3, 3, Outline);
            P(ref img, px, py, White);
        }

        // Kement dugumu ve ip ucu
        Line(ref img, 15, 22, 12, 27, Outline);
        Line(ref img, 16, 22, 13, 27, White);
        Line(ref img, 17, 22, 14, 27, Outline);
    }

    private static void DrawSelWandIcon(ref Image img)
    {
        // Sihirli degnek sopasi
        for (int i = 0; i <= 18; i++)
        {
            int x = 5 + i;
            int y = 26 - i;
            Color body = i > 13 ? GoldLight : SilverDark;
            R(ref img, x - 1, y - 1, 4, 4, Outline);
            R(ref img, x, y, 2, 2, body);
        }

        // Parlayan yildiz ucu ve kivircik pariltilar
        Sparkle(ref img, 23, 8, GoldMid, White);
        Sparkle(ref img, 14, 6, GoldLight, White);
        Sparkle(ref img, 26, 17, GoldLight, White);
    }

    // =========================================================================
    // YARDIMCI IKONLAR (Pixel-Perfect & İnce Eksenli Simetri)
    // =========================================================================
    private static void DrawPixelPerfectIcon(ref Image img)
    {
        // Kusursuz 1:1 diyagonal piksel basamaklari (6 adet 3x3 kare)
        for (int i = 0; i < 6; i++)
        {
            int bx = 5 + i * 3;
            int by = 22 - i * 3;
            R(ref img, bx - 1, by - 1, 5, 5, Outline);
            R(ref img, bx, by, 3, 3, MintMid);
            P(ref img, bx, by, MintLight);
        }

        Sparkle(ref img, 10, 8, GoldMid, White);
    }

    private static void DrawSymmetryIcon(ref Image img)
    {
        // Tam ortada DUMDUZ ve INCE (1px) altin sarisi dikey simetri ekseni (x = 16)
        R(ref img, 15, 3, 3, 26, Outline);
        for (int y = 4; y <= 27; y++)
        {
            P(ref img, 16, y, y % 2 == 0 ? GoldLight : GoldMid);
        }

        // Sol ve Sag simetrik kanat ucgenleri
        for (int y = 7; y <= 24; y++)
        {
            int distY = Math.Abs(y - 15);
            int span = Math.Max(0, 9 - distY);
            if (span == 0) continue;

            // Sol Kanat (Mavi) ve Sag Kanat (Yesil)
            for (int dx = 0; dx <= span; dx++)
            {
                int lx = 12 - dx;
                int rx = 20 + dx;
                bool isBorder = dx == 0 || dx == span || distY == 8;

                P(ref img, lx, y, isBorder ? Outline : (dx == span - 1 ? BlueLight : BlueMid));
                P(ref img, rx, y, isBorder ? Outline : (dx == span - 1 ? MintLight : MintMid));
            }
        }
    }

    private static void DrawGridIcon(ref Image img)
    {
        R(ref img, 5, 5, 22, 22, Outline);
        for (int r = 0; r < 3; r++)
        {
            for (int c = 0; c < 3; c++)
            {
                R(ref img, 6 + c * 7, 6 + r * 7, 6, 6, (r + c) % 2 == 0 ? SilverLight : SilverMid);
            }
        }
    }

    private static void DrawOnionIcon(ref Image img)
    {
        R(ref img, 5, 5, 16, 16, Outline);
        R(ref img, 6, 6, 14, 14, PinkMid);
        R(ref img, 11, 11, 16, 16, Outline);
        R(ref img, 12, 12, 14, 14, BlueMid);
    }

    // =========================================================================
    // DITHER (NOKTALI GOLGELENDIRME) ARACI VE ALT DESEN IKONLARI
    // =========================================================================
    private static void DrawDitherToolIcon(ref Image img)
    {
        R(ref img, 5, 5, 23, 23, Shadow);
        R(ref img, 4, 4, 23, 23, Outline);

        // 10x10 hucreli (her hucre 2x2 px) gecisli dama/dither ikonu
        for (int cy = 0; cy < 10; cy++)
        {
            for (int cx = 0; cx < 10; cx++)
            {
                bool fillLight;
                if (cx < 3) fillLight = true;
                else if (cx < 5) fillLight = !((cx & 1) == 1 && (cy & 1) == 1); // %75
                else if (cx < 7) fillLight = ((cx + cy) & 1) == 0;              // %50 Dama
                else if (cx < 9) fillLight = (cx & 1) == 0 && (cy & 1) == 0;    // %25
                else fillLight = false;

                Color cellCol = fillLight ? GoldLight : BlueDark;
                R(ref img, 5 + cx * 2, 5 + cy * 2, 2, 2, cellCol);
            }
        }
        R(ref img, 4, 25, 23, 2, Outline);
        R(ref img, 25, 4, 2, 23, Outline);
    }

    private static void DrawDitherPatternIcon(ref Image img, DitherPattern pattern)
    {
        R(ref img, 6, 6, 22, 22, Shadow);
        R(ref img, 5, 5, 22, 22, Outline);

        // 10x10 hucre (her biri 2x2 piksel = 20x20 ic alan)
        for (int cy = 0; cy < 10; cy++)
        {
            for (int cx = 0; cx < 10; cx++)
            {
                bool active = ToolManager.ShouldPaintDither(cx, cy, pattern);
                Color col = active ? MintLight : C(32, 38, 52);
                R(ref img, 6 + cx * 2, 6 + cy * 2, 2, 2, col);
            }
        }
    }

    // =========================================================================
    // TONLAMA (SHADING), EGRI (BEZIER), ALFA KILIDI VE TILE WRAP IKONLARI
    // =========================================================================
    private static void DrawShadingToolIcon(ref Image img)
    {
        // Isik-Golge Kuresi (Sol-ust altin/parlak isik -> Sag-alt derin mavi golge)
        double cx = 15.5, cy = 15.5, radius = 11.2;
        for (int y = 3; y <= 28; y++)
        {
            for (int x = 3; x <= 28; x++)
            {
                double dist = Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (dist > radius + 0.5) continue;

                if (dist > radius - 0.7)
                {
                    P(ref img, x + 1, y + 1, Shadow);
                    P(ref img, x, y, Outline);
                }
                else
                {
                    double shade = ((x - cx) + (y - cy)) / (radius * 1.45);
                    Color c;
                    if (shade < -0.45) c = White;
                    else if (shade < -0.10) c = GoldLight;
                    else if (shade < 0.22) c = GoldMid;
                    else if (shade < 0.55) c = BlueMid;
                    else c = BlueDark;

                    P(ref img, x, y, c);
                }
            }
        }
        Sparkle(ref img, 10, 10, GoldLight, White);
    }

    private static void DrawShadingModeIcon(ref Image img, bool paletteMode)
    {
        R(ref img, 6, 6, 22, 22, Shadow);
        R(ref img, 5, 5, 22, 22, Outline);

        if (paletteMode)
        {
            // 4 kademeli net palet tonlama sutunu
            Color[] ramp = { C(34, 58, 118), BlueDark, BlueMid, BlueLight, White };
            for (int i = 0; i < 5; i++)
            {
                R(ref img, 6 + i * 4, 6, 4, 20, ramp[i]);
                if (i > 0) Line(ref img, 6 + i * 4, 6, 6 + i * 4, 25, Outline);
            }
        }
        else
        {
            // Yumusak gecisli HSL/HSV tonlama
            for (int x = 0; x < 20; x++)
            {
                float t = x / 19f;
                byte r = (byte)(35 + t * 215);
                byte g = (byte)(70 + t * 180);
                byte b = (byte)(140 + t * 110);
                R(ref img, 6 + x, 6, 1, 20, new Color(r, g, b, (byte)255));
            }
        }
    }

    private static void DrawCurveIcon(ref Image img)
    {
        // 3 Noktali Bezier Kavis Egrisi: Sol-Alt (5,25) -> Tepe Kontrol (9,6) -> Sag-Ust (26,22)
        var curvePts = ToolManager.GetBezierCurvePixels(6, 25, 13, 8, 26, 23, cleanCorners: false);
        foreach (var (px, py) in curvePts)
        {
            R(ref img, px - 1, py - 1, 4, 4, Outline);
        }
        foreach (var (px, py) in curvePts)
        {
            R(ref img, px, py, 2, 2, BlueLight);
        }

        // Kontrol tepe noktasina kesikli teget cubugu
        P(ref img, 9, 14, GoldMid);
        P(ref img, 11, 11, GoldMid);
        P(ref img, 17, 11, GoldMid);
        P(ref img, 20, 14, GoldMid);

        // Baslangic, Tepe Kontrol ve Bitis tutamac kutulari
        DrawTileBlock(ref img, 3, 22, 6, 6, MintLight, MintMid, MintDark);
        DrawTileBlock(ref img, 23, 20, 6, 6, MintLight, MintMid, MintDark);
        DrawTileBlock(ref img, 10, 4, 7, 7, GoldLight, GoldMid, GoldDark);
    }

    private static void DrawAlphaLockIcon(ref Image img)
    {
        // Sol-ust dama tahtasi (seffaflik simgesi)
        R(ref img, 4, 4, 16, 16, Outline);
        for (int ry = 0; ry < 2; ry++)
        {
            for (int rx = 0; rx < 2; rx++)
            {
                Color c = (rx + ry) % 2 == 0 ? SilverLight : SilverDark;
                R(ref img, 5 + rx * 7, 5 + ry * 7, 7, 7, c);
            }
        }

        // Sag-alt altin asma kilit (Alpha Lock)
        // Kilit kemeri
        R(ref img, 15, 10, 9, 6, Outline);
        R(ref img, 16, 11, 7, 5, SilverLight);
        R(ref img, 18, 13, 3, 3, Outline);

        // Kilit govdesi
        R(ref img, 13, 16, 14, 12, Shadow);
        R(ref img, 12, 15, 14, 12, Outline);
        R(ref img, 13, 16, 12, 10, GoldMid);
        R(ref img, 13, 16, 12, 2, GoldLight);
        R(ref img, 13, 24, 12, 2, GoldDark);
        // Anahtar deligi
        R(ref img, 18, 19, 2, 4, Outline);
    }

    private static void DrawTileWrapIcon(ref Image img)
    {
        // 2x2 sonsuz tekrarlayan karo (Tile Wrap) izgarasi
        DrawTileBlock(ref img, 4, 4, 11, 11, MintLight, MintMid, MintDark);
        DrawTileBlock(ref img, 17, 4, 11, 11, BlueLight, BlueMid, BlueDark);
        DrawTileBlock(ref img, 4, 17, 11, 11, BlueLight, BlueMid, BlueDark);
        DrawTileBlock(ref img, 17, 17, 11, 11, MintLight, MintMid, MintDark);

        // Merkezde baglanti elmas rozeti
        R(ref img, 13, 13, 6, 6, Outline);
        R(ref img, 14, 14, 4, 4, GoldLight);
        R(ref img, 15, 15, 2, 2, White);
    }

    public static void UnloadIcons()
    {
        foreach (var kv in textures)
        {
            Raylib.UnloadTexture(kv.Value);
        }
        textures.Clear();

        if (hasWindowIcon)
        {
            Raylib.UnloadImage(windowIconImage);
            hasWindowIcon = false;
        }
        isLoaded = false;
    }

    public static void DrawIcon(string key, float x, float y, float size = 32f, Color? tint = null)
    {
        if (!isLoaded) LoadIcons();
        if (!textures.TryGetValue(key, out Texture2D tex)) return;

        float snappedSize = MathF.Max(16f, MathF.Round(size / 16f) * 16f);
        Rectangle src = new Rectangle(0, 0, tex.Width, tex.Height);
        Rectangle dst = new Rectangle(MathF.Round(x), MathF.Round(y), snappedSize, snappedSize);
        Raylib.DrawTexturePro(tex, src, dst, Vector2.Zero, 0f, tint ?? Color.White);
    }

    public static string GetToolIconKey(ToolType tool) => tool switch
    {
        ToolType.Pen => "tool_pen",
        ToolType.Eraser => "tool_eraser",
        ToolType.Dither => "tool_dither",
        ToolType.Shading => "tool_shading",
        ToolType.Bucket => "tool_bucket",
        ToolType.SmartBucket => "tool_smartbucket",
        ToolType.Eyedropper => "tool_eyedropper",
        ToolType.Blur => "tool_blur",
        ToolType.Jumble => "tool_jumble",
        ToolType.Line => "tool_line",
        ToolType.Curve => "tool_curve",
        ToolType.Shape => "tool_shape",
        ToolType.SelectMove => "tool_select",
        _ => "tool_pen"
    };

    public static string GetShadingModeIconKey(ShadingMode mode) => mode switch
    {
        ShadingMode.PaletteRamp => "shading_palette",
        ShadingMode.SmoothHsv => "shading_smooth",
        _ => "shading_palette"
    };

    public static string GetDitherIconKey(DitherPattern pattern) => pattern switch
    {
        DitherPattern.Checker50 => "dither_50",
        DitherPattern.Sparse25 => "dither_25",
        DitherPattern.Dense75 => "dither_75",
        DitherPattern.VerticalLines => "dither_vert",
        DitherPattern.HorizontalLines => "dither_horiz",
        DitherPattern.DiagonalLines => "dither_diag",
        _ => "dither_50"
    };

    public static string GetShapeIconKey(ShapeType shape) => shape switch
    {
        ShapeType.RectangleOutline => "shape_rect_out",
        ShapeType.RectangleFilled => "shape_rect_fill",
        ShapeType.CircleOutline => "shape_circle_out",
        ShapeType.CircleFilled => "shape_circle_fill",
        ShapeType.TriangleOutline => "shape_tri_out",
        ShapeType.TriangleFilled => "shape_tri_fill",
        _ => "shape_rect_out"
    };

    public static string GetSelectionModeIconKey(SelectToolMode mode) => mode switch
    {
        SelectToolMode.Rectangle => "sel_rect",
        SelectToolMode.Lasso => "sel_lasso",
        SelectToolMode.MagicWand => "sel_wand",
        _ => "sel_rect"
    };
}
