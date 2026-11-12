using System;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// Необязательные параметры запуска (для автоматических снимков экрана):
        ///   --page=N — открыть раздел N (0–3);  --light — светлая тема;  --instant — без анимаций и заставки.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            int page = 0;
            foreach (string a in args)
            {
                if (a == "--instant") Animator.Instant = true;
                else if (a == "--light") Theme.Set(false);
                else if (a.StartsWith("--page=")) int.TryParse(a.Substring(7), out page);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm { StartPage = page };
            if (Animator.Instant) form.ShowSplash = false;
            Application.Run(form);
        }
    }
}
