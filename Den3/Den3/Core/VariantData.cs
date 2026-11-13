using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace Den3.Core
{
    /// <summary>
    /// Исходные данные вариантов из документа «Варианты, день 3».
    /// Вариант 0 — демонстрационные данные из текста задания, вариант 2 — вариант этого компьютера.
    /// </summary>
    public sealed class VariantData
    {
        public int Number;
        public LaunchData Launch;
        public StrategyData Strategy;
        public ProductionData Production;

        /// <summary>Номера вариантов, данные которых есть в программе.</summary>
        public static readonly int[] Available = { 0, 2 };

        public static VariantData Get(int number)
        {
            switch (number)
            {
                case 0: return Demo();
                case 2: return Variant2();
                default: throw new DecisionException("Данных варианта " + number + " в программе нет (есть варианты: " +
                    string.Join(", ", Available) + ").");
            }
        }

        private static VariantData Demo()
        {
            var s = new StrategyData { Budget = 200 };
            s.Items.Add(new StrategyItem("Консервативная", 50, 80, 0.8));
            s.Items.Add(new StrategyItem("Умеренная", 100, 150, 0.6));
            s.Items.Add(new StrategyItem("Агрессивная", 200, 300, 0.4));
            return new VariantData
            {
                Number = 0,
                Launch = new LaunchData(),
                Strategy = s,
                Production = new ProductionData()
            };
        }

        private static VariantData Variant2()
        {
            var launch = new LaunchData
            {
                LaunchCost = 110, PSuccess = 0.70, Profit = 375, Loss = 40,
                ResearchCost = 10, PFavGivenSuccess = 0.80, PUnfavGivenFailure = 0.70
            };
            var s = new StrategyData { Budget = 280 };
            s.Items.Add(new StrategyItem("Консервативная", 90, 130, 0.70));
            s.Items.Add(new StrategyItem("Умеренная", 190, 295, 0.65));
            s.Items.Add(new StrategyItem("Агрессивная", 240, 385, 0.50));
            var prod = new ProductionData
            {
                ProdCost = new double[] { 20, 40, 60 },
                MktCost = new double[] { 10, 20 },
                Demand = new[] { 0.25, 0.50, 0.25 },
                TestCost = 8,
                Revenue = new[]
                {
                    new double[] { 40, 55, 51 }, new double[] { 43, 54, 64 },
                    new double[] { 35, 75, 104 }, new double[] { 42, 81, 116 },
                    new double[] { 32, 90, 144 }, new double[] { 40, 100, 189 }
                },
                Reliability = new[]
                {
                    new[] { 0.70, 0.20, 0.10 },
                    new[] { 0.15, 0.70, 0.15 },
                    new[] { 0.10, 0.20, 0.70 }
                }
            };
            return new VariantData { Number = 2, Launch = launch, Strategy = s, Production = prod };
        }
    }

    /// <summary>Сохранение и загрузка исходных данных в XML.</summary>
    public static class XmlStore
    {
        public static void Save<T>(string path, T data)
        {
            try
            {
                var ser = new XmlSerializer(typeof(T));
                using (var w = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)))
                    ser.Serialize(w, data);
            }
            catch (Exception ex)
            {
                throw new DecisionException("Не удалось сохранить файл: " + (ex.InnerException ?? ex).Message);
            }
        }

        public static T Load<T>(string path)
        {
            try
            {
                var ser = new XmlSerializer(typeof(T));
                using (var r = new StreamReader(path))
                {
                    object o = ser.Deserialize(r);
                    if (o == null) throw new DecisionException("Файл пуст.");
                    return (T)o;
                }
            }
            catch (DecisionException) { throw; }
            catch (InvalidOperationException ex)
            {
                throw new DecisionException("Файл не содержит данных этого задания или повреждён: " + (ex.InnerException ?? ex).Message);
            }
            catch (Exception ex)
            {
                throw new DecisionException("Не удалось прочитать файл: " + ex.Message);
            }
        }
    }

    /// <summary>Генерация случайных исходных данных в пределах, заданных на форме.</summary>
    public sealed class RandomData
    {
        private readonly Random rnd;

        public RandomData(int? seed = null) { rnd = seed.HasValue ? new Random(seed.Value) : new Random(); }

        /// <summary>Случайное значение, округлённое до целых тыс. руб.</summary>
        public double Money(double lo, double hi)
        {
            if (hi < lo) { double t = lo; lo = hi; hi = t; }
            return Math.Round(lo + rnd.NextDouble() * (hi - lo));
        }

        /// <summary>Вероятность с шагом 0,01 в пределах [lo; hi].</summary>
        public double Probability(double lo = 0.05, double hi = 0.95)
        {
            return Math.Round(lo + rnd.NextDouble() * (hi - lo), 2);
        }

        /// <summary>
        /// Распределение из n вероятностей с шагом 0,01 и суммой ровно 1
        /// (целые доли в процентах; остаток добавляется к наибольшей). bias — номер исхода с повышенным весом.
        /// </summary>
        public double[] Distribution(int n, int bias = -1, double biasWeight = 0)
        {
            var w = new double[n];
            double sum = 0;
            for (int i = 0; i < n; i++)
            {
                w[i] = 0.15 + rnd.NextDouble() + (i == bias ? biasWeight : 0);
                sum += w[i];
            }
            var pct = new int[n];
            int total = 0;
            for (int i = 0; i < n; i++) { pct[i] = Math.Max(1, (int)Math.Round(w[i] / sum * 100)); total += pct[i]; }
            int maxI = Array.IndexOf(pct, pct.Max());
            pct[maxI] += 100 - total;
            return pct.Select(v => v / 100.0).ToArray();
        }

        public LaunchData Launch(double lo, double hi)
        {
            var d = new LaunchData
            {
                LaunchCost = Money(lo, hi),
                Profit = Money(lo, hi) * 2 + Money(lo, hi),
                Loss = Money(lo * 0.2, hi * 0.4),
                ResearchCost = Money(Math.Max(1, lo * 0.05), Math.Max(2, hi * 0.1)),
                PSuccess = Probability(0.3, 0.85),
                PFavGivenSuccess = Probability(0.6, 0.95),
                PUnfavGivenFailure = Probability(0.55, 0.95)
            };
            return d;
        }

        public StrategyData Strategy(int count, double lo, double hi)
        {
            var d = new StrategyData();
            string[] names = { "Консервативная", "Умеренная", "Агрессивная", "Стратегия 4", "Стратегия 5", "Стратегия 6" };
            var inv = Enumerable.Range(0, count).Select(i => Money(lo, hi)).OrderBy(v => v).ToArray();
            var probs = Enumerable.Range(0, count).Select(i => Probability(0.35, 0.9)).OrderByDescending(v => v).ToArray();
            for (int i = 0; i < count; i++)
            {
                double gain = inv[i] * (1.2 + rnd.NextDouble() * 0.8);   // прибыль растёт вместе с риском
                d.Items.Add(new StrategyItem(names[i], inv[i], Math.Round(gain), probs[i]));
            }
            double sum2 = inv.Take(Math.Min(2, count)).Sum();
            d.Budget = Math.Round(Math.Max(inv.Max(), sum2 * (0.9 + rnd.NextDouble() * 0.3)));
            return d;
        }

        public ProductionData Production(double lo, double hi)
        {
            var d = new ProductionData();
            double step = Math.Max(5, Math.Round((hi - lo) / 6));
            d.ProdCost = new[] { Money(lo * 0.3, lo * 0.5), 0.0, 0.0 };
            d.ProdCost[1] = d.ProdCost[0] + Money(step, step * 2);
            d.ProdCost[2] = d.ProdCost[1] + Money(step, step * 2);
            d.MktCost = new[] { Money(5, 12), 0.0 };
            d.MktCost[1] = d.MktCost[0] + Money(5, 12);
            d.Demand = Distribution(3, 1, 0.6);
            d.TestCost = Money(2, 10);
            for (int s = 0; s < 3; s++)
                for (int m = 0; m < 2; m++)
                {
                    double baseLow = Money(lo * 0.6, lo * 0.9);
                    double mid = baseLow + Money(step * (1 + s), step * (2 + s));
                    double high = mid + Money(step * s, step * (1 + 2 * s)) + m * Money(2, step);
                    d.Revenue[s * 2 + m] = new[] { baseLow + m * Money(1, 8), mid + m * Money(0, step), Math.Min(hi * 2, high) };
                }
            for (int i = 0; i < 3; i++) d.Reliability[i] = Distribution(3, i, 2.4);
            return d;
        }
    }
}
