using System;
using System.Diagnostics;

namespace Den1
{
    /// <summary>
    /// Слушатель трассировки: выводит сообщения классов Debug и Trace в журнал на форме.
    /// По стеку вызовов определяет, каким классом отправлено сообщение, и помечает строку
    /// меткой «Debug» или «Trace».
    /// </summary>
    public class LogTraceListener : TraceListener
    {
        private readonly FxConsole target;
        private string pending = "";

        public LogTraceListener(FxConsole target)
        {
            this.target = target;
        }

        public override void Write(string message)
        {
            pending += message;
        }

        public override void WriteLine(string message)
        {
            if (target.IsDisposed) return;
            string indent = new string(' ', IndentLevel * IndentSize);
            target.Add(indent + pending + message, Source());
            pending = "";
        }

        public override void Fail(string message, string detailMessage)
        {
            if (target.IsDisposed) return;
            target.Add("Assert: " + message + (string.IsNullOrEmpty(detailMessage) ? "" : " — " + detailMessage), LogKind.Error);
        }

        private static LogKind Source()
        {
            try
            {
                StackFrame[] frames = new StackTrace(2, false).GetFrames();
                if (frames != null)
                {
                    foreach (StackFrame f in frames)
                    {
                        Type t = f.GetMethod() == null ? null : f.GetMethod().DeclaringType;
                        if (t == typeof(Debug)) return LogKind.Debug;
                        if (t == typeof(Trace)) return LogKind.Trace;
                    }
                }
            }
            catch (Exception)
            {
                // стек недоступен — обычная строка
            }
            return LogKind.Info;
        }
    }
}
