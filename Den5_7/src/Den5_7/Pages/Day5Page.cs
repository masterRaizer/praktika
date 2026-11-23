using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Den5_7.Data;
using Den5_7.Logic;
using Den5_7.Models;
using FxKit;

namespace Den5_7.Pages
{
    /// <summary>
    /// День 5. Функциональная модель, интерфейс, база данных и приложение:
    /// вход, главное окно, справочник фильмов (поиск, фильтр, сортировка), карточка фильма,
    /// 13 SQL-запросов и словарь данных.
    /// </summary>
    public sealed class Day5Page : DayPage
    {
        private const int TabLogin = 0, TabHome = 1, TabFilms = 2, TabCard = 3, TabQueries = 4, TabDictionary = 5, TabModel = 6;

        private readonly AuthService auth = new AuthService();
        private readonly FilmRepository films = new FilmRepository();
        private readonly GenreRepository genres = new GenreRepository();
        private readonly JournalRepository journal = new JournalRepository();

        private List<Genre> genreList = new List<Genre>();

        // вход
        private FxTextBox txtEmail, txtPassword;
        private FxLabel lblLoginError, lblUser;
        private FxButton btnLogout;

        // главная
        private FxStat statFilms, statSessions, statTickets, statRevenue;
        private FxTable tblAfisha;

        // справочник
        private FxTextBox txtSearch;
        private FxSegmented segGenre, segSort;
        private FxSwitch swActive;
        private FxTable tblFilms;
        private FxLabel lblFound, lblRights;
        private FxButton btnAdd, btnEdit, btnDelete;
        private List<Film> shownFilms = new List<Film>();

        // карточка
        private Film editing;
        private FxLabel lblCardMode, lblCardErrors;
        private FxTextBox txtTitle, txtDescription, txtPoster;
        private FxSegmented segCardGenre, segAge;
        private FxNumber numDuration;
        private FxSwitch swCardActive;
        private bool cardDirty, filling;

        // запросы и словарь
        private FxTable tblQueries, tblResult, tblDictionary;
        private FxTextBox txtQuerySearch, txtFrom, txtTo;
        private FxText txtSql;

        public Day5Page() : base("Вход", "Главная", "Фильмы", "Карточка", "SQL-запросы", "Словарь данных", "Модель")
        {
            lblUser = new FxLabel("Вход не выполнен", Fonts.Semi(9.5f), Tone.Dim) { Align = StringAlignment.Far, Icon = "users" };
            btnLogout = Ui.Button("Выйти", "back");
            btnLogout.Visible = false;
            btnLogout.Click += delegate { Logout(); };
            TopBar.Fill(lblUser).Add(btnLogout, btnLogout.Width);

            AddTab(BuildLogin());
            AddTab(new Guard(BuildHome(), u => u != null, ""));
            AddTab(new Guard(BuildFilms(), u => u != null, ""));
            AddTab(new Guard(BuildCard(), u => Session.CanEditFilms(u), "Добавлять и изменять фильмы может только администратор."));
            AddTab(new Guard(BuildQueries(), u => u != null, ""));
            AddTab(new Guard(BuildDictionary(), u => u != null, ""));
            AddTab(BuildModel());
            Session.Changed += delegate { OnSessionChanged(); };
        }

        // =====================================================================
        //  Вход
        // =====================================================================

        private Control BuildLogin()
        {
            txtEmail = new FxTextBox { Placeholder = "например, admin@kinotochka.ru" };
            txtPassword = new FxTextBox { Placeholder = "пароль" };
            txtPassword.Box.UseSystemPasswordChar = true;
            txtPassword.EnterPressed += delegate { DoLogin(); };
            txtEmail.EnterPressed += delegate { txtPassword.FocusBox(); };
            var btn = Ui.Button("Войти", "key", true, 40);
            btn.Click += delegate { DoLogin(); };
            lblLoginError = new FxLabel("", Fonts.Semi(9f), Tone.Bad, true) { Icon = "warn" };

            FxBox form = FxBox.V(10);
            form.Add(new FxLabel("Вход в систему «Киноточка»", Fonts.Display(13f), Tone.Text) { Height = 30 }, 30);
            form.Add(Ui.Para("Введите e-mail и пароль. Права зависят от роли: администратор меняет справочник и видит журнал, кассир — отчёты, зритель — только просмотр."), 54);
            form.Add(Ui.Field("E-mail", txtEmail));
            form.Add(Ui.Field("Пароль", txtPassword));
            FxBox buttons = Ui.Row(36);
            buttons.Add(btn, btn.Width).Fill(new FxPanel());
            form.Add(buttons, 36);
            form.Add(lblLoginError, 40);
            form.Add(Ui.Para("Тестовые учётные записи (пароль у всех 123456): admin@kinotochka.ru — администратор; cashier1@kinotochka.ru — кассир; ivanov@example.com — зритель; morozov@example.com — заблокирован.", Tone.Mute), 80);
            var card = new FxCard { Padding = new Padding(28, 24, 28, 18) };
            card.With(form);

            FxBox row = FxBox.H(0);
            row.Fill(new FxPanel()).Add(card, 470).Fill(new FxPanel());
            FxBox page = FxBox.V(0);
            page.Fill(new FxPanel(), 0.4f).Add(row, 480).Fill(new FxPanel(), 1f);
            return page;
        }

        public void DoLogin()
        {
            lblLoginError.Text = "";
            try
            {
                User user = auth.Login(txtEmail.Value, txtPassword.Value);
                txtPassword.Value = "";
                Session.Current = user;
                Win.Toast("Вход выполнен", user.FullName + " — " + user.RoleName, Tone.Good);
                ShowTab(TabHome);
            }
            catch (ValidationException ex)
            {
                lblLoginError.Text = ex.Message;
                txtPassword.Shake();
            }
            catch (AppDataException ex)
            {
                lblLoginError.Text = "Нет подключения к базе данных.";
                Ui.Error(FindForm(), ex.Message, "Ошибка подключения к базе данных");
            }
        }

        /// <summary>Вход из параметров запуска (для снимков экрана).</summary>
        public void LoginAs(string email, string password)
        {
            txtEmail.Value = email;
            txtPassword.Value = password;
            DoLogin();
        }

        private void Logout()
        {
            string question = cardDirty
                ? "В карточке фильма есть несохранённые изменения. Выйти без сохранения?"
                : "Выйти из системы?";
            if (!Ui.Confirm(FindForm(), question, "Выход из системы")) return;
            auth.Logout(Session.Current);
            cardDirty = false;
            Session.Current = null;
            ShowTab(TabLogin);
        }

        private void OnSessionChanged()
        {
            User u = Session.Current;
            lblUser.Text = u == null ? "Вход не выполнен" : Ui.ShortName(u.FullName) + " · " + u.RoleName;
            btnLogout.Visible = u != null;
            TopBar.Arrange();
            bool canEdit = Session.CanEditFilms(u);
            btnAdd.Enabled = btnEdit.Enabled = btnDelete.Enabled = canEdit;
            lblRights.Text = u != null && !canEdit ? "Роль «" + u.RoleName + "»: справочник только для просмотра." : "";
            if (u != null)
            {
                LoadGenres();
                LoadHome();
                LoadFilms();
            }
        }

        protected override void OnTabShown(int index)
        {
            if (!Session.IsLoggedIn) return;
            if (index == TabDictionary && tblDictionary.Rows.Count == 0) LoadDictionary();
            if (index == TabQueries && tblResult.Rows.Count == 0) RunQuery(0);
            if (index == TabCard && editing == null && !cardDirty) FillCard(null);
        }

        // =====================================================================
        //  Главная
        // =====================================================================

        private Control BuildHome()
        {
            statFilms = new FxStat("Фильмов в прокате", 0) { Tone = Tone.Accent2 };
            statSessions = new FxStat("Сеансов в расписании", 2) { Tone = Tone.Good };
            statTickets = new FxStat("Продано билетов", 1) { Tone = Tone.Accent };
            statRevenue = new FxStat("Выручка", 4) { Tone = Tone.Warn };
            tblAfisha = new FxTable { RowHeight = 32 };
            FxBox page = FxBox.V(12);
            page.Add(Ui.Tiles(statFilms, statSessions, statTickets, statRevenue), 78);
            page.Fill(Ui.Card("Афиша: все сеансы (представление Афиша)", "film", tblAfisha));
            return page;
        }

        private void LoadHome()
        {
            try
            {
                statFilms.Set(Convert.ToDouble(Queries.Scalar("SELECT COUNT(*) FROM dbo.Фильмы WHERE Активен = 1;")), 0);
                statFilms.Sub = "всего в базе: " + Queries.Scalar("SELECT COUNT(*) FROM dbo.Фильмы;");
                statSessions.Set(Convert.ToDouble(Queries.Scalar("SELECT COUNT(*) FROM dbo.Сеансы;")), 0);
                statSessions.Sub = "в 5 залах";
                statTickets.Set(Convert.ToDouble(Queries.Scalar("SELECT COUNT(*) FROM dbo.Билеты t JOIN dbo.Заказы o ON o.КодЗаказа = t.КодЗаказа WHERE o.КодСтатуса IN (3, 4);")), 0);
                statTickets.Sub = "оплаченные и завершённые заказы";
                statRevenue.Set(Convert.ToDouble(Queries.Scalar("SELECT ISNULL(SUM(Выручка), 0) FROM dbo.ВыручкаПоФильмам;")), 0, " руб.");
                statRevenue.Sub = "с учётом возвратов";
                Ui.Fill(tblAfisha, Queries.Table(Queries.Afisha));
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки данных");
            }
        }

        // =====================================================================
        //  Справочник фильмов: поиск, фильтр, сортировка
        // =====================================================================

        private Control BuildFilms()
        {
            txtSearch = new FxTextBox { Placeholder = "поиск по названию и описанию" };
            txtSearch.ValueChanged += delegate { LoadFilms(); };
            segGenre = new FxSegmented("Все жанры");
            segGenre.SelectedIndexChanged += delegate { LoadFilms(); };
            segSort = new FxSegmented("Название А–Я", "Я–А", "Длиннее", "Короче");
            segSort.SelectedIndexChanged += delegate { LoadFilms(); };
            swActive = new FxSwitch();
            swActive.CheckedChanged += delegate { LoadFilms(); };

            FxBox filters = Ui.Row(36);
            filters.Fill(txtSearch, 1f).Add(segSort, 400);
            FxBox filters2 = Ui.Row(30);
            filters2.Add(segGenre, 760).Add(swActive, 46, true).Add(new FxLabel("только в прокате", Fonts.Medium(9f), Tone.Dim), 130).Fill(new FxPanel());

            tblFilms = new FxTable { RowHeight = 34 };
            tblFilms.SetColumns("Код", "Название", "Жанр", "Минут", "Возраст", "Сеансов", "В прокате");
            tblFilms.Columns[0].Weight = 0.5f;
            tblFilms.Columns[1].Weight = 2.4f;
            tblFilms.Columns[1].Align = StringAlignment.Near;
            tblFilms.RowClicked += delegate { UpdateSelection(); };

            btnAdd = Ui.Button("Добавить", "plus", true);
            btnAdd.Click += delegate { OpenCard(null); };
            btnEdit = Ui.Button("Изменить", "data");
            btnEdit.Click += delegate { EditSelected(); };
            btnDelete = Ui.Button("Удалить", "trash");
            btnDelete.Danger = true;
            btnDelete.Click += delegate { DeleteSelected(); };
            var btnExport = Ui.Button("Экспорт в CSV", "download");
            btnExport.Click += delegate { Ui.ExportCsv(FindForm(), tblFilms, "Фильмы.csv", "Список фильмов", Win); };
            lblFound = new FxLabel("", Fonts.Semi(9f), Tone.Dim);
            lblRights = new FxLabel("", Fonts.Medium(9f), Tone.Warn) { Icon = "info" };
            FxBox actions = Ui.Row(34);
            actions.Add(btnAdd, btnAdd.Width).Add(btnEdit, btnEdit.Width).Add(btnDelete, btnDelete.Width).Add(btnExport, btnExport.Width)
                .Add(lblFound, 140).Fill(lblRights);

            FxBox body = FxBox.V(10);
            body.Add(filters, 36).Add(filters2, 30).Fill(tblFilms).Add(actions, 34);
            return Ui.Card("Справочник фильмов", "film", body);
        }

        private void LoadGenres()
        {
            try
            {
                genreList = genres.All();
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки жанров");
                return;
            }
            var names = new List<string> { "Все жанры" };
            names.AddRange(genreList.Select(g => g.Name));
            segGenre.Items = names.ToArray();
            segGenre.SelectedIndex = 0;
            segCardGenre.Items = genreList.Select(g => g.Name).ToArray();
        }

        private void LoadFilms()
        {
            if (!Session.IsLoggedIn || tblFilms == null) return;
            int? genreId = segGenre.SelectedIndex > 0 && segGenre.SelectedIndex <= genreList.Count ? genreList[segGenre.SelectedIndex - 1].Id : (int?)null;
            var sort = (FilmSort)Math.Max(0, segSort.SelectedIndex);
            int keepId = SelectedFilm() == null ? 0 : SelectedFilm().Id;
            try
            {
                shownFilms = films.Search(txtSearch.Value, genreId, sort, swActive.Checked);
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки фильмов");
                return;
            }
            tblFilms.Clear();
            foreach (Film f in shownFilms)
            {
                FxRow row = tblFilms.AddRow(f.Id, f.Title, f.Genre, f.DurationMin, f.AgeRating, f.SessionsCount, f.IsActive ? "да" : "нет");
                if (!f.IsActive) row.Tone = RowTone.Forecast;
                if (f.Id == keepId) tblFilms.SelectedIndex = tblFilms.Rows.Count - 1;
            }
            lblFound.Text = "Найдено: " + shownFilms.Count;
            UpdateSelection();
        }

        private Film SelectedFilm()
        {
            int i = tblFilms == null ? -1 : tblFilms.SelectedIndex;
            return i >= 0 && i < shownFilms.Count ? shownFilms[i] : null;
        }

        private void UpdateSelection()
        {
            bool canEdit = Session.CanEditFilms(Session.Current);
            bool has = SelectedFilm() != null;
            btnEdit.Enabled = btnDelete.Enabled = canEdit && has;
            btnAdd.Enabled = canEdit;
        }

        private void EditSelected()
        {
            Film f = SelectedFilm();
            if (f == null)
            {
                Ui.Warn(FindForm(), "Выберите фильм в списке щелчком по строке.", "Фильм не выбран");
                return;
            }
            OpenCard(f);
        }

        private void DeleteSelected()
        {
            Film f = SelectedFilm();
            if (f == null)
            {
                Ui.Warn(FindForm(), "Выберите фильм в списке щелчком по строке.", "Фильм не выбран");
                return;
            }
            if (!Ui.Confirm(FindForm(), "Удалить фильм «" + f.Title + "»?\n\nДействие нельзя отменить.", "Удаление фильма")) return;
            try
            {
                films.Delete(f.Id);
                journal.Add(Session.Current.Id, "Удаление фильма", "«" + f.Title + "»");
                tblFilms.SelectedIndex = -1;
                LoadFilms();
                LoadHome();
                Win.Toast("Фильм удалён", "«" + f.Title + "»", Tone.Good);
            }
            catch (AppDataException ex)
            {
                Ui.Warn(FindForm(), ex.Message, "Удаление невозможно");
            }
            catch (RecordNotFoundException ex)
            {
                Ui.Warn(FindForm(), ex.Message, "Запись не найдена");
                LoadFilms();
            }
        }

        // =====================================================================
        //  Карточка фильма
        // =====================================================================

        private Control BuildCard()
        {
            lblCardMode = new FxLabel("Новый фильм", Fonts.Display(12f), Tone.Text) { Height = 28 };
            txtTitle = new FxTextBox { Placeholder = "до 150 символов, обязательно" };
            segCardGenre = new FxSegmented("—");
            numDuration = new FxNumber(1, 600, 90, 0, 5);
            segAge = new FxSegmented(FilmValidator.AgeRatings);
            txtDescription = new FxTextBox { Placeholder = "до 1000 символов, необязательно" };
            txtDescription.Box.Multiline = true;
            txtDescription.Box.ScrollBars = ScrollBars.Vertical;
            txtPoster = new FxTextBox { Placeholder = "например, posters/film.jpg (необязательно)" };
            swCardActive = new FxSwitch();
            lblCardErrors = new FxLabel("", Fonts.Semi(9f), Tone.Bad, true) { Icon = "warn" };

            EventHandler dirty = delegate { if (!filling) cardDirty = true; };
            txtTitle.ValueChanged += dirty;
            txtDescription.ValueChanged += dirty;
            txtPoster.ValueChanged += dirty;
            numDuration.ValueChanged += dirty;
            segCardGenre.SelectedIndexChanged += dirty;
            segAge.SelectedIndexChanged += dirty;
            swCardActive.CheckedChanged += dirty;

            var btnSave = Ui.Button("Сохранить", "check", true, 20);
            btnSave.Click += delegate { SaveCard(); };
            var btnCancel = Ui.Button("Отмена", "close");
            btnCancel.Click += delegate { CloseCard(); };

            FxBox left = FxBox.V(10);
            left.Add(Ui.Field("Название *", txtTitle));
            left.Add(Ui.Field("Жанр *", segCardGenre, 32));
            FxBox two = Ui.Row(55, 16);
            two.Add(Ui.Field("Длительность, мин * (1–600)", numDuration), 220).Fill(Ui.Field("Возрастной рейтинг *", segAge, 32));
            left.Add(two, 55);
            left.Add(Ui.Field("Описание", txtDescription, 96));
            left.Add(Ui.Field("Постер", txtPoster));
            FxBox active = Ui.Row(26);
            active.Add(swCardActive, 46, true).Add(new FxLabel("В прокате (показывать в афише)", Fonts.Medium(9.5f), Tone.Dim), 300).Fill(new FxPanel());
            left.Add(active, 26);
            FxBox buttons = Ui.Row(34);
            buttons.Add(btnSave, btnSave.Width).Add(btnCancel, btnCancel.Width).Fill(new FxPanel());
            left.Add(buttons, 34);

            FxBox hint = FxBox.V(8);
            hint.Add(lblCardMode, 28);
            hint.Add(Ui.Para("Поля со звёздочкой обязательны. Те же правила проверяет база данных (ограничения CHECK и NOT NULL), поэтому неверная запись не сохранится даже в обход формы."), 70);
            hint.Add(lblCardErrors, 120);
            hint.Fill(new FxPanel());

            FxBox body = FxBox.H(24);
            body.Fill(left, 1.4f).Fill(hint, 1f);
            return Ui.Card("Карточка фильма: добавление и изменение", "data", body);
        }

        private void OpenCard(Film film)
        {
            if (!Session.CanEditFilms(Session.Current))
            {
                Ui.Warn(FindForm(), "Добавлять и изменять фильмы может только администратор.", "Недостаточно прав");
                return;
            }
            FillCard(film);
            ShowTab(TabCard);
        }

        private void FillCard(Film film)
        {
            filling = true;
            editing = film;
            lblCardMode.Text = film == null ? "Новый фильм" : "Изменение: «" + film.Title + "»";
            txtTitle.Value = film == null ? "" : film.Title;
            int gi = film == null ? 0 : genreList.FindIndex(g => g.Id == film.GenreId);
            segCardGenre.SelectedIndex = Math.Max(0, gi);
            numDuration.Value = film == null ? 90 : film.DurationMin;
            segAge.SelectedIndex = film == null ? 2 : Math.Max(0, Array.IndexOf(FilmValidator.AgeRatings, film.AgeRating));
            txtDescription.Value = film == null ? "" : film.Description ?? "";
            txtPoster.Value = film == null ? "" : film.Poster ?? "";
            swCardActive.SetSilently(film == null || film.IsActive);
            lblCardErrors.Text = "";
            filling = false;
            cardDirty = false;
        }

        private void SaveCard()
        {
            var film = new Film
            {
                Id = editing == null ? 0 : editing.Id,
                Title = txtTitle.Value,
                GenreId = segCardGenre.SelectedIndex >= 0 && segCardGenre.SelectedIndex < genreList.Count ? genreList[segCardGenre.SelectedIndex].Id : 0,
                DurationMin = (int)numDuration.Value,
                AgeRating = segAge.SelectedIndex >= 0 ? FilmValidator.AgeRatings[segAge.SelectedIndex] : null,
                Description = txtDescription.Value,
                Poster = txtPoster.Value,
                IsActive = swCardActive.Checked
            };
            List<string> errors = FilmValidator.Validate(film);
            if (errors.Count > 0)
            {
                lblCardErrors.Text = string.Join("\n", errors);
                if (errors[0].StartsWith("Введите название") || errors[0].StartsWith("Название")) txtTitle.Shake();
                Ui.Warn(FindForm(), "Карточка заполнена с ошибками:\n\n• " + string.Join("\n• ", errors), "Проверьте данные");
                return;
            }
            try
            {
                if (editing == null)
                {
                    film.Id = films.Insert(film);
                    journal.Add(Session.Current.Id, "Добавление фильма", "«" + film.Title.Trim() + "»");
                }
                else
                {
                    films.Update(film);
                    journal.Add(Session.Current.Id, "Изменение фильма", "«" + film.Title.Trim() + "»");
                }
            }
            catch (AppDataException ex)
            {
                lblCardErrors.Text = ex.Message;
                Ui.Error(FindForm(), ex.Message, "Фильм не сохранён");
                return;
            }
            catch (RecordNotFoundException ex)
            {
                Ui.Warn(FindForm(), ex.Message, "Запись не найдена");
                return;
            }
            cardDirty = false;
            editing = null;
            Win.Toast("Сохранено", "«" + film.Title.Trim() + "»", Tone.Good);
            LoadFilms();
            LoadHome();
            ShowTab(TabFilms);
        }

        private void CloseCard()
        {
            if (cardDirty && !Ui.Confirm(FindForm(), "Изменения в карточке не сохранены. Закрыть карточку без сохранения?", "Несохранённые изменения")) return;
            cardDirty = false;
            editing = null;
            ShowTab(TabFilms);
        }

        // =====================================================================
        //  SQL-запросы и словарь данных
        // =====================================================================

        private Control BuildQueries()
        {
            tblQueries = new FxTable { RowHeight = 34, WrapCells = true };
            tblQueries.SetColumns("№", "Запрос", "Что используется");
            tblQueries.Columns[0].Weight = 0.3f;
            tblQueries.Columns[1].Weight = 1.6f;
            tblQueries.Columns[1].Align = StringAlignment.Near;
            for (int i = 0; i < QueryCatalog.Items.Count; i++)
            {
                tblQueries.AddRow(i + 1, QueryCatalog.Items[i].Title, QueryCatalog.Items[i].Kind);
            }
            tblQueries.RowClicked += delegate { RunQuery(tblQueries.SelectedIndex); };

            txtQuerySearch = new FxTextBox { Value = "о" };
            txtFrom = new FxTextBox { Value = "10.11.2026" };
            txtTo = new FxTextBox { Value = "19.11.2026" };
            var run = Ui.Button("Выполнить", "play", true);
            run.Click += delegate { RunQuery(Math.Max(0, tblQueries.SelectedIndex)); };
            FxBox prms = Ui.Row(55, 10);
            prms.Fill(Ui.Field("@Поиск (запрос 11)", txtQuerySearch)).Add(Ui.Field("@С (запрос 12)", txtFrom), 130)
                .Add(Ui.Field("@По (запрос 12)", txtTo), 130);
            FxBox runRow = Ui.Row(34);
            runRow.Add(run, run.Width).Fill(Ui.Para("Значения передаются параметрами SqlParameter, а не склеиваются с текстом SQL.", Tone.Mute));

            tblResult = new FxTable { RowHeight = 30 };
            txtSql = new FxText { FontSize = 9f, KeyHighlight = false };

            FxBox left = FxBox.V(10);
            left.Fill(tblQueries).Add(prms, 55).Add(runRow, 34);
            FxBox right = FxBox.V(12);
            right.Fill(Ui.Card("Результат", "data", tblResult), 1.6f).Fill(Ui.Card("Текст запроса", "terminal", txtSql), 0.8f);
            FxBox body = FxBox.H(12);
            body.Fill(Ui.Card("13 запросов дня 5 (щелчок — выполнить)", "list", left), 0.9f).Fill(right, 1.3f);
            return body;
        }

        private void RunQuery(int index)
        {
            if (index < 0 || index >= QueryCatalog.Items.Count) return;
            tblQueries.SelectedIndex = index;
            QueryCatalog.Item q = QueryCatalog.Items[index];
            bool usesDates = q.Kind.IndexOf("@С", StringComparison.Ordinal) >= 0;   // запрос 12 — период «с … по»
            DateTime from, to;
            if (!DateTime.TryParseExact(txtFrom.Value.Trim(), "dd.MM.yyyy", Ui.Ru, DateTimeStyles.None, out from) ||
                !DateTime.TryParseExact(txtTo.Value.Trim(), "dd.MM.yyyy", Ui.Ru, DateTimeStyles.None, out to))
            {
                if (usesDates)
                {
                    txtFrom.Shake();
                    Ui.Warn(FindForm(), "Введите даты в формате ДД.ММ.ГГГГ, например 10.11.2026.", "Неверный формат даты");
                    return;
                }
                from = to = DateTime.Today;
            }
            if (usesDates && from > to)
            {
                Ui.Warn(FindForm(), "Дата «С» не может быть позже даты «По».", "Неверный период");
                return;
            }
            txtSql.Text = (index + 1) + ". " + q.Title + "\n" + q.Sql.Replace(" FROM ", "\nFROM ").Replace(" WHERE ", "\nWHERE ")
                .Replace(" GROUP BY ", "\nGROUP BY ").Replace(" ORDER BY ", "\nORDER BY ").Replace(" JOIN ", "\nJOIN ").Replace(" HAVING ", "\nHAVING ");
            try
            {
                SqlParameter[] prms = q.Parameters == null ? null : q.Parameters(txtQuerySearch.Value.Trim(), from, to);
                DataTable data = Queries.Table(q.Sql, prms);
                Ui.Fill(tblResult, data);
                if (data.Rows.Count == 0) tblResult.EmptyText = "запрос не вернул строк";
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Запрос не выполнен");
            }
        }

        private Control BuildDictionary()
        {
            tblDictionary = new FxTable { RowHeight = 30, WrapCells = true, FontSize = 8.5f };
            var btnExport = Ui.Button("Экспорт в CSV", "download");
            btnExport.Click += delegate { Ui.ExportCsv(FindForm(), tblDictionary, "Словарь_данных.csv", "Словарь данных", Win); };
            return Ui.Card("Словарь данных: таблицы и поля базы «Киноточка» (из описаний MS_Description)", "list", tblDictionary, btnExport);
        }

        private void LoadDictionary()
        {
            try
            {
                Ui.Fill(tblDictionary, Queries.Table(Queries.DataDictionary));
                float[] weights = { 0.9f, 1.1f, 0.9f, 0.6f, 1.7f, 2.2f };
                for (int i = 0; i < weights.Length && i < tblDictionary.Columns.Count; i++) tblDictionary.Columns[i].Weight = weights[i];
                tblDictionary.Columns[4].Align = StringAlignment.Near;
                tblDictionary.Columns[5].Align = StringAlignment.Near;
                tblDictionary.Touch();
            }
            catch (AppDataException ex)
            {
                Ui.Error(FindForm(), ex.Message, "Ошибка загрузки словаря");
            }
        }

        // =====================================================================
        //  Модель: функциональная декомпозиция и карта экранов
        // =====================================================================

        private Control BuildModel()
        {
            var func = new FxText { FontSize = 9.2f };
            func.Text =
                "ФУНКЦИОНАЛЬНАЯ МОДЕЛЬ (IDEF0, A0)\n" +
                "Главная функция: работа кинотеатра «Киноточка» в приложении. Декомпозиция на 5 подпроцессов; I — вход, C — управление, O — выход, M — механизм.\n" +
                "A1 Войти в систему и определить роль: I — e-mail и пароль; C — ролевая модель, пароли только в виде хеша PBKDF2; O — роль и права доступа (управляют A2 и A4); M — окно входа, таблицы Пользователи и Роли.\n" +
                "A2 Вести справочник фильмов: I — данные фильма из карточки; C — права из A1, ограничения базы NOT NULL, UNIQUE, CHECK, FK; O — каталог фильмов и жанров (управляет A3); M — администратор, таблица Фильмы.\n" +
                "A3 Искать, фильтровать и сортировать: I — строка поиска, жанр, порядок сортировки; C — каталог из A2, ввод передаётся параметрами SqlParameter; O — список фильмов и афиша, отобранный список (вход A4); M — приложение, представление Афиша.\n" +
                "A4 Формировать отчёты и экспорт в CSV: I — заказы, билеты, платежи, отобранный список; C — права из A1 (администратор, кассир); O — отчёты о выручке и заполняемости, файлы CSV, сведения о действиях (вход A5); M — кассир, администратор, представление ВыручкаПоФильмам.\n" +
                "A5 Вести журнал действий: I — сведения о входе, изменениях и экспорте; C — просмотр только администратором; O — журнал действий; M — таблица ЖурналДействий.\n" +
                "Диаграммы — в папке docs/diagrams: den5_idef0_a0 (функции), den5_screen_map (экраны), den5_er (база данных).";
            var screens = new FxText { FontSize = 9.2f };
            screens.Text =
                "КАРТА ЭКРАНОВ И ПЕРЕХОДЫ\n" +
                "Вход: успешный вход → Главная; ошибка → сообщение на том же экране.\n" +
                "Главная: сводка и афиша → Фильмы, SQL-запросы, Словарь данных; кнопка «Выйти» → Вход.\n" +
                "Фильмы (справочник): поиск, фильтр по жанру, сортировка; «Добавить» и «Изменить» → Карточка; «Удалить» → подтверждение.\n" +
                "Карточка: «Сохранить» → проверка → Фильмы; «Отмена» → подтверждение, если есть изменения → Фильмы.\n" +
                "День 6: Отчёты, Журнал, Тест-кейсы, Документация. День 7: Модули, Клиенты, Новая заявка, Тесты интеграции, Контракт API.\n" +
                "Навигация: кнопка «три полоски» сворачивает боковую панель; дни выбираются в панели, разделы дня — вкладками сверху.\n" +
                "Сообщения: ошибка — значок «стоп», предупреждение — «!», информация — «i»; опасные действия (удаление, выход без сохранения) требуют подтверждения.";
            FxBox body = FxBox.H(12);
            body.Fill(Ui.Card("Функциональная модель", "layers", func)).Fill(Ui.Card("Интерфейс", "list", screens));
            return body;
        }
    }
}
