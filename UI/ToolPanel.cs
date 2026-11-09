using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class ToolPanel
{
    public float Width { get; set; } = 92f;
    public float TopOffset { get; }

    private float scrollY = 0f;

    private readonly List<(string Name, Func<KeyboardKey> GetKey, ToolType Tool)> tools = new()
    {
        ("Kalem", () => AppSettings.KeyPen, ToolType.Pen),
        ("Silgi", () => AppSettings.KeyEraser, ToolType.Eraser),
        ("Dither (Noktali)", () => AppSettings.KeyDither, ToolType.Dither),
        ("Tonlama (Sol:Acik / Sag:Koyu)", () => AppSettings.KeyShading, ToolType.Shading),
        ("Boya Kovasi", () => AppSettings.KeyBucket, ToolType.Bucket),
        ("Akilli Kova", () => AppSettings.KeySmartBucket, ToolType.SmartBucket),
        ("Damlalik", () => AppSettings.KeyEyedropper, ToolType.Eyedropper),
        ("Bulaniklastir (Blur)", () => AppSettings.KeyBlur, ToolType.Blur),
        ("Karistir (Jumble)", () => AppSettings.KeyJumble, ToolType.Jumble),
        ("Cizgi Araci", () => AppSettings.KeyLine, ToolType.Line),
        ("Egri Araci (3 Nokta Bezier)", () => AppSettings.KeyCurve, ToolType.Curve),
        ("Sekil Araci", () => AppSettings.KeyShape, ToolType.Shape),
        ("Sec ve Tasi", () => AppSettings.KeySelectMove, ToolType.SelectMove)
    };

    private readonly List<(string Label, DitherPattern Pattern)> ditherPatterns = new()
    {
        ("Dither: %50 Dama", DitherPattern.Checker50),
        ("Dither: %25 Seyrek", DitherPattern.Sparse25),
        ("Dither: %75 Yogun", DitherPattern.Dense75),
        ("Dither: Dikey Cizgi", DitherPattern.VerticalLines),
        ("Dither: Yatay Cizgi", DitherPattern.HorizontalLines),
        ("Dither: Capraz Doku", DitherPattern.DiagonalLines)
    };

    private readonly List<(string Label, ShadingMode Mode)> shadingModes = new()
    {
        ("Palet Tonlama (Sol:Acik / Sag:Koyu)", ShadingMode.PaletteRamp),
        ("Yumusak Tonlama (HSL Isik/Golge)", ShadingMode.SmoothHsv)
    };

    private readonly List<(string Label, ShapeType Shape)> shapes = new()
    {
        ("Kare (Cerceve)", ShapeType.RectangleOutline),
        ("Kare (Dolu)", ShapeType.RectangleFilled),
        ("Cember (Cerceve)", ShapeType.CircleOutline),
        ("Cember (Dolu)", ShapeType.CircleFilled),
        ("Ucgen (Cerceve)", ShapeType.TriangleOutline),
        ("Ucgen (Dolu)", ShapeType.TriangleFilled)
    };

    private readonly List<(string Label, SelectToolMode Mode)> selectionModes = new()
    {
        ("Dikdortgen Secim", SelectToolMode.Rectangle),
        ("Serbest Kement (Lasso)", SelectToolMode.Lasso),
        ("Sihirli Degnek", SelectToolMode.MagicWand)
    };

    // 36x36 buton icinde 32x32 tam piksel ikon = sifir bozulma ve kompakt yerlesim
    private const int BtnSize = 36;
    private const int BtnGap = 3;

    public ToolPanel(float topOffset)
    {
        TopOffset = topOffset;
    }

    private int GetColumns() => Width >= 80f ? 2 : 1;

    private Rectangle GetToolSlotRect(int index)
    {
        int cols = GetColumns();
        int row = index / cols;
        int col = index % cols;
        float totalRowW = cols * BtnSize + (cols - 1) * BtnGap;
        float startX = MathF.Round((Width - totalRowW) / 2f);
        float startY = TopOffset + 23f - scrollY;

        return new Rectangle(
            startX + col * (BtnSize + BtnGap),
            startY + row * (BtnSize + BtnGap),
            BtnSize,
            BtnSize
        );
    }

    private float GetToolsBottomY()
    {
        int cols = GetColumns();
        int rows = (int)Math.Ceiling(tools.Count / (float)cols);
        return TopOffset + 23f - scrollY + rows * (BtnSize + BtnGap);
    }

    private Rectangle GetAssistantSlotRect(int index)
    {
        int cols = GetColumns();
        int row = index / cols;
        int col = index % cols;
        float totalRowW = cols * BtnSize + (cols - 1) * BtnGap;
        float startX = MathF.Round((Width - totalRowW) / 2f);
        float startY = GetToolsBottomY() + 12f;

        return new Rectangle(
            startX + col * (BtnSize + BtnGap),
            startY + row * (BtnSize + BtnGap),
            BtnSize,
            BtnSize
        );
    }

    private float GetSubModesTopY()
    {
        int cols = GetColumns();
        int rows = (int)Math.Ceiling(4 / (float)cols);
        return GetToolsBottomY() + 12f + rows * (BtnSize + BtnGap) + 18f;
    }

    private Rectangle GetSubModeSlotRect(int index)
    {
        int cols = GetColumns();
        int row = index / cols;
        int col = index % cols;
        float totalRowW = cols * BtnSize + (cols - 1) * BtnGap;
        float startX = MathF.Round((Width - totalRowW) / 2f);
        float startY = GetSubModesTopY();

        return new Rectangle(
            startX + col * (BtnSize + BtnGap),
            startY + row * (BtnSize + BtnGap),
            BtnSize,
            BtnSize
        );
    }

    private float GetTotalContentHeight(ToolType activeTool)
    {
        int cols = GetColumns();
        int toolRows = (int)Math.Ceiling(tools.Count / (float)cols);
        int assistRows = (int)Math.Ceiling(4 / (float)cols);
        int subCount = activeTool switch
        {
            ToolType.Dither => ditherPatterns.Count,
            ToolType.Shading => shadingModes.Count,
            ToolType.Shape => shapes.Count,
            ToolType.SelectMove => selectionModes.Count,
            _ => 0
        };
        int subRows = subCount > 0 ? (int)Math.Ceiling(subCount / (float)cols) : 0;
        float h = (toolRows + assistRows) * (BtnSize + BtnGap) + 20f;
        if (subRows > 0)
            h += 18f + subRows * (BtnSize + BtnGap);
        return h + 12f;
    }

    public bool Update(
        Vector2 mousePos,
        Action<ToolType> onSelectTool,
        Action<ShapeType> onSelectShape,
        Action<SelectToolMode> onSelectMode,
        Action? onTogglePixelPerfect = null,
        Action? onCycleSymmetry = null,
        ToolType currentActiveTool = ToolType.Pen,
        Action<DitherPattern>? onSelectDither = null,
        Action<ShadingMode>? onSelectShading = null,
        Action? onToggleAlphaLock = null,
        Action? onToggleTileWrap = null)
    {
        float availH = Math.Max(80f, Raylib.GetScreenHeight() - TopOffset - 23f);
        float maxScroll = Math.Max(0f, GetTotalContentHeight(currentActiveTool) - availH);
        scrollY = Math.Clamp(scrollY, 0f, maxScroll);

        if (mousePos.X > Width || mousePos.Y < TopOffset) return false;

        float wheel = Raylib.GetMouseWheelMove();
        if (wheel != 0f && maxScroll > 0f)
        {
            scrollY = Math.Clamp(scrollY - wheel * 28f, 0f, maxScroll);
        }

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && mousePos.Y >= TopOffset + 20f)
        {
            for (int i = 0; i < tools.Count; i++)
            {
                if (Raylib.CheckCollisionPointRec(mousePos, GetToolSlotRect(i)))
                {
                    onSelectTool(tools[i].Tool);
                    return true;
                }
            }

            if (Raylib.CheckCollisionPointRec(mousePos, GetAssistantSlotRect(0)))
            {
                onTogglePixelPerfect?.Invoke();
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, GetAssistantSlotRect(1)))
            {
                onCycleSymmetry?.Invoke();
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, GetAssistantSlotRect(2)))
            {
                onToggleAlphaLock?.Invoke();
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, GetAssistantSlotRect(3)))
            {
                onToggleTileWrap?.Invoke();
                return true;
            }

            if (currentActiveTool == ToolType.Dither)
            {
                for (int i = 0; i < ditherPatterns.Count; i++)
                {
                    if (Raylib.CheckCollisionPointRec(mousePos, GetSubModeSlotRect(i)))
                    {
                        onSelectDither?.Invoke(ditherPatterns[i].Pattern);
                        return true;
                    }
                }
            }
            else if (currentActiveTool == ToolType.Shading)
            {
                for (int i = 0; i < shadingModes.Count; i++)
                {
                    if (Raylib.CheckCollisionPointRec(mousePos, GetSubModeSlotRect(i)))
                    {
                        onSelectShading?.Invoke(shadingModes[i].Mode);
                        return true;
                    }
                }
            }
            else if (currentActiveTool == ToolType.Shape)
            {
                for (int i = 0; i < shapes.Count; i++)
                {
                    if (Raylib.CheckCollisionPointRec(mousePos, GetSubModeSlotRect(i)))
                    {
                        onSelectShape(shapes[i].Shape);
                        return true;
                    }
                }
            }
            else if (currentActiveTool == ToolType.SelectMove)
            {
                for (int i = 0; i < selectionModes.Count; i++)
                {
                    if (Raylib.CheckCollisionPointRec(mousePos, GetSubModeSlotRect(i)))
                    {
                        onSelectMode(selectionModes[i].Mode);
                        return true;
                    }
                }
            }

            return true;
        }

        return Raylib.IsMouseButtonDown(MouseButton.Left);
    }

    public void Draw(
        ToolType activeTool,
        ShapeType activeShape,
        SelectToolMode activeSelMode,
        bool pixelPerfect,
        SymmetryMode symmetry,
        int screenHeight,
        DitherPattern activeDither = DitherPattern.Checker50,
        ShadingMode activeShading = ShadingMode.PaletteRamp,
        bool alphaLock = false,
        bool tileWrap = false)
    {
        Rectangle panelRect = new Rectangle(0, TopOffset, Width, screenHeight - TopOffset);
        PixelSkin.DrawPanel(panelRect);

        // Ust Baslik Cubugu
        int titleW = Raylib.MeasureText("ARACLAR", 10);
        Raylib.DrawText("ARACLAR", (int)Math.Max(6, (Width - titleW) / 2f), (int)TopOffset + 7, 10, PixelSkin.TextMuted);
        Raylib.DrawLine(6, (int)TopOffset + 19, (int)Width - 6, (int)TopOffset + 19, PixelSkin.BorderOutline);

        Vector2 mousePos = Raylib.GetMousePosition();
        (Vector2 pos, string title, string? key)? activeTooltip = null;

        int clipTop = (int)TopOffset + 20;
        int clipH = Math.Max(1, screenHeight - clipTop);
        Raylib.BeginScissorMode(0, clipTop, (int)Width, clipH);

        // 1. ANA ARAC IKONLARI (Tam 32x32 piksel olcegi)
        for (int i = 0; i < tools.Count; i++)
        {
            Rectangle btnRect = GetToolSlotRect(i);
            bool isSelected = tools[i].Tool == activeTool;
            bool isHover = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, btnRect);

            PixelSkin.DrawButton(btnRect, isHover, isSelected);

            float pad = isSelected ? 3f : 2f;
            string iconKey = PixelIcons.GetToolIconKey(tools[i].Tool);
            PixelIcons.DrawIcon(iconKey, btnRect.X + pad, btnRect.Y + pad, 32f);

            // Alt secenegi olan araclarda sag alt kosede minik isaretci
            if (tools[i].Tool == ToolType.Dither || tools[i].Tool == ToolType.Shading || tools[i].Tool == ToolType.Shape || tools[i].Tool == ToolType.SelectMove)
            {
                Color dotCol = isSelected ? PixelSkin.AccentGold : PixelSkin.TextMuted;
                Raylib.DrawRectangle((int)(btnRect.X + btnRect.Width - 5), (int)(btnRect.Y + btnRect.Height - 5), 3, 3, dotCol);
            }

            if (isHover)
            {
                string keyStr = AppSettings.KeyToString(tools[i].GetKey());
                activeTooltip = (new Vector2(Width + 8, btnRect.Y + 6), tools[i].Name, keyStr != "Atanmadi" ? keyStr : null);
            }
        }

        // 2. YARDIMCI IKONLAR (Pixel-Perfect, Simetri, Alfa Kilidi, Tile Wrap)
        Rectangle ppRect = GetAssistantSlotRect(0);
        Rectangle symRect = GetAssistantSlotRect(1);
        Rectangle lockRect = GetAssistantSlotRect(2);
        Rectangle tileRect = GetAssistantSlotRect(3);

        int divY = (int)ppRect.Y - 7;
        Raylib.DrawLine(6, divY, (int)Width - 6, divY, PixelSkin.BorderOutline);
        Raylib.DrawLine(6, divY + 1, (int)Width - 6, divY + 1, PixelSkin.BevelLight);

        // 2a. Pixel-Perfect
        bool ppHover = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, ppRect);
        PixelSkin.DrawButton(ppRect, ppHover, pixelPerfect);
        PixelIcons.DrawIcon("assist_pixelperfect", ppRect.X + 2, ppRect.Y + 2, 32f);
        if (pixelPerfect)
            Raylib.DrawRectangle((int)ppRect.X + 3, (int)ppRect.Y + 3, 4, 4, PixelSkin.AccentGold);
        if (ppHover)
        {
            string k = AppSettings.KeyToString(AppSettings.KeyPixelPerfect);
            activeTooltip = (new Vector2(Width + 8, ppRect.Y + 6), $"Pixel-Perfect ({(pixelPerfect ? "Acik" : "Kapali")})", k != "Atanmadi" ? k : null);
        }

        // 2b. Simetri
        bool symActive = symmetry != SymmetryMode.None;
        bool symHover = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, symRect);
        PixelSkin.DrawButton(symRect, symHover, symActive);
        PixelIcons.DrawIcon("assist_symmetry", symRect.X + 2, symRect.Y + 2, 32f);
        if (symActive)
        {
            string badge = symmetry switch
            {
                SymmetryMode.Horizontal => "D",
                SymmetryMode.Vertical => "Y",
                _ => "4"
            };
            Raylib.DrawRectangle((int)symRect.X + 2, (int)symRect.Y + 2, 10, 11, new Color((byte)16, (byte)20, (byte)28, (byte)230));
            Raylib.DrawText(badge, (int)symRect.X + 4, (int)symRect.Y + 3, 10, PixelSkin.AccentGold);
        }
        if (symHover)
        {
            string k = AppSettings.KeyToString(AppSettings.KeySymmetry);
            activeTooltip = (new Vector2(Width + 8, symRect.Y + 6), $"Simetri: {ToolManager.SymmetryLabel(symmetry)}", k != "Atanmadi" ? k : null);
        }

        // 2c. Alfa Kilidi (Alpha Lock)
        bool lockHover = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, lockRect);
        PixelSkin.DrawButton(lockRect, lockHover, alphaLock);
        PixelIcons.DrawIcon("assist_alphalock", lockRect.X + 2, lockRect.Y + 2, 32f);
        if (alphaLock)
            Raylib.DrawRectangle((int)lockRect.X + 3, (int)lockRect.Y + 3, 4, 4, PixelSkin.AccentGold);
        if (lockHover)
        {
            string k = AppSettings.KeyToString(AppSettings.KeyAlphaLock);
            activeTooltip = (new Vector2(Width + 8, lockRect.Y + 6), $"Alfa Kilidi ({(alphaLock ? "Acik" : "Kapali")})", k != "Atanmadi" ? k : null);
        }

        // 2d. Tile / Doseme (Wrap) Modu
        bool tileHover = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, tileRect);
        PixelSkin.DrawButton(tileRect, tileHover, tileWrap);
        PixelIcons.DrawIcon("assist_tilewrap", tileRect.X + 2, tileRect.Y + 2, 32f);
        if (tileWrap)
            Raylib.DrawRectangle((int)tileRect.X + 3, (int)tileRect.Y + 3, 4, 4, PixelSkin.AccentGold);
        if (tileHover)
        {
            string k = AppSettings.KeyToString(AppSettings.KeyTileWrap);
            activeTooltip = (new Vector2(Width + 8, tileRect.Y + 6), $"Tile / Doseme Modu ({(tileWrap ? "Acik" : "Kapali")})", k != "Atanmadi" ? k : null);
        }

        // 3. ALT ARAC MODU IKONLARI (Dither, Shading, Sekiller veya Secim Modlari)
        if (activeTool == ToolType.Dither || activeTool == ToolType.Shading || activeTool == ToolType.Shape || activeTool == ToolType.SelectMove)
        {
            int subDivY = (int)GetSubModesTopY() - 13;
            Raylib.DrawLine(6, subDivY, (int)Width - 6, subDivY, PixelSkin.BorderOutline);
            string subHeader = activeTool switch
            {
                ToolType.Dither => "DOKU",
                ToolType.Shading => "TONLAMA",
                ToolType.Shape => "SEKIL",
                _ => "SECIM"
            };
            int shW = Raylib.MeasureText(subHeader, 10);
            Raylib.DrawText(subHeader, (int)Math.Max(6, (Width - shW) / 2f), subDivY + 2, 10, PixelSkin.AccentHover);

            if (activeTool == ToolType.Dither)
            {
                for (int i = 0; i < ditherPatterns.Count; i++)
                {
                    Rectangle sRect = GetSubModeSlotRect(i);
                    bool isSel = ditherPatterns[i].Pattern == activeDither;
                    bool isHov = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, sRect);

                    PixelSkin.DrawButton(sRect, isHov, isSel);
                    PixelIcons.DrawIcon(PixelIcons.GetDitherIconKey(ditherPatterns[i].Pattern), sRect.X + 2, sRect.Y + 2, 32f);

                    if (isHov)
                        activeTooltip = (new Vector2(Width + 8, sRect.Y + 6), ditherPatterns[i].Label, null);
                }
            }
            else if (activeTool == ToolType.Shading)
            {
                for (int i = 0; i < shadingModes.Count; i++)
                {
                    Rectangle sRect = GetSubModeSlotRect(i);
                    bool isSel = shadingModes[i].Mode == activeShading;
                    bool isHov = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, sRect);

                    PixelSkin.DrawButton(sRect, isHov, isSel);
                    PixelIcons.DrawIcon(PixelIcons.GetShadingModeIconKey(shadingModes[i].Mode), sRect.X + 2, sRect.Y + 2, 32f);

                    if (isHov)
                        activeTooltip = (new Vector2(Width + 8, sRect.Y + 6), shadingModes[i].Label, null);
                }
            }
            else if (activeTool == ToolType.Shape)
            {
                for (int i = 0; i < shapes.Count; i++)
                {
                    Rectangle sRect = GetSubModeSlotRect(i);
                    bool isSel = shapes[i].Shape == activeShape;
                    bool isHov = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, sRect);

                    PixelSkin.DrawButton(sRect, isHov, isSel);
                    PixelIcons.DrawIcon(PixelIcons.GetShapeIconKey(shapes[i].Shape), sRect.X + 2, sRect.Y + 2, 32f);

                    if (isHov)
                        activeTooltip = (new Vector2(Width + 8, sRect.Y + 6), shapes[i].Label, null);
                }
            }
            else if (activeTool == ToolType.SelectMove)
            {
                for (int i = 0; i < selectionModes.Count; i++)
                {
                    Rectangle sRect = GetSubModeSlotRect(i);
                    bool isSel = selectionModes[i].Mode == activeSelMode;
                    bool isHov = mousePos.Y >= clipTop && Raylib.CheckCollisionPointRec(mousePos, sRect);

                    PixelSkin.DrawButton(sRect, isHov, isSel);
                    PixelIcons.DrawIcon(PixelIcons.GetSelectionModeIconKey(selectionModes[i].Mode), sRect.X + 2, sRect.Y + 2, 32f);

                    if (isHov)
                        activeTooltip = (new Vector2(Width + 8, sRect.Y + 6), selectionModes[i].Label, null);
                }
            }
        }

        // Eger sol panel kucuk ekranda kaydirilabiliyorsa ince kaydirma cubugu ciz
        float availH = Math.Max(80f, screenHeight - TopOffset - 23f);
        float totalH = GetTotalContentHeight(activeTool);
        float maxScroll = Math.Max(0f, totalH - availH);
        if (maxScroll > 0f)
        {
            float thumbH = Math.Max(24f, (availH / totalH) * (availH - 8f));
            float thumbY = clipTop + 4f + (scrollY / maxScroll) * (availH - 8f - thumbH);
            Raylib.DrawRectangle((int)Width - 4, (int)thumbY, 2, (int)thumbH, PixelSkin.AccentPrimary);
        }

        Raylib.EndScissorMode();

        // Hover Ipucu Kutusu (Tooltip) - Scissor disinda cizilir ki kirpilmasin
        if (activeTooltip.HasValue)
        {
            PixelSkin.DrawTooltip(activeTooltip.Value.pos, activeTooltip.Value.title, activeTooltip.Value.key);
        }
    }
}
