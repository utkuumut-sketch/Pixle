using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace PixelArtApp.UI;

public class Palette
{
    public Color CurrentColor { get; set; } = Color.Black;
    public Vector2 Offset { get; set; }

    private readonly List<Color> colors = new();
    private readonly int boxSize = 22;
    private readonly int spacing = 5;
    private readonly int columns = 5;

    // Kaydırma & Çerçeve Parametreleri
    private float scrollY = 0f;
    private bool isDraggingScrollbar = false;
    private float scrollDragStartMouseY = 0f;
    private float scrollDragStartScrollY = 0f;

    private readonly float viewportWidth = 144f;
    private readonly float viewportHeight = 250f;

    public Rectangle ActiveColorBoxBounds => new Rectangle(Offset.X, Offset.Y, 48, 48);
    private Rectangle ViewportBounds => new Rectangle(Offset.X, Offset.Y + 70, viewportWidth, viewportHeight);

    public Palette(Vector2 offset)
    {
        Offset = offset;

        // Varsayılan renkler
        colors.AddRange(new Color[]
        {
            Color.Black, Color.DarkGray, Color.Gray, Color.LightGray, Color.White,
            Color.Red, Color.Maroon, Color.Orange, Color.Gold, Color.Yellow,
            Color.DarkGreen, Color.Green, Color.Lime, Color.DarkBlue, Color.Blue,
            Color.SkyBlue, Color.Purple, Color.Violet, Color.Beige, Color.Brown
        });
    }

    public void AddColor(Color newColor)
    {
        bool exists = colors.Exists(c => c.R == newColor.R && c.G == newColor.G && c.B == newColor.B && c.A == newColor.A);
        if (!exists)
        {
            colors.Add(newColor);
        }
        CurrentColor = newColor;

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - viewportHeight);
        scrollY = maxScroll;
    }

    public void SetColor(Color newColor)
    {
        CurrentColor = newColor;
    }

    // YENİ EKLENEN METOT: Klavye kısayolları için
    public bool SelectColor(int index)
    {
        if (index >= 0 && index < colors.Count)
        {
            CurrentColor = colors[index];
            return true;
        }
        return false;
    }

    private float GetTotalContentHeight()
    {
        int totalRows = (int)Math.Ceiling(colors.Count / (float)columns);
        return totalRows * (boxSize + spacing) + 6;
    }

    public bool Update(Vector2 mousePos, Action onOpenColorPicker)
    {
        // 1. Aktif Renk Kutusuna Tıklama
        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, ActiveColorBoxBounds))
        {
            onOpenColorPicker();
            return true;
        }

        Rectangle vp = ViewportBounds;
        bool isInsideViewport = Raylib.CheckCollisionPointRec(mousePos, vp);

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - viewportHeight);

        // 2. Fare Tekerleği ile Kaydırma
        if (isInsideViewport)
        {
            float wheel = Raylib.GetMouseWheelMove();
            if (wheel != 0)
            {
                scrollY -= wheel * (boxSize + spacing);
                scrollY = Math.Clamp(scrollY, 0, maxScroll);
            }
        }

        // 3. Scrollbar Sürükleme
        if (maxScroll > 0)
        {
            float trackX = vp.X + vp.Width - 8;
            float trackHeight = vp.Height;
            float thumbHeight = Math.Max(24f, (vp.Height / totalHeight) * trackHeight);
            float thumbY = vp.Y + (scrollY / maxScroll) * (trackHeight - thumbHeight);
            Rectangle thumbRect = new Rectangle(trackX, thumbY, 6, thumbHeight);

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && Raylib.CheckCollisionPointRec(mousePos, thumbRect))
            {
                isDraggingScrollbar = true;
                scrollDragStartMouseY = mousePos.Y;
                scrollDragStartScrollY = scrollY;
            }

            if (isDraggingScrollbar)
            {
                if (Raylib.IsMouseButtonDown(MouseButton.Left))
                {
                    float deltaY = mousePos.Y - scrollDragStartMouseY;
                    float scrollRatio = deltaY / (trackHeight - thumbHeight);
                    scrollY = Math.Clamp(scrollDragStartScrollY + scrollRatio * maxScroll, 0, maxScroll);
                }
                else
                {
                    isDraggingScrollbar = false;
                }
                return true;
            }
        }

        // 4. Renk Kutucuklarına Tıklama
        if (isInsideViewport && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            float startX = vp.X + 6;
            float startY = vp.Y + 6 - scrollY;

            for (int i = 0; i < colors.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;

                Rectangle boxRect = new Rectangle(
                    startX + col * (boxSize + spacing),
                    startY + row * (boxSize + spacing),
                    boxSize,
                    boxSize
                );

                if (Raylib.CheckCollisionPointRec(mousePos, boxRect))
                {
                    CurrentColor = colors[i];
                    return true;
                }
            }
        }

        return isInsideViewport;
    }

    public void Draw()
    {
        // 1. Aktif Renk Bölümü
        Raylib.DrawText("Aktif Renk:", (int)Offset.X, (int)Offset.Y - 20, 14, Color.LightGray);
        Raylib.DrawRectangleRec(ActiveColorBoxBounds, CurrentColor);
        Raylib.DrawRectangleLinesEx(ActiveColorBoxBounds, 2, Color.RayWhite);

        string hex = $"#{CurrentColor.R:X2}{CurrentColor.G:X2}{CurrentColor.B:X2}";
        Raylib.DrawText(hex, (int)Offset.X + 58, (int)Offset.Y + 16, 14, Color.Yellow);

        // 2. Çerçeveli Palet Kutusu Başlığı
        Rectangle vp = ViewportBounds;
        Raylib.DrawText("Palet:", (int)vp.X, (int)vp.Y - 18, 14, Color.LightGray);

        // Zemin ve Çerçeve Sınırı
        Raylib.DrawRectangleRec(vp, new Color(22, 22, 22, 255));
        Raylib.DrawRectangleLinesEx(vp, 1, Color.DarkGray);

        float totalHeight = GetTotalContentHeight();
        float maxScroll = Math.Max(0, totalHeight - viewportHeight);

        // 3. İçerik Kırpma (Scissor Mode)
        Raylib.BeginScissorMode((int)vp.X + 1, (int)vp.Y + 1, (int)vp.Width - 2, (int)vp.Height - 2);

        float startX = vp.X + 6;
        float startY = vp.Y + 6 - scrollY;

        for (int i = 0; i < colors.Count; i++)
        {
            int col = i % columns;
            int row = i / columns;

            Rectangle boxRect = new Rectangle(
                startX + col * (boxSize + spacing),
                startY + row * (boxSize + spacing),
                boxSize,
                boxSize
            );

            Raylib.DrawRectangleRec(boxRect, colors[i]);
            Raylib.DrawRectangleLinesEx(boxRect, 1, Color.Black);

            // Seçili rengi vurgula
            if (colors[i].R == CurrentColor.R && colors[i].G == CurrentColor.G && colors[i].B == CurrentColor.B && colors[i].A == CurrentColor.A)
            {
                Raylib.DrawRectangleLinesEx(boxRect, 2, Color.Yellow);
            }
        }

        Raylib.EndScissorMode();

        // 4. Scrollbar (Sınırı aştığında görünür)
        if (maxScroll > 0)
        {
            float trackX = vp.X + vp.Width - 8;
            float trackHeight = vp.Height - 4;
            float thumbHeight = Math.Max(24f, (vp.Height / totalHeight) * trackHeight);
            float thumbY = vp.Y + 2 + (scrollY / maxScroll) * (trackHeight - thumbHeight);

            // Kanal
            Raylib.DrawRectangle((int)trackX, (int)vp.Y + 2, 6, (int)trackHeight, new Color(15, 15, 15, 200));

            // Kaydırıcı Başlık
            Color thumbColor = isDraggingScrollbar ? Color.SkyBlue : Color.Gray;
            Raylib.DrawRectangle((int)trackX, (int)thumbY, 6, (int)thumbHeight, thumbColor);
        }
    }
}