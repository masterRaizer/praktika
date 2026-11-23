using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Смысловой цвет текста; конкретный оттенок берётся из текущей темы.</summary>
    public enum Tone { Text, Dim, Mute, Accent, Accent2, Good, Warn, Bad }

    public static class Tones
    {
        public static Color Of(Tone t)
        {
            Palette p = Theme.P;
            switch (t)
            {
                case Tone.Dim: return p.TextDim;
                case Tone.Mute: return p.TextMute;
                case Tone.Accent: return p.Dark ? Gfx.Mix(p.Accent, Color.White, 0.25f) : p.Accent;
                case Tone.Accent2: return p.Accent2;
                case Tone.Good: return p.Good;
                case Tone.Warn: return p.Warn;
                case Tone.Bad: return p.Bad;
                default: return p.Text;
            }
        }
    }

    /// <summary>Элемент, у которого есть системные части (поля ввода), перекрашиваемые при смене темы.</summary>
    public interface IThemed
    {
        void ApplyTheme();
    }

    // =====================================================================
    //  Надпись
    // =====================================================================

    /// <summary>Надпись с плавным появлением нового текста.</summary>
    public class FxLabel : FxControl
    {
        private readonly AnimValue fade;
        private string icon;

        public Font Face = Fonts.Body(9.5f);
        public Tone Tone = Tone.Text;
        public bool Wrap;
        public bool AutoHeight;
        public bool Gradient;
        public StringAlignment Align = StringAlignment.Near;
        public StringAlignment VAlign = StringAlignment.Center;

        public FxLabel()
        {
            fade = new AnimValue(this, 1f);
            Height = 22;
        }

        public FxLabel(string text, Font face = null, Tone tone = Tone.Text, bool wrap = false) : this()
        {
            if (face != null) Face = face;
            Tone = tone;
            Wrap = wrap;
            AutoHeight = wrap;
            if (wrap) VAlign = StringAlignment.Near;
            Text = text;
        }

        public string Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            Fit();
            if (IsHandleCreated && Visible)
            {
                fade.Set(0.15f);
                fade.To(1f, 260);
            }
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Fit();
        }

        private bool fitting;

        private void Fit()
        {
            if (!AutoHeight || fitting || Width < 8) return;
            fitting = true;
            try
            {
                int pad = icon != null ? 22 : 0;
                int h = string.IsNullOrEmpty(Text) ? 0 : (int)Math.Ceiling(Gfx.Measure(Text, Face, Width - pad).Height) + 2;
                if (icon != null && h > 0) h = Math.Max(h, 20);
                if (Height != h) Height = h;
            }
            finally { fitting = false; }
        }

        protected override void Draw(Graphics g)
        {
            if (string.IsNullOrEmpty(Text)) return;
            float x = 0;
            Color c = Gfx.A(Tones.Of(Tone), fade.Value);
            if (icon != null)
            {
                Icons.Draw(g, icon, new RectangleF(0, Wrap ? 1 : (Height - 16) / 2f, 16, 16), c, 1.6f);
                x = 22;
            }
            var r = new RectangleF(x, 0, Width - x, Height);
            if (Gradient)
            {
                using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(0, 0, Math.Max(40, Gfx.Measure(Text, Face).Width + x), Height)))
                    Gfx.Text(g, Text, Face, b, r, Align, VAlign, Wrap);
            }
            else
            {
                Gfx.Text(g, Text, Face, c, r, Align, VAlign, Wrap);
            }
        }
    }

    // =====================================================================
    //  Кнопка
    // =====================================================================

    /// <summary>Кнопка с градиентом, свечением при наведении и «волной» от места нажатия.</summary>
    public class FxButton : FxControl
    {
        private readonly AnimValue hover, press, ripple;
        private Point ripplePoint;
        private string icon;

        public bool Primary;
        public bool Danger;
        public int Radius = 10;

        public FxButton()
        {
            SetStyle(ControlStyles.Selectable, true);
            hover = new AnimValue(this);
            press = new AnimValue(this);
            ripple = new AnimValue(this, 1f);
            Height = 36;
            Width = 160;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public FxButton(string text, bool primary = false, string icon = null) : this()
        {
            Text = text;
            Primary = primary;
            this.icon = icon;
        }

        public string Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        /// <summary>Ширина, достаточная для текста и значка.</summary>
        public FxButton AutoWidth(int extra = 0)
        {
            Width = (int)Gfx.Measure(Text, Fonts.Semi(9.5f)).Width + (icon != null ? 26 : 0) + 34 + extra;
            return this;
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f, 260); press.To(0f); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            press.To(1f, 90);
            ripplePoint = e.Location;
            ripple.Set(0f);
            ripple.To(1f, 520, Ease.OutCubic);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            press.To(0f, 220, Ease.OutBack);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)
            {
                ripplePoint = new Point(Width / 2, Height / 2);
                ripple.Set(0f);
                ripple.To(1f, 520);
                OnClick(EventArgs.Empty);
            }
        }

        protected override void Draw(Graphics g)
        {
            float inset = 1.5f * press.Value;
            var r = new RectangleF(1 + inset, 1 + inset, Width - 3 - inset * 2, Height - 3 - inset * 2);
            float h = Enabled ? hover.Value : 0f;
            Color textColor;

            using (GraphicsPath path = Gfx.Round(r, Radius))
            {
                if (Primary)
                {
                    Color c1 = Danger ? P.Bad : P.Accent, c2 = Danger ? P.Accent3 : P.Accent2;
                    if (!Enabled) { c1 = Gfx.Mix(c1, P.Surface, 0.6f); c2 = Gfx.Mix(c2, P.Surface, 0.6f); }
                    if (h > 0.01f) Gfx.Glow(g, r, Radius, c1, 4, 1.2f, (int)(46 * h));
                    using (var b = new LinearGradientBrush(new RectangleF(r.X - 1, r.Y, r.Width + 2, r.Height),
                               Gfx.Mix(c1, Color.White, 0.10f * h), Gfx.Mix(c2, Color.White, 0.16f * h), 20f))
                        g.FillPath(b, path);
                    using (var hl = new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, r.Width, r.Height / 2 + 2),
                               Color.FromArgb(46, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90f))
                        Gfx.FillRound(g, hl, new RectangleF(r.X, r.Y, r.Width, r.Height / 2), Radius);
                    textColor = Color.White;
                }
                else
                {
                    using (var b = new SolidBrush(Gfx.Mix(P.Surface2, P.SurfaceHi, h)))
                        g.FillPath(b, path);
                    using (var pen = new Pen(Gfx.Mix(P.Border, P.Accent, h * 0.9f)))
                        g.DrawPath(pen, path);
                    textColor = Enabled ? Gfx.Mix(P.TextDim, P.Text, 0.5f + 0.5f * h) : P.TextMute;
                }

                if (ripple.Value < 0.999f)
                {
                    GraphicsState st = g.Save();
                    g.SetClip(path, CombineMode.Intersect);
                    float rad = ripple.Value * (Width + 30);
                    using (var b = new SolidBrush(Color.FromArgb((int)(70 * (1 - ripple.Value)), Primary ? Color.White : P.Accent2)))
                        g.FillEllipse(b, ripplePoint.X - rad, ripplePoint.Y - rad, rad * 2, rad * 2);
                    g.Restore(st);
                }

                if (Focused && TabStop)
                    Gfx.DrawRound(g, Gfx.A(P.Accent2, 170), RectangleF.Inflate(r, 0.5f, 0.5f), Radius, 1.4f);
            }

            Font f = Fonts.Semi(9.5f);
            float tw = Gfx.Measure(Text, f).Width;
            float total = tw + (icon != null ? 22 : 0);
            float x = Math.Max(8, (Width - total) / 2f);
            if (icon != null)
            {
                Icons.Draw(g, icon, new RectangleF(x, (Height - 16) / 2f, 16, 16), textColor, 1.7f);
                x += 22;
            }
            Gfx.Text(g, Text, f, textColor, new RectangleF(x, 0, Width - x - 4, Height - 1));
        }
    }

    // =====================================================================
    //  Переключатель вариантов
    // =====================================================================

    /// <summary>Сегментированный переключатель: выбранный вариант отмечает плавно скользящая «таблетка».</summary>
    public class FxSegmented : FxControl
    {
        private string[] items = new string[0];
        private int selected = -1, hoverIndex = -1;
        private readonly AnimValue pos;

        public event EventHandler SelectedIndexChanged;

        public FxSegmented()
        {
            pos = new AnimValue(this);
            Height = 34;
            Cursor = Cursors.Hand;
        }

        public FxSegmented(params string[] items) : this()
        {
            Items = items;
            if (items.Length > 0) { selected = 0; }
        }

        public string[] Items
        {
            get { return items; }
            set { items = value ?? new string[0]; Invalidate(); }
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value == selected || value < -1 || value >= items.Length) return;
                selected = value;
                pos.To(Math.Max(0, value), 300, Ease.OutBack);
                EventHandler h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
                Invalidate();
            }
        }

        public string SelectedText { get { return selected >= 0 ? items[selected] : ""; } }

        /// <summary>Границы сегментов: ширина пропорциональна длине подписи.</summary>
        private float[] Edges()
        {
            int n = items.Length;
            var e = new float[n + 1];
            if (n == 0) return e;
            Font f = Fonts.Semi(9f);
            var w = new float[n];
            float total = 0;
            for (int i = 0; i < n; i++)
            {
                w[i] = Gfx.Measure(items[i], f).Width + 22;
                total += w[i];
            }
            float avail = Width - 6f, x = 3;
            for (int i = 0; i < n; i++)
            {
                e[i] = x;
                x += avail * w[i] / total;
            }
            e[n] = x;
            return e;
        }

        private int HitIndex(int x)
        {
            if (items.Length == 0) return -1;
            float[] e = Edges();
            for (int i = 0; i < items.Length; i++)
                if (x < e[i + 1]) return i;
            return items.Length - 1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = HitIndex(e.X);
            if (i != hoverIndex) { hoverIndex = i; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) SelectedIndex = HitIndex(e.X);
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, P.Surface2, r, 10);
            Gfx.DrawRound(g, P.Border, r, 10);
            int n = items.Length;
            if (n == 0) return;
            float[] e = Edges();
            if (selected >= 0)
            {
                // «таблетка» плавно меняет и положение, и ширину
                float pv = Gfx.Clamp(pos.Value, -0.3f, n - 0.7f);
                int i0 = (int)Math.Floor(Gfx.Clamp(pv, 0, n - 1)), i1 = Math.Min(n - 1, i0 + 1);
                float fr = pv - i0;
                float left = e[i0] + (e[i1] - e[i0]) * fr, right = e[i0 + 1] + (e[i1 + 1] - e[i0 + 1]) * fr;
                var pill = new RectangleF(left, 3, right - left, Height - 7);
                Gfx.Glow(g, pill, 8, P.Accent, 3, 1.2f, 30);
                using (LinearGradientBrush b = Gfx.AccentBrush(RectangleF.Inflate(pill, 1, 0), 15f))
                    Gfx.FillRound(g, b, pill, 8);
            }
            Font f = Fonts.Semi(9f);
            for (int i = 0; i < n; i++)
            {
                float near = 1 - Math.Min(1, Math.Abs(pos.Value - i));
                Color c = selected < 0 ? P.TextDim : Gfx.Mix(i == hoverIndex ? P.Text : P.TextDim, Color.White, near);
                GraphicsState st = g.Save();
                g.SetClip(new RectangleF(e[i] + 1, 0, e[i + 1] - e[i] - 2, Height), CombineMode.Intersect);
                Gfx.Text(g, items[i], f, c, new RectangleF(e[i] + 1, 0, e[i + 1] - e[i] - 2, Height - 1), StringAlignment.Center);
                g.Restore(st);
            }
        }
    }

    // =====================================================================
    //  Ползунок
    // =====================================================================

    /// <summary>Ползунок с градиентной дорожкой; при программной установке значения бегунок плавно переезжает.</summary>
    public class FxSlider : FxControl
    {
        private int min, max = 100, value;
        private bool dragging;
        private readonly AnimValue thumb, hover;

        public event EventHandler ValueChanged;

        /// <summary>Текст подсказки над бегунком.</summary>
        public Func<int, string> Format;

        public FxSlider()
        {
            thumb = new AnimValue(this);
            hover = new AnimValue(this);
            Height = 30;
            Cursor = Cursors.Hand;
        }

        public FxSlider(int min, int max, int value) : this()
        {
            this.min = min;
            this.max = max;
            this.value = Math.Max(min, Math.Min(max, value));
            thumb.Set(this.value);
        }

        public int Minimum { get { return min; } set { min = value; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; Invalidate(); } }

        public int Value
        {
            get { return value; }
            set { SetValue(value, true); }
        }

        private void SetValue(int v, bool animate)
        {
            v = Math.Max(min, Math.Min(max, v));
            if (v == value) return;
            value = v;
            if (animate && IsHandleCreated && Visible) thumb.To(v, 420, Ease.OutCubic);
            else thumb.Set(v);
            EventHandler h = ValueChanged;
            if (h != null) h(this, EventArgs.Empty);
        }

        private const int Pad = 11;

        private float XOf(float v)
        {
            return max == min ? Pad : Pad + (v - min) / (max - min) * (Width - Pad * 2);
        }

        private int ValueAt(int x)
        {
            float t = (x - Pad) / (float)Math.Max(1, Width - Pad * 2);
            return (int)Math.Round(min + Gfx.Clamp(t, 0, 1) * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            hover.To(1f);
            SetValue(ValueAt(e.X), true);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SetValue(ValueAt(e.X), false);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            if (!ClientRectangle.Contains(e.Location)) hover.To(0f, 260);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!dragging) hover.To(0f, 260); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            SetValue(value + Math.Sign(e.Delta), true);
        }

        protected override void Draw(Graphics g)
        {
            float cy = Height / 2f + 1;
            var track = new RectangleF(Pad - 3, cy - 3, Width - Pad * 2 + 6, 6);
            Gfx.FillRound(g, P.Surface2, track, 3);
            Gfx.DrawRound(g, P.Border, track, 3);
            float x = XOf(thumb.Value);
            var fill = new RectangleF(track.X, track.Y, Math.Max(6, x - track.X + 3), 6);
            using (LinearGradientBrush b = Gfx.AccentBrush(new RectangleF(track.X - 1, track.Y, track.Width + 2, 6)))
                Gfx.FillRound(g, b, fill, 3);

            float rad = 7f + 1.5f * hover.Value;
            Gfx.Glow(g, new RectangleF(x - rad, cy - rad, rad * 2, rad * 2), rad, P.Accent2, 4, 1.4f, (int)(18 + 46 * hover.Value));
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, x - rad, cy - rad, rad * 2, rad * 2);
            using (var pen = new Pen(P.Accent, 2.4f))
                g.DrawEllipse(pen, x - rad + 1.2f, cy - rad + 1.2f, rad * 2 - 2.4f, rad * 2 - 2.4f);
        }
    }

    // =====================================================================
    //  Числовое поле
    // =====================================================================

    /// <summary>Числовое поле с кнопками «−» и «+» (замена NumericUpDown).</summary>
    public class FxNumber : FxControl, IThemed
    {
        private readonly TextBox box = new TextBox();
        private readonly Timer repeat = new Timer();
        private readonly AnimValue focus;
        private decimal min, max = 100, value, increment = 1;
        private int decimals, hoverPart, pressPart, repeatDir;
        private bool settingText;
        private const int Btn = 28;

        public event EventHandler ValueChanged;

        /// <summary>Минимальное число цифр целого значения (2 — «09» вместо «9»).</summary>
        public int Digits = 1;

        public FxNumber()
        {
            focus = new AnimValue(this);
            Size = new Size(118, 34);
            box.BorderStyle = BorderStyle.None;
            box.TextAlign = HorizontalAlignment.Center;
            box.Font = Fonts.Semi(10f);
            box.GotFocus += delegate { focus.To(1f); };
            box.LostFocus += delegate { focus.To(0f, 240); Commit(); };
            box.KeyDown += Box_KeyDown;
            box.MouseWheel += delegate(object s, MouseEventArgs e)
            {
                Step(Math.Sign(e.Delta));
                var h = e as HandledMouseEventArgs;
                if (h != null) h.Handled = true;   // иначе сообщение дойдёт до родителя и шаг выполнится дважды
            };
            Controls.Add(box);
            repeat.Tick += delegate
            {
                repeat.Interval = 55;
                Step(repeatDir);
            };
            ApplyTheme();
            ShowValue();
        }

        public FxNumber(decimal min, decimal max, decimal value, int decimals, decimal step) : this()
        {
            this.min = min;
            this.max = max;
            this.decimals = decimals;
            increment = step;
            this.value = Math.Max(min, Math.Min(max, value));
            ShowValue();
        }

        public decimal Minimum { get { return min; } set { min = value; } }
        public decimal Maximum { get { return max; } set { max = value; } }
        public decimal Increment { get { return increment; } set { increment = value; } }
        public int DecimalPlaces { get { return decimals; } set { decimals = value; ShowValue(); } }

        public decimal Value
        {
            get { return value; }
            set
            {
                decimal v = Math.Round(Math.Max(min, Math.Min(max, value)), decimals);
                if (v == this.value) { ShowValue(); return; }
                this.value = v;
                ShowValue();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public void ApplyTheme()
        {
            box.BackColor = P.Surface2;
            box.ForeColor = P.Text;
            Invalidate();
        }

        private void ShowValue()
        {
            settingText = true;
            box.Text = decimals == 0 && Digits > 1
                ? ((long)value).ToString("D" + Digits, CultureInfo.CurrentCulture)
                : value.ToString("F" + decimals, CultureInfo.CurrentCulture);
            settingText = false;
        }

        private void Commit()
        {
            if (settingText) return;
            decimal v;
            string s = box.Text.Trim().Replace(" ", "");
            string sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            s = s.Replace(".", sep).Replace(",", sep);
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.CurrentCulture, out v)) Value = v;
            else ShowValue();
        }

        private void Box_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter) { Commit(); box.SelectAll(); e.SuppressKeyPress = true; }
            else if (e.KeyCode == Keys.Up) { Commit(); Step(1); e.Handled = true; }
            else if (e.KeyCode == Keys.Down) { Commit(); Step(-1); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { ShowValue(); e.SuppressKeyPress = true; }
        }

        private void Step(int dir)
        {
            Value = value + dir * increment;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int h = box.PreferredHeight;
            box.SetBounds(Btn + 2, (Height - h) / 2, Math.Max(10, Width - Btn * 2 - 4), h);
        }

        private int PartAt(int x)
        {
            return x < Btn ? -1 : x >= Width - Btn ? 1 : 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int p = PartAt(e.X);
            if (p != hoverPart) { hoverPart = p; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverPart = 0;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int p = PartAt(e.X);
            if (e.Button != MouseButtons.Left || p == 0) return;
            Commit();
            pressPart = repeatDir = p;
            Step(p);
            repeat.Interval = 380;
            repeat.Start();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            repeat.Stop();
            pressPart = 0;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Step(Math.Sign(e.Delta));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) repeat.Dispose();
            base.Dispose(disposing);
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            if (focus.Value > 0.01f) Gfx.Glow(g, r, 9, P.Accent, 3, 1f, (int)(40 * focus.Value));
            Gfx.FillRound(g, P.Surface2, r, 9);
            Gfx.DrawRound(g, Gfx.Mix(P.Border, P.Accent, focus.Value), r, 9);
            DrawPart(g, -1, new RectangleF(3, 3, Btn - 4, Height - 7), "minus");
            DrawPart(g, 1, new RectangleF(Width - Btn, 3, Btn - 4, Height - 7), "plus");
        }

        private void DrawPart(Graphics g, int part, RectangleF r, string icon)
        {
            bool hot = hoverPart == part, down = pressPart == part;
            if (hot || down) Gfx.FillRound(g, down ? Gfx.A(P.Accent, 90) : P.SurfaceHi, r, 7);
            Icons.Draw(g, icon, new RectangleF(r.X + (r.Width - 12) / 2, r.Y + (r.Height - 12) / 2, 12, 12), hot ? P.Text : P.TextDim, 1.8f);
        }
    }

    // =====================================================================
    //  Поле ввода текста
    // =====================================================================

    /// <summary>Поле ввода в скруглённой рамке: подсветка при фокусе, «встряска» и красная рамка при ошибке.</summary>
    public class FxTextBox : FxControl, IThemed
    {
        private readonly TextBox box = new TextBox();
        private readonly AnimValue focus, error;
        private string placeholder = "";
        private int shakeBase = int.MinValue;

        public event EventHandler ValueChanged;
        public event EventHandler EnterPressed;

        public FxTextBox()
        {
            focus = new AnimValue(this);
            error = new AnimValue(this);
            Size = new Size(220, 36);
            box.BorderStyle = BorderStyle.None;
            box.Font = Fonts.Medium(10.5f);
            box.GotFocus += delegate { focus.To(1f); Invalidate(); };
            box.LostFocus += delegate { focus.To(0f, 240); Invalidate(); };
            box.TextChanged += delegate
            {
                Invalidate();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
            box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode != Keys.Enter) return;
                e.SuppressKeyPress = true;
                EventHandler h = EnterPressed;
                if (h != null) h(this, EventArgs.Empty);
            };
            box.HandleCreated += delegate { Native.SetCue(box, placeholder); };
            Controls.Add(box);
            Cursor = Cursors.IBeam;
            ApplyTheme();
        }

        public TextBox Box { get { return box; } }

        public string Value
        {
            get { return box.Text; }
            set { box.Text = value ?? ""; }
        }

        public string Placeholder
        {
            get { return placeholder; }
            set { placeholder = value ?? ""; Native.SetCue(box, placeholder); }
        }

        public void ApplyTheme()
        {
            box.BackColor = P.Surface2;
            box.ForeColor = P.Text;
            Invalidate();
        }

        public void FocusBox() { box.Focus(); }

        /// <summary>Сигнал об ошибке ввода: красная рамка и затухающая «встряска».</summary>
        public void Shake()
        {
            error.Set(1f);
            error.To(0f, 1400, Ease.InCubic);
            if (shakeBase == int.MinValue) shakeBase = Left;
            int origin = shakeBase;
            Animator.Run(this, "shake", 0, 1, 460, Ease.Linear, delegate(float t)
            {
                Left = origin + (int)Math.Round(Math.Sin(t * Math.PI * 7) * (1 - t) * 9);
            }, delegate { Left = origin; shakeBase = int.MinValue; });
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (box.Multiline)
            {
                box.SetBounds(12, 9, Math.Max(10, Width - 24), Math.Max(10, Height - 18));
                return;
            }
            int h = box.PreferredHeight;
            box.SetBounds(12, (Height - h) / 2, Math.Max(10, Width - 24), h);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            box.Focus();
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Color ring = Gfx.Mix(Gfx.Mix(P.Border, P.Accent, focus.Value), P.Bad, error.Value);
            if (focus.Value > 0.01f || error.Value > 0.01f)
                Gfx.Glow(g, r, 10, error.Value > 0.01f ? P.Bad : P.Accent, 3, 1f, (int)(44 * Math.Max(focus.Value, error.Value)));
            Gfx.FillRound(g, P.Surface2, r, 10);
            Gfx.DrawRound(g, ring, r, 10, 1f + 0.4f * Math.Max(focus.Value, error.Value));
        }
    }

    // =====================================================================
    //  Флажок-«чип» и тумблер
    // =====================================================================

    /// <summary>Отмечаемый «чип» с цветной точкой (замена строки CheckedListBox).</summary>
    public class FxChip : FxControl
    {
        private bool on;
        private readonly AnimValue state, hover;

        /// <summary>Номер цвета ряда из палитры темы; −1 — акцентный цвет.</summary>
        public int SeriesIndex = -1;

        public event EventHandler CheckedChanged;

        public FxChip()
        {
            state = new AnimValue(this);
            hover = new AnimValue(this);
            Height = 30;
            Cursor = Cursors.Hand;
        }

        public FxChip(string text, int seriesIndex, bool isChecked) : this()
        {
            Text = text;
            SeriesIndex = seriesIndex;
            on = isChecked;
            state.Set(on ? 1 : 0);
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                state.To(on ? 1f : 0f, 240, Ease.OutBack);
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover.To(0f, 240); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void Draw(Graphics g)
        {
            Color col = SeriesIndex >= 0 ? P.Series[SeriesIndex % P.Series.Length] : P.Accent;
            float s = Gfx.Clamp(state.Value, 0, 1.2f);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, Gfx.Mix(Gfx.Mix(P.Surface2, P.SurfaceHi, hover.Value), col, 0.13f * Math.Min(1, s)), r, 9);
            Gfx.DrawRound(g, Gfx.Mix(P.Border, col, 0.75f * Math.Min(1, s)), r, 9);
            float cy = Height / 2f;
            float rad = 4f + 2f * s;
            if (s > 0.05f) Gfx.Glow(g, new RectangleF(15 - rad, cy - rad, rad * 2, rad * 2), rad, col, 3, 1.3f, (int)(40 * Math.Min(1, s)));
            using (var b = new SolidBrush(Gfx.Mix(P.TextMute, col, Math.Min(1, s))))
                g.FillEllipse(b, 15 - rad, cy - rad, rad * 2, rad * 2);
            if (s < 0.6f)
                using (var b = new SolidBrush(Gfx.Mix(P.Surface2, P.SurfaceHi, hover.Value)))
                    g.FillEllipse(b, 15 - rad + 2, cy - rad + 2, rad * 2 - 4, rad * 2 - 4);
            Gfx.Text(g, Text, Fonts.Medium(9f), Gfx.Mix(P.TextMute, P.Text, 0.45f + 0.55f * Math.Min(1, s)),
                new RectangleF(30, 0, Width - 34, Height - 1));
        }
    }

    /// <summary>Тумблер «вкл/выкл» с плавно скользящей ручкой.</summary>
    public class FxSwitch : FxControl
    {
        private bool on;
        private readonly AnimValue state;

        public string IconOff, IconOn;
        public event EventHandler CheckedChanged;

        public FxSwitch()
        {
            state = new AnimValue(this);
            Size = new Size(46, 24);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return on; }
            set
            {
                if (on == value) return;
                on = value;
                state.To(on ? 1f : 0f, 260, Ease.OutBack);
                EventHandler h = CheckedChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        /// <summary>Установка без события (синхронизация состояния).</summary>
        public void SetSilently(bool value)
        {
            on = value;
            state.Set(on ? 1 : 0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void Draw(Graphics g)
        {
            float s = Gfx.Clamp(state.Value, 0, 1);
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            float rad = r.Height / 2;
            using (var b = new LinearGradientBrush(RectangleF.Inflate(r, 1, 1),
                       Gfx.Mix(P.Surface2, P.Accent, s), Gfx.Mix(P.SurfaceHi, P.Accent2, s), 0f))
                Gfx.FillRound(g, b, r, rad);
            Gfx.DrawRound(g, Gfx.Mix(P.Border, P.Accent2, s * 0.6f), r, rad);
            float d = Height - 7;
            float x = 3 + state.Value * (Width - d - 7);
            using (var b = new SolidBrush(Color.White))
                g.FillEllipse(b, x, 3, d, d);
            string ic = s > 0.5f ? IconOn : IconOff;
            if (ic != null) Icons.Draw(g, ic, new RectangleF(x + 3, 6, d - 6, d - 6), P.Dark ? Theme.Hex("3B2FB0") : Theme.Hex("C07A00"), 1.6f);
        }
    }

    // =====================================================================
    //  Плитка с показателем
    // =====================================================================

    /// <summary>Плитка-показатель: число «докручивается» до нового значения.</summary>
    public class FxStat : FxCard, IEnterAnim
    {
        private string caption = "", sub = "", textValue;
        private double number, shown;
        private int decimals = 2;
        private string suffix = "";
        private bool hasNumber;
        private float appear = 1f;

        public int SeriesIndex = -1;   // цвет метки: ряд палитры
        public Tone Tone = Tone.Accent2;
        public bool Hero;

        public FxStat()
        {
            Padding = new Padding(14, 10, 12, 10);
            Radius = 12;
            Height = 78;
        }

        public FxStat(string caption, int seriesIndex = -1) : this()
        {
            this.caption = caption;
            SeriesIndex = seriesIndex;
        }

        public string Caption
        {
            get { return caption; }
            set { caption = value ?? ""; Invalidate(); }
        }

        public string Sub
        {
            get { return sub; }
            set { if (sub == value) return; sub = value ?? ""; Invalidate(); }
        }

        public void Set(double value, int digits = 2, string unit = "")
        {
            decimals = digits;
            suffix = unit ?? "";
            textValue = null;
            if (double.IsNaN(value) || double.IsInfinity(value)) { SetText("—"); return; }
            double from = hasNumber ? shown : value * 0.0;
            hasNumber = true;
            number = value;
            if (!IsHandleCreated || !Visible) { shown = value; Invalidate(); return; }
            Animator.Run(this, "num", 0, 1, 520, Ease.OutCubic, delegate(float t)
            {
                shown = from + (number - from) * t;
                Invalidate();
            });
        }

        public void SetText(string text)
        {
            Animator.Cancel(this, "num");
            hasNumber = false;
            textValue = text ?? "—";
            Invalidate();
        }

        public void PrepareEnter()
        {
            appear = 0f;
            if (hasNumber) shown = 0;
            Invalidate();
        }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "appear", 0, 1, 520, Ease.OutCubic, delegate(float t)
            {
                appear = t;
                if (hasNumber) shown = number * t;
                Invalidate();
            }, null, delayMs);
        }

        /// <summary>Прописные буквы, кроме греческих (α, β остаются строчными, как в формулах).</summary>
        private static string Upper(string s)
        {
            char[] a = s.ToCharArray();
            for (int i = 0; i < a.Length; i++)
                if (a[i] < 0x370 || a[i] > 0x3FF) a[i] = char.ToUpperInvariant(a[i]);
            return new string(a);
        }

        protected override void DrawContent(Graphics g)
        {
            Color mark = SeriesIndex >= 0 ? P.Series[SeriesIndex % P.Series.Length] : Tones.Of(Tone);
            float x = Padding.Left;
            using (var b = new SolidBrush(mark))
                g.FillEllipse(b, x, 13, 7, 7);
            Gfx.Text(g, Upper(caption), Fonts.Bold(7.5f), P.TextMute, new RectangleF(x + 13, 6, Width - x - 20, 20));

            string v = hasNumber
                ? shown.ToString("F" + decimals, CultureInfo.GetCultureInfo("ru-RU")) + suffix
                : (textValue ?? "—");
            Font f = Fonts.Display(v.Length > 11 ? 10.5f : v.Length > 8 ? 12.5f : 14.5f);
            float dy = (1 - appear) * 10;
            var vr = new RectangleF(x - 1, 24 + dy, Width - x - 8, 30);
            if (Hero)
            {
                using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(x - 2, 0, Math.Max(60, Gfx.Measure(v, f).Width + 6), Height)))
                    Gfx.Text(g, v, f, b, vr);
            }
            else
            {
                Gfx.Text(g, v, f, Gfx.A(P.Text, appear), vr);
            }
            if (sub.Length > 0)
                Gfx.Text(g, sub, Fonts.Body(8f), Gfx.A(P.TextDim, appear), new RectangleF(x, Height - 22, Width - x - 8, 16));
        }
    }

    // =====================================================================
    //  Многострочный текст с прокруткой
    // =====================================================================

    /// <summary>
    /// Текстовый блок с собственной вёрсткой: абзацы, выделение «заголовок: значение»,
    /// заголовки разделов (строки ПРОПИСНЫМИ), плавная прокрутка колесом.
    /// </summary>
    public class FxText : FxControl, IEnterAnim
    {
        private sealed class Seg
        {
            public string S;
            public Font F;
            public int Kind;   // 0 — текст, 1 — «ключ», 2 — заголовок раздела
            public float X, Y;
            public int Para;
        }

        private readonly List<Seg> segs = new List<Seg>();
        private readonly AnimValue scroll;
        private float contentHeight, enter = 1f;
        private int laidWidth = -1, paraCount;
        private string laidText;
        private bool dragBar;
        private int dragY;
        private float dragScroll;

        public float FontSize = 9.5f;
        public bool KeyHighlight = true;
        public bool AutoHeight;

        public FxText()
        {
            scroll = new AnimValue(this);
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            laidText = null;
            Relayout();
            Invalidate();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            Relayout();
        }

        public void PrepareEnter() { enter = 0f; Invalidate(); }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "enter", 0, 1, 620, Ease.OutCubic, delegate(float t) { enter = t; Invalidate(); }, null, delayMs);
        }

        private static bool IsHeading(string line)
        {
            if (line.Length < 4) return false;
            bool letters = false;
            foreach (char ch in line)
            {
                if (char.IsLetter(ch))
                {
                    letters = true;
                    if (char.IsLower(ch)) return false;
                }
            }
            return letters;
        }

        private void Relayout()
        {
            int w = Width - 10;
            if (w < 30) return;
            if (laidText == Text && laidWidth == w) return;
            laidText = Text;
            laidWidth = w;
            segs.Clear();
            Font body = Fonts.Body(FontSize), key = Fonts.Semi(FontSize), head = Fonts.Display(FontSize - 1.5f);
            float lineH = body.GetHeight() * 1.32f;
            float y = 0;
            int para = 0;
            string[] lines = (Text ?? "").Replace("\r", "").Split('\n');
            foreach (string raw in lines)
            {
                string line = raw.Trim();
                if (line.Length == 0) { y += lineH * 0.55f; continue; }
                if (IsHeading(line))
                {
                    if (y > 0) y += lineH * 0.35f;
                    segs.Add(new Seg { S = line, F = head, Kind = 2, X = 0, Y = y, Para = para++ });
                    y += lineH * 1.25f;
                    continue;
                }
                int keyWords = 0;
                if (KeyHighlight)
                {
                    int colon = line.IndexOf(": ", StringComparison.Ordinal);
                    if (colon > 0 && colon <= 46) keyWords = line.Substring(0, colon + 1).Split(' ').Length;
                }
                string[] words = line.Split(' ');
                float segX = 0;                 // начало текущего отрезка
                int segKind = keyWords > 0 ? 1 : 0;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < words.Length; i++)
                {
                    int kind = i < keyWords ? 1 : 0;
                    if (kind != segKind && sb.Length > 0)
                    {
                        // смена начертания внутри строки: закрываем отрезок и продолжаем сразу за ним
                        Font sf = segKind == 1 ? key : body;
                        string done = sb.ToString();
                        segs.Add(new Seg { S = done, F = sf, Kind = segKind, X = segX, Y = y, Para = para });
                        segX += Gfx.TextWidth(done + " н", sf) - Gfx.TextWidth("н", sf);
                        sb.Length = 0;
                    }
                    segKind = kind;
                    Font f = kind == 1 ? key : body;
                    string candidate = sb.Length > 0 ? sb + " " + words[i] : words[i];
                    if (segX + Gfx.TextWidth(candidate, f) > w && (sb.Length > 0 || segX > 0))
                    {
                        if (sb.Length > 0) segs.Add(new Seg { S = sb.ToString(), F = f, Kind = kind, X = segX, Y = y, Para = para });
                        sb.Length = 0;
                        segX = 0;
                        y += lineH;
                        candidate = words[i];
                    }
                    sb.Length = 0;
                    sb.Append(candidate);
                }
                if (sb.Length > 0) segs.Add(new Seg { S = sb.ToString(), F = segKind == 1 ? key : body, Kind = segKind, X = segX, Y = y, Para = para });
                y += lineH * 1.42f;
                para++;
            }
            paraCount = Math.Max(1, para);
            contentHeight = y;
            if (AutoHeight)
            {
                int h = (int)Math.Ceiling(y) + 2;
                if (Height != h) Height = h;
            }
            ClampScroll();
        }

        private float MaxScroll { get { return Math.Max(0, contentHeight - Height + 4); } }

        private void ClampScroll()
        {
            if (scroll.Target > MaxScroll) scroll.Set(MaxScroll);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            scroll.To(Gfx.Clamp(scroll.Target - Math.Sign(e.Delta) * 64, 0, MaxScroll), 260, Ease.OutCubic);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (MaxScroll > 0 && e.X >= Width - 12)
            {
                dragBar = true;
                dragY = e.Y;
                dragScroll = scroll.Value;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragBar) return;
            float k = contentHeight / Math.Max(1, Height);
            scroll.Set(Gfx.Clamp(dragScroll + (e.Y - dragY) * k, 0, MaxScroll));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragBar = false;
        }

        protected override void Draw(Graphics g)
        {
            Relayout();
            float off = -scroll.Value;
            foreach (Seg s in segs)
            {
                float yy = s.Y + off;
                if (yy > Height || yy < -40) continue;
                float local = Gfx.Clamp(enter * (1 + paraCount * 0.18f) - s.Para * 0.18f, 0, 1);
                if (local <= 0.01f) continue;
                float dx = (1 - local) * 14;
                if (s.Kind == 2)
                {
                    using (LinearGradientBrush b = Gfx.TriBrush(new RectangleF(0, yy, Math.Max(60, Gfx.Measure(s.S, s.F).Width), 20)))
                        Gfx.Text(g, s.S, s.F, b, new RectangleF(dx - 2, yy, Width, 24), StringAlignment.Near, StringAlignment.Near);
                }
                else
                {
                    Color c = Gfx.A(s.Kind == 1 ? Tones.Of(Tone.Accent) : P.TextDim, local);
                    if (s.Kind == 0 && P.Dark) c = Gfx.A(Gfx.Mix(P.TextDim, P.Text, 0.35f), local);
                    Gfx.Text(g, s.S, s.F, c, new RectangleF(s.X + dx - Gfx.TextPad(s.F) / 2, yy, Width + 200, 26), StringAlignment.Near, StringAlignment.Near);
                }
            }
            if (MaxScroll > 0)
            {
                float vh = Height;
                float bh = Math.Max(24, vh * vh / contentHeight);
                float by = (vh - bh) * (scroll.Value / MaxScroll);
                Gfx.FillRound(g, Gfx.A(P.TextMute, 110), new RectangleF(Width - 5, by, 4, bh), 2);
            }
        }
    }
}
