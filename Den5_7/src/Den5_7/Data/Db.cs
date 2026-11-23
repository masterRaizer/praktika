using System;
using System.Configuration;
using System.Data.SqlClient;

namespace Den5_7.Data
{
    /// <summary>Ошибка работы с данными с текстом, понятным пользователю (без технических подробностей).</summary>
    public sealed class AppDataException : Exception
    {
        public AppDataException(string message, Exception inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>Запись не найдена (её удалили или изменили в другом окне).</summary>
    public sealed class RecordNotFoundException : Exception
    {
        public RecordNotFoundException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Подключение к базе «Киноточка». Строка подключения — в файле Den5_7.exe.config (раздел connectionStrings).
    /// Соединения открываются на одну операцию и закрываются блоком using.
    /// </summary>
    public static class Db
    {
        public const string ConnectionName = "Киноточка";

        /// <summary>Подмена строки подключения (используется тестом «нет подключения»).</summary>
        public static string OverrideConnectionString { get; set; }

        public static string ConnectionString
        {
            get
            {
                if (!string.IsNullOrEmpty(OverrideConnectionString)) return OverrideConnectionString;
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new AppDataException(
                        "В файле Den5_7.exe.config нет строки подключения «" + ConnectionName + "». " +
                        "Добавьте её в раздел connectionStrings и запустите программу снова.");
                }
                return settings.ConnectionString;
            }
        }

        /// <summary>Открывает соединение; при неудаче — понятное сообщение без строки подключения и пароля.</summary>
        public static SqlConnection Open()
        {
            var connection = new SqlConnection(ConnectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch (SqlException ex)
            {
                // ошибка при открытии соединения — это всегда проблема подключения, какой бы код ни вернул драйвер
                connection.Dispose();
                string text;
                if (ex.Number == 4060)
                {
                    text = "База данных «Киноточка» не найдена на сервере. Создайте её скриптами из папки sql (01, 02, 04).";
                }
                else if (ex.Number == 18456)
                {
                    text = "Сервер базы данных отклонил вход. Проверьте способ входа в строке подключения (файл Den5_7.exe.config).";
                }
                else
                {
                    text = NoConnectionText;
                }
                throw new AppDataException(text, ex);
            }
            catch (InvalidOperationException ex)
            {
                connection.Dispose();
                throw new AppDataException(NoConnectionText, ex);
            }
            catch (ArgumentException ex)
            {
                connection.Dispose();
                throw new AppDataException("Строка подключения в файле Den5_7.exe.config записана с ошибкой. Проверьте её.", ex);
            }
        }

        private const string NoConnectionText =
            "Нет подключения к базе данных «Киноточка».\n\n" +
            "Проверьте, что SQL Server запущен, база создана скриптами из папки sql " +
            "и строка подключения в файле Den5_7.exe.config указывает на ваш сервер.";

        /// <summary>Перевод ошибки SQL Server в понятный текст.</summary>
        public static string Explain(SqlException ex)
        {
            switch (ex.Number)
            {
                case 547:
                    if (ex.Message.IndexOf("CHECK", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "Значение не прошло проверку базы данных (например, недопустимая длительность, оценка или цена). Исправьте данные.";
                    }
                    return "Операция нарушает связь между таблицами: запись используется в других данных " +
                           "или ссылается на несуществующую запись.";
                case 2601:
                case 2627:
                    return "Такая запись уже есть: значение должно быть уникальным.";
                case 515:
                    return "Не заполнено обязательное поле.";
                case 2628:
                case 8152:
                    return "Слишком длинное значение: сократите текст.";
                case 1205:
                    return "Запись сейчас изменяется другим пользователем. Повторите действие.";
                case -2:
                case -1:
                case 2:
                case 26:
                case 40:
                case 53:
                case 233:
                case 4060:
                case 10061:
                case 11001:
                case 18456:
                    return NoConnectionText;
            }
            if (ex.Number >= 50000)
            {
                return ex.Message;   // собственные сообщения триггеров базы — уже на русском
            }
            return "Ошибка базы данных (код " + ex.Number + "). Действие не выполнено, повторите его или обратитесь к администратору.";
        }

        /// <summary>Выполняет действие с открытым соединением и переводит ошибки SQL Server в понятные.</summary>
        public static T Run<T>(Func<SqlConnection, T> action)
        {
            try
            {
                using (SqlConnection connection = Open())
                {
                    return action(connection);
                }
            }
            catch (SqlException ex)
            {
                throw new AppDataException(Explain(ex), ex);
            }
        }

        public static void Run(Action<SqlConnection> action)
        {
            Run<object>(delegate(SqlConnection c)
            {
                action(c);
                return null;
            });
        }

        public static object DbValue(object value)
        {
            return value ?? DBNull.Value;
        }

        /// <summary>Экранирование символов шаблона LIKE в пользовательском тексте.</summary>
        public static string EscapeLike(string text)
        {
            return (text ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }
    }
}
