using System;
using System.Collections.Generic;
using System.Linq;

namespace Den3.Core
{
    /// <summary>Исходные данные задания 1.3 (тыс. руб.).</summary>
    [Serializable]
    public class ProductionData
    {
        public double[] ProdCost = { 20, 40, 60 };      // малое / среднее / большое
        public double[] MktCost = { 10, 20 };           // низкий / высокий бюджет маркетинга
        public double[] Demand = { 0.3, 0.5, 0.2 };     // низкий / средний / высокий спрос
        public double TestCost = 6;
        /// <summary>Выручка: строка = размер·2 + маркетинг, столбец = спрос.</summary>
        public double[][] Revenue =
        {
            new double[] { 40, 50, 55 }, new double[] { 45, 58, 62 },
            new double[] { 35, 75, 95 }, new double[] { 42, 88, 112 },
            new double[] { 30, 85, 150 }, new double[] { 38, 100, 175 }
        };
        /// <summary>Надёжность теста: строка — фактический спрос, столбец — прогноз.</summary>
        public double[][] Reliability =
        {
            new[] { 0.70, 0.20, 0.10 },
            new[] { 0.15, 0.70, 0.15 },
            new[] { 0.10, 0.20, 0.70 }
        };

        public ProductionData Clone()
        {
            return new ProductionData
            {
                ProdCost = (double[])ProdCost.Clone(),
                MktCost = (double[])MktCost.Clone(),
                Demand = (double[])Demand.Clone(),
                TestCost = TestCost,
                Revenue = Revenue.Select(r => (double[])r.Clone()).ToArray(),
                Reliability = Reliability.Select(r => (double[])r.Clone()).ToArray()
            };
        }

        /// <summary>Прибыль = выручка − производство − маркетинг (без теста).</summary>
        public double ProfitOf(int size, int mkt, int demand)
        {
            return Revenue[size * 2 + mkt][demand] - ProdCost[size] - MktCost[mkt];
        }
    }

    public sealed class ForecastPlan
    {
        public string Forecast;
        public double Probability, Ev;
        public double[] Posterior;
        public int Size, Mkt;
    }

    public sealed class ProductionResult
    {
        public Node Root;
        public double[,] EvTable = new double[3, 2];   // ожидаемая прибыль «размер — маркетинг» без теста
        public int BestSize, BestMkt;
        public double EvNoTest, EvTestGross, EvTest, Evsi, Evpi, EvPerfect;
        public bool TestWorth, TestChosen;
        public List<ForecastPlan> Plans = new List<ForecastPlan>();
        public int Leaves, DecisionNodes, ChanceNodes;
        public string Policy;
    }

    /// <summary>Задание 1.3: тест рынка → прогноз → размер производства → маркетинг → спрос.</summary>
    public static class ProductionProblem
    {
        public static readonly string[] Sizes = { "Малое", "Среднее", "Большое" };
        public static readonly string[] Mkts = { "Низкий", "Высокий" };
        public static readonly string[] Levels = { "Низкий", "Средний", "Высокий" };

        public static string AltName(int s, int m) { return Sizes[s] + " / " + Mkts[m].ToLowerInvariant() + " маркетинг"; }

        public static void Check(ProductionData d)
        {
            if (d.ProdCost.Length != 3 || d.MktCost.Length != 2 || d.Demand.Length != 3 || d.Revenue.Length != 6 || d.Reliability.Length != 3)
                throw new DecisionException("Неполные исходные данные задания 1.3.");
            CheckDistribution(d.Demand, "Вероятности спроса");
            for (int i = 0; i < 3; i++) CheckDistribution(d.Reliability[i], "Надёжность теста при спросе «" + Levels[i].ToLowerInvariant() + "»");
            foreach (double[] r in d.Revenue)
                foreach (double v in r)
                    if (double.IsNaN(v) || double.IsInfinity(v)) throw new DecisionException("Выручка должна быть числом.");
        }

        public static void CheckDistribution(double[] p, string what)
        {
            double s = 0;
            foreach (double v in p)
            {
                if (double.IsNaN(v) || v < 0 || v > 1) throw new DecisionException(what + ": каждая вероятность должна быть от 0 до 1.");
                s += v;
            }
            if (Math.Abs(s - 1) > 1e-4) throw new DecisionException(what + ": сумма равна " + TreeSolver.Fmt(s) + ", а должна быть 1.");
        }

        /// <summary>Поддерево «размер → маркетинг → спрос» при заданных вероятностях спроса.</summary>
        private static void AddProduction(DecisionNode sizeNode, ProductionData d, double[] demand)
        {
            for (int s = 0; s < 3; s++)
            {
                var mk = sizeNode.Add(new DecisionNode("Маркетинг?", Sizes[s]) { Flow = -d.ProdCost[s] });
                for (int m = 0; m < 2; m++)
                {
                    var dem = mk.Add(new ChanceNode("Спрос", Mkts[m]) { Flow = -d.MktCost[m], Tag = s + "," + m });
                    for (int k = 0; k < 3; k++)
                        dem.Add(new OutcomeNode(Levels[k], d.Revenue[s * 2 + m][k]), demand[k]);
                }
            }
        }

        public static Node Build(ProductionData d)
        {
            Check(d);
            var root = new DecisionNode("Тест рынка?");
            var noTest = root.Add(new DecisionNode("Размер?", "Без теста") { Tag = "noTest" });
            AddProduction(noTest, d, d.Demand);

            var test = root.Add(new ChanceNode("Прогноз", "Провести тест") { Flow = -d.TestCost, Tag = "test" });
            double[] marginal = Bayes.Marginal(d.Demand, d.Reliability);
            for (int j = 0; j < 3; j++)
            {
                double[] post = Bayes.Posterior(d.Demand, d.Reliability, j);
                var size = test.Add(new DecisionNode("Размер?", "Прогноз «" + Levels[j].ToLowerInvariant() + "»") { Tag = "f" + j }, marginal[j]);
                AddProduction(size, d, post);
            }
            return root;
        }

        public static ProductionResult Solve(ProductionData d)
        {
            Node root = Build(d);
            TreeSolver.Solve(root);
            var r = new ProductionResult { Root = root };
            Node noTest = root.Children[0], test = root.Children[1];
            for (int s = 0; s < 3; s++)
                for (int m = 0; m < 2; m++)
                    r.EvTable[s, m] = noTest.Children[s].Children[m].BranchValue + noTest.Children[s].Flow;
            r.BestSize = noTest.Best;
            r.BestMkt = noTest.Children[noTest.Best].Best;
            r.EvNoTest = noTest.Value;
            r.EvTestGross = test.Value;
            r.EvTest = test.BranchValue;
            r.Evsi = TreeSolver.Clean(r.EvTestGross - r.EvNoTest);
            r.TestWorth = r.Evsi > d.TestCost + 1e-9;
            r.TestChosen = root.Best == 1;
            for (int j = 0; j < 3; j++)
            {
                Node size = test.Children[j];
                int s = size.Best, m = size.Children[s].Best;
                r.Plans.Add(new ForecastPlan
                {
                    Forecast = Levels[j],
                    Probability = size.Probability,
                    Posterior = Bayes.Posterior(d.Demand, d.Reliability, j),
                    Size = s,
                    Mkt = m,
                    Ev = size.Value
                });
            }
            r.EvPerfect = 0;
            for (int k = 0; k < 3; k++)
            {
                double best = double.NegativeInfinity;
                for (int s = 0; s < 3; s++)
                    for (int m = 0; m < 2; m++) best = Math.Max(best, d.ProfitOf(s, m, k));
                r.EvPerfect += d.Demand[k] * best;
            }
            r.Evpi = TreeSolver.Clean(r.EvPerfect - r.EvNoTest);
            r.Leaves = TreeSolver.CountLeaves(root);
            r.DecisionNodes = TreeSolver.CountNodes(root, NodeKind.Decision);
            r.ChanceNodes = TreeSolver.CountNodes(root, NodeKind.Chance);
            r.Policy = TreeSolver.Policy(root);
            return r;
        }

        /// <summary>Ожидаемая прибыль варианта (s, m) без теста при заданных вероятностях спроса.</summary>
        public static double EvAt(ProductionData d, int s, int m, double[] demand)
        {
            double v = 0;
            for (int k = 0; k < 3; k++) v += demand[k] * d.ProfitOf(s, m, k);
            return v;
        }

        /// <summary>Вероятности спроса для анализа чувствительности: высокий = h, средний = 0,5, низкий = 0,5 − h.</summary>
        public static double[] DemandForHigh(double h) { return new[] { 0.5 - h, 0.5, h }; }

        public static string MonteCarloTrial(ProductionData d, Random rnd)
        {
            ProductionData c = d.Clone();
            c.Demand = Perturb.Distribution(c.Demand, rnd);
            for (int i = 0; i < 3; i++) c.Reliability[i] = Perturb.Distribution(c.Reliability[i], rnd);
            Node root = Build(c);
            TreeSolver.Roll(root);
            if (root.Best == 1) return "Провести тест";
            Node noTest = root.Children[0];
            int s = noTest.Best, m = noTest.Children[s].Best;
            return "Без теста: " + AltName(s, m).ToLowerInvariant();
        }
    }
}
