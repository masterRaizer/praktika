using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace Den5_7.Integration
{
    /// <summary>JSON: сериализация через стандартный JavaScriptSerializer.</summary>
    public static class Json
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();

        public static string Write(object value)
        {
            return Serializer.Serialize(value);
        }

        /// <summary>Разбор JSON-объекта; null — тело не является корректным объектом JSON.</summary>
        public static Dictionary<string, object> ReadObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                return Serializer.DeserializeObject(text) as Dictionary<string, object>;
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        public static object[] ReadArray(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new object[0];
            try
            {
                return Serializer.DeserializeObject(text) as object[] ?? new object[0];
            }
            catch (ArgumentException)
            {
                return new object[0];
            }
        }

        public static string Str(IDictionary<string, object> obj, string key)
        {
            object v;
            return obj != null && obj.TryGetValue(key, out v) && v != null ? Convert.ToString(v) : null;
        }

        public static int? Int(IDictionary<string, object> obj, string key)
        {
            object v;
            if (obj == null || !obj.TryGetValue(key, out v) || v == null) return null;
            if (v is int) return (int)v;
            if (v is long) return (int)(long)v;
            if (v is decimal && (decimal)v == Math.Floor((decimal)v)) return (int)(decimal)v;
            int parsed;
            return v is string && int.TryParse((string)v, out parsed) ? parsed : (int?)null;
        }
    }

    public sealed class HttpRequestData
    {
        public string Method;
        public string Path;
        public Dictionary<string, string> Query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string Body = "";
    }

    public sealed class HttpResponseData
    {
        public int Status;
        public string Json;

        public static HttpResponseData Of(int status, object body)
        {
            return new HttpResponseData { Status = status, Json = Integration.Json.Write(body) };
        }

        public static HttpResponseData Error(int status, string message)
        {
            return Of(status, new Dictionary<string, object> { { "error", message }, { "status", status } });
        }
    }

    /// <summary>
    /// Небольшой HTTP-сервер на TcpListener: слушает только локальный адрес 127.0.0.1,
    /// поэтому не требует прав администратора и настройки брандмауэра.
    /// Каждый запрос обрабатывается в отдельном потоке; ответ — JSON в UTF-8.
    /// </summary>
    public sealed class MiniHttpServer
    {
        private readonly TcpListener listener;
        private readonly Func<HttpRequestData, HttpResponseData> handler;
        private volatile bool running;

        public MiniHttpServer(int port, Func<HttpRequestData, HttpResponseData> handler)
        {
            this.handler = handler;
            listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start()
        {
            listener.Start();
            running = true;
            var thread = new Thread(AcceptLoop) { IsBackground = true, Name = "http-accept" };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            listener.Stop();
        }

        private void AcceptLoop()
        {
            while (running)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    return;   // сервер остановлен
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                ThreadPool.QueueUserWorkItem(delegate { Serve(client); });
            }
        }

        private void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 5000;
                    NetworkStream stream = client.GetStream();
                    HttpRequestData request = ReadRequest(stream);
                    HttpResponseData response;
                    if (request == null)
                    {
                        response = HttpResponseData.Error(400, "Некорректный HTTP-запрос.");
                    }
                    else
                    {
                        try
                        {
                            response = handler(request) ?? HttpResponseData.Error(404, "Адрес не найден.");
                        }
                        catch (Exception)
                        {
                            response = HttpResponseData.Error(500, "Внутренняя ошибка модуля. Запрос не выполнен.");
                        }
                    }
                    WriteResponse(stream, response);
                }
                catch (IOException)
                {
                    // клиент закрыл соединение
                }
                catch (SocketException)
                {
                }
                catch (Exception)
                {
                    // любая другая ошибка обработки одного запроса не должна останавливать модуль
                }
            }
        }

        private static HttpRequestData ReadRequest(NetworkStream stream)
        {
            var header = new MemoryStream();
            int matched = 0;
            byte[] tail = { 13, 10, 13, 10 };
            while (matched < 4)
            {
                int b = stream.ReadByte();
                if (b < 0) return null;
                header.WriteByte((byte)b);
                matched = b == tail[matched] ? matched + 1 : (b == 13 ? 1 : 0);
                if (header.Length > 32 * 1024) return null;
            }
            string[] lines = Encoding.ASCII.GetString(header.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.None);
            string[] first = lines[0].Split(' ');
            if (first.Length < 2) return null;
            var request = new HttpRequestData { Method = first[0].ToUpperInvariant() };
            string target = first[1];
            int q = target.IndexOf('?');
            request.Path = Uri.UnescapeDataString(q >= 0 ? target.Substring(0, q) : target).TrimEnd('/');
            if (request.Path.Length == 0) request.Path = "/";
            if (q >= 0)
            {
                foreach (string pair in target.Substring(q + 1).Split('&'))
                {
                    if (pair.Length == 0) continue;
                    int eq = pair.IndexOf('=');
                    string key = Uri.UnescapeDataString(eq >= 0 ? pair.Substring(0, eq) : pair);
                    string value = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' ')) : "";
                    request.Query[key] = value;
                }
            }
            int length = 0;
            foreach (string line in lines)
            {
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(15).Trim(), out length);
                }
            }
            if (length > 1024 * 1024) return null;
            if (length > 0)
            {
                var body = new byte[length];
                int read = 0;
                while (read < length)
                {
                    int n = stream.Read(body, read, length - read);
                    if (n <= 0) break;
                    read += n;
                }
                request.Body = Encoding.UTF8.GetString(body, 0, read);
            }
            return request;
        }

        private static void WriteResponse(NetworkStream stream, HttpResponseData response)
        {
            byte[] body = Encoding.UTF8.GetBytes(response.Json ?? "{}");
            string head = "HTTP/1.1 " + response.Status + " " + Reason(response.Status) + "\r\n" +
                          "Content-Type: application/json; charset=utf-8\r\n" +
                          "Content-Length: " + body.Length + "\r\n" +
                          "Connection: close\r\n\r\n";
            byte[] h = Encoding.ASCII.GetBytes(head);
            stream.Write(h, 0, h.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static string Reason(int status)
        {
            switch (status)
            {
                case 200: return "OK";
                case 201: return "Created";
                case 400: return "Bad Request";
                case 404: return "Not Found";
                case 405: return "Method Not Allowed";
                case 422: return "Unprocessable Entity";
                case 503: return "Service Unavailable";
                default: return status >= 500 ? "Internal Server Error" : "OK";
            }
        }
    }

    /// <summary>Результат HTTP-вызова; Status = 0 — модуль недоступен (соединение не установлено).</summary>
    public sealed class ApiResult
    {
        public int Status;
        public string Body = "";
        public string Error;

        public bool Ok
        {
            get { return Status >= 200 && Status < 300; }
        }
    }

    /// <summary>Клиент REST API с таймаутом: ошибки сети не выбрасываются, а возвращаются в ApiResult.</summary>
    public static class ApiClient
    {
        public static ApiResult Send(string method, string url, string jsonBody = null, int timeoutMs = 3000)
        {
            var result = new ApiResult();
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = method;
                request.Timeout = timeoutMs;
                request.ReadWriteTimeout = timeoutMs;
                request.Proxy = null;
                request.Accept = "application/json";
                if (jsonBody != null)
                {
                    byte[] data = Encoding.UTF8.GetBytes(jsonBody);
                    request.ContentType = "application/json; charset=utf-8";
                    request.ContentLength = data.Length;
                    using (Stream s = request.GetRequestStream()) s.Write(data, 0, data.Length);
                }
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    result.Status = (int)response.StatusCode;
                    result.Body = ReadBody(response);
                }
            }
            catch (WebException ex)
            {
                var response = ex.Response as HttpWebResponse;
                if (response != null)
                {
                    using (response)
                    {
                        result.Status = (int)response.StatusCode;
                        result.Body = ReadBody(response);
                    }
                }
                else
                {
                    result.Status = 0;
                    result.Error = "нет ответа (" + ex.Status + ")";
                }
            }
            catch (UriFormatException)
            {
                result.Status = 0;
                result.Error = "неверный адрес модуля";
            }
            return result;
        }

        private static string ReadBody(HttpWebResponse response)
        {
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
