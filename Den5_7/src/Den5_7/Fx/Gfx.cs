using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Элемент со сплошной поверхностью: дочерние элементы закрашивают свои углы её цветом.</summary>
    public interface ISurface
    {
        Color SurfaceColor { get; }
    }

    /// <summary>Элемент, лежащий прямо на «живом» фоне и обновляющийся вместе с ним.</summary>
    public interface IBackdropChild
    {
        void BackdropTick();
    }

    /// <summary>Элемент с анимацией появления (проигрывается при переходе на страницу).</summary>
    public interface IEnterAnim
    {
        void PrepareEnter();
        void PlayEnter(int delayMs);
    }

    /// <summary>Вспомогательные функции рисования.</summary>
    public static class Gfx
    {
        private static readonly Bitmap measureBmp = new Bitmap(1, 1);
        private static readonly Graphics measureG = Graphics.FromImage(measureBmp);

        public static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t <= 0) return a;
            if (t >= 1) return b;
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color A(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }

        public static Color A(Color c, float k)
        {
            return A(c, (int)(c.A * Math.Max(0f, Math.Min(1f, k))));
        }

        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * t; }

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var b = new SolidBrush(c))
            using (GraphicsPath p = Round(r, radius))
                g.FillPath(b, p);
        }

        public static void FillRound(Graphics g, Brush b, RectangleF r, float radius)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (GraphicsPath p = Round(r, radius))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Color c, RectangleF r, float radius, float width = 1f)
        {
            if (r.Width <= 0 || r.Height <= 0) return;
            using (var pen = new Pen(c, width))
            using (GraphicsPath p = Round(r, radius))
                g.DrawPath(pen, p);
        }

        /// <summary>Мягкое свечение вокруг скруглённого прямоугольника.</summary>
        public static void Glow(Graphics g, RectangleF r, float radius, Color c, int layers = 6, float spread = 2f, int alpha = 26)
        {
            for (int i = layers; i >= 1; i--)
            {
                float d = i * spread;
                RectangleF rr = RectangleF.Inflate(r, d, d);
                FillRound(g, A(c, alpha * (layers - i + 1) / layers / 2 + 2), rr, radius + d);
            }
        }

        /// <summary>Фирменный градиент акцента (фиолетовый → бирюзовый).</summary>
        public static LinearGradientBrush AccentBrush(RectangleF r, float angle = 0f)
        {
            if (r.Width < 1) r.Width = 1;
            if (r.Height < 1) r.Height = 1;
            return new LinearGradientBrush(r, Theme.P.Accent, Theme.P.Accent2, angle);
        }

        public static LinearGradientBrush TriBrush(RectangleF r, float angle = 0f)
        {
            if (r.Width < 1) r.Width = 1;
            if (r.Height < 1) r.Height = 1;
            var b = new LinearGradientBrush(r, Theme.P.Accent, Theme.P.Accent3, angle);
            var cb = new ColorBlend(3);
            cb.Colors = new[] { Theme.P.Accent, Theme.P.Accent2, Theme.P.Accent3 };
            cb.Positions = new[] { 0f, 0.55f, 1f };
            b.InterpolationColors = cb;
            return b;
        }

        private static StringFormat Format(StringAlignment h, StringAlignment v, bool wrap)
        {
            var sf = new StringFormat(StringFormat.GenericDefault);
            sf.Alignment = h;
            sf.LineAlignment = v;
            sf.Trimming = StringTrimming.EllipsisCharacter;
            if (!wrap) sf.FormatFlags |= StringFormatFlags.NoWrap;
            return sf;
        }

        public static void Text(Graphics g, string s, Font f, Color c, RectangleF r,
                                StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center, bool wrap = false)
        {
            if (string.IsNullOrEmpty(s) || r.Width <= 0 || r.Height <= 0) return;
            using (var b = new SolidBrush(c))
            using (StringFormat sf = Format(h, v, wrap))
                g.DrawString(s, f, b, r, sf);
        }

        public static void Text(Graphics g, string s, Font f, Brush b, RectangleF r,
                                StringAlignment h = StringAlignment.Near, StringAlignment v = StringAlignment.Center, bool wrap = false)
        {
            if (string.IsNullOrEmpty(s) || r.Width <= 0 || r.Height <= 0) return;
            using (StringFormat sf = Format(h, v, wrap))
                g.DrawString(s, f, b, r, sf);
        }

        public static SizeF Measure(string s, Font f, float width = 0)
        {
            if (string.IsNullOrEmpty(s)) return SizeF.Empty;
            lock (measureG)
            {
                using (StringFormat sf = Format(StringAlignment.Near, StringAlignment.Near, width > 0))
                    return measureG.MeasureString(s, f, width > 0 ? (int)width : 100000, sf);
            }
        }

        private static readonly System.Collections.Generic.Dictionary<Font, float> padCache = new System.Collections.Generic.Dictionary<Font, float>();

        /// <summary>Служебный отступ, который GDI+ добавляет к ширине строки при измерении (зависит от шрифта).</summary>
        public static float TextPad(Font f)
        {
            float pad;
            if (padCache.TryGetValue(f, out pad)) return pad;
            pad = Math.Max(0, 2 * Measure("н", f).Width - Measure("нн", f).Width);
            padCache[f] = pad;
            return pad;
        }

        /// <summary>Ширина строки без служебного отступа.</summary>
        public static float TextWidth(string s, Font f)
        {
            return string.IsNullOrEmpty(s) ? 0 : Math.Max(0, Measure(s, f).Width - TextPad(f));
        }

        /// <summary>Закраска области элемента тем, что находится под ним: цветом карточки или «живым» фоном.</summary>
        public static void PaintParentBg(Control c, Graphics g, Rectangle rect)
        {
            for (Control p = c.Parent; p != null; p = p.Parent)
            {
                var s = p as ISurface;
                if (s != null)
                {
                    using (var b = new SolidBrush(s.SurfaceColor)) g.FillRectangle(b, rect);
                    return;
                }
            }
            Backdrop.Paint(g, c, rect);
        }

        /// <summary>true, если элемент лежит на «живом» фоне (над ним нет карточки).</summary>
        public static bool IsOnBackdrop(Control c)
        {
            for (Control p = c.Parent; p != null; p = p.Parent)
                if (p is ISurface) return false;
            return true;
        }

        public static Bitmap Snapshot(Control c)
        {
            int w = Math.Max(1, c.Width), h = Math.Max(1, c.Height);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
            try { c.DrawToBitmap(bmp, new Rectangle(0, 0, w, h)); }
            catch (Exception) { }
            return bmp;
        }

        public static void DrawImageAlpha(Graphics g, Image img, float x, float y, float alpha)
        {
            if (img == null || alpha <= 0.003f) return;
            if (alpha >= 0.997f)
            {
                g.DrawImage(img, x, y, img.Width, img.Height);
                return;
            }
            var cm = new ColorMatrix { Matrix33 = alpha };
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                var dest = new[] { new PointF(x, y), new PointF(x + img.Width, y), new PointF(x, y + img.Height) };
                g.DrawImage(img, dest, new RectangleF(0, 0, img.Width, img.Height), GraphicsUnit.Pixel, ia);
            }
        }

        /// <summary>Часть изображения в заданный прямоугольник с прозрачностью.</summary>
        public static void DrawImageAlpha(Graphics g, Image img, RectangleF dest, RectangleF src, float alpha)
        {
            if (img == null || alpha <= 0.003f) return;
            var cm = new ColorMatrix { Matrix33 = Math.Min(1f, alpha) };
            using (var ia = new ImageAttributes())
            {
                ia.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                var pts = new[] { new PointF(dest.X, dest.Y), new PointF(dest.Right, dest.Y), new PointF(dest.X, dest.Bottom) };
                g.DrawImage(img, pts, src, GraphicsUnit.Pixel, ia);
            }
        }
    }

    /// <summary>
    /// «Живой» фон окна: медленно плывущие цветные пятна. Кадр рисуется в маленьком растре
    /// и растягивается при выводе, поэтому анимация почти не нагружает процессор.
    /// </summary>
    public static class Backdrop
    {
        private const int W = 176, H = 110;
        private static readonly Bitmap frame = new Bitmap(W, H, PixelFormat.Format32bppPArgb);
        private static readonly ImageAttributes attrs = new ImageAttributes();
        private static double time = 3.0, acc;
        private static bool rendered, animated = true;

        /// <summary>Срабатывает после обновления кадра фона.</summary>
        public static event Action Updated;

        static Backdrop()
        {
            attrs.SetWrapMode(WrapMode.TileFlipXY);
            Animator.Frame += OnFrame;
            Theme.Changed += delegate { Render(); Raise(); };
        }

        public static bool Animated
        {
            get { return animated; }
            set { animated = value; }
        }

        /// <summary>Дополнительный оттенок фона (используется для цветовых эффектов), альфа 0 — нет.</summary>
        public static Color Tint = Color.Empty;

        public static void Refresh()
        {
            Render();
            Raise();
        }

        private static void Raise()
        {
            Action h = Updated;
            if (h != null) h();
        }

        private static void OnFrame(float dt)
        {
            if (!animated || Animator.Instant) return;
            acc += dt;
            if (acc < 0.045) return;
            if (Form.ActiveForm == null) { acc = 0; return; }   // окно не активно — фон отдыхает
            time += acc;
            acc = 0;
            Render();
            Raise();
        }

        private static void Render()
        {
            rendered = true;
            Palette p = Theme.P;
            using (Graphics g = Graphics.FromImage(frame))
            {
                g.SmoothingMode = SmoothingMode.None;
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, W, H), p.Bg, p.BgAlt, 55f))
                    g.FillRectangle(b, 0, 0, W, H);

                double t = time * 0.11;
                int baseAlpha = p.Dark ? 168 : 215;
                for (int i = 0; i < p.Blobs.Length; i++)
                {
                    double ph = i * 1.7;
                    float cx = (float)(W * (0.5 + 0.46 * Math.Sin(t * (0.9 + i * 0.23) + ph)));
                    float cy = (float)(H * (0.5 + 0.44 * Math.Cos(t * (0.7 + i * 0.31) + ph * 1.3)));
                    float r = (float)(H * (0.62 + 0.16 * Math.Sin(t * 1.3 + ph)));
                    Blob(g, cx, cy, r, Color.FromArgb(baseAlpha - i * 14, p.Blobs[i]));
                }
                if (Tint.A > 0)
                    using (var tb = new SolidBrush(Tint)) g.FillRectangle(tb, 0, 0, W, H);
            }
        }

        private static void Blob(Graphics g, float cx, float cy, float r, Color c)
        {
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r, r * 2, r * 2);
                using (var b = new PathGradientBrush(path))
                {
                    b.CenterPoint = new PointF(cx, cy);
                    b.CenterColor = c;
                    b.SurroundColors = new[] { Color.FromArgb(0, c) };
                    try { b.SetSigmaBellShape(1f, 1f); }
                    catch (Exception) { }
                    g.FillPath(b, path);
                }
            }
        }

        /// <summary>Рисует участок фона под элементом <paramref name="c"/> (rect — в его координатах).</summary>
        public static void Paint(Graphics g, Control c, Rectangle rect)
        {
            if (rect.Width <= 0 || rect.Height <= 0) return;
            if (!rendered) Render();
            Control top = c.TopLevelControl ?? c;
            Size ws = top.ClientSize;
            if (ws.Width <= 0 || ws.Height <= 0) return;
            Point o = Point.Empty;
            if (top != c && c.IsHandleCreated && top.IsHandleCreated)
            {
                try { o = top.PointToClient(c.PointToScreen(Point.Empty)); }
                catch (Exception) { }
            }
            float sx = (float)W / ws.Width, sy = (float)H / ws.Height;
            // Источник берётся по целым пикселям кадра с запасом, а лишнее отсекается областью отсечения:
            // так даже очень маленький участок (уголок карточки) рисуется точно и без швов.
            int ix0 = (int)Math.Floor((o.X + rect.X) * sx) - 1, iy0 = (int)Math.Floor((o.Y + rect.Y) * sy) - 1;
            int ix1 = (int)Math.Ceiling((o.X + rect.Right) * sx) + 1, iy1 = (int)Math.Ceiling((o.Y + rect.Bottom) * sy) + 1;
            if (ix0 < 0) ix0 = 0;
            if (iy0 < 0) iy0 = 0;
            if (ix1 > W) ix1 = W;
            if (iy1 > H) iy1 = H;
            if (ix1 <= ix0 || iy1 <= iy0) return;
            float dx0 = ix0 / sx - o.X, dy0 = iy0 / sy - o.Y, dx1 = ix1 / sx - o.X, dy1 = iy1 / sy - o.Y;
            GraphicsState st = g.Save();
            g.SetClip(rect, CombineMode.Intersect);
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.SmoothingMode = SmoothingMode.None;
            g.CompositingMode = CompositingMode.SourceCopy;
            var dest = new[] { new PointF(dx0, dy0), new PointF(dx1, dy0), new PointF(dx0, dy1) };
            g.DrawImage(frame, dest, new RectangleF(ix0, iy0, ix1 - ix0, iy1 - iy0), GraphicsUnit.Pixel, attrs);
            g.CompositingMode = CompositingMode.SourceOver;
            g.Restore(st);
            g.CompositingMode = CompositingMode.SourceOver;
        }
    }

    /// <summary>Вызовы Windows API; на других платформах тихо пропускаются.</summary>
    public static class Native
    {
        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        private static bool unavailable;

        /// <summary>Текст-подсказка в пустом поле ввода (стандартная возможность Windows).</summary>
        public static void SetCue(TextBox box, string text)
        {
            if (unavailable || !box.IsHandleCreated) return;
            try { SendMessage(box.Handle, 0x1501, (IntPtr)1, text ?? ""); }   // EM_SETCUEBANNER
            catch (Exception) { }
        }

        /// <summary>Делает шрифт из файла доступным системным элементам (полям ввода) этого процесса.</summary>
        public static void RegisterFont(string path)
        {
            try { AddFontResourceEx(path, 0x10, IntPtr.Zero); }   // FR_PRIVATE
            catch (Exception) { }
        }

        /// <summary>Начать перетаскивание окна за произвольную область.</summary>
        public static bool DragWindow(Form f)
        {
            if (unavailable) return false;
            try
            {
                ReleaseCapture();
                SendMessage(f.Handle, 0xA1, (IntPtr)2, IntPtr.Zero);   // WM_NCLBUTTONDOWN, HTCAPTION
                return true;
            }
            catch (Exception)
            {
                unavailable = true;
                return false;
            }
        }

        /// <summary>Скруглённые углы окна (Windows 11); в остальных системах ничего не делает.</summary>
        public static void RoundCorners(Form f)
        {
            if (unavailable) return;
            try
            {
                int pref = 2;   // DWMWCP_ROUND
                DwmSetWindowAttribute(f.Handle, 33, ref pref, sizeof(int));
            }
            catch (Exception)
            {
                // старая версия Windows или другая платформа
            }
        }
    }

    /// <summary>Векторные значки, нарисованные линиями в сетке 24×24.</summary>
    public static class Icons
    {
        public static void Draw(Graphics g, string name, RectangleF r, Color color, float stroke = 1.7f)
        {
            GraphicsState st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(r.X, r.Y);
            float k = Math.Min(r.Width, r.Height) / 24f;
            g.ScaleTransform(k, k);
            using (var pen = new Pen(color, stroke / k))
            using (var fill = new SolidBrush(color))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                switch (name)
                {
                    case "data":
                        g.DrawRectangle(pen, 4, 5, 16, 14);
                        g.DrawLine(pen, 4, 10, 20, 10);
                        g.DrawLine(pen, 4, 14.5f, 20, 14.5f);
                        g.DrawLine(pen, 10, 10, 10, 19);
                        break;
                    case "pulse":
                        g.DrawLines(pen, new[] { P(3, 14), P(7, 9), P(11, 13), P(15, 6), P(21, 11) });
                        g.DrawLine(pen, 3, 19, 21, 19);
                        break;
                    case "wave":
                        g.DrawBezier(pen, P(3, 16), P(8, 2), P(12, 22), P(21, 7));
                        g.DrawLine(pen, 3, 20, 21, 20);
                        break;
                    case "scatter":
                        g.DrawLine(pen, 4, 19, 20, 6);
                        g.FillEllipse(fill, 5, 12, 3.4f, 3.4f);
                        g.FillEllipse(fill, 9.5f, 15, 3.4f, 3.4f);
                        g.FillEllipse(fill, 12, 7, 3.4f, 3.4f);
                        g.FillEllipse(fill, 17, 10, 3.4f, 3.4f);
                        break;
                    case "trend":
                        g.DrawLines(pen, new[] { P(3, 18), P(9, 11), P(13, 14), P(21, 6) });
                        g.DrawLines(pen, new[] { P(15.5f, 6), P(21, 6), P(21, 11.5f) });
                        break;
                    case "layers":
                        g.DrawLines(pen, new[] { P(3, 9), P(8, 13), P(14, 6), P(21, 10) });
                        g.DrawLines(pen, new[] { P(3, 17), P(9, 15), P(15, 18), P(21, 14) });
                        break;
                    case "bars":
                        pen.Width = 3.2f;
                        g.DrawLine(pen, 6, 19, 6, 12);
                        g.DrawLine(pen, 12, 19, 12, 5);
                        g.DrawLine(pen, 18, 19, 18, 9);
                        break;
                    case "bulb":
                        g.DrawArc(pen, 6, 3, 12, 12, 140, 260);
                        g.DrawLine(pen, 9.2f, 14, 9.2f, 17);
                        g.DrawLine(pen, 14.8f, 14, 14.8f, 17);
                        g.DrawLine(pen, 9.2f, 17, 14.8f, 17);
                        g.DrawLine(pen, 10.5f, 20.5f, 13.5f, 20.5f);
                        break;
                    case "home":
                        g.DrawLines(pen, new[] { P(3, 11.5f), P(12, 4), P(21, 11.5f) });
                        g.DrawLines(pen, new[] { P(6, 10), P(6, 20), P(18, 20), P(18, 10) });
                        break;
                    case "bug":
                        g.DrawEllipse(pen, 8, 8, 8, 12);
                        g.DrawArc(pen, 9, 4, 6, 6, 180, 180);
                        g.DrawLine(pen, 8, 12, 4, 10);
                        g.DrawLine(pen, 8, 16, 4, 18);
                        g.DrawLine(pen, 16, 12, 20, 10);
                        g.DrawLine(pen, 16, 16, 20, 18);
                        g.DrawLine(pen, 12, 11, 12, 20);
                        break;
                    case "shield":
                        using (var path = new GraphicsPath())
                        {
                            path.AddLine(12, 3, 20, 6);
                            path.AddBezier(P(20, 6), P(20, 13), P(17, 18), P(12, 21));
                            path.AddBezier(P(12, 21), P(7, 18), P(4, 13), P(4, 6));
                            path.CloseFigure();
                            g.DrawPath(pen, path);
                        }
                        g.DrawLine(pen, 12, 8, 12, 13);
                        g.FillEllipse(fill, 11, 15, 2, 2);
                        break;
                    case "clock":
                        g.DrawEllipse(pen, 3.5f, 3.5f, 17, 17);
                        g.DrawLines(pen, new[] { P(12, 7), P(12, 12), P(15.5f, 14) });
                        break;
                    case "flag":
                        g.DrawLine(pen, 6, 3, 6, 21);
                        g.DrawLines(pen, new[] { P(6, 4.5f), P(19, 4.5f), P(16, 8.5f), P(19, 12.5f), P(6, 12.5f) });
                        break;
                    case "sun":
                        g.DrawEllipse(pen, 8, 8, 8, 8);
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            g.DrawLine(pen, 12 + (float)Math.Cos(a) * 6.5f, 12 + (float)Math.Sin(a) * 6.5f,
                                            12 + (float)Math.Cos(a) * 9f, 12 + (float)Math.Sin(a) * 9f);
                        }
                        break;
                    case "moon":
                        using (var path = new GraphicsPath())
                        {
                            path.AddArc(4, 4, 16, 16, 300, -280);
                            path.AddArc(9, 2.5f, 13, 13, 108, 113);
                            g.DrawPath(pen, path);
                        }
                        break;
                    case "close":
                        g.DrawLine(pen, 6, 6, 18, 18);
                        g.DrawLine(pen, 18, 6, 6, 18);
                        break;
                    case "min":
                        g.DrawLine(pen, 6, 12, 18, 12);
                        break;
                    case "max":
                        g.DrawRectangle(pen, 6, 6, 12, 12);
                        break;
                    case "restore":
                        g.DrawRectangle(pen, 5, 9, 10, 10);
                        g.DrawLines(pen, new[] { P(9, 9), P(9, 5), P(19, 5), P(19, 15), P(15, 15) });
                        break;
                    case "plus":
                        g.DrawLine(pen, 12, 5, 12, 19);
                        g.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case "minus":
                        g.DrawLine(pen, 5, 12, 19, 12);
                        break;
                    case "check":
                        g.DrawLines(pen, new[] { P(5, 12.5f), P(10, 17.5f), P(19, 7) });
                        break;
                    case "warn":
                        g.DrawLines(pen, new[] { P(12, 4), P(21, 20), P(3, 20), P(12, 4) });
                        g.DrawLine(pen, 12, 10, 12, 14.5f);
                        g.FillEllipse(fill, 11, 16.3f, 2, 2);
                        break;
                    case "info":
                        g.DrawEllipse(pen, 3.5f, 3.5f, 17, 17);
                        g.DrawLine(pen, 12, 11, 12, 16.5f);
                        g.FillEllipse(fill, 11, 7, 2, 2);
                        break;
                    case "play":
                        g.FillPolygon(fill, new[] { P(8, 5), P(19, 12), P(8, 19) });
                        break;
                    case "download":
                        g.DrawLine(pen, 12, 4, 12, 15);
                        g.DrawLines(pen, new[] { P(7, 10.5f), P(12, 15.5f), P(17, 10.5f) });
                        g.DrawLines(pen, new[] { P(4, 17), P(4, 20), P(20, 20), P(20, 17) });
                        break;
                    case "upload":
                        g.DrawLine(pen, 12, 15.5f, 12, 4.5f);
                        g.DrawLines(pen, new[] { P(7, 9.5f), P(12, 4.5f), P(17, 9.5f) });
                        g.DrawLines(pen, new[] { P(4, 17), P(4, 20), P(20, 20), P(20, 17) });
                        break;
                    case "dice":
                        g.DrawRectangle(pen, 4, 4, 16, 16);
                        g.FillEllipse(fill, 7, 7, 2.6f, 2.6f);
                        g.FillEllipse(fill, 10.7f, 10.7f, 2.6f, 2.6f);
                        g.FillEllipse(fill, 14.4f, 14.4f, 2.6f, 2.6f);
                        break;
                    case "target":
                        g.DrawEllipse(pen, 3.5f, 3.5f, 17, 17);
                        g.DrawEllipse(pen, 8, 8, 8, 8);
                        g.FillEllipse(fill, 11, 11, 2, 2);
                        break;
                    case "refresh":
                        g.DrawArc(pen, 4.5f, 4.5f, 15, 15, 20, 280);
                        g.DrawLines(pen, new[] { P(15, 4), P(19.2f, 8.6f), P(13.6f, 9.6f) });
                        break;
                    case "trash":
                        g.DrawLine(pen, 4, 7, 20, 7);
                        g.DrawLines(pen, new[] { P(6.5f, 7), P(7.5f, 20), P(16.5f, 20), P(17.5f, 7) });
                        g.DrawLines(pen, new[] { P(9.5f, 7), P(9.5f, 4), P(14.5f, 4), P(14.5f, 7) });
                        break;
                    case "terminal":
                        g.DrawRectangle(pen, 3, 4.5f, 18, 15);
                        g.DrawLines(pen, new[] { P(7, 9.5f), P(10, 12), P(7, 14.5f) });
                        g.DrawLine(pen, 12.5f, 15, 16.5f, 15);
                        break;
                    case "back":
                        g.DrawLines(pen, new[] { P(14, 5), P(7, 12), P(14, 19) });
                        break;
                    case "arrow":
                        g.DrawLine(pen, 5, 12, 19, 12);
                        g.DrawLines(pen, new[] { P(13, 6), P(19, 12), P(13, 18) });
                        break;
                    case "calc":
                        g.DrawRectangle(pen, 5, 3.5f, 14, 17);
                        g.DrawLine(pen, 8, 8, 16, 8);
                        g.FillEllipse(fill, 7.6f, 11.5f, 2.2f, 2.2f);
                        g.FillEllipse(fill, 10.9f, 11.5f, 2.2f, 2.2f);
                        g.FillEllipse(fill, 14.2f, 11.5f, 2.2f, 2.2f);
                        g.FillEllipse(fill, 7.6f, 15.3f, 2.2f, 2.2f);
                        g.FillEllipse(fill, 10.9f, 15.3f, 2.2f, 2.2f);
                        g.FillEllipse(fill, 14.2f, 15.3f, 2.2f, 2.2f);
                        break;
                    case "spark":
                        g.DrawLine(pen, 12, 3, 12, 8);
                        g.DrawLine(pen, 12, 16, 12, 21);
                        g.DrawLine(pen, 3, 12, 8, 12);
                        g.DrawLine(pen, 16, 12, 21, 12);
                        g.DrawLine(pen, 6, 6, 9, 9);
                        g.DrawLine(pen, 15, 15, 18, 18);
                        g.DrawLine(pen, 18, 6, 15, 9);
                        g.DrawLine(pen, 9, 15, 6, 18);
                        break;
                    case "shoe":
                        g.DrawLine(pen, 3, 18, 21, 18);
                        g.DrawBezier(pen, 3, 18, 3, 13.5f, 5, 12, 8, 11.5f);
                        g.DrawLine(pen, 8, 11.5f, 11.5f, 6.5f);
                        g.DrawBezier(pen, 11.5f, 6.5f, 13, 5, 14.5f, 6, 15, 7.5f);
                        g.DrawLine(pen, 15, 7.5f, 16, 11.5f);
                        g.DrawBezier(pen, 16, 11.5f, 19, 12, 21, 14, 21, 18);
                        g.DrawLine(pen, 10, 9.5f, 12, 11.5f);
                        break;
                    case "menu":
                        g.DrawLine(pen, 4.5f, 7, 19.5f, 7);
                        g.DrawLine(pen, 4.5f, 12, 19.5f, 12);
                        g.DrawLine(pen, 4.5f, 17, 19.5f, 17);
                        break;
                    case "users":
                        g.DrawEllipse(pen, 6, 4, 7, 7);
                        g.DrawArc(pen, 2.5f, 13, 14, 12, 180, 180);
                        g.DrawEllipse(pen, 14, 6, 5, 5);
                        g.DrawArc(pen, 13, 13.5f, 9, 9, 230, 130);
                        break;
                    case "plug":
                        g.DrawLine(pen, 9, 3, 9, 8);
                        g.DrawLine(pen, 15, 3, 15, 8);
                        g.DrawRectangle(pen, 6, 8, 12, 6);
                        g.DrawLine(pen, 12, 14, 12, 21);
                        break;
                    case "film":
                        g.DrawRectangle(pen, 4, 5, 16, 14);
                        g.DrawLine(pen, 8, 5, 8, 19);
                        g.DrawLine(pen, 16, 5, 16, 19);
                        g.DrawLine(pen, 4, 12, 8, 12);
                        g.DrawLine(pen, 16, 12, 20, 12);
                        break;
                    case "key":
                        g.DrawEllipse(pen, 4, 8, 8, 8);
                        g.DrawLine(pen, 12, 12, 20, 12);
                        g.DrawLine(pen, 17, 12, 17, 15);
                        g.DrawLine(pen, 20, 12, 20, 15);
                        break;
                    case "list":
                        g.DrawLine(pen, 9, 7, 20, 7);
                        g.DrawLine(pen, 9, 12, 20, 12);
                        g.DrawLine(pen, 9, 17, 20, 17);
                        g.DrawEllipse(pen, 4, 6, 2, 2);
                        g.DrawEllipse(pen, 4, 11, 2, 2);
                        g.DrawEllipse(pen, 4, 16, 2, 2);
                        break;
                    case "percent":
                        g.DrawLine(pen, 18, 6, 6, 18);
                        g.DrawEllipse(pen, 5, 5, 4.5f, 4.5f);
                        g.DrawEllipse(pen, 14.5f, 14.5f, 4.5f, 4.5f);
                        break;
                    case "box":
                        g.DrawPolygon(pen, new[] { new PointF(12, 3), new PointF(20, 7), new PointF(20, 17), new PointF(12, 21), new PointF(4, 17), new PointF(4, 7) });
                        g.DrawLine(pen, 4, 7, 12, 11);
                        g.DrawLine(pen, 20, 7, 12, 11);
                        g.DrawLine(pen, 12, 11, 12, 21);
                        break;
                    case "calendar":
                        g.DrawRectangle(pen, 4, 6, 16, 14);
                        g.DrawLine(pen, 4, 10.5f, 20, 10.5f);
                        g.DrawLine(pen, 8, 3.5f, 8, 7.5f);
                        g.DrawLine(pen, 16, 3.5f, 16, 7.5f);
                        break;
                    default:
                        g.DrawEllipse(pen, 6, 6, 12, 12);
                        break;
                }
            }
            g.Restore(st);
        }

        private static PointF P(float x, float y) { return new PointF(x, y); }
    }
}
