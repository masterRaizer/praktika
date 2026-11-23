/* ===================================================================
   База данных «Киноточка» — день 5: дополнения для приложения.
   Запускать после 01_create.sql и 02_data.sql (день 4).

   • таблица ЖурналДействий — журнал действий пользователей (функция дня 6);
   • таблица ПрофилиЗрителей — пример связи 1:1 с таблицей Пользователи;
   • роли КОНТРОЛЁР и МЕНЕДЖЕР (в справочнике ролей становится 5 записей);
   • представления Афиша и ВыручкаПоФильмам (вместе с СуммыЗаказов — три представления);
   • описания (MS_Description) всех таблиц и полей — по ним строится словарь данных.
   Повторный запуск безопасен: журнал и представления пересоздаются.
   =================================================================== */
USE Киноточка;
GO

DROP VIEW  IF EXISTS dbo.Афиша;
DROP VIEW  IF EXISTS dbo.ВыручкаПоФильмам;
DROP TABLE IF EXISTS dbo.ЖурналДействий;
DROP TABLE IF EXISTS dbo.ПрофилиЗрителей;
GO

-- Журнал действий. При удалении пользователя его записи остаются, ссылка обнуляется.
CREATE TABLE dbo.ЖурналДействий (
    КодЗаписи       INT IDENTITY(1,1) CONSTRAINT PK_ЖурналДействий PRIMARY KEY,
    КодПользователя INT           NULL CONSTRAINT FK_ЖурналДействий_Пользователи
                                       REFERENCES dbo.Пользователи (КодПользователя) ON DELETE SET NULL,
    Действие        NVARCHAR(200) NOT NULL,
    Подробности     NVARCHAR(500) NULL,
    ВремяДействия   DATETIME2(0)  NOT NULL CONSTRAINT DF_ЖурналДействий_ВремяДействия DEFAULT SYSDATETIME()
);
CREATE INDEX IX_ЖурналДействий_ВремяДействия ON dbo.ЖурналДействий (ВремяДействия);
GO

-- тестовые записи журнала
INSERT INTO dbo.ЖурналДействий (КодПользователя, Действие, Подробности, ВремяДействия) VALUES
    (1, N'Вход в систему', NULL, '2026-11-20T09:00:00'),
    (1, N'Добавление фильма', N'«Старый маяк»', '2026-11-20T09:05:00'),
    (2, N'Вход в систему', NULL, '2026-11-20T10:00:00'),
    (1, N'Изменение фильма', N'«Звёздный маршрут»: длительность 128 мин', '2026-11-20T11:30:00'),
    (1, N'Экспорт в CSV', N'Список фильмов, 7 строк', '2026-11-20T12:10:00');
GO

-- Ещё две роли сотрудников кинотеатра: теперь в каждой таблице не меньше 5 записей
INSERT INTO dbo.Роли (КодРоли, Обозначение, Название)
SELECT v.КодРоли, v.Обозначение, v.Название
FROM (VALUES (4, N'КОНТРОЛЁР', N'Контролёр (проверка билетов на входе)'),
             (5, N'МЕНЕДЖЕР',  N'Менеджер (расписание и цены)')) v (КодРоли, Обозначение, Название)
WHERE NOT EXISTS (SELECT 1 FROM dbo.Роли r WHERE r.Обозначение = v.Обозначение OR r.КодРоли = v.КодРоли);
GO

-- Профиль зрителя: связь 1:1 — первичный ключ таблицы одновременно внешний ключ на Пользователи,
-- поэтому у пользователя может быть только один профиль. Профиль удаляется вместе с пользователем.
CREATE TABLE dbo.ПрофилиЗрителей (
    КодПользователя    INT          NOT NULL CONSTRAINT PK_ПрофилиЗрителей PRIMARY KEY
                                    CONSTRAINT FK_ПрофилиЗрителей_Пользователи
                                        REFERENCES dbo.Пользователи (КодПользователя) ON DELETE CASCADE,
    ДатаРождения       DATE         NULL CONSTRAINT CK_ПрофилиЗрителей_ДатаРождения CHECK (ДатаРождения >= '1900-01-01'),
    Город              NVARCHAR(60) NULL,
    ПодпискаНаРассылку BIT          NOT NULL CONSTRAINT DF_ПрофилиЗрителей_Подписка DEFAULT 0,
    ДатаИзменения      DATETIME2(0) NOT NULL CONSTRAINT DF_ПрофилиЗрителей_ДатаИзменения DEFAULT SYSDATETIME()
);
GO

-- профили пяти зрителей (коды 4–8 из 02_data.sql)
INSERT INTO dbo.ПрофилиЗрителей (КодПользователя, ДатаРождения, Город, ПодпискаНаРассылку, ДатаИзменения) VALUES
    (4, '1990-05-14', N'Смоленск', 1, '2026-11-01T10:00:00'),
    (5, '1998-02-03', N'Смоленск', 0, '2026-11-02T12:30:00'),
    (6, NULL,         N'Вязьма',   1, '2026-11-05T18:15:00'),
    (7, '2001-09-27', N'Смоленск', 1, '2026-11-10T09:45:00'),
    (8, '1985-12-01', NULL,        0, '2026-11-12T20:00:00');
GO

/* ---------- представление 1: афиша со свободными местами ---------- */
CREATE VIEW dbo.Афиша
AS
SELECT
    s.КодСеанса,
    s.НачалоСеанса,
    f.КодФильма,
    f.Название                                   AS Фильм,
    g.Название                                   AS Жанр,
    f.ВозрастнойРейтинг,
    h.Название                                   AS Зал,
    s.Формат,
    s.БазоваяЦена,
    h.КоличествоРядов * h.МестВРяду              AS ВсегоМест,
    ISNULL(z.Занято, 0)                          AS Занято,
    h.КоличествоРядов * h.МестВРяду - ISNULL(z.Занято, 0) AS Свободно
FROM dbo.Сеансы s
JOIN dbo.Фильмы f ON f.КодФильма = s.КодФильма
JOIN dbo.Жанры  g ON g.КодЖанра = f.КодЖанра
JOIN dbo.Залы   h ON h.КодЗала = s.КодЗала
OUTER APPLY (
    SELECT COUNT(*) AS Занято
    FROM dbo.Билеты t
    JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа
    WHERE o.КодСеанса = s.КодСеанса AND o.КодСтатуса IN (1, 2, 3, 4)
) z
WHERE f.Активен = 1;
GO

/* ---------- представление 2: выручка по фильмам ---------- */
CREATE VIEW dbo.ВыручкаПоФильмам
AS
SELECT
    f.КодФильма,
    f.Название AS Фильм,
    ISNULL(SUM(CASE WHEN p.ВидОперации = N'ОПЛАТА'  AND p.Успешно = 1 THEN p.Сумма END), 0) AS Оплачено,
    ISNULL(SUM(CASE WHEN p.ВидОперации = N'ВОЗВРАТ' AND p.Успешно = 1 THEN p.Сумма END), 0) AS Возвращено,
    ISNULL(SUM(CASE WHEN p.Успешно = 1 THEN CASE WHEN p.ВидОперации = N'ОПЛАТА' THEN p.Сумма ELSE -p.Сумма END END), 0) AS Выручка
FROM dbo.Фильмы f
LEFT JOIN dbo.Сеансы s  ON s.КодФильма = f.КодФильма
LEFT JOIN dbo.Заказы o  ON o.КодСеанса = s.КодСеанса
LEFT JOIN dbo.Платежи p ON p.КодЗаказа = o.КодЗаказа
GROUP BY f.КодФильма, f.Название;
GO

/* ---------- описания таблиц и полей (словарь данных) ---------- */

-- Роли
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Роли') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Роли';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Роли пользователей системы', N'SCHEMA', N'dbo', N'TABLE', N'Роли';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Роли') AND c.name = N'КодРоли' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'КодРоли';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ роли', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'КодРоли';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Роли') AND c.name = N'Обозначение' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'Обозначение';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Код роли для программы: ЗРИТЕЛЬ, КАССИР, АДМИНИСТРАТОР, КОНТРОЛЁР, МЕНЕДЖЕР', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'Обозначение';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Роли') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название роли для пользователя', N'SCHEMA', N'dbo', N'TABLE', N'Роли', N'COLUMN', N'Название';
GO
-- Пользователи
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Пользователи') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Учётные записи зрителей и сотрудников', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'КодПользователя' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'КодПользователя';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ пользователя', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'КодПользователя';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'ФИО' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ФИО';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Фамилия, имя и отчество', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ФИО';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'ЭлПочта' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ЭлПочта';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Адрес электронной почты, он же логин; уникален', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ЭлПочта';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'Телефон' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'Телефон';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Контактный телефон (необязателен)', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'Телефон';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'ХешПароля' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ХешПароля';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Хеш пароля PBKDF2-SHA256 с солью; сам пароль не хранится', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ХешПароля';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'КодРоли' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'КодРоли';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Роль пользователя (ссылка на Роли)', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'КодРоли';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'Заблокирован' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'Заблокирован';
EXEC sys.sp_addextendedproperty N'MS_Description', N'1 — вход запрещён', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'Заблокирован';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Пользователи') AND c.name = N'ДатаСоздания' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ДатаСоздания';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время регистрации', N'SCHEMA', N'dbo', N'TABLE', N'Пользователи', N'COLUMN', N'ДатаСоздания';
GO
-- Жанры
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Жанры') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Жанры';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Справочник жанров фильмов', N'SCHEMA', N'dbo', N'TABLE', N'Жанры';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Жанры') AND c.name = N'КодЖанра' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Жанры', N'COLUMN', N'КодЖанра';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ жанра', N'SCHEMA', N'dbo', N'TABLE', N'Жанры', N'COLUMN', N'КодЖанра';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Жанры') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Жанры', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название жанра; уникально', N'SCHEMA', N'dbo', N'TABLE', N'Жанры', N'COLUMN', N'Название';
GO
-- Фильмы
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Фильмы') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Фильмы в прокате', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'КодФильма' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'КодФильма';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ фильма', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'КодФильма';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название фильма', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Название';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'КодЖанра' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'КодЖанра';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Жанр (ссылка на Жанры)', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'КодЖанра';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'ДлительностьМин' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'ДлительностьМин';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Длительность в минутах, 1–600', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'ДлительностьМин';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'ВозрастнойРейтинг' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'ВозрастнойРейтинг';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Возрастное ограничение: 0+, 6+, 12+, 16+, 18+', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'ВозрастнойРейтинг';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'Описание' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Описание';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Краткое описание сюжета (необязательно)', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Описание';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'Постер' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Постер';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Путь к файлу постера (необязательно)', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Постер';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Фильмы') AND c.name = N'Активен' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Активен';
EXEC sys.sp_addextendedproperty N'MS_Description', N'1 — фильм показывается в афише, 0 — снят с проката', N'SCHEMA', N'dbo', N'TABLE', N'Фильмы', N'COLUMN', N'Активен';
GO
-- Залы
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Залы') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Кинозалы', N'SCHEMA', N'dbo', N'TABLE', N'Залы';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Залы') AND c.name = N'КодЗала' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'КодЗала';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ зала', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'КодЗала';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Залы') AND c.name = N'Номер' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'Номер';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Номер зала; уникален', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'Номер';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Залы') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название зала', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'Название';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Залы') AND c.name = N'КоличествоРядов' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'КоличествоРядов';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Число рядов', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'КоличествоРядов';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Залы') AND c.name = N'МестВРяду' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'МестВРяду';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Число мест в ряду', N'SCHEMA', N'dbo', N'TABLE', N'Залы', N'COLUMN', N'МестВРяду';
GO
-- ТипыМест
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.ТипыМест') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Справочник типов мест и наценок', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ТипыМест') AND c.name = N'КодТипаМеста' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'КодТипаМеста';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ типа места', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'КодТипаМеста';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ТипыМест') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название типа; уникально', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'Название';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ТипыМест') AND c.name = N'КоэффициентЦены' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'КоэффициентЦены';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Множитель к базовой цене сеанса, больше 0', N'SCHEMA', N'dbo', N'TABLE', N'ТипыМест', N'COLUMN', N'КоэффициентЦены';
GO
-- Места
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Места') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Места в залах', N'SCHEMA', N'dbo', N'TABLE', N'Места';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Места') AND c.name = N'КодМеста' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодМеста';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ места', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодМеста';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Места') AND c.name = N'КодЗала' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодЗала';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Зал (ссылка на Залы)', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодЗала';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Места') AND c.name = N'Ряд' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'Ряд';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Номер ряда', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'Ряд';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Места') AND c.name = N'НомерМеста' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'НомерМеста';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Номер места в ряду', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'НомерМеста';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Места') AND c.name = N'КодТипаМеста' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодТипаМеста';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Тип места (ссылка на ТипыМест)', N'SCHEMA', N'dbo', N'TABLE', N'Места', N'COLUMN', N'КодТипаМеста';
GO
-- Сеансы
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Сеансы') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Расписание сеансов', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'КодСеанса' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодСеанса';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ сеанса', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодСеанса';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'КодФильма' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодФильма';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Фильм (ссылка на Фильмы)', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодФильма';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'КодЗала' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодЗала';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Зал (ссылка на Залы)', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'КодЗала';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'НачалоСеанса' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'НачалоСеанса';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время начала; в зале не может быть двух сеансов в одно время', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'НачалоСеанса';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'Формат' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'Формат';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Формат показа: 2D, 3D, IMAX', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'Формат';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Сеансы') AND c.name = N'БазоваяЦена' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'БазоваяЦена';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Цена стандартного места, руб.', N'SCHEMA', N'dbo', N'TABLE', N'Сеансы', N'COLUMN', N'БазоваяЦена';
GO
-- Промокоды
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Промокоды') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Промокоды на скидку', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Промокоды') AND c.name = N'КодПромокода' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'КодПромокода';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ промокода', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'КодПромокода';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Промокоды') AND c.name = N'Обозначение' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'Обозначение';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Текст промокода; уникален', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'Обозначение';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Промокоды') AND c.name = N'ПроцентСкидки' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'ПроцентСкидки';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Скидка в процентах, 1–100', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'ПроцентСкидки';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Промокоды') AND c.name = N'ДействуетДо' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'ДействуетДо';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Последний день действия', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'ДействуетДо';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Промокоды') AND c.name = N'Активен' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'Активен';
EXEC sys.sp_addextendedproperty N'MS_Description', N'1 — промокод можно применять', N'SCHEMA', N'dbo', N'TABLE', N'Промокоды', N'COLUMN', N'Активен';
GO
-- СтатусыЗаказа
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.СтатусыЗаказа') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Справочник статусов заказа', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.СтатусыЗаказа') AND c.name = N'КодСтатуса' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'КодСтатуса';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ статуса', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'КодСтатуса';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.СтатусыЗаказа') AND c.name = N'Обозначение' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'Обозначение';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Код статуса: ЗАБРОНИРОВАН, ОПЛАЧИВАЕТСЯ, ОПЛАЧЕН, ЗАВЕРШЁН, ОТМЕНЁН, ВОЗВРАЩЁН', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'Обозначение';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.СтатусыЗаказа') AND c.name = N'Название' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'Название';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Название статуса для пользователя', N'SCHEMA', N'dbo', N'TABLE', N'СтатусыЗаказа', N'COLUMN', N'Название';
GO
-- Заказы
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Заказы') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Заказы билетов', N'SCHEMA', N'dbo', N'TABLE', N'Заказы';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодЗаказа' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодЗаказа';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ заказа', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодЗаказа';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодПользователя' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодПользователя';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Покупатель (ссылка на Пользователи); пусто при продаже в кассе без аккаунта', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодПользователя';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодКассира' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодКассира';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Кассир, оформивший продажу (ссылка на Пользователи); пусто при покупке на сайте', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодКассира';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодСеанса' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодСеанса';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Сеанс (ссылка на Сеансы)', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодСеанса';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодПромокода' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодПромокода';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Применённый промокод (необязательно)', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодПромокода';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'КодСтатуса' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодСтатуса';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Текущий статус (ссылка на СтатусыЗаказа)', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'КодСтатуса';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'ДатаСоздания' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'ДатаСоздания';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время создания заказа', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'ДатаСоздания';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Заказы') AND c.name = N'БроньДо' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'БроньДо';
EXEC sys.sp_addextendedproperty N'MS_Description', N'До какого времени держится бронь мест', N'SCHEMA', N'dbo', N'TABLE', N'Заказы', N'COLUMN', N'БроньДо';
GO
-- Билеты
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Билеты') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Билеты — таблица-связка заказов и мест (N:M)', N'SCHEMA', N'dbo', N'TABLE', N'Билеты';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'КодБилета' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодБилета';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ билета', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодБилета';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'КодЗаказа' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодЗаказа';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Заказ (ссылка на Заказы; удаляется вместе с заказом)', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодЗаказа';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'КодМеста' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодМеста';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Место (ссылка на Места)', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'КодМеста';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'Цена' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'Цена';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Цена билета на момент продажи, руб.', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'Цена';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'QRКод' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'QRКод';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Уникальный код для прохода в зал', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'QRКод';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Билеты') AND c.name = N'ВремяПрохода' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'ВремяПрохода';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Когда билет отсканирован на входе; пусто — ещё не использован', N'SCHEMA', N'dbo', N'TABLE', N'Билеты', N'COLUMN', N'ВремяПрохода';
GO
-- Платежи
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Платежи') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Оплаты и возвраты денег', N'SCHEMA', N'dbo', N'TABLE', N'Платежи';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'КодПлатежа' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'КодПлатежа';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ платежа', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'КодПлатежа';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'КодЗаказа' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'КодЗаказа';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Заказ (ссылка на Заказы)', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'КодЗаказа';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'ВидОперации' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'ВидОперации';
EXEC sys.sp_addextendedproperty N'MS_Description', N'ОПЛАТА или ВОЗВРАТ', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'ВидОперации';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'Сумма' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'Сумма';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Сумма операции, руб., больше 0', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'Сумма';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'НомерТранзакции' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'НомерТранзакции';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Номер операции в банке или кассе; уникален', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'НомерТранзакции';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'Успешно' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'Успешно';
EXEC sys.sp_addextendedproperty N'MS_Description', N'1 — операция прошла', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'Успешно';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Платежи') AND c.name = N'ДатаСоздания' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'ДатаСоздания';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время операции', N'SCHEMA', N'dbo', N'TABLE', N'Платежи', N'COLUMN', N'ДатаСоздания';
GO
-- Отзывы
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.Отзывы') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Отзывы зрителей о фильмах', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'КодОтзыва' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодОтзыва';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ отзыва', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодОтзыва';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'КодПользователя' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодПользователя';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Автор (ссылка на Пользователи)', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодПользователя';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'КодФильма' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодФильма';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Фильм (ссылка на Фильмы)', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'КодФильма';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'Оценка' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'Оценка';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Оценка от 1 до 5', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'Оценка';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'ТекстОтзыва' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'ТекстОтзыва';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Текст отзыва (необязателен)', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'ТекстОтзыва';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.Отзывы') AND c.name = N'ДатаСоздания' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'ДатаСоздания';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время отзыва', N'SCHEMA', N'dbo', N'TABLE', N'Отзывы', N'COLUMN', N'ДатаСоздания';
GO
-- ПрофилиЗрителей
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дополнительные данные зрителя — связь 1:1 с Пользователи (первичный ключ = внешний ключ)', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND c.name = N'КодПользователя' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'КодПользователя';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ и одновременно ссылка на Пользователи: у пользователя не больше одного профиля', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'КодПользователя';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND c.name = N'ДатаРождения' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ДатаРождения';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата рождения (необязательна); не раньше 1900 года', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ДатаРождения';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND c.name = N'Город' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'Город';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Город проживания (необязателен)', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'Город';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND c.name = N'ПодпискаНаРассылку' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ПодпискаНаРассылку';
EXEC sys.sp_addextendedproperty N'MS_Description', N'1 — зритель согласен получать афишу на e-mail', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ПодпискаНаРассылку';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ПрофилиЗрителей') AND c.name = N'ДатаИзменения' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ДатаИзменения';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Когда профиль изменён в последний раз', N'SCHEMA', N'dbo', N'TABLE', N'ПрофилиЗрителей', N'COLUMN', N'ДатаИзменения';
GO
-- ЖурналДействий
IF EXISTS (SELECT 1 FROM sys.extended_properties WHERE major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND minor_id = 0 AND name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Журнал действий пользователей в приложении', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND c.name = N'КодЗаписи' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'КодЗаписи';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Первичный ключ записи журнала', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'КодЗаписи';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND c.name = N'КодПользователя' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'КодПользователя';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Кто выполнил действие (ссылка на Пользователи); пусто — неизвестный пользователь', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'КодПользователя';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND c.name = N'Действие' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'Действие';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Что сделано: вход, добавление, изменение, удаление, экспорт', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'Действие';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND c.name = N'Подробности' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'Подробности';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Подробности действия (необязательно)', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'Подробности';
IF EXISTS (SELECT 1 FROM sys.extended_properties ep JOIN sys.columns c ON c.object_id = ep.major_id AND c.column_id = ep.minor_id WHERE ep.major_id = OBJECT_ID(N'dbo.ЖурналДействий') AND c.name = N'ВремяДействия' AND ep.name = N'MS_Description')
    EXEC sys.sp_dropextendedproperty N'MS_Description', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'ВремяДействия';
EXEC sys.sp_addextendedproperty N'MS_Description', N'Дата и время действия', N'SCHEMA', N'dbo', N'TABLE', N'ЖурналДействий', N'COLUMN', N'ВремяДействия';
GO

PRINT N'Дополнения дня 5 установлены: таблицы ЖурналДействий и ПрофилиЗрителей, представления Афиша и ВыручкаПоФильмам, описания всех таблиц и полей.';
GO
