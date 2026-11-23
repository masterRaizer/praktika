using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using Den5_7.Models;

namespace Den5_7.Data
{
    /// <summary>Пользователи: поиск по e-mail для входа.</summary>
    public sealed class UserRepository
    {
        public sealed class UserRecord
        {
            public User User;
            public string PasswordHash;
            public bool IsBlocked;
        }

        public UserRecord FindByEmail(string email)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                const string sql = @"
SELECT u.КодПользователя, u.ФИО, u.ЭлПочта, u.ХешПароля, u.Заблокирован, r.Обозначение, r.Название
FROM dbo.Пользователи u
JOIN dbo.Роли r ON r.КодРоли = u.КодРоли
WHERE u.ЭлПочта = @ЭлПочта;";
                using (var cmd = new SqlCommand(sql, c))
                {
                    cmd.Parameters.Add("@ЭлПочта", SqlDbType.NVarChar, 100).Value = email;
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (!r.Read()) return null;
                        return new UserRecord
                        {
                            User = new User
                            {
                                Id = r.GetInt32(0),
                                FullName = r.GetString(1),
                                Email = r.GetString(2),
                                RoleCode = r.GetString(5),
                                RoleName = r.GetString(6)
                            },
                            PasswordHash = r.GetString(3),
                            IsBlocked = r.GetBoolean(4)
                        };
                    }
                }
            });
        }
    }

    public sealed class GenreRepository
    {
        public List<Genre> All()
        {
            return Db.Run(delegate(SqlConnection c)
            {
                var list = new List<Genre>();
                using (var cmd = new SqlCommand("SELECT КодЖанра, Название FROM dbo.Жанры ORDER BY Название;", c))
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(new Genre { Id = r.GetInt32(0), Name = r.GetString(1) });
                }
                return list;
            });
        }
    }

    /// <summary>Справочник фильмов: поиск, фильтр, сортировка, добавление, изменение, удаление.</summary>
    public sealed class FilmRepository
    {
        private const string SelectSql = @"
SELECT f.КодФильма, f.Название, f.КодЖанра, g.Название, f.ДлительностьМин, f.ВозрастнойРейтинг,
       f.Описание, f.Постер, f.Активен,
       (SELECT COUNT(*) FROM dbo.Сеансы s WHERE s.КодФильма = f.КодФильма) AS Сеансов
FROM dbo.Фильмы f
JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра";

        /// <summary>Поиск по названию и описанию, фильтр по жанру и активности, сортировка.</summary>
        public List<Film> Search(string text, int? genreId, FilmSort sort, bool onlyActive)
        {
            // порядок сортировки выбирается из фиксированного списка, пользовательский ввод в текст SQL не попадает
            string order;
            switch (sort)
            {
                case FilmSort.TitleDesc: order = "f.Название DESC"; break;
                case FilmSort.DurationDesc: order = "f.ДлительностьМин DESC, f.Название"; break;
                case FilmSort.DurationAsc: order = "f.ДлительностьМин, f.Название"; break;
                default: order = "f.Название"; break;
            }
            string sql = SelectSql + @"
WHERE (@Поиск = N'' OR f.Название LIKE N'%' + @Поиск + N'%' OR f.Описание LIKE N'%' + @Поиск + N'%')
  AND (@КодЖанра IS NULL OR f.КодЖанра = @КодЖанра)
  AND (@ТолькоАктивные = 0 OR f.Активен = 1)
ORDER BY " + order + ";";
            return Db.Run(delegate(SqlConnection c)
            {
                using (var cmd = new SqlCommand(sql, c))
                {
                    cmd.Parameters.Add("@Поиск", SqlDbType.NVarChar, 200).Value = Db.EscapeLike((text ?? "").Trim());
                    cmd.Parameters.Add("@КодЖанра", SqlDbType.Int).Value = genreId.HasValue ? (object)genreId.Value : DBNull.Value;
                    cmd.Parameters.Add("@ТолькоАктивные", SqlDbType.Bit).Value = onlyActive;
                    return ReadFilms(cmd);
                }
            });
        }

        public Film Get(int id)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                using (var cmd = new SqlCommand(SelectSql + " WHERE f.КодФильма = @Код;", c))
                {
                    cmd.Parameters.Add("@Код", SqlDbType.Int).Value = id;
                    List<Film> list = ReadFilms(cmd);
                    return list.Count > 0 ? list[0] : null;
                }
            });
        }

        public int Insert(Film film)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                const string sql = @"
INSERT INTO dbo.Фильмы (Название, КодЖанра, ДлительностьМин, ВозрастнойРейтинг, Описание, Постер, Активен)
OUTPUT INSERTED.КодФильма
VALUES (@Название, @КодЖанра, @Длительность, @Рейтинг, @Описание, @Постер, @Активен);";
                using (var cmd = new SqlCommand(sql, c))
                {
                    AddFilmParameters(cmd, film);
                    return (int)cmd.ExecuteScalar();
                }
            });
        }

        public void Update(Film film)
        {
            int rows = Db.Run(delegate(SqlConnection c)
            {
                const string sql = @"
UPDATE dbo.Фильмы
SET Название = @Название, КодЖанра = @КодЖанра, ДлительностьМин = @Длительность,
    ВозрастнойРейтинг = @Рейтинг, Описание = @Описание, Постер = @Постер, Активен = @Активен
WHERE КодФильма = @Код;";
                using (var cmd = new SqlCommand(sql, c))
                {
                    AddFilmParameters(cmd, film);
                    cmd.Parameters.Add("@Код", SqlDbType.Int).Value = film.Id;
                    return cmd.ExecuteNonQuery();
                }
            });
            if (rows == 0)
            {
                throw new RecordNotFoundException("Фильм не найден: возможно, его уже удалили. Обновите список.");
            }
        }

        public void Delete(int id)
        {
            int rows;
            try
            {
                rows = Db.Run(delegate(SqlConnection c)
                {
                    using (var cmd = new SqlCommand("DELETE FROM dbo.Фильмы WHERE КодФильма = @Код;", c))
                    {
                        cmd.Parameters.Add("@Код", SqlDbType.Int).Value = id;
                        return cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (AppDataException ex)
            {
                var sql = ex.InnerException as SqlException;
                if (sql != null && sql.Number == 547)
                {
                    throw new AppDataException(
                        "Фильм нельзя удалить: на него есть сеансы или отзывы. " +
                        "Чтобы убрать фильм из афиши, снимите флажок «Активен» в карточке.", ex);
                }
                throw;
            }
            if (rows == 0)
            {
                throw new RecordNotFoundException("Фильм не найден: возможно, его уже удалили. Обновите список.");
            }
        }

        private static void AddFilmParameters(SqlCommand cmd, Film f)
        {
            cmd.Parameters.Add("@Название", SqlDbType.NVarChar, 150).Value = (f.Title ?? "").Trim();
            cmd.Parameters.Add("@КодЖанра", SqlDbType.Int).Value = f.GenreId;
            cmd.Parameters.Add("@Длительность", SqlDbType.SmallInt).Value = f.DurationMin;
            cmd.Parameters.Add("@Рейтинг", SqlDbType.NVarChar, 3).Value = f.AgeRating ?? "";
            cmd.Parameters.Add("@Описание", SqlDbType.NVarChar, 1000).Value =
                string.IsNullOrWhiteSpace(f.Description) ? (object)DBNull.Value : f.Description.Trim();
            cmd.Parameters.Add("@Постер", SqlDbType.NVarChar, 260).Value =
                string.IsNullOrWhiteSpace(f.Poster) ? (object)DBNull.Value : f.Poster.Trim();
            cmd.Parameters.Add("@Активен", SqlDbType.Bit).Value = f.IsActive;
        }

        private static List<Film> ReadFilms(SqlCommand cmd)
        {
            var list = new List<Film>();
            using (SqlDataReader r = cmd.ExecuteReader())
            {
                while (r.Read())
                {
                    list.Add(new Film
                    {
                        Id = r.GetInt32(0),
                        Title = r.GetString(1),
                        GenreId = r.GetInt32(2),
                        Genre = r.GetString(3),
                        DurationMin = r.GetInt16(4),
                        AgeRating = r.GetString(5),
                        Description = r.IsDBNull(6) ? null : r.GetString(6),
                        Poster = r.IsDBNull(7) ? null : r.GetString(7),
                        IsActive = r.GetBoolean(8),
                        SessionsCount = r.GetInt32(9)
                    });
                }
            }
            return list;
        }
    }

    /// <summary>Журнал действий. Ошибка записи в журнал не мешает основному действию.</summary>
    public sealed class JournalRepository
    {
        public void Add(int? userId, string action, string details = null)
        {
            try
            {
                Db.Run(delegate(SqlConnection c)
                {
                    const string sql = "INSERT INTO dbo.ЖурналДействий (КодПользователя, Действие, Подробности) VALUES (@Пользователь, @Действие, @Подробности);";
                    using (var cmd = new SqlCommand(sql, c))
                    {
                        cmd.Parameters.Add("@Пользователь", SqlDbType.Int).Value = userId.HasValue ? (object)userId.Value : DBNull.Value;
                        cmd.Parameters.Add("@Действие", SqlDbType.NVarChar, 200).Value = Cut(action, 200);
                        cmd.Parameters.Add("@Подробности", SqlDbType.NVarChar, 500).Value =
                            string.IsNullOrEmpty(details) ? (object)DBNull.Value : Cut(details, 500);
                        cmd.ExecuteNonQuery();
                    }
                });
            }
            catch (AppDataException)
            {
                // журнал вспомогательный: если запись не удалась, действие пользователя всё равно выполнено
            }
        }

        public List<JournalEntry> Last(int count)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                const string sql = @"
SELECT TOP (@Сколько) j.ВремяДействия, ISNULL(u.ФИО, N'—'), j.Действие, ISNULL(j.Подробности, N'')
FROM dbo.ЖурналДействий j
LEFT JOIN dbo.Пользователи u ON u.КодПользователя = j.КодПользователя
ORDER BY j.ВремяДействия DESC, j.КодЗаписи DESC;";
                var list = new List<JournalEntry>();
                using (var cmd = new SqlCommand(sql, c))
                {
                    cmd.Parameters.Add("@Сколько", SqlDbType.Int).Value = count;
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new JournalEntry { Time = r.GetDateTime(0), User = r.GetString(1), Action = r.GetString(2), Details = r.GetString(3) });
                        }
                    }
                }
                return list;
            });
        }

        private static string Cut(string s, int max)
        {
            s = s ?? "";
            return s.Length <= max ? s : s.Substring(0, max);
        }
    }

    /// <summary>Выборки для главной страницы, отчётов и словаря данных — результат в виде таблицы.</summary>
    public static class Queries
    {
        public static DataTable Table(string sql, params SqlParameter[] parameters)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                using (var cmd = new SqlCommand(sql, c))
                {
                    if (parameters != null) cmd.Parameters.AddRange(parameters);
                    var table = new DataTable();
                    using (var adapter = new SqlDataAdapter(cmd))
                    {
                        adapter.Fill(table);
                    }
                    return table;
                }
            });
        }

        public static object Scalar(string sql)
        {
            return Db.Run(delegate(SqlConnection c)
            {
                using (var cmd = new SqlCommand(sql, c))
                {
                    return cmd.ExecuteScalar();
                }
            });
        }

        public const string Afisha = @"
SELECT FORMAT(НачалоСеанса, 'dd.MM HH:mm') AS Сеанс, Фильм, Жанр, ВозрастнойРейтинг AS Возраст, Зал, Формат,
       FORMAT(БазоваяЦена, 'N2', 'ru-RU') AS Цена, Свободно
FROM dbo.Афиша
ORDER BY НачалоСеанса;";

        public const string Revenue = @"
SELECT Фильм, FORMAT(Оплачено, 'N2', 'ru-RU') AS Оплачено, FORMAT(Возвращено, 'N2', 'ru-RU') AS Возвращено,
       FORMAT(Выручка, 'N2', 'ru-RU') AS Выручка
FROM dbo.ВыручкаПоФильмам
ORDER BY ВыручкаПоФильмам.Выручка DESC, Фильм;";

        public const string Occupancy = @"
SELECT FORMAT(НачалоСеанса, 'dd.MM HH:mm') AS Сеанс, Фильм, Зал, Занято, ВсегоМест AS Мест,
       FORMAT(100.0 * Занято / ВсегоМест, 'N1', 'ru-RU') AS [Заполнено, %]
FROM dbo.Афиша
ORDER BY 100.0 * Занято / ВсегоМест DESC, НачалоСеанса;";

        /// <summary>Словарь данных: поля всех таблиц с типом, обязательностью, ключами, ограничениями и назначением.</summary>
        public const string DataDictionary = @"
SELECT t.name AS Таблица, c.name AS Поле,
       ty.name + CASE WHEN ty.name IN ('nvarchar', 'varchar') THEN '(' + CAST(c.max_length / 2 AS VARCHAR(10)) + ')'
                      WHEN ty.name = 'decimal' THEN '(' + CAST(c.precision AS VARCHAR(5)) + ',' + CAST(c.scale AS VARCHAR(5)) + ')'
                      ELSE '' END AS Тип,
       CASE WHEN c.is_nullable = 0 THEN N'да' ELSE N'нет' END AS Обязательное,
       LTRIM(CONCAT(
           CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                             WHERE i.is_primary_key = 1 AND ic.object_id = c.object_id AND ic.column_id = c.column_id) THEN N'PK ' END,
           (SELECT TOP 1 N'FK → ' + OBJECT_NAME(fk.referenced_object_id) + N' ' FROM sys.foreign_key_columns fk
            WHERE fk.parent_object_id = c.object_id AND fk.parent_column_id = c.column_id),
           CASE WHEN EXISTS (SELECT 1 FROM sys.index_columns ic JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                             WHERE i.is_unique_constraint = 1 AND ic.object_id = c.object_id AND ic.column_id = c.column_id) THEN N'UNIQUE ' END,
           CASE WHEN c.is_identity = 1 THEN N'IDENTITY ' END,
           (SELECT TOP 1 N'CHECK ' + cc.definition + N' ' FROM sys.check_constraints cc
            WHERE cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id),
           (SELECT TOP 1 N'DEFAULT ' + dc.definition FROM sys.default_constraints dc
            WHERE dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id))) AS [Ключи и ограничения],
       ISNULL(CAST(ep.value AS NVARCHAR(400)), N'') AS Назначение
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.extended_properties ep ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
ORDER BY t.name, c.column_id;";
    }
}
