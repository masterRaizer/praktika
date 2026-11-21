using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Forms
{
    /// <summary>Общие для форм вещи: показ ошибок, форматирование, преобразование справочников.</summary>
    public static class Ui
    {
        public static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        public const string AppName = "Киноточка";

        private static string Resource(string file)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", file);
        }

        /// <summary>Логотип из ресурсов (null — окно нарисует значок).</summary>
        public static Image LoadLogo()
        {
            try
            {
                string path = Resource("logo.png");
                if (!File.Exists(path)) return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (Image source = Image.FromStream(stream))
                    return new Bitmap(source);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Значок окна из ресурсов.</summary>
        public static void ApplyIcon(Form form)
        {
            try
            {
                string path = Resource("icon.ico");
                if (File.Exists(path)) form.Icon = new Icon(path);
            }
            catch (Exception)
            {
                // остаётся значок по умолчанию
            }
        }

        public static string Money(decimal v) { return v.ToString("N2", Ru); }
        public static string DateTimeText(DateTime d) { return d.ToString("dd.MM.yyyy HH:mm", Ru); }
        public static string DateText(DateTime d) { return d.ToString("dd.MM.yyyy", Ru); }

        public static IEnumerable<FxItem> Items(IEnumerable<LookupItem> list)
        {
            return list.Select(x => new FxItem(x.Id, x.Name));
        }

        /// <summary>
        /// Показ ошибки пользователю: понятный текст; технические подробности (номер ошибки SQL Server)
        /// — только по кнопке «Подробнее» и без паролей и строки подключения.
        /// </summary>
        public static void ShowError(IWin32Window owner, Exception ex, string action)
        {
            AppException a = ErrorTranslator.Translate(ex, action);
            string title;
            switch (a.Kind)
            {
                case ErrorKind.Connection: title = "Нет подключения к базе данных"; break;
                case ErrorKind.Constraint: title = "Действие отклонено базой данных"; break;
                case ErrorKind.Duplicate: title = "Такая запись уже есть"; break;
                case ErrorKind.Validation: title = "Проверьте введённые данные"; break;
                case ErrorKind.Auth: title = "Нет доступа"; break;
                default: title = "Ошибка"; break;
            }
            string details = a.Technical == null ? null
                : "Действие: " + action + "\r\n" + a.Technical + "\r\nЖурнал ошибок: " + ErrorLog.FilePath;
            FxDialog.Info(owner, title, a.Message, Tone.Bad, details);
        }
    }

    /// <summary>
    /// Форма ввода с проверкой полей: ошибка проверки показывается над полями,
    /// поле с ошибкой подсвечивается красной рамкой и получает фокус.
    /// </summary>
    public class EditDialog : FxDialog
    {
        private readonly Dictionary<string, Control> fields = new Dictionary<string, Control>();

        protected T Register<T>(string name, T control) where T : Control
        {
            fields[name] = control;
            return control;
        }

        protected static FxTextBox Input(string placeholder, string value = "")
        {
            return new FxTextBox { Placeholder = placeholder, Value = value ?? "" };
        }

        /// <summary>Выполнить действие; ошибки показать на форме. Возвращает true при успехе.</summary>
        protected bool Try(string action, Action body)
        {
            try
            {
                ShowMessage(null);
                body();
                return true;
            }
            catch (Exception ex)
            {
                AppException a = ErrorTranslator.Translate(ex, action);
                if (a.Kind == ErrorKind.Connection)
                {
                    ShowMessage("Нет подключения к базе данных. Данные не сохранены — проверьте подключение и повторите.");
                    Ui.ShowError(this, a, action);
                    return false;
                }
                ShowMessage(a.Message);
                Control c;
                if (a.Field != null && fields.TryGetValue(a.Field, out c)) Highlight(c);
                return false;
            }
        }

        private static void Highlight(Control c)
        {
            var t = c as FxTextBox;
            if (t != null)
            {
                t.Shake();
                t.FocusBox();
                t.Box.SelectAll();
                return;
            }
            var s = c as FxSelect;
            if (s != null)
            {
                s.Shake();
                s.Box.Focus();
                return;
            }
            c.Focus();
        }
    }
}
