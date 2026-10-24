using System;
using System.Globalization;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;
using Raylib_cs;

namespace PixelArtApp.UI;

public class ColorPickerDialog
{
    public bool IsOpen { get; private set; } = false;

    private Color initialColor;
    private Color currentColor;

    private float hue = 0f;
    private float sat = 1f;
    private float val = 1f;
    private int alpha = 255;

    private Vector2 svHandlePos;

    // HEX Metin Kutusu
    private string hexText = "";
    private bool hexInputActive = false;
    private bool isTextSelected = false;
    private float backspaceHoldTimer = 0f;

    // Pencere ve Sürükleme Durumları
    private Rectangle windowRect = new Rectangle(200, 150, 465, 350);
    private bool isDraggingWindow = false;
    private bool isDraggingSV = false;
    private bool isDraggingHue = false;
    private bool isDraggingVal = false;
    private bool isDraggingAlpha = false;
    private Vector2 dragWindowOffset;

    private RenderTexture2D svTexture;
    private bool textureInitialized = false;

    private void InitTexture()
    {
        if (!textureInitialized)
        {
            svTexture = Raylib.LoadRenderTexture(180, 180);
            textureInitialized = true;
        }
    }

    public void Open(Color startingColor)
    {
        InitTexture();

        initialColor = startingColor;
        currentColor = startingColor;
        alpha = startingColor.A;

        RgbToHsv(startingColor, out hue, out sat, out val);
        svHandlePos = new Vector2(sat * 180f, (1f - val) * 180f);

        SyncHex();
        UpdateSvTexture();

        int sw = Raylib.GetScreenWidth();
        int sh = Raylib.GetScreenHeight();
        windowRect.X = (sw - windowRect.Width) / 2f;
        windowRect.Y = (sh - windowRect.Height) / 2f;

        IsOpen = true;
        ResetDragStates();
    }

    public void Close()
    {
        IsOpen = false;
        ResetDragStates();
    }

    private void ResetDragStates()
    {
        isDraggingWindow = false;
        isDraggingSV = false;
        isDraggingHue = false;
        isDraggingVal = false;
        isDraggingAlpha = false;
        hexInputActive = false;
        isTextSelected = false;
    }

    public bool Update(Vector2 mousePos, Action<Color, bool> onConfirm)
    {
        if (!IsOpen) return false;

        Rectangle titleBar = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 32);
        Rectangle svBox = new Rectangle(windowRect.X + 20, windowRect.Y + 45, 180, 180);
        Rectangle hueBar = new Rectangle(windowRect.X + 210, windowRect.Y + 45, 20, 180);
        Rectangle valBar = new Rectangle(windowRect.X + 238, windowRect.Y + 45, 20, 180);
        Rectangle alphaBar = new Rectangle(windowRect.X + 20, windowRect.Y + 235, 180, 16);
        Rectangle alphaHitBox = new Rectangle(alphaBar.X, alphaBar.Y - 4, alphaBar.Width, 24);
        Rectangle hexBox = new Rectangle(windowRect.X + 275, windowRect.Y + 143, 160, 30);

        // 1. TIKLAMA BAŞLANGICI
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, titleBar))
            {
                isDraggingWindow = true;
                dragWindowOffset = new Vector2(mousePos.X - windowRect.X, mousePos.Y - windowRect.Y);
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, svBox))
            {
                isDraggingSV = true;
                hexInputActive = false;
                isTextSelected = false;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, hueBar))
            {
                isDraggingHue = true;
                hexInputActive = false;
                isTextSelected = false;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, valBar))
            {
                isDraggingVal = true;
                hexInputActive = false;
                isTextSelected = false;
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, alphaHitBox))
            {
                isDraggingAlpha = true;
                hexInputActive = false;
                isTextSelected = false;
            }
            else
            {
                bool clickedHex = Raylib.CheckCollisionPointRec(mousePos, hexBox);

                if (hexInputActive && !clickedHex)
                {
                    if (hexText.Length > 0 && hexText.Length != 3 && hexText.Length < 6)
                    {
                        hexText = hexText.PadRight(6, '0');
                    }
                    ApplyHex(true);
                    SyncHex();
                }

                if (clickedHex && !hexInputActive) isTextSelected = true;
                else if (!clickedHex) isTextSelected = false;
                hexInputActive = clickedHex;
            }
        }

        // 2. SÜRÜKLEME
        if (Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            if (isDraggingWindow)
            {
                windowRect.X = Math.Clamp(mousePos.X - dragWindowOffset.X, 0, Raylib.GetScreenWidth() - windowRect.Width);
                windowRect.Y = Math.Clamp(mousePos.Y - dragWindowOffset.Y, 0, Raylib.GetScreenHeight() - windowRect.Height);
            }
            else if (isDraggingSV)
            {
                svHandlePos.X = Math.Clamp(mousePos.X - svBox.X, 0, 180);
                svHandlePos.Y = Math.Clamp(mousePos.Y - svBox.Y, 0, 180);
                sat = svHandlePos.X / 180f;
                val = 1f - (svHandlePos.Y / 180f);
                currentColor = HsvToRgb(hue, sat, val, alpha);
                SyncHex();
            }
            else if (isDraggingHue)
            {
                float relativeY = Math.Clamp(mousePos.Y - hueBar.Y, 0, 180);
                float newHue = (relativeY / 180f) * 360f;
                if (newHue >= 360f) newHue = 359.9f;

                if (Math.Abs(newHue - hue) > 0.5f)
                {
                    hue = newHue;
                    UpdateSvTexture();
                    currentColor = HsvToRgb(hue, sat, val, alpha);
                    SyncHex();
                }
            }
            else if (isDraggingVal)
            {
                // Yukarıdan aşağı çekildikçe parlaklığı (val) 1.0 -> 0.0 arası değiştirir
                float relativeY = Math.Clamp(mousePos.Y - valBar.Y, 0, 180);
                val = 1f - (relativeY / 180f);
                svHandlePos.Y = relativeY;
                currentColor = HsvToRgb(hue, sat, val, alpha);
                SyncHex();
            }
            else if (isDraggingAlpha)
            {
                float alphaRatio = Math.Clamp((mousePos.X - alphaBar.X) / alphaBar.Width, 0f, 1f);
                alpha = (int)Math.Round(alphaRatio * 255f);
                currentColor.A = (byte)alpha;
            }
        }
        else
        {
            isDraggingWindow = false;
            isDraggingSV = false;
            isDraggingHue = false;
            isDraggingVal = false;
            isDraggingAlpha = false;
        }

        // 3. HEX Klavye Girdisi
        if (hexInputActive)
        {
            bool isCtrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);

            if (isCtrl && Raylib.IsKeyPressed(KeyboardKey.A)) isTextSelected = true;
            if (isCtrl && Raylib.IsKeyPressed(KeyboardKey.C)) SetClipboardSafe($"#{hexText}");
            if (isCtrl && Raylib.IsKeyPressed(KeyboardKey.V))
            {
                string clipboard = GetClipboardSafe();
                if (!string.IsNullOrEmpty(clipboard))
                {
                    clipboard = clipboard.Trim().TrimStart('#');
                    string filtered = "";
                    foreach (char c in clipboard)
                    {
                        if (char.IsAsciiHexDigit(c) && filtered.Length < 6) filtered += char.ToUpper(c);
                    }
                    if (filtered.Length > 0)
                    {
                        if (filtered.Length != 3 && filtered.Length < 6)
                        {
                            filtered = filtered.PadRight(6, filtered[^1]);
                        }

                        hexText = filtered;
                        isTextSelected = false;
                        ApplyHex(true);
                    }
                }
            }

            int key = Raylib.GetCharPressed();
            while (key > 0)
            {
                char c = (char)key;
                if (char.IsAsciiHexDigit(c))
                {
                    if (isTextSelected)
                    {
                        hexText = "";
                        isTextSelected = false;
                    }
                    if (hexText.Length < 6)
                    {
                        hexText += char.ToUpper(c);
                        ApplyHex();
                    }
                }
                key = Raylib.GetCharPressed();
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Backspace))
            {
                if (isTextSelected)
                {
                    hexText = "";
                    isTextSelected = false;
                    ApplyHex();
                }
                else if (hexText.Length > 0)
                {
                    hexText = hexText[..^1];
                    backspaceHoldTimer = 0.35f;
                    ApplyHex();
                }
            }
            else if (Raylib.IsKeyDown(KeyboardKey.Backspace) && !isTextSelected && hexText.Length > 0)
            {
                backspaceHoldTimer -= Raylib.GetFrameTime();
                if (backspaceHoldTimer <= 0)
                {
                    hexText = hexText[..^1];
                    backspaceHoldTimer = 0.05f;
                    ApplyHex();
                }
            }

            if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressed(KeyboardKey.Escape))
            {
                hexInputActive = false;
                isTextSelected = false;

                if (hexText.Length > 0 && hexText.Length != 3 && hexText.Length < 6)
                {
                    hexText = hexText.PadRight(6, '0');
                }
                ApplyHex(true);
                SyncHex();
            }
        }

        // 4. BUTONLAR
        Rectangle btnApply = new Rectangle(windowRect.X + 20, windowRect.Y + 295, 115, 34);
        Rectangle btnAddPalette = new Rectangle(windowRect.X + 145, windowRect.Y + 295, 160, 34);
        Rectangle btnCancel = new Rectangle(windowRect.X + 315, windowRect.Y + 295, 125, 34);
        Rectangle btnClose = new Rectangle(windowRect.X + windowRect.Width - 28, windowRect.Y + 4, 24, 24);

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            if (Raylib.CheckCollisionPointRec(mousePos, btnApply))
            {
                onConfirm(currentColor, false);
                Close();
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, btnAddPalette))
            {
                onConfirm(currentColor, true);
                Close();
            }
            else if (Raylib.CheckCollisionPointRec(mousePos, btnCancel) || Raylib.CheckCollisionPointRec(mousePos, btnClose))
            {
                Close();
            }
        }

        return true;
    }

    private string GetClipboardSafe()
    {
        string clipboardText = "";
        try
        {
            Thread staThread = new Thread(() =>
            {
                try
                {
                    if (System.Windows.Forms.Clipboard.ContainsText())
                    {
                        clipboardText = System.Windows.Forms.Clipboard.GetText();
                    }
                }
                catch { }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join(200);
        }
        catch { }
        return clipboardText;
    }

    private void SetClipboardSafe(string text)
    {
        try
        {
            Thread staThread = new Thread(() =>
            {
                try { System.Windows.Forms.Clipboard.SetText(text); } catch { }
            });
            staThread.SetApartmentState(ApartmentState.STA);
            staThread.Start();
            staThread.Join(200);
        }
        catch { }
    }

    private void UpdateSvTexture()
    {
        Raylib.BeginTextureMode(svTexture);
        Color pureHue = HsvToRgb(hue, 1f, 1f, 255);
        Raylib.DrawRectangleGradientH(0, 0, 180, 180, Color.White, pureHue);
        Raylib.DrawRectangleGradientV(0, 0, 180, 180, Color.Blank, Color.Black);
        Raylib.EndTextureMode();
    }

    private void SyncHex()
    {
        hexText = $"{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}";
    }

    private void ApplyHex(bool forceSpectrumUpdate = false)
    {
        string parseStr = hexText;

        if (parseStr.Length == 0)
        {
            parseStr = "000000";
        }
        else if (parseStr.Length == 3)
        {
            parseStr = $"{parseStr[0]}{parseStr[0]}{parseStr[1]}{parseStr[1]}{parseStr[2]}{parseStr[2]}";
        }
        else if (parseStr.Length < 6)
        {
            parseStr = parseStr.PadRight(6, '0');
        }

        if (uint.TryParse(parseStr, NumberStyles.HexNumber, null, out uint valHex))
        {
            int r = (int)((valHex >> 16) & 0xFF);
            int g = (int)((valHex >> 8) & 0xFF);
            int b = (int)(valHex & 0xFF);

            currentColor = new Color((byte)r, (byte)g, (byte)b, (byte)alpha);

            if (forceSpectrumUpdate || hexText.Length == 3 || hexText.Length == 6)
            {
                RgbToHsv(currentColor, out float newHue, out sat, out this.val);

                if (sat > 0.001f)
                {
                    hue = newHue;
                }

                svHandlePos = new Vector2(sat * 180f, (1f - this.val) * 180f);
                UpdateSvTexture();
            }
        }
    }

    public void Draw()
    {
        if (!IsOpen) return;

        Raylib.DrawRectangleRec(windowRect, new Color(30, 30, 30, 255));
        Raylib.DrawRectangleLinesEx(windowRect, 1, Color.Gray);

        Rectangle titleBar = new Rectangle(windowRect.X, windowRect.Y, windowRect.Width, 32);
        Raylib.DrawRectangleRec(titleBar, new Color(42, 42, 42, 255));
        Raylib.DrawLine((int)windowRect.X, (int)(windowRect.Y + 32), (int)(windowRect.X + windowRect.Width), (int)(windowRect.Y + 32), Color.DarkGray);
        Raylib.DrawText("Renk Paleti ve Spektrum", (int)windowRect.X + 12, (int)windowRect.Y + 8, 15, Color.RayWhite);

        Raylib.DrawText("X", (int)windowRect.X + (int)windowRect.Width - 20, (int)windowRect.Y + 8, 16, Color.LightGray);

        // Spektrum
        Rectangle src = new Rectangle(0, 0, svTexture.Texture.Width, -svTexture.Texture.Height);
        Rectangle dst = new Rectangle(windowRect.X + 20, windowRect.Y + 45, 180, 180);
        Raylib.DrawTexturePro(svTexture.Texture, src, dst, Vector2.Zero, 0f, Color.White);
        Raylib.DrawRectangleLinesEx(dst, 1, Color.DarkGray);

        Vector2 handleCenter = new Vector2(dst.X + svHandlePos.X, dst.Y + svHandlePos.Y);
        Raylib.DrawCircleLines((int)handleCenter.X, (int)handleCenter.Y, 5, Color.Black);
        Raylib.DrawCircleLines((int)handleCenter.X, (int)handleCenter.Y, 4, Color.White);

        // Hue Bar
        Rectangle hueBar = new Rectangle(windowRect.X + 210, windowRect.Y + 45, 20, 180);
        DrawHueBar(hueBar);
        Raylib.DrawRectangleLinesEx(hueBar, 1, Color.DarkGray);

        float hueMarkerY = hueBar.Y + (hue / 360f) * 180f;
        Raylib.DrawRectangle((int)hueBar.X - 2, (int)hueMarkerY - 2, (int)hueBar.Width + 4, 4, Color.White);
        Raylib.DrawRectangleLines((int)hueBar.X - 2, (int)hueMarkerY - 2, (int)hueBar.Width + 4, 4, Color.Black);

        // Karanlık / Parlaklık (Value) Barı (Yukarıdan Aşağıya: Parlak -> Siyah)
        Rectangle valBar = new Rectangle(windowRect.X + 238, windowRect.Y + 45, 20, 180);
        Color topBrightColor = HsvToRgb(hue, sat, 1f, 255);
        Raylib.DrawRectangleGradientV((int)valBar.X, (int)valBar.Y, (int)valBar.Width, (int)valBar.Height, topBrightColor, Color.Black);
        Raylib.DrawRectangleLinesEx(valBar, 1, Color.DarkGray);

        float valMarkerY = valBar.Y + (1f - val) * 180f;
        Raylib.DrawRectangle((int)valBar.X - 2, (int)valMarkerY - 2, (int)valBar.Width + 4, 4, Color.White);
        Raylib.DrawRectangleLines((int)valBar.X - 2, (int)valMarkerY - 2, (int)valBar.Width + 4, 4, Color.Black);

        // Bilgiler
        int infoX = (int)windowRect.X + 275;
        int previewY = (int)windowRect.Y + 45;

        Raylib.DrawText("Eski", infoX, previewY, 12, Color.LightGray);
        Raylib.DrawRectangle(infoX, previewY + 16, 75, 30, initialColor);
        Raylib.DrawRectangleLines(infoX, previewY + 16, 75, 30, Color.DarkGray);

        Raylib.DrawText("Yeni", infoX + 85, previewY, 12, Color.LightGray);
        Raylib.DrawRectangle(infoX + 85, previewY + 16, 75, 30, currentColor);
        Raylib.DrawRectangleLines(infoX + 85, previewY + 16, 75, 30, Color.DarkGray);

        Raylib.DrawText($"R: {currentColor.R,-3} G: {currentColor.G,-3} B: {currentColor.B,-3}", infoX, previewY + 56, 13, Color.RayWhite);
        Raylib.DrawText($"Parlaklik: %{(int)Math.Round(val * 100)}", infoX, previewY + 74, 12, Color.LightGray);

        // HEX Kutusu
        Raylib.DrawText("HEX Kodu:", infoX, previewY + 95, 12, Color.LightGray);
        Rectangle hexBox = new Rectangle(infoX, previewY + 112, 160, 30);
        Color hexBoxBg = hexInputActive ? new Color(18, 18, 22, 255) : new Color(45, 45, 45, 255);
        Raylib.DrawRectangleRec(hexBox, hexBoxBg);
        Raylib.DrawRectangleLinesEx(hexBox, hexInputActive ? 2 : 1, hexInputActive ? Color.SkyBlue : Color.Gray);

        if (isTextSelected && hexText.Length > 0)
        {
            int textWidth = Raylib.MeasureText("#" + hexText, 16);
            Raylib.DrawRectangle((int)hexBox.X + 8, (int)hexBox.Y + 5, textWidth + 4, 20, new Color(40, 90, 180, 200));
        }

        string displayHex = "#" + hexText;
        if (hexInputActive && !isTextSelected && (int)(Raylib.GetTime() * 2.5) % 2 == 0)
        {
            displayHex += "|";
        }
        Raylib.DrawText(displayHex, (int)hexBox.X + 10, (int)hexBox.Y + 7, 16, hexInputActive ? Color.Yellow : Color.RayWhite);

        // Opaklık Barı
        Rectangle alphaBar = new Rectangle(windowRect.X + 20, windowRect.Y + 235, 180, 16);
        Raylib.DrawText($"Opaklik: {alpha} (%{(int)Math.Round(alpha / 255f * 100)})", (int)windowRect.X + 20, (int)windowRect.Y + 256, 12, Color.LightGray);
        Raylib.DrawRectangleRec(alphaBar, new Color(20, 20, 20, 255));
        Raylib.DrawRectangle((int)alphaBar.X, (int)alphaBar.Y, (int)(alphaBar.Width * (alpha / 255f)), (int)alphaBar.Height, Color.White);
        Raylib.DrawRectangleLinesEx(alphaBar, 1, Color.DarkGray);

        // Butonlar
        Vector2 mousePos = Raylib.GetMousePosition();
        DrawButton(new Rectangle(windowRect.X + 20, windowRect.Y + 295, 115, 34), "Sec", new Color(50, 90, 150, 255), mousePos);
        DrawButton(new Rectangle(windowRect.X + 145, windowRect.Y + 295, 160, 34), "+ Palete Ekle", new Color(45, 120, 75, 255), mousePos);
        DrawButton(new Rectangle(windowRect.X + 315, windowRect.Y + 295, 125, 34), "Iptal", new Color(110, 45, 45, 255), mousePos);
    }

    private void DrawHueBar(Rectangle rect)
    {
        Color[] rainbow = new Color[]
        {
            new Color(255, 0, 0, 255),
            new Color(255, 255, 0, 255),
            new Color(0, 255, 0, 255),
            new Color(0, 255, 255, 255),
            new Color(0, 0, 255, 255),
            new Color(255, 0, 255, 255),
            new Color(255, 0, 0, 255)
        };

        float segH = rect.Height / 6f;
        for (int i = 0; i < 6; i++)
        {
            Raylib.DrawRectangleGradientV((int)rect.X, (int)(rect.Y + i * segH), (int)rect.Width, (int)Math.Ceiling(segH), rainbow[i], rainbow[i + 1]);
        }
    }

    private void DrawButton(Rectangle rect, string text, Color baseColor, Vector2 mousePos)
    {
        bool hover = Raylib.CheckCollisionPointRec(mousePos, rect);
        Color col = hover ? new Color((byte)Math.Min(255, baseColor.R + 25), (byte)Math.Min(255, baseColor.G + 25), (byte)Math.Min(255, baseColor.B + 25), baseColor.A) : baseColor;
        Raylib.DrawRectangleRec(rect, col);
        Raylib.DrawRectangleLinesEx(rect, 1, Color.Gray);

        int tw = Raylib.MeasureText(text, 13);
        Raylib.DrawText(text, (int)(rect.X + (rect.Width - tw) / 2), (int)(rect.Y + (rect.Height - 13) / 2), 13, Color.RayWhite);
    }

    private static void RgbToHsv(Color color, out float h, out float s, out float v)
    {
        float r = color.R / 255f;
        float g = color.G / 255f;
        float b = color.B / 255f;

        float max = Math.Max(r, Math.Max(g, b));
        float min = Math.Min(r, Math.Min(g, b));
        float delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0) h = 0;
        else if (max == r) h = 60 * (((g - b) / delta) % 6);
        else if (max == g) h = 60 * (((b - r) / delta) + 2);
        else h = 60 * (((r - g) / delta) + 4);

        if (h < 0) h += 360f;
    }

    private static Color HsvToRgb(float h, float s, float v, int a)
    {
        float c = v * s;
        float x = c * (1 - Math.Abs((h / 60f % 2) - 1));
        float m = v - c;

        float r = 0, g = 0, b = 0;
        if (h < 60) { r = c; g = x; b = 0; }
        else if (h < 120) { r = x; g = c; b = 0; }
        else if (h < 180) { r = 0; g = c; b = x; }
        else if (h < 240) { r = 0; g = x; b = c; }
        else if (h < 300) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        return new Color((byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255), (byte)a);
    }
}