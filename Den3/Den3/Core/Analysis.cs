using System;
using System.Collections.Generic;
using System.Linq;

namespace Den3.Core
{
    /// <summary>Точка смены решения: значение параметра, при котором две альтернативы равноценны.</summary>
    public sealed class CrossPoint
    {
        public string Title;
        public double Analytic = double.NaN, Numeric = double.NaN, Value = double.NaN;
        public int Iterations;
        public double Difference { get { return Math.Abs(Analytic - Numeric); } }
    }

    /// <summary>Численные методы анализа чувствительности.</summary>
    public static class Sensitivity
    {
        /// <summary>
        /// Метод деления отрезка пополам: корень f на [a; b], если на концах разные знаки.
        /// Возвращает NaN, если корня на отрезке нет.
        /// </summary>
        public static double Bisect(Func<double, double> f, double a, double b, out int iterations, double tol = 1e-7)
        {
            iterations = 0;
            double fa = f(a), fb = f(b);
            if (Math.Abs(fa) < 1e-12) return a;
            if (Math.Abs(fb) < 1e-12) return b;
            if (fa * fb > 0) return double.NaN;
            while (b - a > tol && iterations < 200)
            {
                iterations++;
                double m = (a + b) / 2, fm = f(m);
                if (fa * fm <= 0) { b = m; fb = fm; }
                else { a = m; fa = fm; }
            }
            return (a + b) / 2;
        }

        /// <summary>
        /// Поиск всех смен знака f на [lo; hi] по сетке с последующим уточнением делением пополам.
        /// </summary>
        public static List<double> Roots(Func<double, double> f, double lo, double hi, int steps, out int totalIterations)
        {
            var list = new List<double>();
            totalIterations = 0;
            double prevX = lo, prev = f(lo);
            for (int i = 1; i <= steps; i++)
            {
                double x = lo + (hi - lo) * i / steps, v = f(x);
                if (prev == 0 && i == 1) list.Add(prevX);
                if (prev * v < 0 || (v == 0 && prev != 0))
                {
                    int it;
                    double r = Bisect(f, prevX, x, out it);
                    totalIterations += it;
                    if (!double.IsNaN(r)) list.Add(r);
                }
                prevX = x;
                prev = v;
            }
            return list;
        }

        /// <summary>Значения функции на равномерной сетке.</summary>
        public static double[] Grid(double lo, double hi, int n)
        {
            var r = new double[n + 1];
            for (int i = 0; i <= n; i++) r[i] = lo + (hi - lo) * i / n;
            return r;
        }
    }

    /// <summary>Проверка устойчивости решения методом Монте-Карло.</summary>
    public static class MonteCarlo
    {
        public sealed class Item
        {
            public string Name;
            public int Count;
            public double Share;
        }

        public static List<Item> Run(Func<Random, string> trial, int n, int seed)
        {
            var rnd = new Random(seed);
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < n; i++)
            {
                string k = trial(rnd);
                int c;
                counts.TryGetValue(k, out c);
                counts[k] = c + 1;
            }
            return counts.OrderByDescending(p => p.Value)
                .Select(p => new Item { Name = p.Key, Count = p.Value, Share = (double)p.Value / n }).ToList();
        }
    }

    /// <summary>Критерии выбора в условиях неопределённости (вероятности состояний не используются).</summary>
    public sealed class CriteriaResult
    {
        public string[] Names;
        public double[,] Matrix;
        public double[] Wald, Savage, Hurwicz, Laplace;
        public double[,] Regret;
        public int BestWald, BestSavage, BestHurwicz, BestLaplace;
        public double Alpha;
    }

    public static class UncertaintyCriteria
    {
        /// <param name="m">Матрица выигрышей: строка — альтернатива, столбец — состояние среды.</param>
        /// <param name="alpha">Коэффициент оптимизма Гурвица (вес лучшего исхода).</param>
        public static CriteriaResult Compute(string[] names, double[,] m, double alpha)
        {
            int n = m.GetLength(0), k = m.GetLength(1);
            var r = new CriteriaResult
            {
                Names = names, Matrix = m, Alpha = alpha,
                Wald = new double[n], Savage = new double[n], Hurwicz = new double[n], Laplace = new double[n],
                Regret = new double[n, k]
            };
            var colMax = new double[k];
            for (int j = 0; j < k; j++)
            {
                colMax[j] = double.NegativeInfinity;
                for (int i = 0; i < n; i++) colMax[j] = Math.Max(colMax[j], m[i, j]);
            }
            for (int i = 0; i < n; i++)
            {
                double mn = double.PositiveInfinity, mx = double.NegativeInfinity, sum = 0, reg = 0;
                for (int j = 0; j < k; j++)
                {
                    mn = Math.Min(mn, m[i, j]);
                    mx = Math.Max(mx, m[i, j]);
                    sum += m[i, j];
                    r.Regret[i, j] = colMax[j] - m[i, j];
                    reg = Math.Max(reg, r.Regret[i, j]);
                }
                r.Wald[i] = mn;
                r.Hurwicz[i] = alpha * mx + (1 - alpha) * mn;
                r.Laplace[i] = sum / k;
                r.Savage[i] = reg;
            }
            r.BestWald = ArgMax(r.Wald);
            r.BestHurwicz = ArgMax(r.Hurwicz);
            r.BestLaplace = ArgMax(r.Laplace);
            r.BestSavage = ArgMax(r.Savage.Select(v => -v).ToArray());
            return r;
        }

        private static int ArgMax(double[] v)
        {
            int b = 0;
            for (int i = 1; i < v.Length; i++) if (v[i] > v[b] + 1e-9) b = i;
            return b;
        }
    }

    /// <summary>Учёт отношения к риску: экспоненциальная функция полезности U(x) = 1 − exp(−x / R).</summary>
    public static class RiskUtility
    {
        /// <summary>Детерминированный эквивалент распределения при толерантности к риску R.</summary>
        public static double CertaintyEquivalent(List<Outcome> dist, double r)
        {
            double eu = dist.Sum(o => o.Probability * (1 - Math.Exp(-o.Value / r)));
            return -r * Math.Log(1 - eu);
        }
    }

    /// <summary>Исходные данные собственного примера (задание 2.2): открытие кофейни.</summary>
    [Serializable]
    public class CafeData
    {
        public double PHigh = 0.5;          // вероятность высокого спроса в первый год
        public double BigCost = 150;        // открыть сразу большую кофейню
        public double BigHigh = 320, BigLow = 90;
        public double SmallCost = 60;       // малая кофейня
        public double SmallHigh = 160, SmallLow = 70, SellOut = 35;
        public double ExpandCost = 110;     // расширение малой кофейни после удачного года
        public double PKeep = 0.8;          // спрос сохранится после расширения
        public double ExpandHigh = 320, ExpandLow = 140;

        public CafeData Clone() { return (CafeData)MemberwiseClone(); }
    }

    public static class CafeProblem
    {
        /// <summary>Дерево с двумя уровнями решений: формат кофейни → (спрос) → расширение или закрытие.</summary>
        public static Node Build(CafeData d)
        {
            var root = new DecisionNode("Формат?");
            var big = root.Add(new ChanceNode("Спрос", "Большая кофейня") { Flow = -d.BigCost });
            big.Add(new OutcomeNode("Высокий", d.BigHigh), d.PHigh);
            big.Add(new OutcomeNode("Низкий", d.BigLow), 1 - d.PHigh);

            var small = root.Add(new ChanceNode("Спрос, 1-й год", "Малая кофейня") { Flow = -d.SmallCost });
            var up = small.Add(new DecisionNode("Расширять?", "Высокий"), d.PHigh);
            var exp = up.Add(new ChanceNode("Спрос, 2-й год", "Расширить") { Flow = -d.ExpandCost });
            exp.Add(new OutcomeNode("Сохранился", d.ExpandHigh), d.PKeep);
            exp.Add(new OutcomeNode("Упал", d.ExpandLow), 1 - d.PKeep);
            up.Add(new OutcomeNode("Не расширять", d.SmallHigh));
            var down = small.Add(new DecisionNode("Продолжать?", "Низкий"), 1 - d.PHigh);
            down.Add(new OutcomeNode("Продолжить", d.SmallLow));
            down.Add(new OutcomeNode("Закрыть и продать", d.SellOut));

            root.Add(new OutcomeNode("Не открывать", 0));
            return root;
        }
    }
}
