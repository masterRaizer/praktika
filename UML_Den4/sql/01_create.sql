/* ===================================================================
   База данных «Киноточка» (MS SQL Server) — день 4 учебной практики
   Файл 1 из 3: создание базы, таблиц, ограничений, триггера и представления.

   Как запустить: SQL Server Management Studio → File → Open → File →
   01_create.sql → Execute (F5). Затем так же 02_data.sql и 03_queries.sql.
   Повторный запуск пересоздаёт таблицы (данные удаляются).
   Проверено на SQL Server 2022; подходит для SQL Server 2016 и новее
   (в том числе LocalDB из Visual Studio).
   =================================================================== */
USE master;
GO
IF DB_ID(N'Kinotochka') IS NULL
    CREATE DATABASE Kinotochka;
GO
USE Kinotochka;
GO

-- удаление в обратном порядке связей (для повторного запуска)
DROP VIEW  IF EXISTS dbo.vOrderTotals;
DROP TABLE IF EXISTS dbo.Reviews;
DROP TABLE IF EXISTS dbo.Payments;
DROP TABLE IF EXISTS dbo.Tickets;
DROP TABLE IF EXISTS dbo.Orders;
DROP TABLE IF EXISTS dbo.OrderStatuses;
DROP TABLE IF EXISTS dbo.PromoCodes;
DROP TABLE IF EXISTS dbo.Sessions;
DROP TABLE IF EXISTS dbo.Seats;
DROP TABLE IF EXISTS dbo.SeatTypes;
DROP TABLE IF EXISTS dbo.Halls;
DROP TABLE IF EXISTS dbo.Films;
DROP TABLE IF EXISTS dbo.Genres;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.Roles;
GO

/* ---------- справочники ---------- */

-- Роли пользователей: зритель, кассир, администратор
CREATE TABLE dbo.Roles (
    RoleId INT          NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Code   NVARCHAR(20) NOT NULL CONSTRAINT UQ_Roles_Code UNIQUE,
    Name   NVARCHAR(50) NOT NULL
);

-- Жанры фильмов
CREATE TABLE dbo.Genres (
    GenreId INT IDENTITY(1,1) CONSTRAINT PK_Genres PRIMARY KEY,
    Name    NVARCHAR(50) NOT NULL CONSTRAINT UQ_Genres_Name UNIQUE
);

-- Типы мест и наценка (коэффициент к базовой цене сеанса)
CREATE TABLE dbo.SeatTypes (
    SeatTypeId  INT IDENTITY(1,1) CONSTRAINT PK_SeatTypes PRIMARY KEY,
    Name        NVARCHAR(50)  NOT NULL CONSTRAINT UQ_SeatTypes_Name UNIQUE,
    PriceFactor DECIMAL(4,2)  NOT NULL CONSTRAINT CK_SeatTypes_PriceFactor CHECK (PriceFactor > 0)
);

-- Статусы заказа (совпадают с состояниями на диаграмме состояний дня 2)
CREATE TABLE dbo.OrderStatuses (
    StatusId INT          NOT NULL CONSTRAINT PK_OrderStatuses PRIMARY KEY,
    Code     NVARCHAR(20) NOT NULL CONSTRAINT UQ_OrderStatuses_Code UNIQUE,
    Name     NVARCHAR(50) NOT NULL
);

-- Промокоды
CREATE TABLE dbo.PromoCodes (
    PromoCodeId     INT IDENTITY(1,1) CONSTRAINT PK_PromoCodes PRIMARY KEY,
    Code            NVARCHAR(30) NOT NULL CONSTRAINT UQ_PromoCodes_Code UNIQUE,
    DiscountPercent TINYINT      NOT NULL CONSTRAINT CK_PromoCodes_Discount CHECK (DiscountPercent BETWEEN 1 AND 100),
    ValidUntil      DATE         NOT NULL,
    IsActive        BIT          NOT NULL CONSTRAINT DF_PromoCodes_IsActive DEFAULT 1
);

/* ---------- пользователи ---------- */

-- Пароль хранится только как хеш PBKDF2-SHA256 в формате
-- PBKDF2$<итерации>$<соль Base64>$<хеш Base64>
CREATE TABLE dbo.Users (
    UserId       INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    FullName     NVARCHAR(100) NOT NULL,
    Email        NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
    Phone        NVARCHAR(20)  NULL,
    PasswordHash NVARCHAR(200) NOT NULL,
    RoleId       INT           NOT NULL CONSTRAINT FK_Users_Roles REFERENCES dbo.Roles (RoleId),
    IsBlocked    BIT           NOT NULL CONSTRAINT DF_Users_IsBlocked DEFAULT 0,
    CreatedAt    DATETIME2(0)  NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Users_Email CHECK (Email LIKE N'_%@_%._%')
);

/* ---------- каталог и расписание ---------- */

CREATE TABLE dbo.Films (
    FilmId      INT IDENTITY(1,1) CONSTRAINT PK_Films PRIMARY KEY,
    Title       NVARCHAR(150) NOT NULL,
    GenreId     INT           NOT NULL CONSTRAINT FK_Films_Genres REFERENCES dbo.Genres (GenreId),
    DurationMin SMALLINT      NOT NULL CONSTRAINT CK_Films_Duration CHECK (DurationMin BETWEEN 1 AND 600),
    AgeRating   NVARCHAR(3)   NOT NULL CONSTRAINT CK_Films_AgeRating CHECK (AgeRating IN (N'0+', N'6+', N'12+', N'16+', N'18+')),
    Description NVARCHAR(1000) NULL,
    PosterPath  NVARCHAR(260) NULL,
    IsActive    BIT           NOT NULL CONSTRAINT DF_Films_IsActive DEFAULT 1
);

CREATE TABLE dbo.Halls (
    HallId      INT IDENTITY(1,1) CONSTRAINT PK_Halls PRIMARY KEY,
    Number      TINYINT      NOT NULL CONSTRAINT UQ_Halls_Number UNIQUE,
    Name        NVARCHAR(50) NOT NULL,
    RowsCount   TINYINT      NOT NULL CONSTRAINT CK_Halls_Rows  CHECK (RowsCount > 0),
    SeatsPerRow TINYINT      NOT NULL CONSTRAINT CK_Halls_Seats CHECK (SeatsPerRow > 0)
);

-- Места зала; одно место (ряд + номер) в зале встречается один раз
CREATE TABLE dbo.Seats (
    SeatId     INT IDENTITY(1,1) CONSTRAINT PK_Seats PRIMARY KEY,
    HallId     INT     NOT NULL CONSTRAINT FK_Seats_Halls REFERENCES dbo.Halls (HallId),
    RowNum     TINYINT NOT NULL CONSTRAINT CK_Seats_Row  CHECK (RowNum > 0),
    SeatNum    TINYINT NOT NULL CONSTRAINT CK_Seats_Seat CHECK (SeatNum > 0),
    SeatTypeId INT     NOT NULL CONSTRAINT FK_Seats_SeatTypes REFERENCES dbo.SeatTypes (SeatTypeId),
    CONSTRAINT UQ_Seats_HallRowSeat UNIQUE (HallId, RowNum, SeatNum)
);

-- Сеансы; в одном зале не может быть двух сеансов с одинаковым временем начала
CREATE TABLE dbo.Sessions (
    SessionId INT IDENTITY(1,1) CONSTRAINT PK_Sessions PRIMARY KEY,
    FilmId    INT           NOT NULL CONSTRAINT FK_Sessions_Films REFERENCES dbo.Films (FilmId),
    HallId    INT           NOT NULL CONSTRAINT FK_Sessions_Halls REFERENCES dbo.Halls (HallId),
    StartsAt  DATETIME2(0)  NOT NULL,
    Format    NVARCHAR(10)  NOT NULL CONSTRAINT CK_Sessions_Format CHECK (Format IN (N'2D', N'3D', N'IMAX')),
    BasePrice DECIMAL(10,2) NOT NULL CONSTRAINT CK_Sessions_Price CHECK (BasePrice > 0),
    CONSTRAINT UQ_Sessions_HallStart UNIQUE (HallId, StartsAt)
);

/* ---------- продажи ---------- */

-- Заказ. Покупатель (UserId) пуст при продаже в кассе без аккаунта,
-- кассир (CashierId) пуст при покупке на сайте; хотя бы одно из полей заполнено.
CREATE TABLE dbo.Orders (
    OrderId       INT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    UserId        INT          NULL CONSTRAINT FK_Orders_Users    REFERENCES dbo.Users (UserId),
    CashierId     INT          NULL CONSTRAINT FK_Orders_Cashiers REFERENCES dbo.Users (UserId),
    SessionId     INT          NOT NULL CONSTRAINT FK_Orders_Sessions REFERENCES dbo.Sessions (SessionId),
    PromoCodeId   INT          NULL CONSTRAINT FK_Orders_PromoCodes REFERENCES dbo.PromoCodes (PromoCodeId),
    StatusId      INT          NOT NULL CONSTRAINT FK_Orders_Statuses REFERENCES dbo.OrderStatuses (StatusId),
    CreatedAt     DATETIME2(0) NOT NULL CONSTRAINT DF_Orders_CreatedAt DEFAULT SYSDATETIME(),
    ReservedUntil DATETIME2(0) NOT NULL,
    CONSTRAINT CK_Orders_Buyer CHECK (UserId IS NOT NULL OR CashierId IS NOT NULL),
    CONSTRAINT CK_Orders_Reserve CHECK (ReservedUntil > CreatedAt)
);

-- Билет: одно место в заказе. Price — цена на момент продажи (не пересчитывается
-- при смене цен). При удалении заказа его билеты удаляются (композиция).
CREATE TABLE dbo.Tickets (
    TicketId INT IDENTITY(1,1) CONSTRAINT PK_Tickets PRIMARY KEY,
    OrderId  INT              NOT NULL CONSTRAINT FK_Tickets_Orders REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,
    SeatId   INT              NOT NULL CONSTRAINT FK_Tickets_Seats  REFERENCES dbo.Seats (SeatId),
    Price    DECIMAL(10,2)    NOT NULL CONSTRAINT CK_Tickets_Price CHECK (Price >= 0),
    QrCode   UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Tickets_QrCode DEFAULT NEWID() CONSTRAINT UQ_Tickets_QrCode UNIQUE,
    UsedAt   DATETIME2(0)     NULL,
    CONSTRAINT UQ_Tickets_OrderSeat UNIQUE (OrderId, SeatId)
);

-- Платёж: оплата или возврат денег по заказу
CREATE TABLE dbo.Payments (
    PaymentId         INT IDENTITY(1,1) CONSTRAINT PK_Payments PRIMARY KEY,
    OrderId           INT           NOT NULL CONSTRAINT FK_Payments_Orders REFERENCES dbo.Orders (OrderId),
    Kind              NVARCHAR(10)  NOT NULL CONSTRAINT CK_Payments_Kind CHECK (Kind IN (N'PAYMENT', N'REFUND')),
    Amount            DECIMAL(10,2) NOT NULL CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
    BankTransactionId NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Payments_BankTx UNIQUE,
    IsSuccessful      BIT           NOT NULL,
    CreatedAt         DATETIME2(0)  NOT NULL CONSTRAINT DF_Payments_CreatedAt DEFAULT SYSDATETIME()
);

-- Отзыв: один отзыв зрителя на фильм
CREATE TABLE dbo.Reviews (
    ReviewId  INT IDENTITY(1,1) CONSTRAINT PK_Reviews PRIMARY KEY,
    UserId    INT            NOT NULL CONSTRAINT FK_Reviews_Users REFERENCES dbo.Users (UserId),
    FilmId    INT            NOT NULL CONSTRAINT FK_Reviews_Films REFERENCES dbo.Films (FilmId),
    Rating    TINYINT        NOT NULL CONSTRAINT CK_Reviews_Rating CHECK (Rating BETWEEN 1 AND 5),
    Text      NVARCHAR(1000) NULL,
    CreatedAt DATETIME2(0)   NOT NULL CONSTRAINT DF_Reviews_CreatedAt DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Reviews_UserFilm UNIQUE (UserId, FilmId)
);
GO

/* ---------- индексы для частых запросов ---------- */
CREATE INDEX IX_Films_GenreId      ON dbo.Films (GenreId);
CREATE INDEX IX_Sessions_StartsAt  ON dbo.Sessions (StartsAt);
CREATE INDEX IX_Sessions_FilmId    ON dbo.Sessions (FilmId);
CREATE INDEX IX_Orders_UserId      ON dbo.Orders (UserId);
CREATE INDEX IX_Orders_SessionId   ON dbo.Orders (SessionId);
CREATE INDEX IX_Tickets_SeatId     ON dbo.Tickets (SeatId);
CREATE INDEX IX_Payments_OrderId   ON dbo.Payments (OrderId);
CREATE INDEX IX_Reviews_FilmId     ON dbo.Reviews (FilmId);
GO

/* ---------- триггер: место нельзя продать дважды ----------
   Правило «одно место на сеансе — один действующий билет» затрагивает две таблицы
   (сеанс хранится в заказе, место — в билете), поэтому его проверяет триггер, а не
   ограничение UNIQUE. Так не приходится дублировать SessionId в Tickets (это нарушило бы 3НФ).
   Действующими считаются билеты заказов со статусами ЗАБРОНИРОВАН, ОПЛАЧИВАЕТСЯ, ОПЛАЧЕН, ЗАВЕРШЁН. */
CREATE OR ALTER TRIGGER dbo.TR_Tickets_CheckSeat
ON dbo.Tickets
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- место должно быть в том же зале, где идёт сеанс
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Orders   o ON o.OrderId = i.OrderId
        JOIN dbo.Sessions s ON s.SessionId = o.SessionId
        JOIN dbo.Seats    st ON st.SeatId = i.SeatId
        WHERE st.HallId <> s.HallId
    )
    BEGIN
        ;THROW 50001, N'Место находится в другом зале, не в зале сеанса.', 1;
    END;

    -- на одно место одного сеанса может быть только один действующий билет
    IF EXISTS (
        SELECT o.SessionId, t.SeatId
        FROM dbo.Tickets t
        JOIN dbo.Orders o ON o.OrderId = t.OrderId
        JOIN dbo.OrderStatuses os ON os.StatusId = o.StatusId
        WHERE os.Code IN (N'BOOKED', N'PAYING', N'PAID', N'DONE')
          AND EXISTS (SELECT 1 FROM inserted i JOIN dbo.Orders io ON io.OrderId = i.OrderId
                      WHERE i.SeatId = t.SeatId AND io.SessionId = o.SessionId)
        GROUP BY o.SessionId, t.SeatId
        HAVING COUNT(*) > 1
    )
    BEGIN
        ;THROW 50002, N'Это место на сеанс уже продано или забронировано.', 1;
    END;
END;
GO

/* ---------- представление: суммы заказов ----------
   Итоговая сумма не хранится в Orders (её можно вычислить из билетов и промокода —
   хранение нарушило бы 3НФ), поэтому считается в представлении. */
CREATE OR ALTER VIEW dbo.vOrderTotals
AS
SELECT
    o.OrderId,
    SUM(t.Price)                                                     AS Amount,
    CAST(SUM(t.Price) * ISNULL(p.DiscountPercent, 0) / 100.0 AS DECIMAL(10,2)) AS Discount,
    CAST(SUM(t.Price) * (100 - ISNULL(p.DiscountPercent, 0)) / 100.0 AS DECIMAL(10,2)) AS Total,
    COUNT(*)                                                         AS TicketsCount
FROM dbo.Orders o
JOIN dbo.Tickets t ON t.OrderId = o.OrderId
LEFT JOIN dbo.PromoCodes p ON p.PromoCodeId = o.PromoCodeId
GROUP BY o.OrderId, p.DiscountPercent;
GO

PRINT N'База Kinotochka создана: 14 таблиц, триггер TR_Tickets_CheckSeat, представление vOrderTotals.';
GO
