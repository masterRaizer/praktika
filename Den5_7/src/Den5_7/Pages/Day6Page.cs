using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Den5_7.Data;
using Den5_7.Logic;
using Den5_7.Models;
using FxKit;

namespace Den5_7.Pages
{
    /// <summary>
    /// День 6. Дополнительные функции (отчёты, экспорт в CSV, журнал действий), тест-кейсы
    /// с автоматической проверкой и документация (инструкции, демонстрационный сценарий).
    /// </summary>
    public sealed class Day6Page : DayPage
    {
        private FxStat statRevenue, statTickets, statOccupancy, statOrders;
        private FxTable tblRevenue, tblOccupancy, tblJournal, tblTests;
        private FxStat statTotal, statPassed, statFailed;
        private FxButton btnRun;
        private List<TestCase> tests = TestCatalog.Create();
        private readonly JournalRepository journal = new JournalRepository();

        public Day6Page() : base("Отчёты", "Журнал действий", "Тест-кейсы", "Документация")
        {
            AddUserLabel();
            var reports = new Guard(BuildReports(), u => Session.CanViewReports(u), "Отчёты доступны администратору и кассиру.");
            reports.Opened += LoadReports;
            AddTab(reports);
            var log = new Guard(BuildJournal(), u => Session.CanViewJournal(u), "Журнал действий доступен только администратору.");
            log.Opened += LoadJournal;
            AddTab(log);
            AddTab(BuildTests());
            AddTab(BuildDocs());
        }

        protected override void OnTabShown(int index)
        {
            if (index == 0 && Session.CanViewReports(Session.Current)) LoadReports();
            if (index == 1 && Session.CanViewJournal(Session.Current)) LoadJournal();
        }

        // ---------- отчёты ----------

        private Control BuildReports()
        {
            statRevenue = new FxStat("Выручка", 4) { Tone = Tone.Warn };
            statTickets = new FxStat("Продано билетов", 1) { Tone = Tone.Accent };
            statOccupancy = new FxStat("Средняя заполняемость", 2) { Tone = Tone.Good };
            statOrders = new FxStat("Заказов", 0) { Tone = Tone.Accent2 };
            tblRevenue = new FxTable { RowHeight = 32 };
            tblRevenue.SetColumns("Фильм");
            tblOccupancy = new FxTable { RowHeight = 32 };
            tblOccupancy.SetColumns("Сеанс");
            var exportRevenue = Ui.Button("Экспорт в CSV", "download");
            exportRevenue.Click += delegate { Ui.ExportCsv(FindForm(), tblRevenue, "Выручка_по_фильмам.csv", "Отчёт о выручке", Win); };
            var exportOccupancy = Ui.Button("Экспорт в CSV", "download");
            exportOccupancy.Click += delegate { Ui.ExportCsv(FindForm(), tblOccupancy, "Заполняемость.csv", "Отчёт о заполняемости", Win); };
            FxBox row = FxBox.H(12);
            row.Fill(Ui.Card("Выручка по фильмам (с учётом возвратов)", "bars", tblRevenue, exportRevenue))
               .Fill(Ui.Card("Заполняемость сеансов", "film", tblOccupancy, exportOccupancy), 1.25f);
            FxBox page = FxBox.V(12);
            page.Add(Ui.Tiles(statRevenue, statTickets, statOccupancy, statOrders), 78).Fill(row);
            return page;
        }

        private void LoadReports()
        {
            try
            {
                Ui.Fill(tblRevenue, Queries.Table(Queries.Revenue));
                tblRevenue.Columns[0].Weight = 1.6f;
                tblRevenue.Columns[0].Align = StringAlignment.Near;
                Ui.Fill(tblOccupancy, Queries.Table(Queries.Occupancy));
                tblOccupancy.Columns[1].Weight = 1.6f;
                statRevenue.Set(Convert.ToDouble(Queries.Scalar("SELECT ISNULL(SUM(Выручка), 0) FROM dbo.ВыручкаПоФильмам;")), 0, " руб.");
                statRevenue.Sub = "оплаты минус возвраты";
                statTickets.Set(Convert.ToDouble(Queries.Scalar("SELECT COUNT(*) FROM dbo.Билеты t JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа WHERE o.КодСтатуса IN (3, 4);")), 0);
                statTickets.Sub = "оплаченные и завершённые";
                statOccupancy.Set(Convert.ToDouble(Queries.Scalar("SELECT CAST(AVG(100.0 * Занято / ВсегоМест) AS FLOAT) FROM dbo.Афиша;")), 1, " %");
                statOccupancy.Sub = "по всем сеансам";
                statOrders.Set(Convert.ToDouble(Queries.Scalar("SELECT COUNT(*) FROM dbo.Заказы;")), 0);
                statOrders.Sub = "во всех статусах";
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки отчёта");
            }
        }

        // ---------- журнал действий ----------

        private Control BuildJournal()
        {
            tblJournal = new FxTable { RowHeight = 30, WrapCells = true };
            tblJournal.SetColumns("Время", "Пользователь", "Действие", "Подробности");
            tblJournal.Columns[0].Weight = 0.8f;
            tblJournal.Columns[2].Align = StringAlignment.Near;
            tblJournal.Columns[3].Weight = 1.6f;
            tblJournal.Columns[3].Align = StringAlignment.Near;
            var refresh = Ui.Button("Обновить", "refresh");
            refresh.Click += delegate { LoadJournal(); };
            var export = Ui.Button("Экспорт в CSV", "download");
            export.Click += delegate { Ui.ExportCsv(FindForm(), tblJournal, "Журнал_действий.csv", "Журнал действий", Win); };
            FxBox buttons = Ui.Row(32, 8);
            buttons.Add(refresh, refresh.Width).Add(export, export.Width);
            buttons.Width = refresh.Width + export.Width + 8;
            return Ui.Card("Журнал действий: вход, изменения справочника, экспорт (последние 200 записей)", "clock", tblJournal, buttons);
        }

        private void LoadJournal()
        {
            try
            {
                tblJournal.Clear();
                foreach (JournalEntry e in journal.Last(200))
                {
                    tblJournal.AddRow(e.Time.ToString("dd.MM.yyyy HH:mm:ss", Ui.Ru), e.User, e.Action, e.Details);
                }
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки журнала");
            }
        }

        // ---------- тест-кейсы ----------

        private Control BuildTests()
        {
            statTotal = new FxStat("Тест-кейсов", 0) { Tone = Tone.Accent2 };
            statPassed = new FxStat("Пройдено", 2) { Tone = Tone.Good };
            statFailed = new FxStat("Не пройдено", 4) { Tone = Tone.Bad };
            statTotal.Set(tests.Count, 0);
            statTotal.Sub = "позитивные, негативные, граничные";
            statPassed.SetText("—");
            statFailed.SetText("—");
            tblTests = new FxTable { RowHeight = 34, WrapCells = true, FontSize = 8.5f };
            tblTests.SetColumns("№", "Раздел", "Проверка", "Вид", "Ожидается", "Получено");
            float[] w = { 0.4f, 0.9f, 2f, 0.8f, 1.6f, 2.2f };
            for (int i = 0; i < w.Length; i++) tblTests.Columns[i].Weight = w[i];
            for (int i = 2; i < 6; i++) tblTests.Columns[i].Align = StringAlignment.Near;
            FillTests();
            btnRun = Ui.Button("Выполнить все тесты", "play", true, 20);
            btnRun.Click += delegate { RunTests(); };
            var export = Ui.Button("Экспорт в CSV", "download");
            export.Click += delegate { Ui.ExportCsv(FindForm(), tblTests, "Тест-кейсы.csv", "Результаты тестирования", Win); };
            FxBox buttons = Ui.Row(32, 8);
            buttons.Add(btnRun, btnRun.Width).Add(export, export.Width);
            buttons.Width = btnRun.Width + export.Width + 8;
            FxBox page = FxBox.V(12);
            page.Add(Ui.Tiles(statTotal, statPassed, statFailed), 78)
                .Fill(Ui.Card("Тест-кейсы: программа выполняет проверки сама и показывает фактический результат", "check", tblTests, buttons));
            return page;
        }

        private void FillTests()
        {
            tblTests.Clear();
            foreach (TestCase t in tests)
            {
                FxRow row = tblTests.AddRow(t.Id, t.Area, t.Title, t.Kind, t.Expected, t.Actual ?? "не выполнялся");
                if (t.Passed == true) row.Tone = RowTone.Good;
                if (t.Passed == false) row.Tone = RowTone.Bad;
            }
        }

        public void RunTests()
        {
            btnRun.Enabled = false;
            tests = TestCatalog.Create();
            FillTests();
            User keep = Session.Current;
            var worker = new Thread(() =>
            {
                foreach (TestCase t in tests) TestCatalog.Execute(t);
                BeginInvoke((Action)delegate
                {
                    FillTests();
                    int passed = tests.Count(t => t.Passed == true);
                    statPassed.Set(passed, 0);
                    statPassed.Sub = "из " + tests.Count;
                    statFailed.Set(tests.Count - passed, 0);
                    statFailed.Sub = tests.Count == passed ? "ошибок нет" : "смотрите строки, выделенные красным";
                    btnRun.Enabled = true;
                    journal.Add(keep == null ? (int?)null : keep.Id, "Запуск тест-кейсов", "пройдено " + passed + " из " + tests.Count);
                    Win.Toast("Тестирование завершено", "Пройдено " + passed + " из " + tests.Count, passed == tests.Count ? Tone.Good : Tone.Bad);
                });
            }) { IsBackground = true };
            worker.Start();
        }

        // ---------- документация ----------

        private Control BuildDocs()
        {
            var user = new FxText { FontSize = 9.2f };
            user.Text =
                "ИНСТРУКЦИЯ ПОЛЬЗОВАТЕЛЯ\n" +
                "Запуск: распакуйте Den5_7_exe.zip и запустите Den5_7.exe. Слева — дни 5, 6 и 7, сверху — разделы дня; кнопка «три полоски» сворачивает панель.\n" +
                "Вход: день 5 → «Вход», введите e-mail и пароль, нажмите «Войти» или Enter.\n" +
                "Справочник: «Фильмы» — поиск по названию и описанию, фильтр по жанру, сортировка, флажок «только в прокате».\n" +
                "Изменение: выберите строку щелчком, нажмите «Изменить» (администратор). «Добавить» открывает пустую карточку, «Удалить» спросит подтверждение.\n" +
                "Отчёты и экспорт: день 6 → «Отчёты», кнопка «Экспорт в CSV» сохраняет таблицу для Excel.\n" +
                "Выход: кнопка «Выйти» справа сверху (день 5), затем кнопка закрытия окна.\n" +
                "\n" +
                "РАЗВЁРТЫВАНИЕ\n" +
                "Требования: Windows 10/11, .NET Framework 4.7.2, SQL Server 2017 или новее (подходит LocalDB из Visual Studio).\n" +
                "База: выполните по порядку 01_create.sql, 02_data.sql (день 4), 04_den5.sql и 05_queries.sql: sqlcmd -S \"(localdb)\\MSSQLLocalDB\" -E -b -f 65001 -i файл.\n" +
                "Строка подключения: файл Den5_7.exe.config, раздел connectionStrings, имя «Киноточка»; для SQL Express замените (localdb)\\MSSQLLocalDB на .\\SQLEXPRESS.\n" +
                "Порты модулей дня 7: appSettings ClientsPort (5101) и RequestsPort (5102) в том же файле.";
            var demo = new FxText { FontSize = 9.2f };
            demo.Text =
                "ДЕМОНСТРАЦИЯ (5–7 МИНУТ)\n" +
                "1 мин: день 5 → «Вход»: неверный пароль (сообщение), затем вход администратором.\n" +
                "1 мин: «Главная» — сводка и афиша; «Фильмы» — поиск «звёзд», фильтр «Драма», сортировка, флажок «только в прокате».\n" +
                "1,5 мин: «Добавить» → пустое название (проверка) → заполнить и сохранить; «Изменить»; «Удалить» фильм с сеансами — понятный отказ.\n" +
                "1 мин: «SQL-запросы» и «Словарь данных».\n" +
                "1 мин: день 6 — отчёты и экспорт в CSV, журнал действий, «Выполнить все тесты» (21 проверка).\n" +
                "1,5 мин: день 7 — «Модули» → «Запустить оба», «Клиенты» → карточка с заявками, «Новая заявка» для клиента 999 (ошибка 422), «Тесты интеграции».\n" +
                "\n" +
                "ИСПРАВЛЕННЫЕ ОШИБКИ\n" +
                "При недоступном сервере выводилось «Ошибка базы данных (код 0)» — ошибки открытия соединения теперь объясняются как отсутствие подключения (найдено тестом T18).\n" +
                "Длинное ФИО в строке вкладок обрезалось — показывается «Фамилия И. О.».\n" +
                "Подсказка с тестовыми учётными записями не помещалась на экране входа — карточка входа увеличена.\n" +
                "Вкладка «Карточка», открытая напрямую, оставалась незаполненной — теперь готовится как новый фильм.\n" +
                "Кнопка «Выйти» завершала сеанс без вопроса — добавлено подтверждение «Выйти из системы?».\n" +
                "День 7: e-mail клиентов обрезался в таблице, а карточка клиента открывалась пустой — столбец расширен, первый клиент показывается сразу.\n" +
                "День 7: после аварийного закрытия окна модули оставались работать и занимали порты — теперь модуль следит за программой (--parent) и завершается вместе с ней; ошибка одного запроса не останавливает модуль.\n" +
                "База: повторный запуск 01_create.sql после 04_den5.sql падал (таблицы дня 5 ссылаются на Пользователи) — скрипт сначала удаляет объекты дня 5; все скрипты можно запускать повторно.\n" +
                "\n" +
                "КОМПЛЕКТНОСТЬ\n" +
                "Исходный код (src), скрипты базы (sql), диаграммы и макеты (docs), тест-кейсы (вкладка «Тест-кейсы» и docs), пояснительная записка (PDF).";
            FxBox body = FxBox.H(12);
            body.Fill(Ui.Card("Инструкции", "info", user)).Fill(Ui.Card("Сценарий защиты и исправления", "bulb", demo));
            return body;
        }
    }
}
