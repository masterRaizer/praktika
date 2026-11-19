using System;
using System.Configuration;
using System.Data.SqlClient;

namespace ShagShoes.Data
{
    /// <summary>Ошибка работы с базой данных с понятным пользователю текстом.</summary>
    public sealed class DatabaseException : Exception
    {
        public DatabaseException(string message, Exception inner) : base(message, inner)
        {
        }
    }

    /// <summary>Подключение к базе ShagShoes. Строка подключения — в файле ShagShoes.exe.config.</summary>
    public static class Database
    {
        private const string ConnectionName = "ShagShoes";

        public static string ConnectionString
        {
            get
            {
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionName];
                if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                {
                    throw new DatabaseException(
                        "В файле ShagShoes.exe.config нет строки подключения «" + ConnectionName + "».\n\n" +
                        "Добавьте её в раздел connectionStrings и запустите программу снова.", null);
                }
                return settings.ConnectionString;
            }
        }

        /// <summary>Открывает соединение; при неудаче сообщает, что проверить.</summary>
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
                connection.Dispose();
                throw new DatabaseException(
                    "Не удалось подключиться к базе данных ShagShoes.\n\n" +
                    "Проверьте, что:\n" +
                    "• SQL Server запущен;\n" +
                    "• база создана скриптом db\\ShagShoes.sql;\n" +
                    "• строка подключения в файле ShagShoes.exe.config указывает на ваш сервер.\n\n" +
                    "Сообщение сервера: " + ex.Message, ex);
            }
        }
    }
}
