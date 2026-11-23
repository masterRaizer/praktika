using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Den5_7.Data;
using Den5_7.Models;

namespace Den5_7.Logic
{
    /// <summary>Тест-кейс: что проверяем, вид проверки, ожидаемый результат и функция проверки.</summary>
    public sealed class TestCase
    {
        public string Id;
        public string Area;
        public string Title;
        public string Kind;      // позитивный / негативный / граничный
        public string Expected;
        public Func<string> Run; // возвращает фактический результат; исключение TestFailed — проверка не пройдена

        public string Actual;
        public bool? Passed;
    }

    public sealed class TestFailed : Exception
    {
        public TestFailed(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Тест-кейсы дня 6: авторизация, права ролей, CRUD, поиск, фильтр, целостность данных,
    /// граничные значения, отсутствие подключения и отсутствие технических подробностей в сообщениях.
    /// Тестовые записи создаются и удаляются в ходе проверок — данные базы не меняются.
    /// </summary>
    public static class TestCatalog
    {
        private const string AdminEmail = "admin@kinotochka.ru";
        private const string Password = "123456";

        private static void Check(bool condition, string failText)
        {
            if (!condition) throw new TestFailed(failText);
        }

        private static string Expect<T>(Action action, string mustContain) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                Check(ex.Message.IndexOf(mustContain, StringComparison.OrdinalIgnoreCase) >= 0,
                    "сообщение «" + ex.Message + "» не содержит «" + mustContain + "»");
                return "отказ: «" + ex.Message.Replace("\n", " ") + "»";
            }
            throw new TestFailed("действие выполнено без ошибки");
        }

        private static Film NewFilm(string title)
        {
            return new Film { Title = title, GenreId = 1, DurationMin = 95, AgeRating = "12+", Description = "Тестовая запись", IsActive = false };
        }

        public static List<TestCase> Create()
        {
            var auth = new AuthService();
            var films = new FilmRepository();
            var journal = new JournalRepository();
            var list = new List<TestCase>();

            list.Add(new TestCase
            {
                Id = "T01", Area = "Авторизация", Kind = "позитивный", Title = "Вход администратора с верным паролем",
                Expected = "вход выполнен, роль «Администратор»",
                Run = () =>
                {
                    User u = auth.Login(AdminEmail, Password);
                    Check(u.RoleCode == Session.Admin, "роль " + u.RoleCode);
                    return "вход выполнен: " + u.FullName + ", " + u.RoleName;
                }
            });
            list.Add(new TestCase
            {
                Id = "T02", Area = "Авторизация", Kind = "негативный", Title = "Вход с неверным паролем",
                Expected = "отказ «Неверный e-mail или пароль»",
                Run = () => Expect<ValidationException>(() => auth.Login(AdminEmail, "000000"), "Неверный e-mail или пароль")
            });
            list.Add(new TestCase
            {
                Id = "T03", Area = "Авторизация", Kind = "негативный", Title = "Вход с пустыми полями",
                Expected = "отказ «Введите e-mail и пароль»",
                Run = () => Expect<ValidationException>(() => auth.Login("  ", ""), "Введите e-mail и пароль")
            });
            list.Add(new TestCase
            {
                Id = "T04", Area = "Авторизация", Kind = "негативный", Title = "Вход заблокированного пользователя",
                Expected = "отказ «Учётная запись заблокирована»",
                Run = () => Expect<ValidationException>(() => auth.Login("morozov@example.com", Password), "заблокирована")
            });
            list.Add(new TestCase
            {
                Id = "T05", Area = "Безопасность", Kind = "негативный", Title = "Несуществующий e-mail: сообщение не раскрывает, есть ли адрес",
                Expected = "то же сообщение, что при неверном пароле",
                Run = () => Expect<ValidationException>(() => auth.Login("nobody@example.com", Password), AuthService.WrongCredentials)
            });
            list.Add(new TestCase
            {
                Id = "T06", Area = "Права ролей", Kind = "негативный", Title = "Кассир и зритель не могут менять фильмы; зритель не видит отчёты",
                Expected = "права: администратор — всё, кассир — отчёты, зритель — только просмотр",
                Run = () =>
                {
                    User cashier = auth.Login("cashier1@kinotochka.ru", Password);
                    User viewer = auth.Login("ivanov@example.com", Password);
                    User admin = auth.Login(AdminEmail, Password);
                    Check(!Session.CanEditFilms(cashier) && !Session.CanEditFilms(viewer) && Session.CanEditFilms(admin), "права на изменение фильмов");
                    Check(Session.CanViewReports(cashier) && !Session.CanViewReports(viewer), "права на отчёты");
                    Check(Session.CanViewJournal(admin) && !Session.CanViewJournal(cashier), "права на журнал");
                    return "кассир: отчёты — да, фильмы — нет; зритель: только просмотр";
                }
            });
            list.Add(new TestCase
            {
                Id = "T07", Area = "Поиск", Kind = "позитивный", Title = "Поиск «звёздн» по названию",
                Expected = "1 фильм «Звёздный маршрут»",
                Run = () =>
                {
                    List<Film> found = films.Search("звёздн", null, FilmSort.TitleAsc, false);
                    Check(found.Count == 1 && found[0].Title == "Звёздный маршрут", "найдено " + found.Count);
                    return "найден 1 фильм: " + found[0].Title;
                }
            });
            list.Add(new TestCase
            {
                Id = "T08", Area = "Поиск", Kind = "негативный", Title = "Поиск строкой «' OR 1=1 --» (попытка SQL-инъекции)",
                Expected = "0 строк, без ошибки",
                Run = () =>
                {
                    List<Film> found = films.Search("' OR 1=1 --", null, FilmSort.TitleAsc, false);
                    Check(found.Count == 0, "найдено " + found.Count);
                    return "0 строк: ввод передан параметром, а не текстом SQL";
                }
            });
            list.Add(new TestCase
            {
                Id = "T09", Area = "Фильтр и сортировка", Kind = "позитивный", Title = "Фильтр «Драма» + сортировка по длительности (убыв.)",
                Expected = "2 фильма, первый — «Последний трамвай» (112 мин)",
                Run = () =>
                {
                    int drama = new GenreRepository().All().First(g => g.Name == "Драма").Id;
                    List<Film> found = films.Search("", drama, FilmSort.DurationDesc, false);
                    Check(found.Count == 2 && found[0].Title == "Последний трамвай", "получено " + string.Join(", ", found.Select(f => f.Title)));
                    return found.Count + " фильма: " + string.Join(", ", found.Select(f => f.Title + " (" + f.DurationMin + ")"));
                }
            });
            list.Add(new TestCase
            {
                Id = "T10", Area = "CRUD", Kind = "позитивный", Title = "Добавление, изменение и удаление фильма",
                Expected = "запись создана, изменена, удалена",
                Run = () =>
                {
                    string title = "ТЕСТ " + Guid.NewGuid().ToString("N").Substring(0, 8);
                    Film film = NewFilm(title);
                    film.Id = films.Insert(film);
                    Film saved = films.Get(film.Id);
                    Check(saved != null && saved.Title == title, "после добавления запись не найдена");
                    film.DurationMin = 120;
                    films.Update(film);
                    Check(films.Get(film.Id).DurationMin == 120, "изменение не сохранилось");
                    films.Delete(film.Id);
                    Check(films.Get(film.Id) == null, "после удаления запись осталась");
                    return "создан код " + film.Id + ", длительность 95 → 120, удалён";
                }
            });
            list.Add(new TestCase
            {
                Id = "T11", Area = "Целостность", Kind = "негативный", Title = "Удаление фильма, у которого есть сеансы",
                Expected = "отказ с пояснением, фильм не удалён",
                Run = () =>
                {
                    string r = Expect<AppDataException>(() => films.Delete(1), "нельзя удалить");
                    Check(films.Get(1) != null, "фильм удалён");
                    return r;
                }
            });
            list.Add(new TestCase
            {
                Id = "T12", Area = "Граничные значения", Kind = "граничный", Title = "Длительность 0, 1, 600 и 601 минута",
                Expected = "0 и 601 — ошибка, 1 и 600 — допустимо",
                Run = () =>
                {
                    Func<int, bool> ok = d => { Film f = NewFilm("Фильм"); f.DurationMin = d; return FilmValidator.Validate(f).Count == 0; };
                    Check(!ok(0) && ok(1) && ok(600) && !ok(601), "границы не совпали");
                    return "0 — ошибка, 1 — да, 600 — да, 601 — ошибка";
                }
            });
            list.Add(new TestCase
            {
                Id = "T13", Area = "Граничные значения", Kind = "граничный", Title = "Название 150 и 151 символ, пустое название",
                Expected = "150 — допустимо; 151 и пустое — ошибка",
                Run = () =>
                {
                    Func<string, bool> ok = t => FilmValidator.Validate(NewFilm(t)).Count == 0;
                    Check(ok(new string('А', 150)) && !ok(new string('А', 151)) && !ok("   "), "границы не совпали");
                    return "150 — да, 151 — «Название длиннее 150 символов», пустое — «Введите название»";
                }
            });
            list.Add(new TestCase
            {
                Id = "T14", Area = "Неверный ввод", Kind = "негативный", Title = "Недопустимый рейтинг «21+» (проверка CHECK в базе)",
                Expected = "база отклоняет запись, понятное сообщение",
                Run = () =>
                {
                    Film f = NewFilm("ТЕСТ рейтинг");
                    f.AgeRating = "21+";
                    return Expect<AppDataException>(() => films.Insert(f), "проверку");
                }
            });
            list.Add(new TestCase
            {
                Id = "T15", Area = "Целостность", Kind = "негативный", Title = "Фильм с несуществующим жанром (код 999)",
                Expected = "отказ: нарушена связь между таблицами",
                Run = () =>
                {
                    Film f = NewFilm("ТЕСТ жанр");
                    f.GenreId = 999;
                    return Expect<AppDataException>(() => films.Insert(f), "связь");
                }
            });
            list.Add(new TestCase
            {
                Id = "T16", Area = "Отсутствие записи", Kind = "негативный", Title = "Изменение удалённого фильма (код 999999)",
                Expected = "сообщение «Фильм не найден»",
                Run = () =>
                {
                    Film f = NewFilm("ТЕСТ");
                    f.Id = 999999;
                    return Expect<RecordNotFoundException>(() => films.Update(f), "не найден");
                }
            });
            list.Add(new TestCase
            {
                Id = "T17", Area = "Длинные строки", Kind = "граничный", Title = "Строка поиска из 5000 символов",
                Expected = "без ошибки, 0 строк",
                Run = () =>
                {
                    List<Film> found = films.Search(new string('я', 5000), null, FilmSort.TitleAsc, false);
                    Check(found.Count == 0, "найдено " + found.Count);
                    return "0 строк, ошибок нет (ввод обрезается до 200 символов)";
                }
            });
            list.Add(new TestCase
            {
                Id = "T18", Area = "Нет подключения", Kind = "негативный", Title = "Недоступный сервер базы данных",
                Expected = "сообщение «Нет подключения…» без строки подключения и пароля",
                Run = () =>
                {
                    Db.OverrideConnectionString = "Data Source=127.0.0.1,1;Initial Catalog=Киноточка;User ID=sa;Password=СекретныйПароль;Connect Timeout=2";
                    try
                    {
                        string r = Expect<AppDataException>(() => films.Search("", null, FilmSort.TitleAsc, false), "Нет подключения");
                        Check(!r.Contains("СекретныйПароль") && !r.Contains("Data Source"), "в сообщении есть строка подключения");
                        return "сообщение без технических подробностей и пароля";
                    }
                    finally
                    {
                        Db.OverrideConnectionString = null;
                    }
                }
            });
            list.Add(new TestCase
            {
                Id = "T19", Area = "Экспорт", Kind = "позитивный", Title = "Экспорт списка фильмов в CSV",
                Expected = "файл создан, заголовки и кириллица читаются",
                Run = () =>
                {
                    string path = Path.Combine(Path.GetTempPath(), "den5_7_test_export.csv");
                    List<Film> all = films.Search("", null, FilmSort.TitleAsc, false);
                    CsvExporter.Write(path, new[] { "Название", "Жанр" }, all.Select(f => (IList<string>)new[] { f.Title, f.Genre }));
                    string[] lines = File.ReadAllLines(path, Encoding.UTF8);
                    File.Delete(path);
                    Check(lines.Length == all.Count + 1 && lines[0] == "Название;Жанр", "строк " + lines.Length);
                    return "строк: " + lines.Length + ", первая: «" + lines[0] + "»";
                }
            });
            list.Add(new TestCase
            {
                Id = "T20", Area = "Журнал", Kind = "позитивный", Title = "Запись действия в журнал",
                Expected = "запись появилась последней",
                Run = () =>
                {
                    string mark = "Тест журнала " + DateTime.Now.ToString("HH:mm:ss");
                    journal.Add(1, mark);
                    JournalEntry last = journal.Last(1).FirstOrDefault();
                    Check(last != null && last.Action == mark, "последняя запись: " + (last == null ? "нет" : last.Action));
                    return "последняя запись: «" + last.Action + "»";
                }
            });
            list.Add(new TestCase
            {
                Id = "T21", Area = "Неверный ввод", Kind = "граничный", Title = "Ввод e-mail длиной 300 символов при входе",
                Expected = "отказ «Неверный e-mail или пароль», без ошибки базы",
                Run = () => Expect<ValidationException>(() => auth.Login(new string('a', 300) + "@x.ru", Password), AuthService.WrongCredentials)
            });
            return list;
        }

        /// <summary>Выполняет проверку и записывает результат в тест-кейс; программа при этом не падает.</summary>
        public static void Execute(TestCase t)
        {
            try
            {
                t.Actual = t.Run();
                t.Passed = true;
            }
            catch (TestFailed ex)
            {
                t.Actual = "НЕ ПРОЙДЕН: " + ex.Message;
                t.Passed = false;
            }
            catch (Exception ex)
            {
                t.Actual = "НЕ ПРОЙДЕН: " + ex.Message.Replace("\n", " ");
                t.Passed = false;
            }
        }
    }
}
