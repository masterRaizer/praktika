using System;
using System.Security.Cryptography;
using System.Text;
using Den5_7.Models;

namespace Den5_7.Logic
{
    /// <summary>Неверный ввод пользователя: сообщение показывается как есть.</summary>
    public sealed class ValidationException : Exception
    {
        public ValidationException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Проверка пароля по хешу PBKDF2-SHA256 в формате PBKDF2$итерации$соль$хеш (Base64).
    /// Сам пароль нигде не хранится и не выводится.
    /// </summary>
    public static class PasswordHasher
    {
        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored)) return false;
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2") return false;
            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations < 1) return false;
            byte[] salt, expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }
            byte[] actual = Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), salt, iterations, expected.Length);
            // сравнение без раннего выхода, чтобы время не зависело от совпадения
            int diff = actual.Length ^ expected.Length;
            for (int i = 0; i < Math.Min(actual.Length, expected.Length); i++)
            {
                diff |= actual[i] ^ expected[i];
            }
            return diff == 0;
        }

        public static string Hash(string password, byte[] salt, int iterations = 100000)
        {
            byte[] hash = Pbkdf2Sha256(Encoding.UTF8.GetBytes(password), salt, iterations, 32);
            return "PBKDF2$" + iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>PBKDF2 (RFC 2898) с HMAC-SHA256.</summary>
        private static byte[] Pbkdf2Sha256(byte[] password, byte[] salt, int iterations, int length)
        {
            using (var hmac = new HMACSHA256(password))
            {
                var result = new byte[length];
                int blocks = (length + 31) / 32;
                for (int block = 1; block <= blocks; block++)
                {
                    var input = new byte[salt.Length + 4];
                    Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
                    input[salt.Length] = (byte)(block >> 24);
                    input[salt.Length + 1] = (byte)(block >> 16);
                    input[salt.Length + 2] = (byte)(block >> 8);
                    input[salt.Length + 3] = (byte)block;
                    byte[] u = hmac.ComputeHash(input);
                    byte[] t = (byte[])u.Clone();
                    for (int i = 1; i < iterations; i++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int j = 0; j < t.Length; j++) t[j] ^= u[j];
                    }
                    int offset = (block - 1) * 32;
                    Buffer.BlockCopy(t, 0, result, offset, Math.Min(32, length - offset));
                }
                return result;
            }
        }
    }

    /// <summary>Текущий пользователь и права ролей.</summary>
    public static class Session
    {
        public const string Admin = "АДМИНИСТРАТОР";
        public const string Cashier = "КАССИР";
        public const string Viewer = "ЗРИТЕЛЬ";

        private static User current;

        /// <summary>Пользователь вошёл или вышел.</summary>
        public static event EventHandler Changed;

        public static User Current
        {
            get { return current; }
            set
            {
                current = value;
                EventHandler h = Changed;
                if (h != null) h(null, EventArgs.Empty);
            }
        }

        public static bool IsLoggedIn
        {
            get { return current != null; }
        }

        /// <summary>Добавлять, изменять и удалять фильмы — только администратор.</summary>
        public static bool CanEditFilms(User user)
        {
            return user != null && user.RoleCode == Admin;
        }

        /// <summary>Отчёты и экспорт — администратор и кассир.</summary>
        public static bool CanViewReports(User user)
        {
            return user != null && (user.RoleCode == Admin || user.RoleCode == Cashier);
        }

        /// <summary>Журнал действий — только администратор.</summary>
        public static bool CanViewJournal(User user)
        {
            return user != null && user.RoleCode == Admin;
        }
    }
}
