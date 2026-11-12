using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Forecast.Core
{
    /// <summary>
    /// Загрузка данных из CSV. Разделитель столбцов — «;» или табуляция,
    /// десятичный разделитель — запятая или точка. Первая строка-заголовок пропускается.
    /// </summary>
    public static class CsvLoader
    {
        private static string[] ReadLines(string path)
        {
            try
            {
                return File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (FileNotFoundException ex) { throw new ForecastException("Файл не найден: " + path, ex); }
            catch (DirectoryNotFoundException ex) { throw new ForecastException("Папка не найдена: " + path, ex); }
            catch (UnauthorizedAccessException ex) { throw new ForecastException("Нет доступа к файлу: " + path, ex); }
            catch (IOException ex) { throw new ForecastException("Ошибка чтения файла: " + ex.Message, ex); }
        }

        private static string[] SplitCells(string line)
        {
            if (line.IndexOf(';') >= 0) return line.Split(';');
            if (line.IndexOf('\t') >= 0) return line.Split('\t');
            return new[] { line };
        }

        private static bool LooksLikeHeader(string[] cells)
        {
            foreach (string c in cells)
            {
                double d;
                string s = c.Trim().Replace(',', '.');
                if (s.Length > 0 && !double.TryParse(s, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out d))
                    return true;
            }
            return false;
        }

        /// <summary>Ряд: по одному значению в строке либо значения через «;». Если в строке два столбца — берётся последний.</summary>
        public static double[] LoadSeries(string path)
        {
            string[] lines = ReadLines(path);
            var values = new List<double>();
            bool first = true;
            int lineNo = 0;
            foreach (string raw in lines)
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] cells = SplitCells(line);
                if (first && LooksLikeHeader(cells)) { first = false; continue; }
                first = false;
                if (cells.Length == 2)
                    values.Add(DataValidator.ParseValue(cells[1], "CSV, строка " + lineNo));
                else
                    foreach (string c in cells)
                        if (c.Trim().Length > 0) values.Add(DataValidator.ParseValue(c, "CSV, строка " + lineNo));
            }
            if (values.Count == 0) throw new ForecastException("В файле нет числовых данных.");
            return values.ToArray();
        }

        /// <summary>Пары «реклама; продажи» — по одной паре в строке.</summary>
        public static void LoadPairs(string path, out double[] x, out double[] y)
        {
            string[] lines = ReadLines(path);
            var xs = new List<double>();
            var ys = new List<double>();
            bool first = true;
            int lineNo = 0;
            foreach (string raw in lines)
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0) continue;
                string[] cells = SplitCells(line);
                if (first && LooksLikeHeader(cells)) { first = false; continue; }
                first = false;
                if (cells.Length < 2)
                    throw new ForecastException("CSV, строка " + lineNo + ": нужно два столбца «реклама; продажи».");
                xs.Add(DataValidator.ParseValue(cells[0], "CSV, строка " + lineNo));
                ys.Add(DataValidator.ParseValue(cells[1], "CSV, строка " + lineNo));
            }
            if (xs.Count == 0) throw new ForecastException("В файле нет числовых данных.");
            x = xs.ToArray();
            y = ys.ToArray();
        }

        public static void SaveSeries(string path, double[] y)
        {
            try
            {
                var sb = new StringBuilder("Период;Значение\r\n");
                for (int i = 0; i < y.Length; i++) sb.Append(i + 1).Append(';').Append(y[i]).Append("\r\n");
                File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            }
            catch (Exception ex) { throw new ForecastException("Не удалось сохранить файл: " + ex.Message, ex); }
        }
    }
}
