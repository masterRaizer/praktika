using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using Kinotochka.Data;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Tests
{
    /// <summary>
    /// Тестирование приложения «Киноточка» (день 6, задание 4) без сторонних библиотек.
    /// Проверяются бизнес-логика и работа с настоящей базой данных через те же классы, что вызывают формы.
    ///
    /// Запуск: Kinotochka.Tests.exe [--connection="строка подключения"] [--report=папка]
    /// Без --connection берётся строка из Kinotochka.Tests.exe.config. Код возврата 0 — все проверки пройдены.
    /// Результаты: таблица в консоли и файлы «Результаты_тестирования.md» и «.csv» (на русском языке).
    /// Тестовые записи, созданные в ходе проверки, в конце удаляются.
    /// </summary>
    internal static class Program
    {
        private sealed class Case
        {
            public int No;
            public string Group, Name, Input, Expected, Actual;
            public bool Passed;
        }

        private sealed class Outcome
        {
            public bool Ok;
            public string Text;
            public Outcome(bool ok, string text) { Ok = ok; Text = text; }
        }

        private static readonly List<Case> cases = new List<Case>();
        private static string group = "";
        private static readonly string Suffix = DateTime.Now.ToString("HHmmss");
        private static readonly List<Action> cleanup = new List<Action>();

        private static int Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            string reportDir = ".";
            foreach (string a in args)
            {
                if (a.StartsWith("--connection=", StringComparison.Ordinal)) Db.UseConnectionString(a.Substring(13));
                else if (a.StartsWith("--report=", StringComparison.Ordinal)) reportDir = a.Substring(9);
            }

            Console.WriteLine("Тестирование приложения «Киноточка»");
            Console.WriteLine("Сервер: " + Db.ServerName);
            Console.WriteLine();

            UnitTests();
            bool dbReady = DatabaseAvailable();
            if (dbReady)
            {
                try { DatabaseTests(); }
                finally { Cleanup(); }
            }
            UnavailableDatabaseTest();

            int failed = cases.Count(c => !c.Passed);
            Console.WriteLine();
            Console.WriteLine("Итого проверок: " + cases.Count + ", пройдено: " + (cases.Count - failed) + ", не пройдено: " + failed +
                              (dbReady ? "" : " (проверки с базой данных не выполнялись: нет подключения)"));
            WriteReports(reportDir, dbReady);
            return failed == 0 && dbReady ? 0 : 1;
        }

        // =====================================================================
        //  Механика проверок
        // =====================================================================

        private static void Check(string name, string input, string expected, Func<Outcome> body)
        {
            var c = new Case { No = cases.Count + 1, Group = group, Name = name, Input = input, Expected = expected };
            try
            {
                Outcome o = body();
                c.Passed = o.Ok;
                c.Actual = o.Text;
            }
            catch (Exception ex)
            {
                c.Passed = false;
                c.Actual = "непредвиденная ошибка: " + ex.GetType().Name + ": " + ex.Message;
            }
            cases.Add(c);
            Console.WriteLine((c.Passed ? "[пройден]    " : "[НЕ ПРОЙДЕН] ") + c.No + ". " + name + " — " + c.Actual);
        }

        private static Outcome Ok(string text) { return new Outcome(true, text); }
        private static Outcome Fail(string text) { return new Outcome(false, text); }

        /// <summary>Ожидается отказ: AppException нужного вида. Фактический результат — текст сообщения пользователю.</summary>
        private static Outcome Rejected(Action action, ErrorKind kind)
        {
            try
            {
                action();
                return Fail("действие выполнено, отказа нет");
            }
            catch (AppException ex)
            {
                string text = "отказ: «" + ex.Message.Split('\n')[0] + "»";
                return ex.Kind == kind ? Ok(text) : Fail(text + " (вид ошибки " + ex.Kind + ", ожидался " + kind + ")");
            }
        }

        // =====================================================================
        //  Проверки без базы данных
        // =====================================================================

        private static void UnitTests()
        {
            group = "Хеширование паролей";
            Check("Хеш пароля и его проверка", "пароль Kino2026", "Verify = true, пароль в хеше не виден", () =>
            {
                string h = PasswordHasher.Hash("Kino2026");
                return h.StartsWith("PBKDF2$100000$") && !h.Contains("Kino2026") && PasswordHasher.Verify("Kino2026", h)
                    ? Ok("хеш вида PBKDF2$100000$…, проверка пройдена") : Fail("неверный хеш: " + h);
            });
            Check("Неверный пароль не подходит к хешу", "Kino2026 / kino2026", "Verify = false", () =>
                !PasswordHasher.Verify("kino2026", PasswordHasher.Hash("Kino2026")) ? Ok("Verify = false") : Fail("Verify = true"));
            Check("Соль: у одинаковых паролей разные хеши", "дважды 123456", "хеши различаются", () =>
                PasswordHasher.Hash("123456") != PasswordHasher.Hash("123456") ? Ok("хеши различаются") : Fail("хеши совпали"));
            Check("Совместимость с хешами из базы (день 4)", "123456, хеш admin@kinotochka.ru", "Verify = true", () =>
                PasswordHasher.Verify("123456", "PBKDF2$100000$XaIgAfe2jWh1oueO7gQR3w==$mUJFyxMEAoSb+Rj+Ca/HyMUzckTRJ6aq4EybQmaYdaQ=")
                    ? Ok("Verify = true") : Fail("Verify = false"));
            Check("Испорченная строка хеша", "«abc»", "Verify = false без исключения", () =>
                !PasswordHasher.Verify("123456", "abc") ? Ok("Verify = false") : Fail("Verify = true"));

            group = "Проверка ввода";
            Check("Пустая электронная почта", "«»", "отказ: заполните поле", () => Rejected(() => Validator.Email("email", ""), ErrorKind.Validation));
            Check("Почта без @", "ivanov.example.com", "отказ: неверный формат", () => Rejected(() => Validator.Email("email", "ivanov.example.com"), ErrorKind.Validation));
            Check("Корректная почта приводится к нижнему регистру", " Ivanov@Example.COM ", "ivanov@example.com", () =>
            {
                string v = Validator.Email("email", " Ivanov@Example.COM ");
                return v == "ivanov@example.com" ? Ok(v) : Fail(v);
            });
            Check("ФИО из одного слова", "Иванов", "отказ", () => Rejected(() => Validator.FullName("f", "Иванов"), ErrorKind.Validation));
            Check("ФИО с цифрами", "Иванов 1ван", "отказ", () => Rejected(() => Validator.FullName("f", "Иванов 1ван"), ErrorKind.Validation));
            Check("Телефон из 5 цифр", "12345", "отказ", () => Rejected(() => Validator.Phone("p", "12345"), ErrorKind.Validation));
            Check("Короткий пароль", "12a", "отказ: не короче 6 символов", () => Rejected(() => Validator.Password("p", "12a"), ErrorKind.Validation));
            Check("Пароль без букв", "123456789", "отказ: нужна буква и цифра", () => Rejected(() => Validator.Password("p", "123456789"), ErrorKind.Validation));
            Check("Длительность 0 минут", "0", "отказ: от 1 до 600", () => Rejected(() => Validator.IntRange("d", "Длительность", "0", 1, 600), ErrorKind.Validation));
            Check("Длительность — не число", "сто", "отказ: целое число", () => Rejected(() => Validator.IntRange("d", "Длительность", "сто", 1, 600), ErrorKind.Validation));
            Check("Отрицательная цена", "-100", "отказ: больше нуля", () => Rejected(() => Validator.Price("p", "-100"), ErrorKind.Validation));
            Check("Цена с запятой", "450,50", "450,50", () =>
            {
                decimal p = Validator.Price("p", "450,50");
                return p == 450.50m ? Ok("принято: 450,50") : Fail(p.ToString());
            });
            Check("Неверный формат даты", "2026-11-25 19:30", "отказ: дд.мм.гггг чч:мм", () => Rejected(() => Validator.DateTimeValue("s", "2026-11-25 19:30"), ErrorKind.Validation));
            Check("Несуществующая дата", "31.02.2027 19:00", "отказ", () => Rejected(() => Validator.DateTimeValue("s", "31.02.2027 19:00"), ErrorKind.Validation));
            Check("Возрастной рейтинг не из списка", "21+", "отказ", () => Rejected(() => Validator.OneOf("r", "Возраст", "21+", Validator.AgeRatings), ErrorKind.Validation));

            group = "Обработка исключений";
            Check("Пароль скрывается в журнале ошибок", "строка с Password=Secret1", "Password=***", () =>
            {
                string m = ErrorLog.Mask("Data Source=srv;User ID=sa;Password=Secret1;");
                return m.Contains("Password=***") && !m.Contains("Secret1") ? Ok(m) : Fail(m);
            });
        }

        // =====================================================================
        //  Проверки с базой данных
        // =====================================================================

        private static bool DatabaseAvailable()
        {
            group = "Подключение";
            bool ok = false;
            Check("Открытие и закрытие соединения", Db.ServerName, "соединение открыто, данные прочитаны, соединение закрыто", () =>
            {
                string info = Db.TestConnection();
                using (var c = new SqlConnection(Db.ConnectionString))
                {
                    c.Open();
                    c.Close();
                    ok = c.State == System.Data.ConnectionState.Closed;
                }
                return ok ? Ok("подключено: " + info + "; состояние после закрытия — Closed") : Fail("соединение не закрыто");
            });
            return ok;
        }

        private static User Login(string email)
        {
            return new AuthService().Login(email, "123456");
        }

        private static void DatabaseTests()
        {
            User admin = Login("admin@kinotochka.ru");
            User viewer = Login("ivanov@example.com");

            group = "Авторизация";
            Check("Успешная авторизация администратора", "admin@kinotochka.ru / 123456", "вход выполнен, роль «Администратор»", () =>
            {
                User u = new AuthService().Login("admin@kinotochka.ru", "123456");
                return u.RoleCode == Roles.Admin && u.PasswordHash == null ? Ok("вход выполнен: " + u.FullName + ", " + u.RoleName) : Fail(u.RoleName);
            });
            Check("Успешная авторизация кассира", "cashier1@kinotochka.ru / 123456", "роль «Кассир»", () =>
            {
                User u = new AuthService().Login("cashier1@kinotochka.ru", "123456");
                return u.RoleCode == Roles.Cashier ? Ok("вход выполнен: " + u.RoleName) : Fail(u.RoleName);
            });
            Check("Неверный пароль", "admin@kinotochka.ru / 654321", "отказ: неверная почта или пароль", () =>
                Rejected(() => new AuthService().Login("admin@kinotochka.ru", "654321"), ErrorKind.Auth));
            Check("Несуществующая почта", "nobody@example.com / 123456", "тот же отказ, что и при неверном пароле", () =>
                Rejected(() => new AuthService().Login("nobody@example.com", "123456"), ErrorKind.Auth));
            Check("Заблокированный пользователь", "morozov@example.com / 123456", "отказ: учётная запись заблокирована", () =>
                Rejected(() => new AuthService().Login("morozov@example.com", "123456"), ErrorKind.Auth));
            Check("Пустые поля входа", "«» / «»", "отказ: заполните поле", () =>
                Rejected(() => new AuthService().Login("", ""), ErrorKind.Validation));
            Check("Три неверные попытки подряд", "3 × неверный пароль, затем верный", "вход приостановлен на 30 с", () =>
            {
                DateTime now = new DateTime(2026, 11, 21, 12, 0, 0);
                var auth = new AuthService(() => now);
                for (int i = 0; i < 3; i++)
                {
                    try { auth.Login("admin@kinotochka.ru", "000000"); } catch (AppException) { }
                }
                Outcome locked = Rejected(() => auth.Login("admin@kinotochka.ru", "123456"), ErrorKind.Auth);
                now = now.AddSeconds(31);
                User u = auth.Login("admin@kinotochka.ru", "123456");
                return locked.Ok && u != null ? Ok("вход заблокирован на 30 с, после паузы — успешен") : Fail(locked.Text);
            });

            group = "Регистрация";
            string mail = "test" + Suffix + "@example.com";
            Check("Регистрация нового зрителя", "Тестов Тест, " + mail + ", пароль Kino2026", "запись добавлена, роль «Зритель», в базе хеш", () =>
            {
                User u = new AuthService().Register("Тестов Тест", mail, "+7 900 000-00-00", "Kino2026", "Kino2026");
                cleanup.Add(() => Db.Execute("DELETE FROM dbo.Пользователи WHERE КодПользователя = @Код;", "очистка", Db.Int("@Код", u.Id)));
                object hash = Db.Scalar("SELECT ХешПароля FROM dbo.Пользователи WHERE КодПользователя = @Код;", "проверка", Db.Int("@Код", u.Id));
                string h = Convert.ToString(hash);
                return h.StartsWith("PBKDF2$") && !h.Contains("Kino2026") ? Ok("код " + u.Id + ", в столбце ХешПароля: " + h.Substring(0, 22) + "…") : Fail(h);
            });
            Check("Вход под новой учётной записью", mail + " / Kino2026", "вход выполнен", () =>
            {
                User u = new AuthService().Login(mail, "Kino2026");
                return u.RoleCode == Roles.Viewer ? Ok("вход выполнен, роль " + u.RoleName) : Fail(u.RoleName);
            });
            Check("Повторная регистрация с той же почтой", mail, "отказ: почта уже зарегистрирована", () =>
                Rejected(() => new AuthService().Register("Тестов Тест", mail, null, "Kino2026", "Kino2026"), ErrorKind.Validation));
            Check("Пароли не совпадают", "Kino2026 / Kino2027", "отказ", () =>
                Rejected(() => new AuthService().Register("Тестов Тест", "x" + mail, null, "Kino2026", "Kino2027"), ErrorKind.Validation));

            group = "Фильмы";
            var films = new FilmService();
            int genreId = new LookupRepository().Genres()[0].Id;
            string title = "Тестовый фильм " + Suffix;
            int filmId = 0;
            Check("Добавление фильма", title + ", 95 мин, 12+", "запись добавлена в таблицу Фильмы", () =>
            {
                Film f = films.Validate(0, title, genreId, "95", "12+", "проверка", true);
                filmId = films.Save(admin, f);
                cleanup.Add(() => Db.Execute("DELETE FROM dbo.Фильмы WHERE КодФильма = @Код;", "очистка", Db.Int("@Код", filmId)));
                return filmId > 0 ? Ok("добавлен, КодФильма = " + filmId) : Fail("код не получен");
            });
            Check("Поиск фильма по части названия", "«" + Suffix + "»", "найден 1 фильм", () =>
            {
                List<Film> found = films.Search(Suffix, 0, false);
                return found.Count == 1 && found[0].Id == filmId ? Ok("найдено: 1 — " + found[0].Title) : Fail("найдено: " + found.Count);
            });
            Check("Поиск по жанру и фрагменту «звёзд»", "«звёзд», все жанры", "найден «Звёздный маршрут»", () =>
            {
                List<Film> found = films.Search("звёзд", 0, false);
                return found.Any(x => x.Title == "Звёздный маршрут") ? Ok("найдено: " + string.Join(", ", found.Select(x => x.Title))) : Fail("не найден");
            });
            Check("Изменение фильма", "длительность 95 → 110", "в базе 110", () =>
            {
                Film f = films.Validate(filmId, title, genreId, "110", "16+", "изменено", true);
                films.Save(admin, f);
                int d = films.Search(Suffix, 0, false)[0].DurationMin;
                return d == 110 ? Ok("ДлительностьМин = 110, рейтинг 16+") : Fail("ДлительностьМин = " + d);
            });
            Check("Пустое название фильма", "«»", "отказ: заполните поле", () =>
                Rejected(() => films.Validate(0, "  ", genreId, "90", "12+", null, true), ErrorKind.Validation));
            Check("Длительность 700 минут", "700", "отказ: от 1 до 600", () =>
                Rejected(() => films.Validate(0, "Х", genreId, "700", "12+", null, true), ErrorKind.Validation));
            Check("Добавление фильма зрителем", "роль «Зритель»", "отказ: недостаточно прав", () =>
                Rejected(() => films.Save(viewer, films.Validate(0, "Х", genreId, "90", "12+", null, true)), ErrorKind.Auth));
            Check("SQL-инъекция в поиске", "'; DROP TABLE dbo.Фильмы; --", "0 записей, таблица на месте", () =>
            {
                int found = films.Search("'; DROP TABLE dbo.Фильмы; --", 0, false).Count;
                int total = films.Search("", 0, false).Count;
                return found == 0 && total > 0 ? Ok("найдено 0, в таблице Фильмы " + total + " записей") : Fail("найдено " + found);
            });

            group = "Сеансы";
            var screenings = new ScreeningService();
            int hallId = new LookupRepository().Halls()[0].Id;
            DateTime start = DateTime.Today.AddDays(400).AddHours(10);
            int screeningId = 0;
            Check("Добавление сеанса", Ui(start) + ", зал 1, 2D, 350", "запись добавлена в таблицу Сеансы", () =>
            {
                Screening s = screenings.Validate(0, filmId, hallId, Ui(start), "2D", "350", true);
                screeningId = screenings.Save(admin, s);
                cleanup.Insert(0, () => Db.Execute("DELETE FROM dbo.Сеансы WHERE КодСеанса = @Код;", "очистка", Db.Int("@Код", screeningId)));
                return screeningId > 0 ? Ok("добавлен, КодСеанса = " + screeningId) : Fail("код не получен");
            });
            Check("Сеанс в занятый зал через 30 минут", Ui(start.AddMinutes(30)) + ", тот же зал", "отказ: зал занят", () =>
                Rejected(() => screenings.Save(admin, screenings.Validate(0, filmId, hallId, Ui(start.AddMinutes(30)), "2D", "350", true)), ErrorKind.Validation));
            Check("Сеанс в прошедшее время", "01.01.2025 10:00", "отказ", () =>
                Rejected(() => screenings.Validate(0, filmId, hallId, "01.01.2025 10:00", "2D", "350", true), ErrorKind.Validation));
            Check("Цена сеанса 0", "0", "отказ: больше нуля", () =>
                Rejected(() => screenings.Validate(0, filmId, hallId, Ui(start), "2D", "0", true), ErrorKind.Validation));

            group = "Удаление и целостность";
            Check("Удаление фильма, у которого есть сеанс", "тестовый фильм + 1 сеанс", "удаление запрещено, показана причина", () =>
            {
                string blocker = films.DeleteBlocker(filmId);
                return blocker != null ? Ok("«" + blocker + "»") : Fail("проверка связей не сработала");
            });
            Check("Удаление фильма в обход проверки", "DELETE фильма с сеансом", "база отклоняет (внешний ключ 547), понятное сообщение", () =>
                Rejected(() => films.Delete(admin, filmId), ErrorKind.Constraint));
            Check("Удаление сеанса без заказов", "КодСеанса тестового сеанса", "сеанс удалён", () =>
            {
                if (screenings.DeleteBlocker(screeningId) != null) return Fail("неожиданный запрет");
                screenings.Delete(admin, screeningId);
                return screenings.Search(0, filmId).Count == 0 ? Ok("удалён, сеансов у фильма: 0") : Fail("не удалён");
            });
            Check("Удаление фильма без связей", title, "фильм удалён", () =>
            {
                if (films.DeleteBlocker(filmId) != null) return Fail("неожиданный запрет");
                films.Delete(admin, filmId);
                return films.Search(Suffix, 0, false).Count == 0 ? Ok("удалён, поиск возвращает 0 записей") : Fail("не удалён");
            });
            Check("Удаление сеанса с заказами", "сеанс № 5", "удаление запрещено", () =>
            {
                string blocker = screenings.DeleteBlocker(5);
                return blocker != null ? Ok("«" + blocker + "»") : Fail("запрета нет");
            });
            Check("Удаление заказа с платежами", "заказ № 5", "удаление запрещено", () =>
            {
                Order o = new OrderService().Search(admin, "5", 0).First(x => x.Id == 5);
                string blocker = new OrderService().DeleteBlocker(o);
                return blocker != null ? Ok("«" + blocker + "»") : Fail("запрета нет");
            });
            Check("Повторная продажа занятого места", "новый заказ на сеанс заказа № 5 и уже проданное место", "триггер отклоняет (ошибка 50002)", () =>
            {
                Outcome o = Rejected(() => Db.Execute(
                    "BEGIN TRAN; " +
                    "DECLARE @Сеанс INT = (SELECT КодСеанса FROM dbo.Заказы WHERE КодЗаказа = 5); " +
                    "INSERT INTO dbo.Заказы (КодПользователя, КодСеанса, КодСтатуса, БроньДо) VALUES (4, @Сеанс, 1, DATEADD(HOUR, 1, SYSDATETIME())); " +
                    "DECLARE @Заказ INT = SCOPE_IDENTITY(); " +
                    "INSERT INTO dbo.Билеты (КодЗаказа, КодМеста, Цена) SELECT TOP 1 @Заказ, КодМеста, 1 FROM dbo.Билеты WHERE КодЗаказа = 5; " +
                    "ROLLBACK;", "продажа билета"), ErrorKind.Constraint);
                return o.Ok && o.Text.Contains("уже продано") ? o : Fail(o.Text);
            });

            group = "Заказы и права";
            Check("Зритель видит только свои заказы", "ivanov@example.com", "все заказы — Иванова", () =>
            {
                List<Order> list = new OrderService().Search(viewer, "", 0);
                return list.Count > 0 && list.All(o => o.Customer == viewer.FullName) ? Ok("заказов: " + list.Count + ", все принадлежат " + viewer.FullName) : Fail("чужие заказы в списке");
            });
            Check("Администратор видит все заказы", "admin@kinotochka.ru", "12 заказов", () =>
            {
                int n = new OrderService().Search(admin, "", 0).Count;
                return n >= 12 ? Ok("заказов: " + n) : Fail("заказов: " + n);
            });
            Check("Смена статуса заказа зрителем", "роль «Зритель»", "отказ: недостаточно прав", () =>
                Rejected(() => new OrderService().ChangeStatus(viewer, new Order { Id = 10, StatusId = 2 }, 3), ErrorKind.Auth));
            Check("Удаление своей учётной записи администратором", "admin@kinotochka.ru", "запрещено", () =>
            {
                string blocker = new UserService().DeleteBlocker(admin, admin.Id);
                return blocker != null ? Ok("«" + blocker + "»") : Fail("запрета нет");
            });
        }

        private static string Ui(DateTime d) { return d.ToString("dd.MM.yyyy HH:mm"); }

        private static void Cleanup()
        {
            foreach (Action a in cleanup)
            {
                try { a(); } catch (Exception) { }
            }
        }

        // =====================================================================
        //  Недоступность базы данных
        // =====================================================================

        private static void UnavailableDatabaseTest()
        {
            group = "Недоступность базы";
            string saved = Db.ConnectionString;
            Db.UseConnectionString("Data Source=127.0.0.1,1;Initial Catalog=Киноточка;User ID=test;Password=Secret1;Connect Timeout=2");
            try
            {
                Check("Сервер базы данных недоступен", "сервер 127.0.0.1, порт 1", "понятное сообщение «Нет подключения…», без пароля", () =>
                {
                    try
                    {
                        new FilmService().Search("", 0, false);
                        return Fail("данные получены");
                    }
                    catch (AppException ex)
                    {
                        bool clean = !ex.Message.Contains("Secret1") && (ex.Technical == null || !ex.Technical.Contains("Secret1"));
                        return ex.Kind == ErrorKind.Connection && clean
                            ? Ok("«" + ex.Message.Split('\n')[0] + "»; пароль в сообщении и журнале не виден")
                            : Fail(ex.Kind + ": " + ex.Message);
                    }
                });
                Check("Вход при недоступной базе", "admin@kinotochka.ru / 123456", "отказ: нет подключения, программа не падает", () =>
                    Rejected(() => new AuthService().Login("admin@kinotochka.ru", "123456"), ErrorKind.Connection));
            }
            finally
            {
                Db.UseConnectionString(saved);
            }
        }

        // =====================================================================
        //  Отчёт
        // =====================================================================

        private static string Cell(string s)
        {
            return (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ");
        }

        private static void WriteReports(string dir, bool dbReady)
        {
            Directory.CreateDirectory(dir);
            int failed = cases.Count(c => !c.Passed);
            var md = new StringBuilder();
            md.AppendLine("# Результаты тестирования приложения «Киноточка»");
            md.AppendLine();
            md.AppendLine("Дата: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") + ". Сервер базы данных: " + Db.ServerName + ".");
            md.AppendLine("Проверок: " + cases.Count + ", пройдено: " + (cases.Count - failed) + ", не пройдено: " + failed + "." +
                          (dbReady ? "" : " Проверки с базой данных не выполнялись: нет подключения."));
            md.AppendLine();
            md.AppendLine("| № | Раздел | Что проверяется | Входные данные | Ожидаемый результат | Фактический результат | Итог |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (Case c in cases)
                md.AppendLine("| " + c.No + " | " + Cell(c.Group) + " | " + Cell(c.Name) + " | " + Cell(c.Input) + " | " + Cell(c.Expected) + " | " +
                              Cell(c.Actual) + " | " + (c.Passed ? "Пройден" : "Не пройден") + " |");
            File.WriteAllText(Path.Combine(dir, "Результаты_тестирования.md"), md.ToString(), new UTF8Encoding(false));

            var csv = new StringBuilder();
            csv.AppendLine("№;Раздел;Что проверяется;Входные данные;Ожидаемый результат;Фактический результат;Итог");
            foreach (Case c in cases)
                csv.AppendLine(string.Join(";", new[] { c.No.ToString(), c.Group, c.Name, c.Input, c.Expected, c.Actual, c.Passed ? "Пройден" : "Не пройден" }
                    .Select(v => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"")));
            File.WriteAllText(Path.Combine(dir, "Результаты_тестирования.csv"), csv.ToString(), new UTF8Encoding(true));
            Console.WriteLine("Таблица результатов: " + Path.GetFullPath(Path.Combine(dir, "Результаты_тестирования.md")));
        }
    }
}
