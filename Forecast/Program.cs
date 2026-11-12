using System;
using System.Threading;
using System.Windows.Forms;
using FxKit;

namespace Forecast
{
    internal static class Program
    {
        /// <summary>
        /// Необязательные параметры запуска (нужны для автоматических снимков экрана):
        ///   --page=N   открыть раздел N (0–7);   --tab=N   внутренняя вкладка разделов 2.1 и 2.2;
        ///   --light    светлая тема;             --instant  без анимаций и заставки.
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Любая необработанная ошибка показывается сообщением, программа не завершается аварийно.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                MessageBox.Show("Непредвиденная ошибка: " + e.ExceptionObject, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);

            int page = 0, tab = -1;
            foreach (string a in args)
            {
                if (a == "--instant") Animator.Instant = true;
                else if (a == "--light") Theme.Set(false);
                else if (a.StartsWith("--page=")) int.TryParse(a.Substring(7), out page);
                else if (a.StartsWith("--tab=")) int.TryParse(a.Substring(6), out tab);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm { StartPage = page };
            if (Animator.Instant) form.ShowSplash = false;
            if (tab >= 0) form.Load += delegate { form.ShowInnerTab(tab); };
            Application.Run(form);
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("Ошибка: " + e.Exception.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
