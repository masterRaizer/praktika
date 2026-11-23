using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Den5_7.Logic;
using Den5_7.Models;
using FxKit;

namespace Den5_7.Pages
{
    /// <summary>Общие элементы оформления и стандартные окна сообщений.</summary>
    public static class Ui
    {
        public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        public static FxLabel Caption(string text)
        {
            return new FxLabel(text, Fonts.Semi(8f), Tone.Mute) { Height = 16 };
        }

        public static FxLabel Para(string text, Tone tone = Tone.Dim)
        {
            return new FxLabel(text, Fonts.Body(9f), tone, true);
        }

        /// <summary>Поле с подписью сверху.</summary>
        public static FxBox Field(string caption, Control input, int inputHeight = 36)
        {
            FxBox b = FxBox.V(3);
            b.Add(Caption(caption), 16).Add(input, inputHeight);
            b.Height = inputHeight + 19;
            return b;
        }

        public static FxBox Row(int height, int gap = 10)
        {
            FxBox b = FxBox.H(gap);
            b.Height = height;
            return b;
        }

        public static FxBox Tiles(params FxStat[] stats)
        {
            FxBox b = FxBox.H(10);
            foreach (FxStat s in stats) b.Fill(s);
            b.Height = 78;
            return b;
        }

        public static FxCard Card(string title, string icon, Control content, Control header = null)
        {
            var card = new FxCard(title, icon) { Padding = new Padding(14, 12, 10, 10) };
            card.With(content);
            if (header != null) card.WithHeader(header);
            return card;
        }

        public static FxButton Button(string text, string icon, bool primary = false, int extra = 10)
        {
            var b = new FxButton(text, primary, icon).AutoWidth(extra);
            b.Height = 32;
            return b;
        }

        /// <summary>Заполнение таблицы из DataTable.</summary>
        public static void Fill(FxTable table, DataTable data)
        {
            table.Clear();
            var headers = new string[data.Columns.Count];
            for (int i = 0; i < headers.Length; i++) headers[i] = data.Columns[i].ColumnName;
            table.SetColumns(headers);
            foreach (DataRow row in data.Rows)
            {
                var cells = new object[headers.Length];
                for (int i = 0; i < cells.Length; i++) cells[i] = Format(row[i]);
                table.AddRow(cells);
            }
        }

        public static string Format(object value)
        {
            if (value == null || value is DBNull) return "";
            if (value is decimal) return ((decimal)value).ToString("N2", Ru);
            if (value is DateTime) return ((DateTime)value).ToString("dd.MM.yyyy HH:mm", Ru);
            if (value is bool) return (bool)value ? "да" : "нет";
            return Convert.ToString(value, Ru);
        }

        /// <summary>Строки таблицы для экспорта.</summary>
        public static IEnumerable<IList<string>> Rows(FxTable table)
        {
            foreach (FxRow r in table.Rows) yield return r.Cells;
        }

        public static string[] Headers(FxTable table)
        {
            var h = new string[table.Columns.Count];
            for (int i = 0; i < h.Length; i++) h[i] = table.Columns[i].Header;
            return h;
        }

        /// <summary>«Смирнов Андрей Петрович» → «Смирнов А. П.»</summary>
        public static string ShortName(string fullName)
        {
            string[] parts = (fullName ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return fullName ?? "";
            string result = parts[0];
            for (int i = 1; i < parts.Length && i < 3; i++) result += " " + parts[i][0] + ".";
            return result;
        }

        // ---------- окна сообщений: заголовок и значок по типу ----------

        public static void Info(IWin32Window owner, string text, string title = "Готово")
        {
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public static void Warn(IWin32Window owner, string text, string title = "Проверьте данные")
        {
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        public static void Error(IWin32Window owner, string text, string title = "Ошибка")
        {
            MessageBox.Show(owner, text, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool Confirm(IWin32Window owner, string text, string title = "Подтверждение")
        {
            return MessageBox.Show(owner, text, title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        /// <summary>Сохранение CSV: окно выбора файла, экспорт, запись в журнал.</summary>
        public static void ExportCsv(IWin32Window owner, FxTable table, string defaultName, string what, FxWindow win)
        {
            if (table.Rows.Count == 0)
            {
                Warn(owner, "В таблице нет строк — экспортировать нечего.", "Экспорт в CSV");
                return;
            }
            using (var dialog = new SaveFileDialog { Filter = "CSV для Excel (*.csv)|*.csv", FileName = defaultName, Title = "Экспорт в CSV" })
            {
                if (dialog.ShowDialog(owner) != DialogResult.OK) return;
                try
                {
                    CsvExporter.Write(dialog.FileName, Headers(table), Rows(table));
                }
                catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
                {
                    Error(owner, "Файл не сохранён: " + ex.Message + "\n\nВыберите другую папку или закройте файл в Excel.", "Экспорт в CSV");
                    return;
                }
                User user = Session.Current;
                new Data.JournalRepository().Add(user == null ? (int?)null : user.Id, "Экспорт в CSV", what + ", строк: " + table.Rows.Count);
                win.Toast("Экспорт выполнен", what + ": " + table.Rows.Count + " строк сохранено в " + System.IO.Path.GetFileName(dialog.FileName), Tone.Good);
            }
        }
    }

    /// <summary>
    /// Раздел, доступный только вошедшему пользователю с нужными правами:
    /// пока прав нет, вместо содержимого показывается пояснение.
    /// </summary>
    public sealed class Guard : FxPager
    {
        private readonly Func<User, bool> allow;
        private readonly FxLabel message;
        private readonly string deniedText;
        private bool? shown;

        public event Action Opened;

        public Guard(Control content, Func<User, bool> allow, string deniedText)
        {
            this.allow = allow;
            this.deniedText = deniedText;
            VerticalSlide = false;
            Duration = 300;
            var card = new FxCard { Padding = new Padding(40) };
            FxBox box = FxBox.V(8);
            var icon = new FxLabel("", Fonts.Display(14f), Tone.Accent) { Icon = "key", Height = 40, Align = StringAlignment.Center };
            message = new FxLabel("", Fonts.Semi(11f), Tone.Text, true) { Align = StringAlignment.Center };
            box.Add(new FxPanel(), 120).Add(icon, 40).Add(message, 60);
            card.With(box);
            AddPage(card);
            AddPage(content);
            Session.Changed += delegate { Refresh(true); };
            Refresh(false);
        }

        public void Refresh(bool animate)
        {
            User u = Session.Current;
            bool ok = allow(u);
            message.Text = u == null ? "Войдите в систему на вкладке «Вход» (день 5)." : deniedText;
            if (shown == ok) return;
            shown = ok;
            ShowPage(ok ? 1 : 0, animate);
            if (ok)
            {
                Action h = Opened;
                if (h != null) h();
            }
        }
    }

    /// <summary>Страница дня: переключатель вкладок сверху и плавная смена их содержимого.</summary>
    public class DayPage : FxBox
    {
        protected readonly FxSegmented Tabs;
        protected readonly FxPager Pager = new FxPager { VerticalSlide = false, Duration = 320 };
        protected readonly FxBox TopBar = Ui.Row(36);

        public DayPage(params string[] tabs)
        {
            Vertical = true;
            Gap = 12;
            Padding = new Padding(0, 6, 6, 6);
            Tabs = new FxSegmented(tabs);
            Tabs.SelectedIndexChanged += delegate
            {
                Pager.ShowPage(Tabs.SelectedIndex);
                OnTabShown(Tabs.SelectedIndex);
            };
            TopBar.Add(Tabs, Math.Min(122 * tabs.Length, 800));
            Add(TopBar, 36).Fill(Pager);
        }

        /// <summary>Справа в строке вкладок — кто вошёл в систему.</summary>
        protected FxLabel AddUserLabel()
        {
            var label = new FxLabel("", Fonts.Semi(9.5f), Tone.Dim) { Align = StringAlignment.Far, Icon = "users" };
            EventHandler update = delegate
            {
                User u = Session.Current;
                label.Text = u == null ? "Вход не выполнен (день 5 → «Вход»)" : Ui.ShortName(u.FullName) + " · " + u.RoleName;
            };
            Session.Changed += update;
            update(null, EventArgs.Empty);
            TopBar.Fill(label);
            return label;
        }

        protected void AddTab(Control page)
        {
            Pager.AddPage(page);
            if (Pager.Count == 1) Pager.ShowPage(0, false);
        }

        public void ShowTab(int index)
        {
            Tabs.SelectedIndex = index;
        }

        protected virtual void OnTabShown(int index)
        {
        }

        protected FxWindow Win
        {
            get { return FindForm() as FxWindow; }
        }
    }
}
