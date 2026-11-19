using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Windows.Forms;

namespace FxKit
{
    public enum FxSeriesKind { Line, Points, Columns, Band, Stems }

    /// <summary>Один ряд данных графика.</summary>
    public sealed class FxSeries
    {
        public string Name = "";
        public FxSeriesKind Kind;
        public double[] X = new double[0], Y = new double[0], Y2;
        public string[] Labels;
        /// <summary>Номер цвета в палитре темы; −1 — нейтральный серый.</summary>
        public int Color;
        public bool Dashed, Markers, Area, InLegend = true, HideFirstMarker;
        public float Width = 2.2f;
        public int MarkerSize = 7;
        internal double[] FromY, FromY2;
        internal bool Fresh;
    }

    /// <summary>
    /// График собственной отрисовки: линии «прорисовываются» слева направо, столбцы вырастают,
    /// при изменении параметров ряды и оси плавно перетекают в новое состояние,
    /// под курсором показывается перекрестие и подсказка со значениями.
    /// </summary>
    public class FxChart : FxControl, IEnterAnim
    {
        private sealed class NoteItem
        {
            public FxSeries Series;
            public int Index;
            public string Text;
        }

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private List<FxSeries> series = new List<FxSeries>();
        private List<FxSeries> pending;
        private List<NoteItem> notes = new List<NoteItem>(), pendingNotes;
        private double tx0, tx1 = 1, ty0, ty1 = 1;      // целевые границы осей
        private double fx0, fx1 = 1, fy0, fy1 = 1;      // границы, с которых идёт переход
        private double dx0, dx1 = 1, dy0, dy1 = 1;      // отображаемые сейчас
        private float intro = 1f, morph = 1f;
        private RectangleF plot;
        private bool hoverOn;
        private Point mouse;
        private readonly AnimValue hoverA, hoverX;
        private string message;

        public string Title = "", XTitle = "", YTitle = "";
        public bool YFromZero;
        public double? XMin, XMax;
        public bool ShowLegend = true;
        public bool Sparkline;
        /// <summary>Дополнительный текст подсказки для произвольной точки оси X (например, прогноз регрессии).</summary>
        public Func<double, string> HoverInfo;
        public event Action<double> HoverMoved;

        public FxChart()
        {
            hoverA = new AnimValue(this);
            hoverX = new AnimValue(this);
            MinimumSize = new Size(60, 40);
        }

        public FxChart(string title, string xTitle, string yTitle) : this()
        {
            Title = title ?? "";
            XTitle = xTitle ?? "";
            YTitle = yTitle ?? "";
        }

        // ---------- наполнение ----------

        /// <summary>Начать формирование нового содержимого графика.</summary>
        public void Begin()
        {
            pending = new List<FxSeries>();
            pendingNotes = new List<NoteItem>();
            XMin = XMax = null;
            message = null;
        }

        private List<FxSeries> Target { get { if (pending == null) Begin(); return pending; } }

        private static void Clean(double[] x, double[] y, out double[] cx, out double[] cy)
        {
            var lx = new List<double>();
            var ly = new List<double>();
            int n = Math.Min(x.Length, y.Length);
            for (int i = 0; i < n; i++)
            {
                if (double.IsNaN(x[i]) || double.IsNaN(y[i]) || double.IsInfinity(x[i]) || double.IsInfinity(y[i])) continue;
                lx.Add(x[i]);
                ly.Add(y[i]);
            }
            cx = lx.ToArray();
            cy = ly.ToArray();
        }

        public FxSeries Line(string name, double[] x, double[] y, int color, bool dashed, bool markers)
        {
            var s = new FxSeries { Name = name, Kind = FxSeriesKind.Line, Color = color, Dashed = dashed, Markers = markers, Width = dashed ? 2f : 2.4f };
            Clean(x, y, out s.X, out s.Y);
            Target.Add(s);
            return s;
        }

        public FxSeries Points(string name, double[] x, double[] y, int color, int size)
        {
            var s = new FxSeries { Name = name, Kind = FxSeriesKind.Points, Color = color, MarkerSize = size, Markers = true };
            Clean(x, y, out s.X, out s.Y);
            Target.Add(s);
            return s;
        }

        public FxSeries Stems(string name, double[] x, double[] y, int color)
        {
            var s = new FxSeries { Name = name, Kind = FxSeriesKind.Stems, Color = color, MarkerSize = 7 };
            Clean(x, y, out s.X, out s.Y);
            Target.Add(s);
            return s;
        }

        public FxSeries Columns(string name, string[] labels, double[] values, int color)
        {
            var s = new FxSeries { Name = name, Kind = FxSeriesKind.Columns, Color = color, Labels = labels };
            s.Y = new double[values.Length];
            s.X = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                s.X[i] = i;
                s.Y[i] = double.IsNaN(values[i]) || double.IsInfinity(values[i]) ? 0 : values[i];
            }
            Target.Add(s);
            return s;
        }

        /// <summary>Полупрозрачная полоса между верхней и нижней границами (доверительный интервал).</summary>
        public FxSeries Band(string name, double[] x, double[] upper, double[] lower, int color)
        {
            var s = new FxSeries { Name = name, Kind = FxSeriesKind.Band, Color = color, X = (double[])x.Clone(), Y = (double[])upper.Clone(), Y2 = (double[])lower.Clone() };
            Target.Add(s);
            return s;
        }

        public void Note(FxSeries s, int pointIndex, string text)
        {
            if (s == null || s.X.Length == 0) return;
            if (pendingNotes == null) pendingNotes = new List<NoteItem>();
            pendingNotes.Add(new NoteItem { Series = s, Index = Math.Max(0, Math.Min(pointIndex, s.X.Length - 1)), Text = text });
        }

        /// <summary>Показать сообщение вместо графика (нет данных, ошибка).</summary>
        public void ShowMessage(string text)
        {
            Begin();
            message = text;
            End();
        }

        /// <summary>Применить сформированное содержимое: с плавным переходом от предыдущего состояния.</summary>
        public void End()
        {
            List<FxSeries> next = pending ?? new List<FxSeries>();
            pending = null;
            double nx0, nx1, ny0, ny1;
            ComputeAxes(next, out nx0, out nx1, out ny0, out ny1);

            bool live = IsHandleCreated && Visible && !Animator.Instant;
            bool hadData = series.Count > 0;
            if (live && hadData && intro >= 0.999f)
            {
                // переход: фиксируем то, что сейчас на экране, как начальное состояние
                for (int i = 0; i < next.Count; i++)
                {
                    FxSeries n = next[i];
                    FxSeries o = i < series.Count ? series[i] : null;
                    if (o != null && o.Kind == n.Kind && o.Y.Length == n.Y.Length)
                    {
                        n.FromY = Current(o.FromY, o.Y);
                        if (n.Y2 != null && o.Y2 != null && o.Y2.Length == n.Y2.Length) n.FromY2 = Current(o.FromY2, o.Y2);
                    }
                    else n.Fresh = true;
                }
                fx0 = dx0; fx1 = dx1; fy0 = dy0; fy1 = dy1;
                tx0 = nx0; tx1 = nx1; ty0 = ny0; ty1 = ny1;
                series = next;
                notes = pendingNotes ?? new List<NoteItem>();
                Animator.Run(this, "morph", 0, 1, 340, Ease.OutCubic, delegate(float t) { morph = t; UpdateAxes(); Invalidate(); });
            }
            else
            {
                Animator.Cancel(this, "morph");
                morph = 1f;
                series = next;
                notes = pendingNotes ?? new List<NoteItem>();
                fx0 = tx0 = nx0; fx1 = tx1 = nx1; fy0 = ty0 = ny0; fy1 = ty1 = ny1;
                UpdateAxes();
                if (live && !hadData && next.Count > 0) PlayEnter(0);
                else Invalidate();
            }
            pendingNotes = null;
        }

        private double[] Current(double[] from, double[] to)
        {
            if (from == null || morph >= 0.999f || from.Length != to.Length) return to;
            var r = new double[to.Length];
            for (int i = 0; i < r.Length; i++) r[i] = from[i] + (to[i] - from[i]) * morph;
            return r;
        }

        private void UpdateAxes()
        {
            dx0 = fx0 + (tx0 - fx0) * morph;
            dx1 = fx1 + (tx1 - fx1) * morph;
            dy0 = fy0 + (ty0 - fy0) * morph;
            dy1 = fy1 + (ty1 - fy1) * morph;
        }

        public void PrepareEnter()
        {
            Animator.Cancel(this, "morph");
            morph = 1f;
            UpdateAxes();
            intro = 0f;
            Invalidate();
        }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "intro", 0, 1, Sparkline ? 700 : 950, Ease.Linear, delegate(float t) { intro = t; Invalidate(); }, null, delayMs);
        }

        public void Replay()
        {
            PrepareEnter();
            PlayEnter(0);
        }

        /// <summary>Изображение графика для сохранения в файл.</summary>
        public Bitmap Render(int width, int height)
        {
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(P.Surface);
                Gfx.Hq(g);
                float keepIntro = intro, keepMorph = morph;
                bool keepHover = hoverOn;
                intro = 1f; morph = 1f; hoverOn = false;
                UpdateAxes();
                PaintChart(g, new RectangleF(10, 8, width - 20, height - 16), false);
                intro = keepIntro; morph = keepMorph; hoverOn = keepHover;
                UpdateAxes();
            }
            return bmp;
        }

        // ---------- оси ----------

        private static double Nice(double range, bool round)
        {
            if (range <= 0 || double.IsNaN(range) || double.IsInfinity(range)) return 1;
            double exp = Math.Floor(Math.Log10(range));
            double f = range / Math.Pow(10, exp);
            double nf;
            if (round) nf = f < 1.5 ? 1 : f < 3 ? 2 : f < 7 ? 5 : 10;
            else nf = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
            return nf * Math.Pow(10, exp);
        }

        private void ComputeAxes(List<FxSeries> list, out double x0, out double x1, out double y0, out double y1)
        {
            double xmin = double.MaxValue, xmax = double.MinValue, ymin = double.MaxValue, ymax = double.MinValue;
            bool categorical = false;
            foreach (FxSeries s in list)
            {
                if (s.Kind == FxSeriesKind.Columns)
                {
                    categorical = true;
                    xmin = Math.Min(xmin, -0.5);
                    xmax = Math.Max(xmax, s.Y.Length - 0.5);
                }
                for (int i = 0; i < s.Y.Length; i++)
                {
                    if (s.Kind != FxSeriesKind.Columns && i < s.X.Length)
                    {
                        xmin = Math.Min(xmin, s.X[i]);
                        xmax = Math.Max(xmax, s.X[i]);
                    }
                    ymin = Math.Min(ymin, s.Y[i]);
                    ymax = Math.Max(ymax, s.Y[i]);
                    if (s.Y2 != null && i < s.Y2.Length)
                    {
                        ymin = Math.Min(ymin, s.Y2[i]);
                        ymax = Math.Max(ymax, s.Y2[i]);
                    }
                }
                if (s.Kind == FxSeriesKind.Stems || s.Kind == FxSeriesKind.Columns)
                {
                    ymin = Math.Min(ymin, 0);
                    ymax = Math.Max(ymax, 0);
                }
            }
            if (xmin > xmax) { xmin = 0; xmax = 1; }
            if (ymin > ymax) { ymin = 0; ymax = 1; }
            if (YFromZero) { ymin = Math.Min(ymin, 0); ymax = Math.Max(ymax, 0); }

            if (!categorical)
            {
                double pad = (xmax - xmin) * 0.035;
                if (pad <= 0) pad = 0.5;
                xmin -= pad;
                xmax += pad;
            }
            x0 = XMin ?? xmin;
            x1 = XMax ?? xmax;
            if (x1 <= x0) x1 = x0 + 1;

            double range = ymax - ymin;
            if (range <= 0) { range = Math.Abs(ymax) > 1e-9 ? Math.Abs(ymax) * 0.2 : 1; ymin -= range / 2; ymax += range / 2; range = ymax - ymin; }
            if (Sparkline)
            {
                y0 = ymin - range * 0.12;
                y1 = ymax + range * 0.12;
                return;
            }
            double step = Nice(range / 5, true);
            bool zeroFloor = ymin >= 0 && (YFromZero || categorical) || Math.Abs(ymin) < 1e-12;
            y0 = zeroFloor ? 0 : Math.Floor((ymin - range * 0.04) / step) * step;
            y1 = Math.Ceiling((ymax + range * 0.07) / step) * step;
            if (ymin >= 0 && y0 < 0) y0 = 0;
            if (y1 <= y0) y1 = y0 + step;
        }

        private static string Num(double v, double step)
        {
            int digits = step >= 1 ? 0 : (int)Math.Min(6, Math.Ceiling(-Math.Log10(step) - 1e-9));
            return v.ToString("F" + digits, Ru);
        }

        private static string Val(double v)
        {
            double a = Math.Abs(v);
            return v.ToString(a >= 1000 ? "F1" : a >= 10 ? "F2" : "F3", Ru);
        }

        private float SX(double x)
        {
            double t = (x - dx0) / (dx1 - dx0);
            return (float)Math.Max(-20000, Math.Min(20000, plot.Left + t * plot.Width));
        }

        private float SY(double y)
        {
            double t = (y - dy0) / (dy1 - dy0);
            return (float)Math.Max(-20000, Math.Min(20000, plot.Bottom - t * plot.Height));
        }

        private Color ColorOf(FxSeries s)
        {
            return s.Color >= 0 ? P.Series[s.Color % P.Series.Length] : P.TextMute;
        }

        // ---------- мышь ----------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            bool inside = plot.Contains(e.Location) && series.Count > 0 && !Sparkline;
            if (inside != hoverOn)
            {
                hoverOn = inside;
                hoverA.To(inside ? 1f : 0f, inside ? 140 : 260);
            }
            if (inside)
            {
                float target = HoverInfo != null ? e.X : SX(NearestX(e.X));
                if (hoverA.Value < 0.05f) hoverX.Set(target);
                else hoverX.To(target, 110, Ease.OutCubic);
                Action<double> h = HoverMoved;
                if (h != null) h(dx0 + (e.X - plot.Left) / plot.Width * (dx1 - dx0));
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverOn = false;
            hoverA.To(0f, 260);
        }

        private double NearestX(float px)
        {
            double best = double.NaN;
            float bestD = float.MaxValue;
            foreach (FxSeries s in series)
            {
                if (s.Kind == FxSeriesKind.Band) continue;
                for (int i = 0; i < s.X.Length; i++)
                {
                    float d = Math.Abs(SX(s.X[i]) - px);
                    if (d < bestD) { bestD = d; best = s.X[i]; }
                }
            }
            return double.IsNaN(best) ? dx0 : best;
        }

        // ---------- отрисовка ----------

        protected override void Draw(Graphics g)
        {
            PaintChart(g, new RectangleF(0, 0, Width, Height), true);
        }

        private void PaintChart(Graphics g, RectangleF area, bool interactive)
        {
            if (Sparkline)
            {
                plot = RectangleF.Inflate(area, -2, -3);
                if (plot.Width > 4 && plot.Height > 4) DrawSeries(g);
                return;
            }

            float top = area.Top;
            if (Title.Length > 0)
            {
                Gfx.Text(g, Title, Fonts.Semi(9.5f), P.Text, new RectangleF(area.Left + 2, top, area.Width - 4, 20));
                top += 24;
            }
            if (ShowLegend && series.Count > 0 && message == null) top = DrawLegend(g, area, top);

            // поля под подписи осей
            double step = Nice((ty1 - ty0) / 5, true);
            Font tf = Fonts.Mono(8f);
            float labelW = 20;
            for (double v = ty0; v <= ty1 + step * 0.5; v += step)
                labelW = Math.Max(labelW, Gfx.Measure(Num(v, step), tf).Width);
            float left = area.Left + labelW + (YTitle.Length > 0 ? 30 : 8);
            float bottom = area.Bottom - (XTitle.Length > 0 ? 38 : 22);
            bool categorical = series.Count > 0 && series[0].Kind == FxSeriesKind.Columns;
            if (categorical) bottom -= 12;
            plot = new RectangleF(left, top + 6, area.Right - left - 12, bottom - top - 6);
            if (plot.Width < 20 || plot.Height < 20) return;

            if (message != null || series.Count == 0)
            {
                Icons.Draw(g, "pulse", new RectangleF(plot.Left + plot.Width / 2 - 14, plot.Top + plot.Height / 2 - 34, 28, 28), P.TextMute, 1.6f);
                Gfx.Text(g, message ?? "нет данных", Fonts.Body(9.5f), P.TextMute,
                    new RectangleF(plot.Left, plot.Top + plot.Height / 2, plot.Width, 40), StringAlignment.Center, StringAlignment.Near, true);
                return;
            }

            // сетка и подписи оси Y
            using (var grid = new Pen(P.Grid))
            {
                for (double v = Math.Ceiling(dy0 / step - 1e-9) * step; v <= dy1 + step * 1e-6; v += step)
                {
                    float y = SY(v);
                    if (y < plot.Top - 1 || y > plot.Bottom + 1) continue;
                    g.DrawLine(grid, plot.Left, y, plot.Right, y);
                    Gfx.Text(g, Num(Math.Abs(v) < step * 1e-6 ? 0 : v, step), tf, P.TextMute,
                        new RectangleF(area.Left, y - 9, left - area.Left - 6, 18), StringAlignment.Far);
                }
            }
            if (dy0 < 0 && dy1 > 0)
                using (var zero = new Pen(P.BorderHi)) g.DrawLine(zero, plot.Left, SY(0), plot.Right, SY(0));

            // подписи оси X
            if (categorical)
            {
                string[] labels = series[0].Labels ?? new string[0];
                float cw = plot.Width / Math.Max(1, labels.Length);
                for (int i = 0; i < labels.Length; i++)
                    Gfx.Text(g, labels[i], Fonts.Medium(8f), P.TextDim, new RectangleF(plot.Left + i * cw + 2, plot.Bottom + 4, cw - 4, 30),
                        StringAlignment.Center, StringAlignment.Near, true);
            }
            else
            {
                double xr = dx1 - dx0;
                double xs = Nice(xr / Math.Max(2, Math.Min(12, plot.Width / 56)), true);
                bool ints = AllIntegerX();
                if (ints && xs < 1) xs = 1;
                for (double v = Math.Ceiling(dx0 / xs - 1e-9) * xs; v <= dx1 + xs * 1e-6; v += xs)
                {
                    float x = SX(v);
                    if (x < plot.Left - 1 || x > plot.Right + 1) continue;
                    using (var tick = new Pen(P.Border)) g.DrawLine(tick, x, plot.Bottom, x, plot.Bottom + 4);
                    Gfx.Text(g, Num(Math.Abs(v) < xs * 1e-6 ? 0 : v, xs), tf, P.TextMute, new RectangleF(x - 30, plot.Bottom + 5, 60, 16), StringAlignment.Center);
                }
            }
            using (var axis = new Pen(P.Border))
                g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
            if (XTitle.Length > 0)
                Gfx.Text(g, XTitle, Fonts.Medium(8.5f), P.TextMute, new RectangleF(plot.Left, area.Bottom - 18, plot.Width, 18), StringAlignment.Center);
            if (YTitle.Length > 0)
            {
                GraphicsState st = g.Save();
                g.TranslateTransform(area.Left + 8, plot.Top + plot.Height / 2);
                g.RotateTransform(-90);
                Gfx.Text(g, YTitle, Fonts.Medium(8.5f), P.TextMute, new RectangleF(-plot.Height / 2, -9, plot.Height, 18), StringAlignment.Center);
                g.Restore(st);
            }

            DrawSeries(g);
            DrawNotes(g);
            if (interactive && hoverA.Value > 0.01f) DrawHover(g, categorical);
        }

        private bool AllIntegerX()
        {
            foreach (FxSeries s in series)
            {
                if (s.Kind == FxSeriesKind.Band) continue;
                foreach (double x in s.X)
                    if (Math.Abs(x - Math.Round(x)) > 1e-9) return false;
            }
            return true;
        }

        private float DrawLegend(Graphics g, RectangleF area, float top)
        {
            Font f = Fonts.Medium(8.5f);
            float x = area.Left + 2, y = top;
            bool any = false;
            foreach (FxSeries s in series)
            {
                if (!s.InLegend || s.Name.Length == 0) continue;
                float w = Gfx.Measure(s.Name, f).Width + 30;
                if (x + w > area.Right && x > area.Left + 4) { x = area.Left + 2; y += 18; }
                Color c = ColorOf(s);
                float cy = y + 9;
                if (s.Kind == FxSeriesKind.Columns || s.Kind == FxSeriesKind.Band)
                    Gfx.FillRound(g, s.Kind == FxSeriesKind.Band ? Gfx.A(c, 90) : c, new RectangleF(x + 3, cy - 5, 12, 10), 3);
                else if (s.Kind == FxSeriesKind.Points || s.Kind == FxSeriesKind.Stems)
                    using (var b = new SolidBrush(c)) g.FillEllipse(b, x + 5, cy - 4, 8, 8);
                else
                {
                    using (var pen = new Pen(c, 2.2f))
                    {
                        if (s.Dashed) pen.DashPattern = new[] { 2.2f, 1.6f };
                        g.DrawLine(pen, x, cy, x + 18, cy);
                    }
                    if (s.Markers) using (var b = new SolidBrush(c)) g.FillEllipse(b, x + 6, cy - 3, 6, 6);
                }
                Gfx.Text(g, s.Name, f, P.TextDim, new RectangleF(x + 22, y, w, 18));
                x += w + 8;
                any = true;
            }
            return any ? y + 20 : top;
        }

        private float Local(int index, int count)
        {
            // прогресс появления ряда с учётом задержки «лесенкой»
            float lag = Math.Min(0.14f, 0.5f / Math.Max(1, count));
            return Gfx.Clamp((intro - index * lag) / Math.Max(0.2f, 1 - (count - 1) * lag), 0, 1);
        }

        private void DrawSeries(Graphics g)
        {
            int count = series.Count;
            int colSeries = 0, colIndex = 0;
            foreach (FxSeries s in series) if (s.Kind == FxSeriesKind.Columns) colSeries++;

            GraphicsState outer = g.Save();
            g.SetClip(RectangleF.Inflate(plot, 6, 8), CombineMode.Intersect);

            for (int si = 0; si < count; si++)
            {
                FxSeries s = series[si];
                float local = Local(si, count);
                float fresh = s.Fresh ? morph : 1f;
                Color c = ColorOf(s);
                double[] ys = Current(s.FromY, s.Y);
                int n = Math.Min(s.X.Length, ys.Length);
                if (n == 0) continue;

                if (s.Kind == FxSeriesKind.Band)
                {
                    double[] lo = Current(s.FromY2, s.Y2);
                    var pts = new List<PointF>();
                    for (int i = 0; i < n; i++) pts.Add(new PointF(SX(s.X[i]), SY(ys[i])));
                    for (int i = n - 1; i >= 0; i--) pts.Add(new PointF(SX(s.X[i]), SY(lo[i])));
                    if (pts.Count >= 3)
                    {
                        float a = Ease.OutCubic(local) * fresh;
                        using (var b = new SolidBrush(Gfx.A(c, (int)((P.Dark ? 34 : 30) * a))))
                            g.FillPolygon(b, pts.ToArray());
                        using (var pen = new Pen(Gfx.A(c, (int)(130 * a)), 1f) { DashPattern = new[] { 4f, 3f } })
                        {
                            g.DrawLines(pen, pts.GetRange(0, n).ToArray());
                            g.DrawLines(pen, pts.GetRange(n, n).ToArray());
                        }
                    }
                    continue;
                }

                if (s.Kind == FxSeriesKind.Columns)
                {
                    float cw = plot.Width / n;
                    float groupW = Math.Min(cw * 0.72f, 46f * colSeries + 2 * (colSeries - 1));
                    float bw = (groupW - 2 * (colSeries - 1)) / colSeries;
                    float baseY = SY(0);
                    for (int i = 0; i < n; i++)
                    {
                        float bl = Ease.OutBack(Gfx.Clamp(local * 1.6f - i * 0.12f, 0, 1));
                        float x = plot.Left + i * cw + (cw - groupW) / 2 + colIndex * (bw + 2);
                        float y = baseY + (SY(ys[i]) - baseY) * bl;
                        var r = new RectangleF(x, Math.Min(y, baseY), bw, Math.Abs(y - baseY));
                        if (r.Height < 0.5f) continue;
                        bool hot = hoverOn && r.Left <= mouse.X && mouse.X <= r.Right;
                        using (var b = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height + 2),
                                   Gfx.Mix(c, Color.White, hot ? 0.28f : 0.1f), Gfx.A(c, hot ? 255 : 215), 90f))
                        using (GraphicsPath path = TopRound(r, Math.Min(4, bw / 2)))
                            g.FillPath(b, path);
                        float la = Gfx.Clamp(local * 3 - 2, 0, 1) * fresh;
                        if (la > 0.02f && bw >= 16)
                            Gfx.Text(g, ys[i].ToString("F2", Ru), Fonts.Mono(7.5f), Gfx.A(P.TextDim, la),
                                new RectangleF(x - 14, r.Y - 16, bw + 28, 14), StringAlignment.Center);
                    }
                    colIndex++;
                    continue;
                }

                var p = new PointF[n];
                for (int i = 0; i < n; i++) p[i] = new PointF(SX(s.X[i]), SY(ys[i]));

                if (s.Kind == FxSeriesKind.Stems)
                {
                    float baseY = SY(0);
                    for (int i = 0; i < n; i++)
                    {
                        float bl = Ease.OutBack(Gfx.Clamp(local * 1.8f - i * 0.08f, 0, 1));
                        float y = baseY + (p[i].Y - baseY) * bl;
                        using (var pen = new Pen(Gfx.A(c, 200), 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                            g.DrawLine(pen, p[i].X, baseY, p[i].X, y);
                        Dot(g, p[i].X, y, s.MarkerSize * Math.Min(1, bl), c);
                    }
                    continue;
                }

                if (s.Kind == FxSeriesKind.Line && n >= 2)
                {
                    float reveal = Ease.InOutCubic(local);
                    GraphicsState st = g.Save();
                    if (reveal < 0.999f)
                        g.SetClip(new RectangleF(plot.Left - 8, plot.Top - 10, (plot.Width + 16) * reveal, plot.Height + 20), CombineMode.Intersect);
                    if (s.Area)
                    {
                        using (var path = new GraphicsPath())
                        {
                            path.AddLines(p);
                            path.AddLine(p[n - 1].X, plot.Bottom, p[0].X, plot.Bottom);
                            path.CloseFigure();
                            using (var b = new LinearGradientBrush(new RectangleF(plot.Left, plot.Top - 1, plot.Width, plot.Height + 2),
                                       Gfx.A(c, (int)((P.Dark ? 70 : 52) * fresh)), Gfx.A(c, 0), 90f))
                                g.FillPath(b, path);
                        }
                    }
                    if (!s.Dashed && P.Dark && !Sparkline)
                        using (var glow = new Pen(Gfx.A(c, (int)(38 * fresh)), s.Width + 5f) { LineJoin = LineJoin.Round })
                            g.DrawLines(glow, p);
                    using (var pen = new Pen(Gfx.A(c, fresh), s.Width) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        if (s.Dashed) pen.DashPattern = new[] { 3.2f, 2.4f };
                        g.DrawLines(pen, p);
                    }
                    g.Restore(st);
                    if (Sparkline && reveal > 0.98f) Dot(g, p[n - 1].X, p[n - 1].Y, 6, c);
                }

                if (s.Markers && (!Sparkline || s.Kind == FxSeriesKind.Points))
                {
                    for (int i = s.HideFirstMarker ? 1 : 0; i < n; i++)
                    {
                        float t = (p[i].X - plot.Left) / Math.Max(1, plot.Width);
                        float ml = s.Kind == FxSeriesKind.Points
                            ? Ease.OutBack(Gfx.Clamp(local * 2.2f - t * 1.2f, 0, 1))
                            : Ease.OutBack(Gfx.Clamp((Ease.InOutCubic(local) - t) * 9 + 0.4f, 0, 1));
                        if (ml <= 0.02f) continue;
                        Dot(g, p[i].X, p[i].Y, s.MarkerSize * ml * fresh, c);
                    }
                }
            }
            g.Restore(outer);
        }

        private static GraphicsPath TopRound(RectangleF r, float rad)
        {
            var path = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d < 1) { path.AddRectangle(r); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddLine(r.Right, r.Bottom, r.X, r.Bottom);
            path.CloseFigure();
            return path;
        }

        private void Dot(Graphics g, float x, float y, float size, Color c)
        {
            if (size < 0.6f) return;
            float ring = size + 3.2f;
            using (var b = new SolidBrush(P.Surface))
                g.FillEllipse(b, x - ring / 2, y - ring / 2, ring, ring);
            using (var b = new SolidBrush(c))
                g.FillEllipse(b, x - size / 2, y - size / 2, size, size);
        }

        private void DrawNotes(Graphics g)
        {
            float a = Gfx.Clamp(intro * 4 - 3, 0, 1);
            if (a <= 0.02f) return;
            Font f = Fonts.Semi(8.5f);
            foreach (NoteItem nt in notes)
            {
                FxSeries s = nt.Series;
                if (!series.Contains(s) || nt.Index >= s.X.Length) continue;
                double[] ys = Current(s.FromY, s.Y);
                float px = SX(s.X[nt.Index]), py = SY(ys[nt.Index]);
                SizeF sz = Gfx.Measure(nt.Text, f, 220);
                float w = sz.Width + 12, h = sz.Height + 8;
                float bx = Gfx.Clamp(px - w / 2, plot.Left + 2, plot.Right - w - 2);
                float by = py - h - 14;
                if (by < plot.Top + 2) by = py + 14;
                var r = new RectangleF(bx, by, w, h);
                Color c = ColorOf(s);
                using (var pen = new Pen(Gfx.A(c, (int)(200 * a))))
                    g.DrawLine(pen, px, py, px, by < py ? r.Bottom : r.Top);
                Gfx.FillRound(g, Gfx.A(P.Surface2, (int)(238 * a)), r, 7);
                Gfx.DrawRound(g, Gfx.A(c, (int)(220 * a)), r, 7);
                Gfx.Text(g, nt.Text, f, Gfx.A(P.Text, a), new RectangleF(r.X + 6, r.Y + 3, r.Width - 8, r.Height - 4), StringAlignment.Near, StringAlignment.Near, true);
            }
        }

        private void DrawHover(Graphics g, bool categorical)
        {
            float a = hoverA.Value;
            var lines = new List<KeyValuePair<int, string>>();   // цвет ряда → текст
            string head = "";
            float cx = hoverX.Value;

            if (categorical)
            {
                string[] labels = series[0].Labels ?? new string[0];
                int n = Math.Max(1, labels.Length);
                int idx = (int)Gfx.Clamp((mouse.X - plot.Left) / (plot.Width / n), 0, n - 1);
                head = idx < labels.Length ? labels[idx] : "";
                foreach (FxSeries s in series)
                    if (s.Kind == FxSeriesKind.Columns && idx < s.Y.Length)
                        lines.Add(new KeyValuePair<int, string>(s.Color, s.Name + ": " + s.Y[idx].ToString("F2", Ru)));
                cx = plot.Left + (idx + 0.5f) * plot.Width / n;
            }
            else if (HoverInfo != null)
            {
                double x = dx0 + (mouse.X - plot.Left) / plot.Width * (dx1 - dx0);
                head = HoverInfo(x) ?? "";
                cx = mouse.X;
                using (var pen = new Pen(Gfx.A(P.Accent2, (int)(170 * a)), 1f) { DashPattern = new[] { 3f, 3f } })
                    g.DrawLine(pen, cx, plot.Top, cx, plot.Bottom);
            }
            else
            {
                double hx = NearestX(mouse.X);
                head = (XTitle.Length > 0 ? XTitle + " " : "x = ") + (Math.Abs(hx - Math.Round(hx)) < 1e-9 ? hx.ToString("F0", Ru) : Val(hx));
                using (var pen = new Pen(Gfx.A(P.Accent2, (int)(150 * a)), 1f) { DashPattern = new[] { 3f, 3f } })
                    g.DrawLine(pen, cx, plot.Top, cx, plot.Bottom);
                foreach (FxSeries s in series)
                {
                    if (s.Kind == FxSeriesKind.Band) continue;
                    for (int i = 0; i < s.X.Length && i < s.Y.Length; i++)
                    {
                        if (Math.Abs(s.X[i] - hx) > 1e-9) continue;
                        if (s.Name.Length > 0) lines.Add(new KeyValuePair<int, string>(s.Color, s.Name + ": " + Val(s.Y[i])));
                        Color c = ColorOf(s);
                        float py = SY(s.Y[i]);
                        using (var b = new SolidBrush(Gfx.A(c, (int)(70 * a)))) g.FillEllipse(b, cx - 8, py - 8, 16, 16);
                        Dot(g, cx, py, 8, c);
                        break;
                    }
                }
            }
            if (head.Length == 0 && lines.Count == 0) return;

            Font hf = Fonts.Semi(8.5f), lf = Fonts.Medium(8.5f);
            SizeF hs = Gfx.Measure(head, hf, 300);
            float w = hs.Width + 8, h = head.Length > 0 ? hs.Height + 2 : 0;
            foreach (KeyValuePair<int, string> l in lines)
            {
                w = Math.Max(w, Gfx.Measure(l.Value, lf).Width + 22);
                h += 17;
            }
            w = Math.Min(w + 14, plot.Width);
            h += 12;
            float bx = cx + 14;
            if (bx + w > plot.Right) bx = cx - 14 - w;
            bx = Gfx.Clamp(bx, plot.Left, Math.Max(plot.Left, plot.Right - w));
            float by = Gfx.Clamp(mouse.Y - h - 10, plot.Top, Math.Max(plot.Top, plot.Bottom - h));
            var box = new RectangleF(bx, by, w, h);
            Gfx.Glow(g, box, 9, Color.Black, 4, 1.5f, (int)(50 * a));
            Gfx.FillRound(g, Gfx.A(P.SurfaceHi, (int)(246 * a)), box, 9);
            Gfx.DrawRound(g, Gfx.A(P.BorderHi, (int)(255 * a)), box, 9);
            float ty = by + 6;
            if (head.Length > 0)
            {
                Gfx.Text(g, head, hf, Gfx.A(P.Text, a), new RectangleF(bx + 8, ty, w - 12, hs.Height + 2), StringAlignment.Near, StringAlignment.Near, true);
                ty += hs.Height + 2;
            }
            foreach (KeyValuePair<int, string> l in lines)
            {
                Color c = l.Key >= 0 ? P.Series[l.Key % P.Series.Length] : P.TextMute;
                using (var b = new SolidBrush(Gfx.A(c, a))) g.FillEllipse(b, bx + 9, ty + 5, 7, 7);
                Gfx.Text(g, l.Value, lf, Gfx.A(P.TextDim, a), new RectangleF(bx + 21, ty, w - 24, 17));
                ty += 17;
            }
        }
    }

    /// <summary>
    /// Тепловая карта ошибки для сочетаний двух параметров: чем ярче ячейка, тем меньше ошибка.
    /// Текущее сочетание отмечено рамкой, плавно переезжающей при изменении параметров.
    /// </summary>
    public class FxHeat : FxControl, IEnterAnim
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        private double[] axis = new double[0];
        private double[,] data;
        private double min, max;
        private int bestI = -1, bestJ = -1, hoverI = -1, hoverJ = -1;
        private readonly AnimValue curI, curJ;
        private float enter = 1f, pulse;
        private RectangleF grid;
        private bool hasCurrent;

        public string RowTitle = "α", ColTitle = "β", ValueTitle = "MSE";
        /// <summary>Щелчок по ячейке: (значение строки, значение столбца).</summary>
        public event Action<double, double> CellClicked;

        public FxHeat()
        {
            curI = new AnimValue(this);
            curJ = new AnimValue(this);
            Cursor = Cursors.Hand;
            Animator.Frame += OnFrame;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Frame -= OnFrame;
            base.Dispose(disposing);
        }

        private void OnFrame(float dt)
        {
            if (!hasCurrent || !IsHandleCreated || !Visible || Animator.Instant) return;
            pulse += dt;
            if (grid.Width > 0)
            {
                float cw = grid.Width / Math.Max(1, axis.Length), ch = grid.Height / Math.Max(1, axis.Length);
                Invalidate(Rectangle.Round(new RectangleF(grid.Left + curJ.Value * cw - 8, grid.Top + curI.Value * ch - 8, cw + 16, ch + 16)));
            }
        }

        public void SetData(double[] axisValues, double[,] values, double minValue, double maxValue, double bestRow, double bestCol)
        {
            axis = axisValues ?? new double[0];
            data = values;
            min = minValue;
            max = maxValue;
            bestI = Nearest(bestRow);
            bestJ = Nearest(bestCol);
            // цвет по рангу значения: оттенки распределяются равномерно, даже если ошибки сильно различаются
            sorted = new double[values == null ? 0 : values.Length];
            int k = 0;
            if (values != null) foreach (double v in values) sorted[k++] = v;
            Array.Sort(sorted);
            Invalidate();
        }

        public void Clear()
        {
            data = null;
            hasCurrent = false;
            Invalidate();
        }

        private int Nearest(double v)
        {
            int best = -1;
            double d = double.MaxValue;
            for (int i = 0; i < axis.Length; i++)
                if (Math.Abs(axis[i] - v) < d) { d = Math.Abs(axis[i] - v); best = i; }
            return best;
        }

        private float Position(double v)
        {
            if (axis.Length < 2) return 0;
            double step = axis[1] - axis[0];
            return (float)Gfx.Clamp((float)((v - axis[0]) / step), -0.4f, axis.Length - 0.6f);
        }

        /// <summary>Отметить текущее сочетание параметров.</summary>
        public void SetCurrent(double row, double col)
        {
            float i = Position(row), j = Position(col);
            if (!hasCurrent || !IsHandleCreated || !Visible) { curI.Set(i); curJ.Set(j); }
            else { curI.To(i, 380, Ease.OutBack); curJ.To(j, 380, Ease.OutBack); }
            hasCurrent = true;
            Invalidate();
        }

        public void PrepareEnter() { enter = 0f; Invalidate(); }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "enter", 0, 1, 900, Ease.Linear, delegate(float t) { enter = t; Invalidate(); }, null, delayMs);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = -1, j = -1;
            if (data != null && grid.Contains(e.Location) && axis.Length > 0)
            {
                j = (int)Gfx.Clamp((e.X - grid.Left) / (grid.Width / axis.Length), 0, axis.Length - 1);
                i = (int)Gfx.Clamp((e.Y - grid.Top) / (grid.Height / axis.Length), 0, axis.Length - 1);
            }
            if (i != hoverI || j != hoverJ) { hoverI = i; hoverJ = j; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverI = hoverJ = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || hoverI < 0) return;
            Action<double, double> h = CellClicked;
            if (h != null) h(axis[hoverI], axis[hoverJ]);
        }

        private double[] sorted = new double[0];

        private Color CellColor(double v)
        {
            float t = 0f;
            if (sorted.Length > 1)
            {
                int idx = Array.BinarySearch(sorted, v);
                if (idx < 0) idx = ~idx;
                t = Gfx.Clamp(idx / (float)(sorted.Length - 1), 0, 1);
            }
            float good = (float)Math.Pow(1 - t, 1.6);
            Color lo = P.Dark ? Theme.Hex("1B2440") : Theme.Hex("E9EEF8");
            Color hi = P.Dark ? Theme.Hex("35E0F5") : Theme.Hex("0B86C9");
            return Gfx.Mix(lo, hi, good);
        }

        protected override void Draw(Graphics g)
        {
            int n = axis.Length;
            Font lf = Fonts.Mono(7.5f);
            const float left = 46, top = 30, bottomPad = 24;
            grid = new RectangleF(left, top, Width - left - 6, Height - top - bottomPad);
            if (data == null || n == 0 || grid.Width < 40 || grid.Height < 30)
            {
                Gfx.Text(g, "нет данных", Fonts.Body(9.5f), P.TextMute, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
                return;
            }
            float cw = grid.Width / n, ch = grid.Height / n;

            Gfx.Text(g, ColTitle + "  →", Fonts.Semi(8.5f), P.TextDim, new RectangleF(left, 0, 80, 14));
            Gfx.Text(g, RowTitle + " ↓", Fonts.Semi(8.5f), P.TextDim, new RectangleF(0, top - 16, left - 4, 14), StringAlignment.Far);
            int every = cw < 30 ? 2 : 1;
            for (int j = 0; j < n; j += every)
                Gfx.Text(g, axis[j].ToString("0.00", Ru), lf, j == hoverJ ? P.Text : P.TextMute,
                    new RectangleF(grid.Left + j * cw - 12, top - 15, cw + 24, 14), StringAlignment.Center);
            int everyRow = ch < 13 ? 2 : 1;
            for (int i = 0; i < n; i += everyRow)
                Gfx.Text(g, axis[i].ToString("0.00", Ru), lf, i == hoverI ? P.Text : P.TextMute,
                    new RectangleF(0, grid.Top + i * ch - 2, left - 6, ch + 4), StringAlignment.Far);

            g.SmoothingMode = SmoothingMode.None;
            float gap = cw > 14 && ch > 8 ? 1f : 0f;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float local = Gfx.Clamp(enter * 2.6f - (i + j) / (2f * n) * 1.6f, 0, 1);
                    if (local <= 0.01f) continue;
                    Color c = Gfx.Mix(P.Surface, CellColor(data[i, j]), local);
                    using (var b = new SolidBrush(c))
                        g.FillRectangle(b, grid.Left + j * cw, grid.Top + i * ch, cw - gap, ch - gap);
                }
            }
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (enter > 0.9f)
            {
                if (bestI >= 0)
                {
                    var br = new RectangleF(grid.Left + bestJ * cw - 1.5f, grid.Top + bestI * ch - 1.5f, cw + 2 - gap, ch + 2 - gap);
                    Gfx.DrawRound(g, P.Dark ? Color.White : P.Text, br, 3, 2f);
                }
                if (hasCurrent)
                {
                    float grow = 2.5f + 1.5f * (float)Math.Sin(pulse * 4.2);
                    var cr = new RectangleF(grid.Left + curJ.Value * cw - grow, grid.Top + curI.Value * ch - grow, cw + grow * 2 - gap, ch + grow * 2 - gap);
                    Gfx.DrawRound(g, P.Accent3, cr, 4, 2.2f);
                }
            }

            // шкала и подсказка
            float ly = Height - 15;
            Gfx.Text(g, "ошибка меньше", Fonts.Body(8f), P.TextMute, new RectangleF(left, ly - 2, 100, 16));
            using (var b = new LinearGradientBrush(new RectangleF(left + 97, ly, 92, 10), CellColor(min), CellColor(max), 0f))
                Gfx.FillRound(g, b, new RectangleF(left + 98, ly + 2, 90, 8), 4);
            Gfx.Text(g, "больше", Fonts.Body(8f), P.TextMute, new RectangleF(left + 194, ly - 2, 60, 16));
            float lx = left + 252;
            if (lx + 250 < Width)
            {
                Gfx.DrawRound(g, P.Dark ? Color.White : P.Text, new RectangleF(lx, ly + 1, 11, 10), 3, 1.8f);
                Gfx.Text(g, "минимум", Fonts.Body(8f), P.TextMute, new RectangleF(lx + 16, ly - 2, 64, 16));
                Gfx.DrawRound(g, P.Accent3, new RectangleF(lx + 82, ly + 1, 11, 10), 3, 1.8f);
                Gfx.Text(g, "текущие α, β", Fonts.Body(8f), P.TextMute, new RectangleF(lx + 98, ly - 2, 100, 16));
            }

            if (hoverI >= 0)
            {
                var hr = new RectangleF(grid.Left + hoverJ * cw - 1, grid.Top + hoverI * ch - 1, cw + 1, ch + 1);
                Gfx.DrawRound(g, P.Accent2, hr, 3, 1.6f);
                string tip = RowTitle + " = " + axis[hoverI].ToString("0.00", Ru) + "   " + ColTitle + " = " + axis[hoverJ].ToString("0.00", Ru) +
                             "   " + ValueTitle + " = " + data[hoverI, hoverJ].ToString("F2", Ru);
                Font tf = Fonts.Semi(8.5f);
                float tw = Gfx.Measure(tip, tf).Width + 16;
                float tx = Gfx.Clamp(hr.Left + cw / 2 - tw / 2, 2, Width - tw - 2);
                float ty = hr.Top - 28;
                if (ty < 0) ty = hr.Bottom + 6;
                var tr = new RectangleF(tx, ty, tw, 22);
                Gfx.FillRound(g, Gfx.A(P.SurfaceHi, 248), tr, 7);
                Gfx.DrawRound(g, P.BorderHi, tr, 7);
                Gfx.Text(g, tip, tf, P.Text, tr, StringAlignment.Center);
            }
        }
    }
}
