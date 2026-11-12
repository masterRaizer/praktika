using System;
using System.Collections.Generic;
using System.Globalization;

namespace Forecast.Core
{
    /// <summary>
    /// Единая проверка корректности данных для всех источников (вариант, ручной ввод, CSV, генерация).
    /// </summary>
    public static class DataValidator
    {
        /// <summary>
        /// Разбор значения ячейки: принимаются и запятая, и точка как десятичный разделитель.
        /// </summary>
        public static double ParseValue(object value, string where)
        {
            string s = value == null ? "" : Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            if (s.Length == 0)
                throw new ForecastException(where + ": пустое значение.");
            s = s.Replace(" ", "").Replace(' '.ToString(), "").Replace(',', '.');
            double v;
            if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                throw new ForecastException(where + ": «" + value + "» не является числом.");
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ForecastException(where + ": недопустимое значение.");
            return v;
        }

        /// <summary>Проверка ряда продаж: длина, отсутствие NaN и отрицательных значений.</summary>
        public static void CheckSeries(double[] y, int minLength, string what)
        {
            if (y == null || y.Length == 0)
                throw new ForecastException("Пустой " + what + ".");
            if (y.Length < minLength)
                throw new ForecastException("Слишком короткий " + what + ": " + y.Length + " знач., нужно не меньше " + minLength + ".");
            for (int i = 0; i < y.Length; i++)
            {
                if (double.IsNaN(y[i]) || double.IsInfinity(y[i]))
                    throw new ForecastException("Некорректное значение в позиции " + (i + 1) + ".");
                if (y[i] < 0)
                    throw new ForecastException("Отрицательные продажи в позиции " + (i + 1) + ": " + Util.F(y[i]) + ".");
            }
        }

        /// <summary>Предупреждения, которые не останавливают расчёт (нулевые значения и т. п.).</summary>
        public static List<string> Warnings(double[] y, string what)
        {
            var w = new List<string>();
            int zeros = 0;
            foreach (double v in y) if (v == 0) zeros++;
            if (zeros > 0)
                w.Add(what + ": нулевых значений — " + zeros + "; MAPE считается без этих точек (деление на ноль).");
            return w;
        }

        public static void CheckPairs(double[] x, double[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
                throw new ForecastException("Число значений расходов на рекламу и продаж не совпадает.");
            for (int i = 0; i < x.Length; i++)
            {
                if (x[i] < 0) throw new ForecastException("Отрицательные расходы на рекламу в строке " + (i + 1) + ".");
                if (y[i] < 0) throw new ForecastException("Отрицательные продажи в строке " + (i + 1) + ".");
            }
            if (x.Length < 3) throw new ForecastException("Для регрессии нужно не меньше 3 пар наблюдений.");
        }

        public static void CheckAlpha(double a, string name)
        {
            if (double.IsNaN(a) || a <= 0 || a >= 1)
                throw new ForecastException("Коэффициент " + name + " должен лежать в интервале (0; 1).");
        }
    }
}
