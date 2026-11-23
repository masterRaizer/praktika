using System;
using System.Configuration;
using System.Diagnostics;
using System.Windows.Forms;

namespace Den5_7.Integration
{
    /// <summary>
    /// Запуск и остановка модулей «Клиенты» и «Заявки» как независимых процессов (каждый на своём порту).
    /// Модуль — это тот же Den5_7.exe с параметром --module, окна у него нет.
    /// </summary>
    public sealed class ModuleHost
    {
        public string Name { get; private set; }
        public string Title { get; private set; }
        public int Port { get; private set; }
        public string PeerUrl { get; private set; }
        private Process process;

        public ModuleHost(string name, string title, int port, string peerUrl)
        {
            Name = name;
            Title = title;
            Port = port;
            PeerUrl = peerUrl;
        }

        public string Url
        {
            get { return "http://127.0.0.1:" + Port; }
        }

        public bool IsRunning
        {
            get
            {
                try
                {
                    return process != null && !process.HasExited;
                }
                catch (InvalidOperationException)
                {
                    return false;
                }
            }
        }

        public int ProcessId
        {
            get { return IsRunning ? process.Id : 0; }
        }

        public void Start()
        {
            if (IsRunning) return;
            string exe = Application.ExecutablePath;
            string args = "--module=" + Name + " --port=" + Port + " --peer=" + PeerUrl +
                          " --parent=" + Process.GetCurrentProcess().Id;
            var info = new ProcessStartInfo { UseShellExecute = false, CreateNoWindow = true };
            if (Type.GetType("Mono.Runtime") != null)
            {
                info.FileName = "mono";
                info.Arguments = "\"" + exe + "\" " + args;
            }
            else
            {
                info.FileName = exe;
                info.Arguments = args;
            }
            process = Process.Start(info);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            try
            {
                process.Kill();
                process.WaitForExit(3000);
            }
            catch (InvalidOperationException)
            {
                // процесс уже завершился
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            process = null;
        }

        public ApiResult Health()
        {
            return ApiClient.Send("GET", Url + "/api/health", null, 1500);
        }

        /// <summary>Порты модулей из Den5_7.exe.config (appSettings ClientsPort, RequestsPort).</summary>
        public static int ConfigPort(string key, int fallback)
        {
            int port;
            string raw = ConfigurationManager.AppSettings[key];
            return int.TryParse(raw, out port) && port > 1024 && port < 65536 ? port : fallback;
        }
    }
}
