/* ===================================================================
   База данных ShagShoes (MS SQL Server) — система оформления заказа обуви
   компании «Шаг». Скрипт создаёт базу, таблицы со связями и тестовые данные.

   Как запустить: SQL Server Management Studio → File → Open → File →
   ShagShoes.sql → Execute (F5). Повторный запуск пересоздаёт таблицы.
   Подходит для SQL Server 2016 и новее, в том числе LocalDB из Visual Studio.

   Даты заказов задаются относительно дня запуска скрипта, чтобы скидка
   «нет заказов в предыдущем календарном месяце» всегда была видна на примерах.
   =================================================================== */
USE master;
GO
IF DB_ID(N'ShagShoes') IS NULL
    CREATE DATABASE ShagShoes;
GO
USE ShagShoes;
GO

DROP TABLE IF EXISTS dbo.OrderItems;
DROP TABLE IF EXISTS dbo.Orders;
DROP TABLE IF EXISTS dbo.ProductSizes;
DROP TABLE IF EXISTS dbo.Products;
DROP TABLE IF EXISTS dbo.Manufacturers;
DROP TABLE IF EXISTS dbo.Categories;
DROP TABLE IF EXISTS dbo.Users;
DROP TABLE IF EXISTS dbo.Roles;
GO

/* ---------- пользователи ---------- */

-- Роли: администратор, менеджер, авторизованный клиент
CREATE TABLE dbo.Roles (
    RoleId INT          NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
    Name   NVARCHAR(50) NOT NULL CONSTRAINT UQ_Roles_Name UNIQUE
);

-- Вход выполняется только по логину (пароль по заданию не требуется)
CREATE TABLE dbo.Users (
    UserId     INT IDENTITY(1,1) CONSTRAINT PK_Users PRIMARY KEY,
    LastName   NVARCHAR(50) NOT NULL,
    FirstName  NVARCHAR(50) NOT NULL,
    Patronymic NVARCHAR(50) NULL,
    Login      NVARCHAR(50) NOT NULL CONSTRAINT UQ_Users_Login UNIQUE,
    RoleId     INT          NOT NULL CONSTRAINT FK_Users_Roles REFERENCES dbo.Roles (RoleId)
);

/* ---------- каталог ---------- */

CREATE TABLE dbo.Categories (
    CategoryId INT IDENTITY(1,1) CONSTRAINT PK_Categories PRIMARY KEY,
    Name       NVARCHAR(50) NOT NULL CONSTRAINT UQ_Categories_Name UNIQUE
);

-- Производство: фабрика и страна
CREATE TABLE dbo.Manufacturers (
    ManufacturerId INT IDENTITY(1,1) CONSTRAINT PK_Manufacturers PRIMARY KEY,
    Name           NVARCHAR(80) NOT NULL CONSTRAINT UQ_Manufacturers_Name UNIQUE,
    Country        NVARCHAR(50) NOT NULL
);

-- Модель обуви; изображение — имя файла в папке Images рядом с программой
CREATE TABLE dbo.Products (
    ProductId      INT IDENTITY(1,1) CONSTRAINT PK_Products PRIMARY KEY,
    Article        NVARCHAR(20)   NOT NULL CONSTRAINT UQ_Products_Article UNIQUE,
    Name           NVARCHAR(100)  NOT NULL,
    CategoryId     INT            NOT NULL CONSTRAINT FK_Products_Categories REFERENCES dbo.Categories (CategoryId),
    ManufacturerId INT            NOT NULL CONSTRAINT FK_Products_Manufacturers REFERENCES dbo.Manufacturers (ManufacturerId),
    Composition    NVARCHAR(200)  NOT NULL,
    Description    NVARCHAR(1000) NULL,
    Price          DECIMAL(10,2)  NOT NULL CONSTRAINT CK_Products_Price CHECK (Price > 0),
    ImageFile      NVARCHAR(100)  NULL
);

-- Товарная позиция: сочетание модели и размера с доступным для заказа количеством.
-- При удалении модели её размеры удаляются вместе с ней.
CREATE TABLE dbo.ProductSizes (
    ProductSizeId INT IDENTITY(1,1) CONSTRAINT PK_ProductSizes PRIMARY KEY,
    ProductId     INT          NOT NULL CONSTRAINT FK_ProductSizes_Products REFERENCES dbo.Products (ProductId) ON DELETE CASCADE,
    Size          DECIMAL(3,1) NOT NULL CONSTRAINT CK_ProductSizes_Size CHECK (Size BETWEEN 15 AND 50),
    Quantity      INT          NOT NULL CONSTRAINT CK_ProductSizes_Quantity CHECK (Quantity >= 0),
    CONSTRAINT UQ_ProductSizes_ProductSize UNIQUE (ProductId, Size)
);

/* ---------- заказы ---------- */

CREATE TABLE dbo.Orders (
    OrderId   INT IDENTITY(1,1) CONSTRAINT PK_Orders PRIMARY KEY,
    UserId    INT  NOT NULL CONSTRAINT FK_Orders_Users REFERENCES dbo.Users (UserId),
    OrderDate DATE NOT NULL
);

-- Позиция заказа: товарная позиция (модель + размер), количество пар и цена за пару на момент заказа.
-- Позиции удаляются вместе с заказом; товарную позицию, которая есть в заказах, удалить нельзя.
CREATE TABLE dbo.OrderItems (
    OrderItemId   INT IDENTITY(1,1) CONSTRAINT PK_OrderItems PRIMARY KEY,
    OrderId       INT           NOT NULL CONSTRAINT FK_OrderItems_Orders REFERENCES dbo.Orders (OrderId) ON DELETE CASCADE,
    ProductSizeId INT           NOT NULL CONSTRAINT FK_OrderItems_ProductSizes REFERENCES dbo.ProductSizes (ProductSizeId),
    Quantity      INT           NOT NULL CONSTRAINT CK_OrderItems_Quantity CHECK (Quantity > 0),
    UnitPrice     DECIMAL(10,2) NOT NULL CONSTRAINT CK_OrderItems_UnitPrice CHECK (UnitPrice > 0),
    CONSTRAINT UQ_OrderItems_OrderProductSize UNIQUE (OrderId, ProductSizeId)
);
GO

CREATE INDEX IX_Products_CategoryId     ON dbo.Products (CategoryId);
CREATE INDEX IX_ProductSizes_ProductId  ON dbo.ProductSizes (ProductId);
CREATE INDEX IX_Orders_OrderDate        ON dbo.Orders (OrderDate);
CREATE INDEX IX_OrderItems_ProductSizeId ON dbo.OrderItems (ProductSizeId);
GO

/* ===================================================================
   Тестовые данные
   =================================================================== */
SET NOCOUNT ON;

INSERT INTO dbo.Roles (RoleId, Name) VALUES
    (1, N'Администратор'),
    (2, N'Менеджер'),
    (3, N'Авторизированный клиент');

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, LastName, FirstName, Patronymic, Login, RoleId) VALUES
    (1, N'Никифорова', N'Весения', N'Николаевна', N'admin',    1),
    (2, N'Сазонов',    N'Руслан',  N'Германович', N'manager',  2),
    (3, N'Одинцов',    N'Серафим', N'Артёмович',  N'client1',  3),
    (4, N'Степанов',   N'Михаил',  N'Артёмович',  N'client2',  3),
    (5, N'Ворсин',     N'Пётр',    N'Евгеньевич', N'client3',  3),
    (6, N'Старикова',  N'Елена',   N'Павловна',   N'client4',  3);
SET IDENTITY_INSERT dbo.Users OFF;

SET IDENTITY_INSERT dbo.Categories ON;
INSERT INTO dbo.Categories (CategoryId, Name) VALUES
    (1, N'Кроссовки'), (2, N'Ботинки'), (3, N'Туфли'),
    (4, N'Сапоги'), (5, N'Кеды'), (6, N'Сандалии');
SET IDENTITY_INSERT dbo.Categories OFF;

SET IDENTITY_INSERT dbo.Manufacturers ON;
INSERT INTO dbo.Manufacturers (ManufacturerId, Name, Country) VALUES
    (1, N'«Северный шаг»',  N'Россия'),
    (2, N'«Уральская обувь»', N'Россия'),
    (3, N'Alpen Trek',      N'Австрия'),
    (4, N'Città Pelle',     N'Италия'),
    (5, N'Runline',         N'Вьетнам');
SET IDENTITY_INSERT dbo.Manufacturers OFF;

SET IDENTITY_INSERT dbo.Products ON;
INSERT INTO dbo.Products (ProductId, Article, Name, CategoryId, ManufacturerId, Composition, Description, Price, ImageFile) VALUES
    ( 1, N'SH-1001', N'Кроссовки «Ветер» беговые',     1, 5, N'Текстиль, сетка, подошва EVA',          N'Лёгкие беговые кроссовки с амортизирующей подошвой и дышащим верхом.', 5490.00, N'sneaker_wind.png'),
    ( 2, N'SH-1002', N'Кроссовки «Город» повседневные', 1, 1, N'Натуральная кожа, резина',              N'Классические белые кроссовки на каждый день.',                         6990.00, N'sneaker_city.png'),
    ( 3, N'SH-2001', N'Ботинки «Тайга» зимние',         2, 2, N'Натуральная кожа, натуральный мех, ТЭП', N'Тёплые ботинки для морозов до −30 °C, нескользящая подошва.',            9890.00, N'boots_taiga.png'),
    ( 4, N'SH-2002', N'Ботинки «Трекер» треккинговые',  2, 3, N'Нубук, мембрана, резина Vibram',        N'Водонепроницаемые ботинки для походов.',                               12450.00, N'boots_trek.png'),
    ( 5, N'SH-3001', N'Туфли «Милан» классические',     3, 4, N'Натуральная кожа, кожаная подошва',     N'Мужские туфли-оксфорды ручной работы.',                                11200.00, N'shoes_milan.png'),
    ( 6, N'SH-3002', N'Туфли «Лодочка» на каблуке',     3, 4, N'Лаковая кожа, полиуретан',              N'Женские туфли на каблуке 7 см.',                                       7650.00, NULL),
    ( 7, N'SH-4001', N'Сапоги «Вьюга» зимние',          4, 2, N'Замша, шерсть, ТЭП',                    N'Высокие женские сапоги с шерстяной подкладкой.',                       10990.00, N'boots_vyuga.png'),
    ( 8, N'SH-4002', N'Сапоги «Дождик» резиновые',      4, 1, N'ПВХ, текстильная подкладка',            N'Резиновые сапоги для дождливой погоды.',                               2190.00, NULL),
    ( 9, N'SH-5001', N'Кеды «Старт» парусиновые',       5, 1, N'Хлопок, резина',                        N'Парусиновые кеды на вулканизированной подошве.',                       2990.00, N'sneaker_start.png'),
    (10, N'SH-5002', N'Кеды «Квадрат» высокие',         5, 5, N'Текстиль, искусственная кожа, резина',  N'Высокие кеды с усиленным мыском.',                                     3790.00, NULL),
    (11, N'SH-6001', N'Сандалии «Бриз» летние',         6, 3, N'Натуральная кожа, пробка',              N'Сандалии с анатомической пробковой стелькой.',                         4350.00, N'sandals_breeze.png'),
    (12, N'SH-6002', N'Сандалии «Тропа» спортивные',    6, 5, N'Текстиль, неопрен, резина',             N'Спортивные сандалии на липучках.',                                     3290.00, NULL);
SET IDENTITY_INSERT dbo.Products OFF;
GO

-- размеры и остатки (суммы по модели: много, мало и «≤ 3 пар» для подсветки)
INSERT INTO dbo.ProductSizes (ProductId, Size, Quantity)
SELECT v.ProductId, v.Size, v.Quantity
FROM (VALUES
    ( 1, 39.0, 3), ( 1, 40.0, 5), ( 1, 41.0, 4), ( 1, 42.0, 2),   -- 14
    ( 2, 40.0, 1), ( 2, 41.0, 2), ( 2, 42.0, 0),                   --  3
    ( 3, 41.0, 2), ( 3, 42.0, 3), ( 3, 43.0, 2), ( 3, 44.0, 1),   --  8
    ( 4, 42.0, 1), ( 4, 43.0, 1), ( 4, 44.0, 0),                   --  2
    ( 5, 41.0, 2), ( 5, 42.0, 2), ( 5, 43.0, 1),                   --  5
    ( 6, 36.0, 3), ( 6, 37.0, 4), ( 6, 38.0, 2),                   --  9
    ( 7, 37.0, 0), ( 7, 38.0, 1), ( 7, 39.0, 0),                   --  1
    ( 8, 33.0, 4), ( 8, 35.0, 6), ( 8, 37.0, 5), ( 8, 39.0, 3),   -- 18
    ( 9, 38.0, 2), ( 9, 39.0, 2),                                  --  4
    (10, 40.0, 0), (10, 41.0, 0), (10, 42.0, 0),                   --  0
    (11, 37.0, 3), (11, 38.0, 3), (11, 39.0, 2), (11, 40.0, 1),   --  9
    (12, 39.0, 2), (12, 40.0, 3), (12, 41.0, 1)                    --  6
) AS v (ProductId, Size, Quantity)
ORDER BY v.ProductId, v.Size;
GO

-- заказы: в прошлом месяце, в текущем и два месяца назад (последние не влияют на скидку)
DECLARE @ThisMonth DATE = DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1);
DECLARE @PrevMonth DATE = DATEADD(MONTH, -1, @ThisMonth);
DECLARE @TwoMonthsAgo DATE = DATEADD(MONTH, -2, @ThisMonth);

SET IDENTITY_INSERT dbo.Orders ON;
INSERT INTO dbo.Orders (OrderId, UserId, OrderDate) VALUES
    (1, 3, DATEADD(DAY, 2,  @PrevMonth)),             -- 3-е число прошлого месяца
    (2, 4, DATEADD(DAY, 14, @PrevMonth)),
    (3, 5, EOMONTH(@PrevMonth)),                      -- последний день прошлого месяца
    (4, 6, DATEADD(DAY, 9,  @TwoMonthsAgo)),          -- два месяца назад
    (5, 3, @ThisMonth),                               -- 1-е число текущего месяца
    (6, 4, DATEADD(DAY, 20, @TwoMonthsAgo));
SET IDENTITY_INSERT dbo.Orders OFF;

-- позиции: модель задаётся артикулом и размером, цена за пару — цена модели
INSERT INTO dbo.OrderItems (OrderId, ProductSizeId, Quantity, UnitPrice)
SELECT v.OrderId, ps.ProductSizeId, v.Quantity, p.Price
FROM (VALUES
    (1, N'SH-1001', 40.0, 1),
    (1, N'SH-5001', 39.0, 1),
    (2, N'SH-2001', 42.0, 1),
    (2, N'SH-1002', 41.0, 2),
    (3, N'SH-4001', 38.0, 1),
    (3, N'SH-6001', 38.0, 1),
    (4, N'SH-3001', 42.0, 1),
    (4, N'SH-6002', 40.0, 2),
    (5, N'SH-2002', 43.0, 1),
    (5, N'SH-3002', 37.0, 1),
    (6, N'SH-4002', 35.0, 1)
) AS v (OrderId, Article, Size, Quantity)
JOIN dbo.Products p      ON p.Article = v.Article
JOIN dbo.ProductSizes ps ON ps.ProductId = p.ProductId AND ps.Size = v.Size
ORDER BY v.OrderId, v.Article;
GO

/* ---------- проверка: каталог так, как его показывает программа ---------- */
DECLARE @PrevStart DATE = DATEADD(MONTH, -1, DATEFROMPARTS(YEAR(GETDATE()), MONTH(GETDATE()), 1));
DECLARE @PrevEnd   DATE = DATEADD(MONTH, 1, @PrevStart);   -- не включительно

SELECT
    p.Article                                          AS Артикул,
    m.Name + N' | ' + p.Name                           AS [Производство | Наименование],
    c.Name                                             AS Категория,
    ISNULL(q.Total, 0)                                 AS Пар,
    CASE WHEN ISNULL(q.Total, 0) > 5 THEN N'много' ELSE N'мало' END AS Количество,
    p.Price                                            AS Цена,
    CASE WHEN o.HasOrders = 1 THEN p.Price ELSE CAST(ROUND(p.Price * 0.75, 2) AS DECIMAL(10,2)) END AS [Цена со скидкой],
    CASE WHEN ISNULL(q.Total, 0) <= 3 THEN N'подсветить' ELSE N'' END AS Подсветка
FROM dbo.Products p
JOIN dbo.Categories c    ON c.CategoryId = p.CategoryId
JOIN dbo.Manufacturers m ON m.ManufacturerId = p.ManufacturerId
OUTER APPLY (SELECT SUM(ps.Quantity) AS Total FROM dbo.ProductSizes ps WHERE ps.ProductId = p.ProductId) q
OUTER APPLY (
    SELECT CASE WHEN EXISTS (
        SELECT 1
        FROM dbo.OrderItems oi
        JOIN dbo.ProductSizes ps ON ps.ProductSizeId = oi.ProductSizeId
        JOIN dbo.Orders od       ON od.OrderId = oi.OrderId
        WHERE ps.ProductId = p.ProductId AND od.OrderDate >= @PrevStart AND od.OrderDate < @PrevEnd
    ) THEN 1 ELSE 0 END AS HasOrders
) o
ORDER BY p.ProductId;
GO
