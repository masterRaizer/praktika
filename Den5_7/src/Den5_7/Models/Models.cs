using System;

namespace Den5_7.Models
{
    /// <summary>Вошедший пользователь.</summary>
    public sealed class User
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string RoleCode { get; set; }
        public string RoleName { get; set; }
    }

    public sealed class Genre
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public sealed class Film
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int GenreId { get; set; }
        public string Genre { get; set; }
        public int DurationMin { get; set; }
        public string AgeRating { get; set; }
        public string Description { get; set; }
        public string Poster { get; set; }
        public bool IsActive { get; set; }
        public int SessionsCount { get; set; }
    }

    public enum FilmSort
    {
        TitleAsc,
        TitleDesc,
        DurationDesc,
        DurationAsc
    }

    public sealed class JournalEntry
    {
        public DateTime Time { get; set; }
        public string User { get; set; }
        public string Action { get; set; }
        public string Details { get; set; }
    }
}
