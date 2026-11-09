// === DOSYA: \Core\Selection\SelectionManager.UI.cs ===
using System;
using System.Numerics;
using Raylib_cs;
using PixelArtApp.UI;

namespace PixelArtApp.Core;

public partial class SelectionManager
{
    public void DrawWorldOverlay(Canvas canvas)
    {
        int scale = canvas.PixelScale;

        if (IsCreatingSelection)
        {
            int minX = Math.Min(createStartPos.x, createCurrentPos.x);
            int maxX = Math.Max(createStartPos.x, createCurrentPos.x);
            int minY = Math.Min(createStartPos.y, createCurrentPos.y);
            int maxY = Math.Max(createStartPos.y, createCurrentPos.y);

            if (LassoPath.Count > 1)
            {
                foreach (var (px, py) in LassoPath)
                    Raylib.DrawRectangle((int)canvas.Offset.X + px * scale, (int)canvas.Offset.Y + py * scale, scale, scale, new Color(0, 180, 255, 110));
            }
            else
            {
                Rectangle previewRect = new Rectangle(
                    canvas.Offset.X + minX * scale,
                    canvas.Offset.Y + minY * scale,
                    (maxX - minX + 1) * scale,
                    (maxY - minY + 1) * scale
                );
                Raylib.DrawRectangleRec(previewRect, new Color(0, 160, 255, 50));
                Raylib.DrawRectangleLinesEx(previewRect, 1.5f, Color.SkyBlue);
            }
            return;
        }

        if (!HasSelection) return;

        int bw = mask.GetLength(0), bh = mask.GetLength(1);
        int baseX = (int)Math.Round(Bounds.X + Offset.X);
        int baseY = (int)Math.Round(Bounds.Y + Offset.Y);

        if (IsFloating)
        {
            for (int x = 0; x < bw; x++)
            {
                for (int y = 0; y < bh; y++)
                {
                    if (mask[x, y] && floatingPixels[x, y].A > 0)
                    {
                        Raylib.DrawRectangle(
                            (int)canvas.Offset.X + (baseX + x) * scale,
                            (int)canvas.Offset.Y + (baseY + y) * scale,
                            scale, scale, floatingPixels[x, y]
                        );
                    }
                }
            }
        }

        bool animPhase = ((int)(Raylib.GetTime() * 6) % 2) == 0;
        Color borderCol1 = animPhase ? Color.White : Color.Black;
        Color borderCol2 = animPhase ? Color.Black : Color.Yellow;

        for (int x = 0; x < bw; x++)
        {
            for (int y = 0; y < bh; y++)
            {
                if (!mask[x, y]) continue;

                int rx = (int)canvas.Offset.X + (baseX + x) * scale;
                int ry = (int)canvas.Offset.Y + (baseY + y) * scale;

                Raylib.DrawRectangle(rx, ry, scale, scale, new Color(0, 140, 255, 30));

                if (y == 0 || !mask[x, y - 1]) Raylib.DrawLine(rx, ry, rx + scale, ry, borderCol1);
                if (y == bh - 1 || !mask[x, y + 1]) Raylib.DrawLine(rx, ry + scale, rx + scale, ry + scale, borderCol2);
                if (x == 0 || !mask[x - 1, y]) Raylib.DrawLine(rx, ry, rx, ry + scale, borderCol1);
                if (x == bw - 1 || !mask[x + 1, y]) Raylib.DrawLine(rx + scale, ry, rx + scale, ry + scale, borderCol2);
            }
        }
    }

    public Rectangle GetFloatingMenuBounds(Canvas canvas, Camera2D camera)
    {
        Rectangle anchorBounds = (floatingMenuState == 2 && rotBackupMask != null)
            ? new Rectangle(rotBackupBounds.X + rotBackupOffset.X, rotBackupBounds.Y + rotBackupOffset.Y, rotBackupBounds.Width, rotBackupBounds.Height)
            : GetCurrentBounds();

        Vector2 bottomCenterWorld = new Vector2(
            canvas.Offset.X + (anchorBounds.X + anchorBounds.Width / 2f) * canvas.PixelScale,
            canvas.Offset.Y + (anchorBounds.Y + anchorBounds.Height) * canvas.PixelScale
        );

        Vector2 screenAnchor = Raylib.GetWorldToScreen2D(bottomCenterWorld, camera);
        int menuWidth = floatingMenuState == 0 ? 385 : (floatingMenuState == 1 ? 165 : 375);
        int menuHeight = floatingMenuState == 2 ? 48 : 32;

        float minX = AppSettings.LeftPanelWidth + 6f;
        float maxX = Math.Max(minX, Raylib.GetScreenWidth() - AppSettings.RightPanelWidth - menuWidth - 6f);
        float minY = 76f;
        float maxY = Math.Max(minY, Raylib.GetScreenHeight() - menuHeight - 12f);

        float menuX = Math.Clamp(screenAnchor.X - menuWidth / 2f, minX, maxX);
        float menuY = Math.Clamp(screenAnchor.Y + 12, minY, maxY);

        return new Rectangle(menuX, menuY, menuWidth, menuHeight);
    }

    public bool CheckAndDrawFloatingMenu(Canvas canvas, Camera2D camera, Vector2 mouseScreenPos, HistoryManager history, Action<string> onStatus)
    {
        if (!HasSelection || IsCreatingSelection || IsDraggingFloating) return false;

        Rectangle menuRect = GetFloatingMenuBounds(canvas, camera);
        Raylib.DrawRectangleRec(menuRect, new Color(28, 28, 32, 245));
        Raylib.DrawRectangleLinesEx(menuRect, 1, Color.SkyBlue);

        float curX = menuRect.X + 6;
        bool interacted = Raylib.CheckCollisionPointRec(mouseScreenPos, menuRect);
        bool click = Raylib.IsMouseButtonPressed(MouseButton.Left);

        if (floatingMenuState == 0)
        {
            string[] labels = { "Kopyala", "Kes", "Cogalt", "Sil", "Yansit", "Dondur", "X" };
            int[] widths = { 58, 40, 52, 38, 55, 60, 28 };
            Color[] colors = {
                new Color(50, 80, 130, 255), new Color(120, 80, 40, 255),
                new Color(45, 110, 75, 255), new Color(130, 45, 45, 255),
                new Color(60, 60, 80, 255), new Color(60, 60, 80, 255),
                new Color(70, 70, 70, 255)
            };

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle btnRect = new Rectangle(curX, menuRect.Y + 4, widths[i], 24);
                if (UIWidgets.DrawColorButton(btnRect, labels[i], colors[i], mouseScreenPos, 12))
                {
                    if (i == 0) { Copy(canvas); onStatus("Secim kopyalandi (Ctrl+C)"); }
                    else if (i == 1) { Cut(canvas, history); onStatus("Secim kesildi (Ctrl+X)"); }
                    else if (i == 2) { Duplicate(canvas, history); onStatus("Secim cogaltildi (Ctrl+D)"); }
                    else if (i == 3) { DeleteSelection(canvas, history); onStatus("Secim silindi (Del)"); }
                    else if (i == 4) floatingMenuState = 1;
                    else if (i == 5) { BeginRotationSession(canvas, history); floatingMenuState = 2; }
                    else if (i == 6) { CancelOrCommit(canvas); onStatus("Secim tamamlandi"); }
                    return true;
                }
                curX += widths[i] + 6;
            }
        }
        else if (floatingMenuState == 1)
        {
            string[] labels = { "Yatay", "Dikey", "Geri" };
            int[] widths = { 50, 50, 40 };

            for (int i = 0; i < labels.Length; i++)
            {
                Rectangle btnRect = new Rectangle(curX, menuRect.Y + 4, widths[i], 24);
                if (UIWidgets.DrawColorButton(btnRect, labels[i], new Color(60, 60, 80, 255), mouseScreenPos, 12))
                {
                    if (i == 0) { FlipHorizontal(canvas, history); onStatus("Yatay yansitildi."); floatingMenuState = 0; }
                    else if (i == 1) { FlipVertical(canvas, history); onStatus("Dikey yansitildi."); floatingMenuState = 0; }
                    else if (i == 2) floatingMenuState = 0;
                    return true;
                }
                curX += widths[i] + 6;
            }
        }
        else if (floatingMenuState == 2)
        {
            if (DrawRotationSubMenu(curX, menuRect.Y, mouseScreenPos, click, onStatus))
                return true;
        }

        return (interacted && click) || IsDraggingDial;
    }

    private bool DrawRotationSubMenu(float curX, float menuY, Vector2 mouseScreenPos, bool click, Action<string> onStatus)
    {
        Vector2 dialCenter = new Vector2(curX + 20, menuY + 24);
        float dialRadius = 18f;
        bool hoverDial = Vector2.Distance(mouseScreenPos, dialCenter) <= dialRadius + 4;

        if (click && hoverDial)
        {
            IsDraggingDial = true;
            isRotateInputActive = false;
        }

        if (IsDraggingDial)
        {
            if (Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                float dx = mouseScreenPos.X - dialCenter.X;
                float dy = mouseScreenPos.Y - dialCenter.Y;
                float deg = MathF.Atan2(dy, dx) * (180f / MathF.PI) + 90f;
                if (deg > 180f) deg -= 360f;
                if (deg < -180f) deg += 360f;

                if (Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift))
                    deg = MathF.Round(deg / 15f) * 15f;
                else
                    deg = MathF.Round(deg);

                if (MathF.Abs(deg - currentRotationAngle) >= 0.5f)
                {
                    currentRotationAngle = deg;
                    rotateAngleText = ((int)currentRotationAngle).ToString();
                    ApplyLiveRotation(currentRotationAngle);
                }
            }
            else IsDraggingDial = false;
        }

        Raylib.DrawCircleV(dialCenter, dialRadius, new Color(40, 40, 48, 255));
        Raylib.DrawCircleLines((int)dialCenter.X, (int)dialCenter.Y, dialRadius, IsDraggingDial || hoverDial ? Color.Yellow : Color.SkyBlue);

        for (int a = 0; a < 360; a += 90)
        {
            float r = a * (MathF.PI / 180f);
            Vector2 p1 = new Vector2(dialCenter.X + MathF.Cos(r) * (dialRadius - 3), dialCenter.Y + MathF.Sin(r) * (dialRadius - 3));
            Vector2 p2 = new Vector2(dialCenter.X + MathF.Cos(r) * dialRadius, dialCenter.Y + MathF.Sin(r) * dialRadius);
            Raylib.DrawLineV(p1, p2, Color.Gray);
        }

        float radAngle = (currentRotationAngle - 90f) * (MathF.PI / 180f);
        Vector2 knobPos = new Vector2(dialCenter.X + MathF.Cos(radAngle) * (dialRadius - 4), dialCenter.Y + MathF.Sin(radAngle) * (dialRadius - 4));
        Raylib.DrawLineEx(dialCenter, knobPos, 2f, Color.Yellow);
        Raylib.DrawCircleV(knobPos, 3.5f, Color.Red);
        Raylib.DrawCircleV(dialCenter, 2f, Color.White);

        curX += 46;

        Rectangle inputRect = new Rectangle(curX, menuY + 12, 46, 24);
        if (click) isRotateInputActive = Raylib.CheckCollisionPointRec(mouseScreenPos, inputRect);

        if (isRotateInputActive) HandleRotationTextInput();

        Raylib.DrawRectangleRec(inputRect, isRotateInputActive ? new Color(15, 15, 20, 255) : new Color(40, 40, 45, 255));
        Raylib.DrawRectangleLinesEx(inputRect, 1, isRotateInputActive ? Color.Yellow : Color.Gray);
        string displayAngle = rotateAngleText + (isRotateInputActive && (int)(Raylib.GetTime() * 3) % 2 == 0 ? "|" : "");
        Raylib.DrawText(displayAngle, (int)inputRect.X + 5, (int)inputRect.Y + 6, 12, isRotateInputActive ? Color.Yellow : Color.White);

        curX += 52;

        string[] labels = { "-90", "90", "180", "Onayla", "Iptal" };
        int[] widths = { 34, 30, 34, 52, 42 };

        for (int i = 0; i < labels.Length; i++)
        {
            Rectangle btnRect = new Rectangle(curX, menuY + 12, widths[i], 24);
            Color bg = i == 3 ? new Color(45, 110, 75, 255) : (i == 4 ? new Color(120, 45, 45, 255) : new Color(60, 60, 80, 255));

            if (UIWidgets.DrawColorButton(btnRect, labels[i], bg, mouseScreenPos, 12))
            {
                if (i == 0) { currentRotationAngle = -90f; rotateAngleText = "-90"; ApplyLiveRotation(-90f); }
                else if (i == 1) { currentRotationAngle = 90f; rotateAngleText = "90"; ApplyLiveRotation(90f); }
                else if (i == 2) { currentRotationAngle = 180f; rotateAngleText = "180"; ApplyLiveRotation(180f); }
                else if (i == 3)
                {
                    ConfirmRotationSession();
                    onStatus($"{currentRotationAngle} derece donduruldu.");
                }
                else if (i == 4)
                {
                    CancelRotationSession();
                    onStatus("Dondurme iptal edildi.");
                }
                return true;
            }
            curX += widths[i] + 5;
        }

        return false;
    }

    private void HandleRotationTextInput()
    {
        bool textChanged = false;
        int key = Raylib.GetCharPressed();
        while (key > 0)
        {
            if ((char.IsDigit((char)key) || key == '-') && rotateAngleText.Length < 5)
            {
                if (key == '-')
                {
                    rotateAngleText = rotateAngleText.StartsWith("-") ? rotateAngleText[1..] : "-" + rotateAngleText;
                    textChanged = true;
                }
                else
                {
                    rotateAngleText = (rotateAngleText == "0") ? ((char)key).ToString() : rotateAngleText + (char)key;
                    textChanged = true;
                }
            }
            key = Raylib.GetCharPressed();
        }
        if (Raylib.IsKeyPressed(KeyboardKey.Backspace) && rotateAngleText.Length > 0)
        {
            rotateAngleText = rotateAngleText[..^1];
            textChanged = true;
        }

        if (textChanged)
        {
            if (float.TryParse(rotateAngleText, out float parsedAngle))
            {
                currentRotationAngle = Math.Clamp(parsedAngle, -360f, 360f);
                ApplyLiveRotation(currentRotationAngle);
            }
            else if (string.IsNullOrEmpty(rotateAngleText) || rotateAngleText == "-")
            {
                currentRotationAngle = 0f;
                ApplyLiveRotation(0f);
            }
        }
    }
}