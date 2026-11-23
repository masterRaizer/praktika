using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace Den5_7.Integration
{
    /// <summary>
    /// Модуль «Клиенты»: справочник заказчиков (id, ФИО, телефон, email).
    /// Карточка клиента собирается вместе со списком его заявок из модуля «Заявки».
    /// </summary>
    public sealed class ClientsModule
    {
        private readonly object sync = new object();
        private readonly List<Dictionary<string, object>> clients = new List<Dictionary<string, object>>();
        private readonly string requestsUrl;
        private int nextId = 1;

        public ClientsModule(string requestsUrl)
        {
            this.requestsUrl = requestsUrl.TrimEnd('/');
            Add("Иванов Иван Иванович", "+7 900 111-22-33", "ivanov@example.com");
            Add("Петрова Анна Сергеевна", "+7 900 222-33-44", "petrova@example.com");
            Add("ООО «Ромашка»", "+7 4812 55-66-77", "office@romashka.example");
            Add("Сидоров Павел Олегович", "+7 900 333-44-55", "sidorov@example.com");
            Add("Козлова Екатерина Дмитриевна", "+7 900 444-55-66", "kozlova@example.com");
        }

        private Dictionary<string, object> Add(string fullName, string phone, string email)
        {
            var c = new Dictionary<string, object> { { "id", nextId++ }, { "fullName", fullName }, { "phone", phone }, { "email", email } };
            clients.Add(c);
            return c;
        }

        public HttpResponseData Handle(HttpRequestData r)
        {
            if (r.Path == "/api/health" && r.Method == "GET")
            {
                return HttpResponseData.Of(200, new Dictionary<string, object> { { "module", "clients" }, { "status", "ok" } });
            }
            if (r.Path == "/api/clients")
            {
                if (r.Method == "GET")
                {
                    lock (sync) return HttpResponseData.Of(200, clients.ToArray());
                }
                if (r.Method == "POST") return Create(r.Body);
                return HttpResponseData.Error(405, "Метод не поддерживается.");
            }
            Match m = Regex.Match(r.Path, @"^/api/clients/([^/]+)(/card)?$");
            if (!m.Success) return HttpResponseData.Error(404, "Адрес не найден.");
            if (r.Method != "GET") return HttpResponseData.Error(405, "Метод не поддерживается.");
            int id;
            if (!int.TryParse(m.Groups[1].Value, out id) || id <= 0)
            {
                return HttpResponseData.Error(400, "Код клиента должен быть положительным целым числом.");
            }
            Dictionary<string, object> client;
            lock (sync) client = clients.FirstOrDefault(c => (int)c["id"] == id);
            if (client == null) return HttpResponseData.Error(404, "Клиент с id = " + id + " не найден.");
            if (!m.Groups[2].Success) return HttpResponseData.Of(200, client);

            // карточка: данные клиента + заявки из модуля «Заявки»
            ApiResult requests = ApiClient.Send("GET", requestsUrl + "/api/requests?clientId=" + id);
            if (requests.Status == 0)
            {
                return HttpResponseData.Error(503, "Модуль «Заявки» недоступен: карточку клиента собрать нельзя. Повторите позже.");
            }
            if (!requests.Ok)
            {
                return HttpResponseData.Error(502, "Модуль «Заявки» вернул ошибку " + requests.Status + ".");
            }
            object[] list = Json.ReadArray(requests.Body);
            return HttpResponseData.Of(200, new Dictionary<string, object>
            {
                { "client", client }, { "requests", list }, { "requestsCount", list.Length }
            });
        }

        private HttpResponseData Create(string body)
        {
            Dictionary<string, object> data = Json.ReadObject(body);
            if (data == null) return HttpResponseData.Error(400, "Неверный формат данных: ожидается объект JSON.");
            string fullName = (Json.Str(data, "fullName") ?? "").Trim();
            string phone = (Json.Str(data, "phone") ?? "").Trim();
            string email = (Json.Str(data, "email") ?? "").Trim();
            if (fullName.Length == 0 || fullName.Length > 150) return HttpResponseData.Error(400, "Поле fullName обязательно, до 150 символов.");
            if (email.Length > 0 && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return HttpResponseData.Error(400, "Поле email заполнено неверно.");
            lock (sync) return HttpResponseData.Of(201, Add(fullName, phone, email));
        }
    }

    /// <summary>
    /// Модуль «Заявки»: заявки (id, тема, описание, клиент). Перед созданием заявки проверяет,
    /// что клиент существует, запросом к модулю «Клиенты».
    /// </summary>
    public sealed class RequestsModule
    {
        private readonly object sync = new object();
        private readonly List<Dictionary<string, object>> requests = new List<Dictionary<string, object>>();
        private readonly string clientsUrl;
        private int nextId = 1;

        public RequestsModule(string clientsUrl)
        {
            this.clientsUrl = clientsUrl.TrimEnd('/');
            Seed("Возврат билета", "Сеанс 21.11 в 19:00 перенесли, прошу вернуть деньги за два билета.", 1, "Иванов Иван Иванович");
            Seed("Забытая вещь", "Оставил зонт в зале «Большой» после вечернего сеанса.", 1, "Иванов Иван Иванович");
            Seed("День рождения", "Забронировать зал «Малый» на 12 человек в субботу.", 2, "Петрова Анна Сергеевна");
            Seed("Корпоративный показ", "Показ фильма для сотрудников в зале IMAX, 120 мест.", 3, "ООО «Ромашка»");
            Seed("Счёт для оплаты", "Выставить счёт на организацию за корпоративный показ.", 3, "ООО «Ромашка»");
            Seed("Не пришёл билет", "Оплатил билет на сайте, но письмо с билетом не пришло.", 4, "Сидоров Павел Олегович");
        }

        private Dictionary<string, object> Seed(string topic, string description, int clientId, string clientName)
        {
            var r = new Dictionary<string, object>
            {
                { "id", nextId++ }, { "topic", topic }, { "description", description }, { "clientId", clientId },
                { "clientName", clientName }, { "createdAt", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") }
            };
            requests.Add(r);
            return r;
        }

        public HttpResponseData Handle(HttpRequestData r)
        {
            if (r.Path == "/api/health" && r.Method == "GET")
            {
                return HttpResponseData.Of(200, new Dictionary<string, object> { { "module", "requests" }, { "status", "ok" } });
            }
            if (r.Path == "/api/requests")
            {
                if (r.Method == "GET") return List(r);
                if (r.Method == "POST") return Create(r.Body);
                return HttpResponseData.Error(405, "Метод не поддерживается.");
            }
            Match m = Regex.Match(r.Path, @"^/api/requests/([^/]+)$");
            if (!m.Success) return HttpResponseData.Error(404, "Адрес не найден.");
            if (r.Method != "GET") return HttpResponseData.Error(405, "Метод не поддерживается.");
            int id;
            if (!int.TryParse(m.Groups[1].Value, out id) || id <= 0)
            {
                return HttpResponseData.Error(400, "Код заявки должен быть положительным целым числом.");
            }
            Dictionary<string, object> found;
            lock (sync) found = requests.FirstOrDefault(x => (int)x["id"] == id);
            return found == null ? HttpResponseData.Error(404, "Заявка с id = " + id + " не найдена.") : HttpResponseData.Of(200, found);
        }

        private HttpResponseData List(HttpRequestData r)
        {
            string raw;
            if (r.Query.TryGetValue("clientId", out raw))
            {
                int clientId;
                if (!int.TryParse(raw, out clientId) || clientId <= 0)
                {
                    return HttpResponseData.Error(400, "Параметр clientId должен быть положительным целым числом.");
                }
                lock (sync) return HttpResponseData.Of(200, requests.Where(x => (int)x["clientId"] == clientId).ToArray());
            }
            lock (sync) return HttpResponseData.Of(200, requests.ToArray());
        }

        private HttpResponseData Create(string body)
        {
            Dictionary<string, object> data = Json.ReadObject(body);
            if (data == null) return HttpResponseData.Error(400, "Неверный формат данных: ожидается объект JSON с полями topic, description, clientId.");
            string topic = (Json.Str(data, "topic") ?? "").Trim();
            string description = (Json.Str(data, "description") ?? "").Trim();
            int? clientId = Json.Int(data, "clientId");
            if (topic.Length == 0) return HttpResponseData.Error(400, "Поле topic (тема) обязательно.");
            if (topic.Length > 200) return HttpResponseData.Error(400, "Тема длиннее 200 символов.");
            if (description.Length > 2000) return HttpResponseData.Error(400, "Описание длиннее 2000 символов.");
            if (!clientId.HasValue || clientId.Value <= 0) return HttpResponseData.Error(400, "Поле clientId должно быть положительным целым числом.");

            // проверка существования клиента в модуле «Клиенты»
            ApiResult client = ApiClient.Send("GET", clientsUrl + "/api/clients/" + clientId.Value);
            if (client.Status == 0)
            {
                return HttpResponseData.Error(503, "Модуль «Клиенты» недоступен: заявка не создана. Повторите позже.");
            }
            if (client.Status == 404)
            {
                return HttpResponseData.Error(422, "Клиент с id = " + clientId.Value + " не найден: заявка не создана.");
            }
            if (!client.Ok)
            {
                return HttpResponseData.Error(502, "Модуль «Клиенты» вернул ошибку " + client.Status + ".");
            }
            string clientName = Json.Str(Json.ReadObject(client.Body), "fullName") ?? "";
            lock (sync) return HttpResponseData.Of(201, Seed(topic, description, clientId.Value, clientName));
        }
    }

    /// <summary>Запуск модуля в отдельном процессе: Den5_7.exe --module=clients|requests --port=N --peer=URL.</summary>
    public static class ModuleServer
    {
        public static int Run(string module, int port, string peerUrl, int parentPid = 0)
        {
            Func<HttpRequestData, HttpResponseData> handler;
            if (module == "clients") handler = new ClientsModule(peerUrl).Handle;
            else if (module == "requests") handler = new RequestsModule(peerUrl).Handle;
            else return 2;
            var server = new MiniHttpServer(port, handler);
            try
            {
                server.Start();
            }
            catch (System.Net.Sockets.SocketException)
            {
                return 3;   // порт занят
            }
            if (parentPid <= 0)
            {
                new ManualResetEvent(false).WaitOne();
                return 0;
            }
            // модуль запущен из окна программы: завершаемся вместе с ним, даже если окно закрыли аварийно,
            // иначе «осиротевший» модуль занимал бы порт до перезагрузки компьютера
            while (ParentAlive(parentPid)) Thread.Sleep(1000);
            server.Stop();
            return 0;
        }

        private static bool ParentAlive(int pid)
        {
            try
            {
                using (System.Diagnostics.Process p = System.Diagnostics.Process.GetProcessById(pid))
                {
                    return !p.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;   // процесса с таким номером уже нет
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
