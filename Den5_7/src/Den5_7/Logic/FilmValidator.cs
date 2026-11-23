using System.Collections.Generic;
using Den5_7.Models;

namespace Den5_7.Logic
{
    /// <summary>Проверка карточки фильма до сохранения (те же правила, что и ограничения CHECK в базе).</summary>
    public static class FilmValidator
    {
        public const int TitleMax = 150;
        public const int DescriptionMax = 1000;
        public const int PosterMax = 260;
        public const int DurationMin = 1;
        public const int DurationMax = 600;
        public static readonly string[] AgeRatings = { "0+", "6+", "12+", "16+", "18+" };

        /// <summary>Список ошибок; пустой — карточка заполнена верно.</summary>
        public static List<string> Validate(Film film)
        {
            var errors = new List<string>();
            string title = film.Title == null ? "" : film.Title.Trim();
            if (title.Length == 0)
            {
                errors.Add("Введите название фильма.");
            }
            else if (title.Length > TitleMax)
            {
                errors.Add("Название длиннее " + TitleMax + " символов (сейчас " + title.Length + ").");
            }
            if (film.GenreId <= 0)
            {
                errors.Add("Выберите жанр.");
            }
            if (film.DurationMin < DurationMin || film.DurationMin > DurationMax)
            {
                errors.Add("Длительность должна быть от " + DurationMin + " до " + DurationMax + " минут.");
            }
            if (System.Array.IndexOf(AgeRatings, film.AgeRating) < 0)
            {
                errors.Add("Выберите возрастной рейтинг: 0+, 6+, 12+, 16+ или 18+.");
            }
            if (film.Description != null && film.Description.Length > DescriptionMax)
            {
                errors.Add("Описание длиннее " + DescriptionMax + " символов (сейчас " + film.Description.Length + ").");
            }
            if (film.Poster != null && film.Poster.Length > PosterMax)
            {
                errors.Add("Путь к постеру длиннее " + PosterMax + " символов.");
            }
            return errors;
        }
    }
}
