using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    // =====================================================================
    //  Журнал сообщений (консоль)
    // =====================================================================

    public enum LogKind { Info, Debug, Trace, Error, Good }

    /// <summary>
    /// Журнал в виде консоли: каждая новая строка плавно «въезжает», тип сообщения отмечен цветной меткой,
    /// прокрутка — колесом мыши.
    /// </summary>
    public class FxConsole : FxControl
    {
        private sealed class Line
        {
            public string Time, Text;
            public LogKind Kind;
            public float Appear;
        }

        private readonly List<Line> lines = new List<Line>();
        private readonly AnimValue scroll;
        private float clearing;
        private const int LineH = 21;

        public string EmptyText = "журнал пуст";
        public bool ShowTime = true;

        public FxConsole()
        {
            scroll = new AnimValue(this);
        }

        public int Count { get { return lines.Count; } }

        public void Add(string text, LogKind kind = LogKind.Info)
        {
            var line = new Line { Time = DateTime.Now.ToString("HH:mm:ss"), Text = text ?? "", Kind = kind };
            lines.Add(line);
            Animator.Run(line, "in", 0, 1, 380, Ease.OutCubic, delegate(float v) { line.Appear = v; Invalidate(); });
            scroll.To(MaxScroll, 260, Ease.OutCubic);
        }

        /// <summary>Очистка с затуханием.</summary>
        public void Clear()
        {
            if (lines.Count == 0) return;
            Animator.Run(this, "clear", 0, 1, 260, Ease.InCubic, delegate(float v) { clearing = v; Invalidate(); }, delegate
            {
                lines.Clear();
                clearing = 0;
                scroll.Set(0);
                Invalidate();
            });
        }

        private float MaxScroll { get { return Math.Max(0, lines.Count * LineH + 16 - Height); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll.To(Gfx.Clamp(scroll.Target - Math.Sign(e.Delta) * LineH * 3, 0, MaxScroll), 240, Ease.OutCubic);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (scroll.Target > MaxScroll) scroll.Set(MaxScroll);
        }

        private static Color KindColor(LogKind k)
        {
            switch (k)
            {
                case LogKind.Debug: return Tones.Of(Tone.Accent);
                case LogKind.Trace: return P.Accent2;
                case LogKind.Error: return P.Bad;
                case LogKind.Good: return P.Good;
                default: return P.TextMute;
            }
        }

        private static string KindTag(LogKind k)
        {
            switch (k)
            {
                case LogKind.Debug: return "Debug";
                case LogKind.Trace: return "Trace";
                case LogKind.Error: return "Ошибка";
                case LogKind.Good: return "OK";
                default: return "·";
            }
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, P.Dark ? Theme.Hex("0A0E1B") : Theme.Hex("F2F4FA"), r, 10);
            Gfx.DrawRound(g, P.Border, r, 10);
            if (lines.Count == 0)
            {
                Gfx.Text(g, EmptyText, Fonts.Mono(9f), P.TextMute, r, StringAlignment.Center);
                return;
            }
            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(2, 4, Width - 4, Height - 8), CombineMode.Intersect);
            Font mono = Fonts.Mono(9f), tagFont = Fonts.Bold(7.5f);
            float y = 8 - scroll.Value;
            float fade = 1 - clearing;
            foreach (Line l in lines)
            {
                if (y > -LineH && y < Height)
                {
                    float a = l.Appear * fade;
                    float dx = (1 - l.Appear) * 22;
                    float x = 12 + dx;
                    if (ShowTime)
                    {
                        Gfx.Text(g, l.Time, mono, Gfx.A(P.TextMute, a), new RectangleF(x, y, 70, LineH));
                        x += 68;
                    }
                    Color kc = KindColor(l.Kind);
                    string tag = KindTag(l.Kind);
                    if (l.Kind != LogKind.Info)
                    {
                        var tr = new RectangleF(x, y + 3, 50, LineH - 6);
                        Gfx.FillRound(g, Gfx.A(kc, (int)(46 * a)), tr, 5);
                        Gfx.Text(g, tag, tagFont, Gfx.A(kc, a), tr, StringAlignment.Center);
                    }
                    x += 58;
                    Color tc = l.Kind == LogKind.Error ? Gfx.Mix(P.Bad, P.Text, 0.35f) : Gfx.Mix(P.TextDim, P.Text, 0.5f);
                    Gfx.Text(g, l.Text, mono, Gfx.A(tc, a), new RectangleF(x, y, Width - x - 10, LineH));
                }
                y += LineH;
            }
            g.Restore(st);
            if (MaxScroll > 0)
            {
                float vh = Height - 8, total = lines.Count * LineH + 16;
                float bh = Math.Max(22, vh * vh / total);
                Gfx.FillRound(g, Gfx.A(P.TextMute, 110), new RectangleF(Width - 6, 4 + (vh - bh) * (scroll.Value / MaxScroll), 4, bh), 2);
            }
        }
    }

    // =====================================================================
    //  Лабораторная № 7: надпись и сцена с флагом
    // =====================================================================

    /// <summary>Надпись над рисунком: новый текст «печатается», размер шрифта меняется плавно.</summary>
    public class CaptionLabel : FxControl
    {
        private string from = "", to = "";
        private float typing = 1f, size = 11f;
        private string face = "semi";

        public CaptionLabel()
        {
            Height = 40;
        }

        public string Caption { get { return to; } }
        public float FontSize { get { return size; } }

        public void SetText(string text, bool animate = true)
        {
            from = Shown(typing);
            to = text ?? "";
            if (!animate) { typing = 1f; Invalidate(); return; }
            Animator.Run(this, "type", 0, 1, 700, Ease.Linear, delegate(float v) { typing = v; Invalidate(); });
        }

        public void SetSize(float points, bool animate = true)
        {
            Animator.Run(this, "size", size, points, animate ? 520 : 0, Ease.OutBack, delegate(float v) { size = v; Invalidate(); });
        }

        public void SetFace(string fontFace)
        {
            face = fontFace;
            Invalidate();
        }

        // сначала стирается старый текст, затем печатается новый
        private string Shown(float t)
        {
            if (t >= 1f) return to;
            if (t < 0.35f)
            {
                int keep = (int)Math.Round(from.Length * (1 - t / 0.35f));
                return from.Substring(0, Math.Max(0, Math.Min(from.Length, keep)));
            }
            int n = (int)Math.Round(to.Length * (t - 0.35f) / 0.65f);
            return to.Substring(0, Math.Max(0, Math.Min(to.Length, n)));
        }

        protected override void Draw(Graphics g)
        {
            string s = Shown(typing);
            Font f = Fonts.Get(face, (float)Math.Round(size * 2) / 2f);
            float w = Gfx.TextWidth(s, f);
            if (s.Length > 0)
                using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(0, 0, Math.Max(60, w + 12), Height)))
                    Gfx.Text(g, s, f, b, new RectangleF(0, 0, Width, Height));
            if (typing < 1f)
            {
                float cx = w + Gfx.TextPad(f) / 2 + 3;
                Gfx.FillRound(g, P.Accent2, new RectangleF(cx, Height / 2f - size * 0.75f, 2.5f, size * 1.5f), 1);
            }
        }
    }

    /// <summary>
    /// Сцена с флагом: полотнище развевается, плавно сдвигается вправо, раскладывается мозаикой,
    /// переворачивается при смене рисунка; фон сцены плавно меняет цвет.
    /// </summary>
    public class FlagStage : FxControl
    {
        private Bitmap flag, pending;
        private readonly AnimValue pos, mosaic, tint;
        private float flip = 1f;
        private double time;
        public static readonly Color Pink = Color.FromArgb(255, 128, 255);   // &HFF80FF из задания

        public FlagStage()
        {
            pos = new AnimValue(this);
            mosaic = new AnimValue(this);
            tint = new AnimValue(this);
            Animator.Frame += OnFrame;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Frame -= OnFrame;
            base.Dispose(disposing);
        }

        private void OnFrame(float dt)
        {
            if (!IsHandleCreated || !Visible || Animator.Instant) return;
            time += dt;
            Invalidate();
        }

        public bool IsMosaic { get { return mosaic.Target > 0.5f; } }
        public bool IsRight { get { return pos.Target > 0.5f; } }
        public bool IsPink { get { return tint.Target > 0.5f; } }

        /// <summary>Смена рисунка: при animate = true флаг «переворачивается».</summary>
        public void SetFlag(Bitmap image, bool animate)
        {
            if (image == null) return;
            if (!animate || flag == null || !IsHandleCreated)
            {
                flag = image;
                Invalidate();
                return;
            }
            pending = image;
            Animator.Run(this, "flip", 0, 1, 620, Ease.InOutCubic, delegate(float v)
            {
                flip = v;
                if (v >= 0.5f && pending != null) { flag = pending; pending = null; }
                Invalidate();
            });
        }

        public void MoveRight(bool right) { pos.To(right ? 1f : 0f, 720, Ease.OutBack); }
        public void SetMosaic(bool on) { mosaic.To(on ? 1f : 0f, on ? 900 : 420, on ? (Func<float, float>)Ease.Linear : Ease.OutCubic); }
        public void SetPink(bool on) { tint.To(on ? 1f : 0f, 700, Ease.InOutCubic); }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Color back = Gfx.Mix(P.Dark ? Theme.Hex("0D1222") : Theme.Hex("EEF1F9"), Pink, tint.Value);
            using (GraphicsPath clip = Gfx.Round(r, 12))
            {
                using (var b = new SolidBrush(back)) g.FillPath(b, clip);
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);

                // точечная сетка сцены
                using (var dot = new SolidBrush(Gfx.A(tint.Value > 0.5f ? Color.White : P.TextMute, 46)))
                    for (int y = 14; y < Height; y += 22)
                        for (int x = 14; x < Width; x += 22)
                            g.FillEllipse(dot, x, y, 2, 2);

                if (flag != null)
                {
                    float m = mosaic.Value;
                    if (m > 0.001f) DrawMosaic(g, m);
                    if (m < 0.999f) DrawWaving(g, 1 - Gfx.Clamp(m * 2.5f, 0, 1));
                }
                else
                {
                    Gfx.Text(g, "рисунок не загружен", Fonts.Body(10f), P.TextMute, r, StringAlignment.Center);
                }
                g.Restore(st);
                using (var pen = new Pen(Gfx.Mix(P.Border, Color.White, tint.Value * 0.5f)))
                    g.DrawPath(pen, clip);
            }
        }

        private void DrawMosaic(Graphics g, float m)
        {
            int tw = flag.Width, th = flag.Height;
            int cols = (int)Math.Ceiling(Width / (float)tw), rows = (int)Math.Ceiling(Height / (float)th);
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < cols; i++)
                {
                    float local = Gfx.Clamp(m * 2.2f - (i + j) / (float)(cols + rows) * 1.2f, 0, 1);
                    if (local <= 0.01f) continue;
                    float s = Ease.OutBack(local);
                    float cx = i * tw + tw / 2f, cy = j * th + th / 2f;
                    g.DrawImage(flag, cx - tw * s / 2, cy - th * s / 2, tw * s, th * s);
                }
            }
        }

        private void DrawWaving(Graphics g, float alpha)
        {
            if (alpha <= 0.01f) return;
            float scale = Math.Min(2.2f, Math.Max(1.2f, (Height - 60) / (flag.Height * 1.9f)));
            float fw = flag.Width * scale, fh = flag.Height * scale;
            float pad = 34;
            float left = pad + pos.Value * (Width - fw - pad * 2);
            float top = 26;
            float flipScale = Math.Abs((float)Math.Cos(flip * Math.PI));
            if (flip >= 1f) flipScale = 1f;

            // древко
            using (var pole = new Pen(Gfx.A(P.Dark ? Theme.Hex("C9D2F0") : Theme.Hex("5A6485"), alpha), 4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                g.DrawLine(pole, left - 6, top - 10, left - 6, Math.Min(Height - 16, top + fh + 110));
            using (var b = new SolidBrush(Gfx.A(P.Accent2, alpha)))
                g.FillEllipse(b, left - 11, top - 19, 10, 10);

            GraphicsState st = g.Save();
            g.InterpolationMode = InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            const int strip = 2;
            int n = (int)Math.Ceiling(flag.Width / (float)strip);
            float centerX = left + fw / 2f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                float wave = (float)Math.Sin(time * 4.2 - i * 0.22) * 7f * t;
                float sx = i * strip;
                float sw = Math.Min(strip, flag.Width - sx);
                float x0 = left + sx * scale, x1 = left + (sx + sw) * scale + 0.6f;
                // переворот: полосы сжимаются к вертикальной оси флага
                x0 = centerX + (x0 - centerX) * flipScale;
                x1 = centerX + (x1 - centerX) * flipScale;
                if (x1 - x0 < 0.2f) continue;
                var dest = new RectangleF(x0, top + wave, x1 - x0, fh);
                if (alpha >= 0.99f) g.DrawImage(flag, dest, new RectangleF(sx, 0, sw, flag.Height), GraphicsUnit.Pixel);
                else Gfx.DrawImageAlpha(g, flag, dest, new RectangleF(sx, 0, sw, flag.Height), alpha);
                float shade = (float)Math.Cos(time * 4.2 - i * 0.22) * t;
                Color sc = shade > 0 ? Color.FromArgb((int)(60 * shade * alpha), 0, 0, 0) : Color.FromArgb((int)(-46 * shade * alpha), 255, 255, 255);
                using (var b = new SolidBrush(sc)) g.FillRectangle(b, dest);
            }
            g.Restore(st);
        }
    }

    /// <summary>
    /// Наглядная «отладка» цикла: указатель i шагает по ячейкам массива, сумма накапливается;
    /// в варианте с ошибкой указатель выходит за границу массива.
    /// </summary>
    public class ArrayDebugView : FxControl
    {
        private int[] data = new int[0];
        private int step = -1, sum;
        private bool bug = true, crashed, running;
        private readonly AnimValue pointer, flash;
        private float shake;

        public ArrayDebugView()
        {
            pointer = new AnimValue(this);
            flash = new AnimValue(this);
        }

        public bool Running { get { return running; } }

        public void SetData(int[] values)
        {
            data = values ?? new int[0];
            step = -1;
            sum = 0;
            crashed = false;
            Invalidate();
        }

        /// <summary>Пошаговое выполнение цикла; по окончании вызывается <paramref name="done"/>.</summary>
        public void Run(bool withBug, Action done)
        {
            bug = withBug;
            crashed = false;
            running = true;
            sum = 0;
            step = -1;
            pointer.Set(0);
            int steps = data.Length + (withBug ? 1 : 0);
            int last = -1;
            Animator.Run(this, "steps", 0, steps, steps * 430, Ease.Linear, delegate(float v)
            {
                int i = Math.Min(steps - 1, (int)v);
                if (i == last) return;
                last = i;
                step = i;
                pointer.To(i, 260, Ease.OutBack);
                if (i < data.Length)
                {
                    sum += data[i];
                    flash.Set(1f);
                    flash.To(0f, 380);
                }
                else
                {
                    crashed = true;
                    flash.Set(1f);
                    flash.To(0f, 900);
                    Animator.Run(this, "shake", 0, 1, 520, Ease.Linear, delegate(float t) { shake = (float)Math.Sin(t * Math.PI * 8) * (1 - t) * 7; Invalidate(); });
                }
                Invalidate();
            }, delegate
            {
                running = false;
                Invalidate();
                if (done != null) done();
            });
        }

        protected override void Draw(Graphics g)
        {
            int n = data.Length;
            Font mono = Fonts.Mono(9.5f), monoB = Fonts.MonoBold(11f);

            // строка кода с подсветкой условия цикла
            string a = "for (int i = 0; i ", cond = bug ? "<=" : "<", b = " values.Length; i++)  sum += values[i];";
            float x = 2;
            Gfx.Text(g, a, mono, P.TextDim, new RectangleF(x, 0, 400, 22));
            x += Gfx.TextWidth(a + "н", mono) - Gfx.TextWidth("н", mono);
            Color cc = cond == "<=" ? P.Bad : P.Good;
            float cw = Gfx.TextWidth(cond, mono) + 10;
            Gfx.FillRound(g, Gfx.A(cc, 50), new RectangleF(x, 2, cw, 18), 5);
            Gfx.Text(g, cond, Fonts.MonoBold(9.5f), cc, new RectangleF(x, 0, cw, 22), StringAlignment.Center);
            x += cw;
            Gfx.Text(g, b, mono, P.TextDim, new RectangleF(x, 0, Width - x, 22));

            // ячейки массива
            float top = 52;
            float cellW = Math.Min(74, (Width - 230) / (float)(n + 1)), cellH = 42, gap = 8;
            float ox = 4;
            for (int i = 0; i <= n; i++)
            {
                bool ghost = i == n;
                float cx = ox + i * (cellW + gap) + (ghost && crashed ? shake : 0);
                var cr = new RectangleF(cx, top, cellW, cellH);
                bool cur = i == step;
                bool visited = step >= i && i < n;
                if (ghost)
                {
                    Color gc = crashed ? P.Bad : P.TextMute;
                    if (crashed) Gfx.Glow(g, cr, 9, P.Bad, 4, 1.6f, (int)(30 + 60 * flash.Value));
                    Gfx.FillRound(g, Gfx.A(gc, crashed ? 46 : 14), cr, 9);
                    using (var pen = new Pen(Gfx.A(gc, crashed ? 255 : 150), 1.2f) { DashStyle = DashStyle.Dash })
                    using (GraphicsPath p = Gfx.Round(cr, 9))
                        g.DrawPath(pen, p);
                    Gfx.Text(g, crashed ? "?!" : "—", monoB, gc, cr, StringAlignment.Center);
                    Gfx.Text(g, "[" + i + "]", Fonts.Mono(8f), gc, new RectangleF(cx, top + cellH + 2, cellW, 16), StringAlignment.Center);
                    Gfx.Text(g, "вне массива", Fonts.Body(7.5f), gc, new RectangleF(cx - 14, top + cellH + 16, cellW + 28, 14), StringAlignment.Center);
                    continue;
                }
                Color fill = visited ? Gfx.A(P.Accent, 62) : P.Surface2;
                if (cur) Gfx.Glow(g, cr, 9, P.Accent2, 4, 1.5f, (int)(26 + 70 * flash.Value));
                Gfx.FillRound(g, fill, cr, 9);
                Gfx.DrawRound(g, cur ? P.Accent2 : visited ? Gfx.A(P.Accent, 190) : P.Border, cr, 9, cur ? 1.6f : 1f);
                Gfx.Text(g, data[i].ToString(), monoB, visited || cur ? P.Text : P.TextDim, cr, StringAlignment.Center);
                Gfx.Text(g, "[" + i + "]", Fonts.Mono(8f), P.TextMute, new RectangleF(cx, top + cellH + 2, cellW, 16), StringAlignment.Center);
            }

            // указатель i
            if (step >= 0)
            {
                float px = ox + pointer.Value * (cellW + gap) + cellW / 2f;
                Color pc = crashed ? P.Bad : P.Accent2;
                var pill = new RectangleF(px - 22, top - 25, 44, 18);
                Gfx.FillRound(g, pc, pill, 9);
                Gfx.Text(g, "i = " + step, Fonts.MonoBold(8f), P.Dark ? Theme.Hex("06121A") : Color.White, pill, StringAlignment.Center);
                using (var pb = new SolidBrush(pc))
                    g.FillPolygon(pb, new[] { new PointF(px - 5, top - 8), new PointF(px + 5, top - 8), new PointF(px, top - 2) });
            }

            // накопленная сумма
            float sx = ox + (n + 1) * (cellW + gap) + 14;
            var sr = new RectangleF(sx, top - 6, Math.Max(120, Width - sx - 4), cellH + 12);
            Gfx.FillRound(g, P.Surface2, sr, 10);
            Gfx.DrawRound(g, P.Border, sr, 10);
            Gfx.Text(g, "sum", Fonts.Mono(8.5f), P.TextMute, new RectangleF(sr.X + 12, sr.Y + 4, 60, 16));
            Gfx.Text(g, sum.ToString(), Fonts.Display(15f), crashed ? P.Bad : P.Text, new RectangleF(sr.X + 10, sr.Y + 18, sr.Width - 16, 32));
        }
    }

    // =====================================================================
    //  Лабораторная № 8: схема try / catch / finally
    // =====================================================================

    public sealed class FlowStep
    {
        public string Title, Detail;
        /// <summary>0 — выполнено без ошибок, 1 — исключение перехвачено, 2 — блок finally.</summary>
        public int State;

        public FlowStep(string title, string detail, int state)
        {
            Title = title;
            Detail = detail;
            State = state;
        }
    }

    /// <summary>Схема выполнения защищённых блоков: узлы загораются по очереди, между ними бежит импульс.</summary>
    public class FlowDiagram : FxControl
    {
        private List<FlowStep> steps = new List<FlowStep>();
        private float progress;

        public FlowDiagram()
        {
            Height = 96;
        }

        public void Play(List<FlowStep> list)
        {
            steps = list ?? new List<FlowStep>();
            Animator.Run(this, "flow", 0, steps.Count, Math.Max(1, steps.Count) * 340, Ease.Linear, delegate(float v) { progress = v; Invalidate(); });
        }

        public void Reset()
        {
            Animator.Cancel(this, "flow");
            steps = new List<FlowStep>();
            progress = 0;
            Invalidate();
        }

        protected override void Draw(Graphics g)
        {
            List<FlowStep> list = steps;
            bool idle = list.Count == 0;
            if (idle)
            {
                list = new List<FlowStep>
                {
                    new FlowStep("try", "чтение первого числа", 0),
                    new FlowStep("try", "чтение второго числа", 0),
                    new FlowStep("расчёт", "сумма, произведение, разность", 0),
                    new FlowStep("try", "деление", 0),
                    new FlowStep("finally", "выполняется всегда", 2)
                };
            }
            int n = list.Count;
            float gap = 26, w = (Width - gap * (n - 1) - 2) / n, h = Height - 8;
            for (int i = 0; i < n; i++)
            {
                FlowStep s = list[i];
                float lit = idle ? 0 : Ease.OutCubic(Gfx.Clamp(progress - i, 0, 1));
                Color c = s.State == 1 ? P.Warn : s.State == 2 ? P.Accent2 : P.Good;
                var r = new RectangleF(1 + i * (w + gap), 4, w, h);

                if (i < n - 1)
                {
                    float y = r.Y + h / 2;
                    using (var pen = new Pen(Gfx.Mix(P.Border, c, lit), 2f))
                        g.DrawLine(pen, r.Right + 3, y, r.Right + gap - 3, y);
                    float along = Gfx.Clamp(progress - i - 0.5f, 0, 1);
                    if (!idle && along > 0 && along < 1)
                        using (var b = new SolidBrush(P.Accent2))
                            g.FillEllipse(b, r.Right + along * gap - 4, y - 4, 8, 8);
                }

                if (lit > 0.01f) Gfx.Glow(g, r, 11, c, 4, 1.4f, (int)(40 * lit));
                Gfx.FillRound(g, Gfx.Mix(P.Surface2, Gfx.Mix(P.Surface2, c, 0.16f), lit), r, 11);
                Gfx.DrawRound(g, Gfx.Mix(P.Border, c, lit), r, 11, 1f + 0.5f * lit);
                float pop = idle ? 0.8f : Ease.OutBack(Gfx.Clamp((progress - i) * 1.6f, 0, 1));
                if (pop > 0.02f)
                    Icons.Draw(g, s.State == 1 ? "warn" : s.State == 2 ? "refresh" : "check",
                        new RectangleF(r.X + 10 + 8 * (1 - pop), r.Y + 10 + 8 * (1 - pop), 16 * pop, 16 * pop), idle ? P.TextMute : c, 1.8f);
                Gfx.Text(g, s.Title, Fonts.MonoBold(9f), idle ? P.TextMute : Gfx.Mix(P.TextDim, P.Text, lit), new RectangleF(r.X + 32, r.Y + 8, w - 38, 20));
                Gfx.Text(g, s.Detail, Fonts.Body(8.5f), idle ? P.TextMute : Gfx.Mix(P.TextMute, P.TextDim, lit),
                    new RectangleF(r.X + 10, r.Y + 32, w - 18, h - 38), StringAlignment.Near, StringAlignment.Near, true);
            }
        }
    }

    // =====================================================================
    //  Лабораторная № 9: лента смен
    // =====================================================================

    /// <summary>Журнал смен на шкале суток 0–24 ч: полоса каждой смены «вырастает» при добавлении.</summary>
    public class ShiftTimeline : FxControl
    {
        private sealed class Item
        {
            public WorkRecord R;
            public float Grow;
        }

        private readonly List<Item> items = new List<Item>();
        private readonly AnimValue scroll;
        private const int RowH = 36, Head = 26;

        public ShiftTimeline()
        {
            scroll = new AnimValue(this);
        }

        public void Add(WorkRecord record)
        {
            var it = new Item { R = record };
            items.Add(it);
            Animator.Run(it, "grow", 0, 1, 760, Ease.OutCubic, delegate(float v) { it.Grow = v; Invalidate(); });
            scroll.To(MaxScroll, 300, Ease.OutCubic);
        }

        private float MaxScroll { get { return Math.Max(0, items.Count * RowH + Head + 6 - Height); } }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll.To(Gfx.Clamp(scroll.Target - Math.Sign(e.Delta) * RowH * 2, 0, MaxScroll), 240, Ease.OutCubic);
        }

        protected override void Draw(Graphics g)
        {
            float nameW = Math.Min(210, Width * 0.26f), right = 86;
            float x0 = nameW + 8, x1 = Width - right;
            if (x1 - x0 < 60) return;
            Font mono = Fonts.Mono(8f);

            // шкала суток
            for (int hr = 0; hr <= 24; hr += 3)
            {
                float x = x0 + (x1 - x0) * hr / 24f;
                using (var pen = new Pen(hr % 6 == 0 ? P.Border : Gfx.A(P.Border, 120)))
                    g.DrawLine(pen, x, Head - 4, x, Height);
                Gfx.Text(g, hr.ToString("00") + ":00", mono, P.TextMute, new RectangleF(x - 24, 2, 48, 16), StringAlignment.Center);
            }
            if (items.Count == 0)
            {
                Gfx.Text(g, "записей пока нет — добавьте первую смену", Fonts.Body(9.5f), P.TextMute,
                    new RectangleF(0, Head, Width, Height - Head), StringAlignment.Center);
                return;
            }

            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(0, Head, Width, Height - Head), CombineMode.Intersect);
            float y = Head + 4 - scroll.Value;
            foreach (Item it in items)
            {
                if (y > -RowH && y < Height)
                {
                    WorkRecord r = it.R;
                    float a = Gfx.Clamp(it.Grow * 2.5f, 0, 1);
                    bool over = r.Hours > 8;
                    Gfx.Text(g, r.Employee, Fonts.Semi(9.5f), Gfx.A(P.Text, a), new RectangleF(4 + (1 - a) * 12, y, nameW - 4, RowH - 6));
                    float bs = x0 + (x1 - x0) * (float)(r.Start.TimeOfDay.TotalHours / 24.0);
                    float be = x0 + (x1 - x0) * (float)(r.End.TimeOfDay.TotalHours / 24.0);
                    float cur = bs + (be - bs) * it.Grow;
                    var bar = new RectangleF(bs, y + 5, Math.Max(4, cur - bs), RowH - 16);
                    Color c1 = over ? P.Warn : P.Accent, c2 = over ? P.Accent3 : P.Accent2;
                    Gfx.Glow(g, bar, 8, c1, 3, 1.3f, 26);
                    using (var b = new LinearGradientBrush(new RectangleF(bs - 1, bar.Y, Math.Max(6, be - bs + 2), bar.Height), c1, c2, 0f))
                        Gfx.FillRound(g, b, bar, 8);
                    if (it.Grow > 0.7f && be - bs > 88)
                        Gfx.Text(g, r.Start.ToString("HH:mm") + " – " + r.End.ToString("HH:mm"), Fonts.MonoBold(8f),
                            Gfx.A(Color.White, (it.Grow - 0.7f) / 0.3f), bar, StringAlignment.Center);
                    Gfx.Text(g, (r.Hours * it.Grow).ToString("F2", CultureInfo.GetCultureInfo("ru-RU")) + " ч", Fonts.MonoBold(9f),
                        over ? P.Warn : P.Text, new RectangleF(x1 + 6, y, right - 10, RowH - 6), StringAlignment.Far);
                }
                y += RowH;
            }
            g.Restore(st);
        }
    }

    // =====================================================================
    //  Главная страница: плитка лабораторной работы
    // =====================================================================

    /// <summary>Большая плитка-ссылка на лабораторную работу с собственной мини-иллюстрацией.</summary>
    public class LabTile : FxCard, IEnterAnim
    {
        private readonly AnimValue hover;
        private float appear = 1f;
        private double time;

        public string Number = "", Heading = "", Body = "", IconName = "bug";
        public int Art;

        public LabTile()
        {
            hover = new AnimValue(this);
            Radius = 18;
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
            if (hover.Value < 0.02f || !IsHandleCreated || !Visible) return;
            time += dt;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f, 240); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f, 360); }

        public void PrepareEnter() { appear = 0f; Invalidate(); }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "appear", 0, 1, 700, Ease.OutCubic, delegate(float v) { appear = v; Invalidate(); }, null, delayMs);
        }

        protected override void DrawContent(Graphics g)
        {
            float h = hover.Value, a = appear;
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath clip = Gfx.Round(r, Radius))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                if (h > 0.01f)
                    using (var b = new LinearGradientBrush(new RectangleF(-1, -1, Width + 2, Height + 2),
                               Gfx.A(P.Accent, (int)(44 * h)), Gfx.A(P.Accent2, (int)(10 * h)), 60f))
                        g.FillRectangle(b, r);
                Gfx.Text(g, Number, Fonts.Display(54f), Gfx.A(P.Text, (int)((P.Dark ? 16 : 14) + 22 * h)),
                    new RectangleF(Width - 190, -6, 186, 96), StringAlignment.Far);
                DrawArt(g, new RectangleF(22, Height - 150, Width - 44, 92), h);
                g.Restore(st);
                if (h > 0.01f)
                    using (var pen = new Pen(Gfx.A(P.Accent2, (int)(200 * h)), 1.5f))
                        g.DrawPath(pen, clip);
            }

            float dy = (1 - a) * 22;
            var badge = new RectangleF(24, 26 + dy - 4 * h, 50, 50);
            Gfx.Glow(g, badge, 14, P.Accent, 5, 1.6f, (int)(22 + 40 * h));
            using (LinearGradientBrush b = Gfx.TriBrush(RectangleF.Inflate(badge, 1, 1), 45f))
                Gfx.FillRound(g, b, badge, 14);
            Icons.Draw(g, IconName, RectangleF.Inflate(badge, -13, -13), Color.White, 2f);

            Gfx.Text(g, Heading, Fonts.Display(11.5f), Gfx.A(P.Text, a), new RectangleF(24, 92 + dy, Width - 44, 52),
                StringAlignment.Near, StringAlignment.Near, true);
            Gfx.Text(g, Body, Fonts.Body(9.5f), Gfx.A(P.TextDim, a), new RectangleF(24, 148 + dy, Width - 46, Math.Max(20, Height - 148 - 160)),
                StringAlignment.Near, StringAlignment.Near, true);

            float ax = 24 + 8 * h;
            Gfx.Text(g, "Открыть", Fonts.Semi(9.5f), Gfx.Mix(P.TextDim, P.Accent2, h), new RectangleF(24, Height - 46, 90, 22));
            Icons.Draw(g, "arrow", new RectangleF(ax + 66, Height - 43, 16, 16), Gfx.Mix(P.TextDim, P.Accent2, h), 1.8f);
        }

        // мини-иллюстрация: 0 — ячейки массива, 1 — узлы try/catch/finally, 2 — полосы смен
        private void DrawArt(Graphics g, RectangleF r, float h)
        {
            float t = (float)time;
            int alpha = (int)(70 + 120 * h);
            if (Art == 0)
            {
                float w = Math.Min(44, (r.Width - 40) / 6f);
                int cur = (int)(t * 2.4f) % 6;
                for (int i = 0; i < 6; i++)
                {
                    var c = new RectangleF(r.X + i * (w + 6), r.Y + 30, w, 36);
                    bool on = h > 0.05f && i == cur;
                    Gfx.FillRound(g, Gfx.A(on ? P.Accent2 : P.Accent, on ? 150 : alpha / 3), c, 8);
                    Gfx.DrawRound(g, Gfx.A(on ? P.Accent2 : P.BorderHi, alpha), c, 8);
                }
            }
            else if (Art == 1)
            {
                string[] names = { "try", "catch", "finally" };
                float w = Math.Min(92, (r.Width - 40) / 3f);
                int cur = (int)(t * 1.8f) % 3;
                for (int i = 0; i < 3; i++)
                {
                    var c = new RectangleF(r.X + i * (w + 18), r.Y + 30, w, 36);
                    bool on = h > 0.05f && i == cur;
                    Color col = i == 1 ? P.Warn : i == 2 ? P.Accent2 : P.Good;
                    Gfx.FillRound(g, Gfx.A(col, on ? 110 : alpha / 4), c, 9);
                    Gfx.DrawRound(g, Gfx.A(col, alpha), c, 9);
                    Gfx.Text(g, names[i], Fonts.MonoBold(8.5f), Gfx.A(P.Text, alpha + 40), c, StringAlignment.Center);
                    if (i < 2)
                        using (var pen = new Pen(Gfx.A(P.BorderHi, alpha), 2f))
                            g.DrawLine(pen, c.Right + 3, c.Y + 18, c.Right + 15, c.Y + 18);
                }
            }
            else
            {
                float[] s = { 0.30f, 0.38f, 0.22f }, e = { 0.72f, 0.80f, 0.60f };
                for (int i = 0; i < 3; i++)
                {
                    float grow = h > 0.05f ? 0.75f + 0.25f * (float)Math.Sin(t * 2 + i) : 1f;
                    var track = new RectangleF(r.X, r.Y + 22 + i * 22, r.Width, 12);
                    Gfx.FillRound(g, Gfx.A(P.BorderHi, alpha / 4), track, 6);
                    var bar = new RectangleF(r.X + r.Width * s[i], track.Y, r.Width * (e[i] - s[i]) * grow, 12);
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(bar, 1, 1), Gfx.A(P.Accent, alpha + 40), Gfx.A(P.Accent2, alpha + 40), 0f))
                        Gfx.FillRound(g, b, bar, 6);
                }
            }
        }
    }

    /// <summary>Шапка главной страницы: крупный заголовок и бегущие волны.</summary>
    public class HeroBanner : FxCard
    {
        private double time;
        public string Heading = "", Sub = "";
        public string[] Chips = new string[0];

        public HeroBanner()
        {
            Radius = 18;
            Animator.Frame += OnFrame;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Frame -= OnFrame;
            base.Dispose(disposing);
        }

        private void OnFrame(float dt)
        {
            if (!IsHandleCreated || !Visible || Animator.Instant) return;
            time += dt;
            Invalidate(new Rectangle((int)(Width * 0.58f), 2, (int)(Width * 0.42f) - 4, Height - 4));
        }

        protected override void DrawContent(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            using (GraphicsPath clip = Gfx.Round(r, Radius))
            {
                GraphicsState st = g.Save();
                g.SetClip(clip, CombineMode.Intersect);
                using (var b = new LinearGradientBrush(new RectangleF(-1, -1, Width + 2, Height + 2), Gfx.A(P.Accent, P.Dark ? 60 : 40), Gfx.A(P.Accent2, 0), 0f))
                    g.FillRectangle(b, r);
                // волны
                float x0 = Width * 0.6f;
                for (int k = 0; k < 4; k++)
                {
                    var pts = new List<PointF>();
                    for (float x = x0; x <= Width + 8; x += 8)
                    {
                        float u = (x - x0) / (Width - x0);
                        float y = Height * (0.36f + k * 0.12f) + (float)Math.Sin(u * 7 + time * (0.9 + k * 0.25) + k * 1.3) * (10 + k * 4) * u;
                        pts.Add(new PointF(x, y));
                    }
                    Color c = k % 2 == 0 ? P.Accent2 : P.Accent3;
                    using (var pen = new Pen(Gfx.A(c, 150 - k * 22), 2f))
                        g.DrawCurve(pen, pts.ToArray(), 0.5f);
                }
                g.Restore(st);
            }
            Font f = Fonts.Display(21f);
            using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(24, 0, Math.Max(80, Gfx.Measure(Heading, f).Width + 10), Height)))
                Gfx.Text(g, Heading, f, b, new RectangleF(26, 22, Width - 60, 44));
            Gfx.Text(g, Sub, Fonts.Body(10.5f), P.TextDim, new RectangleF(29, 68, Width * 0.56f, 44), StringAlignment.Near, StringAlignment.Near, true);
            float x1 = 29;
            foreach (string chip in Chips)
            {
                float w = Gfx.TextWidth(chip, Fonts.Semi(8.5f)) + 24;
                var cr = new RectangleF(x1, Height - 44, w, 24);
                Gfx.FillRound(g, Gfx.A(P.Surface2, 220), cr, 12);
                Gfx.DrawRound(g, P.BorderHi, cr, 12);
                Gfx.Text(g, chip, Fonts.Semi(8.5f), P.TextDim, cr, StringAlignment.Center);
                x1 += w + 8;
            }
        }
    }
}
