using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Den5_7.Logic
{
    /// <summary>Экспорт таблицы в CSV для Excel: UTF-8 с BOM, разделитель «;».</summary>
    public static class CsvExporter
    {
        public static void Write(string path, IList<string> headers, IEnumerable<IList<string>> rows)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine(Line(headers));
                foreach (IList<string> row in rows)
                {
                    writer.WriteLine(Line(row));
                }
            }
        }

        private static string Line(IList<string> cells)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < cells.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(Escape(cells[i]));
            }
            return sb.ToString();
        }

        private static string Escape(string value)
        {
            value = value ?? "";
            bool quote = value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) >= 0;
            value = value.Replace("\"", "\"\"");
            return quote ? "\"" + value + "\"" : value;
        }
    }
}
