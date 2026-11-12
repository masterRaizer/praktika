using System;
using System.Collections.Generic;
using System.Linq;

namespace Forecast.Core
{
    /// <summary>Результат перебора коэффициента сглаживания.</summary>
    public class AlphaSearchResult
    {
        public double[] Alphas;
        public double[] Mse;
        public double BestAlpha;
        public double BestMse;
    }

    /// <summary>Результат перебора пары (α, β) метода Хольта.</summary>
    public class HoltGridResult
    {
        public double[] Values;   // значения сетки для α и β
        public double[,] Mse;     // [индекс α, индекс β]
        public double BestAlpha;
        public double BestBeta;
        public double BestMse;
        public double MinMse;
        public double MaxMse;
    }

    /// <summary>Результат сравнения периодов скользящего среднего.</summary>
    public class MaPeriodScore
    {
        public int Period;
        public ErrorStats Own;      // по всем месяцам, где есть прогноз этого периода
        public ErrorStats Common;   // по общим месяцам для всех сравниваемых периодов
    }

    public static class Optimizer
    {
        /// <summary>Перебор α от 0,01 до 0,99 с шагом 0,01 по минимуму MSE одношаговых прогнозов.</summary>
        public static AlphaSearchResult SearchSesAlpha(double[] y, SesInit init)
        {
            var r = new AlphaSearchResult();
            var alphas = new List<double>();
            var mses = new List<double>();
            r.BestMse = double.MaxValue;
            for (int k = 1; k <= 99; k++)
            {
                double a = k / 100.0;
                double[] f = new ExponentialSmoothingMethod(a, init).Fit(y);
                double mse = ForecastMetrics.Compute(y, f).Mse;
                alphas.Add(a); mses.Add(mse);
                if (mse < r.BestMse) { r.BestMse = mse; r.BestAlpha = a; }
            }
            r.Alphas = alphas.ToArray();
            r.Mse = mses.ToArray();
            return r;
        }

        /// <summary>Перебор α и β от 0,05 до 0,95 с шагом 0,05 по минимуму MSE.</summary>
        public static HoltGridResult SearchHolt(double[] y)
        {
            var values = new List<double>();
            for (int k = 1; k <= 19; k++) values.Add(k * 0.05);
            var r = new HoltGridResult();
            r.Values = values.ToArray();
            r.Mse = new double[values.Count, values.Count];
            r.BestMse = double.MaxValue; r.MinMse = double.MaxValue; r.MaxMse = double.MinValue;
            for (int i = 0; i < values.Count; i++)
                for (int j = 0; j < values.Count; j++)
                {
                    double[] f = new HoltMethod(values[i], values[j]).Fit(y);
                    double mse = ForecastMetrics.Compute(y, f).Mse;
                    r.Mse[i, j] = mse;
                    r.MinMse = Math.Min(r.MinMse, mse);
                    r.MaxMse = Math.Max(r.MaxMse, mse);
                    if (mse < r.BestMse) { r.BestMse = mse; r.BestAlpha = values[i]; r.BestBeta = values[j]; }
                }
            return r;
        }

        /// <summary>
        /// Сравнение периодов скользящего среднего. Лучший период выбирается по MAPE
        /// на общих месяцах (иначе у меньшего периода больше проверочных точек).
        /// </summary>
        public static List<MaPeriodScore> CompareMaPeriods(double[] y, IEnumerable<int> periods)
        {
            var list = periods.Where(p => p >= 1 && p < y.Length).ToList();
            if (list.Count == 0) throw new ForecastException("Ряд слишком короткий для выбранных периодов скользящего среднего.");
            var fits = list.Select(p => new MovingAverageMethod(p).Fit(y)).ToArray();
            var common = ForecastMetrics.CommonIndices(fits);
            var result = new List<MaPeriodScore>();
            for (int i = 0; i < list.Count; i++)
                result.Add(new MaPeriodScore
                {
                    Period = list[i],
                    Own = ForecastMetrics.Compute(y, fits[i]),
                    Common = ForecastMetrics.Compute(y, fits[i], common)
                });
            return result;
        }

        public static int BestMaPeriod(double[] y, IEnumerable<int> periods)
        {
            return CompareMaPeriods(y, periods).OrderBy(s => s.Common.Mape).ThenBy(s => s.Common.Mae).First().Period;
        }
    }
}
