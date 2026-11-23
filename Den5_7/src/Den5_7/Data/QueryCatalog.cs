using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Den5_7.Data
{
    /// <summary>Тринадцать запросов дня 5 (те же, что в sql/05_queries.sql), выполняемые из приложения.</summary>
    public static class QueryCatalog
    {
        public sealed class Item
        {
            public string Title;
            public string Kind;
            public string Sql;
            /// <summary>Параметры из полей формы: текст поиска, дата «с», дата «по».</summary>
            public Func<string, DateTime, DateTime, SqlParameter[]> Parameters;
        }

        public static readonly List<Item> Items = new List<Item>
        {
            new Item
            {
                Title = "Фильмы длиннее 100 минут", Kind = "SELECT, WHERE, ORDER BY",
                Sql = "SELECT Название AS Фильм, ДлительностьМин AS Минут, ВозрастнойРейтинг AS Возраст FROM dbo.Фильмы " +
                      "WHERE Активен = 1 AND ДлительностьМин > 100 ORDER BY ДлительностьМин DESC;"
            },
            new Item
            {
                Title = "Фильмы и жанры", Kind = "JOIN",
                Sql = "SELECT f.Название AS Фильм, g.Название AS Жанр, f.ВозрастнойРейтинг AS Возраст FROM dbo.Фильмы f " +
                      "JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра ORDER BY g.Название, f.Название;"
            },
            new Item
            {
                Title = "Фильмов в каждом жанре", Kind = "LEFT JOIN, GROUP BY, COUNT",
                Sql = "SELECT g.Название AS Жанр, COUNT(f.КодФильма) AS Фильмов FROM dbo.Жанры g " +
                      "LEFT JOIN dbo.Фильмы f ON f.КодЖанра = g.КодЖанра GROUP BY g.Название ORDER BY Фильмов DESC, Жанр;"
            },
            new Item
            {
                Title = "Афиша (представление)", Kind = "VIEW Афиша",
                Sql = "SELECT FORMAT(НачалоСеанса, 'dd.MM HH:mm') AS Сеанс, Фильм, Зал, Формат, Свободно FROM dbo.Афиша ORDER BY НачалоСеанса;"
            },
            new Item
            {
                Title = "Выручка по фильмам (представление)", Kind = "VIEW ВыручкаПоФильмам",
                Sql = "SELECT Фильм, Оплачено, Возвращено, Выручка FROM dbo.ВыручкаПоФильмам WHERE Оплачено > 0 ORDER BY Выручка DESC;"
            },
            new Item
            {
                Title = "Заказы по статусам", Kind = "GROUP BY, SUM, AVG",
                Sql = "SELECT st.Название AS Статус, COUNT(*) AS Заказов, SUM(s.Итого) AS Сумма, CAST(AVG(s.Итого) AS DECIMAL(10,2)) AS СреднийЧек " +
                      "FROM dbo.Заказы o JOIN dbo.СтатусыЗаказа st ON st.КодСтатуса = o.КодСтатуса " +
                      "JOIN dbo.СуммыЗаказов s ON s.КодЗаказа = o.КодЗаказа GROUP BY st.КодСтатуса, st.Название ORDER BY st.КодСтатуса;"
            },
            new Item
            {
                Title = "Топ-3 зрителя по сумме покупок", Kind = "JOIN, GROUP BY, TOP",
                Sql = "SELECT TOP (3) u.ФИО AS Зритель, COUNT(*) AS Заказов, SUM(s.Итого) AS Сумма FROM dbo.Заказы o " +
                      "JOIN dbo.Пользователи u ON u.КодПользователя = o.КодПользователя JOIN dbo.СуммыЗаказов s ON s.КодЗаказа = o.КодЗаказа " +
                      "WHERE o.КодСтатуса IN (3, 4) GROUP BY u.ФИО ORDER BY Сумма DESC;"
            },
            new Item
            {
                Title = "Средняя оценка фильмов", Kind = "AVG, HAVING",
                Sql = "SELECT f.Название AS Фильм, COUNT(*) AS Отзывов, CAST(AVG(CAST(r.Оценка AS DECIMAL(3,2))) AS DECIMAL(3,2)) AS Оценка " +
                      "FROM dbo.Отзывы r JOIN dbo.Фильмы f ON f.КодФильма = r.КодФильма GROUP BY f.Название HAVING COUNT(*) >= 2 ORDER BY Оценка DESC;"
            },
            new Item
            {
                Title = "Сеансы без заказов", Kind = "NOT EXISTS",
                Sql = "SELECT FORMAT(s.НачалоСеанса, 'dd.MM HH:mm') AS Сеанс, f.Название AS Фильм, h.Название AS Зал FROM dbo.Сеансы s " +
                      "JOIN dbo.Фильмы f ON f.КодФильма = s.КодФильма JOIN dbo.Залы h ON h.КодЗала = s.КодЗала " +
                      "WHERE NOT EXISTS (SELECT 1 FROM dbo.Заказы o WHERE o.КодСеанса = s.КодСеанса) ORDER BY s.НачалоСеанса;"
            },
            new Item
            {
                Title = "Последние действия в журнале", Kind = "LEFT JOIN, TOP",
                Sql = "SELECT TOP (10) FORMAT(j.ВремяДействия, 'dd.MM.yyyy HH:mm') AS Время, ISNULL(u.ФИО, N'—') AS Пользователь, j.Действие " +
                      "FROM dbo.ЖурналДействий j LEFT JOIN dbo.Пользователи u ON u.КодПользователя = j.КодПользователя ORDER BY j.ВремяДействия DESC, j.КодЗаписи DESC;"
            },
            new Item
            {
                Title = "Поиск фильма (параметр)", Kind = "параметр @Поиск",
                Sql = "SELECT f.Название AS Фильм, g.Название AS Жанр FROM dbo.Фильмы f JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра " +
                      "WHERE f.Название LIKE N'%' + @Поиск + N'%' ORDER BY f.Название;",
                Parameters = (text, from, to) => new[] { new SqlParameter("@Поиск", SqlDbType.NVarChar, 100) { Value = Db.EscapeLike(text ?? "") } }
            },
            new Item
            {
                Title = "Выручка по дням за период (параметры)", Kind = "параметры @С, @По",
                Sql = "SELECT FORMAT(CAST(p.ДатаСоздания AS DATE), 'dd.MM.yyyy') AS День, " +
                      "SUM(CASE WHEN p.ВидОперации = N'ОПЛАТА' THEN p.Сумма ELSE -p.Сумма END) AS Выручка, COUNT(*) AS Операций " +
                      "FROM dbo.Платежи p WHERE p.Успешно = 1 AND p.ДатаСоздания >= @С AND p.ДатаСоздания < DATEADD(DAY, 1, @По) " +
                      "GROUP BY CAST(p.ДатаСоздания AS DATE) ORDER BY CAST(p.ДатаСоздания AS DATE);",
                Parameters = (text, from, to) => new[]
                {
                    new SqlParameter("@С", SqlDbType.Date) { Value = from },
                    new SqlParameter("@По", SqlDbType.Date) { Value = to }
                }
            },
            new Item
            {
                Title = "Зрители и их профили (связь 1:1)", Kind = "JOIN по ключу 1:1",
                Sql = "SELECT u.ФИО AS Зритель, u.ЭлПочта, ISNULL(p.Город, N'—') AS Город, " +
                      "ISNULL(FORMAT(p.ДатаРождения, 'dd.MM.yyyy'), N'—') AS ДатаРождения, " +
                      "CASE p.ПодпискаНаРассылку WHEN 1 THEN N'да' ELSE N'нет' END AS Рассылка " +
                      "FROM dbo.Пользователи u JOIN dbo.ПрофилиЗрителей p ON p.КодПользователя = u.КодПользователя ORDER BY u.ФИО;"
            }
        };
    }
}
