/* ===================================================================
   База данных «Киноточка» — файл 2 из 3: тестовые данные.
   Запускать после 01_create.sql. Повторный запуск очищает таблицы и
   заполняет их заново.

   Тестовые учётные записи (пароль у всех — 123456, в базе хранится
   только хеш PBKDF2-SHA256, 100 000 итераций, своя соль у каждого):
     admin@kinotochka.ru    — администратор
     cashier1@kinotochka.ru — кассир
     ivanov@example.com     — зритель
   =================================================================== */
USE Kinotochka;
GO
SET NOCOUNT ON;

-- очистка в обратном порядке связей
DELETE FROM dbo.Reviews;
DELETE FROM dbo.Payments;
DELETE FROM dbo.Tickets;
DELETE FROM dbo.Orders;
DELETE FROM dbo.OrderStatuses;
DELETE FROM dbo.PromoCodes;
DELETE FROM dbo.Sessions;
DELETE FROM dbo.Seats;
DELETE FROM dbo.SeatTypes;
DELETE FROM dbo.Halls;
DELETE FROM dbo.Films;
DELETE FROM dbo.Genres;
DELETE FROM dbo.Users;
DELETE FROM dbo.Roles;
GO

/* ---------- справочники ---------- */
-- ролей и статусов столько, сколько их в предметной области (3 и 6)
INSERT INTO dbo.Roles (RoleId, Code, Name) VALUES
    (1, N'VIEWER',  N'Зритель'),
    (2, N'CASHIER', N'Кассир'),
    (3, N'ADMIN',   N'Администратор');

INSERT INTO dbo.OrderStatuses (StatusId, Code, Name) VALUES
    (1, N'BOOKED',    N'Забронирован'),
    (2, N'PAYING',    N'Оплачивается'),
    (3, N'PAID',      N'Оплачен'),
    (4, N'DONE',      N'Завершён'),
    (5, N'CANCELLED', N'Отменён'),
    (6, N'REFUNDED',  N'Возвращён');

SET IDENTITY_INSERT dbo.Genres ON;
INSERT INTO dbo.Genres (GenreId, Name) VALUES
    (1, N'Драма'), (2, N'Комедия'), (3, N'Фантастика'),
    (4, N'Мультфильм'), (5, N'Триллер'), (6, N'Приключения');
SET IDENTITY_INSERT dbo.Genres OFF;

SET IDENTITY_INSERT dbo.SeatTypes ON;
INSERT INTO dbo.SeatTypes (SeatTypeId, Name, PriceFactor) VALUES
    (1, N'Стандарт', 1.00),
    (2, N'Комфорт', 1.20),
    (3, N'VIP', 1.60),
    (4, N'Диван', 1.80),
    (5, N'Для маломобильных зрителей', 0.70);
SET IDENTITY_INSERT dbo.SeatTypes OFF;

SET IDENTITY_INSERT dbo.PromoCodes ON;
INSERT INTO dbo.PromoCodes (PromoCodeId, Code, DiscountPercent, ValidUntil, IsActive) VALUES
    (1, N'STUDENT20',  20, '2026-12-31', 1),
    (2, N'FIRST10',    10, '2027-06-30', 1),
    (3, N'NEWYEAR25',  25, '2027-01-15', 1),
    (4, N'BIRTHDAY50', 50, '2026-12-31', 1),
    (5, N'AUTUMN15',   15, '2026-10-31', 0);
SET IDENTITY_INSERT dbo.PromoCodes OFF;
GO

/* ---------- пользователи (пароль 123456) ---------- */
SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, FullName, Email, Phone, PasswordHash, RoleId, IsBlocked, CreatedAt) VALUES
    (1, N'Смирнов Андрей Петрович',     N'admin@kinotochka.ru',    N'+7 900 100-00-01',
        N'PBKDF2$100000$XaIgAfe2jWh1oueO7gQR3w==$mUJFyxMEAoSb+Rj+Ca/HyMUzckTRJ6aq4EybQmaYdaQ=', 3, 0, '2026-09-01T09:00:00'),
    (2, N'Орлова Мария Сергеевна',      N'cashier1@kinotochka.ru', N'+7 900 100-00-02',
        N'PBKDF2$100000$4vQxpMw9j5pKmSSWqD80WQ==$eZujsqUEhjqYiW42l/yyb/w5DSr4j8+khdaU+oR+KMM=', 2, 0, '2026-09-01T09:10:00'),
    (3, N'Кузнецов Илья Андреевич',     N'cashier2@kinotochka.ru', N'+7 900 100-00-03',
        N'PBKDF2$100000$+Yd8/Fk159oAefS+O1MWPg==$w/sqXj2EBjgoaybjx3CD0C0WJPDQOI71Jlvm4UN/yzM=', 2, 0, '2026-09-01T09:20:00'),
    (4, N'Иванов Иван Иванович',        N'ivanov@example.com',     N'+7 900 200-00-04',
        N'PBKDF2$100000$hP5rYcUWzqj8ScWARyZuqA==$7kKAgHSnK2Ab6J4lgLIoBg7kuiz8OLPEfIOH+/BSGJs=', 1, 0, '2026-10-02T18:30:00'),
    (5, N'Петрова Анна Викторовна',     N'petrova@example.com',    NULL,
        N'PBKDF2$100000$DScUmHjxVZ+Bc/z1wkEBrQ==$PNIYhUWPj0WRSoYr1ft0k+Y0qxUQ+Hu3wLiDbGH4kig=', 1, 0, '2026-10-05T12:00:00'),
    (6, N'Сидоров Павел Олегович',      N'sidorov@example.com',    N'+7 900 200-00-06',
        N'PBKDF2$100000$P4/I3wmUCBznWQ2sfWcONw==$Vw2YUkPwKDeUfbuceivMLRhR2TKA3nDW5Rc/fKF6ZY0=', 1, 0, '2026-10-11T20:15:00'),
    (7, N'Козлова Екатерина Дмитриевна', N'kozlova@example.com',   N'+7 900 200-00-07',
        N'PBKDF2$100000$bTcYt14CxMF0DYtKv3tBoQ==$Dig0B+M1KbQ4NwrfKgNK2veXEogYuew41LeVgnwc72k=', 1, 0, '2026-10-20T08:45:00'),
    (8, N'Морозов Дмитрий Алексеевич',  N'morozov@example.com',    NULL,
        N'PBKDF2$100000$AE6DiWzwh93jjO8/tOXXKA==$KD0sIhLKbdq/LfzLvo+j6wC0taNEMCzNPuomb9wOcO8=', 1, 1, '2026-11-01T10:00:00');
SET IDENTITY_INSERT dbo.Users OFF;
GO

/* ---------- каталог ---------- */
SET IDENTITY_INSERT dbo.Films ON;
INSERT INTO dbo.Films (FilmId, Title, GenreId, DurationMin, AgeRating, Description, PosterPath, IsActive) VALUES
    (1, N'Звёздный маршрут',  3, 128, N'12+', N'Экипаж грузового корабля сбивается с курса и находит заброшенную станцию.', N'posters/star_route.jpg', 1),
    (2, N'Последний трамвай', 1, 112, N'16+', N'Ночной рейс через весь город, который меняет жизнь пятерых пассажиров.', N'posters/last_tram.jpg', 1),
    (3, N'Кот в библиотеке',  4,  88, N'0+',  N'Рыжий кот помогает маленькой читательнице найти пропавшую книгу.', N'posters/library_cat.jpg', 1),
    (4, N'Ночной звонок',     5, 104, N'18+', N'Диспетчер службы спасения получает звонок от человека, которого нет в живых.', N'posters/night_call.jpg', 1),
    (5, N'Свадьба по ошибке', 2,  96, N'12+', N'Двух гостей перепутали с женихом и невестой — и отступать уже поздно.', N'posters/wrong_wedding.jpg', 1),
    (6, N'Остров туманов',    6, 118, N'6+',  N'Школьники отправляются на поиски старого маяка на острове, которого нет на картах.', N'posters/fog_island.jpg', 1),
    (7, N'Старый маяк',       1, 101, N'12+', N'Фильм снят с проката.', NULL, 0);
SET IDENTITY_INSERT dbo.Films OFF;

SET IDENTITY_INSERT dbo.Halls ON;
INSERT INTO dbo.Halls (HallId, Number, Name, RowsCount, SeatsPerRow) VALUES
    (1, 1, N'Большой', 8, 12),
    (2, 2, N'Комфорт', 6, 10),
    (3, 3, N'VIP',     4,  6),
    (4, 4, N'Малый',   5,  8),
    (5, 5, N'IMAX',    9, 14);
SET IDENTITY_INSERT dbo.Halls OFF;
GO

-- места генерируются по размерам залов:
-- зал VIP — все места VIP; в остальных последний ряд — диваны, средние ряды — комфорт,
-- два крайних места первого ряда — для маломобильных зрителей, остальное — стандарт
WITH n AS (
    SELECT TOP (20) CAST(ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS TINYINT) AS k
    FROM sys.all_objects
)
INSERT INTO dbo.Seats (HallId, RowNum, SeatNum, SeatTypeId)
SELECT h.HallId, r.k, s.k,
       CASE
           WHEN h.Number = 3                                       THEN 3
           WHEN r.k = h.RowsCount                                  THEN 4
           WHEN r.k = 1 AND s.k IN (1, h.SeatsPerRow)              THEN 5
           WHEN r.k BETWEEN h.RowsCount / 2 AND h.RowsCount / 2 + 1 THEN 2
           ELSE 1
       END
FROM dbo.Halls h
JOIN n r ON r.k <= h.RowsCount
JOIN n s ON s.k <= h.SeatsPerRow
ORDER BY h.HallId, r.k, s.k;
GO

SET IDENTITY_INSERT dbo.Sessions ON;
INSERT INTO dbo.Sessions (SessionId, FilmId, HallId, StartsAt, Format, BasePrice) VALUES
    ( 1, 1, 1, '2026-11-12T19:00:00', N'2D',   450.00),
    ( 2, 3, 2, '2026-11-14T11:00:00', N'2D',   300.00),
    ( 3, 2, 4, '2026-11-15T20:30:00', N'2D',   400.00),
    ( 4, 1, 5, '2026-11-21T18:00:00', N'IMAX', 650.00),
    ( 5, 1, 1, '2026-11-21T21:00:00', N'3D',   500.00),
    ( 6, 3, 2, '2026-11-21T10:00:00', N'2D',   300.00),
    ( 7, 5, 4, '2026-11-21T19:30:00', N'2D',   380.00),
    ( 8, 4, 3, '2026-11-21T22:00:00', N'2D',   600.00),
    ( 9, 2, 1, '2026-11-22T17:00:00', N'2D',   420.00),
    (10, 6, 2, '2026-11-22T13:00:00', N'3D',   420.00),
    (11, 5, 5, '2026-11-23T20:00:00', N'2D',   450.00),
    (12, 4, 4, '2026-11-23T23:00:00', N'2D',   400.00);
SET IDENTITY_INSERT dbo.Sessions OFF;
GO

/* ---------- заказы ---------- */
-- статусы: 1 забронирован, 2 оплачивается, 3 оплачен, 4 завершён, 5 отменён, 6 возвращён
SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders (OrderId, UserId, CashierId, SessionId, PromoCodeId, StatusId, CreatedAt, ReservedUntil) VALUES
    ( 1,    4, NULL,  1, NULL, 4, '2026-11-10T12:00:00', '2026-11-10T12:15:00'),
    ( 2,    5, NULL,  1,    1, 4, '2026-11-11T09:00:00', '2026-11-11T09:15:00'),
    ( 3, NULL,    3,  2, NULL, 4, '2026-11-14T10:40:00', '2026-11-14T10:55:00'),
    ( 4,    6, NULL,  3, NULL, 4, '2026-11-13T19:05:00', '2026-11-13T19:20:00'),
    ( 5,    4, NULL,  4,    2, 3, '2026-11-18T14:00:00', '2026-11-18T14:15:00'),
    ( 6,    7, NULL,  5, NULL, 3, '2026-11-18T16:20:00', '2026-11-18T16:35:00'),
    ( 7,    5, NULL,  8, NULL, 6, '2026-11-17T21:00:00', '2026-11-17T21:15:00'),
    ( 8,    6, NULL,  6, NULL, 5, '2026-11-18T20:00:00', '2026-11-18T20:15:00'),
    ( 9,    7, NULL,  9, NULL, 1, '2026-11-19T09:50:00', '2026-11-19T10:05:00'),
    (10,    4, NULL,  7,    3, 2, '2026-11-19T09:55:00', '2026-11-19T10:10:00'),
    (11, NULL,    2,  4, NULL, 3, '2026-11-19T09:30:00', '2026-11-19T09:45:00'),
    (12,    8, NULL, 11,    4, 3, '2026-11-01T11:00:00', '2026-11-01T11:15:00');
SET IDENTITY_INSERT dbo.Orders OFF;
GO

-- билеты: место задаётся рядом и номером в зале сеанса,
-- цена = базовая цена сеанса × коэффициент типа места
INSERT INTO dbo.Tickets (OrderId, SeatId, Price, UsedAt)
SELECT v.OrderId, st.SeatId, CAST(s.BasePrice * ty.PriceFactor AS DECIMAL(10,2)), v.UsedAt
FROM (VALUES
    ( 1, 5,  6, CAST('2026-11-12T18:48:00' AS DATETIME2(0))),
    ( 1, 5,  7, CAST('2026-11-12T18:48:00' AS DATETIME2(0))),
    ( 2, 6, 10, CAST('2026-11-12T18:55:00' AS DATETIME2(0))),
    ( 3, 3,  4, CAST('2026-11-14T10:58:00' AS DATETIME2(0))),
    ( 3, 3,  5, CAST('2026-11-14T10:58:00' AS DATETIME2(0))),
    ( 3, 3,  6, CAST('2026-11-14T10:58:00' AS DATETIME2(0))),
    ( 4, 2,  3, CAST('2026-11-15T20:20:00' AS DATETIME2(0))),
    ( 4, 2,  4, NULL),
    ( 5, 5,  7, NULL), ( 5, 5,  8, NULL),
    ( 6, 8,  5, NULL), ( 6, 8,  6, NULL),
    ( 7, 2,  3, NULL), ( 7, 2,  4, NULL),
    ( 8, 4,  5, NULL),
    ( 9, 4,  6, NULL), ( 9, 4,  7, NULL),
    (10, 3,  4, NULL),
    (11, 3,  5, NULL), (11, 3,  6, NULL), (11, 3,  7, NULL), (11, 3,  8, NULL),
    (12, 6,  9, NULL), (12, 6, 10, NULL)
) AS v (OrderId, RowNum, SeatNum, UsedAt)
JOIN dbo.Orders    o  ON o.OrderId = v.OrderId
JOIN dbo.Sessions  s  ON s.SessionId = o.SessionId
JOIN dbo.Seats     st ON st.HallId = s.HallId AND st.RowNum = v.RowNum AND st.SeatNum = v.SeatNum
JOIN dbo.SeatTypes ty ON ty.SeatTypeId = st.SeatTypeId
ORDER BY v.OrderId, v.RowNum, v.SeatNum;
GO

-- платежи: сумма берётся из представления vOrderTotals (стоимость билетов минус скидка)
SET IDENTITY_INSERT dbo.Payments ON;
INSERT INTO dbo.Payments (PaymentId, OrderId, Kind, Amount, BankTransactionId, IsSuccessful, CreatedAt)
SELECT v.PaymentId, v.OrderId, v.Kind, t.Total, v.Tx, v.Ok, v.CreatedAt
FROM (VALUES
    ( 1,  1, N'PAYMENT', N'BANK-2026-000101', 1, CAST('2026-11-10T12:03:00' AS DATETIME2(0))),
    ( 2,  2, N'PAYMENT', N'BANK-2026-000102', 1, CAST('2026-11-11T09:04:00' AS DATETIME2(0))),
    ( 3,  3, N'PAYMENT', N'CASH-2026-000001', 1, CAST('2026-11-14T10:42:00' AS DATETIME2(0))),
    ( 4,  4, N'PAYMENT', N'BANK-2026-000103', 1, CAST('2026-11-13T19:08:00' AS DATETIME2(0))),
    ( 5,  5, N'PAYMENT', N'BANK-2026-000104', 1, CAST('2026-11-18T14:05:00' AS DATETIME2(0))),
    ( 6,  6, N'PAYMENT', N'BANK-2026-000105', 0, CAST('2026-11-18T16:24:00' AS DATETIME2(0))),
    ( 7,  6, N'PAYMENT', N'BANK-2026-000106', 1, CAST('2026-11-18T16:27:00' AS DATETIME2(0))),
    ( 8,  7, N'PAYMENT', N'BANK-2026-000107', 1, CAST('2026-11-17T21:06:00' AS DATETIME2(0))),
    ( 9,  7, N'REFUND',  N'BANK-2026-000108', 1, CAST('2026-11-19T08:30:00' AS DATETIME2(0))),
    (10,  8, N'PAYMENT', N'BANK-2026-000109', 0, CAST('2026-11-18T20:09:00' AS DATETIME2(0))),
    (11, 11, N'PAYMENT', N'CASH-2026-000002', 1, CAST('2026-11-19T09:33:00' AS DATETIME2(0))),
    (12, 12, N'PAYMENT', N'BANK-2026-000110', 1, CAST('2026-11-01T11:06:00' AS DATETIME2(0)))
) AS v (PaymentId, OrderId, Kind, Tx, Ok, CreatedAt)
JOIN dbo.vOrderTotals t ON t.OrderId = v.OrderId;
SET IDENTITY_INSERT dbo.Payments OFF;
GO

/* ---------- отзывы ---------- */
SET IDENTITY_INSERT dbo.Reviews ON;
INSERT INTO dbo.Reviews (ReviewId, UserId, FilmId, Rating, Text, CreatedAt) VALUES
    (1, 4, 1, 5, N'Отличная фантастика, смотрели в IMAX — звук потрясающий.', '2026-11-12T22:00:00'),
    (2, 5, 1, 4, N'Красиво, но середина немного затянута.',                    '2026-11-13T10:15:00'),
    (3, 6, 2, 5, N'Сильная драма, актёры играют прекрасно.',                   '2026-11-15T23:10:00'),
    (4, 7, 3, 5, N'Ребёнок в восторге, взрослым тоже понравилось.',             '2026-11-14T13:30:00'),
    (5, 8, 1, 3, N'Ожидал большего от сюжета.',                                '2026-11-13T19:45:00'),
    (6, 6, 3, 4, N'Добрый мультфильм для всей семьи.',                         '2026-11-14T14:00:00');
SET IDENTITY_INSERT dbo.Reviews OFF;
GO

/* ---------- итог: число записей в каждой таблице ---------- */
SELECT N'Roles' AS TableName, COUNT(*) AS Rows FROM dbo.Roles
UNION ALL SELECT N'Users', COUNT(*) FROM dbo.Users
UNION ALL SELECT N'Genres', COUNT(*) FROM dbo.Genres
UNION ALL SELECT N'Films', COUNT(*) FROM dbo.Films
UNION ALL SELECT N'Halls', COUNT(*) FROM dbo.Halls
UNION ALL SELECT N'SeatTypes', COUNT(*) FROM dbo.SeatTypes
UNION ALL SELECT N'Seats', COUNT(*) FROM dbo.Seats
UNION ALL SELECT N'Sessions', COUNT(*) FROM dbo.Sessions
UNION ALL SELECT N'PromoCodes', COUNT(*) FROM dbo.PromoCodes
UNION ALL SELECT N'OrderStatuses', COUNT(*) FROM dbo.OrderStatuses
UNION ALL SELECT N'Orders', COUNT(*) FROM dbo.Orders
UNION ALL SELECT N'Tickets', COUNT(*) FROM dbo.Tickets
UNION ALL SELECT N'Payments', COUNT(*) FROM dbo.Payments
UNION ALL SELECT N'Reviews', COUNT(*) FROM dbo.Reviews;
GO
