using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.IO;
using PixelArtApp.Core;

namespace PixelArtApp.UI;

public class ReferenceImageManager
{
    public Texture2D? Texture { get; private set; }
    public bool ShowOnCanvas { get; set; } = false;
    public bool ShowInSidePanel { get; set; } = true;
    public float Opacity { get; set; } = 0.4f;

    private bool isDraggingSlider = false;

    public void LoadReference()
    {
        string? path = Exporter.OpenImageDialog();
        if (!string.IsNullOrEmpty(path))
        {
            Image img = Raylib.LoadImage(path);
            if (img.Width > 0 && img.Height > 0)
            {
                if (Texture.HasValue) Raylib.UnloadTexture(Texture.Value);
                Texture = Raylib.LoadTextureFromImage(img);
                ShowInSidePanel = true;
                Raylib.UnloadImage(img);
            }
        }
    }

    public void ClearReference()
    {
        if (Texture.HasValue)
        {
            Raylib.UnloadTexture(Texture.Value);
            Texture = null;
            ShowOnCanvas = false;
        }
    }

    public bool UpdateUI(Vector2 mousePos, Rectangle bounds)
    {
        if (!AppSettings.ShowReferencePanel)
        {
            isDraggingSlider = false;
            Rectangle showTabBtn = new Rectangle(bounds.X + bounds.Width - 78, bounds.Y + bounds.Height - 20, 78, 18);
            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, showTabBtn))
            {
                AppSettings.ShowReferencePanel = true;
                return true;
            }
            return false;
        }

        bool intercepted = false;

        Rectangle hidePanelBtn = new Rectangle(bounds.X + bounds.Width - 44, bounds.Y + 5, 38, 18);
        Rectangle loadBtn = new Rectangle(bounds.X + 6, bounds.Y + 26, bounds.Width / 2 - 8, 22);
        Rectangle clearBtn = new Rectangle(bounds.X + bounds.Width / 2 + 2, bounds.Y + 26, bounds.Width / 2 - 8, 22);

        Rectangle canvasToggleBtn = new Rectangle(bounds.X + 6, bounds.Y + 52, bounds.Width / 2 - 8, 22);
        Rectangle sideToggleBtn = new Rectangle(bounds.X + bounds.Width / 2 + 2, bounds.Y + 52, bounds.Width / 2 - 8, 22);

        Rectangle sliderTrackHitbox = new Rectangle(bounds.X + 4, bounds.Y + 76, bounds.Width - 8, 24);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, hidePanelBtn))
            {
                AppSettings.ShowReferencePanel = false;
                isDraggingSlider = false;
                return true;
            }
            if (Raylib.CheckCollisionPointRec(mousePos, loadBtn))
            {
                LoadReference();
                intercepted = true;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, clearBtn) && Texture.HasValue)
            {
                ClearReference();
                intercepted = true;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, canvasToggleBtn) && Texture.HasValue)
            {
                ShowOnCanvas = !ShowOnCanvas;
                intercepted = true;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, sideToggleBtn) && Texture.HasValue)
            {
                ShowInSidePanel = !ShowInSidePanel;
                intercepted = true;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, sliderTrackHitbox) && Texture.HasValue && ShowOnCanvas)
            {
                isDraggingSlider = true;
                intercepted = true;
            }
        }

        if (isDraggingSlider)
        {
            if (Raylib.IsMouseButtonDown(MouseButton.Left) && ShowOnCanvas)
            {
                float trackX = bounds.X + 10;
                float trackW = Math.Max(1f, bounds.Width - 20);
                Opacity = Math.Clamp((mousePos.X - trackX) / trackW, 0.05f, 1f);
                intercepted = true;
            }
            else
            {
                isDraggingSlider = false;
            }
        }

        if (Raylib.CheckCollisionPointRec(mousePos, bounds) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            intercepted = true;

        return intercepted;
    }

    public void DrawUI(Rectangle bounds, Vector2 mousePos)
    {
        if (!AppSettings.ShowReferencePanel)
        {
            Rectangle showTabBtn = new Rectangle(bounds.X + bounds.Width - 78, bounds.Y + bounds.Height - 20, 78, 18);
            bool hoverShow = Raylib.CheckCollisionPointRec(mousePos, showTabBtn);
            PixelSkin.DrawButton(showTabBtn, hoverShow, false);
            Raylib.DrawText("Referans Ac", (int)showTabBtn.X + 6, (int)showTabBtn.Y + 4, 10, PixelSkin.TextMuted);
            return;
        }

        PixelSkin.DrawInsetBox(bounds);
        Raylib.DrawText("Referans", (int)bounds.X + 8, (int)bounds.Y + 8, 12, PixelSkin.TextPrimary);

        Rectangle hidePanelBtn = new Rectangle(bounds.X + bounds.Width - 44, bounds.Y + 5, 38, 18);
        bool hideHover = Raylib.CheckCollisionPointRec(mousePos, hidePanelBtn);
        PixelSkin.DrawButton(hidePanelBtn, hideHover, false);
        Raylib.DrawText("Gizle", (int)hidePanelBtn.X + 6, (int)hidePanelBtn.Y + 4, 10, hideHover ? Color.Yellow : PixelSkin.TextMuted);

        Rectangle loadBtn = new Rectangle(bounds.X + 6, bounds.Y + 26, bounds.Width / 2 - 8, 22);
        Rectangle clearBtn = new Rectangle(bounds.X + bounds.Width / 2 + 2, bounds.Y + 26, bounds.Width / 2 - 8, 22);

        PixelSkin.DrawButton(loadBtn, Raylib.CheckCollisionPointRec(mousePos, loadBtn), false);
        Raylib.DrawText("Yukle", (int)loadBtn.X + (int)(loadBtn.Width - Raylib.MeasureText("Yukle", 11)) / 2, (int)loadBtn.Y + 6, 11, Color.White);

        bool clearHover = Raylib.CheckCollisionPointRec(mousePos, clearBtn) && Texture.HasValue;
        PixelSkin.DrawButton(clearBtn, clearHover, false);
        Color clearColor = Texture.HasValue ? (clearHover ? new Color((byte)255, (byte)100, (byte)100, (byte)255) : Color.White) : PixelSkin.TextMuted;
        Raylib.DrawText("Temizle", (int)clearBtn.X + (int)(clearBtn.Width - Raylib.MeasureText("Temizle", 11)) / 2, (int)clearBtn.Y + 6, 11, clearColor);

        if (!Texture.HasValue)
        {
            Raylib.DrawText("Gorsel yuklenmedi.", (int)bounds.X + 10, (int)bounds.Y + 58, 11, PixelSkin.TextMuted);
            return;
        }

        Rectangle canvasToggleBtn = new Rectangle(bounds.X + 6, bounds.Y + 52, bounds.Width / 2 - 8, 22);
        Rectangle sideToggleBtn = new Rectangle(bounds.X + bounds.Width / 2 + 2, bounds.Y + 52, bounds.Width / 2 - 8, 22);

        PixelSkin.DrawButton(canvasToggleBtn, Raylib.CheckCollisionPointRec(mousePos, canvasToggleBtn), ShowOnCanvas);
        string cTxt = "Tuvalde";
        Raylib.DrawText(cTxt, (int)canvasToggleBtn.X + (int)(canvasToggleBtn.Width - Raylib.MeasureText(cTxt, 11)) / 2, (int)canvasToggleBtn.Y + 6, 11, ShowOnCanvas ? Color.Yellow : Color.RayWhite);

        PixelSkin.DrawButton(sideToggleBtn, Raylib.CheckCollisionPointRec(mousePos, sideToggleBtn), ShowInSidePanel);
        string sTxt = "Panelde";
        Raylib.DrawText(sTxt, (int)sideToggleBtn.X + (int)(sideToggleBtn.Width - Raylib.MeasureText(sTxt, 11)) / 2, (int)sideToggleBtn.Y + 6, 11, ShowInSidePanel ? Color.SkyBlue : Color.RayWhite);

        float contentTopY = bounds.Y + 78f;

        if (ShowOnCanvas)
        {
            Raylib.DrawText($"Tuval Opaklik: %{(int)(Opacity * 100)}", (int)bounds.X + 8, (int)contentTopY, 10, PixelSkin.TextMuted);

            Rectangle sliderTrack = new Rectangle(bounds.X + 10, contentTopY + 14, bounds.Width - 20, 5);
            Raylib.DrawRectangleRounded(sliderTrack, 0.5f, 4, PixelSkin.PanelBg);

            Rectangle sliderFill = new Rectangle(sliderTrack.X, sliderTrack.Y, sliderTrack.Width * Opacity, sliderTrack.Height);
            Raylib.DrawRectangleRounded(sliderFill, 0.5f, 4, PixelSkin.AccentPrimary);

            Rectangle sliderThumb = new Rectangle(bounds.X + 10 + (Opacity * (bounds.Width - 20)) - 4, contentTopY + 10, 8, 13);
            Raylib.DrawRectangleRounded(sliderThumb, 0.4f, 4, isDraggingSlider ? PixelSkin.AccentHover : Color.White);

            contentTopY += 26f;
        }

        if (ShowInSidePanel)
        {
            float availH = (bounds.Y + bounds.Height) - contentTopY - 6f;
            float availW = bounds.Width - 12f;
            if (availH >= 24f && availW >= 24f)
            {
                Rectangle previewFrame = new Rectangle(bounds.X + 6, contentTopY, availW, availH);
                Raylib.DrawRectangleRec(previewFrame, new Color((byte)18, (byte)18, (byte)22, (byte)255));
                Raylib.DrawRectangleLinesEx(previewFrame, 1f, PixelSkin.BorderOutline);

                float texW = Texture.Value.Width;
                float texH = Texture.Value.Height;
                float scale = Math.Min((availW - 4f) / texW, (availH - 4f) / texH);
                float drawW = texW * scale;
                float drawH = texH * scale;
                float drawX = previewFrame.X + (availW - drawW) / 2f;
                float drawY = previewFrame.Y + (availH - drawH) / 2f;

                Rectangle src = new Rectangle(0, 0, texW, texH);
                Rectangle dest = new Rectangle(drawX, drawY, drawW, drawH);
                Raylib.DrawTexturePro(Texture.Value, src, dest, Vector2.Zero, 0f, Color.White);
            }
        }
    }

    public void DrawOnCanvas(Canvas canvas)
    {
        if (!ShowOnCanvas || !Texture.HasValue) return;

        Rectangle src = new Rectangle(0, 0, Texture.Value.Width, Texture.Value.Height);
        Rectangle dest = new Rectangle(canvas.Offset.X, canvas.Offset.Y, canvas.Width * canvas.PixelScale, canvas.Height * canvas.PixelScale);

        byte alpha = (byte)Math.Clamp(Opacity * 255f, 0f, 255f);
        Raylib.DrawTexturePro(Texture.Value, src, dest, Vector2.Zero, 0f, new Color((byte)255, (byte)255, (byte)255, alpha));
    }
}
