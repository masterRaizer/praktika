using System;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;
using FxKit;

namespace ShagShoes
{
    internal static class Program
    {
        /// <summary>
        /// Необязательные параметры запуска:
        ///   --dark            тёмная тема (по умолчанию светлая);
        ///   --page=N          открыть раздел N (0 — каталог, 1 — расчёт скидки);
        ///   --date=ГГГГ-ММ-ДД дата расчёта цен вместо сегодняшней (для проверки скидки);
        ///   --instant         без анимаций и заставки (для снимков экрана).
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Любая непредвиденная ошибка показывается сообщением, программа не завершается аварийно.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            int page = 0;
            DateTime calcDate = DateTime.Today;
            Theme.Set(false);
            foreach (string arg in args)
            {
                if (arg == "--instant")
                {
                    Animator.Instant = true;
                }
                else if (arg == "--dark")
                {
                    Theme.Set(true);
                }
                else if (arg.StartsWith("--page=", StringComparison.Ordinal))
                {
                    int.TryParse(arg.Substring(7), out page);
                }
                else if (arg.StartsWith("--date=", StringComparison.Ordinal))
                {
                    DateTime parsed;
                    if (DateTime.TryParseExact(arg.Substring(7), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
                    {
                        calcDate = parsed;
                    }
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm(calcDate) { StartPage = page };
            if (Animator.Instant)
            {
                form.ShowSplash = false;
            }
            Application.Run(form);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show(
                "Произошла непредвиденная ошибка: " + e.Exception.Message + "\n\nПрограмма продолжит работу. Если ошибка повторяется, перезапустите её.",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            MessageBox.Show(
                "Произошла непредвиденная ошибка: " + (ex != null ? ex.Message : "неизвестная ошибка") + ".",
                "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
