using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Den5_7.Integration;
using Den5_7.Logic;
using FxKit;

namespace Den5_7
{
    internal static class Program
    {
        /// <summary>
        /// Параметры запуска (все необязательные):
        ///   --module=clients|requests --port=N --peer=URL [--parent=PID]   запуск модуля дня 7 без окна
        ///                                 (так модули запускает сама программа; с --parent модуль завершается вместе с окном);
        ///   --light                       светлая тема;
        ///   --page=N --tab=N              открыть день (0–2) и вкладку;
        ///   --login=e-mail                войти сразу (пароль тестовых учётных записей — 123456);
        ///   --instant                     без анимаций и заставки (для снимков экрана);
        ///   --run-tests=файл.md           выполнить тест-кейсы дня 6 и записать отчёт, без окна;
        ///   --run-integration=файл.md     запустить модули, выполнить тесты дня 7 и записать отчёт, без окна.
        /// </summary>
        [STAThread]
        private static int Main(string[] args)
        {
            Dictionary<string, string> a = Parse(args);
            string module;
            if (a.TryGetValue("module", out module))
            {
                int port;
                int.TryParse(Get(a, "port"), out port);
                int parent;
                int.TryParse(Get(a, "parent"), out parent);
                return ModuleServer.Run(module, port, Get(a, "peer") ?? "", parent);
            }
            if (a.ContainsKey("run-tests")) return RunTests(a["run-tests"]);
            if (a.ContainsKey("run-integration")) return RunIntegration(a["run-integration"]);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                MessageBox.Show("Произошла непредвиденная ошибка: " + e.Exception.Message + "\n\nПрограмма продолжит работу.",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show("Произошла непредвиденная ошибка: " + (ex != null ? ex.Message : "неизвестная ошибка") + ".",
                    "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            if (a.ContainsKey("instant")) Animator.Instant = true;
            if (a.ContainsKey("light")) Theme.Set(false);
            int page, tab;
            int.TryParse(Get(a, "page"), out page);
            int.TryParse(Get(a, "tab"), out tab);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var form = new MainForm { StartPage = page, StartTab = tab };
            if (Animator.Instant) form.ShowSplash = false;
            string login = Get(a, "login");
            if (login != null)
            {
                form.Load += delegate
                {
                    form.Day5.LoginAs(login, "123456");
                    if (page == 0) form.Day5.ShowTab(tab);
                };
            }
            if (a.ContainsKey("collapsed")) form.Load += delegate { form.ToggleSidebar(); };
            if (a.ContainsKey("run")) form.Shown += delegate
            {
                if (page == 1) form.Day6.RunTests();
                if (page == 2) form.Day7.RunIntegration();
            };
            Application.Run(form);
            return 0;
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string arg in args)
            {
                if (!arg.StartsWith("--", StringComparison.Ordinal)) continue;
                int eq = arg.IndexOf('=');
                d[eq > 0 ? arg.Substring(2, eq - 2) : arg.Substring(2)] = eq > 0 ? arg.Substring(eq + 1) : "";
            }
            return d;
        }

        private static string Get(Dictionary<string, string> d, string key)
        {
            string v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        private static string Report(string title, List<TestCase> list)
        {
            var sb = new StringBuilder();
            int passed = list.Count(t => t.Passed == true);
            sb.AppendLine("# " + title);
            sb.AppendLine();
            sb.AppendLine("Выполнено " + DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("ru-RU")) +
                          ". Пройдено: **" + passed + " из " + list.Count + "**.");
            sb.AppendLine();
            sb.AppendLine("| № | Раздел | Проверка | Вид | Ожидается | Получено | Итог |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (TestCase t in list)
            {
                sb.AppendLine("| " + string.Join(" | ", new[] { t.Id, t.Area, t.Title, t.Kind ?? "", t.Expected, t.Actual ?? "", t.Passed == true ? "пройден" : "НЕ ПРОЙДЕН" }
                    .Select(s => (s ?? "").Replace("|", "\\|").Replace("\n", " "))) + " |");
            }
            return sb.ToString();
        }

        private static int RunTests(string path)
        {
            List<TestCase> list = TestCatalog.Create();
            foreach (TestCase t in list) TestCatalog.Execute(t);
            File.WriteAllText(path, Report("Тест-кейсы дня 6", list), new UTF8Encoding(false));
            return list.All(t => t.Passed == true) ? 0 : 1;
        }

        private static int RunIntegration(string path)
        {
            var clients = new ModuleHost("clients", "Клиенты", ModuleHost.ConfigPort("ClientsPort", 5101), "http://127.0.0.1:" + ModuleHost.ConfigPort("RequestsPort", 5102));
            var requests = new ModuleHost("requests", "Заявки", ModuleHost.ConfigPort("RequestsPort", 5102), "http://127.0.0.1:" + ModuleHost.ConfigPort("ClientsPort", 5101));
            try
            {
                clients.Start();
                requests.Start();
                for (int i = 0; i < 60 && (clients.Health().Status != 200 || requests.Health().Status != 200); i++) Thread.Sleep(200);
                List<TestCase> list = IntegrationTests.Create(clients, requests);
                foreach (TestCase t in list) TestCatalog.Execute(t);
                File.WriteAllText(path, Report("Тесты интеграции дня 7", list), new UTF8Encoding(false));
                return list.All(t => t.Passed == true) ? 0 : 1;
            }
            finally
            {
                clients.Stop();
                requests.Stop();
            }
        }
    }
}
