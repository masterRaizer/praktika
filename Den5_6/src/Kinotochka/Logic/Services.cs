using System;
using System.Collections.Generic;
using Kinotochka.Data;
using Kinotochka.Models;

namespace Kinotochka.Logic
{
    /// <summary>Роли из справочника Роли и права, которые они дают в приложении.</summary>
    public static class Roles
    {
        public const string Viewer = "ЗРИТЕЛЬ";
        public const string Cashier = "КАССИР";
        public const string Admin = "АДМИНИСТРАТОР";
        public const int ViewerId = 1;

        public static bool CanEditCatalog(User u) { return u != null && u.RoleCode == Admin; }
        public static bool CanManageUsers(User u) { return u != null && u.RoleCode == Admin; }
        public static bool CanManageOrders(User u) { return u != null && (u.RoleCode == Admin || u.RoleCode == Cashier); }
        public static bool CanDeleteOrders(User u) { return u != null && u.RoleCode == Admin; }
        public static bool SeesOnlyOwnOrders(User u) { return u == null || u.RoleCode == Viewer; }

        public static void Require(bool allowed)
        {
            if (!allowed) throw new AppException(ErrorKind.Auth, "Для этого действия недостаточно прав.");
        }
    }

    /// <summary>Вход и регистрация.</summary>
    public sealed class AuthService
    {
        public const int MaxAttempts = 3;
        public const int LockSeconds = 30;

        private readonly UserRepository users = new UserRepository();
        private int failedAttempts;
        private DateTime lockedUntil = DateTime.MinValue;
        private readonly Func<DateTime> clock;

        public AuthService() : this(() => DateTime.Now) { }

        public AuthService(Func<DateTime> clock) { this.clock = clock; }

        /// <summary>Сколько секунд осталось до разблокировки формы входа (0 — вход разрешён).</summary>
        public int SecondsLocked
        {
            get
            {
                double s = (lockedUntil - clock()).TotalSeconds;
                return s > 0 ? (int)Math.Ceiling(s) : 0;
            }
        }

        /// <summary>
        /// Проверка учётных данных. Текст ошибки одинаковый для неизвестной почты и неверного пароля —
        /// так нельзя узнать, какие адреса зарегистрированы. После трёх неудачных попыток вход
        /// приостанавливается на 30 секунд.
        /// </summary>
        public User Login(string email, string password)
        {
            if (SecondsLocked > 0)
                throw new AppException(ErrorKind.Auth, "Слишком много неудачных попыток. Повторите вход через " + SecondsLocked + " с.");
            string mail = Validator.Email("email", email);
            if (string.IsNullOrEmpty(password)) throw AppException.Validation("password", "Введите пароль.");

            User u = users.FindByEmail(mail);
            if (u == null || !PasswordHasher.Verify(password, u.PasswordHash))
            {
                failedAttempts++;
                if (failedAttempts >= MaxAttempts)
                {
                    failedAttempts = 0;
                    lockedUntil = clock().AddSeconds(LockSeconds);
                    throw new AppException(ErrorKind.Auth, "Неверная электронная почта или пароль. Вход приостановлен на " + LockSeconds + " секунд.");
                }
                throw new AppException(ErrorKind.Auth, "Неверная электронная почта или пароль. Осталось попыток: " + (MaxAttempts - failedAttempts) + ".");
            }
            if (u.IsBlocked)
                throw new AppException(ErrorKind.Auth, "Учётная запись заблокирована. Обратитесь к администратору кинотеатра.");
            failedAttempts = 0;
            u.PasswordHash = null;   // хеш больше не нужен в памяти приложения
            return u;
        }

        /// <summary>Регистрация зрителя: проверка полей, уникальности почты, хеширование пароля.</summary>
        public User Register(string fullName, string email, string phone, string password, string confirm)
        {
            var u = new User
            {
                FullName = Validator.FullName("fullName", fullName),
                Email = Validator.Email("email", email),
                Phone = Validator.Phone("phone", phone),
                RoleId = Roles.ViewerId,
                RoleCode = Roles.Viewer,
                RoleName = "Зритель"
            };
            Validator.Password("password", password);
            if (password != confirm) throw AppException.Validation("confirm", "Пароли не совпадают.");
            if (users.EmailExists(u.Email, 0))
                throw AppException.Validation("email", "Пользователь с такой электронной почтой уже зарегистрирован.");
            u.PasswordHash = PasswordHasher.Hash(password);
            u.Id = users.Add(u);
            u.PasswordHash = null;
            u.CreatedAt = DateTime.Now;
            return u;
        }
    }

    /// <summary>Фильмы: проверка и сохранение, удаление с проверкой связей.</summary>
    public sealed class FilmService
    {
        private readonly FilmRepository repo = new FilmRepository();

        public List<Film> Search(string text, int genreId, bool onlyActive)
        {
            return repo.Search((text ?? "").Trim(), genreId, onlyActive);
        }

        public Film Validate(int id, string title, int genreId, string duration, string rating, string description, bool active)
        {
            return new Film
            {
                Id = id,
                Title = Validator.Required("title", "Название", title, 150),
                GenreId = Validator.Selected("genre", "Жанр", genreId),
                DurationMin = Validator.IntRange("duration", "Длительность, мин", duration, 1, 600),
                AgeRating = Validator.OneOf("rating", "Возрастной рейтинг", rating, Validator.AgeRatings),
                Description = Validator.Optional("description", "Описание", description, 1000),
                IsActive = active
            };
        }

        public int Save(User actor, Film f)
        {
            Roles.Require(Roles.CanEditCatalog(actor));
            if (f.Id == 0) return f.Id = repo.Add(f);
            repo.Update(f);
            return f.Id;
        }

        /// <summary>Что мешает удалить фильм (null — удалить можно).</summary>
        public string DeleteBlocker(int id)
        {
            int screenings, reviews;
            repo.LinkedRecords(id, out screenings, out reviews);
            if (screenings == 0 && reviews == 0) return null;
            var parts = new List<string>();
            if (screenings > 0) parts.Add("сеансов: " + screenings);
            if (reviews > 0) parts.Add("отзывов: " + reviews);
            return "С фильмом связаны записи (" + string.Join(", ", parts) + "). Удаление нарушит целостность базы данных.";
        }

        public void Delete(User actor, int id)
        {
            Roles.Require(Roles.CanEditCatalog(actor));
            repo.Delete(id);
        }

        public void SetActive(User actor, int id, bool active)
        {
            Roles.Require(Roles.CanEditCatalog(actor));
            repo.SetActive(id, active);
        }
    }

    /// <summary>Сеансы.</summary>
    public sealed class ScreeningService
    {
        private readonly ScreeningRepository repo = new ScreeningRepository();

        public List<Screening> Search(int period, int filmId)
        {
            return repo.Search(period, filmId, DateTime.Now);
        }

        public Screening Validate(int id, int filmId, int hallId, string startsAt, string format, string price, bool isNew)
        {
            var s = new Screening
            {
                Id = id,
                FilmId = Validator.Selected("film", "Фильм", filmId),
                HallId = Validator.Selected("hall", "Зал", hallId),
                StartsAt = Validator.DateTimeValue("start", startsAt),
                Format = Validator.OneOf("format", "Формат", format, Validator.Formats),
                BasePrice = Validator.Price("price", price)
            };
            if (isNew && s.StartsAt < DateTime.Now)
                throw AppException.Validation("start", "Новый сеанс нельзя назначить на прошедшее время.");
            return s;
        }

        public int Save(User actor, Screening s)
        {
            Roles.Require(Roles.CanEditCatalog(actor));
            string overlap = repo.FindOverlap(s);
            if (overlap != null)
                throw AppException.Validation("start", "Зал в это время занят: идёт сеанс «" + overlap + "» (с учётом длительности фильма и 15 минут на уборку).");
            if (s.Id == 0) return s.Id = repo.Add(s);
            repo.Update(s);
            return s.Id;
        }

        public string DeleteBlocker(int id)
        {
            int orders = repo.OrderCount(id);
            return orders == 0 ? null : "На сеанс оформлено заказов: " + orders + ". Удаление нарушит целостность базы данных.";
        }

        public void Delete(User actor, int id)
        {
            Roles.Require(Roles.CanEditCatalog(actor));
            repo.Delete(id);
        }
    }

    /// <summary>Пользователи (раздел администратора).</summary>
    public sealed class UserService
    {
        private readonly UserRepository repo = new UserRepository();

        public List<User> Search(string text, int roleId)
        {
            return repo.Search((text ?? "").Trim(), roleId);
        }

        /// <summary>password пустой при изменении — пароль остаётся прежним.</summary>
        public User Validate(int id, string fullName, string email, string phone, int roleId, bool blocked, string password)
        {
            var u = new User
            {
                Id = id,
                FullName = Validator.FullName("fullName", fullName),
                Email = Validator.Email("email", email),
                Phone = Validator.Phone("phone", phone),
                RoleId = Validator.Selected("role", "Роль", roleId),
                IsBlocked = blocked
            };
            if (id == 0 || !string.IsNullOrEmpty(password))
                u.PasswordHash = PasswordHasher.Hash(Validator.Password("password", password));
            return u;
        }

        public int Save(User actor, User u)
        {
            Roles.Require(Roles.CanManageUsers(actor));
            if (u.Id == actor.Id && (u.IsBlocked || u.RoleId != actor.RoleId))
                throw AppException.Validation("role", "Нельзя заблокировать себя или сменить себе роль — вы потеряете доступ к разделу.");
            if (repo.EmailExists(u.Email, u.Id))
                throw AppException.Validation("email", "Пользователь с такой электронной почтой уже зарегистрирован.");
            if (u.Id == 0) return u.Id = repo.Add(u);
            repo.Update(u);
            return u.Id;
        }

        public string DeleteBlocker(User actor, int id)
        {
            if (id == actor.Id) return "Нельзя удалить учётную запись, под которой вы сейчас работаете.";
            int linked = repo.LinkedRecords(id);
            return linked == 0 ? null : "С пользователем связаны заказы или отзывы (" + linked + "). Удаление нарушит целостность базы данных.";
        }

        public void Delete(User actor, int id)
        {
            Roles.Require(Roles.CanManageUsers(actor));
            repo.Delete(id);
        }

        public void SetBlocked(User actor, int id, bool blocked)
        {
            Roles.Require(Roles.CanManageUsers(actor));
            if (id == actor.Id) throw new AppException(ErrorKind.Validation, "Нельзя заблокировать свою учётную запись.");
            repo.SetBlocked(id, blocked);
        }
    }

    /// <summary>Заказы.</summary>
    public sealed class OrderService
    {
        private readonly OrderRepository repo = new OrderRepository();
        public const int CancelledId = 5, ReturnedId = 6;

        public List<Order> Search(User actor, string text, int statusId)
        {
            int own = Roles.SeesOnlyOwnOrders(actor) ? actor.Id : 0;
            return repo.Search((text ?? "").Trim(), statusId, own);
        }

        public void ChangeStatus(User actor, Order o, int statusId)
        {
            Roles.Require(Roles.CanManageOrders(actor));
            if (statusId == o.StatusId) throw AppException.Validation("status", "Выберите статус, отличный от текущего.");
            bool wasFree = o.StatusId == CancelledId || o.StatusId == ReturnedId;
            bool becomesActive = statusId != CancelledId && statusId != ReturnedId;
            if (wasFree && becomesActive && repo.SeatConflicts(o.Id) > 0)
                throw AppException.Validation("status", "Места этого заказа уже проданы по другому заказу. Вернуть заказ в действующие нельзя.");
            repo.ChangeStatus(o.Id, statusId);
        }

        public string DeleteBlocker(Order o)
        {
            return o.PaymentCount == 0 ? null
                : "По заказу проходили платежи (" + o.PaymentCount + "). Это финансовые записи, их удалять нельзя — измените статус заказа.";
        }

        public void Delete(User actor, int id)
        {
            Roles.Require(Roles.CanDeleteOrders(actor));
            repo.Delete(id);
        }
    }
}
