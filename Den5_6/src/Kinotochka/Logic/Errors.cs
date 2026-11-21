using System;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Kinotochka.Logic
{
    /// <summary>Вид ошибки — от него зависит, как форма её покажет.</summary>
    public enum ErrorKind
    {
        /// <summary>Нет связи с сервером или базой.</summary>
        Connection,
        /// <summary>Нарушено ограничение базы (внешний ключ, CHECK, триггер).</summary>
        Constraint,
        /// <summary>Повтор уникального значения.</summary>
        Duplicate,
        /// <summary>Неверно заполнены поля формы.</summary>
        Validation,
        /// <summary>Неверный логин или пароль, учётная запись заблокирована.</summary>
        Auth,
        /// <summary>Прочие ошибки.</summary>
        Other
    }

    /// <summary>
    /// Ошибка с текстом для пользователя. Технические подробности (номер ошибки SQL Server,
    /// исходное сообщение) хранятся отдельно и пишутся в журнал, а не показываются вместе с данными.
    /// </summary>
    public sealed class AppException : Exception
    {
        public ErrorKind Kind { get; private set; }
        public int SqlNumber { get; private set; }
        public string Technical { get; private set; }
        /// <summary>Поле формы, к которому относится ошибка проверки ввода (или null).</summary>
        public string Field { get; private set; }

        public AppException(ErrorKind kind, string message, Exception inner = null, int sqlNumber = 0, string field = null)
            : base(message, inner)
        {
            Kind = kind;
            SqlNumber = sqlNumber;
            Field = field;
            Technical = inner == null ? null : ErrorLog.Mask(inner.GetType().Name + (sqlNumber != 0 ? " №" + sqlNumber : "") + ": " + inner.Message);
        }

        public static AppException Validation(string field, string message)
        {
            return new AppException(ErrorKind.Validation, message, null, 0, field);
        }
    }

    /// <summary>Перевод исключений ADO.NET в понятные сообщения на русском языке.</summary>
    public static class ErrorTranslator
    {
        public static AppException Translate(Exception ex, string action)
        {
            var already = ex as AppException;
            if (already != null) return already;

            AppException result;
            var sql = ex as SqlException;
            if (sql != null)
            {
                result = FromSql(sql, action);
            }
            else if (ex is InvalidOperationException || ex is TimeoutException)
            {
                result = new AppException(ErrorKind.Connection, ConnectionText(), ex);
            }
            else
            {
                result = new AppException(ErrorKind.Other,
                    "Не удалось выполнить действие «" + action + "». Попробуйте ещё раз; если ошибка повторяется, сообщите администратору.", ex);
            }
            ErrorLog.Write(action, result);
            return result;
        }

        /// <summary>Ошибка при открытии соединения — всегда «нет подключения», какой бы ни был номер ошибки.</summary>
        public static AppException ConnectionFailed(Exception ex)
        {
            var sql = ex as SqlException;
            var result = new AppException(ErrorKind.Connection, ConnectionText(), ex, sql != null ? sql.Number : 0);
            ErrorLog.Write("подключение к базе данных", result);
            return result;
        }

        private static string ConnectionText()
        {
            return "Нет подключения к базе данных «Киноточка».\n\n" +
                   "Проверьте, что:\n" +
                   "• SQL Server (или LocalDB) запущен;\n" +
                   "• база создана скриптами db\\01_create.sql и db\\02_data.sql;\n" +
                   "• в окне «Подключение» указан правильный сервер.";
        }

        private static AppException FromSql(SqlException ex, string action)
        {
            int n = ex.Number;
            string m = ex.Message ?? "";
            switch (n)
            {
                // сервер не найден, сеть, неверный вход, база не существует, тайм-аут
                case -2:
                case -1:
                case 2:
                case 53:
                case 64:
                case 233:
                case 10053:
                case 10054:
                case 10060:
                case 10061:
                case 11001:
                case 4060:
                case 18456:
                case 18452:
                case -2146893019:
                    return new AppException(ErrorKind.Connection, ConnectionText(), ex, n);
                case 2627:
                case 2601:
                    return new AppException(ErrorKind.Duplicate, DuplicateText(m), ex, n);
                case 547:
                    return new AppException(ErrorKind.Constraint, ConstraintText(m, action), ex, n);
                case 50001:
                case 50002:
                    // сообщение триггера уже написано для пользователя
                    return new AppException(ErrorKind.Constraint, m, ex, n);
                case 8152:
                case 2628:
                    return new AppException(ErrorKind.Validation, "Слишком длинное значение в одном из полей. Сократите текст и сохраните снова.", ex, n);
                case 1205:
                    return new AppException(ErrorKind.Other, "Запись сейчас изменяет другой пользователь. Повторите действие через несколько секунд.", ex, n);
                default:
                    return new AppException(ErrorKind.Other,
                        "База данных не выполнила действие «" + action + "». Проверьте введённые данные и попробуйте ещё раз.", ex, n);
            }
        }

        private static string DuplicateText(string m)
        {
            if (m.Contains("UQ_Пользователи_ЭлПочта")) return "Пользователь с такой электронной почтой уже зарегистрирован.";
            if (m.Contains("UQ_Сеансы_ЗалНачало")) return "В этом зале уже есть сеанс с таким временем начала. Выберите другое время или зал.";
            if (m.Contains("UQ_Жанры_Название")) return "Такой жанр уже есть.";
            if (m.Contains("UQ_Залы_Номер")) return "Зал с таким номером уже есть.";
            return "Такая запись уже есть в базе данных: значение должно быть уникальным.";
        }

        private static string ConstraintText(string m, string action)
        {
            if (m.Contains("DELETE"))
            {
                if (m.Contains("FK_Сеансы_Фильмы")) return "Фильм нельзя удалить: по нему есть сеансы. Сначала удалите сеансы или снимите фильм с проката.";
                if (m.Contains("FK_Отзывы_Фильмы")) return "Фильм нельзя удалить: о нём есть отзывы зрителей. Снимите фильм с проката.";
                if (m.Contains("FK_Заказы_Сеансы")) return "Сеанс нельзя удалить: на него оформлены заказы.";
                if (m.Contains("FK_Платежи_Заказы")) return "Заказ нельзя удалить: по нему проходили платежи. Измените статус заказа на «Отменён» или «Возвращён».";
                if (m.Contains("FK_Заказы_Пользователи") || m.Contains("FK_Заказы_Кассиры") || m.Contains("FK_Отзывы_Пользователи"))
                    return "Пользователя нельзя удалить: с его учётной записью связаны заказы или отзывы. Заблокируйте его вместо удаления.";
                return "Запись нельзя удалить: на неё ссылаются другие данные.";
            }
            if (m.Contains("CHECK"))
            {
                if (m.Contains("CK_Фильмы_Длительность")) return "Длительность фильма должна быть от 1 до 600 минут.";
                if (m.Contains("CK_Фильмы_ВозрастнойРейтинг")) return "Возрастной рейтинг: 0+, 6+, 12+, 16+ или 18+.";
                if (m.Contains("CK_Сеансы_Цена")) return "Цена сеанса должна быть больше нуля.";
                if (m.Contains("CK_Сеансы_Формат")) return "Формат сеанса: 2D, 3D или IMAX.";
                if (m.Contains("CK_Пользователи_ЭлПочта")) return "Неверный формат электронной почты.";
                return "Значение не прошло проверку базы данных (ограничение CHECK).";
            }
            return "Выбрана несуществующая связанная запись (например, удалённый жанр или зал). Обновите данные и повторите.";
        }
    }

    /// <summary>
    /// Журнал ошибок в файле %LOCALAPPDATA%\Kinotochka\errors.log — технические подробности для разработчика.
    /// Пароли и строки подключения в журнал не попадают.
    /// </summary>
    public static class ErrorLog
    {
        public static string FilePath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kinotochka", "errors.log");
            }
        }

        /// <summary>Убирает из текста пароли (Password=..., Pwd=...) и хеши паролей.</summary>
        public static string Mask(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = Regex.Replace(text, @"(?i)(password|pwd)\s*=\s*[^;'""\s]*", "$1=***");
            text = Regex.Replace(text, @"PBKDF2\$[^\s'""]+", "PBKDF2$***");
            return text;
        }

        public static void Write(string action, AppException ex)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                var sb = new StringBuilder();
                sb.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append(" | ").Append(ex.Kind)
                  .Append(" | ").Append(action).Append(" | ").Append((ex.Technical ?? ex.Message).Replace("\r", " ").Replace("\n", " "))
                  .AppendLine();
                File.AppendAllText(FilePath, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception)
            {
                // журнал не должен мешать работе программы
            }
        }
    }
}
