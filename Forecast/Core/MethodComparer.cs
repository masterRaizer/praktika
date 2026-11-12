using System;
using System.Collections.Generic;
using System.Linq;

namespace Forecast.Core
{
    /// <summary>Оценка одного метода в сравнении.</summary>
    public class MethodScore
    {
        public string Key;                 // MA, SES, HOLT, TREND, COMB
        public IForecastMethod Method;     // настроен по всему ряду
        public IForecastMethod HoldoutMethod; // настроен по ряду без последних наблюдений
        public double[] Fit;
        public ErrorStats InSample;        // одношаговые ошибки на общих точках
        public double[] HoldoutForecast;
        public ErrorStats Holdout;         // ошибки на отложенных наблюдениях
    }

    public class ComparisonResult
    {
        public double[] Series;
        public int HoldoutCount;
        public List<int> CommonIndices;
        public List<MethodScore> Scores = new List<MethodScore>();

        /// <summary>Лучший метод: минимум MAPE на отложенных данных, при равенстве — на обучающей части.</summary>
        public MethodScore BestByHoldout(bool includeCombined)
        {
            return Scores.Where(s => includeCombined || s.Key != "COMB")
                         .OrderBy(s => s.Holdout.Mape).ThenBy(s => s.InSample.Mape).First();
        }

        public MethodScore BestByInSample(bool includeCombined)
        {
            return Scores.Where(s => includeCombined || s.Key != "COMB")
                         .OrderBy(s => s.InSample.Mape).ThenBy(s => s.Holdout.Mape).First();
        }

        public MethodScore Get(string key) { return Scores.FirstOrDefault(s => s.Key == key); }
    }

    /// <summary>Сравнение методов на одних и тех же данных (задания 2.1 и 2.2).</summary>
    public static class MethodComparer
    {
        public static readonly int[] MaPeriods = { 3, 4, 5 };

        /// <summary>Набор методов с параметрами, подобранными по ряду y.</summary>
        public static List<KeyValuePair<string, IForecastMethod>> TunedMethods(double[] y)
        {
            DataValidator.CheckSeries(y, 6, "ряд для сравнения методов");
            var list = new List<KeyValuePair<string, IForecastMethod>>();
            int period = Optimizer.BestMaPeriod(y, MaPeriods.Where(p => p + 1 < y.Length));
            list.Add(new KeyValuePair<string, IForecastMethod>("MA", new MovingAverageMethod(period)));
            double alpha = Optimizer.SearchSesAlpha(y, SesInit.FirstValue).BestAlpha;
            list.Add(new KeyValuePair<string, IForecastMethod>("SES", new ExponentialSmoothingMethod(alpha)));
            HoltGridResult g = Optimizer.SearchHolt(y);
            list.Add(new KeyValuePair<string, IForecastMethod>("HOLT", new HoltMethod(g.BestAlpha, g.BestBeta)));
            list.Add(new KeyValuePair<string, IForecastMethod>("TREND", new LinearTrendMethod()));
            return list;
        }

        public static ComparisonResult Compare(double[] y, int holdout, bool includeCombined)
        {
            if (holdout < 1) throw new ForecastException("Число отложенных наблюдений должно быть не меньше 1.");
            DataValidator.CheckSeries(y, holdout + 6, "ряд для сравнения методов");

            var res = new ComparisonResult { Series = y, HoldoutCount = holdout };
            var full = TunedMethods(y);
            double[] train = Util.Take(y, y.Length - holdout);
            double[] test = Util.Skip(y, y.Length - holdout);
            var trainMethods = TunedMethods(train);

            if (includeCombined)
            {
                full.Add(new KeyValuePair<string, IForecastMethod>("COMB", new CombinedMethod(full.Select(p => p.Value))));
                trainMethods.Add(new KeyValuePair<string, IForecastMethod>("COMB", new CombinedMethod(trainMethods.Select(p => p.Value))));
            }

            double[][] fits = full.Select(p => p.Value.Fit(y)).ToArray();
            res.CommonIndices = ForecastMetrics.CommonIndices(fits);

            for (int i = 0; i < full.Count; i++)
            {
                var sc = new MethodScore
                {
                    Key = full[i].Key,
                    Method = full[i].Value,
                    HoldoutMethod = trainMethods[i].Value,
                    Fit = fits[i],
                    InSample = ForecastMetrics.Compute(y, fits[i], res.CommonIndices)
                };
                sc.HoldoutForecast = trainMethods[i].Value.Forecast(train, holdout);
                sc.Holdout = ForecastMetrics.Compute(test, sc.HoldoutForecast);
                res.Scores.Add(sc);
            }
            return res;
        }

        /// <summary>Зависимость ошибки от периода скользящего среднего (на общих точках).</summary>
        public static List<MaPeriodScore> MaPeriodInfluence(double[] y, int from, int to)
        {
            var periods = new List<int>();
            for (int p = from; p <= to; p++) if (p + 1 < y.Length) periods.Add(p);
            return Optimizer.CompareMaPeriods(y, periods);
        }

        /// <summary>Зависимость MAPE и MAE экспоненциального сглаживания от α (шаг 0,01).</summary>
        public static void SesInfluence(double[] y, out double[] alphas, out double[] mape, out double[] mae, out double[] mse)
        {
            alphas = new double[99]; mape = new double[99]; mae = new double[99]; mse = new double[99];
            for (int k = 1; k <= 99; k++)
            {
                double a = k / 100.0;
                ErrorStats e = ForecastMetrics.Compute(y, new ExponentialSmoothingMethod(a).Fit(y));
                alphas[k - 1] = a; mape[k - 1] = e.Mape; mae[k - 1] = e.Mae; mse[k - 1] = e.Mse;
            }
        }

        /// <summary>
        /// Скользящая проверка: прогноз строится от каждой точки начала (по первым t наблюдениям, t ≥ minTrain)
        /// на 1..maxH шагов вперёд и сравнивается с фактом. Возвращает MAPE, %, для каждого горизонта.
        /// </summary>
        public static double[] RollingOriginMape(IForecastMethod m, double[] y, int minTrain, int maxH)
        {
            DataValidator.CheckSeries(y, minTrain + maxH, "ряд для скользящей проверки");
            double[] result = new double[maxH];
            for (int h = 1; h <= maxH; h++)
            {
                double sum = 0;
                int cnt = 0;
                for (int t = minTrain; t + h <= y.Length; t++)
                {
                    double f = m.Forecast(Util.Take(y, t), h)[h - 1];
                    double pct = ForecastMetrics.PercentError(y[t + h - 1], f);
                    if (double.IsNaN(pct)) continue;
                    sum += pct; cnt++;
                }
                result[h - 1] = cnt > 0 ? sum / cnt : double.NaN;
            }
            return result;
        }

        public static string DisplayName(string key)
        {
            switch (key)
            {
                case "MA": return "Скользящее среднее";
                case "SES": return "Экспоненциальное сглаживание";
                case "HOLT": return "Метод Хольта";
                case "TREND": return "Линейная регрессия";
                case "COMB": return "Комбинированный";
                default: return key;
            }
        }
    }
}
