using System;
using System.Security.Cryptography;
using System.Text;

namespace Kinotochka.Logic
{
    /// <summary>
    /// Безопасное хеширование паролей: PBKDF2-SHA256, 100 000 итераций, случайная соль 16 байт
    /// у каждого пользователя. В базе хранится строка PBKDF2$итерации$соль$хеш (Base64) —
    /// тот же формат, что в тестовых данных дня 4. Сам пароль нигде не сохраняется.
    /// </summary>
    public static class PasswordHasher
    {
        public const int Iterations = 100000;
        private const int SaltSize = 16;
        private const int HashSize = 32;

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException("password");
            byte[] salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            byte[] hash = Derive(password, salt, Iterations);
            return "PBKDF2$" + Iterations + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        public static bool Verify(string password, string stored)
        {
            if (password == null || string.IsNullOrEmpty(stored)) return false;
            string[] parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != "PBKDF2") return false;
            int iterations;
            if (!int.TryParse(parts[1], out iterations) || iterations < 1000) return false;
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
            byte[] actual = Derive(password, salt, iterations);
            return FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations)
        {
            using (var kdf = new Rfc2898DeriveBytes(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256))
                return kdf.GetBytes(HashSize);
        }

        /// <summary>Сравнение за постоянное время — по времени ответа нельзя угадать хеш.</summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
