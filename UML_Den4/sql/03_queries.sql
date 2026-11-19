/* ===================================================================
   База данных «Киноточка» — файл 3 из 3: примеры запросов и проверки.
   Запускать после 01_create.sql и 02_data.sql.
   Все значения, которые в приложении вводит пользователь, вынесены
   в переменные @... — в программе они передаются как параметры запроса.
   =================================================================== */
USE Kinotochka;
GO
SET NOCOUNT ON;

/* ---------- 1. Афиша на дату: сеансы, фильмы, залы, свободные места ----------
   JOIN четырёх таблиц, фильтр по дате и активности фильма, сортировка по времени */
DECLARE @Date DATE = '2026-11-21';

SELECT
    FORMAT(s.StartsAt, 'HH:mm')                AS Начало,
    f.Title                                    AS Фильм,
    g.Name                                     AS Жанр,
    f.AgeRating                                AS Возраст,
    h.Name                                     AS Зал,
    s.Format                                   AS Формат,
    s.BasePrice                                AS Цена,
    h.RowsCount * h.SeatsPerRow - ISNULL(b.Busy, 0) AS СвободноМест
FROM dbo.Sessions s
JOIN dbo.Films  f ON f.FilmId = s.FilmId
JOIN dbo.Genres g ON g.GenreId = f.GenreId
JOIN dbo.Halls  h ON h.HallId = s.HallId
OUTER APPLY (
    SELECT COUNT(*) AS Busy
    FROM dbo.Tickets t
    JOIN dbo.Orders o ON o.OrderId = t.OrderId
    WHERE o.SessionId = s.SessionId AND o.StatusId IN (1, 2, 3, 4)
) b
WHERE CAST(s.StartsAt AS DATE) = @Date
  AND f.IsActive = 1
ORDER BY s.StartsAt;
GO

/* ---------- 2. Поиск фильма по части названия и жанру ----------
   LIKE с параметром, необязательный фильтр по жанру, сортировка по названию */
DECLARE @Query NVARCHAR(100) = N'ноч';
DECLARE @GenreId INT = NULL;   -- NULL — любой жанр

SELECT f.Title AS Фильм, g.Name AS Жанр, f.DurationMin AS Минут, f.AgeRating AS Возраст
FROM dbo.Films f
JOIN dbo.Genres g ON g.GenreId = f.GenreId
WHERE f.IsActive = 1
  AND f.Title LIKE N'%' + @Query + N'%'
  AND (@GenreId IS NULL OR f.GenreId = @GenreId)
ORDER BY f.Title;
GO

/* ---------- 3. Свободные места на сеанс с ценой по типу места ----------
   LEFT JOIN + проверка на NULL: место свободно, если на него нет действующего билета */
DECLARE @SessionId INT = 5;

SELECT
    st.RowNum                                       AS Ряд,
    ty.Name                                         AS ТипМеста,
    CAST(s.BasePrice * ty.PriceFactor AS DECIMAL(10,2)) AS Цена,
    COUNT(*)                                        AS Свободно,
    STRING_AGG(CAST(st.SeatNum AS NVARCHAR(3)), N', ') WITHIN GROUP (ORDER BY st.SeatNum) AS Места
FROM dbo.Sessions s
JOIN dbo.Seats st     ON st.HallId = s.HallId
JOIN dbo.SeatTypes ty ON ty.SeatTypeId = st.SeatTypeId
LEFT JOIN (
    SELECT t.SeatId
    FROM dbo.Tickets t
    JOIN dbo.Orders o ON o.OrderId = t.OrderId
    WHERE o.SessionId = @SessionId AND o.StatusId IN (1, 2, 3, 4)
) busy ON busy.SeatId = st.SeatId
WHERE s.SessionId = @SessionId
  AND busy.SeatId IS NULL
GROUP BY st.RowNum, ty.Name, s.BasePrice, ty.PriceFactor
ORDER BY st.RowNum, ty.Name;
GO

/* ---------- 4. «Мои билеты» зрителя ----------
   JOIN семи таблиц, сортировка по дате сеанса (новые сверху) */
DECLARE @UserId INT = 4;

SELECT
    o.OrderId                                AS Заказ,
    f.Title                                  AS Фильм,
    FORMAT(s.StartsAt, 'dd.MM.yyyy HH:mm')   AS Сеанс,
    h.Name                                   AS Зал,
    st.RowNum                                AS Ряд,
    st.SeatNum                               AS Место,
    t.Price                                  AS Цена,
    os.Name                                  AS Статус
FROM dbo.Orders o
JOIN dbo.OrderStatuses os ON os.StatusId = o.StatusId
JOIN dbo.Sessions s ON s.SessionId = o.SessionId
JOIN dbo.Films f    ON f.FilmId = s.FilmId
JOIN dbo.Halls h    ON h.HallId = s.HallId
JOIN dbo.Tickets t  ON t.OrderId = o.OrderId
JOIN dbo.Seats st   ON st.SeatId = t.SeatId
WHERE o.UserId = @UserId
ORDER BY s.StartsAt DESC, st.RowNum, st.SeatNum;
GO

/* ---------- 5. Суммы заказов через представление vOrderTotals ----------
   LEFT JOIN: у заказов из кассы нет покупателя-пользователя */
SELECT
    o.OrderId                                 AS Заказ,
    ISNULL(u.FullName, N'(продажа в кассе)')  AS Покупатель,
    os.Name                                   AS Статус,
    t.TicketsCount                            AS Билетов,
    t.Amount                                  AS Сумма,
    t.Discount                                AS Скидка,
    t.Total                                   AS Итого,
    ISNULL(pc.Code, N'—')                     AS Промокод
FROM dbo.Orders o
JOIN dbo.vOrderTotals t   ON t.OrderId = o.OrderId
JOIN dbo.OrderStatuses os ON os.StatusId = o.StatusId
LEFT JOIN dbo.Users u      ON u.UserId = o.UserId
LEFT JOIN dbo.PromoCodes pc ON pc.PromoCodeId = o.PromoCodeId
ORDER BY t.Total DESC;
GO

/* ---------- 6. Отчёт: выручка по фильмам за период ----------
   GROUP BY + SUM с учётом возвратов, сортировка по выручке */
DECLARE @From DATE = '2026-11-01', @To DATE = '2026-11-30';

SELECT
    f.Title AS Фильм,
    SUM(CASE WHEN p.Kind = N'PAYMENT' THEN p.Amount ELSE 0 END) AS Оплачено,
    SUM(CASE WHEN p.Kind = N'REFUND'  THEN p.Amount ELSE 0 END) AS Возвращено,
    SUM(CASE WHEN p.Kind = N'PAYMENT' THEN p.Amount ELSE -p.Amount END) AS Выручка
FROM dbo.Payments p
JOIN dbo.Orders o   ON o.OrderId = p.OrderId
JOIN dbo.Sessions s ON s.SessionId = o.SessionId
JOIN dbo.Films f    ON f.FilmId = s.FilmId
WHERE p.IsSuccessful = 1
  AND p.CreatedAt >= @From AND p.CreatedAt < DATEADD(DAY, 1, @To)
GROUP BY f.Title
ORDER BY Выручка DESC;
GO

/* ---------- 7. Отчёт: заполняемость сеансов ----------
   доля проданных мест от вместимости зала, сортировка по заполняемости */
SELECT
    FORMAT(s.StartsAt, 'dd.MM HH:mm') AS Сеанс,
    f.Title                           AS Фильм,
    h.Name                            AS Зал,
    COUNT(t.TicketId)                 AS Продано,
    h.RowsCount * h.SeatsPerRow       AS Мест,
    CAST(100.0 * COUNT(t.TicketId) / (h.RowsCount * h.SeatsPerRow) AS DECIMAL(5,1)) AS Процент
FROM dbo.Sessions s
JOIN dbo.Films f ON f.FilmId = s.FilmId
JOIN dbo.Halls h ON h.HallId = s.HallId
LEFT JOIN dbo.Orders o  ON o.SessionId = s.SessionId AND o.StatusId IN (3, 4)
LEFT JOIN dbo.Tickets t ON t.OrderId = o.OrderId
GROUP BY s.SessionId, s.StartsAt, f.Title, h.Name, h.RowsCount, h.SeatsPerRow
HAVING COUNT(t.TicketId) > 0
ORDER BY Процент DESC, s.StartsAt;
GO

/* ---------- 8. Рейтинг фильмов по отзывам ----------
   AVG + HAVING: только фильмы, у которых не меньше двух отзывов */
SELECT
    f.Title                                  AS Фильм,
    COUNT(r.ReviewId)                        AS Отзывов,
    CAST(AVG(CAST(r.Rating AS DECIMAL(3,2))) AS DECIMAL(3,2)) AS СредняяОценка
FROM dbo.Films f
JOIN dbo.Reviews r ON r.FilmId = f.FilmId
GROUP BY f.Title
HAVING COUNT(r.ReviewId) >= 2
ORDER BY СредняяОценка DESC;
GO

/* ---------- 9. Просроченные брони (функция 4.5 «Снять просроченные брони») ----------
   заказы в статусе «Забронирован», у которых истекло время брони */
DECLARE @Now DATETIME2(0) = '2026-11-19T10:30:00';   -- в приложении: SYSDATETIME()

SELECT o.OrderId AS Заказ, u.Email AS Зритель, o.ReservedUntil AS БроньДо,
       DATEDIFF(MINUTE, o.ReservedUntil, @Now) AS ПросроченоМинут
FROM dbo.Orders o
JOIN dbo.Users u ON u.UserId = o.UserId
WHERE o.StatusId = 1 AND o.ReservedUntil < @Now
ORDER BY o.ReservedUntil;
-- в приложении затем выполняется:
-- UPDATE dbo.Orders SET StatusId = 5 WHERE StatusId = 1 AND ReservedUntil < SYSDATETIME();
GO

/* ---------- 10. Проверка целостности ----------
   а) все внешние ключи и CHECK-ограничения включены и выполняются */
SELECT COUNT(*) AS ВнешнихКлючей,
       SUM(CAST(is_disabled AS INT) + CAST(is_not_trusted AS INT)) AS Отключённых
FROM sys.foreign_keys;

DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;   -- пустой результат = нарушений нет

/* б) «висячих» ссылок нет — каждая строка с внешним ключом ссылается на существующую запись */
SELECT N'Tickets → Orders' AS Связь, COUNT(*) AS БезРодителя
FROM dbo.Tickets t LEFT JOIN dbo.Orders o ON o.OrderId = t.OrderId WHERE o.OrderId IS NULL
UNION ALL
SELECT N'Tickets → Seats', COUNT(*)
FROM dbo.Tickets t LEFT JOIN dbo.Seats s ON s.SeatId = t.SeatId WHERE s.SeatId IS NULL
UNION ALL
SELECT N'Orders → Sessions', COUNT(*)
FROM dbo.Orders o LEFT JOIN dbo.Sessions s ON s.SessionId = o.SessionId WHERE s.SessionId IS NULL
UNION ALL
SELECT N'Payments → Orders', COUNT(*)
FROM dbo.Payments p LEFT JOIN dbo.Orders o ON o.OrderId = p.OrderId WHERE o.OrderId IS NULL;
GO

/* в) ошибочные данные отклоняются. Каждая попытка выполняется в транзакции,
   которая откатывается, — база не меняется. */
DECLARE @Tests TABLE (Проверка NVARCHAR(100), Результат NVARCHAR(300));

BEGIN TRY  -- билет на несуществующий заказ
    BEGIN TRAN; INSERT INTO dbo.Tickets (OrderId, SeatId, Price) VALUES (999, 1, 100); ROLLBACK;
    INSERT @Tests VALUES (N'Билет на несуществующий заказ', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Билет на несуществующий заказ', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): внешний ключ'));
END CATCH;

BEGIN TRY  -- повтор email
    BEGIN TRAN;
    INSERT INTO dbo.Users (FullName, Email, PasswordHash, RoleId) VALUES (N'Тест', N'ivanov@example.com', N'x', 1);
    ROLLBACK;
    INSERT @Tests VALUES (N'Второй пользователь с тем же email', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Второй пользователь с тем же email', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): UNIQUE'));
END CATCH;

BEGIN TRY  -- оценка вне диапазона 1–5
    BEGIN TRAN; INSERT INTO dbo.Reviews (UserId, FilmId, Rating) VALUES (5, 2, 7); ROLLBACK;
    INSERT @Tests VALUES (N'Отзыв с оценкой 7', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Отзыв с оценкой 7', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): CHECK'));
END CATCH;

BEGIN TRY  -- место, уже проданное на этот сеанс (ряд 5, место 7 сеанса 4 — заказ 5)
    BEGIN TRAN;
    INSERT INTO dbo.Orders (UserId, SessionId, StatusId, ReservedUntil) VALUES (6, 4, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Tickets (OrderId, SeatId, Price)
    SELECT SCOPE_IDENTITY(), SeatId, 650 FROM dbo.Seats WHERE HallId = 5 AND RowNum = 5 AND SeatNum = 7;
    ROLLBACK;
    INSERT @Tests VALUES (N'Повторная продажа занятого места', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Повторная продажа занятого места', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- место освободилось после отмены заказа 8 — его можно купить снова
    BEGIN TRAN;
    INSERT INTO dbo.Orders (UserId, SessionId, StatusId, ReservedUntil) VALUES (5, 6, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Tickets (OrderId, SeatId, Price)
    SELECT SCOPE_IDENTITY(), SeatId, 300 FROM dbo.Seats WHERE HallId = 2 AND RowNum = 4 AND SeatNum = 5;
    ROLLBACK;
    INSERT @Tests VALUES (N'Покупка места из отменённого заказа', N'принято — место свободно');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Покупка места из отменённого заказа', CONCAT(N'ОШИБКА (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- место из другого зала
    BEGIN TRAN;
    INSERT INTO dbo.Orders (UserId, SessionId, StatusId, ReservedUntil) VALUES (6, 4, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Tickets (OrderId, SeatId, Price)
    SELECT SCOPE_IDENTITY(), SeatId, 650 FROM dbo.Seats WHERE HallId = 1 AND RowNum = 1 AND SeatNum = 3;
    ROLLBACK;
    INSERT @Tests VALUES (N'Билет на место из другого зала', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Билет на место из другого зала', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- удаление фильма, на который есть сеансы
    BEGIN TRAN; DELETE FROM dbo.Films WHERE FilmId = 1; ROLLBACK;
    INSERT @Tests VALUES (N'Удаление фильма, у которого есть сеансы', N'ОШИБКА: удалено');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Tests VALUES (N'Удаление фильма, у которого есть сеансы', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): внешний ключ'));
END CATCH;

SELECT * FROM @Tests;
GO
