using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Kinotochka.Models;

namespace Kinotochka.Data
{
    /// <summary>Справочники: жанры, залы, роли, статусы заказа.</summary>
    public sealed class LookupRepository
    {
        private static LookupItem Map(SqlDataReader r)
        {
            return new LookupItem { Id = Db.I(r, "Код"), Name = Db.S(r, "Название"), Code = Db.S(r, "Обозначение") };
        }

        public List<LookupItem> Genres()
        {
            return Db.Query("SELECT КодЖанра AS Код, Название, NULL AS Обозначение FROM dbo.Жанры ORDER BY Название;", Map, "чтение жанров");
        }

        public List<LookupItem> Halls()
        {
            return Db.Query(
                "SELECT КодЗала AS Код, N'№' + CAST(Номер AS NVARCHAR(5)) + N' «' + Название + N'», мест: ' + CAST(КоличествоРядов * МестВРяду AS NVARCHAR(10)) AS Название, " +
                "NULL AS Обозначение FROM dbo.Залы ORDER BY Номер;", Map, "чтение залов");
        }

        public List<LookupItem> Roles()
        {
            return Db.Query("SELECT КодРоли AS Код, Название, Обозначение FROM dbo.Роли ORDER BY КодРоли;", Map, "чтение ролей");
        }

        public List<LookupItem> Statuses()
        {
            return Db.Query("SELECT КодСтатуса AS Код, Название, Обозначение FROM dbo.СтатусыЗаказа ORDER BY КодСтатуса;", Map, "чтение статусов заказа");
        }

        public List<LookupItem> FilmsShort()
        {
            return Db.Query("SELECT КодФильма AS Код, Название, NULL AS Обозначение FROM dbo.Фильмы ORDER BY Название;", Map, "чтение фильмов");
        }
    }

    /// <summary>Учётные записи (таблица Пользователи).</summary>
    public sealed class UserRepository
    {
        private const string Select =
            "SELECT p.КодПользователя, p.ФИО, p.ЭлПочта, p.Телефон, p.ХешПароля, p.КодРоли, r.Обозначение AS КодРолиОбозначение, " +
            "r.Название AS НазваниеРоли, p.Заблокирован, p.ДатаСоздания " +
            "FROM dbo.Пользователи p JOIN dbo.Роли r ON r.КодРоли = p.КодРоли ";

        private static User Map(SqlDataReader r)
        {
            return new User
            {
                Id = Db.I(r, "КодПользователя"),
                FullName = Db.S(r, "ФИО"),
                Email = Db.S(r, "ЭлПочта"),
                Phone = Db.S(r, "Телефон"),
                PasswordHash = Db.S(r, "ХешПароля"),
                RoleId = Db.I(r, "КодРоли"),
                RoleCode = Db.S(r, "КодРолиОбозначение"),
                RoleName = Db.S(r, "НазваниеРоли"),
                IsBlocked = Db.B(r, "Заблокирован"),
                CreatedAt = Db.D(r, "ДатаСоздания")
            };
        }

        public User FindByEmail(string email)
        {
            List<User> list = Db.Query(Select + "WHERE p.ЭлПочта = @ЭлПочта;", Map, "вход в систему",
                Db.Text("@ЭлПочта", email, 100));
            return list.Count > 0 ? list[0] : null;
        }

        public bool EmailExists(string email, int exceptId)
        {
            object n = Db.Scalar("SELECT COUNT(*) FROM dbo.Пользователи WHERE ЭлПочта = @ЭлПочта AND КодПользователя <> @Код;",
                "проверка электронной почты", Db.Text("@ЭлПочта", email, 100), Db.Int("@Код", exceptId));
            return Convert.ToInt32(n) > 0;
        }

        /// <summary>Поиск по ФИО, почте или телефону и фильтр по роли (0 — все роли).</summary>
        public List<User> Search(string text, int roleId)
        {
            return Db.Query(Select +
                "WHERE (@Поиск IS NULL OR p.ФИО LIKE N'%' + @Поиск + N'%' OR p.ЭлПочта LIKE N'%' + @Поиск + N'%' OR p.Телефон LIKE N'%' + @Поиск + N'%') " +
                "AND (@КодРоли IS NULL OR p.КодРоли = @КодРоли) ORDER BY p.КодРоли DESC, p.ФИО;", Map, "поиск пользователей",
                Db.Text("@Поиск", text, 100), Db.NullableInt("@КодРоли", roleId));
        }

        public int Add(User u)
        {
            object id = Db.Scalar(
                "INSERT INTO dbo.Пользователи (ФИО, ЭлПочта, Телефон, ХешПароля, КодРоли, Заблокирован) " +
                "VALUES (@ФИО, @ЭлПочта, @Телефон, @ХешПароля, @КодРоли, @Заблокирован); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                "добавление пользователя",
                Db.Text("@ФИО", u.FullName, 100), Db.Text("@ЭлПочта", u.Email, 100), Db.Text("@Телефон", u.Phone, 20),
                Db.Text("@ХешПароля", u.PasswordHash, 200), Db.Int("@КодРоли", u.RoleId), Db.Bit("@Заблокирован", u.IsBlocked));
            return Convert.ToInt32(id);
        }

        /// <summary>Изменение данных; пароль меняется, только если передан новый хеш.</summary>
        public void Update(User u)
        {
            Db.Execute(
                "UPDATE dbo.Пользователи SET ФИО = @ФИО, ЭлПочта = @ЭлПочта, Телефон = @Телефон, КодРоли = @КодРоли, " +
                "Заблокирован = @Заблокирован, ХешПароля = COALESCE(@ХешПароля, ХешПароля) WHERE КодПользователя = @Код;",
                "изменение пользователя",
                Db.Text("@ФИО", u.FullName, 100), Db.Text("@ЭлПочта", u.Email, 100), Db.Text("@Телефон", u.Phone, 20),
                Db.Int("@КодРоли", u.RoleId), Db.Bit("@Заблокирован", u.IsBlocked), Db.Text("@ХешПароля", u.PasswordHash, 200),
                Db.Int("@Код", u.Id));
        }

        public void SetBlocked(int id, bool blocked)
        {
            Db.Execute("UPDATE dbo.Пользователи SET Заблокирован = @Заблокирован WHERE КодПользователя = @Код;",
                "блокировка пользователя", Db.Bit("@Заблокирован", blocked), Db.Int("@Код", id));
        }

        /// <summary>Сколько заказов (как покупатель или кассир) и отзывов связано с пользователем.</summary>
        public int LinkedRecords(int id)
        {
            object n = Db.Scalar(
                "SELECT (SELECT COUNT(*) FROM dbo.Заказы WHERE КодПользователя = @Код OR КодКассира = @Код) + " +
                "(SELECT COUNT(*) FROM dbo.Отзывы WHERE КодПользователя = @Код);", "проверка связей пользователя", Db.Int("@Код", id));
            return Convert.ToInt32(n);
        }

        public void Delete(int id)
        {
            Db.Execute("DELETE FROM dbo.Пользователи WHERE КодПользователя = @Код;", "удаление пользователя", Db.Int("@Код", id));
        }
    }

    /// <summary>Фильмы.</summary>
    public sealed class FilmRepository
    {
        private static Film Map(SqlDataReader r)
        {
            return new Film
            {
                Id = Db.I(r, "КодФильма"),
                Title = Db.S(r, "Название"),
                GenreId = Db.I(r, "КодЖанра"),
                GenreName = Db.S(r, "Жанр"),
                DurationMin = Db.I(r, "ДлительностьМин"),
                AgeRating = Db.S(r, "ВозрастнойРейтинг"),
                Description = Db.S(r, "Описание"),
                IsActive = Db.B(r, "Активен"),
                ScreeningCount = Db.I(r, "Сеансов")
            };
        }

        /// <summary>Поиск по части названия, фильтр по жанру (0 — все) и по прокату.</summary>
        public List<Film> Search(string text, int genreId, bool onlyActive)
        {
            return Db.Query(
                "SELECT f.КодФильма, f.Название, f.КодЖанра, g.Название AS Жанр, f.ДлительностьМин, f.ВозрастнойРейтинг, f.Описание, f.Активен, " +
                "(SELECT COUNT(*) FROM dbo.Сеансы s WHERE s.КодФильма = f.КодФильма) AS Сеансов " +
                "FROM dbo.Фильмы f JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра " +
                "WHERE (@Поиск IS NULL OR f.Название LIKE N'%' + @Поиск + N'%') AND (@КодЖанра IS NULL OR f.КодЖанра = @КодЖанра) " +
                "AND (@ТолькоВПрокате = 0 OR f.Активен = 1) ORDER BY f.Название;",
                Map, "поиск фильмов",
                Db.Text("@Поиск", text, 150), Db.NullableInt("@КодЖанра", genreId), Db.Bit("@ТолькоВПрокате", onlyActive));
        }

        public int Add(Film f)
        {
            object id = Db.Scalar(
                "INSERT INTO dbo.Фильмы (Название, КодЖанра, ДлительностьМин, ВозрастнойРейтинг, Описание, Активен) " +
                "VALUES (@Название, @КодЖанра, @Длительность, @Рейтинг, @Описание, @Активен); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                "добавление фильма",
                Db.Text("@Название", f.Title, 150), Db.Int("@КодЖанра", f.GenreId), Db.Int("@Длительность", f.DurationMin),
                Db.Text("@Рейтинг", f.AgeRating, 3), Db.Text("@Описание", f.Description, 1000), Db.Bit("@Активен", f.IsActive));
            return Convert.ToInt32(id);
        }

        public void Update(Film f)
        {
            Db.Execute(
                "UPDATE dbo.Фильмы SET Название = @Название, КодЖанра = @КодЖанра, ДлительностьМин = @Длительность, " +
                "ВозрастнойРейтинг = @Рейтинг, Описание = @Описание, Активен = @Активен WHERE КодФильма = @Код;",
                "изменение фильма",
                Db.Text("@Название", f.Title, 150), Db.Int("@КодЖанра", f.GenreId), Db.Int("@Длительность", f.DurationMin),
                Db.Text("@Рейтинг", f.AgeRating, 3), Db.Text("@Описание", f.Description, 1000), Db.Bit("@Активен", f.IsActive),
                Db.Int("@Код", f.Id));
        }

        /// <summary>Связанные записи: сеансы и отзывы (внешние ключи без каскадного удаления).</summary>
        public void LinkedRecords(int id, out int screenings, out int reviews)
        {
            List<int[]> r = Db.Query(
                "SELECT (SELECT COUNT(*) FROM dbo.Сеансы WHERE КодФильма = @Код) AS Сеансов, (SELECT COUNT(*) FROM dbo.Отзывы WHERE КодФильма = @Код) AS Отзывов;",
                x => new[] { Db.I(x, "Сеансов"), Db.I(x, "Отзывов") }, "проверка связей фильма", Db.Int("@Код", id));
            screenings = r[0][0];
            reviews = r[0][1];
        }

        public void SetActive(int id, bool active)
        {
            Db.Execute("UPDATE dbo.Фильмы SET Активен = @Активен WHERE КодФильма = @Код;", "снятие фильма с проката",
                Db.Bit("@Активен", active), Db.Int("@Код", id));
        }

        public void Delete(int id)
        {
            Db.Execute("DELETE FROM dbo.Фильмы WHERE КодФильма = @Код;", "удаление фильма", Db.Int("@Код", id));
        }
    }

    /// <summary>Сеансы.</summary>
    public sealed class ScreeningRepository
    {
        /// <summary>Действующие заказы (их билеты занимают места).</summary>
        private const string ActiveStatuses = "(N'ЗАБРОНИРОВАН', N'ОПЛАЧИВАЕТСЯ', N'ОПЛАЧЕН', N'ЗАВЕРШЁН')";

        private static Screening Map(SqlDataReader r)
        {
            return new Screening
            {
                Id = Db.I(r, "КодСеанса"),
                FilmId = Db.I(r, "КодФильма"),
                FilmTitle = Db.S(r, "Фильм"),
                HallId = Db.I(r, "КодЗала"),
                HallName = Db.S(r, "Зал"),
                StartsAt = Db.D(r, "НачалоСеанса"),
                Format = Db.S(r, "Формат"),
                BasePrice = Db.M(r, "БазоваяЦена"),
                SoldSeats = Db.I(r, "Продано"),
                Capacity = Db.I(r, "Мест"),
                OrderCount = Db.I(r, "Заказов")
            };
        }

        /// <summary>Период: 0 — все, 1 — предстоящие, 2 — прошедшие; фильм: 0 — все.</summary>
        public List<Screening> Search(int period, int filmId, DateTime now)
        {
            return Db.Query(
                "SELECT s.КодСеанса, s.КодФильма, f.Название AS Фильм, s.КодЗала, z.Название AS Зал, s.НачалоСеанса, s.Формат, s.БазоваяЦена, " +
                "z.КоличествоРядов * z.МестВРяду AS Мест, " +
                "(SELECT COUNT(*) FROM dbo.Заказы o JOIN dbo.Билеты t ON t.КодЗаказа = o.КодЗаказа JOIN dbo.СтатусыЗаказа st ON st.КодСтатуса = o.КодСтатуса " +
                " WHERE o.КодСеанса = s.КодСеанса AND st.Обозначение IN " + ActiveStatuses + ") AS Продано, " +
                "(SELECT COUNT(*) FROM dbo.Заказы o WHERE o.КодСеанса = s.КодСеанса) AS Заказов " +
                "FROM dbo.Сеансы s JOIN dbo.Фильмы f ON f.КодФильма = s.КодФильма JOIN dbo.Залы z ON z.КодЗала = s.КодЗала " +
                "WHERE (@Период = 0 OR (@Период = 1 AND s.НачалоСеанса >= @Сейчас) OR (@Период = 2 AND s.НачалоСеанса < @Сейчас)) " +
                "AND (@КодФильма IS NULL OR s.КодФильма = @КодФильма) " +
                "ORDER BY CASE WHEN @Период = 2 THEN 0 ELSE 1 END, " +
                "CASE WHEN @Период = 2 THEN s.НачалоСеанса END DESC, s.НачалоСеанса;",
                Map, "чтение сеансов",
                Db.Int("@Период", period), Db.NullableInt("@КодФильма", filmId), Db.Date("@Сейчас", now));
        }

        /// <summary>Пересекается ли сеанс по времени с другим сеансом того же зала (с учётом длительности фильма и 15 минут на уборку).</summary>
        public string FindOverlap(Screening s)
        {
            List<string> r = Db.Query(
                "SELECT TOP 1 f2.Название + N' в ' + FORMAT(s2.НачалоСеанса, 'dd.MM.yyyy HH:mm') AS Сеанс " +
                "FROM dbo.Сеансы s2 JOIN dbo.Фильмы f2 ON f2.КодФильма = s2.КодФильма " +
                "CROSS JOIN (SELECT ДлительностьМин FROM dbo.Фильмы WHERE КодФильма = @КодФильма) f " +
                "WHERE s2.КодЗала = @КодЗала AND s2.КодСеанса <> @Код " +
                "AND s2.НачалоСеанса < DATEADD(MINUTE, f.ДлительностьМин + 15, @Начало) " +
                "AND @Начало < DATEADD(MINUTE, f2.ДлительностьМин + 15, s2.НачалоСеанса);",
                x => Db.S(x, "Сеанс"), "проверка расписания зала",
                Db.Int("@КодФильма", s.FilmId), Db.Int("@КодЗала", s.HallId), Db.Int("@Код", s.Id), Db.Date("@Начало", s.StartsAt));
            return r.Count > 0 ? r[0] : null;
        }

        public int Add(Screening s)
        {
            object id = Db.Scalar(
                "INSERT INTO dbo.Сеансы (КодФильма, КодЗала, НачалоСеанса, Формат, БазоваяЦена) " +
                "VALUES (@КодФильма, @КодЗала, @Начало, @Формат, @Цена); SELECT CAST(SCOPE_IDENTITY() AS INT);",
                "добавление сеанса",
                Db.Int("@КодФильма", s.FilmId), Db.Int("@КодЗала", s.HallId), Db.Date("@Начало", s.StartsAt),
                Db.Text("@Формат", s.Format, 10), Db.Money("@Цена", s.BasePrice));
            return Convert.ToInt32(id);
        }

        public void Update(Screening s)
        {
            Db.Execute(
                "UPDATE dbo.Сеансы SET КодФильма = @КодФильма, КодЗала = @КодЗала, НачалоСеанса = @Начало, Формат = @Формат, " +
                "БазоваяЦена = @Цена WHERE КодСеанса = @Код;",
                "изменение сеанса",
                Db.Int("@КодФильма", s.FilmId), Db.Int("@КодЗала", s.HallId), Db.Date("@Начало", s.StartsAt),
                Db.Text("@Формат", s.Format, 10), Db.Money("@Цена", s.BasePrice), Db.Int("@Код", s.Id));
        }

        public int OrderCount(int id)
        {
            return Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM dbo.Заказы WHERE КодСеанса = @Код;", "проверка заказов сеанса", Db.Int("@Код", id)));
        }

        public void Delete(int id)
        {
            Db.Execute("DELETE FROM dbo.Сеансы WHERE КодСеанса = @Код;", "удаление сеанса", Db.Int("@Код", id));
        }
    }

    /// <summary>Заказы.</summary>
    public sealed class OrderRepository
    {
        private static Order Map(SqlDataReader r)
        {
            return new Order
            {
                Id = Db.I(r, "КодЗаказа"),
                CreatedAt = Db.D(r, "ДатаСоздания"),
                Customer = Db.S(r, "Покупатель"),
                Cashier = Db.S(r, "Кассир"),
                ScreeningText = Db.S(r, "Сеанс"),
                StatusId = Db.I(r, "КодСтатуса"),
                StatusCode = Db.S(r, "Обозначение"),
                StatusName = Db.S(r, "Статус"),
                TicketCount = Db.I(r, "Билетов"),
                Total = Db.M(r, "Итого"),
                PromoCode = Db.S(r, "Промокод"),
                PaymentCount = Db.I(r, "Платежей")
            };
        }

        /// <summary>Поиск по номеру заказа, покупателю или фильму; фильтр по статусу; userId &gt; 0 — только заказы этого зрителя.</summary>
        public List<Order> Search(string text, int statusId, int userId)
        {
            int number;
            int.TryParse((text ?? "").Trim().TrimStart('№'), out number);
            return Db.Query(
                "SELECT o.КодЗаказа, o.ДатаСоздания, ISNULL(u.ФИО, N'(продажа в кассе)') AS Покупатель, k.ФИО AS Кассир, " +
                "f.Название + N', ' + FORMAT(s.НачалоСеанса, 'dd.MM.yyyy HH:mm') AS Сеанс, o.КодСтатуса, st.Обозначение, st.Название AS Статус, " +
                "ISNULL(v.КоличествоБилетов, 0) AS Билетов, ISNULL(v.Итого, 0) AS Итого, pr.Обозначение AS Промокод, " +
                "(SELECT COUNT(*) FROM dbo.Платежи p WHERE p.КодЗаказа = o.КодЗаказа) AS Платежей " +
                "FROM dbo.Заказы o " +
                "JOIN dbo.Сеансы s ON s.КодСеанса = o.КодСеанса JOIN dbo.Фильмы f ON f.КодФильма = s.КодФильма " +
                "JOIN dbo.СтатусыЗаказа st ON st.КодСтатуса = o.КодСтатуса " +
                "LEFT JOIN dbo.Пользователи u ON u.КодПользователя = o.КодПользователя " +
                "LEFT JOIN dbo.Пользователи k ON k.КодПользователя = o.КодКассира " +
                "LEFT JOIN dbo.Промокоды pr ON pr.КодПромокода = o.КодПромокода " +
                "LEFT JOIN dbo.СуммыЗаказов v ON v.КодЗаказа = o.КодЗаказа " +
                "WHERE (@Поиск IS NULL OR u.ФИО LIKE N'%' + @Поиск + N'%' OR f.Название LIKE N'%' + @Поиск + N'%' OR o.КодЗаказа = @Номер) " +
                "AND (@КодСтатуса IS NULL OR o.КодСтатуса = @КодСтатуса) AND (@КодПользователя IS NULL OR o.КодПользователя = @КодПользователя) " +
                "ORDER BY o.ДатаСоздания DESC;",
                Map, "чтение заказов",
                Db.Text("@Поиск", text, 100), Db.Int("@Номер", number), Db.NullableInt("@КодСтатуса", statusId), Db.NullableInt("@КодПользователя", userId));
        }

        /// <summary>
        /// Сколько мест заказа уже занято другими действующими заказами того же сеанса
        /// (нужно, если отменённый заказ снова делают действующим).
        /// </summary>
        public int SeatConflicts(int orderId)
        {
            return Convert.ToInt32(Db.Scalar(
                "SELECT COUNT(*) FROM dbo.Билеты t JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа " +
                "JOIN dbo.Билеты t2 ON t2.КодМеста = t.КодМеста AND t2.КодЗаказа <> t.КодЗаказа " +
                "JOIN dbo.Заказы o2 ON o2.КодЗаказа = t2.КодЗаказа AND o2.КодСеанса = o.КодСеанса " +
                "JOIN dbo.СтатусыЗаказа st ON st.КодСтатуса = o2.КодСтатуса " +
                "WHERE t.КодЗаказа = @Код AND st.Обозначение IN (N'ЗАБРОНИРОВАН', N'ОПЛАЧИВАЕТСЯ', N'ОПЛАЧЕН', N'ЗАВЕРШЁН');",
                "проверка занятости мест", Db.Int("@Код", orderId)));
        }

        public void ChangeStatus(int orderId, int statusId)
        {
            Db.Execute("UPDATE dbo.Заказы SET КодСтатуса = @КодСтатуса WHERE КодЗаказа = @Код;", "смена статуса заказа",
                Db.Int("@КодСтатуса", statusId), Db.Int("@Код", orderId));
        }

        /// <summary>Удаление заказа; билеты удаляются каскадно (ON DELETE CASCADE), платежи — запрещают удаление.</summary>
        public void Delete(int orderId)
        {
            Db.Execute("DELETE FROM dbo.Заказы WHERE КодЗаказа = @Код;", "удаление заказа", Db.Int("@Код", orderId));
        }
    }

    /// <summary>Показатели главной страницы.</summary>
    public sealed class DashboardRepository
    {
        public Dashboard Load(DateTime now)
        {
            List<Dashboard> r = Db.Query(
                "SELECT (SELECT COUNT(*) FROM dbo.Фильмы WHERE Активен = 1) AS Фильмов, " +
                "(SELECT COUNT(*) FROM dbo.Сеансы WHERE НачалоСеанса >= @Сейчас) AS Сеансов, " +
                "(SELECT COUNT(*) FROM dbo.Заказы o JOIN dbo.СтатусыЗаказа st ON st.КодСтатуса = o.КодСтатуса " +
                " WHERE st.Обозначение IN (N'ЗАБРОНИРОВАН', N'ОПЛАЧИВАЕТСЯ', N'ОПЛАЧЕН')) AS Заказов, " +
                "(SELECT ISNULL(SUM(CASE WHEN ВидОперации = N'ОПЛАТА' THEN Сумма ELSE -Сумма END), 0) FROM dbo.Платежи WHERE Успешно = 1) AS Выручка, " +
                "(SELECT COUNT(*) FROM dbo.Пользователи) AS Пользователей;",
                x => new Dashboard
                {
                    Films = Db.I(x, "Фильмов"),
                    UpcomingScreenings = Db.I(x, "Сеансов"),
                    ActiveOrders = Db.I(x, "Заказов"),
                    Revenue = Db.M(x, "Выручка"),
                    Users = Db.I(x, "Пользователей")
                }, "чтение показателей", Db.Date("@Сейчас", now));
            return r[0];
        }
    }
}
