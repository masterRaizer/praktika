using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Den5_7.Integration;
using Den5_7.Logic;
using FxKit;

namespace Den5_7.Pages
{
    /// <summary>
    /// День 7. Интеграция модулей «Клиенты» и «Заявки» через REST API: запуск модулей отдельными процессами,
    /// клиенты и карточка с заявками, создание заявки, сквозные тесты и контракт API.
    /// </summary>
    public sealed class Day7Page : DayPage
    {
        public readonly ModuleHost Clients;
        public readonly ModuleHost Requests;

        private FxLabel lblClients, lblRequests;
        private FxText txtLog;
        private FxTable tblClients, tblCardRequests, tblPick, tblIntegration;
        private FxLabel lblCard, lblCreateResult;
        private FxTextBox txtTopic, txtDescription;
        private FxNumber numClientId;
        private FxStat statTotal, statPassed, statFailed;
        private FxButton btnRunIntegration;
        private readonly List<string> log = new List<string>();
        private List<Dictionary<string, object>> clientList = new List<Dictionary<string, object>>();
        private readonly System.Windows.Forms.Timer statusTimer = new System.Windows.Forms.Timer { Interval = 2000 };

        public Day7Page() : base("Модули", "Клиенты", "Новая заявка", "Тесты интеграции", "Контракт API")
        {
            int clientsPort = ModuleHost.ConfigPort("ClientsPort", 5101);
            int requestsPort = ModuleHost.ConfigPort("RequestsPort", 5102);
            Clients = new ModuleHost("clients", "Клиенты", clientsPort, "http://127.0.0.1:" + requestsPort);
            Requests = new ModuleHost("requests", "Заявки", requestsPort, "http://127.0.0.1:" + clientsPort);
            AddUserLabel();
            AddTab(BuildModules());
            AddTab(BuildClients());
            AddTab(BuildCreate());
            AddTab(BuildIntegration());
            AddTab(BuildContract());
            statusTimer.Tick += delegate { RefreshStatus(); };
            statusTimer.Start();
        }

        protected override void OnTabShown(int index)
        {
            if (index == 1 || index == 2) LoadClients();
            if (index == 1 && clientList.Count > 0)
            {
                // чтобы карточка не была пустой — сразу показываем первого клиента
                tblClients.SelectedIndex = 0;
                tblClients.Invalidate();
                ShowCard();
            }
        }

        public void StopAll()
        {
            statusTimer.Stop();
            Clients.Stop();
            Requests.Stop();
        }

        private void Log(string text)
        {
            log.Insert(0, DateTime.Now.ToString("HH:mm:ss") + "  " + text);
            if (log.Count > 40) log.RemoveAt(log.Count - 1);
            txtLog.Text = string.Join("\n", log);
        }

        // ---------- модули ----------

        private FxCard ModuleCard(ModuleHost m, string description, out FxLabel status)
        {
            status = new FxLabel("остановлен", Fonts.Semi(10.5f), Tone.Bad) { Icon = "plug", Height = 26 };
            var start = Ui.Button("Запустить", "play", true);
            var stop = Ui.Button("Остановить", "close");
            var check = Ui.Button("Проверить", "refresh");
            start.Click += delegate { StartModule(m); };
            stop.Click += delegate { m.Stop(); Log("Модуль «" + m.Title + "» остановлен"); RefreshStatus(); };
            check.Click += delegate
            {
                ApiResult r = m.Health();
                Log("GET " + m.Url + "/api/health → " + (r.Status == 0 ? "нет ответа" : r.Status + " " + r.Body));
            };
            FxBox buttons = Ui.Row(32, 8);
            buttons.Add(start, start.Width).Add(stop, stop.Width).Add(check, check.Width).Fill(new FxPanel());
            FxBox body = FxBox.V(8);
            body.Add(status, 26).Add(Ui.Para(description), 64).Add(new FxLabel(m.Url, Fonts.Mono(9.5f), Tone.Accent2) { Height = 22 }, 22).Add(buttons, 32);
            return Ui.Card("Модуль «" + m.Title + "» — порт " + m.Port, m.Name == "clients" ? "users" : "list", body);
        }

        private Control BuildModules()
        {
            FxCard c = ModuleCard(Clients, "Справочник клиентов: id, ФИО, телефон, email. Карточку клиента собирает вместе с заявками, запрашивая модуль «Заявки».", out lblClients);
            FxCard r = ModuleCard(Requests, "Заявки: id, тема, описание, клиент. Перед созданием заявки проверяет клиента запросом к модулю «Клиенты».", out lblRequests);
            var both = Ui.Button("Запустить оба модуля", "play", true, 30);
            both.Click += delegate { StartModule(Clients); StartModule(Requests); };
            var stopBoth = Ui.Button("Остановить оба", "close");
            stopBoth.Click += delegate { Clients.Stop(); Requests.Stop(); Log("Оба модуля остановлены"); RefreshStatus(); };
            txtLog = new FxText { FontSize = 9f, KeyHighlight = false };
            txtLog.Text = "Модули — независимые процессы (Den5_7.exe --module=…), обмен только через HTTP/REST в формате JSON.";
            FxBox top = FxBox.H(12);
            top.Fill(c).Fill(r);
            FxBox buttons = Ui.Row(34);
            buttons.Add(both, both.Width).Add(stopBoth, stopBoth.Width).Fill(Ui.Para("Модули слушают только локальный адрес 127.0.0.1 — права администратора и настройка брандмауэра не нужны.", Tone.Mute));
            FxBox page = FxBox.V(12);
            page.Add(top, 210).Add(buttons, 34).Fill(Ui.Card("Журнал запросов", "terminal", txtLog));
            return page;
        }

        private void StartModule(ModuleHost m)
        {
            try
            {
                m.Start();
                Log("Модуль «" + m.Title + "» запущен: PID " + m.ProcessId + ", " + m.Url);
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception || ex is InvalidOperationException)
            {
                Ui.Error(FindForm(), "Модуль «" + m.Title + "» не запустился: " + ex.Message, "Ошибка запуска модуля");
            }
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (lblClients == null) return;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ApiResult a = Clients.Health(), b = Requests.Health();
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    SetStatus(lblClients, Clients, a);
                    SetStatus(lblRequests, Requests, b);
                });
            });
        }

        private static void SetStatus(FxLabel label, ModuleHost m, ApiResult health)
        {
            if (health.Status == 200)
            {
                label.Text = "работает" + (m.IsRunning ? ", PID " + m.ProcessId : " (запущен отдельно)");
                label.Tone = Tone.Good;
            }
            else
            {
                label.Text = m.IsRunning ? "запускается…" : "остановлен";
                label.Tone = m.IsRunning ? Tone.Warn : Tone.Bad;
            }
        }

        // ---------- клиенты и карточка ----------

        private Control BuildClients()
        {
            tblClients = new FxTable { RowHeight = 34 };
            tblClients.SetColumns("id", "ФИО", "Телефон", "Email");
            tblClients.Columns[0].Weight = 0.4f;
            tblClients.Columns[1].Weight = 1.8f;
            tblClients.Columns[1].Align = StringAlignment.Near;
            tblClients.Columns[2].Weight = 1.2f;
            tblClients.Columns[3].Weight = 1.6f;
            tblClients.Columns[3].Align = StringAlignment.Near;
            tblClients.RowClicked += delegate { ShowCard(); };
            var refresh = Ui.Button("Обновить", "refresh");
            refresh.Click += delegate { LoadClients(); };
            lblCard = new FxLabel("Выберите клиента — карточка соберётся из двух модулей.", Fonts.Semi(10f), Tone.Dim, true);
            tblCardRequests = new FxTable { RowHeight = 34, WrapCells = true };
            tblCardRequests.SetColumns("id", "Тема", "Описание", "Создана");
            tblCardRequests.Columns[0].Weight = 0.35f;
            tblCardRequests.Columns[2].Weight = 2f;
            tblCardRequests.Columns[2].Align = StringAlignment.Near;
            FxBox card = FxBox.V(10);
            card.Add(lblCard, 64).Fill(tblCardRequests);
            FxBox body = FxBox.H(12);
            body.Fill(Ui.Card("Клиенты: GET /api/clients (модуль «Клиенты»)", "users", tblClients, refresh), 1.25f)
                .Fill(Ui.Card("Карточка клиента: GET /api/clients/{id}/card", "list", card), 1f);
            return body;
        }

        private void LoadClients()
        {
            if (tblClients == null) return;
            ApiResult r = ApiClient.Send("GET", Clients.Url + "/api/clients");
            tblClients.Clear();
            tblPick.Clear();
            clientList.Clear();
            if (!r.Ok)
            {
                string text = r.Status == 0 ? "Модуль «Клиенты» не отвечает. Запустите модули на вкладке «Модули»." : "Ошибка " + r.Status;
                tblClients.EmptyText = text;
                tblPick.EmptyText = text;
                return;
            }
            clientList = Json.ReadArray(r.Body).OfType<Dictionary<string, object>>().ToList();
            foreach (Dictionary<string, object> c in clientList)
            {
                tblClients.AddRow(Json.Int(c, "id"), Json.Str(c, "fullName"), Json.Str(c, "phone"), Json.Str(c, "email"));
                tblPick.AddRow(Json.Int(c, "id"), Json.Str(c, "fullName"));
            }
        }

        private void ShowCard()
        {
            int i = tblClients.SelectedIndex;
            if (i < 0 || i >= clientList.Count) return;
            int id = Json.Int(clientList[i], "id") ?? 0;
            ApiResult r = ApiClient.Send("GET", Clients.Url + "/api/clients/" + id + "/card");
            Log("GET /api/clients/" + id + "/card → " + (r.Status == 0 ? "нет ответа" : r.Status.ToString()));
            tblCardRequests.Clear();
            Dictionary<string, object> body = Json.ReadObject(r.Body);
            if (!r.Ok)
            {
                lblCard.Text = r.Status == 0 ? "Модуль «Клиенты» не отвечает." : "Ошибка " + r.Status + ": " + Json.Str(body, "error");
                lblCard.Tone = Tone.Bad;
                return;
            }
            var client = body["client"] as Dictionary<string, object>;
            var items = body["requests"] as object[] ?? new object[0];
            lblCard.Text = Json.Str(client, "fullName") + "\n" + Json.Str(client, "phone") + " · " + Json.Str(client, "email") + " · заявок: " + items.Length;
            lblCard.Tone = Tone.Text;
            foreach (Dictionary<string, object> q in items.OfType<Dictionary<string, object>>())
            {
                string created = (Json.Str(q, "createdAt") ?? "").Replace("T", " ");
                tblCardRequests.AddRow(Json.Int(q, "id"), Json.Str(q, "topic"), Json.Str(q, "description"), created.Length >= 16 ? created.Substring(0, 16) : created);
            }
        }

        // ---------- новая заявка ----------

        private Control BuildCreate()
        {
            tblPick = new FxTable { RowHeight = 34 };
            tblPick.SetColumns("id", "Клиент (из модуля «Клиенты»)");
            tblPick.Columns[0].Weight = 0.3f;
            tblPick.Columns[1].Align = StringAlignment.Near;
            tblPick.RowClicked += delegate(FxRow row) { numClientId.Value = int.Parse(row.Cells[0]); };
            numClientId = new FxNumber(1, 9999, 1, 0, 1);
            txtTopic = new FxTextBox { Placeholder = "тема заявки, обязательно" };
            txtDescription = new FxTextBox { Placeholder = "описание обращения" };
            txtDescription.Box.Multiline = true;
            var send = Ui.Button("Создать заявку", "upload", true, 20);
            send.Click += delegate { CreateRequest(); };
            lblCreateResult = new FxLabel("", Fonts.Semi(9.5f), Tone.Dim, true);
            FxBox form = FxBox.V(10);
            form.Add(Ui.Field("Код клиента (выберите в списке слева или введите вручную, например 999)", numClientId));
            form.Add(Ui.Field("Тема *", txtTopic));
            form.Add(Ui.Field("Описание", txtDescription, 90));
            FxBox buttons = Ui.Row(34);
            buttons.Add(send, send.Width).Fill(new FxPanel());
            form.Add(buttons, 34);
            form.Fill(lblCreateResult);
            FxBox body = FxBox.H(12);
            body.Fill(Ui.Card("Выбор клиента из справочника", "users", tblPick), 0.8f)
                .Fill(Ui.Card("POST /api/requests (модуль «Заявки»)", "upload", form), 1.2f);
            return body;
        }

        private void CreateRequest()
        {
            string topic = txtTopic.Value.Trim();
            if (topic.Length == 0)
            {
                txtTopic.Shake();
                lblCreateResult.Text = "Введите тему заявки.";
                lblCreateResult.Tone = Tone.Bad;
                return;
            }
            string json = Json.Write(new Dictionary<string, object>
            {
                { "topic", topic }, { "description", txtDescription.Value.Trim() }, { "clientId", (int)numClientId.Value }
            });
            ApiResult r = ApiClient.Send("POST", Requests.Url + "/api/requests", json);
            Log("POST /api/requests " + json + " → " + (r.Status == 0 ? "нет ответа" : r.Status.ToString()));
            Dictionary<string, object> body = Json.ReadObject(r.Body);
            if (r.Status == 201)
            {
                lblCreateResult.Text = "201 Created: заявка №" + Json.Int(body, "id") + " для клиента «" + Json.Str(body, "clientName") + "».\nОтвет: " + r.Body;
                lblCreateResult.Tone = Tone.Good;
                txtTopic.Value = "";
                txtDescription.Value = "";
                Win.Toast("Заявка создана", "№" + Json.Int(body, "id") + " — " + Json.Str(body, "clientName"), Tone.Good);
            }
            else
            {
                lblCreateResult.Text = r.Status == 0
                    ? "Модуль «Заявки» не отвечает. Запустите модули на вкладке «Модули»."
                    : r.Status + ": " + (Json.Str(body, "error") ?? r.Body);
                lblCreateResult.Tone = Tone.Bad;
            }
        }

        // ---------- тесты интеграции ----------

        private Control BuildIntegration()
        {
            statTotal = new FxStat("Сценариев", 0) { Tone = Tone.Accent2 };
            statPassed = new FxStat("Пройдено", 2) { Tone = Tone.Good };
            statFailed = new FxStat("Не пройдено", 4) { Tone = Tone.Bad };
            statTotal.Set(10, 0);
            statTotal.Sub = "сквозные и ошибки";
            statPassed.SetText("—");
            statFailed.SetText("—");
            tblIntegration = new FxTable { RowHeight = 34, WrapCells = true, FontSize = 8.5f };
            tblIntegration.SetColumns("№", "Раздел", "Сценарий", "Ожидается", "Получено");
            float[] w = { 0.4f, 0.8f, 2f, 1.7f, 2.2f };
            for (int i = 0; i < w.Length; i++) tblIntegration.Columns[i].Weight = w[i];
            for (int i = 2; i < 5; i++) tblIntegration.Columns[i].Align = StringAlignment.Near;
            FillIntegration(IntegrationTests.Create(Clients, Requests));
            btnRunIntegration = Ui.Button("Запустить модули и тесты", "play", true, 20);
            btnRunIntegration.Click += delegate { RunIntegration(); };
            FxBox page = FxBox.V(12);
            page.Add(Ui.Tiles(statTotal, statPassed, statFailed), 78)
                .Fill(Ui.Card("Сквозные сценарии: создание заявки, несуществующий клиент, карточка, неверный JSON, отказ модуля", "check", tblIntegration, btnRunIntegration));
            return page;
        }

        private void FillIntegration(List<TestCase> list)
        {
            tblIntegration.Clear();
            foreach (TestCase t in list)
            {
                FxRow row = tblIntegration.AddRow(t.Id, t.Area, t.Title, t.Expected, t.Actual ?? "не выполнялся");
                if (t.Passed == true) row.Tone = RowTone.Good;
                if (t.Passed == false) row.Tone = RowTone.Bad;
            }
        }

        /// <summary>Запускает модули (если не запущены) и выполняет сценарии в фоне.</summary>
        public void RunIntegration()
        {
            btnRunIntegration.Enabled = false;
            StartModule(Clients);
            StartModule(Requests);
            List<TestCase> list = IntegrationTests.Create(Clients, Requests);
            FillIntegration(list);
            var worker = new Thread(() =>
            {
                for (int i = 0; i < 40 && (Clients.Health().Status != 200 || Requests.Health().Status != 200); i++) Thread.Sleep(150);
                foreach (TestCase t in list) TestCatalog.Execute(t);
                BeginInvoke((Action)delegate
                {
                    FillIntegration(list);
                    int passed = list.Count(t => t.Passed == true);
                    statPassed.Set(passed, 0);
                    statPassed.Sub = "из " + list.Count;
                    statFailed.Set(list.Count - passed, 0);
                    statFailed.Sub = passed == list.Count ? "ошибок нет" : "смотрите строки, выделенные красным";
                    btnRunIntegration.Enabled = true;
                    RefreshStatus();
                    Win.Toast("Тесты интеграции", "Пройдено " + passed + " из " + list.Count, passed == list.Count ? Tone.Good : Tone.Bad);
                });
            }) { IsBackground = true };
            worker.Start();
        }

        // ---------- контракт ----------

        private Control BuildContract()
        {
            var t = new FxTable { RowHeight = 38, WrapCells = true, FontSize = 8.5f };
            t.SetColumns("Модуль", "Метод и путь", "Тело запроса", "Ответы");
            t.Columns[0].Weight = 0.6f;
            t.Columns[1].Weight = 1.4f;
            t.Columns[1].Align = StringAlignment.Near;
            t.Columns[2].Weight = 1.4f;
            t.Columns[2].Align = StringAlignment.Near;
            t.Columns[3].Weight = 2.2f;
            t.Columns[3].Align = StringAlignment.Near;
            t.AddRow("Клиенты", "GET /api/health", "—", "200 {module, status}");
            t.AddRow("Клиенты", "GET /api/clients", "—", "200 [{id, fullName, phone, email}]");
            t.AddRow("Клиенты", "GET /api/clients/{id}", "—", "200 клиент; 400 неверный id; 404 нет клиента");
            t.AddRow("Клиенты", "GET /api/clients/{id}/card", "—", "200 {client, requests[], requestsCount}; 404; 503 модуль «Заявки» недоступен");
            t.AddRow("Клиенты", "POST /api/clients", "{fullName, phone, email}", "201 клиент; 400 неверные данные");
            t.AddRow("Заявки", "GET /api/health", "—", "200 {module, status}");
            t.AddRow("Заявки", "GET /api/requests?clientId={id}", "—", "200 [{id, topic, description, clientId, clientName, createdAt}]; 400 неверный clientId");
            t.AddRow("Заявки", "GET /api/requests/{id}", "—", "200 заявка; 400; 404 нет заявки");
            t.AddRow("Заявки", "POST /api/requests", "{topic, description, clientId}", "201 заявка; 400 неверный JSON или поля; 422 клиента нет; 503 модуль «Клиенты» недоступен");
            t.AddRow("Оба", "любой другой адрес или метод", "—", "404 / 405; 500 — внутренняя ошибка, модуль продолжает работать");
            return Ui.Card("Контракт API (формат JSON, ошибки — {error, status}); полное описание — docs/openapi.yaml", "data", t);
        }
    }
}
