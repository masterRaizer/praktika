using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Раздел приложения: пункт боковой панели и его страница.</summary>
    public sealed class NavItem
    {
        public string Icon, Title, Hint, Header, Sub;
        public Control Page;
    }

    /// <summary>
    /// Окно без системной рамки: «живой» фон, стеклянная боковая панель навигации, собственный заголовок
    /// с кнопками окна, заставка при запуске, смена темы круговым раскрытием, всплывающие уведомления.
    /// </summary>
    public class FxWindow : Form
    {
        private readonly List<NavItem> items = new List<NavItem>();
        private readonly Sidebar sidebar;
        private readonly HeaderBar header;
        private readonly FxPager pager = new FxPager();
        private readonly RevealOverlay reveal = new RevealOverlay();
        private readonly List<ToastView> toasts = new List<ToastView>();
        private Splash splash;
        private static bool wheelHooked;

        public string AppTitle = "Приложение";
        public string AppCaption = "";
        public string AppIcon = "pulse";
        public string SplashTitle;
        public string SplashSubtitle = "";
        public bool ShowSplash = true;
        public const int SidebarWidth = 236, HeaderHeight = 62, Frame = 6;

        /// <summary>Раздел показан (после завершения перехода).</summary>
        public event Action<int> PageShown;

        public FxWindow()
        {
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.P.Bg;
            Font = Fonts.Body(9.5f);
            Padding = new Padding(Frame);

            sidebar = new Sidebar(this);
            header = new HeaderBar(this);
            pager.VerticalSlide = true;
            pager.PageShown += delegate(int i)
            {
                Action<int> h = PageShown;
                if (h != null) h(i);
            };
            Controls.Add(pager);
            Controls.Add(header);
            Controls.Add(sidebar);
            Controls.Add(reveal);

            Animator.Start();
            Backdrop.Updated += OnBackdrop;
            Theme.Changed += OnThemeChanged;
            if (!wheelHooked)
            {
                wheelHooked = true;
                try { Application.AddMessageFilter(new WheelRouter()); }
                catch (Exception) { }
            }
        }

        public FxPager Pager { get { return pager; } }
        public int PageIndex { get { return pager.Index; } }
        public IList<NavItem> Items { get { return items; } }

        // ---------- разделы ----------

        public NavItem AddPage(string icon, string title, string hint, string headerTitle, string headerSub, Control page)
        {
            var it = new NavItem { Icon = icon, Title = title, Hint = hint, Header = headerTitle, Sub = headerSub, Page = page };
            items.Add(it);
            pager.AddPage(page);
            sidebar.Invalidate();
            return it;
        }

        public void Navigate(int index, bool animate = true)
        {
            if (index < 0 || index >= items.Count || index == pager.Index) return;
            sidebar.Select(index, animate);
            header.SetTitle(items[index].Header, items[index].Sub, animate);
            pager.ShowPage(index, animate);
        }

        // ---------- системные мелочи ----------

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000;        // WS_MINIMIZEBOX — сворачивание щелчком по панели задач
                cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW — тень окна
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(this);
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
            {
                base.WndProc(ref m);
                long lp = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));
                int g = Frame + 2, w = ClientSize.Width, h = ClientSize.Height;
                bool l = p.X < g, r = p.X >= w - g, t = p.Y < g, b = p.Y >= h - g;
                int hit = t && l ? 13 : t && r ? 14 : b && l ? 16 : b && r ? 17 : l ? 10 : r ? 11 : t ? 12 : b ? 15 : 0;
                if (hit != 0) m.Result = (IntPtr)hit;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (sidebar == null || header == null) return;   // раскладка может сработать ещё в конструкторе
            int f = WindowState == FormWindowState.Maximized ? 0 : Frame;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 50 || h < 50) return;
            sidebar.SetBounds(f, f, SidebarWidth, h - f * 2);
            int cx = f + SidebarWidth + 12;
            header.SetBounds(cx, f, w - cx - f, HeaderHeight);
            pager.SetBounds(cx, f + HeaderHeight, w - cx - f - 6, h - f * 2 - HeaderHeight - 6);
            if (splash != null) splash.Bounds = ClientRectangle;
            PlaceToasts();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FxPanel.PaintGaps(this, e.Graphics, e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (WindowState != FormWindowState.Normal) return;
            using (var pen = new Pen(Gfx.A(Theme.P.BorderHi, 150)))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        private void OnBackdrop()
        {
            if (IsDisposed || !IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            FxPanel.TickChildren(this);
            if (splash != null && splash.Visible) splash.Invalidate();
        }

        private void OnThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            BackColor = Theme.P.Bg;
            ApplyThemeTree(this);
            Invalidate(true);
        }

        private static void ApplyThemeTree(Control c)
        {
            var t = c as IThemed;
            if (t != null) t.ApplyTheme();
            foreach (Control child in c.Controls) ApplyThemeTree(child);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Backdrop.Updated -= OnBackdrop;
                Theme.Changed -= OnThemeChanged;
            }
            base.Dispose(disposing);
        }

        // ---------- запуск: проявление окна и заставка ----------

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Animator.Instant) return;
            try
            {
                Opacity = 0;
                Animator.Run(this, "fadeIn", 0, 1, 320, Ease.OutCubic, delegate(float v) { Opacity = v; });
                // страховка: окно в любом случае становится непрозрачным
                Animator.After(this, "fadeGuard", 700, delegate { if (!IsDisposed && Opacity < 1) Opacity = 1; });
            }
            catch (Exception) { }
            if (ShowSplash)
            {
                splash = new Splash(this) { Bounds = ClientRectangle };
                Controls.Add(splash);
                splash.BringToFront();
                splash.Start(delegate
                {
                    Bitmap snap = Gfx.Snapshot(splash);
                    Control page = pager.Current;
                    if (page != null) EnterAnim.Prepare(page);
                    reveal.Play(this, snap, new Point(ClientSize.Width / 2, ClientSize.Height / 2), 760, delegate
                    {
                        if (page != null) EnterAnim.Play(page);
                    });
                    Controls.Remove(splash);
                    splash.Dispose();
                    splash = null;
                });
            }
        }

        /// <summary>Плавное закрытие окна.</summary>
        public void CloseAnimated()
        {
            if (Animator.Instant) { Close(); return; }
            Animator.Run(this, "fadeOut", 1, 0, 170, Ease.InCubic, delegate(float v)
            {
                try { Opacity = v; } catch (Exception) { }
            }, Close);
        }

        public void ToggleMaximize()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                WindowState = FormWindowState.Normal;
            }
            else
            {
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = FormWindowState.Maximized;
            }
            PerformLayout();
            Invalidate(true);
        }

        // ---------- тема ----------

        /// <summary>Смена темы с круговым раскрытием из указанной точки окна.</summary>
        public void ToggleTheme(Point origin)
        {
            if (Animator.Instant || !IsHandleCreated)
            {
                Theme.Set(!Theme.IsDark);
                return;
            }
            Bitmap snap = Gfx.Snapshot(this);
            reveal.Play(this, snap, origin, 620, null);
            Theme.Set(!Theme.IsDark);
            reveal.BringToFront();
        }

        // ---------- уведомления ----------

        /// <summary>Всплывающее уведомление в правом верхнем углу (замена MessageBox).</summary>
        public void Toast(string title, string text, Tone tone = Tone.Accent2, int ms = 5200)
        {
            if (IsDisposed) return;
            while (toasts.Count >= 3) toasts[0].Dismiss(true);
            var t = new ToastView(this, title, text, tone, ms);
            toasts.Add(t);
            Controls.Add(t);
            t.BringToFront();
            PlaceToasts();
            t.Appear();
        }

        internal void ToastClosed(ToastView t)
        {
            toasts.Remove(t);
            Controls.Remove(t);
            t.Dispose();
            PlaceToasts();
        }

        private void PlaceToasts()
        {
            int y = HeaderHeight + 4;
            foreach (ToastView t in toasts)
            {
                t.Home = new Point(ClientSize.Width - t.Width - 22, y);
                t.Reposition();
                y += t.Height + 10;
            }
        }

        // =================================================================
        //  Боковая панель
        // =================================================================
        private sealed class Sidebar : FxControl
        {
            private readonly FxWindow win;
            private readonly AnimValue pill, themeA, bgA;
            private int selected, hoverIndex = -1, hoverToggle = -1;
            private float[] hov = new float[0];
            private const int Top0 = 96, ItemH = 48;

            public Sidebar(FxWindow win)
            {
                this.win = win;
                pill = new AnimValue(this);
                themeA = new AnimValue(this, Theme.IsDark ? 1 : 0);
                bgA = new AnimValue(this, 1);
                Animator.Frame += OnFrame;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) Animator.Frame -= OnFrame;
                base.Dispose(disposing);
            }

            public void Select(int index, bool animate)
            {
                selected = index;
                if (animate) pill.To(index, 420, Ease.OutBack);
                else pill.Set(index);
            }

            private void OnFrame(float dt)
            {
                int n = win.items.Count;
                if (hov.Length != n) hov = new float[n];
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    float target = i == hoverIndex ? 1 : 0;
                    if (Math.Abs(hov[i] - target) < 0.005f) continue;
                    hov[i] += (target - hov[i]) * Math.Min(1, dt * 12);
                    changed = true;
                }
                if (changed && IsHandleCreated) Invalidate();
            }

            private Rectangle ToggleRect(int k)
            {
                return new Rectangle(12, Height - 88 + k * 36, Width - 24, 32);
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int i = e.Y >= Top0 && e.X > 8 && e.X < Width - 8 ? (e.Y - Top0) / ItemH : -1;
                if (i >= win.items.Count) i = -1;
                int t = ToggleRect(0).Contains(e.Location) ? 0 : ToggleRect(1).Contains(e.Location) ? 1 : -1;
                if (i != hoverIndex || t != hoverToggle)
                {
                    hoverIndex = i;
                    hoverToggle = t;
                    Invalidate();
                }
                Cursor = i >= 0 || t >= 0 ? Cursors.Hand : Cursors.Default;
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hoverIndex = hoverToggle = -1;
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                if (hoverIndex >= 0) { win.Navigate(hoverIndex); return; }
                if (hoverToggle == 0)
                {
                    Rectangle r = ToggleRect(0);
                    themeA.To(Theme.IsDark ? 0f : 1f, 300, Ease.OutBack);
                    win.ToggleTheme(win.PointToClient(PointToScreen(new Point(r.Right - 24, r.Top + r.Height / 2))));
                    return;
                }
                if (hoverToggle == 1)
                {
                    Backdrop.Animated = !Backdrop.Animated;
                    bgA.To(Backdrop.Animated ? 1f : 0f, 300, Ease.OutBack);
                    return;
                }
                if (e.Y < Top0 - 10) win.BeginDrag(this, e);
            }

            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); win.EndDrag(); }

            protected override void OnMouseDoubleClick(MouseEventArgs e)
            {
                base.OnMouseDoubleClick(e);
                if (e.Y < Top0 - 10) win.ToggleMaximize();
            }

            protected override void Draw(Graphics g)
            {
                var r = new RectangleF(0, 0, Width - 1, Height - 1);
                Gfx.FillRound(g, Gfx.A(P.Surface, P.Dark ? 168 : 190), r, 16);
                Gfx.DrawRound(g, Gfx.A(P.BorderHi, 150), r, 16);

                // логотип
                float breath = 0.5f + 0.5f * (float)Math.Sin(Animator.Now * 1.6);
                var mark = new RectangleF(18, 22, 38, 38);
                Gfx.Glow(g, mark, 11, P.Accent, 5, 1.6f, (int)(26 + 34 * breath));
                using (LinearGradientBrush b = Gfx.TriBrush(RectangleF.Inflate(mark, 1, 1), 45f))
                    Gfx.FillRound(g, b, mark, 11);
                Icons.Draw(g, win.AppIcon, RectangleF.Inflate(mark, -8, -8), Color.White, 2f);
                Gfx.Text(g, win.AppTitle, Fonts.Display(10.5f), P.Text, new RectangleF(66, 20, Width - 72, 22));
                Gfx.Text(g, win.AppCaption, Fonts.Body(8f), P.TextMute, new RectangleF(67, 41, Width - 72, 18));
                using (var pen = new Pen(Gfx.A(P.Border, 200)))
                    g.DrawLine(pen, 18, 78, Width - 19, 78);

                // «таблетка» выбранного раздела
                int n = win.items.Count;
                if (n > 0)
                {
                    var pr = new RectangleF(10, Top0 + pill.Value * ItemH + 2, Width - 21, ItemH - 4);
                    using (var b = new LinearGradientBrush(RectangleF.Inflate(pr, 1, 1), Gfx.A(P.Accent, P.Dark ? 92 : 70), Gfx.A(P.Accent2, P.Dark ? 30 : 34), 0f))
                        Gfx.FillRound(g, b, pr, 12);
                    Gfx.DrawRound(g, Gfx.A(P.Accent, 150), pr, 12);
                    using (LinearGradientBrush b = Gfx.AccentBrush(new RectangleF(pr.X, pr.Y + 9, 4, pr.Height - 18), 90f))
                        Gfx.FillRound(g, b, new RectangleF(pr.X + 1, pr.Y + 10, 3.5f, pr.Height - 20), 2);
                }

                for (int i = 0; i < n; i++)
                {
                    NavItem it = win.items[i];
                    float y = Top0 + i * ItemH;
                    float near = 1 - Math.Min(1, Math.Abs(pill.Value - i));
                    float h = i < hov.Length ? hov[i] : 0;
                    if (h > 0.01f && near < 0.99f)
                        Gfx.FillRound(g, Gfx.A(P.SurfaceHi, (int)(150 * h * (1 - near))), new RectangleF(10, y + 2, Width - 21, ItemH - 4), 12);
                    Color ic = Gfx.Mix(Gfx.Mix(P.TextMute, P.TextDim, h), P.Dark ? Color.White : P.Accent, near);
                    Icons.Draw(g, it.Icon, new RectangleF(26 + 3 * h * (1 - near), y + 14, 20, 20), ic, 1.7f);
                    Gfx.Text(g, it.Title, Fonts.Semi(9.5f), Gfx.Mix(Gfx.Mix(P.TextDim, P.Text, h), P.Text, near),
                        new RectangleF(58, y + 6, Width - 70, 20));
                    Gfx.Text(g, it.Hint, Fonts.Body(8f), P.TextMute, new RectangleF(58, y + 24, Width - 70, 16));
                }

                DrawToggle(g, 0, Theme.IsDark ? "moon" : "sun", "Тёмная тема", themeA.Value);
                DrawToggle(g, 1, "spark", "Живой фон", bgA.Value);
            }

            private void DrawToggle(Graphics g, int k, string icon, string text, float s)
            {
                Rectangle r = ToggleRect(k);
                if (hoverToggle == k) Gfx.FillRound(g, Gfx.A(P.SurfaceHi, 130), r, 10);
                Icons.Draw(g, icon, new RectangleF(r.X + 12, r.Y + 8, 16, 16), P.TextDim, 1.6f);
                Gfx.Text(g, text, Fonts.Medium(9f), P.TextDim, new RectangleF(r.X + 38, r.Y, r.Width - 90, r.Height));
                float c = Gfx.Clamp(s, 0, 1);
                var sw = new RectangleF(r.Right - 48, r.Y + 6, 38, 20);
                using (var b = new LinearGradientBrush(RectangleF.Inflate(sw, 1, 1), Gfx.Mix(P.Surface2, P.Accent, c), Gfx.Mix(P.SurfaceHi, P.Accent2, c), 0f))
                    Gfx.FillRound(g, b, sw, 10);
                Gfx.DrawRound(g, Gfx.Mix(P.BorderHi, P.Accent2, c * 0.5f), sw, 10);
                using (var b = new SolidBrush(Color.White))
                    g.FillEllipse(b, sw.X + 3 + s * 18, sw.Y + 3, 14, 14);
            }
        }

        // =================================================================
        //  Заголовок окна
        // =================================================================
        private sealed class HeaderBar : FxControl
        {
            private readonly FxWindow win;
            private readonly AnimValue swap;
            private readonly AnimValue[] btn = new AnimValue[3];
            private string title = "", sub = "", oldTitle = "", oldSub = "";
            private int hoverBtn = -1;

            public HeaderBar(FxWindow win)
            {
                this.win = win;
                swap = new AnimValue(this, 1f);
                for (int i = 0; i < 3; i++) btn[i] = new AnimValue(this);
            }

            public void SetTitle(string t, string s, bool animate)
            {
                oldTitle = title;
                oldSub = sub;
                title = t ?? "";
                sub = s ?? "";
                if (animate)
                {
                    swap.Set(0f);
                    swap.To(1f, 520, Ease.OutCubic);
                }
                else swap.Set(1f);
            }

            private RectangleF BtnRect(int i)
            {
                return new RectangleF(Width - 12 - (3 - i) * 44, 12, 38, 30);
            }

            private int BtnAt(Point p)
            {
                for (int i = 0; i < 3; i++) if (BtnRect(i).Contains(p)) return i;
                return -1;
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                win.DragMove();
                int b = BtnAt(e.Location);
                if (b == hoverBtn) return;
                hoverBtn = b;
                for (int i = 0; i < 3; i++) btn[i].To(i == b ? 1f : 0f, i == b ? 130 : 240);
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hoverBtn = -1;
                for (int i = 0; i < 3; i++) btn[i].To(0f, 240);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                int b = BtnAt(e.Location);
                if (b == 0) win.WindowState = FormWindowState.Minimized;
                else if (b == 1) win.ToggleMaximize();
                else if (b == 2) win.CloseAnimated();
                else if (e.Clicks >= 2) win.ToggleMaximize();
                else win.BeginDrag(this, e);
            }

            protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); win.EndDrag(); }

            protected override void Draw(Graphics g)
            {
                float t = swap.Value;
                float right = Width - 150;
                if (t < 0.999f)
                {
                    float a = 1 - Gfx.Clamp(t * 2.2f, 0, 1);
                    Gfx.Text(g, oldTitle, Fonts.Display(13f), Gfx.A(P.Text, a), new RectangleF(4, 6 - t * 22, right, 28));
                    Gfx.Text(g, oldSub, Fonts.Body(9f), Gfx.A(P.TextDim, a), new RectangleF(5, 34 - t * 22, right, 20));
                }
                float e2 = Gfx.Clamp(t * 1.6f - 0.3f, 0, 1);
                float dy = (1 - Ease.OutCubic(e2)) * 20;
                if (e2 > 0.01f)
                {
                    using (var b = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(80, Gfx.Measure(title, Fonts.Display(13f)).Width + 10), 40),
                               Gfx.A(P.Text, e2), Gfx.A(Gfx.Mix(P.Text, P.Accent2, 0.55f), e2), 0f))
                        Gfx.Text(g, title, Fonts.Display(13f), b, new RectangleF(4, 6 + dy, right, 28));
                    Gfx.Text(g, sub, Fonts.Body(9f), Gfx.A(P.TextDim, e2), new RectangleF(5, 34 + dy, right, 20));
                }

                string[] icons = { "min", win.WindowState == FormWindowState.Maximized ? "restore" : "max", "close" };
                for (int i = 0; i < 3; i++)
                {
                    RectangleF r = BtnRect(i);
                    float h = btn[i].Value;
                    Color hot = i == 2 ? P.Bad : P.SurfaceHi;
                    Gfx.FillRound(g, Gfx.A(Gfx.Mix(P.Surface, hot, h), (int)(150 + 105 * h)), r, 9);
                    Gfx.DrawRound(g, Gfx.A(P.BorderHi, 140), r, 9);
                    Icons.Draw(g, icons[i], new RectangleF(r.X + (r.Width - 13) / 2, r.Y + (r.Height - 13) / 2, 13, 13),
                        Gfx.Mix(P.TextDim, i == 2 ? Color.White : P.Text, h), 1.6f);
                }
            }
        }

        // ---------- перетаскивание окна ----------

        private bool manualDrag;
        private Point dragOrigin, dragWindow;

        internal void BeginDrag(Control source, MouseEventArgs e)
        {
            if (WindowState == FormWindowState.Maximized) return;
            if (Native.DragWindow(this)) return;
            manualDrag = true;
            dragOrigin = Cursor.Position;
            dragWindow = Location;
        }

        internal void DragMove()
        {
            if (!manualDrag) return;
            if (Control.MouseButtons != MouseButtons.Left) { manualDrag = false; return; }
            Point c = Cursor.Position;
            Location = new Point(dragWindow.X + c.X - dragOrigin.X, dragWindow.Y + c.Y - dragOrigin.Y);
        }

        internal void EndDrag() { manualDrag = false; }

        // =================================================================
        //  Заставка
        // =================================================================
        private sealed class Splash : Control
        {
            private readonly FxWindow win;
            private float t;
            private Action done;
            private bool finished;

            public Splash(FxWindow win)
            {
                this.win = win;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            public void Start(Action onDone)
            {
                done = onDone;
                Animator.Run(this, "splash", 0, 1, 2100, Ease.Linear, delegate(float v) { t = v; Invalidate(); }, Finish);
            }

            private void Finish()
            {
                if (finished) return;
                finished = true;
                Animator.Cancel(this, "splash");
                t = 1f;
                Refresh();
                if (done != null) done();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Finish();
            }

            protected override void OnPaintBackground(PaintEventArgs e) { }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Palette p = Theme.P;
                Backdrop.Paint(g, this, ClientRectangle);
                Gfx.Hq(g);
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                float cx = Width / 2f, cy = Height / 2f - 30;

                // расходящиеся кольца
                for (int k = 0; k < 3; k++)
                {
                    float ph = (float)((Animator.Now * 0.35 + k / 3.0) % 1.0);
                    float rad = 60 + ph * 240;
                    using (var pen = new Pen(Gfx.A(p.Accent2, (int)(70 * (1 - ph))), 1.5f))
                        g.DrawEllipse(pen, cx - rad, cy - rad, rad * 2, rad * 2);
                }

                // знак
                float m = Ease.OutBack(Gfx.Clamp(t / 0.28f, 0, 1));
                float size = 92 * m;
                if (size > 2)
                {
                    var mark = new RectangleF(cx - size / 2, cy - size / 2, size, size);
                    Gfx.Glow(g, mark, 26 * m, p.Accent, 9, 3f, 60);
                    using (LinearGradientBrush b = Gfx.TriBrush(RectangleF.Inflate(mark, 1, 1), 45f))
                        Gfx.FillRound(g, b, mark, 26 * m);
                    Icons.Draw(g, win.AppIcon, RectangleF.Inflate(mark, -size * 0.24f, -size * 0.24f), Color.White, 2.3f);
                }

                // название по буквам
                string title = win.SplashTitle ?? win.AppTitle;
                Font f = Fonts.Display(24f);
                float total = Gfx.Measure(title, f).Width;
                float x0 = cx - total / 2, ty = cy + 78;
                float tp = Gfx.Clamp((t - 0.2f) / 0.5f, 0, 1);
                using (LinearGradientBrush tb = Gfx.TriBrush(new RectangleF(x0 - 4, ty, total + 8, 50)))
                {
                    for (int i = 0; i < title.Length; i++)
                    {
                        float local = Ease.OutCubic(Gfx.Clamp(tp * (1 + title.Length * 0.06f) - i * 0.06f, 0, 1));
                        if (local <= 0.01f || title[i] == ' ') continue;
                        float px = x0 + Gfx.Measure(title.Substring(0, i) + ".", f).Width - Gfx.Measure(".", f).Width;
                        GraphicsState st = g.Save();
                        g.TranslateTransform(0, (1 - local) * 26);
                        if (local < 0.99f)
                            using (var ab = new SolidBrush(Gfx.A(p.Text, local)))
                                g.DrawString(title[i].ToString(), f, ab, px, ty);
                        else
                            g.DrawString(title[i].ToString(), f, tb, px, ty);
                        g.Restore(st);
                    }
                }

                float sp = Gfx.Clamp((t - 0.55f) / 0.25f, 0, 1);
                Gfx.Text(g, win.SplashSubtitle, Fonts.Medium(11f), Gfx.A(p.TextDim, sp), new RectangleF(0, ty + 52 + (1 - sp) * 10, Width, 26), StringAlignment.Center);

                // полоса загрузки
                float bw = 260, bx = cx - bw / 2, by = ty + 100;
                Gfx.FillRound(g, Gfx.A(p.Surface2, 200), new RectangleF(bx, by, bw, 5), 2.5f);
                float fill = bw * Ease.InOutCubic(Gfx.Clamp(t / 0.96f, 0, 1));
                if (fill > 3)
                    using (LinearGradientBrush b = Gfx.AccentBrush(new RectangleF(bx - 1, by, bw + 2, 5)))
                        Gfx.FillRound(g, b, new RectangleF(bx, by, fill, 5), 2.5f);
                Gfx.Text(g, "щёлкните, чтобы пропустить", Fonts.Body(8.5f), Gfx.A(p.TextMute, sp), new RectangleF(0, Height - 44, Width, 20), StringAlignment.Center);
            }
        }

        // =================================================================
        //  Уведомление
        // =================================================================
        internal sealed class ToastView : Control
        {
            private readonly FxWindow win;
            private readonly string title, text;
            private readonly Tone tone;
            private readonly int life;
            private float slide = 1f, remain = 1f;
            private bool closing;
            public Point Home;

            public ToastView(FxWindow win, string title, string text, Tone tone, int ms)
            {
                this.win = win;
                this.title = title ?? "";
                this.text = text ?? "";
                this.tone = tone;
                life = ms;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
                int w = 380;
                int h = 44 + (int)Math.Ceiling(Gfx.Measure(this.text, Fonts.Body(9f), w - 66).Height);
                Size = new Size(w, Math.Max(62, Math.Min(h, 260)));
                using (GraphicsPath p = Gfx.Round(new RectangleF(0, 0, Width, Height), 13))
                    Region = new Region(p);
                Cursor = Cursors.Hand;
            }

            public void Reposition()
            {
                Location = new Point(Home.X + (int)(slide * (Width + 40)), Home.Y);
            }

            public void Appear()
            {
                Animator.Run(this, "slide", 1, 0, 460, Ease.OutBack, delegate(float v) { slide = v; Reposition(); });
                Animator.Run(this, "life", 1, 0, life, Ease.Linear, delegate(float v) { remain = v; Invalidate(); }, delegate { Dismiss(false); });
            }

            public void Dismiss(bool immediately)
            {
                if (closing) return;
                closing = true;
                Animator.Cancel(this, "life");
                if (immediately || Animator.Instant) { win.ToastClosed(this); return; }
                Animator.Run(this, "slide", slide, 1, 260, Ease.InCubic, delegate(float v) { slide = v; Reposition(); },
                    delegate { win.ToastClosed(this); });
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                Dismiss(false);
            }

            protected override void OnPaintBackground(PaintEventArgs e) { }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Palette p = Theme.P;
                Gfx.Hq(g);
                g.Clear(p.SurfaceHi);
                Color c = Tones.Of(tone);
                var r = new RectangleF(0.5f, 0.5f, Width - 2, Height - 2);
                Gfx.DrawRound(g, Gfx.A(c, 220), r, 13, 1.6f);
                var badge = new RectangleF(14, 14, 30, 30);
                Gfx.FillRound(g, Gfx.A(c, 60), badge, 10);
                Icons.Draw(g, tone == Tone.Bad || tone == Tone.Warn ? "warn" : tone == Tone.Good ? "check" : "info",
                    RectangleF.Inflate(badge, -7, -7), c, 1.8f);
                Gfx.Text(g, title, Fonts.Bold(9.5f), p.Text, new RectangleF(54, 11, Width - 66, 20));
                Gfx.Text(g, text, Fonts.Body(9f), p.TextDim, new RectangleF(54, 31, Width - 66, Height - 40), StringAlignment.Near, StringAlignment.Near, true);
                Gfx.FillRound(g, Gfx.A(c, 200), new RectangleF(14, Height - 6, (Width - 28) * remain, 2.5f), 1.2f);
            }
        }

        /// <summary>Колесо мыши прокручивает элемент под курсором, а не тот, что в фокусе.</summary>
        private sealed class WheelRouter : IMessageFilter
        {
            [DllImport("user32.dll")]
            private static extern IntPtr WindowFromPoint(Point p);

            [DllImport("user32.dll")]
            private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

            private bool broken;

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != 0x20A || broken) return false;
                try
                {
                    IntPtr h = WindowFromPoint(Cursor.Position);
                    if (h == IntPtr.Zero || h == m.HWnd || Control.FromHandle(h) == null) return false;
                    SendMessage(h, m.Msg, m.WParam, m.LParam);
                    return true;
                }
                catch (Exception)
                {
                    broken = true;
                    return false;
                }
            }
        }
    }
}
