using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FxKit
{
    /// <summary>Смысловая подсветка строки или ячейки таблицы.</summary>
    public enum RowTone { None, Good, Bad, Accent, Forecast, Total }

    public sealed class FxColumn
    {
        public string Header = "";
        public float Weight = 1f;
        public StringAlignment Align = StringAlignment.Center;
        public bool ReadOnly;
        public bool NoWrap;
    }

    public sealed class FxRow
    {
        public string[] Cells;
        public RowTone Tone;
        public bool Bold;
        public RowTone[] CellTone;
        public string[] CellError;
        public object Tag;
        internal float Y, H;

        public FxRow SetTone(RowTone tone) { Tone = tone; return this; }
        public FxRow SetBold() { Bold = true; return this; }
    }

    /// <summary>
    /// Таблица, нарисованная целиком своими силами: каскадное появление строк, подсветка под курсором,
    /// тонкая полоса прокрутки, перенос текста и (по желанию) редактирование с добавлением и удалением строк.
    /// </summary>
    public class FxTable : FxControl, IEnterAnim, IThemed
    {
        public readonly List<FxColumn> Columns = new List<FxColumn>();
        public readonly List<FxRow> Rows = new List<FxRow>();

        private readonly AnimValue scroll;
        private readonly TextBox editor = new TextBox();
        private float enter = 1f, headerH = 32, contentH;
        private float[] colX = new float[0], colW = new float[0];
        private bool dirty = true, dragBar, committing;
        private int hoverRow = -1, hoverCol = -1, editRow = -1, editCol = -1, dragY;
        private float dragScroll;

        public bool Editable;
        public bool WrapCells;
        public int RowHeight = 28;
        public string EmptyText = "нет данных";
        public string AddRowText = "добавить строку";
        public float FontSize = 9f;

        /// <summary>Пользователь изменил значение ячейки.</summary>
        public event Action<FxRow, int> CellEdited;
        /// <summary>Пользователь добавил или удалил строку.</summary>
        public event Action RowsEdited;

        public FxTable()
        {
            scroll = new AnimValue(this);
            editor.BorderStyle = BorderStyle.None;
            editor.Visible = false;
            editor.TextAlign = HorizontalAlignment.Center;
            editor.KeyDown += Editor_KeyDown;
            editor.LostFocus += delegate { CommitEdit(); };
            Controls.Add(editor);
            ApplyTheme();
        }

        public void ApplyTheme()
        {
            editor.BackColor = P.SurfaceHi;
            editor.ForeColor = P.Text;
            editor.Font = Fonts.Semi(FontSize + 0.5f);
            Invalidate();
        }

        // ---------- наполнение ----------

        public void SetColumns(params string[] headers)
        {
            Columns.Clear();
            for (int i = 0; i < headers.Length; i++)
                Columns.Add(new FxColumn { Header = headers[i] });
            Touch();
        }

        public void Clear()
        {
            CancelEdit();
            Rows.Clear();
            Touch();
        }

        public FxRow AddRow(params object[] cells)
        {
            var row = new FxRow { Cells = new string[Math.Max(cells.Length, Columns.Count)] };
            for (int i = 0; i < row.Cells.Length; i++)
                row.Cells[i] = i < cells.Length && cells[i] != null ? Convert.ToString(cells[i]) : "";
            Rows.Add(row);
            Touch();
            return row;
        }

        public void SetCellTone(FxRow row, int col, RowTone tone)
        {
            if (row.CellTone == null) row.CellTone = new RowTone[row.Cells.Length];
            row.CellTone[col] = tone;
            Invalidate();
        }

        public void SetCellError(FxRow row, int col, string message)
        {
            if (row.CellError == null)
            {
                if (message == null) return;
                row.CellError = new string[row.Cells.Length];
            }
            row.CellError[col] = message;
            Invalidate();
        }

        /// <summary>Пометить, что содержимое изменилось (пересчёт раскладки при следующей отрисовке).</summary>
        public void Touch()
        {
            dirty = true;
            Invalidate();
        }

        public void PrepareEnter() { enter = 0f; Invalidate(); }

        public void PlayEnter(int delayMs)
        {
            Animator.Run(this, "enter", 0, 1, 640, Ease.OutCubic, delegate(float t) { enter = t; Invalidate(); }, null, delayMs);
        }

        // ---------- раскладка ----------

        private Font CellFont(bool bold) { return bold ? Fonts.Bold(FontSize) : Fonts.Medium(FontSize); }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            dirty = true;
        }

        private void LayoutNow()
        {
            if (!dirty) return;
            dirty = false;
            int n = Columns.Count;
            colX = new float[n];
            colW = new float[n];
            float total = 0;
            foreach (FxColumn c in Columns) total += Math.Max(0.01f, c.Weight);
            float avail = Math.Max(10, Width - 10 - (Editable ? 22 : 0)), x = 2;
            for (int i = 0; i < n; i++)
            {
                colX[i] = x;
                colW[i] = avail * Math.Max(0.01f, Columns[i].Weight) / total;
                x += colW[i];
            }
            Font hf = Fonts.Bold(FontSize - 1f);
            headerH = 30;
            for (int i = 0; i < n; i++)
                headerH = Math.Max(headerH, Gfx.Measure(Columns[i].Header, hf, colW[i] - 8).Height + 12);
            headerH = Math.Min(headerH, 58);

            float y = 0;
            foreach (FxRow r in Rows)
            {
                float h = RowHeight;
                if (WrapCells)
                {
                    Font f = CellFont(r.Bold);
                    for (int i = 0; i < n && i < r.Cells.Length; i++)
                        if (!Columns[i].NoWrap) h = Math.Max(h, Gfx.Measure(r.Cells[i], f, colW[i] - 16).Height + 16);
                }
                r.Y = y;
                r.H = h;
                y += h;
            }
            if (Editable) y += RowHeight;
            contentH = y;
            if (scroll.Target > MaxScroll) scroll.Set(MaxScroll);
        }

        private float ViewH { get { return Math.Max(1, Height - headerH - 2); } }
        private float MaxScroll { get { return Math.Max(0, contentH - ViewH); } }

        private int RowAt(float y)
        {
            float cy = y - headerH + scroll.Value;
            if (y < headerH || cy < 0) return -1;
            for (int i = 0; i < Rows.Count; i++)
                if (cy >= Rows[i].Y && cy < Rows[i].Y + Rows[i].H) return i;
            if (Editable && cy >= contentH - RowHeight && cy < contentH) return Rows.Count;   // строка «добавить»
            return -1;
        }

        private int ColAt(float x)
        {
            for (int i = 0; i < colX.Length; i++)
                if (x >= colX[i] && x < colX[i] + colW[i]) return i;
            return -1;
        }

        // ---------- мышь ----------

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragBar)
            {
                float k = contentH / ViewH;
                scroll.Set(Gfx.Clamp(dragScroll + (e.Y - dragY) * k, 0, MaxScroll));
                return;
            }
            int r = RowAt(e.Y), c = ColAt(e.X);
            if (r != hoverRow || c != hoverCol)
            {
                hoverRow = r;
                hoverCol = c;
                Invalidate();
            }
            bool hand = Editable && r >= 0 && (r == Rows.Count || e.X >= Width - 30 || (c >= 0 && !Columns[c].ReadOnly));
            Cursor = hand ? (r < Rows.Count && c >= 0 && e.X < Width - 30 ? Cursors.IBeam : Cursors.Hand) : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            hoverRow = hoverCol = -1;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (MaxScroll <= 0) return;
            CommitEdit();
            scroll.To(Gfx.Clamp(scroll.Target - Math.Sign(e.Delta) * RowHeight * 3, 0, MaxScroll), 260, Ease.OutCubic);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            LayoutNow();
            if (MaxScroll > 0 && e.X >= Width - 9)
            {
                dragBar = true;
                dragY = e.Y;
                dragScroll = scroll.Value;
                return;
            }
            if (!Editable) return;
            int r = RowAt(e.Y), c = ColAt(e.X);
            if (r < 0) { CommitEdit(); return; }
            if (r == Rows.Count)
            {
                CommitEdit();
                AppendRow();
                return;
            }
            if (e.X >= Width - 30)
            {
                CommitEdit();
                Rows.RemoveAt(r);
                Touch();
                RaiseRows();
                return;
            }
            if (c >= 0 && !Columns[c].ReadOnly) BeginEdit(r, c);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            dragBar = false;
        }

        // ---------- редактирование ----------

        private void AppendRow()
        {
            var cells = new object[Columns.Count];
            for (int i = 0; i < cells.Length; i++) cells[i] = "";
            AddRow(cells);
            LayoutNow();
            scroll.Set(MaxScroll);
            RaiseRows();
            int first = -1;
            for (int i = 0; i < Columns.Count; i++)
                if (!Columns[i].ReadOnly) { first = i; break; }
            if (first >= 0) BeginEdit(Rows.Count - 1, first);
        }

        private void BeginEdit(int row, int col)
        {
            CommitEdit();
            if (row < 0 || row >= Rows.Count) return;
            LayoutNow();
            FxRow r = Rows[row];
            float y = headerH + r.Y - scroll.Value;
            if (y < headerH) { scroll.Set(r.Y); y = headerH; }
            if (y + r.H > Height) { scroll.Set(Math.Min(MaxScroll, r.Y + r.H - ViewH)); y = headerH + r.Y - scroll.Value; }
            editRow = row;
            editCol = col;
            editor.Text = r.Cells[col];
            int h = editor.PreferredHeight;
            editor.SetBounds((int)colX[col] + 8, (int)(y + (r.H - h) / 2), (int)colW[col] - 16, h);
            editor.Visible = true;
            editor.BringToFront();
            editor.Focus();
            editor.SelectAll();
            Invalidate();
        }

        private void CommitEdit()
        {
            if (editRow < 0 || committing) return;
            committing = true;
            try
            {
                int r = editRow, c = editCol;
                editRow = editCol = -1;
                editor.Visible = false;
                if (r < Rows.Count && Rows[r].Cells[c] != editor.Text)
                {
                    Rows[r].Cells[c] = editor.Text;
                    Action<FxRow, int> h = CellEdited;
                    if (h != null) h(Rows[r], c);
                }
                Invalidate();
            }
            finally { committing = false; }
        }

        private void CancelEdit()
        {
            editRow = editCol = -1;
            editor.Visible = false;
        }

        private void Editor_KeyDown(object sender, KeyEventArgs e)
        {
            int r = editRow, c = editCol;
            if (r < 0) return;
            if (e.KeyCode == Keys.Escape)
            {
                CancelEdit();
                Invalidate();
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Down)
            {
                e.SuppressKeyPress = true;
                CommitEdit();
                if (r + 1 < Rows.Count) BeginEdit(r + 1, c);
                else if (e.KeyCode == Keys.Enter) AppendRow();
            }
            else if (e.KeyCode == Keys.Up)
            {
                e.SuppressKeyPress = true;
                CommitEdit();
                if (r > 0) BeginEdit(r - 1, c);
            }
            else if (e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                CommitEdit();
                for (int k = c + 1; k < Columns.Count; k++)
                    if (!Columns[k].ReadOnly) { BeginEdit(r, k); return; }
                if (r + 1 < Rows.Count)
                    for (int k = 0; k < Columns.Count; k++)
                        if (!Columns[k].ReadOnly) { BeginEdit(r + 1, k); return; }
            }
        }

        private void RaiseRows()
        {
            Action h = RowsEdited;
            if (h != null) h();
        }

        // ---------- отрисовка ----------

        private static Color ToneColor(RowTone t)
        {
            switch (t)
            {
                case RowTone.Good: return P.Good;
                case RowTone.Bad: return P.Bad;
                case RowTone.Accent: return P.Accent;
                case RowTone.Forecast: return P.Series[1];
                default: return Color.Empty;
            }
        }

        protected override void Draw(Graphics g)
        {
            LayoutNow();
            int n = Columns.Count;
            if (n == 0) return;

            // заголовок
            Font hf = Fonts.Bold(FontSize - 1f);
            for (int i = 0; i < n; i++)
                Gfx.Text(g, Columns[i].Header, hf, P.TextMute, new RectangleF(colX[i] + 4, 0, colW[i] - 8, headerH - 2),
                    Columns[i].Align, StringAlignment.Center, true);
            using (var pen = new Pen(P.Border))
                g.DrawLine(pen, 0, headerH - 1, Width - 8, headerH - 1);

            if (Rows.Count == 0 && !Editable)
            {
                Gfx.Text(g, EmptyText, Fonts.Body(9.5f), P.TextMute, new RectangleF(0, headerH, Width, Height - headerH), StringAlignment.Center);
                return;
            }

            GraphicsState st = g.Save();
            g.SetClip(new RectangleF(0, headerH, Width, Height - headerH));
            float off = headerH - scroll.Value;
            int shownIndex = 0;
            using (var grid = new Pen(Gfx.A(P.Grid, P.Dark ? 255 : 255)))
            {
                for (int ri = 0; ri < Rows.Count; ri++)
                {
                    FxRow r = Rows[ri];
                    float y = r.Y + off;
                    if (y + r.H < headerH || y > Height) continue;
                    float local = Gfx.Clamp(enter * 3.2f - shownIndex * 0.11f, 0, 1);
                    shownIndex++;
                    if (local <= 0.01f) continue;
                    float dy = (1 - Ease.OutCubic(local)) * 12;
                    var rr = new RectangleF(1, y + dy + 1, Width - 11, r.H - 2);

                    Color tone = ToneColor(r.Tone);
                    if (r.Tone == RowTone.Good || r.Tone == RowTone.Bad || r.Tone == RowTone.Accent)
                        Gfx.FillRound(g, Gfx.A(tone, (int)((P.Dark ? 44 : 34) * local)), rr, 7);
                    else if (ri == hoverRow)
                        Gfx.FillRound(g, Gfx.A(P.SurfaceHi, (int)(190 * local)), rr, 7);
                    if (r.Tone == RowTone.Good || r.Tone == RowTone.Bad || r.Tone == RowTone.Accent)
                        Gfx.FillRound(g, Gfx.A(tone, (int)(230 * local)), new RectangleF(1, y + dy + 6, 3, r.H - 12), 1.5f);
                    if (r.Tone == RowTone.Total)
                        g.DrawLine(grid, 2, y + dy, Width - 12, y + dy);
                    else if (ri > 0 && Rows[ri - 1].Tone == RowTone.None && r.Tone == RowTone.None && ri != hoverRow && ri - 1 != hoverRow)
                        g.DrawLine(grid, 8, y + dy, Width - 18, y + dy);

                    bool bold = r.Bold || r.Tone == RowTone.Forecast || r.Tone == RowTone.Total;
                    Font f = CellFont(bold);
                    Color text = r.Tone == RowTone.Forecast ? Gfx.Mix(P.Series[1], P.Text, 0.25f) : (bold ? P.Text : Gfx.Mix(P.TextDim, P.Text, 0.55f));
                    for (int c = 0; c < n && c < r.Cells.Length; c++)
                    {
                        if (ri == editRow && c == editCol)
                        {
                            Gfx.FillRound(g, P.SurfaceHi, new RectangleF(colX[c] + 3, y + 3, colW[c] - 6, r.H - 6), 6);
                            Gfx.DrawRound(g, P.Accent, new RectangleF(colX[c] + 3, y + 3, colW[c] - 6, r.H - 6), 6);
                            continue;
                        }
                        var cr = new RectangleF(colX[c] + 6, y + dy, colW[c] - 12, r.H);
                        bool err = r.CellError != null && c < r.CellError.Length && r.CellError[c] != null;
                        RowTone ct = r.CellTone != null && c < r.CellTone.Length ? r.CellTone[c] : RowTone.None;
                        if (err)
                        {
                            Gfx.FillRound(g, Gfx.A(P.Bad, 60), new RectangleF(colX[c] + 3, y + dy + 3, colW[c] - 6, r.H - 6), 6);
                            Gfx.DrawRound(g, Gfx.A(P.Bad, 200), new RectangleF(colX[c] + 3, y + dy + 3, colW[c] - 6, r.H - 6), 6);
                        }
                        else if (ct != RowTone.None)
                        {
                            Gfx.FillRound(g, Gfx.A(ToneColor(ct), P.Dark ? 60 : 46), new RectangleF(colX[c] + 5, y + dy + 4, colW[c] - 10, r.H - 8), 6);
                        }
                        else if (Editable && Columns[c].ReadOnly)
                        {
                            Gfx.Text(g, r.Cells[c], Fonts.Mono(FontSize - 0.5f), Gfx.A(P.TextMute, local), cr, Columns[c].Align);
                            continue;
                        }
                        else if (Editable && ri == hoverRow && c == hoverCol)
                        {
                            Gfx.DrawRound(g, P.BorderHi, new RectangleF(colX[c] + 3, y + dy + 3, colW[c] - 6, r.H - 6), 6);
                        }
                        Gfx.Text(g, r.Cells[c], f, Gfx.A(text, local), cr, Columns[c].Align, StringAlignment.Center, WrapCells && !Columns[c].NoWrap);
                    }
                    if (Editable && ri == hoverRow)
                        Icons.Draw(g, "close", new RectangleF(Width - 27, y + (r.H - 12) / 2, 12, 12), hoverCol < 0 ? P.Bad : P.TextMute, 1.7f);
                }

                if (Editable)
                {
                    float y = contentH - RowHeight + off;
                    bool hot = hoverRow == Rows.Count;
                    var ar = new RectangleF(3, y + 3, Width - 15, RowHeight - 6);
                    using (var pen = new Pen(hot ? P.Accent : P.Border) { DashStyle = DashStyle.Dash })
                    using (GraphicsPath path = Gfx.Round(ar, 7))
                        g.DrawPath(pen, path);
                    float tw = Gfx.Measure(AddRowText, Fonts.Medium(8.5f)).Width;
                    float sx = (Width - 12 - tw - 16) / 2;
                    Icons.Draw(g, "plus", new RectangleF(sx, y + (RowHeight - 11) / 2f, 11, 11), hot ? P.Accent2 : P.TextMute, 1.7f);
                    Gfx.Text(g, AddRowText, Fonts.Medium(8.5f), hot ? P.Text : P.TextMute, new RectangleF(sx + 15, y, tw + 10, RowHeight));
                }
            }
            g.Restore(st);

            if (MaxScroll > 0)
            {
                float bh = Math.Max(22, ViewH * ViewH / contentH);
                float by = headerH + (ViewH - bh) * (scroll.Value / MaxScroll);
                Gfx.FillRound(g, Gfx.A(P.TextMute, dragBar ? 200 : 110), new RectangleF(Width - 6, by, 4, bh), 2);
            }

            // всплывающее сообщение об ошибке в ячейке
            if (hoverRow >= 0 && hoverRow < Rows.Count && hoverCol >= 0)
            {
                FxRow r = Rows[hoverRow];
                string msg = r.CellError != null && hoverCol < r.CellError.Length ? r.CellError[hoverCol] : null;
                if (msg != null)
                {
                    Font tf = Fonts.Medium(8.5f);
                    float w = Math.Min(Width - 12, 280);
                    SizeF sz = Gfx.Measure(msg, tf, w - 16);
                    float ty = headerH + r.Y - scroll.Value + r.H + 2;
                    if (ty + sz.Height + 12 > Height) ty = headerH + r.Y - scroll.Value - sz.Height - 14;
                    var tr = new RectangleF(Math.Max(4, Math.Min(Width - w - 8, colX[hoverCol])), ty, w, sz.Height + 10);
                    Gfx.FillRound(g, P.Dark ? Theme.Hex("2A1420") : Theme.Hex("FFF0F1"), tr, 8);
                    Gfx.DrawRound(g, P.Bad, tr, 8);
                    Gfx.Text(g, msg, tf, P.Text, RectangleF.Inflate(tr, -8, -4), StringAlignment.Near, StringAlignment.Near, true);
                }
            }
        }
    }
}
