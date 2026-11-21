using System;
using System.Threading;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Data;
using Kinotochka.Forms;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka
{
    internal static class Program
    {
        /// <summary>
        /// Порядок работы: форма входа → главное окно → (кнопка «Выйти») → снова форма входа.
        /// Необязательные параметры запуска:
        ///   --dark                 тёмная тема (по умолчанию светлая);
        ///   --email=адрес          подставить адрес в форму входа;
        ///   --page=N               раздел главного окна после входа (0 — главная, 1 — фильмы, 2 — сеансы, 3 — заказы, 4 — пользователи);
        ///   --connection="строка"  своя строка подключения (вместо App.config и окна «Подключение»);
        ///   --instant              без анимаций (для снимков экрана).
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            // Любая непредвиденная ошибка показывается сообщением, программа не завершается аварийно.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            string email = "";
            int page = 0;
            Theme.Set(false);
            foreach (string arg in args)
            {
                if (arg == "--instant") Animator.Instant = true;
                else if (arg == "--dark") Theme.Set(true);
                else if (arg.StartsWith("--email=", StringComparison.Ordinal)) email = arg.Substring(8);
                else if (arg.StartsWith("--page=", StringComparison.Ordinal)) int.TryParse(arg.Substring(7), out page);
                else if (arg.StartsWith("--connection=", StringComparison.Ordinal)) Db.UseConnectionString(arg.Substring(13));
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var auth = new AuthService();
            while (true)
            {
                User user;
                using (var login = new LoginForm(auth, email))
                {
                    if (login.ShowDialog() != DialogResult.OK) return;
                    user = login.LoggedUser;
                    email = user.Email;
                }
                using (var main = new MainForm(user) { StartPage = page })
                {
                    Application.Run(main);
                    if (!main.LoggedOut) return;
                }
                page = 0;
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            AppException a = ErrorTranslator.Translate(e.Exception, "работа программы");
            MessageBox.Show(a.Message + "\n\nПрограмма продолжит работу. Подробности записаны в журнал:\n" + ErrorLog.FilePath,
                "Киноточка — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            AppException a = ErrorTranslator.Translate(ex ?? new Exception("неизвестная ошибка"), "работа программы");
            MessageBox.Show(a.Message, "Киноточка — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
