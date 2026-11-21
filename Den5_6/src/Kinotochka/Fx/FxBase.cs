using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Базовый класс всех рисованных элементов: двойная буферизация и фон «из-под» элемента.</summary>
    public class FxControl : Control, IBackdropChild
    {
        public FxControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected static Palette P { get { return Theme.P; } }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            PaintBase(g, e.ClipRectangle);
            Gfx.Hq(g);
            try { Draw(g); }
            catch (Exception ex) { Debug.WriteLine(GetType().Name + ".Draw: " + ex); }
        }

        protected virtual void PaintBase(Graphics g, Rectangle clip)
        {
            Gfx.PaintParentBg(this, g, ClientRectangle);
        }

        protected virtual void Draw(Graphics g) { }

        public virtual void BackdropTick()
        {
            if (IsHandleCreated && Visible) Invalidate();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Animator.Cancel(this);
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Прозрачная панель-контейнер. На «живом» фоне рисует его только в промежутках между
    /// дочерними элементами, внутри карточки — закрашивается цветом карточки.
    /// </summary>
    public class FxPanel : Panel, IBackdropChild
    {
        public FxPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            PaintGaps(this, e.Graphics, e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e) { }

        public virtual void BackdropTick()
        {
            TickChildren(this);
        }

        internal static void TickChildren(Control host)
        {
            if (!host.IsHandleCreated || !host.Visible) return;
            host.Invalidate(false);
            foreach (Control c in host.Controls)
            {
                if (!c.Visible) continue;
                var b = c as IBackdropChild;
                if (b != null) b.BackdropTick();
            }
        }

        internal static void PaintGaps(Control host, Graphics g, Rectangle clip)
        {
            if (!Gfx.IsOnBackdrop(host) && !(host is Form))
            {
                Gfx.PaintParentBg(host, g, clip);
                return;
            }
            using (var region = new Region(clip))
            {
                foreach (Control c in host.Controls)
                    if (c.Visible) region.Exclude(c.Bounds);
                RectangleF[] scans;
                using (var m = new Matrix()) scans = region.GetRegionScans(m);
                if (scans.Length > 48)
                {
                    Backdrop.Paint(g, host, clip);
                    return;
                }
                foreach (RectangleF s in scans)
                    Backdrop.Paint(g, host, Rectangle.Round(s));
            }
        }
    }

    /// <summary>Линейная раскладка (вертикальная или горизонтальная) с фиксированными и «резиновыми» элементами.</summary>
    public class FxBox : FxPanel
    {
        private sealed class Item
        {
            public Control C;
            public int Fixed;       // > 0 — размер в пикселях; 0 — брать текущий размер элемента
            public float Weight;    // > 0 — доля свободного места
            public bool KeepCross;  // не растягивать поперёк
        }

        private readonly List<Item> items = new List<Item>();
        private bool arranging;

        public bool Vertical = true;
        public int Gap = 12;

        public static FxBox V(int gap = 12) { return new FxBox { Vertical = true, Gap = gap }; }
        public static FxBox H(int gap = 12) { return new FxBox { Vertical = false, Gap = gap }; }

        /// <summary>Элемент фиксированного размера (0 — размер самого элемента, может меняться).</summary>
        public FxBox Add(Control c, int size = 0, bool keepCross = false)
        {
            // вложенная раскладка с заданной высотой (поле формы, строка полей) — фиксированного размера
            if (size == 0 && c is FxBox && (Vertical ? c.Height : c.Width) > 0) size = Vertical ? c.Height : c.Width;
            items.Add(new Item { C = c, Fixed = size, KeepCross = keepCross });
            Controls.Add(c);
            return this;
        }

        /// <summary>Элемент, занимающий долю свободного места.</summary>
        public FxBox Fill(Control c, float weight = 1f)
        {
            items.Add(new Item { C = c, Weight = weight });
            Controls.Add(c);
            return this;
        }

        /// <summary>Убрать все элементы раскладки.</summary>
        public void Clear()
        {
            items.Clear();
            Controls.Clear();
        }

        public FxBox Pad(int all)
        {
            Padding = new Padding(all);
            return this;
        }

        public FxBox Pad(int left, int top, int right, int bottom)
        {
            Padding = new Padding(left, top, right, bottom);
            return this;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Arrange();
        }

        /// <summary>Скрытые элементы и элементы, «схлопнувшиеся» до нулевого размера, места не занимают.</summary>
        private bool Skip(Item it)
        {
            if (!it.C.Visible) return true;
            return it.Weight <= 0 && it.Fixed <= 0 && (Vertical ? it.C.Height : it.C.Width) <= 0;
        }

        public void Arrange()
        {
            if (arranging) return;
            arranging = true;
            try
            {
                Rectangle r = ClientRectangle;
                r = new Rectangle(r.X + Padding.Left, r.Y + Padding.Top,
                    Math.Max(0, r.Width - Padding.Horizontal), Math.Max(0, r.Height - Padding.Vertical));
                int main = Vertical ? r.Height : r.Width;
                int count = 0, fixedSum = 0;
                float weights = 0;
                foreach (Item it in items)
                {
                    if (Skip(it)) continue;
                    count++;
                    if (it.Weight > 0) weights += it.Weight;
                    else fixedSum += it.Fixed > 0 ? it.Fixed : (Vertical ? it.C.Height : it.C.Width);
                }
                if (count == 0) return;
                float free = Math.Max(0, main - fixedSum - Gap * (count - 1));
                float pos = Vertical ? r.Y : r.X;
                foreach (Item it in items)
                {
                    if (Skip(it)) continue;
                    float size = it.Weight > 0 ? free * it.Weight / weights
                               : it.Fixed > 0 ? it.Fixed : (Vertical ? it.C.Height : it.C.Width);
                    int a = (int)Math.Round(pos), b = (int)Math.Round(pos + size);
                    Rectangle nb;
                    if (Vertical)
                        nb = it.KeepCross ? new Rectangle(r.X, a, Math.Min(it.C.Width, r.Width), b - a) : new Rectangle(r.X, a, r.Width, b - a);
                    else
                        nb = it.KeepCross ? new Rectangle(a, r.Y + (r.Height - it.C.Height) / 2, b - a, it.C.Height) : new Rectangle(a, r.Y, b - a, r.Height);
                    if (it.C.Bounds != nb) it.C.Bounds = nb;
                    pos += size + Gap;
                }
            }
            finally
            {
                arranging = false;
            }
        }
    }

    /// <summary>
    /// Карточка со скруглёнными углами. Рамка подсвечивается рядом с курсором («прожектор»).
    /// </summary>
    public class FxCard : FxControl, ISurface
    {
        private static readonly List<FxCard> live = new List<FxCard>();
        private static Point lastCursor = new Point(-9999, -9999);
        private static bool hooked;

        private string title = "";
        private string icon;
        private Point spot = new Point(-9999, -9999);
        private bool spotOn;

        public int Radius = 14;
        public bool Spotlight = true;
        public bool Raised;          // более светлая поверхность (плитки)
        public Control HeaderRight;  // элемент в правой части заголовка

        public FxCard()
        {
            Padding = new Padding(14);
            live.Add(this);
            if (!hooked)
            {
                hooked = true;
                Animator.Frame += TrackCursor;
            }
        }

        public FxCard(string title, string icon = null) : this()
        {
            this.title = title ?? "";
            this.icon = icon;
        }

        public string Title
        {
            get { return title; }
            set { title = value ?? ""; Invalidate(); PerformLayout(); }
        }

        public string Icon
        {
            get { return icon; }
            set { icon = value; Invalidate(); }
        }

        public virtual Color SurfaceColor { get { return Raised ? P.Surface2 : P.Surface; } }

        protected int HeaderHeight { get { return title.Length > 0 ? 30 : 0; } }

        public override Rectangle DisplayRectangle
        {
            get
            {
                Rectangle r = ClientRectangle;
                return new Rectangle(r.X + Padding.Left, r.Y + Padding.Top + HeaderHeight,
                    Math.Max(0, r.Width - Padding.Horizontal), Math.Max(0, r.Height - Padding.Vertical - HeaderHeight));
            }
        }

        /// <summary>Помещает элемент во всю карточку.</summary>
        public FxCard With(Control content)
        {
            content.Dock = DockStyle.Fill;
            Controls.Add(content);
            return this;
        }

        /// <summary>Помещает элемент в правую часть заголовка.</summary>
        public FxCard WithHeader(Control c)
        {
            HeaderRight = c;
            Controls.Add(c);
            PlaceHeader();
            return this;
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            PlaceHeader();
        }

        private void PlaceHeader()
        {
            if (HeaderRight == null) return;
            HeaderRight.Dock = DockStyle.None;
            HeaderRight.Location = new Point(Width - Padding.Right - HeaderRight.Width, Padding.Top - 4);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) live.Remove(this);
            base.Dispose(disposing);
        }

        private static void TrackCursor(float dt)
        {
            Point cur = Cursor.Position;
            if (cur == lastCursor) return;
            lastCursor = cur;
            foreach (FxCard c in live)
            {
                if (!c.Spotlight || !c.IsHandleCreated || !c.Visible) continue;
                Point local;
                try { local = c.PointToClient(cur); }
                catch (Exception) { continue; }
                var zone = new Rectangle(-60, -60, c.Width + 120, c.Height + 120);
                bool on = zone.Contains(local);
                if (on || c.spotOn)
                {
                    c.spot = local;
                    c.spotOn = on;
                    c.InvalidateBorder();
                }
            }
        }

        private void InvalidateBorder()
        {
            int t = 3, w = Width, h = Height;
            Invalidate(new Rectangle(0, 0, w, Radius + t));
            Invalidate(new Rectangle(0, h - Radius - t, w, Radius + t));
            Invalidate(new Rectangle(0, 0, t, h));
            Invalidate(new Rectangle(w - t, 0, t, h));
        }

        public override void BackdropTick()
        {
            if (!IsHandleCreated || !Visible) return;
            int r = Radius + 1, w = Width, h = Height;
            Invalidate(new Rectangle(0, 0, r, r));
            Invalidate(new Rectangle(w - r, 0, r, r));
            Invalidate(new Rectangle(0, h - r, r, r));
            Invalidate(new Rectangle(w - r, h - r, r, r));
        }

        protected override void PaintBase(Graphics g, Rectangle clip)
        {
            int r = Radius + 1, w = Width, h = Height;
            Rectangle[] corners =
            {
                new Rectangle(0, 0, r, r), new Rectangle(w - r, 0, r, r),
                new Rectangle(0, h - r, r, r), new Rectangle(w - r, h - r, r, r)
            };
            foreach (Rectangle c in corners)
                if (c.IntersectsWith(clip)) Gfx.PaintParentBg(this, g, c);
        }

        protected override void Draw(Graphics g)
        {
            var r = new RectangleF(0, 0, Width - 1, Height - 1);
            Gfx.FillRound(g, SurfaceColor, r, Radius);
            Gfx.DrawRound(g, P.Border, r, Radius);

            if (Spotlight && spotOn)
            {
                // «прожектор»: участки рамки тем ярче, чем ближе они к курсору
                const float reach = 200f;
                using (GraphicsPath rp = Gfx.Round(r, Radius))
                {
                    rp.Flatten(null, 0.3f);
                    PointF[] pts = rp.PathPoints;
                    {
                        for (int i = 0; i < pts.Length; i++)
                        {
                            PointF a = pts[i], b = pts[(i + 1) % pts.Length];
                            float len = Math.Abs(b.X - a.X) + Math.Abs(b.Y - a.Y);
                            int parts = Math.Max(1, (int)(len / 14f));
                            for (int k = 0; k < parts; k++)
                            {
                                float t0 = k / (float)parts, t1 = (k + 1) / (float)parts;
                                var p0 = new PointF(a.X + (b.X - a.X) * t0, a.Y + (b.Y - a.Y) * t0);
                                var p1 = new PointF(a.X + (b.X - a.X) * t1, a.Y + (b.Y - a.Y) * t1);
                                float dx = (p0.X + p1.X) / 2 - spot.X, dy = (p0.Y + p1.Y) / 2 - spot.Y;
                                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                                if (d >= reach) continue;
                                float k2 = 1 - d / reach;
                                using (var pen = new Pen(Gfx.A(Gfx.Mix(P.Accent, P.Accent2, k2), (int)(235 * k2 * k2)), 1.5f))
                                    g.DrawLine(pen, p0, p1);
                            }
                        }
                    }
                }
            }

            if (title.Length > 0)
            {
                float x = Padding.Left;
                float y = Padding.Top - 3;
                if (icon != null)
                {
                    Icons.Draw(g, icon, new RectangleF(x, y + 2, 16, 16), P.Accent2, 1.6f);
                    x += 23;
                }
                else
                {
                    using (LinearGradientBrush ab = Gfx.AccentBrush(new RectangleF(x, y + 3, 4, 15), 90f))
                        Gfx.FillRound(g, ab, new RectangleF(x, y + 3, 4, 14), 2);
                    x += 12;
                }
                float right = HeaderRight != null ? HeaderRight.Left - 8 : Width - Padding.Right;
                Gfx.Text(g, title, Fonts.Semi(9.5f), P.Text, new RectangleF(x, y, right - x, 20));
            }
            DrawContent(g);
        }

        protected virtual void DrawContent(Graphics g) { }
    }

    /// <summary>Запуск анимаций появления у всех видимых элементов страницы.</summary>
    public static class EnterAnim
    {
        public static void Prepare(Control root)
        {
            Visit(root, delegate(IEnterAnim a) { a.PrepareEnter(); });
        }

        public static void Play(Control root, int baseDelay = 0, int step = 55)
        {
            int i = 0;
            Visit(root, delegate(IEnterAnim a) { a.PlayEnter(baseDelay + i * step); i++; });
        }

        private static void Visit(Control c, Action<IEnterAnim> action)
        {
            if (!c.Visible) return;
            var a = c as IEnterAnim;
            if (a != null) action(a);
            foreach (Control child in c.Controls) Visit(child, action);
        }
    }

    /// <summary>
    /// Контейнер страниц с плавным переходом: старая страница растворяется и уезжает,
    /// новая проявляется и подъезжает на её место.
    /// </summary>
    public class FxPager : FxPanel
    {
        private sealed class Overlay : Control
        {
            public Bitmap Old, New;
            public float T;
            public int Dir = 1;
            public bool Vertical;

            public Overlay()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaintBackground(PaintEventArgs e) { }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                Gfx.PaintParentBg(this, g, ClientRectangle);
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                const float shift = 56f;
                float e1 = Ease.InCubic(Gfx.Clamp(T * 1.7f, 0, 1));
                float e2 = Ease.OutCubic(Gfx.Clamp((T - 0.12f) / 0.88f, 0, 1));
                float o1 = -Dir * shift * e1, o2 = Dir * shift * (1 - e2);
                if (Old != null)
                    Gfx.DrawImageAlpha(g, Old, Vertical ? 0 : o1, Vertical ? o1 : 0, 1 - e1);
                if (New != null)
                    Gfx.DrawImageAlpha(g, New, Vertical ? 0 : o2, Vertical ? o2 : 0, e2);
            }
        }

        private readonly List<Control> pages = new List<Control>();
        private readonly Overlay overlay = new Overlay { Visible = false };
        private Control current;
        private int index = -1;

        public bool VerticalSlide = true;
        public int Duration = 420;

        /// <summary>Страница показана (после завершения перехода).</summary>
        public event Action<int> PageShown;

        public FxPager()
        {
            Controls.Add(overlay);
        }

        public int Index { get { return index; } }
        public int Count { get { return pages.Count; } }
        public Control Current { get { return current; } }

        public void AddPage(Control page)
        {
            page.Visible = false;
            pages.Add(page);
            Controls.Add(page);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (current != null && current.Bounds != ClientRectangle) current.Bounds = ClientRectangle;
        }

        public void ShowPage(int newIndex, bool animate = true)
        {
            if (newIndex < 0 || newIndex >= pages.Count || newIndex == index) return;
            FinishTransition();
            Control next = pages[newIndex];
            int dir = newIndex > index ? 1 : -1;
            bool canAnimate = animate && current != null && IsHandleCreated && Visible && Width > 4 && Height > 4 && !Animator.Instant;

            if (!canAnimate)
            {
                if (current != null) current.Visible = false;
                next.Bounds = ClientRectangle;
                next.Visible = true;
                current = next;
                index = newIndex;
                EnterAnim.Prepare(next);
                EnterAnim.Play(next, 60);
                Raise();
                return;
            }

            overlay.Old = Gfx.Snapshot(this);
            overlay.New = null;
            overlay.T = 0;
            overlay.Dir = dir;
            overlay.Vertical = VerticalSlide;
            overlay.Bounds = ClientRectangle;
            overlay.Visible = true;
            overlay.BringToFront();
            overlay.Update();

            current.Visible = false;
            next.Bounds = ClientRectangle;
            next.Visible = true;
            overlay.BringToFront();
            current = next;
            index = newIndex;
            EnterAnim.Prepare(next);

            // Даём новой странице разложиться и отрисоваться, затем снимаем её изображение.
            Animator.After(this, "snap", 15, delegate
            {
                if (IsDisposed) return;
                overlay.New = Gfx.Snapshot(next);
                Animator.Run(this, "tr", 0, 1, Duration, Ease.Linear, delegate(float v)
                {
                    overlay.T = v;
                    overlay.Invalidate();
                }, delegate
                {
                    FinishTransition();
                    EnterAnim.Play(next);
                    Raise();
                });
            });
        }

        private void FinishTransition()
        {
            bool wasSnap = Animator.IsRunning(this, "snap"), wasTr = Animator.IsRunning(this, "tr");
            Animator.Cancel(this, "snap");
            Animator.Cancel(this, "tr");
            if (overlay.Visible)
            {
                overlay.Visible = false;
                if (overlay.Old != null) { overlay.Old.Dispose(); overlay.Old = null; }
                if (overlay.New != null) { overlay.New.Dispose(); overlay.New = null; }
                if ((wasSnap || wasTr) && current != null) EnterAnim.Play(current);
            }
        }

        private void Raise()
        {
            Action<int> h = PageShown;
            if (h != null) h(index);
        }
    }

    /// <summary>
    /// Накладка «круговое раскрытие»: показывает снимок прежнего состояния окна и открывает
    /// новое расширяющимся кругом. Используется при смене темы и после заставки.
    /// </summary>
    public sealed class RevealOverlay : Control
    {
        private Bitmap image;
        private Action<Graphics, Rectangle> painter;
        private Point center;
        private float radius;

        public RevealOverlay()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Visible = false;
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.P.Bg);
            if (image != null) e.Graphics.DrawImageUnscaled(image, 0, 0);
            else if (painter != null) painter(e.Graphics, ClientRectangle);
            if (radius > 1)
            {
                // светящееся кольцо по границе раскрытия
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Gfx.A(Theme.P.Accent2, 190), 3f))
                    e.Graphics.DrawEllipse(pen, center.X - radius - 2, center.Y - radius - 2, radius * 2 + 4, radius * 2 + 4);
            }
        }

        /// <summary>Запускает раскрытие из точки <paramref name="from"/> (координаты родителя).</summary>
        public void Play(Control host, Bitmap snapshot, Point from, int ms, Action done)
        {
            if (Parent != host) host.Controls.Add(this);
            if (image != null) image.Dispose();
            image = snapshot;
            painter = null;
            center = from;
            radius = 0;
            Bounds = host.ClientRectangle;
            Region = null;
            Visible = true;
            BringToFront();
            Update();
            float max = (float)Math.Sqrt(Width * (double)Width + Height * (double)Height) + 8;
            Animator.Run(this, "reveal", 0, max, ms, Ease.InOutCubic, delegate(float v)
            {
                radius = v;
                // Круглое «окно» набирается из горизонтальных полос: так область строится быстро и одинаково на любой системе.
                var region = new Region(new Rectangle(0, 0, Width, Height));
                const int band = 3;
                int yFrom = Math.Max(0, (int)(center.Y - v)), yTo = Math.Min(Height, (int)(center.Y + v) + 1);
                for (int y = yFrom; y < yTo; y += band)
                {
                    float dy = y + band / 2f - center.Y;
                    float half = v * v - dy * dy;
                    if (half <= 0) continue;
                    half = (float)Math.Sqrt(half);
                    int x0 = Math.Max(0, (int)(center.X - half)), x1 = Math.Min(Width, (int)(center.X + half) + 1);
                    if (x1 > x0) region.Exclude(new Rectangle(x0, y, x1 - x0, band));
                }
                Region old = Region;
                Region = region;
                if (old != null) old.Dispose();
                Invalidate();
            }, delegate
            {
                Visible = false;
                Region = null;
                if (image != null) { image.Dispose(); image = null; }
                if (done != null) done();
            });
        }
    }
}
