using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>
    /// Рисунок дерева решений средствами GDI+ (событие Paint): квадрат — решение, круг — случайное событие,
    /// треугольник — исход. На ветвях подписаны вероятности и денежные потоки, у узлов — ожидаемые значения,
    /// оптимальный путь выделен цветом, отвергнутые ветви узлов решения перечёркнуты.
    /// Рисунок вписывается в элемент и масштабируется вместе с окном; колесо мыши — увеличение,
    /// перетаскивание — сдвиг, двойной щелчок — вернуть исходный масштаб.
    /// </summary>
    public class TreeCanvas : FxControl
    {
        private sealed class Item
        {
            public Node N;
            public Item Parent;
            public readonly List<Item> Kids = new List<Item>();
            public int Col;
            public float Row;
            public bool Folded;     // у узла есть скрытые потомки
            public PointF Pos;      // центр узла на экране
        }

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private Node root;
        private Item top;
        private readonly List<Item> items = new List<Item>();
        private int rows, maxCol, hiddenLeaves;
        private string signature = "";
        private bool collapse;
        private float reveal = 1f;
        private float zoom = 1f, panX, panY;
        private bool dragging;
        private Point dragStart;
        private float dragPanX, dragPanY;
        private Item hover;

        public string EmptyText = "дерево не построено";
        public string Unit = "";
        /// <summary>Показывать накопленный итог у исходов.</summary>
        public bool ShowTotals = true;
        /// <summary>Число исходов, при котором неоптимальные ветви сворачиваются автоматически.</summary>
        public int AutoCollapseLeaves = 30;

        public TreeCanvas()
        {
            Cursor = Cursors.Hand;
        }

        /// <summary>Сворачивать отвергнутые ветви узлов решения (показывается только их значение).</summary>
        public bool CollapseRejected
        {
            get { return collapse; }
            set { collapse = value; Relayout(); Invalidate(); }
        }

        public Node Tree { get { return root; } }

        /// <summary>Показать новое дерево; при изменении структуры дерево «прорастает» от корня.</summary>
        public void SetTree(Node tree)
        {
            root = tree;
            string sig = tree == null ? "" : string.Join(";", tree.All().Select(n => n.Path));
            bool changed = sig != signature;
            signature = sig;
            Relayout();
            if (changed && tree != null && IsHandleCreated && Visible && !Animator.Instant)
                Animator.Run(this, "reveal", 0, 1, 900, Ease.OutCubic, delegate(float t) { reveal = t; Invalidate(); });
            else
            {
                if (!Animator.IsRunning(this, "reveal")) reveal = 1f;
                Invalidate();
            }
        }

        public void ResetView()
        {
            zoom = 1f;
            panX = panY = 0;
            Invalidate();
        }

        public int VisibleLeaves { get { return items.Count(i => i.Kids.Count == 0); } }

        // ---------- раскладка ----------

        private void Relayout()
        {
            items.Clear();
            top = null;
            rows = 0;
            maxCol = 0;
            hiddenLeaves = 0;
            if (root == null) return;
            top = Build(root, null, 0);
        }

        private Item Build(Node n, Item parent, int col)
        {
            var it = new Item { N = n, Parent = parent, Col = col };
            items.Add(it);
            maxCol = Math.Max(maxCol, col);
            bool fold = collapse && n.Kind != NodeKind.Outcome && parent != null &&
                        parent.N.Kind == NodeKind.Decision && !n.Optimal && n.Children.Count > 0;
            if (fold)
            {
                it.Folded = true;
                hiddenLeaves += TreeSolver.CountLeaves(n);
            }
            if (!fold)
                foreach (Node c in n.Children) it.Kids.Add(Build(c, it, col + 1));
            if (it.Kids.Count == 0) it.Row = rows++;
            else it.Row = (it.Kids[0].Row + it.Kids[it.Kids.Count - 1].Row) / 2f;
            return it;
        }

        // ---------- геометрия ----------

        private const float PadTop = 60, PadBottom = 26, PadLeft = 40;

        private float RightPad(float fs) { return ShowTotals ? 118 + fs * 4 : 70 + fs * 3; }

        private void Place(float width, float height, float z, float px, float py, out float colW, out float rowH)
        {
            float usableW = Math.Max(40, width - PadLeft - RightPad(8.5f));
            float usableH = Math.Max(20, height - PadTop - PadBottom);
            float colW0 = usableW / Math.Max(1, maxCol);
            float rowH0 = rows > 1 ? usableH / (rows - 1) : usableH;
            float cx = width / 2f, cy = (PadTop + height - PadBottom) / 2f;
            foreach (Item it in items)
            {
                float bx = PadLeft + 10 + it.Col * colW0;
                float by = rows > 1 ? PadTop + it.Row * rowH0 : cy;
                // масштаб относительно центра рисунка и сдвиг
                it.Pos = new PointF(cx + (bx - cx) * z + px, cy + (by - cy) * z + py);
            }
            colW = colW0 * z;
            rowH = rowH0 * z;
        }

        private static float NodeSize(float rowH, float colW)
        {
            return Gfx.Clamp(Math.Min(rowH * 0.55f, colW * 0.16f), 7f, 17f);
        }

        // ---------- мышь ----------

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float old = zoom;
            zoom = Gfx.Clamp(zoom * (e.Delta > 0 ? 1.2f : 1 / 1.2f), 1f, 10f);
            if (zoom <= 1.0001f) { panX = panY = 0; }
            else
            {
                // точка под курсором остаётся на месте
                float cx = Width / 2f + panX, cy = (PadTop + Height - PadBottom) / 2f + panY;
                float k = zoom / old;
                panX += (e.X - cx) * (1 - k);
                panY += (e.Y - cy) * (1 - k);
            }
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragStart = e.Location;
            dragPanX = panX;
            dragPanY = panY;
            Cursor = Cursors.SizeAll;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging)
            {
                if (zoom > 1f)
                {
                    panX = dragPanX + e.X - dragStart.X;
                    panY = dragPanY + e.Y - dragStart.Y;
                    Invalidate();
                }
                return;
            }
            Item h = null;
            float best = 14 * 14;
            foreach (Item it in items)
            {
                float dx = it.Pos.X - e.X, dy = it.Pos.Y - e.Y, d = dx * dx + dy * dy;
                if (d < best) { best = d; h = it; }
            }
            if (h != hover) { hover = h; Invalidate(); }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragging = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hover != null) { hover = null; Invalidate(); }
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }

        // ---------- отрисовка ----------

        protected override void Draw(Graphics g)
        {
            if (root == null || top == null)
            {
                Gfx.Text(g, EmptyText, Fonts.Body(9.5f), P.TextMute, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
                return;
            }
            PaintTree(g, Width, Height, zoom, panX, panY, reveal, true);
        }

        private string Money(double v)
        {
            string s = Math.Abs(v) < 0.005 ? "0" : v.ToString("0.##", Ru);
            return s.Replace("-", "−") + Unit;
        }

        private static string Prob(double p) { return "p = " + p.ToString("0.###", Ru); }

        private void PaintTree(Graphics g, int width, int height, float z, float px, float py, float rev, bool interactive)
        {
            float colW, rowH;
            Place(width, height, z, px, py, out colW, out rowH);
            float s = NodeSize(rowH, colW);
            float fs = Gfx.Clamp(Math.Min(rowH * 0.36f, colW * 0.07f), 6.5f, 10f);
            bool roomy = rowH >= 15, labels = rowH >= 11;
            Font fBranch = Fonts.Medium(fs), fSmall = Fonts.Mono(Math.Max(6.3f, fs - 0.8f)), fValue = Fonts.Bold(fs);
            Color good = P.Accent2, dim = P.BorderHi;
            float steps = maxCol + 1;

            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(0, 26, width, height - 26 - 18));

            // ветви
            foreach (Item it in items)
            {
                if (it.Parent == null) continue;
                float a = Gfx.Clamp(rev * steps - (it.Col - 1) - 0.3f, 0, 1);
                if (a <= 0.01f) continue;
                PointF p0 = it.Parent.Pos, p1 = it.Pos;
                bool opt = it.N.Optimal;
                bool rejected = it.Parent.N.Kind == NodeKind.Decision && !opt;
                float x0 = p0.X + s / 2 + 1, x1 = p1.X - s / 2 - 2;
                float knee = p0.X + Math.Min(colW * 0.2f, 34);
                float xe = knee + (x1 - knee) * a;
                Color c = opt ? good : Gfx.A(dim, rejected ? 150 : 255);
                using (var pen = new Pen(Gfx.A(c, a), opt ? 2.4f : 1.3f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLine(pen, x0, p0.Y, knee, p0.Y + (p1.Y - p0.Y) * Math.Min(1, a * 1.6f));
                    if (a > 0.6f / 1.6f) g.DrawLine(pen, knee, p1.Y, xe, p1.Y);
                }
                if (rejected && a > 0.9f)
                    using (var pen = new Pen(Gfx.A(P.Bad, 170), 1.5f))
                    {
                        float mx = x1 - 16;
                        g.DrawLine(pen, mx, p1.Y - 4, mx + 4, p1.Y + 4);
                        g.DrawLine(pen, mx + 4, p1.Y - 4, mx + 8, p1.Y + 4);
                    }
                if (!labels || a < 0.95f) continue;
                float lw = x1 - knee - 4;
                if (lw < 20) continue;
                Color tc = opt ? P.Text : P.TextDim;
                var above = new RectangleF(knee + 3, p1.Y - fs * 2f - 1, lw + 6, fs * 2f);
                Font bf = fBranch;
                for (float size = fs; Gfx.TextWidth(it.N.Branch, bf) > lw + 4 && size > 6.6f; )
                {
                    size -= 0.4f;
                    bf = Fonts.Medium(size);
                }
                Gfx.Text(g, it.N.Branch, bf, tc, above, StringAlignment.Near, StringAlignment.Far);
                var parts = new List<string>();
                if (!double.IsNaN(it.N.Probability)) parts.Add(Prob(it.N.Probability));
                if (Math.Abs(it.N.Flow) > 1e-9) parts.Add(Money(it.N.Flow));
                if (parts.Count > 0 && roomy)
                    Gfx.Text(g, string.Join("   ", parts), fSmall, opt ? Gfx.Mix(P.TextDim, good, 0.45f) : P.TextMute,
                        new RectangleF(knee + 3, p1.Y + 1, lw, fs * 1.9f), StringAlignment.Near, StringAlignment.Near);
            }

            // узлы
            foreach (Item it in items)
            {
                float a = Gfx.Clamp(rev * steps - it.Col, 0, 1);
                if (a <= 0.01f) continue;
                float k = Ease.OutBack(a);
                float ss = s * k;
                PointF p = it.Pos;
                bool opt = it.N.Optimal;
                Color c = opt ? good : P.TextMute;
                var r = new RectangleF(p.X - ss / 2, p.Y - ss / 2, ss, ss);
                if (interactive && it == hover) Gfx.Glow(g, RectangleF.Inflate(r, 2, 2), ss / 2, P.Accent, 4, 1.4f, 60);
                using (var fill = new SolidBrush(Gfx.Mix(P.Surface2, c, opt ? 0.28f : 0.08f)))
                using (var pen = new Pen(Gfx.A(c, a), opt ? 2f : 1.4f))
                {
                    switch (it.N.Kind)
                    {
                        case NodeKind.Decision:
                            g.FillRectangle(fill, r);
                            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                            break;
                        case NodeKind.Chance:
                            g.FillEllipse(fill, r);
                            g.DrawEllipse(pen, r);
                            break;
                        default:
                            PointF[] tri = { new PointF(r.Left, p.Y), new PointF(r.Right, r.Top + 1), new PointF(r.Right, r.Bottom - 1) };
                            g.FillPolygon(fill, tri);
                            g.DrawPolygon(pen, tri);
                            break;
                    }
                }
                if (a < 0.9f) continue;
                if (it.N.Kind == NodeKind.Outcome)
                {
                    string txt = Money(it.N.Value);
                    float tx = p.X + s / 2 + 6;
                    Gfx.Text(g, txt, fValue, opt ? P.Text : P.TextDim, new RectangleF(tx, p.Y - fs, 90, fs * 2), StringAlignment.Near);
                    if (ShowTotals && roomy)
                    {
                        double total = it.N.Accumulated + it.N.Value;
                        float w0 = Gfx.TextWidth(txt, fValue);
                        Gfx.Text(g, "итог " + Money(total), fSmall, P.TextMute, new RectangleF(tx + w0 + 6, p.Y - fs, 110, fs * 2), StringAlignment.Near);
                    }
                }
                else
                {
                    string txt = Money(it.N.Value);
                    float w = Gfx.TextWidth(txt, fValue) + 10;
                    var tr = new RectangleF(p.X - w / 2, p.Y - s / 2 - fs * 2.1f - 1, w, fs * 2f);
                    if (it.Parent == null) tr.X = Math.Max(2, tr.X);
                    if (tr.X < 2) tr.X = 2;
                    if (roomy || it.Parent == null || opt)
                    {
                        if (opt) Gfx.FillRound(g, Gfx.A(good, P.Dark ? 40 : 30), tr, 5);
                        Gfx.Text(g, txt, fValue, opt ? P.Text : P.TextDim, tr, StringAlignment.Center);
                    }
                    if (it.Folded)
                    {
                        int leaves = TreeSolver.CountLeaves(it.N);
                        Gfx.Text(g, "… свёрнуто: " + leaves + " исх.", fSmall, P.TextMute,
                            new RectangleF(p.X + s / 2 + 6, p.Y - fs, 140, fs * 2), StringAlignment.Near);
                    }
                    else if (it.Parent == null || rowH > 24)
                    {
                        var nr = new RectangleF(p.X - 60, p.Y + s / 2 + 1, 120, fs * 1.8f);
                        if (nr.X < 2) nr.X = 2;
                        Gfx.Text(g, it.N.Name, fSmall, P.TextMute, nr, nr.X <= 2 ? StringAlignment.Near : StringAlignment.Center, StringAlignment.Near);
                    }
                }
            }
            g.Restore(st);

            DrawLegend(g, width, height);

            if (interactive && hover != null) DrawTip(g, hover);
        }

        private void DrawLegend(Graphics g, int width, int height)
        {
            Font f = Fonts.Medium(8f);
            float x = 4, y = 6;
            Color c = P.TextDim;
            using (var pen = new Pen(c, 1.4f))
            {
                g.DrawRectangle(pen, x, y + 3, 10, 10);
                Gfx.Text(g, "решение", f, c, new RectangleF(x + 15, y, 70, 16)); x += 80;
                g.DrawEllipse(pen, x, y + 3, 10, 10);
                Gfx.Text(g, "случайное событие", f, c, new RectangleF(x + 15, y, 130, 16)); x += 135;
                g.DrawPolygon(pen, new[] { new PointF(x, y + 8), new PointF(x + 10, y + 3), new PointF(x + 10, y + 13) });
                Gfx.Text(g, "исход", f, c, new RectangleF(x + 15, y, 60, 16)); x += 64;
            }
            using (var pen = new Pen(P.Accent2, 2.4f))
                g.DrawLine(pen, x, y + 8, x + 18, y + 8);
            Gfx.Text(g, "оптимальный путь", f, c, new RectangleF(x + 23, y, 120, 16)); x += 140;
            using (var pen = new Pen(Gfx.A(P.Bad, 170), 1.5f))
            {
                g.DrawLine(pen, x, y + 4, x + 4, y + 12);
                g.DrawLine(pen, x + 4, y + 4, x + 8, y + 12);
            }
            Gfx.Text(g, "отвергнутая ветвь", f, c, new RectangleF(x + 13, y, 130, 16));

            int total = TreeSolver.CountLeaves(root);
            string info = "исходов: " + total + (hiddenLeaves > 0 ? " (свёрнуто " + hiddenLeaves + ")" : "") +
                          (zoom > 1.001f ? "   ·   масштаб ×" + zoom.ToString("0.0", Ru) + ", двойной щелчок — сброс" : "   ·   колесо мыши — увеличение");
            Gfx.Text(g, info, Fonts.Mono(7.5f), P.TextMute, new RectangleF(4, height - 18, width - 8, 16), StringAlignment.Far);
        }

        private void DrawTip(Graphics g, Item it)
        {
            Node n = it.N;
            var lines = new List<string>();
            string kind = n.Kind == NodeKind.Decision ? "Решение" : n.Kind == NodeKind.Chance ? "Случайное событие" : "Исход";
            lines.Add(kind + (n.Name.Length > 0 && n.Kind != NodeKind.Outcome ? ": " + n.Name : ""));
            if (n.Branch.Length > 0) lines.Add("Ветвь: " + n.Branch);
            if (!double.IsNaN(n.Probability)) lines.Add("Вероятность: " + n.Probability.ToString("0.####", Ru));
            if (Math.Abs(n.Flow) > 1e-9) lines.Add("Поток на ветви: " + Money(n.Flow));
            lines.Add((n.Kind == NodeKind.Outcome ? "Результат: " : "Ожидаемое значение: ") + Money(n.Value));
            if (n.Kind == NodeKind.Decision && n.Best >= 0) lines.Add("Выбор: " + n.Children[n.Best].Branch);
            lines.Add(n.Optimal ? "На оптимальном пути" : "Вне оптимального пути");
            Font f = Fonts.Medium(8.5f);
            float w = lines.Max(l => Gfx.TextWidth(l, f)) + 20, h = lines.Count * 17 + 10;
            float x = it.Pos.X + 14, y = it.Pos.Y + 12;
            if (x + w > Width - 4) x = it.Pos.X - w - 14;
            if (y + h > Height - 4) y = it.Pos.Y - h - 12;
            var r = new RectangleF(Math.Max(4, x), Math.Max(4, y), w, h);
            Gfx.Glow(g, r, 9, Color.Black, 4, 2f, 18);
            Gfx.FillRound(g, P.SurfaceHi, r, 9);
            Gfx.DrawRound(g, P.BorderHi, r, 9);
            for (int i = 0; i < lines.Count; i++)
                Gfx.Text(g, lines[i], i == 0 ? Fonts.Bold(8.5f) : f, i == 0 ? P.Text : P.TextDim, new RectangleF(r.X + 10, r.Y + 5 + i * 17, w - 14, 17));
        }

        /// <summary>Изображение дерева в натуральную величину (для файла PNG).</summary>
        public Bitmap Render()
        {
            if (root == null) throw new DecisionException("Дерево не построено.");
            int w = Math.Max(1200, (maxCol + 1) * 210 + 260);
            int h = Math.Max(560, rows * 36 + 80);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(P.Surface);
                Gfx.Hq(g);
                Item keep = hover;
                hover = null;
                PaintTree(g, w, h, 1f, 0, 0, 1f, false);
                hover = keep;
            }
            return bmp;
        }

        public void SavePng(string path)
        {
            using (Bitmap b = Render()) b.Save(path, ImageFormat.Png);
        }
    }
}
