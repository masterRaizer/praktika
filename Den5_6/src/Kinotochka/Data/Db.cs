using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using Kinotochka.Logic;

namespace Kinotochka.Data
{
    /// <summary>
    /// Подключение к базе «Киноточка» и выполнение запросов.
    /// Каждое обращение открывает соединение в блоке using, поэтому оно закрывается всегда —
    /// и при успешном выполнении, и при ошибке. Значения пользователя передаются только параметрами
    /// (SqlParameter), а не склейкой строк, поэтому SQL-инъекция невозможна.
    /// </summary>
    public static class Db
    {
        public const string ConnectionName = "Kinotochka";

        private static string overrideConnection;

        /// <summary>Файл, в котором форма «Подключение» сохраняет имя сервера (пароли туда не пишутся).</summary>
        public static string SettingsFile
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Kinotochka", "server.txt");
            }
        }

        /// <summary>Задать строку подключения явно (параметр запуска --connection и проверки).</summary>
        public static void UseConnectionString(string connectionString)
        {
            overrideConnection = connectionString;
        }

        /// <summary>Строка подключения: из App.config, имя сервера — из настроек пользователя, если он его менял.</summary>
        public static string ConnectionString
        {
            get
            {
                if (!string.IsNullOrEmpty(overrideConnection)) return overrideConnection;
                ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings[ConnectionName];
                string baseString = settings != null && !string.IsNullOrWhiteSpace(settings.ConnectionString)
                    ? settings.ConnectionString
                    : @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=Киноточка;Integrated Security=True;Connect Timeout=8";
                var builder = new SqlConnectionStringBuilder(baseString);
                string server = LoadServer();
                if (!string.IsNullOrEmpty(server)) builder.DataSource = server;
                return builder.ConnectionString;
            }
        }

        /// <summary>Имя сервера из текущей строки подключения (для показа пользователю).</summary>
        public static string ServerName
        {
            get
            {
                try { return new SqlConnectionStringBuilder(ConnectionString).DataSource; }
                catch (Exception) { return "?"; }
            }
        }

        public static string LoadServer()
        {
            try
            {
                return File.Exists(SettingsFile) ? File.ReadAllText(SettingsFile).Trim() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void SaveServer(string server)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
            File.WriteAllText(SettingsFile, server ?? "");
        }

        /// <summary>Открывает соединение. При неудаче — AppException с понятным текстом.</summary>
        public static SqlConnection Open()
        {
            var connection = new SqlConnection(ConnectionString);
            try
            {
                connection.Open();
                return connection;
            }
            catch (Exception ex)
            {
                connection.Dispose();
                throw ErrorTranslator.ConnectionFailed(ex);
            }
        }

        /// <summary>Проверка подключения: открыть, прочитать версию сервера, закрыть.</summary>
        public static string TestConnection()
        {
            using (SqlConnection c = Open())
            using (var cmd = new SqlCommand("SELECT DB_NAME() + N' на ' + @@SERVERNAME", c))
            {
                try
                {
                    return Convert.ToString(cmd.ExecuteScalar());
                }
                catch (Exception ex)
                {
                    throw ErrorTranslator.Translate(ex, "проверка подключения");
                }
            }
        }

        // ---------- параметры ----------

        public static SqlParameter P(string name, SqlDbType type, object value, int size = 0)
        {
            var p = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
            if (size > 0) p.Size = size;
            if (value is string && string.IsNullOrEmpty((string)value) && size > 0) p.Value = DBNull.Value;
            return p;
        }

        public static SqlParameter Int(string name, int value) { return P(name, SqlDbType.Int, value); }
        public static SqlParameter NullableInt(string name, int value) { return P(name, SqlDbType.Int, value > 0 ? (object)value : null); }
        public static SqlParameter Text(string name, string value, int size) { return P(name, SqlDbType.NVarChar, value, size); }
        public static SqlParameter Money(string name, decimal value) { return new SqlParameter(name, SqlDbType.Decimal) { Precision = 10, Scale = 2, Value = value }; }
        public static SqlParameter Date(string name, DateTime value) { return P(name, SqlDbType.DateTime2, value); }
        public static SqlParameter Bit(string name, bool value) { return P(name, SqlDbType.Bit, value); }

        // ---------- выполнение ----------

        /// <summary>SELECT: каждая строка результата превращается в объект функцией map.</summary>
        public static List<T> Query<T>(string sql, Func<SqlDataReader, T> map, string action, params SqlParameter[] parameters)
        {
            var result = new List<T>();
            using (SqlConnection c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                cmd.Parameters.AddRange(parameters);
                try
                {
                    using (SqlDataReader r = cmd.ExecuteReader())
                        while (r.Read()) result.Add(map(r));
                }
                catch (Exception ex)
                {
                    throw ErrorTranslator.Translate(ex, action);
                }
            }
            return result;
        }

        /// <summary>INSERT / UPDATE / DELETE; возвращает число изменённых строк.</summary>
        public static int Execute(string sql, string action, params SqlParameter[] parameters)
        {
            using (SqlConnection c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                cmd.Parameters.AddRange(parameters);
                try
                {
                    return cmd.ExecuteNonQuery();
                }
                catch (Exception ex)
                {
                    throw ErrorTranslator.Translate(ex, action);
                }
            }
        }

        /// <summary>Запрос, возвращающий одно значение (COUNT, новый код записи).</summary>
        public static object Scalar(string sql, string action, params SqlParameter[] parameters)
        {
            using (SqlConnection c = Open())
            using (var cmd = new SqlCommand(sql, c))
            {
                cmd.Parameters.AddRange(parameters);
                try
                {
                    object v = cmd.ExecuteScalar();
                    return v == DBNull.Value ? null : v;
                }
                catch (Exception ex)
                {
                    throw ErrorTranslator.Translate(ex, action);
                }
            }
        }

        // ---------- чтение полей ----------

        public static string S(SqlDataReader r, string name)
        {
            object v = r[name];
            return v == DBNull.Value ? null : Convert.ToString(v);
        }

        public static int I(SqlDataReader r, string name)
        {
            object v = r[name];
            return v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        public static decimal M(SqlDataReader r, string name)
        {
            object v = r[name];
            return v == DBNull.Value ? 0m : Convert.ToDecimal(v);
        }

        public static DateTime D(SqlDataReader r, string name)
        {
            object v = r[name];
            return v == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(v);
        }

        public static bool B(SqlDataReader r, string name)
        {
            object v = r[name];
            return v != DBNull.Value && Convert.ToBoolean(v);
        }
    }
}
