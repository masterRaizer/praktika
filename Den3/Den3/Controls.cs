using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    // =====================================================================
    //  Дерево в виде раскрывающегося списка (замена TreeView)
    // =====================================================================

    /// <summary>
    /// Иерархический список узлов дерева решений (собственная замена TreeView): каждый узел — строка
    /// с отступом, значком вида узла, вероятностью и ожидаемым значением; узлы раскрываются и сворачиваются
    /// щелчком, строки оптимального пути выделены цветом.
    /// </summary>
    public class FxTreeList : FxControl
    {
        private sealed class Row
        {
            public Node N;
            public int Depth;
            public float Appear = 1f;
        }

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        private readonly List<Row> rows = new List<Row>();
        private readonly HashSet<string> collapsed = new HashSet<string>();
        private readonly AnimValue scroll;
        private Node root;
        private int hoverRow = -1;
        private bool dragBar;
        private int dragY;
        private float dragScroll;
        private const int RowH = 26;

        public FxTreeList()
        {
            scroll = new AnimValue(this);
        }

        public void SetTree(Node tree, bool resetExpansion = false)
        {
            bool first = root == null || resetExpansion;
            root = tree;
            if (first && tree != null)
            {
                collapsed.Clear();
                // большие деревья: сразу раскрыт только оптимальный путь
                if (TreeSolver.CountLeaves(tree) > 24)
                    foreach (Node n in tree.All())
                        if (n.Children.Count > 0 && !n.Optimal) collapsed.Add(n.Path);
            }
            Rebuild(null);
        }

        public void ExpandAll() { collapsed.Clear(); Rebuild(null); }

        public void CollapseToOptimal()
        {
            collapsed.Clear();
            if (root != null)
                foreach (Node n in root.All())
                    if (n.Children.Count > 0 && !n.Optimal) collapsed.Add(n.Path);
            Rebuild(null);
        }

        private void Rebuild(string animateUnder)
        {
            var oldPaths = new HashSet<string>(rows.Select(r => r.N.Path));
            rows.Clear();
            if (root != null) Walk(root, 0);
            if (animateUnder != null)
                foreach (Row r in rows)
                    if (!oldPaths.Contains(r.N.Path))
                    {
                        Row row = r;
                        row.Appear = 0;
                        Animator.Run(row, "in", 0, 1, 260, Ease.OutCubic, delegate(float v) { row.Appear = v; Invalidate(); });
                    }
            if (scroll.Target > MaxScroll) scroll.Set(MaxScroll);
            Invalidate();
        }

        private void Walk(Node n, int depth)
        {
            rows.Add(new Row { N = n, Depth = depth });
            if (collapsed.Contains(n.Path)) return;
            foreach (Node c in n.Children) Walk(c, depth + 1);
        }

        private float MaxScroll { get { return Math.Max(0, rows.Count * RowH + 8 - Height); } }

        private int RowAt(int y)
        {
            int i = (int)((y + scroll.Value - 4) / RowH);
            return i >= 0 && i < rows.Count ? i : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragBar)
            {
                float k = (rows.Count * RowH + 8f) / Math.Max(1, Height);
                scroll.Set(Gfx.Clamp(dragScroll + (e.Y - dragY) * k, 0, MaxScroll));
                return;
            }
            int r = RowAt(e.Y);
            if (r != hoverRow) { hoverRow = r; Invalidate(); }
            Cursor = r >= 0 && rows[r].N.Children.Count > 0 ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverRow = -1;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (MaxScroll > 0 && e.X >= Width - 10)
            {
                dragBar = true;
                dragY = e.Y;
                dragScroll = scroll.Value;
                return;
            }
            int r = RowAt(e.Y);
            if (r < 0 || rows[r].N.Children.Count == 0) return;
            string path = rows[r].N.Path;
            if (!collapsed.Remove(path)) collapsed.Add(path);
            Rebuild(path);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragBar = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            scroll.To(Gfx.Clamp(scroll.Target - Math.Sign(e.Delta) * RowH * 3, 0, MaxScroll), 240, Ease.OutCubic);
        }

        private static string Money(double v)
        {
            return (Math.Abs(v) < 0.005 ? "0" : v.ToString("0.##", Ru)).Replace("-", "−");
        }

        protected override void Draw(Graphics g)
        {
            if (rows.Count == 0)
            {
                Gfx.Text(g, "дерево не построено", Fonts.Body(9.5f), P.TextMute, new RectangleF(0, 0, Width, Height), StringAlignment.Center);
                return;
            }
            GraphicsState st = g.Save();
            g.SetClip(new Rectangle(0, 0, Width, Height));
            float y0 = 4 - scroll.Value;
            Font fName = Fonts.Medium(9f), fBold = Fonts.Bold(9f), fMono = Fonts.Mono(8.5f), fMonoB = Fonts.MonoBold(8.5f);
            float colEv = Width - 96, colP = Width - 176;
            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];
                float y = y0 + i * RowH;
                if (y + RowH < 0 || y > Height) continue;
                float a = r.Appear;
                float x = 8 + r.Depth * 18 - (1 - a) * 10;
                Node n = r.N;
                bool opt = n.Optimal;
                var rr = new RectangleF(2, y + 1, Width - 14, RowH - 2);
                if (opt) Gfx.FillRound(g, Gfx.A(P.Accent2, (int)((P.Dark ? 26 : 22) * a)), rr, 6);
                if (i == hoverRow) Gfx.FillRound(g, Gfx.A(P.SurfaceHi, (int)(200 * a)), rr, 6);
                if (opt) Gfx.FillRound(g, Gfx.A(P.Accent2, (int)(220 * a)), new RectangleF(2, y + 6, 3, RowH - 12), 1.5f);

                // направляющие уровней
                using (var pen = new Pen(Gfx.A(P.Border, a)))
                    for (int d = 0; d < r.Depth; d++) g.DrawLine(pen, 14 + d * 18, y, 14 + d * 18, y + RowH);

                // стрелка раскрытия
                if (n.Children.Count > 0)
                {
                    bool closed = collapsed.Contains(n.Path);
                    var cr = new RectangleF(x, y + 7, 12, 12);
                    PointF[] tri = closed
                        ? new[] { new PointF(cr.X + 3, cr.Y + 1), new PointF(cr.X + 10, cr.Y + 6), new PointF(cr.X + 3, cr.Y + 11) }
                        : new[] { new PointF(cr.X + 1, cr.Y + 3), new PointF(cr.X + 11, cr.Y + 3), new PointF(cr.X + 6, cr.Y + 10) };
                    using (var b = new SolidBrush(Gfx.A(i == hoverRow ? P.Text : P.TextMute, a))) g.FillPolygon(b, tri);
                }
                // значок вида узла
                float gx = x + 16, gy = y + RowH / 2f;
                Color gc = opt ? P.Accent2 : P.TextMute;
                using (var pen = new Pen(Gfx.A(gc, a), 1.5f))
                {
                    if (n.Kind == NodeKind.Decision) g.DrawRectangle(pen, gx, gy - 5, 10, 10);
                    else if (n.Kind == NodeKind.Chance) g.DrawEllipse(pen, gx, gy - 5, 10, 10);
                    else g.DrawPolygon(pen, new[] { new PointF(gx, gy), new PointF(gx + 10, gy - 5), new PointF(gx + 10, gy + 5) });
                }
                string text = n.Parent == null ? n.Name : n.Branch;
                if (n.Kind != NodeKind.Outcome && n.Parent != null) text += "  ·  " + n.Name;
                if (Math.Abs(n.Flow) > 1e-9) text += "   [" + Money(n.Flow) + "]";
                Gfx.Text(g, text, opt ? fBold : fName, Gfx.A(opt ? P.Text : P.TextDim, a), new RectangleF(gx + 16, y, colP - gx - 20, RowH));
                if (!double.IsNaN(n.Probability))
                    Gfx.Text(g, "p " + n.Probability.ToString("0.###", Ru), fMono, Gfx.A(P.TextMute, a), new RectangleF(colP, y, 76, RowH), StringAlignment.Far);
                Gfx.Text(g, (n.Kind == NodeKind.Outcome ? "" : "EV ") + Money(n.Value), opt ? fMonoB : fMono,
                    Gfx.A(opt ? P.Accent2 : P.TextDim, a), new RectangleF(colEv, y, 84, RowH), StringAlignment.Far);
            }
            g.Restore(st);
            if (MaxScroll > 0)
            {
                float total = rows.Count * RowH + 8f;
                float bh = Math.Max(22, Height * Height / total);
                float by = (Height - bh) * (scroll.Value / MaxScroll);
                Gfx.FillRound(g, Gfx.A(P.TextMute, dragBar ? 200 : 110), new RectangleF(Width - 6, by, 4, bh), 2);
            }
        }
    }

    // =====================================================================
    //  Сообщения об ошибках ввода (ErrorProvider)
    // =====================================================================

    /// <summary>
    /// Ошибки ввода показываются стандартным компонентом ErrorProvider: значок у поля, сообщение — во всплывающей
    /// подсказке. Значок нарисован в стиле программы; у числовых полей дополнительно подсвечивается рамка.
    /// </summary>
    public sealed class ErrorMarks : IDisposable
    {
        private readonly ErrorProvider provider = new ErrorProvider();
        private readonly Dictionary<Control, string> errors = new Dictionary<Control, string>();
        private static Icon icon;

        public ErrorMarks()
        {
            provider.BlinkStyle = ErrorBlinkStyle.NeverBlink;
            try { provider.Icon = Badge(); } catch (Exception) { /* стандартный значок */ }
        }

        private static Icon Badge()
        {
            if (icon != null) return icon;
            using (var bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var b = new SolidBrush(Theme.Hex("F0596B"))) g.FillEllipse(b, 0.5f, 0.5f, 15, 15);
                    using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLine(pen, 8, 4, 8, 9);
                    g.FillEllipse(Brushes.White, 6.9f, 10.8f, 2.3f, 2.3f);
                }
                icon = Icon.FromHandle(bmp.GetHicon());
            }
            return icon;
        }

        /// <summary>Установить (message ≠ null) или снять ошибку у элемента.</summary>
        public void Set(Control c, string message)
        {
            if (c == null) return;
            provider.SetIconAlignment(c, ErrorIconAlignment.TopRight);
            provider.SetIconPadding(c, -9);
            provider.SetError(c, message ?? "");
            var num = c as FxNumber;
            if (num != null) num.HasError = message != null;
            if (message == null) errors.Remove(c); else errors[c] = message;
        }

        public void Clear(IEnumerable<Control> list) { foreach (Control c in list.ToList()) Set(c, null); }

        public void ClearAll() { Clear(errors.Keys); }

        public int Count { get { return errors.Count; } }

        public string First { get { return errors.Values.FirstOrDefault(); } }

        public void Dispose() { provider.Dispose(); }
    }

    // =====================================================================
    //  Общие элементы страниц
    // =====================================================================

    public static class Ui
    {
        public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static FxLabel Caption(string text)
        {
            return new FxLabel(text, Fonts.Semi(8f), Tone.Mute) { Height = 16 };
        }

        /// <summary>Поле с подписью сверху.</summary>
        public static FxBox Field(string caption, Control input, int height = 34)
        {
            FxBox b = FxBox.V(2);
            b.Add(Caption(caption), 16).Add(input, height);
            b.Height = 18 + height;
            return b;
        }

        /// <summary>Подпись слева, поле справа.</summary>
        public static FxBox Row(string caption, Control input, int inputWidth = 116)
        {
            FxBox b = FxBox.H(8);
            b.Fill(new FxLabel(caption, Fonts.Medium(9f), Tone.Dim));
            b.Add(input, inputWidth, true);
            b.Height = 34;
            return b;
        }

        public static FxNumber Money(double value, double min = -100000, double max = 100000)
        {
            return new FxNumber((decimal)min, (decimal)max, (decimal)value, 1, 5) { Size = new Size(116, 32) };
        }

        public static FxNumber Prob(double value)
        {
            return new FxNumber(0, 1, (decimal)value, 2, 0.01m) { Size = new Size(116, 32) };
        }

        public static FxLabel Status()
        {
            return new FxLabel("", Fonts.Semi(9f), Tone.Good, true) { Icon = "check" };
        }

        public static void SetStatus(FxLabel l, string error, string ok = "Данные проверены: ошибок нет.")
        {
            l.Tone = error == null ? Tone.Good : Tone.Bad;
            l.Icon = error == null ? "check" : "warn";
            l.Text = error ?? ok;
        }

        public static FxTable Table(params string[] headers)
        {
            var t = new FxTable();
            t.SetColumns(headers);
            return t;
        }

        public static FxCard Card(string title, string icon, Control content, Control header = null)
        {
            var c = new FxCard(title, icon).With(content);
            if (header != null) c.WithHeader(header);
            return c;
        }

        /// <summary>Карточка с вкладками: переключатель в заголовке и плавная смена содержимого.</summary>
        public static FxCard TabCard(string title, string icon, int segWidth, string[] tabs, Control[] pages, out FxSegmented seg)
        {
            var card = new FxCard(title, icon);
            var pager = new FxPager { VerticalSlide = false, Duration = 320 };
            foreach (Control p in pages) pager.AddPage(p);
            var s = new FxSegmented(tabs) { Size = new Size(segWidth, 28) };
            s.SelectedIndexChanged += delegate { pager.ShowPage(s.SelectedIndex); };
            card.With(pager);
            card.WithHeader(s);
            pager.ShowPage(0, false);
            seg = s;
            return card;
        }

        public static FxBox Tiles(params FxStat[] stats)
        {
            FxBox b = FxBox.H(10);
            foreach (FxStat s in stats) b.Fill(s);
            b.Height = 74;
            return b;
        }

        public static string N(double v, string fmt = "0.##")
        {
            if (double.IsNaN(v)) return "—";
            string s = v.ToString(fmt, Ru);
            if (s == "-0") s = "0";
            return s.Replace("-", "−");
        }

        public static string P(double v) { return N(v, "0.####"); }

        /// <summary>Разбор денежного значения из ячейки таблицы.</summary>
        public static double ParseNumber(string text, string what)
        {
            string s = (text ?? "").Trim().Replace(" ", "").Replace("−", "-").Replace(".", ",");
            double v;
            if (s.Length == 0) throw new DecisionException(what + ": значение не задано.");
            if (!double.TryParse(s, NumberStyles.Float, Ru, out v) || double.IsNaN(v) || double.IsInfinity(v))
                throw new DecisionException(what + ": «" + text + "» — не число.");
            return v;
        }

        public static double ParseProbability(string text, string what)
        {
            double v = ParseNumber(text, what);
            if (v < 0 || v > 1) throw new DecisionException(what + ": вероятность " + N(v, "0.###") + " вне отрезка [0; 1].");
            return v;
        }

        public static void SaveDialog<T>(FxWindow win, string title, string fileName, T data)
        {
            using (var dlg = new SaveFileDialog { Filter = "Данные XML (*.xml)|*.xml", Title = title, FileName = fileName })
            {
                if (dlg.ShowDialog(win) != DialogResult.OK) return;
                try
                {
                    XmlStore.Save(dlg.FileName, data);
                    win.Toast("Данные сохранены", System.IO.Path.GetFileName(dlg.FileName), Tone.Good, 3000);
                }
                catch (DecisionException ex) { win.Toast("Сохранение", ex.Message, Tone.Bad); }
            }
        }

        /// <summary>Открыть файл XML; возвращает данные или default, имя файла — в name.</summary>
        public static T OpenDialog<T>(FxWindow win, string title, out string name) where T : class
        {
            name = null;
            using (var dlg = new OpenFileDialog { Filter = "Данные XML (*.xml)|*.xml|Все файлы|*.*", Title = title })
            {
                if (dlg.ShowDialog(win) != DialogResult.OK) return null;
                try
                {
                    T data = XmlStore.Load<T>(dlg.FileName);
                    name = System.IO.Path.GetFileName(dlg.FileName);
                    return data;
                }
                catch (DecisionException ex)
                {
                    win.Toast("Загрузка данных", ex.Message, Tone.Bad);
                    return null;
                }
            }
        }

        public static void SavePng(FxWindow win, TreeCanvas canvas, string fileName)
        {
            using (var dlg = new SaveFileDialog { Filter = "Рисунок PNG (*.png)|*.png", Title = "Сохранить рисунок дерева", FileName = fileName })
            {
                if (dlg.ShowDialog(win) != DialogResult.OK) return;
                try
                {
                    canvas.SavePng(dlg.FileName);
                    win.Toast("Рисунок сохранён", System.IO.Path.GetFileName(dlg.FileName), Tone.Good, 3000);
                }
                catch (Exception ex) { win.Toast("Сохранение рисунка", ex.Message, Tone.Bad); }
            }
        }
    }

    /// <summary>
    /// Панель источника данных: номер варианта, случайные данные, файл; всегда показывает, откуда взяты данные.
    /// </summary>
    public class SourceBar : FxCard
    {
        public readonly FxSegmented Variant;
        public readonly FxButton BtnRandom, BtnOpen, BtnSave;
        private readonly FxLabel source;
        public event Action<int> VariantChosen;
        private bool silent;

        public SourceBar(int defaultVariant)
        {
            Padding = new Padding(14, 10, 14, 10);
            Variant = new FxSegmented(VariantData.Available.Select(v => v == 0 ? "0 · демо" : v + " · мой").ToArray()) { Size = new Size(170, 30) };
            Variant.SelectedIndex = Array.IndexOf(VariantData.Available, defaultVariant);
            Variant.SelectedIndexChanged += delegate
            {
                Action<int> h = VariantChosen;
                if (!silent && h != null && Variant.SelectedIndex >= 0) h(VariantData.Available[Variant.SelectedIndex]);
            };
            BtnRandom = new FxButton("Случайные данные", false, "dice").AutoWidth();
            BtnOpen = new FxButton("Открыть…", false, "folder").AutoWidth();
            BtnSave = new FxButton("Сохранить…", false, "save").AutoWidth();
            source = new FxLabel("", Fonts.Semi(9f), Tone.Accent2) { Icon = "data" };

            FxBox row = FxBox.H(10);
            row.Add(new FxLabel("Вариант", Fonts.Semi(9.5f)) { Width = 62 }, 62);
            row.Add(Variant, 170, true);
            row.Add(BtnRandom, BtnRandom.Width, true);
            row.Add(BtnOpen, BtnOpen.Width, true);
            row.Add(BtnSave, BtnSave.Width, true);
            row.Fill(source);
            With(row);
        }

        public string Source
        {
            get { return source.Text; }
            set { source.Text = "Источник данных: " + value; }
        }

        /// <summary>Отметить выбранный вариант без повторной загрузки.</summary>
        public void ShowVariant(int number)
        {
            int i = Array.IndexOf(VariantData.Available, number);
            if (i < 0 || Variant.SelectedIndex == i) return;
            silent = true;
            try { Variant.SelectedIndex = i; }
            finally { silent = false; }
        }
    }
}
