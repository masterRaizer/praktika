using System;
using System.Collections.Generic;
using System.Linq;

namespace Forecast.Core
{
    /// <summary>Показатели точности прогноза.</summary>
    public class ErrorStats
    {
        /// <summary>Средняя абсолютная ошибка.</summary>
        public double Mae;
        /// <summary>Средняя абсолютная процентная ошибка, %.</summary>
        public double Mape;
        /// <summary>Среднеквадратическая ошибка (средний квадрат ошибки).</summary>
        public double Mse;
        public double Rmse { get { return Math.Sqrt(Mse); } }
        /// <summary>Число точек, по которым считались MAE и MSE.</summary>
        public int Count;
        /// <summary>Точки с нулевым фактом, исключённые из MAPE.</summary>
        public int ZeroSkipped;
    }

    public static class ForecastMetrics
    {
        /// <summary>Ошибки по всем позициям, где есть и факт, и прогноз.</summary>
        public static ErrorStats Compute(double[] actual, double[] forecast)
        {
            var idx = new List<int>();
            for (int i = 0; i < Math.Min(actual.Length, forecast.Length); i++)
                if (!double.IsNaN(forecast[i])) idx.Add(i);
            return Compute(actual, forecast, idx);
        }

        /// <summary>Ошибки по заданному набору позиций.</summary>
        public static ErrorStats Compute(double[] actual, double[] forecast, IList<int> indices)
        {
            var s = new ErrorStats();
            double sumAbs = 0, sumSq = 0, sumPct = 0;
            int pctCount = 0;
            foreach (int i in indices)
            {
                double e = actual[i] - forecast[i];
                sumAbs += Math.Abs(e);
                sumSq += e * e;
                if (actual[i] == 0) { s.ZeroSkipped++; continue; }
                sumPct += Math.Abs(e / actual[i]) * 100.0;
                pctCount++;
            }
            s.Count = indices.Count;
            s.Mae = s.Count > 0 ? sumAbs / s.Count : double.NaN;
            s.Mse = s.Count > 0 ? sumSq / s.Count : double.NaN;
            s.Mape = pctCount > 0 ? sumPct / pctCount : double.NaN;
            return s;
        }

        /// <summary>Позиции, в которых прогноз есть у всех переданных методов.</summary>
        public static List<int> CommonIndices(params double[][] forecasts)
        {
            int n = forecasts.Min(f => f.Length);
            var r = new List<int>();
            for (int i = 0; i < n; i++)
                if (forecasts.All(f => !double.IsNaN(f[i]))) r.Add(i);
            return r;
        }

        /// <summary>Ошибка в процентах для одной точки (NaN при нулевом факте).</summary>
        public static double PercentError(double actual, double forecast)
        {
            if (actual == 0 || double.IsNaN(forecast)) return double.NaN;
            return Math.Abs(actual - forecast) / Math.Abs(actual) * 100.0;
        }
    }
}
