using System;
using System.Collections.Generic;

namespace Den3.Core
{
    /// <summary>Исходные данные задания 1.1 (тыс. руб.).</summary>
    [Serializable]
    public class LaunchData
    {
        public double LaunchCost = 100;          // стоимость запуска, вычитается при любом исходе
        public double PSuccess = 0.6;            // вероятность успеха
        public double Profit = 300;              // прибыль при успехе (без учёта запуска)
        public double Loss = 50;                 // убыток при неудаче (дополнительно к запуску)
        public double ResearchCost = 10;         // стоимость маркетингового исследования
        public double PFavGivenSuccess = 0.8;    // благоприятный прогноз при успехе
        public double PUnfavGivenFailure = 0.7;  // неблагоприятный прогноз при неудаче

        public LaunchData Clone() { return (LaunchData)MemberwiseClone(); }
    }

    /// <summary>Результаты задания 1.1.</summary>
    public sealed class LaunchResult
    {
        public Node Root;
        public double EvLaunch, EvNoResearch, EvResearchGross, EvResearch, Evsi, MaxPrice, Evpi, EvPerfect;
        public double PFav, PUnfav, PSuccessFav, PSuccessUnfav, EvLaunchFav, EvLaunchUnfav;
        public bool LaunchFav, LaunchUnfav, LaunchNoResearch, ResearchWorth;
        public double CriticalP;
        public string Policy;
    }

    /// <summary>Задание 1.1: запуск нового продукта с возможностью заказать маркетинговое исследование.</summary>
    public static class LaunchProblem
    {
        public static void Check(LaunchData d)
        {
            CheckProbability(d.PSuccess, "Вероятность успеха");
            CheckProbability(d.PFavGivenSuccess, "Вероятность благоприятного прогноза при успехе");
            CheckProbability(d.PUnfavGivenFailure, "Вероятность неблагоприятного прогноза при неудаче");
            foreach (double v in new[] { d.LaunchCost, d.Profit, d.Loss, d.ResearchCost })
                if (double.IsNaN(v) || double.IsInfinity(v)) throw new DecisionException("Денежные значения должны быть числами.");
        }

        internal static void CheckProbability(double p, string what)
        {
            if (double.IsNaN(p) || p < 0 || p > 1) throw new DecisionException(what + " должна быть в пределах от 0 до 1.");
        }

        /// <summary>Ветвь «Запустить»: стоимость запуска на ветви, далее случайный узел «Рынок».</summary>
        private static void AddLaunch(DecisionNode parent, LaunchData d, double pSuccess)
        {
            var market = parent.Add(new ChanceNode("Рынок", "Запустить") { Flow = -d.LaunchCost });
            market.Add(new OutcomeNode("Успех", d.Profit), pSuccess);
            market.Add(new OutcomeNode("Неудача", -d.Loss), 1 - pSuccess);
            parent.Add(new OutcomeNode("Не запускать", 0));
        }

        /// <summary>Построение полного дерева: решение об исследовании → прогноз → запуск → рынок.</summary>
        public static Node Build(LaunchData d)
        {
            Check(d);
            var root = new DecisionNode("Исследование?") { Tag = "root" };

            var noResearch = root.Add(new DecisionNode("Запуск?", "Без исследования") { Tag = "noR" });
            AddLaunch(noResearch, d, d.PSuccess);

            double p = d.PSuccess;
            double[] prior = { p, 1 - p };
            double[][] like =
            {
                new[] { d.PFavGivenSuccess, 1 - d.PFavGivenSuccess },        // при успехе: благоприятный / неблагоприятный
                new[] { 1 - d.PUnfavGivenFailure, d.PUnfavGivenFailure }     // при неудаче
            };
            double[] marginal = Bayes.Marginal(prior, like);
            var research = root.Add(new ChanceNode("Прогноз", "Заказать исследование") { Flow = -d.ResearchCost, Tag = "R" });
            string[] names = { "Благоприятный", "Неблагоприятный" };
            for (int j = 0; j < 2; j++)
            {
                double[] post = Bayes.Posterior(prior, like, j);
                var dec = research.Add(new DecisionNode("Запуск?", names[j]) { Tag = j == 0 ? "fav" : "unfav" }, marginal[j]);
                AddLaunch(dec, d, post[0]);
            }
            return root;
        }

        /// <summary>Расчёт всех показателей задания.</summary>
        public static LaunchResult Solve(LaunchData d)
        {
            Node root = Build(d);
            TreeSolver.Solve(root);
            var r = new LaunchResult { Root = root };
            Node noR = root.Children[0], res = root.Children[1];
            r.EvLaunch = noR.Children[0].BranchValue;
            r.EvNoResearch = noR.Value;
            r.LaunchNoResearch = noR.Best == 0;
            r.EvResearch = res.BranchValue;
            r.EvResearchGross = res.Value;
            r.Evsi = TreeSolver.Clean(r.EvResearchGross - r.EvNoResearch);
            r.MaxPrice = Math.Max(0, r.Evsi);
            r.ResearchWorth = r.Evsi > d.ResearchCost + 1e-9;

            Node fav = res.Children[0], unfav = res.Children[1];
            r.PFav = fav.Probability;
            r.PUnfav = unfav.Probability;
            r.PSuccessFav = fav.Children[0].Children[0].Probability;
            r.PSuccessUnfav = unfav.Children[0].Children[0].Probability;
            r.EvLaunchFav = fav.Children[0].BranchValue;
            r.EvLaunchUnfav = unfav.Children[0].BranchValue;
            r.LaunchFav = fav.Best == 0;
            r.LaunchUnfav = unfav.Best == 0;

            // совершенная информация: запускаем только тогда, когда точно известен успех
            r.EvPerfect = d.PSuccess * Math.Max(0, d.Profit - d.LaunchCost) + (1 - d.PSuccess) * Math.Max(0, -d.Loss - d.LaunchCost);
            r.Evpi = TreeSolver.Clean(r.EvPerfect - r.EvNoResearch);
            r.CriticalP = CriticalProbability(d);
            r.Policy = TreeSolver.Policy(root);
            return r;
        }

        /// <summary>
        /// Точка безразличия «запускать / не запускать» без исследования:
        /// p·Π − (1 − p)·L − C = 0  ⇒  p* = (C + L) / (Π + L).
        /// </summary>
        public static double CriticalProbability(LaunchData d)
        {
            double den = d.Profit + d.Loss;
            return Math.Abs(den) < 1e-12 ? double.NaN : (d.LaunchCost + d.Loss) / den;
        }

        /// <summary>Ожидаемое значение ветви «Запустить» без исследования как функция p.</summary>
        public static double EvLaunchAt(LaunchData d, double p)
        {
            return p * d.Profit - (1 - p) * d.Loss - d.LaunchCost;
        }

        /// <summary>Ожидаемое значение ветви «Заказать исследование» (с оптимальными решениями после прогноза) при вероятности p.</summary>
        public static double EvResearchAt(LaunchData d, double p)
        {
            LaunchData c = d.Clone();
            c.PSuccess = p;
            Node root = Build(c);
            TreeSolver.Roll(root);
            return root.Children[1].BranchValue;
        }

        /// <summary>
        /// Ожидаемое значение исследования при политике «запускать только при благоприятном прогнозе» —
        /// линейная функция p: A·p + B. Нужна для аналитического поиска границ области, где исследование выгодно.
        /// </summary>
        public static void FavOnlyLine(LaunchData d, out double a, out double b)
        {
            double q1 = d.PFavGivenSuccess, f1 = 1 - d.PUnfavGivenFailure;   // P(благ | успех), P(благ | неудача)
            // EV = p·q1·Π − (1 − p)·f1·L − (p·q1 + (1 − p)·f1)·C − R
            a = q1 * d.Profit + f1 * d.Loss - (q1 - f1) * d.LaunchCost;
            b = -f1 * d.Loss - f1 * d.LaunchCost - d.ResearchCost;
        }

        /// <summary>Подпись выбранной политики для метода Монте-Карло.</summary>
        public static string MonteCarloTrial(LaunchData d, Random rnd)
        {
            LaunchData c = d.Clone();
            c.PSuccess = Perturb.Binary(c.PSuccess, rnd);
            c.PFavGivenSuccess = Perturb.Binary(c.PFavGivenSuccess, rnd);
            c.PUnfavGivenFailure = Perturb.Binary(c.PUnfavGivenFailure, rnd);
            Node root = Build(c);
            TreeSolver.Roll(root);
            return root.Best == 0 ? "Без исследования: " + root.Children[0].Children[root.Children[0].Best].Branch.ToLowerInvariant()
                                  : "Заказать исследование";
        }
    }

    /// <summary>Случайное изменение вероятностей для проверки устойчивости (±0,1 и нормировка).</summary>
    public static class Perturb
    {
        public const double Delta = 0.1;

        /// <summary>Изменить распределение: каждую вероятность сдвинуть на U(−0,1; 0,1), отсечь отрицательные и нормировать.</summary>
        public static double[] Distribution(double[] p, Random rnd)
        {
            var r = new double[p.Length];
            double sum = 0;
            for (int i = 0; i < p.Length; i++)
            {
                r[i] = Math.Max(0, p[i] + (rnd.NextDouble() * 2 - 1) * Delta);
                sum += r[i];
            }
            if (sum <= 0) return (double[])p.Clone();
            for (int i = 0; i < r.Length; i++) r[i] /= sum;
            return r;
        }

        /// <summary>Два исхода: p и 1 − p изменяются независимо и нормируются.</summary>
        public static double Binary(double p, Random rnd)
        {
            return Distribution(new[] { p, 1 - p }, rnd)[0];
        }
    }
}
