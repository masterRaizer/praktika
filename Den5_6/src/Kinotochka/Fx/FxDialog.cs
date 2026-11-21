using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>
    /// Отдельное окно-форма в стиле FxKit (без системной рамки): заголовок со значком, кнопка закрытия,
    /// перетаскивание за заголовок, «живой» фон, область содержимого и нижняя панель кнопок.
    /// На его основе сделаны формы входа, регистрации и редактирования записей.
    /// </summary>
    public class FxDialog : Form
    {
        public const int HeaderH = 74, FooterH = 64, Frame = 6;

        private readonly AnimValue closeHover;
        private readonly HeaderView header;
        private readonly FxLabel message;
        private bool manualDrag;
        private Point dragOrigin, dragWindow;

        /// <summary>Содержимое формы (вертикальная раскладка).</summary>
        public readonly FxBox Body = FxBox.V(10);
        /// <summary>Кнопки внизу формы (горизонтальная раскладка, выравнивание вправо).</summary>
        public readonly FxBox Footer = FxBox.H(10);

        public string Title = "";
        public string Subtitle = "";
        public string IconName = "info";

        public FxDialog()
        {
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.P.Bg;
            Font = Fonts.Body(9.5f);
            Size = new Size(520, 420);

            closeHover = new AnimValue(this);
            header = new HeaderView(this);
            message = new FxLabel("", Fonts.Medium(9f), Tone.Bad, true) { Icon = "warn", Visible = false };

            var card = new DialogCard();
            var inner = FxBox.V(10);
            inner.Add(message, 0);
            inner.Fill(Body);
            card.With(inner);
            content = card;

            Controls.Add(card);
            Controls.Add(Footer);
            Controls.Add(header);

            Animator.Start();
            Backdrop.Updated += OnBackdrop;
            Theme.Changed += OnThemeChanged;
        }

        private readonly Control content;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ClassStyle |= 0x20000;   // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.RoundCorners(this);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (header == null) return;
            int w = ClientSize.Width, h = ClientSize.Height;
            header.SetBounds(Frame, Frame, w - Frame * 2, HeaderH);
            Footer.SetBounds(Frame + 14, h - FooterH - Frame + 12, w - Frame * 2 - 28, 40);
            content.SetBounds(Frame + 14, Frame + HeaderH, w - Frame * 2 - 28, h - HeaderH - FooterH - Frame * 2);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            FxPanel.PaintGaps(this, e.Graphics, e.ClipRectangle);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using (var pen = new Pen(Gfx.A(Theme.P.BorderHi, 170)))
                e.Graphics.DrawRectangle(pen, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                DialogResult = DialogResult.Cancel;
                Close();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (Animator.Instant) return;
            try
            {
                Opacity = 0;
                Animator.Run(this, "fadeIn", 0, 1, 220, Ease.OutCubic, delegate(float v) { Opacity = v; });
                Animator.After(this, "fadeGuard", 600, delegate { if (!IsDisposed && Opacity < 1) Opacity = 1; });
            }
            catch (Exception) { }
        }

        private void OnBackdrop()
        {
            if (IsDisposed || !IsHandleCreated || !Visible) return;
            FxPanel.TickChildren(this);
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
                Animator.Cancel(this);
            }
            base.Dispose(disposing);
        }

        // ---------- перетаскивание за заголовок ----------

        private void BeginDrag()
        {
            if (Native.DragWindow(this)) return;
            manualDrag = true;
            dragOrigin = Cursor.Position;
            dragWindow = Location;
        }

        private void DragMove()
        {
            if (!manualDrag) return;
            if (Control.MouseButtons != MouseButtons.Left) { manualDrag = false; return; }
            Point c = Cursor.Position;
            Location = new Point(dragWindow.X + c.X - dragOrigin.X, dragWindow.Y + c.Y - dragOrigin.Y);
        }

        // ---------- сообщения на форме ----------

        /// <summary>Сообщение об ошибке над полями формы (null — скрыть).</summary>
        public void ShowMessage(string text, Tone tone = Tone.Bad)
        {
            if (string.IsNullOrEmpty(text))
            {
                message.Visible = false;
                message.Text = "";
            }
            else
            {
                message.Tone = tone;
                message.Icon = tone == Tone.Bad ? "warn" : tone == Tone.Good ? "check" : "info";
                message.Text = text;
                message.Visible = true;
            }
            message.Parent.PerformLayout();
        }

        // ---------- построение полей ----------

        /// <summary>Поле с подписью над ним.</summary>
        public static FxBox Field(string caption, Control input, int inputHeight = 36)
        {
            var box = FxBox.V(4);
            box.Add(new FxLabel(caption, Fonts.Semi(8.8f), Tone.Dim), 18);
            box.Add(input, inputHeight);
            box.Height = 18 + 4 + inputHeight;
            return box;
        }

        /// <summary>Несколько полей в одну строку.</summary>
        public static FxBox Row(params Control[] fields)
        {
            var row = FxBox.H(12);
            int h = 0;
            foreach (Control f in fields) h = Math.Max(h, f.Height);   // до добавления: раскладка растянет поля
            row.Height = h;
            foreach (Control f in fields) row.Fill(f);
            return row;
        }

        /// <summary>Добавить кнопку в нижнюю панель (кнопки прижаты вправо в порядке добавления).</summary>
        public FxButton AddButton(string text, bool primary, string icon, EventHandler click)
        {
            var b = new FxButton(text, primary, icon).AutoWidth();
            b.Click += click;
            footerButtons.Add(b);
            RebuildFooter();
            return b;
        }

        private readonly List<Control> footerButtons = new List<Control>();
        private readonly List<Control> footerLeft = new List<Control>();

        /// <summary>Элемент в левой части нижней панели (например, ссылка «Регистрация»).</summary>
        public void AddFooterLeft(Control c)
        {
            footerLeft.Add(c);
            RebuildFooter();
        }

        private void RebuildFooter()
        {
            Footer.Clear();
            foreach (Control c in footerLeft) Footer.Add(c, c.Width);
            Footer.Fill(new FxPanel());
            foreach (Control c in footerButtons) Footer.Add(c, c.Width);
            Footer.PerformLayout();
        }

        // ---------- стандартные окна сообщений ----------

        /// <summary>Подтверждение действия (замена MessageBox с кнопками «Да» / «Нет»).</summary>
        public static bool Confirm(IWin32Window owner, string title, string text, string okText, bool danger)
        {
            using (var d = new MessageDialog(title, text, danger ? "warn" : "info", null))
            {
                FxButton ok = null;
                d.AddButton("Отмена", false, null, delegate { d.DialogResult = DialogResult.Cancel; d.Close(); });
                ok = d.AddButton(okText, true, danger ? "trash" : "check", delegate { d.DialogResult = DialogResult.OK; d.Close(); });
                ok.Danger = danger;
                return d.ShowDialog(owner) == DialogResult.OK;
            }
        }

        /// <summary>Сообщение пользователю; details — технические подробности, показываются по кнопке.</summary>
        public static void Info(IWin32Window owner, string title, string text, Tone tone, string details = null)
        {
            string icon = tone == Tone.Bad ? "warn" : tone == Tone.Good ? "check" : "info";
            using (var d = new MessageDialog(title, text, icon, details))
            {
                d.AddButton("Понятно", true, null, delegate { d.DialogResult = DialogResult.OK; d.Close(); });
                d.ShowDialog(owner);
            }
        }

        private sealed class MessageDialog : FxDialog
        {
            public MessageDialog(string title, string text, string icon, string details)
            {
                Title = title;
                IconName = icon;
                Text = title;
                var label = new FxLabel(text, Fonts.Body(10f), Tone.Text, true);
                Body.Add(label, 0);
                int h = (int)Math.Ceiling(Gfx.Measure(text, Fonts.Body(10f), 440).Height) + 8;
                FxText detailBox = null;
                if (!string.IsNullOrEmpty(details))
                {
                    detailBox = new FxText { Text = details, FontSize = 8.5f, Visible = false };
                    Body.Add(detailBox, 110);
                    var more = new FxButton("Подробнее", false, "terminal").AutoWidth();
                    more.Click += delegate
                    {
                        detailBox.Visible = !detailBox.Visible;
                        Height += detailBox.Visible ? 120 : -120;
                        Body.PerformLayout();
                    };
                    AddFooterLeft(more);
                }
                Size = new Size(540, Math.Min(560, HeaderH + FooterH + Frame * 2 + 40 + h));
            }
        }

        // ---------- заголовок ----------

        private sealed class HeaderView : FxControl
        {
            private readonly FxDialog d;
            private bool hot;

            public HeaderView(FxDialog d) { this.d = d; }

            private RectangleF CloseRect { get { return new RectangleF(Width - 46, 14, 36, 30); } }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                d.DragMove();
                bool h = CloseRect.Contains(e.Location);
                if (h != hot)
                {
                    hot = h;
                    d.closeHover.To(h ? 1f : 0f, h ? 130 : 240);
                    Cursor = h ? Cursors.Hand : Cursors.Default;
                }
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                base.OnMouseLeave(e);
                hot = false;
                d.closeHover.To(0f, 240);
                Invalidate();
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                if (CloseRect.Contains(e.Location))
                {
                    d.DialogResult = DialogResult.Cancel;
                    d.Close();
                    return;
                }
                d.BeginDrag();
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                d.manualDrag = false;
            }

            protected override void Draw(Graphics g)
            {
                var mark = new RectangleF(14, 16, 40, 40);
                Gfx.Glow(g, mark, 12, P.Accent, 5, 1.6f, 40);
                using (LinearGradientBrush b = Gfx.TriBrush(RectangleF.Inflate(mark, 1, 1), 45f))
                    Gfx.FillRound(g, b, mark, 12);
                Icons.Draw(g, d.IconName, RectangleF.Inflate(mark, -9, -9), Color.White, 2f);
                float right = Width - 60;
                using (var b = new LinearGradientBrush(new RectangleF(0, 0, Math.Max(80, Gfx.Measure(d.Title, Fonts.Display(12.5f)).Width + 80), 40),
                           P.Text, Gfx.Mix(P.Text, P.Accent2, 0.55f), 0f))
                    Gfx.Text(g, d.Title, Fonts.Display(12.5f), b, new RectangleF(66, string.IsNullOrEmpty(d.Subtitle) ? 22 : 14, right - 66, 28));
                if (!string.IsNullOrEmpty(d.Subtitle))
                    Gfx.Text(g, d.Subtitle, Fonts.Body(8.8f), P.TextDim, new RectangleF(67, 42, right - 66, 20));

                RectangleF r = CloseRect;
                float h = d.closeHover.Value;
                Gfx.FillRound(g, Gfx.A(Gfx.Mix(P.Surface, P.Bad, h), (int)(150 + 105 * h)), r, 9);
                Gfx.DrawRound(g, Gfx.A(P.BorderHi, 140), r, 9);
                Icons.Draw(g, "close", new RectangleF(r.X + (r.Width - 13) / 2, r.Y + (r.Height - 13) / 2, 13, 13),
                    Gfx.Mix(P.TextDim, Color.White, h), 1.6f);
            }
        }

        private sealed class DialogCard : FxCard
        {
            public DialogCard()
            {
                Padding = new Padding(18, 16, 18, 16);
            }
        }
    }

    // =====================================================================
    //  Выпадающий список
    // =====================================================================

    /// <summary>Элемент выпадающего списка: код записи и текст.</summary>
    public sealed class FxItem
    {
        public int Id;
        public string Text;

        public FxItem(int id, string text)
        {
            Id = id;
            Text = text;
        }

        public override string ToString() { return Text; }
    }

    /// <summary>Выпадающий список в скруглённой рамке (обёртка над ComboBox в режиме DropDownList).</summary>
    public class FxSelect : FxControl, IThemed
    {
        private readonly ComboBox box = new ComboBox();
        private readonly AnimValue focus, error;

        public event EventHandler SelectedChanged;

        public FxSelect()
        {
            focus = new AnimValue(this);
            error = new AnimValue(this);
            Size = new Size(220, 36);
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.FlatStyle = FlatStyle.Flat;
            box.Font = Fonts.Medium(10f);
            box.IntegralHeight = false;
            box.MaxDropDownItems = 12;
            box.GotFocus += delegate { focus.To(1f); Invalidate(); };
            box.LostFocus += delegate { focus.To(0f, 240); Invalidate(); };
            box.SelectedIndexChanged += delegate
            {
                EventHandler h = SelectedChanged;
                if (h != null) h(this, EventArgs.Empty);
            };
            Controls.Add(box);
            ApplyTheme();
        }

        public ComboBox Box { get { return box; } }

        public void SetItems(IEnumerable<FxItem> items, bool withEmpty = false, string emptyText = "— все —")
        {
            box.BeginUpdate();
            box.Items.Clear();
            if (withEmpty) box.Items.Add(new FxItem(0, emptyText));
            foreach (FxItem it in items) box.Items.Add(it);
            box.EndUpdate();
            if (box.Items.Count > 0) box.SelectedIndex = 0;
        }

        public void SetItems(params string[] texts)
        {
            box.BeginUpdate();
            box.Items.Clear();
            for (int i = 0; i < texts.Length; i++) box.Items.Add(new FxItem(i, texts[i]));
            box.EndUpdate();
            if (box.Items.Count > 0) box.SelectedIndex = 0;
        }

        public FxItem SelectedItem { get { return box.SelectedItem as FxItem; } }

        /// <summary>Код выбранной записи; 0 — ничего не выбрано или выбран пункт «все».</summary>
        public int SelectedId
        {
            get { FxItem it = SelectedItem; return it != null ? it.Id : 0; }
            set
            {
                for (int i = 0; i < box.Items.Count; i++)
                {
                    var it = box.Items[i] as FxItem;
                    if (it != null && it.Id == value) { box.SelectedIndex = i; return; }
                }
            }
        }

        public string SelectedText
        {
            get { FxItem it = SelectedItem; return it != null ? it.Text : ""; }
            set
            {
                for (int i = 0; i < box.Items.Count; i++)
                {
                    var it = box.Items[i] as FxItem;
                    if (it != null && it.Text == value) { box.SelectedIndex = i; return; }
                }
            }
        }

        public void ApplyTheme()
        {
            box.BackColor = P.Surface2;
            box.ForeColor = P.Text;
            Invalidate();
        }

        public void Shake()
        {
            error.Set(1f);
            error.To(0f, 1400, Ease.InCubic);
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            int h = box.PreferredHeight;
            box.SetBounds(8, (Height - h) / 2, Math.Max(10, Width - 16), h);
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
}
