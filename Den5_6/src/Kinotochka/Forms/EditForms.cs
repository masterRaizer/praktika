using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Forms
{
    /// <summary>Строка «тумблер + подпись».</summary>
    internal static class SwitchRow
    {
        public static FxBox Make(FxSwitch sw, string caption)
        {
            var row = FxBox.H(10);
            row.Add(sw, 46, true);
            row.Fill(new FxLabel(caption, Fonts.Medium(9.2f), Tone.Dim));
            row.Height = 28;
            return row;
        }
    }

    /// <summary>Форма 5. Добавление и изменение фильма.</summary>
    public sealed class FilmEditForm : EditDialog
    {
        private readonly FilmService service;
        private readonly User actor;
        private readonly int id;
        private readonly FxTextBox title, duration, description;
        private readonly FxSelect genre, rating;
        private readonly FxSwitch active;

        public FilmEditForm(FilmService service, User actor, Film film, IEnumerable<LookupItem> genres)
        {
            this.service = service;
            this.actor = actor;
            id = film != null ? film.Id : 0;
            Title = film == null ? "Новый фильм" : "Изменение фильма";
            Subtitle = film == null ? "Добавление записи в таблицу Фильмы" : "Фильм № " + film.Id + " — " + film.Title;
            IconName = "film";
            Text = "Киноточка — " + Title.ToLowerInvariant();
            Size = new Size(600, 520);

            title = Register("title", Input("название фильма", film != null ? film.Title : ""));
            genre = Register("genre", new FxSelect());
            genre.SetItems(Ui.Items(genres), true, "— выберите жанр —");
            duration = Register("duration", Input("1–600", film != null ? film.DurationMin.ToString() : ""));
            rating = Register("rating", new FxSelect());
            rating.SetItems(Validator.AgeRatings);
            description = Register("description", Input("краткое описание (необязательно)", film != null ? film.Description : ""));
            description.Box.Multiline = true;
            description.Box.ScrollBars = ScrollBars.Vertical;
            active = new FxSwitch { IconOn = "check" };
            active.SetSilently(film == null || film.IsActive);
            if (film != null)
            {
                genre.SelectedId = film.GenreId;
                rating.SelectedText = film.AgeRating;
            }

            Body.Add(Field("Название *", title), 0);
            Body.Add(Row(Field("Жанр *", genre), Field("Длительность, мин *", duration), Field("Возраст *", rating)), 0);
            Body.Add(Field("Описание", description, 96), 0);
            Body.Add(SwitchRow.Make(active, "Фильм в прокате (показывается в афише)"), 0);

            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Сохранить", true, "check", delegate { Save(); });
            Shown += delegate { title.FocusBox(); };
        }

        private void Save()
        {
            if (!Try("сохранение фильма", delegate
                {
                    Film f = service.Validate(id, title.Value, genre.SelectedId, duration.Value, rating.SelectedText, description.Value, active.Checked);
                    service.Save(actor, f);
                })) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Форма 6. Добавление и изменение сеанса.</summary>
    public sealed class ScreeningEditForm : EditDialog
    {
        private readonly ScreeningService service;
        private readonly User actor;
        private readonly int id;
        private readonly FxSelect film, hall, format;
        private readonly FxTextBox start, price;

        public ScreeningEditForm(ScreeningService service, User actor, Screening s, IEnumerable<LookupItem> films, IEnumerable<LookupItem> halls)
        {
            this.service = service;
            this.actor = actor;
            id = s != null ? s.Id : 0;
            Title = s == null ? "Новый сеанс" : "Изменение сеанса";
            Subtitle = s == null ? "Добавление записи в таблицу Сеансы" : "Сеанс № " + s.Id + " — " + s.FilmTitle;
            IconName = "calendar";
            Text = "Киноточка — " + Title.ToLowerInvariant();
            Size = new Size(620, 460);

            film = Register("film", new FxSelect());
            film.SetItems(Ui.Items(films), true, "— выберите фильм —");
            hall = Register("hall", new FxSelect());
            hall.SetItems(Ui.Items(halls), true, "— выберите зал —");
            format = Register("format", new FxSelect());
            format.SetItems(Validator.Formats);
            DateTime suggested = DateTime.Today.AddDays(1).AddHours(19);
            start = Register("start", Input("дд.мм.гггг чч:мм", Ui.DateTimeText(s != null ? s.StartsAt : suggested)));
            price = Register("price", Input("например 450", s != null ? s.BasePrice.ToString("0.##", Ui.Ru) : ""));
            if (s != null)
            {
                film.SelectedId = s.FilmId;
                hall.SelectedId = s.HallId;
                format.SelectedText = s.Format;
            }

            Body.Add(Field("Фильм *", film), 0);
            Body.Add(Field("Зал *", hall), 0);
            Body.Add(Row(Field("Начало (дд.мм.гггг чч:мм) *", start), Field("Формат *", format), Field("Базовая цена, руб. *", price)), 0);
            Body.Add(new FxLabel("Цена билета = базовая цена × коэффициент типа места (стандарт 1,0; комфорт 1,2; VIP 1,6; диван 1,8). " +
                                 "Программа не даст поставить два сеанса в один зал в одно время.", Fonts.Body(8.5f), Tone.Mute, true), 0);

            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Сохранить", true, "check", delegate { Save(); });
        }

        private void Save()
        {
            if (!Try("сохранение сеанса", delegate
                {
                    Screening s = service.Validate(id, film.SelectedId, hall.SelectedId, start.Value, format.SelectedText, price.Value, id == 0);
                    service.Save(actor, s);
                })) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Форма 7. Управление пользователями: добавление и изменение учётной записи.</summary>
    public sealed class UserEditForm : EditDialog
    {
        private readonly UserService service;
        private readonly User actor;
        private readonly int id;
        private readonly FxTextBox fullName, email, phone, password;
        private readonly FxSelect role;
        private readonly FxSwitch blocked;

        public UserEditForm(UserService service, User actor, User u, IEnumerable<LookupItem> roles)
        {
            this.service = service;
            this.actor = actor;
            id = u != null ? u.Id : 0;
            Title = u == null ? "Новый пользователь" : "Изменение пользователя";
            Subtitle = u == null ? "Добавление записи в таблицу Пользователи" : "Пользователь № " + u.Id + " — " + u.Email;
            IconName = "user";
            Text = "Киноточка — " + Title.ToLowerInvariant();
            Size = new Size(600, 470);

            fullName = Register("fullName", Input("Фамилия Имя Отчество", u != null ? u.FullName : ""));
            email = Register("email", Input("name@example.com", u != null ? u.Email : ""));
            phone = Register("phone", Input("+7 900 123-45-67", u != null ? u.Phone : ""));
            role = Register("role", new FxSelect());
            role.SetItems(Ui.Items(roles), true, "— выберите роль —");
            password = Register("password", Input(u == null ? "не короче 6 символов, буквы и цифры" : "оставьте пустым, чтобы не менять"));
            password.Box.UseSystemPasswordChar = true;
            blocked = new FxSwitch { IconOn = "lock" };
            if (u != null)
            {
                role.SelectedId = u.RoleId;
                blocked.SetSilently(u.IsBlocked);
            }
            else role.SelectedId = Roles.ViewerId;

            Body.Add(Field("ФИО *", fullName), 0);
            Body.Add(Row(Field("Электронная почта *", email), Field("Телефон", phone)), 0);
            Body.Add(Row(Field("Роль *", role), Field(u == null ? "Пароль *" : "Новый пароль", password)), 0);
            Body.Add(SwitchRow.Make(blocked, "Учётная запись заблокирована (вход запрещён)"), 0);

            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Сохранить", true, "check", delegate { Save(); });
        }

        private void Save()
        {
            if (!Try("сохранение пользователя", delegate
                {
                    User u = service.Validate(id, fullName.Value, email.Value, phone.Value, role.SelectedId, blocked.Checked, password.Value);
                    service.Save(actor, u);
                })) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Форма 8. Смена статуса заказа (кассир, администратор).</summary>
    public sealed class OrderStatusForm : EditDialog
    {
        private readonly OrderService service;
        private readonly User actor;
        private readonly Order order;
        private readonly FxSelect status;

        public OrderStatusForm(OrderService service, User actor, Order order, IEnumerable<LookupItem> statuses)
        {
            this.service = service;
            this.actor = actor;
            this.order = order;
            Title = "Статус заказа № " + order.Id;
            Subtitle = order.Customer + " — " + order.ScreeningText;
            IconName = "ticket";
            Text = "Киноточка — статус заказа";
            Size = new Size(560, 370);

            status = Register("status", new FxSelect());
            status.SetItems(Ui.Items(statuses));
            status.SelectedId = order.StatusId;

            Body.Add(new FxLabel("Текущий статус: " + order.StatusName + ". Билетов: " + order.TicketCount + ", сумма " + Ui.Money(order.Total) + " руб.",
                Fonts.Medium(9.5f), Tone.Text, true), 0);
            Body.Add(Field("Новый статус *", status), 0);
            Body.Add(new FxLabel("Статусы — по диаграмме состояний заказа (день 2): забронирован → оплачивается → оплачен → завершён; " +
                                 "отменённый и возвращённый заказ освобождает места.", Fonts.Body(8.5f), Tone.Mute, true), 0);

            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Сохранить", true, "check", delegate { Save(); });
        }

        private void Save()
        {
            if (!Try("смена статуса заказа", delegate { service.ChangeStatus(actor, order, status.SelectedId); })) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
