using System;
using System.Collections.Generic;
using System.Linq;

namespace Den3.Core
{
    [Serializable]
    public class StrategyItem
    {
        public string Name = "";
        public double Investment;   // теряется полностью при неудаче
        public double Profit;       // чистая прибыль при успехе
        public double Probability;  // вероятность успеха

        public StrategyItem() { }
        public StrategyItem(string name, double inv, double profit, double p)
        {
            Name = name; Investment = inv; Profit = profit; Probability = p;
        }

        public StrategyItem Clone() { return (StrategyItem)MemberwiseClone(); }

        /// <summary>Ожидаемая полезность одной стратегии при вероятности успеха p.</summary>
        public double EvAt(double p) { return p * Profit - (1 - p) * Investment; }
    }

    /// <summary>Исходные данные задания 1.2.</summary>
    [Serializable]
    public class StrategyData
    {
        public List<StrategyItem> Items = new List<StrategyItem>();
        public double Budget = 200;

        public StrategyData Clone()
        {
            return new StrategyData { Budget = Budget, Items = Items.Select(i => i.Clone()).ToList() };
        }
    }

    /// <summary>Строка результатов: одна стратегия или сочетание стратегий.</summary>
    public sealed class StrategyAlt
    {
        public string Name;
        public int[] Members;
        public double Investment, Ev, Sigma, PLoss, Criterion;
        public bool Feasible;
        public Node Node;
    }

    public sealed class StrategyResult
    {
        public Node Root;
        public List<StrategyAlt> Alternatives = new List<StrategyAlt>();
        public StrategyAlt BestEv, BestCriterion;
        public int Feasible, Total;
    }

    /// <summary>Задание 1.2: выбор стратегии развития, в том числе сочетания стратегий в пределах бюджета.</summary>
    public static class StrategyProblem
    {
        public const int MaxItems = 6;

        public static void Check(StrategyData d)
        {
            if (d.Items.Count == 0) throw new DecisionException("Не задано ни одной стратегии.");
            if (d.Items.Count > MaxItems) throw new DecisionException("Допускается не более " + MaxItems + " стратегий.");
            if (d.Budget < 0) throw new DecisionException("Бюджет не может быть отрицательным.");
            foreach (StrategyItem s in d.Items)
            {
                LaunchProblem.CheckProbability(s.Probability, "Вероятность успеха стратегии «" + s.Name + "»");
                if (s.Investment < 0) throw new DecisionException("Инвестиции стратегии «" + s.Name + "» не могут быть отрицательными.");
            }
        }

        public static string ComboName(StrategyData d, int[] members)
        {
            return string.Join(" + ", members.Select(i => d.Items[i].Name));
        }

        /// <summary>
        /// Дерево: решение «Стратегия?» → допустимые по бюджету варианты (одна стратегия или сочетание) →
        /// случайный узел со всеми сочетаниями исходов (исходы стратегий независимы) → итоговый результат.
        /// </summary>
        public static StrategyResult Solve(StrategyData d)
        {
            Check(d);
            var res = new StrategyResult();
            var root = new DecisionNode("Стратегия?");
            res.Root = root;
            int n = d.Items.Count;

            for (int mask = 1; mask < (1 << n); mask++)
            {
                int[] members = Enumerable.Range(0, n).Where(i => (mask & (1 << i)) != 0).ToArray();
                var alt = new StrategyAlt
                {
                    Members = members,
                    Name = ComboName(d, members),
                    Investment = members.Sum(i => d.Items[i].Investment)
                };
                alt.Feasible = alt.Investment <= d.Budget + 1e-9;
                res.Alternatives.Add(alt);
                if (!alt.Feasible) continue;

                var chance = root.Add(new ChanceNode("Исход", alt.Name));
                int k = members.Length;
                for (int o = 0; o < (1 << k); o++)
                {
                    double p = 1, value = 0;
                    var label = new List<string>();
                    for (int t = 0; t < k; t++)
                    {
                        StrategyItem s = d.Items[members[t]];
                        bool ok = (o & (1 << t)) == 0;
                        p *= ok ? s.Probability : 1 - s.Probability;
                        value += ok ? s.Profit : -s.Investment;
                        label.Add(k == 1 ? (ok ? "Успех" : "Неудача") : Short(s.Name) + (ok ? "+" : "−"));
                    }
                    chance.Add(new OutcomeNode(string.Join(", ", label), value), p);
                }
                alt.Node = chance;
            }
            var nothing = root.Add(new OutcomeNode("Не инвестировать", 0));
            res.Alternatives.Add(new StrategyAlt { Name = "Не инвестировать", Members = new int[0], Feasible = true, Node = nothing });

            TreeSolver.Solve(root);
            foreach (StrategyAlt a in res.Alternatives.Where(x => x.Feasible))
            {
                List<Outcome> dist = TreeSolver.Distribution(a.Node);
                a.Ev = TreeSolver.Mean(dist);
                a.Sigma = TreeSolver.StdDev(dist);
                a.PLoss = TreeSolver.LossProbability(dist);
                a.Criterion = a.Ev - a.Sigma / 2;
            }
            List<StrategyAlt> ok2 = res.Alternatives.Where(x => x.Feasible).ToList();
            res.BestEv = ok2.OrderByDescending(x => x.Ev).First();
            res.BestCriterion = ok2.OrderByDescending(x => x.Criterion).First();
            res.Feasible = ok2.Count;
            res.Total = res.Alternatives.Count;
            return res;
        }

        public static string Short(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Length <= 4 ? name : name.Substring(0, 4) + ".";
        }

        /// <summary>Прямые EV_i(p) = p·(Π_i + I_i) − I_i: точка, где стратегия j сравнивается со стратегией i.</summary>
        public static double CrossCommon(StrategyItem i, StrategyItem j)
        {
            double den = (j.Profit + j.Investment) - (i.Profit + i.Investment);
            return Math.Abs(den) < 1e-12 ? double.NaN : (j.Investment - i.Investment) / den;
        }

        /// <summary>Собственная вероятность стратегии j, при которой её EV сравнивается с EV стратегии i (данные i не меняются).</summary>
        public static double CrossOwn(StrategyItem i, StrategyItem j)
        {
            double den = j.Profit + j.Investment;
            return Math.Abs(den) < 1e-12 ? double.NaN : (i.EvAt(i.Probability) + j.Investment) / den;
        }

        public static string MonteCarloTrial(StrategyData d, Random rnd)
        {
            StrategyData c = d.Clone();
            foreach (StrategyItem s in c.Items) s.Probability = Perturb.Binary(s.Probability, rnd);
            return Solve(c).BestEv.Name;
        }
    }
}
