/* ===================================================================
   База данных «Киноточка» — файл 3 из 3: примеры запросов и проверки.
   Запускать после 01_create.sql и 02_data.sql.
   Все значения, которые в приложении вводит пользователь, вынесены
   в переменные @... — в программе они передаются как параметры запроса.
   =================================================================== */
USE Киноточка;
GO
SET NOCOUNT ON;

/* ---------- 1. Афиша на дату: сеансы, фильмы, залы, свободные места ----------
   JOIN четырёх таблиц, фильтр по дате и активности фильма, сортировка по времени */
DECLARE @Дата DATE = '2026-11-21';

SELECT
    FORMAT(s.НачалоСеанса, 'HH:mm')                AS Начало,
    f.Название                                    AS Фильм,
    g.Название                                     AS Жанр,
    f.ВозрастнойРейтинг                                AS Возраст,
    h.Название                                     AS Зал,
    s.Формат                                   AS Формат,
    s.БазоваяЦена                                AS Цена,
    h.КоличествоРядов * h.МестВРяду - ISNULL(b.Занято, 0) AS СвободноМест
FROM dbo.Сеансы s
JOIN dbo.Фильмы  f ON f.КодФильма = s.КодФильма
JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра
JOIN dbo.Залы  h ON h.КодЗала = s.КодЗала
OUTER APPLY (
    SELECT COUNT(*) AS Занято
    FROM dbo.Билеты t
    JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа
    WHERE o.КодСеанса = s.КодСеанса AND o.КодСтатуса IN (1, 2, 3, 4)
) b
WHERE CAST(s.НачалоСеанса AS DATE) = @Дата
  AND f.Активен = 1
ORDER BY s.НачалоСеанса;
GO

/* ---------- 2. Поиск фильма по части названия и жанру ----------
   LIKE с параметром, необязательный фильтр по жанру, сортировка по названию */
DECLARE @Запрос NVARCHAR(100) = N'ноч';
DECLARE @КодЖанра INT = NULL;   -- NULL — любой жанр

SELECT f.Название AS Фильм, g.Название AS Жанр, f.ДлительностьМин AS Минут, f.ВозрастнойРейтинг AS Возраст
FROM dbo.Фильмы f
JOIN dbo.Жанры g ON g.КодЖанра = f.КодЖанра
WHERE f.Активен = 1
  AND f.Название LIKE N'%' + @Запрос + N'%'
  AND (@КодЖанра IS NULL OR f.КодЖанра = @КодЖанра)
ORDER BY f.Название;
GO

/* ---------- 3. Свободные места на сеанс с ценой по типу места ----------
   LEFT JOIN + проверка на NULL: место свободно, если на него нет действующего билета */
DECLARE @КодСеанса INT = 5;

SELECT
    st.Ряд                                       AS Ряд,
    ty.Название                                         AS ТипМеста,
    CAST(s.БазоваяЦена * ty.КоэффициентЦены AS DECIMAL(10,2)) AS Цена,
    COUNT(*)                                        AS Свободно,
    STRING_AGG(CAST(st.НомерМеста AS NVARCHAR(3)), N', ') WITHIN GROUP (ORDER BY st.НомерМеста) AS Места
FROM dbo.Сеансы s
JOIN dbo.Места st     ON st.КодЗала = s.КодЗала
JOIN dbo.ТипыМест ty ON ty.КодТипаМеста = st.КодТипаМеста
LEFT JOIN (
    SELECT t.КодМеста
    FROM dbo.Билеты t
    JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа
    WHERE o.КодСеанса = @КодСеанса AND o.КодСтатуса IN (1, 2, 3, 4)
) занято ON занято.КодМеста = st.КодМеста
WHERE s.КодСеанса = @КодСеанса
  AND занято.КодМеста IS NULL
GROUP BY st.Ряд, ty.Название, s.БазоваяЦена, ty.КоэффициентЦены
ORDER BY st.Ряд, ty.Название;
GO

/* ---------- 4. «Мои билеты» зрителя ----------
   JOIN семи таблиц, сортировка по дате сеанса (новые сверху) */
DECLARE @КодПользователя INT = 4;

SELECT
    o.КодЗаказа                                AS Заказ,
    f.Название                                  AS Фильм,
    FORMAT(s.НачалоСеанса, 'dd.MM.yyyy HH:mm')   AS Сеанс,
    h.Название                                   AS Зал,
    st.Ряд                                AS Ряд,
    st.НомерМеста                               AS Место,
    t.Цена                                  AS Цена,
    os.Название                                  AS Статус
FROM dbo.Заказы o
JOIN dbo.СтатусыЗаказа os ON os.КодСтатуса = o.КодСтатуса
JOIN dbo.Сеансы s ON s.КодСеанса = o.КодСеанса
JOIN dbo.Фильмы f    ON f.КодФильма = s.КодФильма
JOIN dbo.Залы h    ON h.КодЗала = s.КодЗала
JOIN dbo.Билеты t  ON t.КодЗаказа = o.КодЗаказа
JOIN dbo.Места st   ON st.КодМеста = t.КодМеста
WHERE o.КодПользователя = @КодПользователя
ORDER BY s.НачалоСеанса DESC, st.Ряд, st.НомерМеста;
GO

/* ---------- 5. Суммы заказов через представление СуммыЗаказов ----------
   LEFT JOIN: у заказов из кассы нет покупателя-пользователя */
SELECT
    o.КодЗаказа                                 AS Заказ,
    ISNULL(u.ФИО, N'(продажа в кассе)')  AS Покупатель,
    os.Название                                   AS Статус,
    t.КоличествоБилетов                            AS Билетов,
    t.Сумма                                  AS Сумма,
    t.Скидка                                AS Скидка,
    t.Итого                                   AS Итого,
    ISNULL(pc.Обозначение, N'—')                     AS Промокод
FROM dbo.Заказы o
JOIN dbo.СуммыЗаказов t   ON t.КодЗаказа = o.КодЗаказа
JOIN dbo.СтатусыЗаказа os ON os.КодСтатуса = o.КодСтатуса
LEFT JOIN dbo.Пользователи u      ON u.КодПользователя = o.КодПользователя
LEFT JOIN dbo.Промокоды pc ON pc.КодПромокода = o.КодПромокода
ORDER BY t.Итого DESC;
GO

/* ---------- 6. Отчёт: выручка по фильмам за период ----------
   GROUP BY + SUM с учётом возвратов, сортировка по выручке */
DECLARE @С DATE = '2026-11-01', @По DATE = '2026-11-30';

SELECT
    f.Название AS Фильм,
    SUM(CASE WHEN p.ВидОперации = N'ОПЛАТА' THEN p.Сумма ELSE 0 END) AS Оплачено,
    SUM(CASE WHEN p.ВидОперации = N'ВОЗВРАТ'  THEN p.Сумма ELSE 0 END) AS Возвращено,
    SUM(CASE WHEN p.ВидОперации = N'ОПЛАТА' THEN p.Сумма ELSE -p.Сумма END) AS Выручка
FROM dbo.Платежи p
JOIN dbo.Заказы o   ON o.КодЗаказа = p.КодЗаказа
JOIN dbo.Сеансы s ON s.КодСеанса = o.КодСеанса
JOIN dbo.Фильмы f    ON f.КодФильма = s.КодФильма
WHERE p.Успешно = 1
  AND p.ДатаСоздания >= @С AND p.ДатаСоздания < DATEADD(DAY, 1, @По)
GROUP BY f.Название
ORDER BY Выручка DESC;
GO

/* ---------- 7. Отчёт: заполняемость сеансов ----------
   доля проданных мест от вместимости зала, сортировка по заполняемости */
SELECT
    FORMAT(s.НачалоСеанса, 'dd.MM HH:mm') AS Сеанс,
    f.Название                           AS Фильм,
    h.Название                            AS Зал,
    COUNT(t.КодБилета)                 AS Продано,
    h.КоличествоРядов * h.МестВРяду       AS Мест,
    CAST(100.0 * COUNT(t.КодБилета) / (h.КоличествоРядов * h.МестВРяду) AS DECIMAL(5,1)) AS Процент
FROM dbo.Сеансы s
JOIN dbo.Фильмы f ON f.КодФильма = s.КодФильма
JOIN dbo.Залы h ON h.КодЗала = s.КодЗала
LEFT JOIN dbo.Заказы o  ON o.КодСеанса = s.КодСеанса AND o.КодСтатуса IN (3, 4)
LEFT JOIN dbo.Билеты t ON t.КодЗаказа = o.КодЗаказа
GROUP BY s.КодСеанса, s.НачалоСеанса, f.Название, h.Название, h.КоличествоРядов, h.МестВРяду
HAVING COUNT(t.КодБилета) > 0
ORDER BY Процент DESC, s.НачалоСеанса;
GO

/* ---------- 8. Рейтинг фильмов по отзывам ----------
   AVG + HAVING: только фильмы, у которых не меньше двух отзывов */
SELECT
    f.Название                                  AS Фильм,
    COUNT(r.КодОтзыва)                        AS Отзывов,
    CAST(AVG(CAST(r.Оценка AS DECIMAL(3,2))) AS DECIMAL(3,2)) AS СредняяОценка
FROM dbo.Фильмы f
JOIN dbo.Отзывы r ON r.КодФильма = f.КодФильма
GROUP BY f.Название
HAVING COUNT(r.КодОтзыва) >= 2
ORDER BY СредняяОценка DESC;
GO

/* ---------- 9. Просроченные брони (функция 4.5 «Снять просроченные брони») ----------
   заказы в статусе «Забронирован», у которых истекло время брони */
DECLARE @Сейчас DATETIME2(0) = '2026-11-19T10:30:00';   -- в приложении: SYSDATETIME()

SELECT o.КодЗаказа AS Заказ, u.ЭлПочта AS Зритель, o.БроньДо AS БроньДо,
       DATEDIFF(MINUTE, o.БроньДо, @Сейчас) AS ПросроченоМинут
FROM dbo.Заказы o
JOIN dbo.Пользователи u ON u.КодПользователя = o.КодПользователя
WHERE o.КодСтатуса = 1 AND o.БроньДо < @Сейчас
ORDER BY o.БроньДо;
-- в приложении затем выполняется:
-- UPDATE dbo.Заказы SET КодСтатуса = 5 WHERE КодСтатуса = 1 AND БроньДо < SYSDATETIME();
GO

/* ---------- 10. Проверка целостности ----------
   а) все внешние ключи и CHECK-ограничения включены и выполняются */
SELECT COUNT(*) AS ВнешнихКлючей,
       SUM(CAST(is_disabled AS INT) + CAST(is_not_trusted AS INT)) AS Отключённых
FROM sys.foreign_keys;

DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;   -- пустой результат = нарушений нет

/* б) «висячих» ссылок нет — каждая строка с внешним ключом ссылается на существующую запись */
SELECT N'Билеты → Заказы' AS Связь, COUNT(*) AS БезРодителя
FROM dbo.Билеты t LEFT JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа WHERE o.КодЗаказа IS NULL
UNION ALL
SELECT N'Билеты → Места', COUNT(*)
FROM dbo.Билеты t LEFT JOIN dbo.Места s ON s.КодМеста = t.КодМеста WHERE s.КодМеста IS NULL
UNION ALL
SELECT N'Заказы → Сеансы', COUNT(*)
FROM dbo.Заказы o LEFT JOIN dbo.Сеансы s ON s.КодСеанса = o.КодСеанса WHERE s.КодСеанса IS NULL
UNION ALL
SELECT N'Платежи → Заказы', COUNT(*)
FROM dbo.Платежи p LEFT JOIN dbo.Заказы o ON o.КодЗаказа = p.КодЗаказа WHERE o.КодЗаказа IS NULL;
GO

/* в) ошибочные данные отклоняются. Каждая попытка выполняется в транзакции,
   которая откатывается, — база не меняется. */
DECLARE @Проверки TABLE (Проверка NVARCHAR(100), Результат NVARCHAR(300));

BEGIN TRY  -- билет на несуществующий заказ
    BEGIN TRAN; INSERT INTO dbo.Билеты (КодЗаказа, КодМеста, Цена) VALUES (999, 1, 100); ROLLBACK;
    INSERT @Проверки VALUES (N'Билет на несуществующий заказ', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Билет на несуществующий заказ', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): внешний ключ'));
END CATCH;

BEGIN TRY  -- повтор email
    BEGIN TRAN;
    INSERT INTO dbo.Пользователи (ФИО, ЭлПочта, ХешПароля, КодРоли) VALUES (N'Тест', N'ivanov@example.com', N'x', 1);
    ROLLBACK;
    INSERT @Проверки VALUES (N'Второй пользователь с тем же email', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Второй пользователь с тем же email', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): UNIQUE'));
END CATCH;

BEGIN TRY  -- оценка вне диапазона 1–5
    BEGIN TRAN; INSERT INTO dbo.Отзывы (КодПользователя, КодФильма, Оценка) VALUES (5, 2, 7); ROLLBACK;
    INSERT @Проверки VALUES (N'Отзыв с оценкой 7', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Отзыв с оценкой 7', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): CHECK'));
END CATCH;

BEGIN TRY  -- место, уже проданное на этот сеанс (ряд 5, место 7 сеанса 4 — заказ 5)
    BEGIN TRAN;
    INSERT INTO dbo.Заказы (КодПользователя, КодСеанса, КодСтатуса, БроньДо) VALUES (6, 4, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Билеты (КодЗаказа, КодМеста, Цена)
    SELECT SCOPE_IDENTITY(), КодМеста, 650 FROM dbo.Места WHERE КодЗала = 5 AND Ряд = 5 AND НомерМеста = 7;
    ROLLBACK;
    INSERT @Проверки VALUES (N'Повторная продажа занятого места', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Повторная продажа занятого места', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- место освободилось после отмены заказа 8 — его можно купить снова
    BEGIN TRAN;
    INSERT INTO dbo.Заказы (КодПользователя, КодСеанса, КодСтатуса, БроньДо) VALUES (5, 6, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Билеты (КодЗаказа, КодМеста, Цена)
    SELECT SCOPE_IDENTITY(), КодМеста, 300 FROM dbo.Места WHERE КодЗала = 2 AND Ряд = 4 AND НомерМеста = 5;
    ROLLBACK;
    INSERT @Проверки VALUES (N'Покупка места из отменённого заказа', N'принято — место свободно');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Покупка места из отменённого заказа', CONCAT(N'ОШИБКА (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- место из другого зала
    BEGIN TRAN;
    INSERT INTO dbo.Заказы (КодПользователя, КодСеанса, КодСтатуса, БроньДо) VALUES (6, 4, 1, DATEADD(MINUTE, 15, SYSDATETIME()));
    INSERT INTO dbo.Билеты (КодЗаказа, КодМеста, Цена)
    SELECT SCOPE_IDENTITY(), КодМеста, 650 FROM dbo.Места WHERE КодЗала = 1 AND Ряд = 1 AND НомерМеста = 3;
    ROLLBACK;
    INSERT @Проверки VALUES (N'Билет на место из другого зала', N'ОШИБКА: запись принята');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Билет на место из другого зала', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): ', ERROR_MESSAGE()));
END CATCH;

BEGIN TRY  -- удаление фильма, на который есть сеансы
    BEGIN TRAN; DELETE FROM dbo.Фильмы WHERE КодФильма = 1; ROLLBACK;
    INSERT @Проверки VALUES (N'Удаление фильма, у которого есть сеансы', N'ОШИБКА: удалено');
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    INSERT @Проверки VALUES (N'Удаление фильма, у которого есть сеансы', CONCAT(N'отклонено (', ERROR_NUMBER(), N'): внешний ключ'));
END CATCH;

SELECT * FROM @Проверки;
GO
