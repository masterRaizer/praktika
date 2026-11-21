using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Kinotochka.Logic
{
    /// <summary>
    /// Проверка вводимых данных до обращения к базе. Каждый метод либо возвращает очищенное значение,
    /// либо бросает AppException с понятным текстом и именем поля (форма подсвечивает это поле).
    /// Правила совпадают с ограничениями CHECK в базе данных (день 4).
    /// </summary>
    public static class Validator
    {
        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        public static readonly string[] AgeRatings = { "0+", "6+", "12+", "16+", "18+" };
        public static readonly string[] Formats = { "2D", "3D", "IMAX" };

        public static string Required(string field, string caption, string value, int maxLength)
        {
            string v = (value ?? "").Trim();
            if (v.Length == 0) throw AppException.Validation(field, "Заполните поле «" + caption + "».");
            if (v.Length > maxLength) throw AppException.Validation(field, "Поле «" + caption + "» — не длиннее " + maxLength + " символов.");
            return v;
        }

        public static string Optional(string field, string caption, string value, int maxLength)
        {
            string v = (value ?? "").Trim();
            if (v.Length > maxLength) throw AppException.Validation(field, "Поле «" + caption + "» — не длиннее " + maxLength + " символов.");
            return v.Length == 0 ? null : v;
        }

        public static string Email(string field, string value)
        {
            string v = Required(field, "Электронная почта", value, 100).ToLowerInvariant();
            if (!Regex.IsMatch(v, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                throw AppException.Validation(field, "Неверный формат электронной почты. Пример: ivanov@example.com");
            return v;
        }

        public static string FullName(string field, string value)
        {
            string v = Regex.Replace(Required(field, "ФИО", value, 100), @"\s+", " ");
            if (!Regex.IsMatch(v, @"^[А-ЯЁа-яёA-Za-z\-]+( [А-ЯЁа-яёA-Za-z\-]+){1,2}$"))
                throw AppException.Validation(field, "ФИО — фамилия и имя (отчество по желанию) буквами, через пробел.");
            return v;
        }

        public static string Phone(string field, string value)
        {
            string v = Optional(field, "Телефон", value, 20);
            if (v == null) return null;
            string digits = Regex.Replace(v, @"\D", "");
            if (!Regex.IsMatch(v, @"^[\d\s\-\+\(\)]+$") || digits.Length < 10 || digits.Length > 12)
                throw AppException.Validation(field, "Телефон — от 10 до 12 цифр, например +7 900 123-45-67.");
            return v;
        }

        /// <summary>Пароль: не короче 6 символов, буквы и цифры.</summary>
        public static string Password(string field, string value)
        {
            string v = value ?? "";
            if (v.Length == 0) throw AppException.Validation(field, "Введите пароль.");
            if (v.Length < 6) throw AppException.Validation(field, "Пароль должен быть не короче 6 символов.");
            if (v.Length > 64) throw AppException.Validation(field, "Пароль — не длиннее 64 символов.");
            if (!Regex.IsMatch(v, @"\d") || !Regex.IsMatch(v, @"\p{L}"))
                throw AppException.Validation(field, "Пароль должен содержать хотя бы одну букву и одну цифру.");
            return v;
        }

        public static int IntRange(string field, string caption, string value, int min, int max)
        {
            int n;
            string v = (value ?? "").Trim();
            if (v.Length == 0) throw AppException.Validation(field, "Заполните поле «" + caption + "».");
            if (!int.TryParse(v, NumberStyles.Integer, Ru, out n))
                throw AppException.Validation(field, "Поле «" + caption + "» — целое число.");
            if (n < min || n > max)
                throw AppException.Validation(field, "Поле «" + caption + "» — от " + min + " до " + max + ".");
            return n;
        }

        public static decimal Price(string field, string value)
        {
            string v = (value ?? "").Trim().Replace(" ", "").Replace('.', ',');
            decimal d;
            if (v.Length == 0) throw AppException.Validation(field, "Укажите цену.");
            if (!decimal.TryParse(v, NumberStyles.Number, Ru, out d))
                throw AppException.Validation(field, "Цена — число, например 450 или 450,50.");
            if (d <= 0) throw AppException.Validation(field, "Цена должна быть больше нуля.");
            if (d > 100000) throw AppException.Validation(field, "Цена не может быть больше 100 000 руб.");
            if (decimal.Round(d, 2) != d) throw AppException.Validation(field, "Цена — не больше двух знаков после запятой.");
            return d;
        }

        /// <summary>Дата и время в виде «дд.мм.гггг чч:мм».</summary>
        public static DateTime DateTimeValue(string field, string value)
        {
            string v = Regex.Replace((value ?? "").Trim(), @"\s+", " ");
            DateTime d;
            if (v.Length == 0) throw AppException.Validation(field, "Укажите дату и время начала.");
            if (!DateTime.TryParseExact(v, new[] { "dd.MM.yyyy HH:mm", "d.M.yyyy H:mm" }, Ru, DateTimeStyles.None, out d))
                throw AppException.Validation(field, "Дата и время — в виде дд.мм.гггг чч:мм, например 25.11.2026 19:30.");
            if (d.Year < 2020 || d.Year > 2100) throw AppException.Validation(field, "Проверьте год даты сеанса.");
            return d;
        }

        public static string OneOf(string field, string caption, string value, string[] allowed)
        {
            foreach (string a in allowed)
                if (a == value) return a;
            throw AppException.Validation(field, "Выберите значение поля «" + caption + "»: " + string.Join(", ", allowed) + ".");
        }

        public static int Selected(string field, string caption, int id)
        {
            if (id <= 0) throw AppException.Validation(field, "Выберите значение поля «" + caption + "».");
            return id;
        }
    }
}
