using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Data;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Forms
{
    /// <summary>
    /// Форма 4. Главное окно: разделы в боковой панели, набор разделов зависит от роли пользователя.
    /// Каждый раздел — таблица записей с поиском, фильтром и кнопками действий.
    /// </summary>
    public sealed class MainForm : FxWindow
    {
        private readonly User user;
        private readonly LookupRepository lookups = new LookupRepository();
        private readonly FilmService films = new FilmService();
        private readonly ScreeningService screenings = new ScreeningService();
        private readonly OrderService orders = new OrderService();
        private readonly UserService users = new UserService();
        private readonly DashboardRepository dashboard = new DashboardRepository();
        private readonly Dictionary<int, Action> loaders = new Dictionary<int, Action>();
        private readonly HashSet<int> loaded = new HashSet<int>();

        // главная
        private FxStat statFilms, statScreenings, statOrders, statRevenue;
        private FxTable upcomingTable;
        // фильмы
        private FxTable filmTable;
        private FxTextBox filmSearch;
        private FxSelect filmGenre, filmActive;
        private FxLabel filmCount;
        // сеансы
        private FxTable screeningTable;
        private FxSelect screeningPeriod, screeningFilm;
        private FxLabel screeningCount;
        // заказы
        private FxTable orderTable;
        private FxTextBox orderSearch;
        private FxSelect orderStatus;
        private FxLabel orderCount;
        // пользователи
        private FxTable userTable;
        private FxTextBox userSearch;
        private FxSelect userRole;
        private FxLabel userCount;

        /// <summary>Пользователь нажал «Выйти» — программа снова покажет форму входа.</summary>
        public bool LoggedOut { get; private set; }
        public int StartPage { get; set; }

        public MainForm(User user)
        {
            this.user = user;
            Text = Ui.AppName + " — главная";
            AppTitle = Ui.AppName;
            AppCaption = "продажа билетов в кино";
            AppIcon = "film";
            AppLogo = Ui.LoadLogo();
            Ui.ApplyIcon(this);
            ShowSplash = false;
            UserName = user.FullName;
            UserRole = user.RoleName;
            LogoutRequested += delegate
            {
                if (!FxDialog.Confirm(this, "Выход из учётной записи", "Завершить работу пользователя " + user.FullName + " и вернуться к форме входа?", "Выйти", false))
                    return;
                LoggedOut = true;
                CloseAnimated();
            };

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1400, area.Width - 24), Math.Min(880, area.Height - 24));
            MinimumSize = new Size(Math.Min(1100, area.Width), Math.Min(660, area.Height));

            AddSection("home", "Главная", "афиша и показатели", "Добро пожаловать, " + FirstName(user.FullName),
                "Роль: " + user.RoleName.ToLowerInvariant() + ". " + RoleHint(), BuildHome(), LoadHome);
            AddSection("film", "Фильмы", "каталог фильмов", "Фильмы",
                Roles.CanEditCatalog(user) ? "Таблица Фильмы: поиск, добавление, изменение и удаление" : "Каталог фильмов кинотеатра", BuildFilms(), LoadFilms);
            AddSection("calendar", "Сеансы", "расписание", "Сеансы",
                Roles.CanEditCatalog(user) ? "Таблица Сеансы: расписание залов, цены, продажи" : "Расписание сеансов и свободные места", BuildScreenings(), LoadScreenings);
            AddSection("ticket", Roles.SeesOnlyOwnOrders(user) ? "Мои заказы" : "Заказы", "билеты и статусы",
                Roles.SeesOnlyOwnOrders(user) ? "Мои заказы" : "Заказы",
                Roles.SeesOnlyOwnOrders(user) ? "Ваши заказы билетов и их статус" : "Таблица Заказы: суммы из представления СуммыЗаказов, смена статуса", BuildOrders(), LoadOrders);
            if (Roles.CanManageUsers(user))
                AddSection("users", "Пользователи", "учётные записи", "Пользователи",
                    "Таблица Пользователи: роли, блокировка, пароли (только хеши)", BuildUsers(), LoadUsers);

            PageShown += delegate(int index)
            {
                Text = Ui.AppName + " — " + Items[index].Title.ToLowerInvariant();
                if (!loaded.Contains(index)) Reload(index);
            };
            Load += delegate
            {
                int page = StartPage >= 0 && StartPage < Items.Count ? StartPage : 0;
                Reload(page);
                Navigate(page, false);
            };
        }

        private static string FirstName(string fullName)
        {
            string[] p = (fullName ?? "").Split(' ');
            return p.Length >= 2 ? p[1] : fullName;
        }

        private string RoleHint()
        {
            if (Roles.CanManageUsers(user)) return "Доступны все разделы и редактирование данных.";
            if (Roles.CanManageOrders(user)) return "Просмотр каталога и расписания, работа с заказами.";
            return "Афиша, каталог фильмов и ваши заказы.";
        }

        private void AddSection(string icon, string title, string hint, string header, string sub, Control page, Action loader)
        {
            loaders[Items.Count] = loader;
            AddPage(icon, title, hint, header, sub, page);
        }

        private void Reload(int index)
        {
            Action loader;
            if (!loaders.TryGetValue(index, out loader)) return;
            try
            {
                Cursor = Cursors.WaitCursor;
                loader();
                loaded.Add(index);
            }
            catch (Exception ex)
            {
                AppException a = ErrorTranslator.Translate(ex, "загрузка раздела «" + Items[index].Title + "»");
                MarkUnavailable(a);
                Ui.ShowError(this, a, "загрузка раздела «" + Items[index].Title + "»");
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void MarkUnavailable(AppException a)
        {
            string text = a.Kind == ErrorKind.Connection ? "нет подключения к базе данных — нажмите «Обновить»" : "данные не загружены";
            foreach (FxTable t in new[] { upcomingTable, filmTable, screeningTable, orderTable, userTable })
            {
                if (t == null) continue;
                t.Clear();
                t.EmptyText = text;
            }
        }

        // =====================================================================
        //  Общие элементы разделов
        // =====================================================================

        private static FxTable MakeTable(params string[] columns)
        {
            var t = new FxTable { RowHeight = 34, Selectable = true, WrapCells = true, FontSize = 9f };
            t.SetColumns(columns);
            return t;
        }

        private static FxButton Tool(string text, string icon, bool primary, EventHandler click)
        {
            var b = new FxButton(text, primary, icon).AutoWidth();
            b.Height = 36;
            b.Click += click;
            return b;
        }

        /// <summary>Раздел: панель поиска и кнопок, карточка с таблицей, строка состояния.</summary>
        private static Control Section(Control toolbar, string cardTitle, string cardIcon, FxTable table, FxLabel count)
        {
            var card = new FxCard(cardTitle, cardIcon) { Padding = new Padding(14, 12, 10, 10) };
            card.With(table);
            var page = FxBox.V(10).Pad(0, 6, 6, 6);
            page.Add(toolbar, 40);
            page.Fill(card);
            page.Add(count, 22);
            return page;
        }

        private static FxLabel CountLabel()
        {
            return new FxLabel("", Fonts.Medium(8.8f), Tone.Mute) { Icon = "info" };
        }

        /// <summary>Выделенная строка; если ничего не выбрано — подсказка.</summary>
        private T Selected<T>(FxTable table, string what) where T : class
        {
            FxRow row = table.SelectedRow;
            if (row == null)
            {
                Toast("Не выбрана запись", "Щёлкните строку " + what + " в таблице, затем повторите действие.", Tone.Warn);
                return null;
            }
            return row.Tag as T;
        }

        private bool Run(string action, Action body)
        {
            try
            {
                body();
                return true;
            }
            catch (Exception ex)
            {
                Ui.ShowError(this, ex, action);
                return false;
            }
        }

        // =====================================================================
        //  Главная
        // =====================================================================

        private Control BuildHome()
        {
            statFilms = new FxStat("Фильмов в прокате", 0) { Tone = Tone.Accent2 };
            statScreenings = new FxStat("Предстоящих сеансов", 2) { Tone = Tone.Good };
            statOrders = new FxStat("Действующих заказов", 3) { Tone = Tone.Warn };
            statRevenue = new FxStat("Выручка, руб.", 5) { Tone = Tone.Accent };
            var tiles = FxBox.H(10);
            tiles.Fill(statFilms).Fill(statScreenings).Fill(statOrders).Fill(statRevenue);

            upcomingTable = MakeTable("Дата и время", "Фильм", "Зал", "Формат", "Цена, руб.", "Свободно мест");
            upcomingTable.Columns[1].Weight = 2f;
            upcomingTable.Columns[1].Align = StringAlignment.Near;
            upcomingTable.Columns[2].Weight = 1.1f;
            upcomingTable.RowActivated += delegate { Navigate(2); };
            var card = new FxCard("Афиша: ближайшие сеансы", "calendar") { Padding = new Padding(14, 12, 10, 10) };
            card.With(upcomingTable);
            var refresh = new FxButton("Обновить", false, "refresh").AutoWidth();
            refresh.Height = 28;
            refresh.Click += delegate { Reload(0); };
            card.WithHeader(refresh);

            var page = FxBox.V(12).Pad(0, 6, 6, 6);
            page.Add(tiles, 78).Fill(card);
            return page;
        }

        private void LoadHome()
        {
            Dashboard d = dashboard.Load(DateTime.Now);
            statFilms.Set(d.Films, 0);
            statFilms.Sub = "в таблице Фильмы";
            statScreenings.Set(d.UpcomingScreenings, 0);
            statScreenings.Sub = "с " + Ui.DateText(DateTime.Today);
            statOrders.Set(d.ActiveOrders, 0);
            statOrders.Sub = "забронировано и оплачено";
            if (Roles.CanManageOrders(user))
            {
                statRevenue.Set((double)d.Revenue, 0);
                statRevenue.Sub = "оплаты минус возвраты";
            }
            else
            {
                statRevenue.Caption = "Пользователей";
                statRevenue.Set(d.Users, 0);
                statRevenue.Sub = "зарегистрировано";
            }

            upcomingTable.Clear();
            upcomingTable.EmptyText = "нет предстоящих сеансов";
            foreach (Screening s in screenings.Search(1, 0).Take(12))
            {
                FxRow row = upcomingTable.AddRow(Ui.DateTimeText(s.StartsAt), s.FilmTitle, s.HallName, s.Format, Ui.Money(s.BasePrice), s.Capacity - s.SoldSeats);
                row.Tag = s;
            }
        }

        // =====================================================================
        //  Фильмы
        // =====================================================================

        private Control BuildFilms()
        {
            filmSearch = new FxTextBox { Placeholder = "Поиск по названию" };
            filmSearch.EnterPressed += delegate { Reload(1); };
            filmGenre = new FxSelect();
            filmGenre.SelectedChanged += delegate { if (loaded.Contains(1)) Reload(1); };
            filmActive = new FxSelect();
            filmActive.SetItems("Все фильмы", "Только в прокате");
            filmActive.SelectedChanged += delegate { if (loaded.Contains(1)) Reload(1); };

            var bar = FxBox.H(8);
            bar.Fill(filmSearch, 1.4f).Add(Tool("Найти", "search", false, delegate { Reload(1); }), 0);
            bar.Fill(filmGenre, 0.9f).Fill(filmActive, 0.8f);
            if (Roles.CanEditCatalog(user))
            {
                bar.Add(Tool("Добавить", "plus", true, delegate { EditFilm(null); }), 0);
                bar.Add(Tool("Изменить", "edit", false, delegate { EditFilm(Selected<Film>(filmTable, "фильма")); }), 0);
                bar.Add(Tool("Удалить", "trash", false, delegate { DeleteFilm(); }), 0);
            }

            filmTable = MakeTable("Код", "Название", "Жанр", "Длительность, мин", "Возраст", "Сеансов", "В прокате");
            filmTable.Columns[0].Weight = 0.5f;
            filmTable.Columns[1].Weight = 2.2f;
            filmTable.Columns[1].Align = StringAlignment.Near;
            filmTable.Columns[2].Weight = 1.1f;
            filmTable.Columns[3].Weight = 1.1f;
            if (Roles.CanEditCatalog(user)) filmTable.RowActivated += r => EditFilm(r.Tag as Film);
            filmCount = CountLabel();
            return Section(bar, "Таблица «Фильмы»", "film", filmTable, filmCount);
        }

        private void LoadFilms()
        {
            if (filmGenre.Box.Items.Count == 0)
            {
                filmGenre.SetItems(Ui.Items(lookups.Genres()), true, "Все жанры");
            }
            List<Film> list = films.Search(filmSearch.Value, filmGenre.SelectedId, filmActive.SelectedId == 1);
            filmTable.Clear();
            filmTable.EmptyText = "ничего не найдено — измените условия поиска";
            foreach (Film f in list)
            {
                FxRow row = filmTable.AddRow(f.Id, f.Title, f.GenreName, f.DurationMin, f.AgeRating, f.ScreeningCount, f.IsActive ? "да" : "нет");
                row.Tag = f;
                if (!f.IsActive) filmTable.SetCellTone(row, 6, RowTone.Bad);
            }
            filmCount.Text = "Найдено фильмов: " + list.Count + (filmSearch.Value.Trim().Length > 0 ? " по запросу «" + filmSearch.Value.Trim() + "»" : "") +
                             (Roles.CanEditCatalog(user) ? ". Двойной щелчок по строке — изменить." : ".");
        }

        private void EditFilm(Film f)
        {
            List<LookupItem> genres = null;
            if (!Run("чтение жанров", delegate { genres = lookups.Genres(); })) return;
            using (var form = new FilmEditForm(films, user, f, genres))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }
            Reload(1);
            loaded.Remove(0);
            Toast(f == null ? "Фильм добавлен" : "Фильм изменён", "Изменения сохранены в таблице Фильмы.", Tone.Good);
        }

        private void DeleteFilm()
        {
            var f = Selected<Film>(filmTable, "фильма");
            if (f == null) return;
            string blocker = null;
            if (!Run("проверка связей фильма", delegate { blocker = films.DeleteBlocker(f.Id); })) return;
            if (blocker != null)
            {
                if (f.IsActive && FxDialog.Confirm(this, "Удалить фильм нельзя",
                        blocker + "\n\nСнять фильм «" + f.Title + "» с проката? Он останется в базе, но не будет показываться в афише.",
                        "Снять с проката", false))
                {
                    if (Run("снятие фильма с проката", delegate { films.SetActive(user, f.Id, false); }))
                    {
                        Reload(1);
                        Toast("Фильм снят с проката", f.Title, Tone.Good);
                    }
                }
                else if (!f.IsActive)
                {
                    FxDialog.Info(this, "Удалить фильм нельзя", blocker, Tone.Bad);
                }
                return;
            }
            if (!FxDialog.Confirm(this, "Удаление фильма", "Удалить фильм «" + f.Title + "» (код " + f.Id + ")?\nДействие нельзя отменить.", "Удалить", true))
                return;
            if (Run("удаление фильма", delegate { films.Delete(user, f.Id); }))
            {
                Reload(1);
                Toast("Фильм удалён", f.Title, Tone.Good);
            }
        }

        // =====================================================================
        //  Сеансы
        // =====================================================================

        private Control BuildScreenings()
        {
            screeningPeriod = new FxSelect();
            screeningPeriod.SetItems("Все сеансы", "Предстоящие", "Прошедшие");
            screeningPeriod.SelectedId = 1;
            screeningPeriod.SelectedChanged += delegate { if (loaded.Contains(2)) Reload(2); };
            screeningFilm = new FxSelect();
            screeningFilm.SelectedChanged += delegate { if (loaded.Contains(2)) Reload(2); };

            var bar = FxBox.H(8);
            bar.Fill(screeningPeriod, 0.8f).Fill(screeningFilm, 1.3f);
            bar.Add(Tool("Обновить", "refresh", false, delegate { Reload(2); }), 0);
            if (Roles.CanEditCatalog(user))
            {
                bar.Add(Tool("Добавить", "plus", true, delegate { EditScreening(null); }), 0);
                bar.Add(Tool("Изменить", "edit", false, delegate { EditScreening(Selected<Screening>(screeningTable, "сеанса")); }), 0);
                bar.Add(Tool("Удалить", "trash", false, delegate { DeleteScreening(); }), 0);
            }

            screeningTable = MakeTable("Код", "Дата и время", "Фильм", "Зал", "Формат", "Цена, руб.", "Продано", "Свободно");
            screeningTable.Columns[0].Weight = 0.5f;
            screeningTable.Columns[1].Weight = 1.2f;
            screeningTable.Columns[2].Weight = 2f;
            screeningTable.Columns[2].Align = StringAlignment.Near;
            if (Roles.CanEditCatalog(user)) screeningTable.RowActivated += r => EditScreening(r.Tag as Screening);
            screeningCount = CountLabel();
            return Section(bar, "Таблица «Сеансы»", "calendar", screeningTable, screeningCount);
        }

        private void LoadScreenings()
        {
            if (screeningFilm.Box.Items.Count == 0)
                screeningFilm.SetItems(Ui.Items(lookups.FilmsShort()), true, "Все фильмы");
            List<Screening> list = screenings.Search(screeningPeriod.SelectedId, screeningFilm.SelectedId);
            screeningTable.Clear();
            screeningTable.EmptyText = "сеансов нет";
            DateTime now = DateTime.Now;
            foreach (Screening s in list)
            {
                int free = s.Capacity - s.SoldSeats;
                FxRow row = screeningTable.AddRow(s.Id, Ui.DateTimeText(s.StartsAt), s.FilmTitle, s.HallName, s.Format, Ui.Money(s.BasePrice), s.SoldSeats, free);
                row.Tag = s;
                if (free == 0) screeningTable.SetCellTone(row, 7, RowTone.Bad);
                else if (s.StartsAt >= now) screeningTable.SetCellTone(row, 7, RowTone.Good);
            }
            screeningCount.Text = "Сеансов: " + list.Count + ". Проданные места считаются по заказам со статусами «Забронирован», «Оплачивается», «Оплачен», «Завершён».";
        }

        private void EditScreening(Screening s)
        {
            List<LookupItem> filmList = null, halls = null;
            if (!Run("чтение справочников", delegate { filmList = lookups.FilmsShort(); halls = lookups.Halls(); })) return;
            using (var form = new ScreeningEditForm(screenings, user, s, filmList, halls))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }
            Reload(2);
            loaded.Remove(0);
            loaded.Remove(1);
            Toast(s == null ? "Сеанс добавлен" : "Сеанс изменён", "Изменения сохранены в таблице Сеансы.", Tone.Good);
        }

        private void DeleteScreening()
        {
            var s = Selected<Screening>(screeningTable, "сеанса");
            if (s == null) return;
            string blocker = null;
            if (!Run("проверка заказов сеанса", delegate { blocker = screenings.DeleteBlocker(s.Id); })) return;
            if (blocker != null)
            {
                FxDialog.Info(this, "Удалить сеанс нельзя", blocker + "\n\nСначала отмените или удалите заказы этого сеанса.", Tone.Bad);
                return;
            }
            if (!FxDialog.Confirm(this, "Удаление сеанса",
                    "Удалить сеанс «" + s.FilmTitle + "» " + Ui.DateTimeText(s.StartsAt) + ", зал «" + s.HallName + "»?\nДействие нельзя отменить.", "Удалить", true))
                return;
            if (Run("удаление сеанса", delegate { screenings.Delete(user, s.Id); }))
            {
                Reload(2);
                loaded.Remove(0);
                loaded.Remove(1);
                Toast("Сеанс удалён", s.FilmTitle + ", " + Ui.DateTimeText(s.StartsAt), Tone.Good);
            }
        }

        // =====================================================================
        //  Заказы
        // =====================================================================

        private Control BuildOrders()
        {
            orderSearch = new FxTextBox { Placeholder = Roles.SeesOnlyOwnOrders(user) ? "Поиск по фильму или номеру" : "Поиск: покупатель, фильм или номер заказа" };
            orderSearch.EnterPressed += delegate { Reload(3); };
            orderStatus = new FxSelect();
            orderStatus.SelectedChanged += delegate { if (loaded.Contains(3)) Reload(3); };

            var bar = FxBox.H(8);
            bar.Fill(orderSearch, 1.5f).Add(Tool("Найти", "search", false, delegate { Reload(3); }), 0).Fill(orderStatus, 0.9f);
            if (Roles.CanManageOrders(user))
                bar.Add(Tool("Сменить статус", "edit", true, delegate { ChangeOrderStatus(); }), 0);
            if (Roles.CanDeleteOrders(user))
                bar.Add(Tool("Удалить", "trash", false, delegate { DeleteOrder(); }), 0);

            orderTable = MakeTable("№", "Создан", "Покупатель", "Сеанс", "Билетов", "Итого, руб.", "Промокод", "Статус");
            orderTable.Columns[0].Weight = 0.45f;
            orderTable.Columns[1].Weight = 1.1f;
            orderTable.Columns[2].Weight = 1.7f;
            orderTable.Columns[2].Align = StringAlignment.Near;
            orderTable.Columns[3].Weight = 2.1f;
            orderTable.Columns[3].Align = StringAlignment.Near;
            orderTable.Columns[4].Weight = 0.7f;
            if (Roles.CanManageOrders(user)) orderTable.RowActivated += delegate { ChangeOrderStatus(); };
            orderCount = CountLabel();
            return Section(bar, Roles.SeesOnlyOwnOrders(user) ? "Мои заказы" : "Таблица «Заказы»", "ticket", orderTable, orderCount);
        }

        private void LoadOrders()
        {
            if (orderStatus.Box.Items.Count == 0)
                orderStatus.SetItems(Ui.Items(lookups.Statuses()), true, "Все статусы");
            List<Order> list = orders.Search(user, orderSearch.Value, orderStatus.SelectedId);
            orderTable.Clear();
            orderTable.EmptyText = Roles.SeesOnlyOwnOrders(user) ? "у вас пока нет заказов" : "заказов не найдено";
            foreach (Order o in list)
            {
                FxRow row = orderTable.AddRow(o.Id, Ui.DateTimeText(o.CreatedAt), o.Customer, o.ScreeningText, o.TicketCount,
                    Ui.Money(o.Total), o.PromoCode ?? "—", o.StatusName);
                row.Tag = o;
                RowTone tone = o.StatusId == OrderService.CancelledId || o.StatusId == OrderService.ReturnedId ? RowTone.Bad
                    : o.StatusCode == "ОПЛАЧЕН" || o.StatusCode == "ЗАВЕРШЁН" ? RowTone.Good : RowTone.Accent;
                orderTable.SetCellTone(row, 7, tone);
            }
            orderCount.Text = "Заказов: " + list.Count + ", на сумму " + Ui.Money(list.Sum(o => o.Total)) + " руб. Сумма заказа считается в представлении СуммыЗаказов с учётом промокода.";
        }

        private void ChangeOrderStatus()
        {
            var o = Selected<Order>(orderTable, "заказа");
            if (o == null) return;
            List<LookupItem> statuses = null;
            if (!Run("чтение статусов", delegate { statuses = lookups.Statuses(); })) return;
            using (var form = new OrderStatusForm(orders, user, o, statuses))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }
            Reload(3);
            loaded.Remove(0);
            loaded.Remove(2);
            Toast("Статус изменён", "Заказ № " + o.Id, Tone.Good);
        }

        private void DeleteOrder()
        {
            var o = Selected<Order>(orderTable, "заказа");
            if (o == null) return;
            string blocker = orders.DeleteBlocker(o);
            if (blocker != null)
            {
                FxDialog.Info(this, "Удалить заказ нельзя", blocker, Tone.Bad);
                return;
            }
            if (!FxDialog.Confirm(this, "Удаление заказа",
                    "Удалить заказ № " + o.Id + " (" + o.Customer + ")?\nВместе с ним будут удалены его билеты: " + o.TicketCount +
                    " (каскадное удаление по связи Заказы → Билеты). Действие нельзя отменить.", "Удалить", true))
                return;
            if (Run("удаление заказа", delegate { orders.Delete(user, o.Id); }))
            {
                Reload(3);
                loaded.Remove(0);
                loaded.Remove(2);
                Toast("Заказ удалён", "№ " + o.Id + ", билетов: " + o.TicketCount, Tone.Good);
            }
        }

        // =====================================================================
        //  Пользователи
        // =====================================================================

        private Control BuildUsers()
        {
            userSearch = new FxTextBox { Placeholder = "Поиск: ФИО, почта или телефон" };
            userSearch.EnterPressed += delegate { Reload(4); };
            userRole = new FxSelect();
            userRole.SelectedChanged += delegate { if (loaded.Contains(4)) Reload(4); };

            var bar = FxBox.H(8);
            bar.Fill(userSearch, 1.4f).Add(Tool("Найти", "search", false, delegate { Reload(4); }), 0).Fill(userRole, 0.8f);
            bar.Add(Tool("Добавить", "plus", true, delegate { EditUser(null); }), 0);
            bar.Add(Tool("Изменить", "edit", false, delegate { EditUser(Selected<User>(userTable, "пользователя")); }), 0);
            bar.Add(Tool("Блокировка", "lock", false, delegate { ToggleBlock(); }), 0);
            bar.Add(Tool("Удалить", "trash", false, delegate { DeleteUser(); }), 0);

            userTable = MakeTable("Код", "ФИО", "Электронная почта", "Телефон", "Роль", "Состояние", "Зарегистрирован");
            userTable.Columns[0].Weight = 0.45f;
            userTable.Columns[1].Weight = 1.9f;
            userTable.Columns[1].Align = StringAlignment.Near;
            userTable.Columns[2].Weight = 1.7f;
            userTable.Columns[3].Weight = 1.2f;
            userTable.RowActivated += r => EditUser(r.Tag as User);
            userCount = CountLabel();
            return Section(bar, "Таблица «Пользователи»", "users", userTable, userCount);
        }

        private void LoadUsers()
        {
            if (userRole.Box.Items.Count == 0)
                userRole.SetItems(Ui.Items(lookups.Roles()), true, "Все роли");
            List<User> list = users.Search(userSearch.Value, userRole.SelectedId);
            userTable.Clear();
            userTable.EmptyText = "пользователи не найдены";
            foreach (User u in list)
            {
                FxRow row = userTable.AddRow(u.Id, u.FullName, u.Email, u.Phone ?? "—", u.RoleName, u.IsBlocked ? "заблокирован" : "активен", Ui.DateText(u.CreatedAt));
                row.Tag = u;
                userTable.SetCellTone(row, 5, u.IsBlocked ? RowTone.Bad : RowTone.Good);
            }
            userCount.Text = "Пользователей: " + list.Count + ". Пароли в базе хранятся только в виде хеша PBKDF2-SHA256 и в программе не показываются.";
        }

        private void EditUser(User u)
        {
            List<LookupItem> roles = null;
            if (!Run("чтение ролей", delegate { roles = lookups.Roles(); })) return;
            using (var form = new UserEditForm(users, user, u, roles))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;
            }
            Reload(4);
            Toast(u == null ? "Пользователь добавлен" : "Пользователь изменён", "Изменения сохранены в таблице Пользователи.", Tone.Good);
        }

        private void ToggleBlock()
        {
            var u = Selected<User>(userTable, "пользователя");
            if (u == null) return;
            bool block = !u.IsBlocked;
            if (!FxDialog.Confirm(this, block ? "Блокировка" : "Разблокировка",
                    (block ? "Заблокировать" : "Разблокировать") + " учётную запись «" + u.FullName + "» (" + u.Email + ")?" +
                    (block ? "\nПользователь не сможет войти в программу." : ""), block ? "Заблокировать" : "Разблокировать", block))
                return;
            if (Run("блокировка пользователя", delegate { users.SetBlocked(user, u.Id, block); }))
            {
                Reload(4);
                Toast(block ? "Пользователь заблокирован" : "Пользователь разблокирован", u.FullName, Tone.Good);
            }
        }

        private void DeleteUser()
        {
            var u = Selected<User>(userTable, "пользователя");
            if (u == null) return;
            string blocker = null;
            if (!Run("проверка связей пользователя", delegate { blocker = users.DeleteBlocker(user, u.Id); })) return;
            if (blocker != null)
            {
                FxDialog.Info(this, "Удалить пользователя нельзя", blocker + (u.Id != user.Id ? "\n\nВместо удаления заблокируйте учётную запись кнопкой «Блокировка»." : ""), Tone.Bad);
                return;
            }
            if (!FxDialog.Confirm(this, "Удаление пользователя", "Удалить учётную запись «" + u.FullName + "» (" + u.Email + ")?\nДействие нельзя отменить.", "Удалить", true))
                return;
            if (Run("удаление пользователя", delegate { users.Delete(user, u.Id); }))
            {
                Reload(4);
                Toast("Пользователь удалён", u.FullName, Tone.Good);
            }
        }
    }
}
