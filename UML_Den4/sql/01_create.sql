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
IF DB_ID(N'Киноточка') IS NULL
    CREATE DATABASE Киноточка;
GO
USE Киноточка;
GO

-- удаление в обратном порядке связей (для повторного запуска)
DROP VIEW  IF EXISTS dbo.СуммыЗаказов;
DROP TABLE IF EXISTS dbo.Отзывы;
DROP TABLE IF EXISTS dbo.Платежи;
DROP TABLE IF EXISTS dbo.Билеты;
DROP TABLE IF EXISTS dbo.Заказы;
DROP TABLE IF EXISTS dbo.СтатусыЗаказа;
DROP TABLE IF EXISTS dbo.Промокоды;
DROP TABLE IF EXISTS dbo.Сеансы;
DROP TABLE IF EXISTS dbo.Места;
DROP TABLE IF EXISTS dbo.ТипыМест;
DROP TABLE IF EXISTS dbo.Залы;
DROP TABLE IF EXISTS dbo.Фильмы;
DROP TABLE IF EXISTS dbo.Жанры;
DROP TABLE IF EXISTS dbo.Пользователи;
DROP TABLE IF EXISTS dbo.Роли;
GO

/* ---------- справочники ---------- */

-- Роли пользователей: зритель, кассир, администратор
CREATE TABLE dbo.Роли (
    КодРоли     INT          NOT NULL CONSTRAINT PK_Роли PRIMARY KEY,
    Обозначение NVARCHAR(20) NOT NULL CONSTRAINT UQ_Роли_Обозначение UNIQUE,
    Название    NVARCHAR(50) NOT NULL
);

-- Жанры фильмов
CREATE TABLE dbo.Жанры (
    КодЖанра INT IDENTITY(1,1) CONSTRAINT PK_Жанры PRIMARY KEY,
    Название NVARCHAR(50) NOT NULL CONSTRAINT UQ_Жанры_Название UNIQUE
);

-- Типы мест и наценка (коэффициент к базовой цене сеанса)
CREATE TABLE dbo.ТипыМест (
    КодТипаМеста    INT IDENTITY(1,1) CONSTRAINT PK_ТипыМест PRIMARY KEY,
    Название        NVARCHAR(50)  NOT NULL CONSTRAINT UQ_ТипыМест_Название UNIQUE,
    КоэффициентЦены DECIMAL(4,2)  NOT NULL CONSTRAINT CK_ТипыМест_КоэффициентЦены CHECK (КоэффициентЦены > 0)
);

-- Статусы заказа (совпадают с состояниями на диаграмме состояний дня 2)
CREATE TABLE dbo.СтатусыЗаказа (
    КодСтатуса  INT          NOT NULL CONSTRAINT PK_СтатусыЗаказа PRIMARY KEY,
    Обозначение NVARCHAR(20) NOT NULL CONSTRAINT UQ_СтатусыЗаказа_Обозначение UNIQUE,
    Название    NVARCHAR(50) NOT NULL
);

-- Промокоды
CREATE TABLE dbo.Промокоды (
    КодПромокода  INT IDENTITY(1,1) CONSTRAINT PK_Промокоды PRIMARY KEY,
    Обозначение   NVARCHAR(30) NOT NULL CONSTRAINT UQ_Промокоды_Обозначение UNIQUE,
    ПроцентСкидки TINYINT      NOT NULL CONSTRAINT CK_Промокоды_Скидка CHECK (ПроцентСкидки BETWEEN 1 AND 100),
    ДействуетДо   DATE         NOT NULL,
    Активен       BIT          NOT NULL CONSTRAINT DF_Промокоды_Активен DEFAULT 1
);

/* ---------- пользователи ---------- */

-- Пароль хранится только как хеш PBKDF2-SHA256 в формате
-- PBKDF2$<итерации>$<соль Base64>$<хеш Base64>
CREATE TABLE dbo.Пользователи (
    КодПользователя INT IDENTITY(1,1) CONSTRAINT PK_Пользователи PRIMARY KEY,
    ФИО             NVARCHAR(100) NOT NULL,
    ЭлПочта         NVARCHAR(100) NOT NULL CONSTRAINT UQ_Пользователи_ЭлПочта UNIQUE,
    Телефон         NVARCHAR(20)  NULL,
    ХешПароля       NVARCHAR(200) NOT NULL,
    КодРоли         INT           NOT NULL CONSTRAINT FK_Пользователи_Роли REFERENCES dbo.Роли (КодРоли),
    Заблокирован    BIT           NOT NULL CONSTRAINT DF_Пользователи_Заблокирован DEFAULT 0,
    ДатаСоздания    DATETIME2(0)  NOT NULL CONSTRAINT DF_Пользователи_ДатаСоздания DEFAULT SYSDATETIME(),
    CONSTRAINT CK_Пользователи_ЭлПочта CHECK (ЭлПочта LIKE N'_%@_%._%')
);

/* ---------- каталог и расписание ---------- */

CREATE TABLE dbo.Фильмы (
    КодФильма         INT IDENTITY(1,1) CONSTRAINT PK_Фильмы PRIMARY KEY,
    Название          NVARCHAR(150) NOT NULL,
    КодЖанра          INT           NOT NULL CONSTRAINT FK_Фильмы_Жанры REFERENCES dbo.Жанры (КодЖанра),
    ДлительностьМин   SMALLINT      NOT NULL CONSTRAINT CK_Фильмы_Длительность CHECK (ДлительностьМин BETWEEN 1 AND 600),
    ВозрастнойРейтинг NVARCHAR(3)   NOT NULL CONSTRAINT CK_Фильмы_ВозрастнойРейтинг CHECK (ВозрастнойРейтинг IN (N'0+', N'6+', N'12+', N'16+', N'18+')),
    Описание          NVARCHAR(1000) NULL,
    Постер            NVARCHAR(260) NULL,
    Активен           BIT           NOT NULL CONSTRAINT DF_Фильмы_Активен DEFAULT 1
);

CREATE TABLE dbo.Залы (
    КодЗала         INT IDENTITY(1,1) CONSTRAINT PK_Залы PRIMARY KEY,
    Номер           TINYINT      NOT NULL CONSTRAINT UQ_Залы_Номер UNIQUE,
    Название        NVARCHAR(50) NOT NULL,
    КоличествоРядов TINYINT      NOT NULL CONSTRAINT CK_Залы_Записей  CHECK (КоличествоРядов > 0),
    МестВРяду       TINYINT      NOT NULL CONSTRAINT CK_Залы_Места CHECK (МестВРяду > 0)
);

-- Места зала; одно место (ряд + номер) в зале встречается один раз
CREATE TABLE dbo.Места (
    КодМеста     INT IDENTITY(1,1) CONSTRAINT PK_Места PRIMARY KEY,
    КодЗала      INT     NOT NULL CONSTRAINT FK_Места_Залы REFERENCES dbo.Залы (КодЗала),
    Ряд          TINYINT NOT NULL CONSTRAINT CK_Места_Ряд  CHECK (Ряд > 0),
    НомерМеста   TINYINT NOT NULL CONSTRAINT CK_Места_Место CHECK (НомерМеста > 0),
    КодТипаМеста INT     NOT NULL CONSTRAINT FK_Места_ТипыМест REFERENCES dbo.ТипыМест (КодТипаМеста),
    CONSTRAINT UQ_Места_ЗалРядМесто UNIQUE (КодЗала, Ряд, НомерМеста)
);

-- Сеансы; в одном зале не может быть двух сеансов с одинаковым временем начала
CREATE TABLE dbo.Сеансы (
    КодСеанса    INT IDENTITY(1,1) CONSTRAINT PK_Сеансы PRIMARY KEY,
    КодФильма    INT           NOT NULL CONSTRAINT FK_Сеансы_Фильмы REFERENCES dbo.Фильмы (КодФильма),
    КодЗала      INT           NOT NULL CONSTRAINT FK_Сеансы_Залы REFERENCES dbo.Залы (КодЗала),
    НачалоСеанса DATETIME2(0)  NOT NULL,
    Формат       NVARCHAR(10)  NOT NULL CONSTRAINT CK_Сеансы_Формат CHECK (Формат IN (N'2D', N'3D', N'IMAX')),
    БазоваяЦена  DECIMAL(10,2) NOT NULL CONSTRAINT CK_Сеансы_Цена CHECK (БазоваяЦена > 0),
    CONSTRAINT UQ_Сеансы_ЗалНачало UNIQUE (КодЗала, НачалоСеанса)
);

/* ---------- продажи ---------- */

-- Заказ. Покупатель (КодПользователя) пуст при продаже в кассе без аккаунта,
-- кассир (КодКассира) пуст при покупке на сайте; хотя бы одно из полей заполнено.
CREATE TABLE dbo.Заказы (
    КодЗаказа       INT IDENTITY(1,1) CONSTRAINT PK_Заказы PRIMARY KEY,
    КодПользователя INT          NULL CONSTRAINT FK_Заказы_Пользователи    REFERENCES dbo.Пользователи (КодПользователя),
    КодКассира      INT          NULL CONSTRAINT FK_Заказы_Кассиры REFERENCES dbo.Пользователи (КодПользователя),
    КодСеанса       INT          NOT NULL CONSTRAINT FK_Заказы_Сеансы REFERENCES dbo.Сеансы (КодСеанса),
    КодПромокода    INT          NULL CONSTRAINT FK_Заказы_Промокоды REFERENCES dbo.Промокоды (КодПромокода),
    КодСтатуса      INT          NOT NULL CONSTRAINT FK_Заказы_Статусы REFERENCES dbo.СтатусыЗаказа (КодСтатуса),
    ДатаСоздания    DATETIME2(0) NOT NULL CONSTRAINT DF_Заказы_ДатаСоздания DEFAULT SYSDATETIME(),
    БроньДо         DATETIME2(0) NOT NULL,
    CONSTRAINT CK_Заказы_Покупатель CHECK (КодПользователя IS NOT NULL OR КодКассира IS NOT NULL),
    CONSTRAINT CK_Заказы_Бронь CHECK (БроньДо > ДатаСоздания)
);

-- Билет: одно место в заказе. Цена — цена на момент продажи (не пересчитывается
-- при смене цен). При удалении заказа его билеты удаляются (композиция).
CREATE TABLE dbo.Билеты (
    КодБилета    INT IDENTITY(1,1) CONSTRAINT PK_Билеты PRIMARY KEY,
    КодЗаказа    INT              NOT NULL CONSTRAINT FK_Билеты_Заказы REFERENCES dbo.Заказы (КодЗаказа) ON DELETE CASCADE,
    КодМеста     INT              NOT NULL CONSTRAINT FK_Билеты_Места  REFERENCES dbo.Места (КодМеста),
    Цена         DECIMAL(10,2)    NOT NULL CONSTRAINT CK_Билеты_Цена CHECK (Цена >= 0),
    QRКод        UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Билеты_QRКод DEFAULT NEWID() CONSTRAINT UQ_Билеты_QRКод UNIQUE,
    ВремяПрохода DATETIME2(0)     NULL,
    CONSTRAINT UQ_Билеты_ЗаказМесто UNIQUE (КодЗаказа, КодМеста)
);

-- Платёж: оплата или возврат денег по заказу
CREATE TABLE dbo.Платежи (
    КодПлатежа      INT IDENTITY(1,1) CONSTRAINT PK_Платежи PRIMARY KEY,
    КодЗаказа       INT           NOT NULL CONSTRAINT FK_Платежи_Заказы REFERENCES dbo.Заказы (КодЗаказа),
    ВидОперации     NVARCHAR(10)  NOT NULL CONSTRAINT CK_Платежи_ВидОперации CHECK (ВидОперации IN (N'ОПЛАТА', N'ВОЗВРАТ')),
    Сумма           DECIMAL(10,2) NOT NULL CONSTRAINT CK_Платежи_Сумма CHECK (Сумма > 0),
    НомерТранзакции NVARCHAR(50)  NOT NULL CONSTRAINT UQ_Платежи_НомерТранзакции UNIQUE,
    Успешно         BIT           NOT NULL,
    ДатаСоздания    DATETIME2(0)  NOT NULL CONSTRAINT DF_Платежи_ДатаСоздания DEFAULT SYSDATETIME()
);

-- Отзыв: один отзыв зрителя на фильм
CREATE TABLE dbo.Отзывы (
    КодОтзыва       INT IDENTITY(1,1) CONSTRAINT PK_Отзывы PRIMARY KEY,
    КодПользователя INT            NOT NULL CONSTRAINT FK_Отзывы_Пользователи REFERENCES dbo.Пользователи (КодПользователя),
    КодФильма       INT            NOT NULL CONSTRAINT FK_Отзывы_Фильмы REFERENCES dbo.Фильмы (КодФильма),
    Оценка          TINYINT        NOT NULL CONSTRAINT CK_Отзывы_Оценка CHECK (Оценка BETWEEN 1 AND 5),
    ТекстОтзыва     NVARCHAR(1000) NULL,
    ДатаСоздания    DATETIME2(0)   NOT NULL CONSTRAINT DF_Отзывы_ДатаСоздания DEFAULT SYSDATETIME(),
    CONSTRAINT UQ_Отзывы_ПользовательФильм UNIQUE (КодПользователя, КодФильма)
);
GO

/* ---------- индексы для частых запросов ---------- */
CREATE INDEX IX_Фильмы_КодЖанра      ON dbo.Фильмы (КодЖанра);
CREATE INDEX IX_Сеансы_НачалоСеанса  ON dbo.Сеансы (НачалоСеанса);
CREATE INDEX IX_Сеансы_КодФильма    ON dbo.Сеансы (КодФильма);
CREATE INDEX IX_Заказы_КодПользователя      ON dbo.Заказы (КодПользователя);
CREATE INDEX IX_Заказы_КодСеанса   ON dbo.Заказы (КодСеанса);
CREATE INDEX IX_Билеты_КодМеста     ON dbo.Билеты (КодМеста);
CREATE INDEX IX_Платежи_КодЗаказа   ON dbo.Платежи (КодЗаказа);
CREATE INDEX IX_Отзывы_КодФильма     ON dbo.Отзывы (КодФильма);
GO

/* ---------- триггер: место нельзя продать дважды ----------
   Правило «одно место на сеансе — один действующий билет» затрагивает две таблицы
   (сеанс хранится в заказе, место — в билете), поэтому его проверяет триггер, а не
   ограничение UNIQUE. Так не приходится дублировать КодСеанса в Билеты (это нарушило бы 3НФ).
   Действующими считаются билеты заказов со статусами ЗАБРОНИРОВАН, ОПЛАЧИВАЕТСЯ, ОПЛАЧЕН, ЗАВЕРШЁН. */
CREATE OR ALTER TRIGGER dbo.ТР_Билеты_ПроверкаМеста
ON dbo.Билеты
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- место должно быть в том же зале, где идёт сеанс
    IF EXISTS (
        SELECT 1
        FROM inserted i
        JOIN dbo.Заказы   o ON o.КодЗаказа = i.КодЗаказа
        JOIN dbo.Сеансы s ON s.КодСеанса = o.КодСеанса
        JOIN dbo.Места    st ON st.КодМеста = i.КодМеста
        WHERE st.КодЗала <> s.КодЗала
    )
    BEGIN
        ;THROW 50001, N'Место находится в другом зале, не в зале сеанса.', 1;
    END;

    -- на одно место одного сеанса может быть только один действующий билет
    IF EXISTS (
        SELECT o.КодСеанса, t.КодМеста
        FROM dbo.Билеты t
        JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа
        JOIN dbo.СтатусыЗаказа os ON os.КодСтатуса = o.КодСтатуса
        WHERE os.Обозначение IN (N'ЗАБРОНИРОВАН', N'ОПЛАЧИВАЕТСЯ', N'ОПЛАЧЕН', N'ЗАВЕРШЁН')
          AND EXISTS (SELECT 1 FROM inserted i JOIN dbo.Заказы io ON io.КодЗаказа = i.КодЗаказа
                      WHERE i.КодМеста = t.КодМеста AND io.КодСеанса = o.КодСеанса)
        GROUP BY o.КодСеанса, t.КодМеста
        HAVING COUNT(*) > 1
    )
    BEGIN
        ;THROW 50002, N'Это место на сеанс уже продано или забронировано.', 1;
    END;
END;
GO

/* ---------- представление: суммы заказов ----------
   Итоговая сумма не хранится в Заказы (её можно вычислить из билетов и промокода —
   хранение нарушило бы 3НФ), поэтому считается в представлении. */
CREATE OR ALTER VIEW dbo.СуммыЗаказов
AS
SELECT
    o.КодЗаказа,
    SUM(t.Цена)                                                     AS Сумма,
    CAST(SUM(t.Цена) * ISNULL(p.ПроцентСкидки, 0) / 100.0 AS DECIMAL(10,2)) AS Скидка,
    CAST(SUM(t.Цена) * (100 - ISNULL(p.ПроцентСкидки, 0)) / 100.0 AS DECIMAL(10,2)) AS Итого,
    COUNT(*)                                                         AS КоличествоБилетов
FROM dbo.Заказы o
JOIN dbo.Билеты t ON t.КодЗаказа = o.КодЗаказа
LEFT JOIN dbo.Промокоды p ON p.КодПромокода = o.КодПромокода
GROUP BY o.КодЗаказа, p.ПроцентСкидки;
GO

PRINT N'База Киноточка создана: 14 таблиц, триггер ТР_Билеты_ПроверкаМеста, представление СуммыЗаказов.';
GO
