using System;

namespace Kinotochka.Models
{
    /// <summary>Строка справочника (жанр, зал, роль, статус): код и название.</summary>
    public sealed class LookupItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }

        public override string ToString() { return Name; }
    }

    /// <summary>Учётная запись (таблица Пользователи).</summary>
    public sealed class User
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string PasswordHash { get; set; }
        public int RoleId { get; set; }
        public string RoleCode { get; set; }
        public string RoleName { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>Фильм (таблица Фильмы).</summary>
    public sealed class Film
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int GenreId { get; set; }
        public string GenreName { get; set; }
        public int DurationMin { get; set; }
        public string AgeRating { get; set; }
        public string Description { get; set; }
        public bool IsActive { get; set; }
        /// <summary>Количество сеансов фильма (для проверки перед удалением).</summary>
        public int ScreeningCount { get; set; }
    }

    /// <summary>Сеанс (таблица Сеансы) с названиями фильма и зала.</summary>
    public sealed class Screening
    {
        public int Id { get; set; }
        public int FilmId { get; set; }
        public string FilmTitle { get; set; }
        public int HallId { get; set; }
        public string HallName { get; set; }
        public DateTime StartsAt { get; set; }
        public string Format { get; set; }
        public decimal BasePrice { get; set; }
        public int SoldSeats { get; set; }
        public int Capacity { get; set; }
        public int OrderCount { get; set; }
    }

    /// <summary>Заказ (таблица Заказы) с суммой из представления СуммыЗаказов.</summary>
    public sealed class Order
    {
        public int Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Customer { get; set; }
        public string Cashier { get; set; }
        public string ScreeningText { get; set; }
        public int StatusId { get; set; }
        public string StatusCode { get; set; }
        public string StatusName { get; set; }
        public int TicketCount { get; set; }
        public decimal Total { get; set; }
        public string PromoCode { get; set; }
        public int PaymentCount { get; set; }
    }

    /// <summary>Показатели для главной страницы.</summary>
    public sealed class Dashboard
    {
        public int Films { get; set; }
        public int UpcomingScreenings { get; set; }
        public int ActiveOrders { get; set; }
        public decimal Revenue { get; set; }
        public int Users { get; set; }
    }
}
