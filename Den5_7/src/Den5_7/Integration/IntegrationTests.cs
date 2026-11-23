using System;
using System.Collections.Generic;
using System.Threading;
using Den5_7.Logic;

namespace Den5_7.Integration
{
    /// <summary>
    /// Сквозные сценарии интеграции модулей «Клиенты» и «Заявки» (день 7).
    /// Сценарии с недоступным модулем останавливают его процесс и затем запускают снова.
    /// </summary>
    public static class IntegrationTests
    {
        private static void Check(bool condition, string failText)
        {
            if (!condition) throw new TestFailed(failText);
        }

        private static string Describe(ApiResult r)
        {
            if (r.Status == 0) return "нет ответа";
            string error = Json.Str(Json.ReadObject(r.Body), "error");
            return r.Status + (error != null ? " «" + error + "»" : "");
        }

        private static bool WaitHealth(ModuleHost module, bool up)
        {
            for (int i = 0; i < 40; i++)
            {
                if ((module.Health().Status == 200) == up) return true;
                Thread.Sleep(150);
            }
            return false;
        }

        public static List<TestCase> Create(ModuleHost clients, ModuleHost requests)
        {
            string c = clients.Url, q = requests.Url;
            int createdId = 0;
            var list = new List<TestCase>();
            list.Add(new TestCase
            {
                Id = "И01", Area = "Доступность", Kind = "позитивный", Title = "Оба модуля отвечают на /api/health",
                Expected = "200 и 200",
                Run = () =>
                {
                    ApiResult a = clients.Health(), b = requests.Health();
                    Check(a.Status == 200 && b.Status == 200, "Клиенты: " + Describe(a) + ", Заявки: " + Describe(b));
                    return "Клиенты — 200, Заявки — 200";
                }
            });
            list.Add(new TestCase
            {
                Id = "И02", Area = "Клиенты", Kind = "позитивный", Title = "GET /api/clients — список клиентов",
                Expected = "200, не меньше 5 клиентов",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("GET", c + "/api/clients");
                    object[] list2 = Json.ReadArray(r.Body);
                    Check(r.Status == 200 && list2.Length >= 5, Describe(r));
                    return "200, клиентов: " + list2.Length;
                }
            });
            list.Add(new TestCase
            {
                Id = "И03", Area = "Интеграция", Kind = "позитивный", Title = "Создание заявки для существующего клиента (id 2)",
                Expected = "201, заявка с именем клиента из модуля «Клиенты»",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("POST", q + "/api/requests",
                        Json.Write(new Dictionary<string, object> { { "topic", "Проверка интеграции" }, { "description", "Сквозной сценарий" }, { "clientId", 2 } }));
                    Dictionary<string, object> body = Json.ReadObject(r.Body);
                    Check(r.Status == 201 && Json.Str(body, "clientName") == "Петрова Анна Сергеевна", Describe(r));
                    createdId = Json.Int(body, "id") ?? 0;
                    return "201, заявка №" + createdId + ", клиент «" + Json.Str(body, "clientName") + "»";
                }
            });
            list.Add(new TestCase
            {
                Id = "И04", Area = "Интеграция", Kind = "негативный", Title = "Создание заявки для несуществующего клиента (id 999)",
                Expected = "422 «Клиент с id = 999 не найден»",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("POST", q + "/api/requests",
                        Json.Write(new Dictionary<string, object> { { "topic", "Нет клиента" }, { "description", "" }, { "clientId", 999 } }));
                    Check(r.Status == 422, Describe(r));
                    return Describe(r);
                }
            });
            list.Add(new TestCase
            {
                Id = "И05", Area = "Интеграция", Kind = "позитивный", Title = "Карточка клиента id 2 со списком его заявок",
                Expected = "200, в карточке есть созданная заявка",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("GET", c + "/api/clients/2/card");
                    Dictionary<string, object> body = Json.ReadObject(r.Body);
                    Check(r.Status == 200, Describe(r));
                    var items = body["requests"] as object[] ?? new object[0];
                    bool hasCreated = false;
                    foreach (object o in items)
                    {
                        if (Json.Int(o as Dictionary<string, object>, "id") == createdId) hasCreated = true;
                    }
                    Check(hasCreated, "созданной заявки №" + createdId + " нет в карточке");
                    return "200, заявок в карточке: " + items.Length + ", среди них №" + createdId;
                }
            });
            list.Add(new TestCase
            {
                Id = "И06", Area = "Неверный формат", Kind = "негативный", Title = "POST /api/requests с телом «это не JSON»",
                Expected = "400 «Неверный формат данных»",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("POST", q + "/api/requests", "это не JSON");
                    Check(r.Status == 400, Describe(r));
                    return Describe(r);
                }
            });
            list.Add(new TestCase
            {
                Id = "И07", Area = "Неверный формат", Kind = "негативный", Title = "Заявка без темы и с clientId = «abc»",
                Expected = "400 с описанием ошибки поля",
                Run = () =>
                {
                    ApiResult r = ApiClient.Send("POST", q + "/api/requests", "{\"topic\":\"\",\"clientId\":\"abc\"}");
                    Check(r.Status == 400, Describe(r));
                    return Describe(r);
                }
            });
            list.Add(new TestCase
            {
                Id = "И08", Area = "Нет записи", Kind = "негативный", Title = "GET /api/clients/777 и /api/requests/777",
                Expected = "404 и 404",
                Run = () =>
                {
                    ApiResult a = ApiClient.Send("GET", c + "/api/clients/777"), b = ApiClient.Send("GET", q + "/api/requests/777");
                    Check(a.Status == 404 && b.Status == 404, Describe(a) + "; " + Describe(b));
                    return "404 и 404";
                }
            });
            list.Add(new TestCase
            {
                Id = "И09", Area = "Отказ модуля", Kind = "негативный", Title = "Модуль «Клиенты» остановлен — создание заявки",
                Expected = "503 «Модуль «Клиенты» недоступен», модуль «Заявки» продолжает работать",
                Run = () =>
                {
                    clients.Stop();
                    Check(WaitHealth(clients, false), "модуль «Клиенты» не остановился");
                    try
                    {
                        ApiResult r = ApiClient.Send("POST", q + "/api/requests",
                            Json.Write(new Dictionary<string, object> { { "topic", "Без клиентов" }, { "description", "" }, { "clientId", 1 } }));
                        Check(r.Status == 503, Describe(r));
                        Check(requests.Health().Status == 200, "модуль «Заявки» перестал отвечать");
                        return Describe(r) + "; «Заявки» работают";
                    }
                    finally
                    {
                        clients.Start();
                        WaitHealth(clients, true);
                    }
                }
            });
            list.Add(new TestCase
            {
                Id = "И10", Area = "Отказ модуля", Kind = "негативный", Title = "Модуль «Заявки» остановлен — карточка клиента",
                Expected = "503 «Модуль «Заявки» недоступен», модуль «Клиенты» продолжает работать",
                Run = () =>
                {
                    requests.Stop();
                    Check(WaitHealth(requests, false), "модуль «Заявки» не остановился");
                    try
                    {
                        ApiResult r = ApiClient.Send("GET", c + "/api/clients/1/card");
                        Check(r.Status == 503, Describe(r));
                        Check(clients.Health().Status == 200, "модуль «Клиенты» перестал отвечать");
                        return Describe(r) + "; «Клиенты» работают";
                    }
                    finally
                    {
                        requests.Start();
                        WaitHealth(requests, true);
                    }
                }
            });
            return list;
        }
    }
}
