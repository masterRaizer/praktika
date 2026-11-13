using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Den3.Core
{
    /// <summary>Ошибка в исходных данных или в структуре дерева.</summary>
    public class DecisionException : Exception
    {
        public DecisionException(string message) : base(message) { }
    }

    public enum NodeKind { Decision, Chance, Outcome }

    /// <summary>
    /// Узел дерева решений. Общая часть для всех трёх видов узлов:
    /// подпись ветви, ведущей в узел, денежный поток на этой ветви и вероятность (для ветвей случайного узла).
    /// </summary>
    public abstract class Node
    {
        /// <summary>Подпись ветви, ведущей в этот узел («Запустить», «Успех», «Прогноз: высокий»).</summary>
        public string Branch = "";
        /// <summary>Подпись самого узла («Запуск?», «Спрос»).</summary>
        public string Name = "";
        /// <summary>Вероятность ветви (только для потомков случайного узла), иначе NaN.</summary>
        public double Probability = double.NaN;
        /// <summary>Денежный поток на входящей ветви (затраты со знаком минус), тыс. руб.</summary>
        public double Flow;

        public Node Parent;
        public readonly List<Node> Children = new List<Node>();

        /// <summary>Ожидаемое значение узла после свёртки (без потока входящей ветви).</summary>
        public double Value;
        /// <summary>Узел лежит на оптимальном пути от корня.</summary>
        public bool Optimal;
        /// <summary>Для узла решения — номер выбранной ветви.</summary>
        public int Best = -1;
        /// <summary>Произвольная метка (используется задачами, чтобы найти нужный узел).</summary>
        public string Tag;

        public abstract NodeKind Kind { get; }

        /// <summary>Значение ветви, ведущей в узел: поток плюс значение узла.</summary>
        public double BranchValue { get { return Flow + Value; } }

        public T Add<T>(T child) where T : Node
        {
            child.Parent = this;
            Children.Add(child);
            return child;
        }

        public int Depth { get { int d = 0; for (Node n = Parent; n != null; n = n.Parent) d++; return d; } }

        /// <summary>Накопленный денежный поток от корня до узла включительно.</summary>
        public double Accumulated { get { double s = 0; for (Node n = this; n != null; n = n.Parent) s += n.Flow; return s; } }

        /// <summary>Путь от корня (для сохранения состояния раскрытия в списке).</summary>
        public string Path
        {
            get
            {
                var parts = new List<string>();
                for (Node n = this; n != null; n = n.Parent) parts.Add(n.Branch + "|" + n.Name);
                parts.Reverse();
                return string.Join("/", parts);
            }
        }

        public IEnumerable<Node> All()
        {
            yield return this;
            foreach (Node c in Children)
                foreach (Node d in c.All()) yield return d;
        }

        public override string ToString() { return Kind + ": " + (Branch.Length > 0 ? Branch : Name); }
    }

    /// <summary>Узел решения (квадрат): выбирается ветвь с наибольшим ожидаемым значением.</summary>
    public sealed class DecisionNode : Node
    {
        public DecisionNode(string name, string branch = "") { Name = name; Branch = branch; }
        public override NodeKind Kind { get { return NodeKind.Decision; } }
    }

    /// <summary>Случайный узел (круг): ожидаемое значение — сумма значений ветвей, взвешенных вероятностями.</summary>
    public sealed class ChanceNode : Node
    {
        public ChanceNode(string name, string branch = "") { Name = name; Branch = branch; }
        public override NodeKind Kind { get { return NodeKind.Chance; } }

        /// <summary>Добавить ветвь с вероятностью.</summary>
        public T Add<T>(T child, double probability) where T : Node
        {
            child.Probability = probability;
            return Add(child);
        }
    }

    /// <summary>Исход (треугольник): конечный результат.</summary>
    public sealed class OutcomeNode : Node
    {
        public double Payoff;
        public OutcomeNode(string branch, double payoff) { Branch = branch; Payoff = payoff; }
        public override NodeKind Kind { get { return NodeKind.Outcome; } }
    }

    /// <summary>Распределение результата при выбранной стратегии.</summary>
    public struct Outcome
    {
        public double Value, Probability;
        public Outcome(double v, double p) { Value = v; Probability = p; }
    }

    /// <summary>
    /// Свёртка дерева решений методом обратной индукции.
    /// Один рекурсивный алгоритм подходит для дерева любой глубины и любого сочетания узлов.
    /// </summary>
    public static class TreeSolver
    {
        public const double Eps = 1e-6;

        /// <summary>Проверка и свёртка; возвращает ожидаемое значение корня (с потоком корневой ветви).</summary>
        public static double Solve(Node root)
        {
            Validate(root);
            Roll(root);
            foreach (Node n in root.All()) n.Optimal = false;
            Mark(root);
            return root.BranchValue;
        }

        /// <summary>Рекурсивная свёртка: значение узла вычисляется по значениям потомков.</summary>
        public static double Roll(Node node)
        {
            switch (node.Kind)
            {
                case NodeKind.Outcome:
                    node.Value = ((OutcomeNode)node).Payoff;
                    break;
                case NodeKind.Chance:
                    double sum = 0;
                    foreach (Node c in node.Children)
                    {
                        Roll(c);
                        sum += c.Probability * c.BranchValue;
                    }
                    node.Value = sum;
                    break;
                default:
                    node.Best = -1;
                    double best = double.NegativeInfinity;
                    for (int i = 0; i < node.Children.Count; i++)
                    {
                        Node c = node.Children[i];
                        Roll(c);
                        if (c.BranchValue > best + 1e-9) { best = c.BranchValue; node.Best = i; }
                    }
                    node.Value = best;
                    break;
            }
            return node.Value;
        }

        /// <summary>Отметка оптимального пути: в узле решения — выбранная ветвь, в случайном — все ветви.</summary>
        private static void Mark(Node node)
        {
            node.Optimal = true;
            if (node.Kind == NodeKind.Decision)
            {
                if (node.Best >= 0) Mark(node.Children[node.Best]);
            }
            else
            {
                foreach (Node c in node.Children) Mark(c);
            }
        }

        /// <summary>
        /// Проверка структуры и данных: вероятности от 0 до 1, сумма вероятностей ветвей
        /// каждого случайного узла равна 1, все денежные значения — конечные числа.
        /// </summary>
        public static void Validate(Node root)
        {
            List<string> errors = Check(root);
            if (errors.Count > 0) throw new DecisionException(errors[0]);
        }

        public static List<string> Check(Node root)
        {
            var errors = new List<string>();
            foreach (Node n in root.All())
            {
                if (double.IsNaN(n.Flow) || double.IsInfinity(n.Flow))
                    errors.Add("Ветвь «" + n.Branch + "»: денежное значение не является числом.");
                if (n.Kind == NodeKind.Outcome)
                {
                    double v = ((OutcomeNode)n).Payoff;
                    if (double.IsNaN(v) || double.IsInfinity(v)) errors.Add("Исход «" + n.Branch + "»: результат не является числом.");
                    continue;
                }
                if (n.Children.Count == 0)
                {
                    errors.Add("Узел «" + n.Name + "» не имеет ветвей.");
                    continue;
                }
                if (n.Kind == NodeKind.Chance)
                {
                    double sum = 0;
                    foreach (Node c in n.Children)
                    {
                        if (double.IsNaN(c.Probability) || c.Probability < -Eps || c.Probability > 1 + Eps)
                            errors.Add("Узел «" + n.Name + "», ветвь «" + c.Branch + "»: вероятность " + Fmt(c.Probability) + " вне отрезка [0; 1].");
                        else sum += c.Probability;
                    }
                    if (Math.Abs(sum - 1) > 1e-4)
                        errors.Add("Узел «" + n.Name + "»: сумма вероятностей ветвей равна " + Fmt(sum) + ", а должна быть 1.");
                }
            }
            return errors;
        }

        /// <summary>Число конечных узлов (исходов).</summary>
        public static int CountLeaves(Node root)
        {
            return root.All().Count(n => n.Kind == NodeKind.Outcome);
        }

        public static int CountNodes(Node root, NodeKind kind)
        {
            return root.All().Count(n => n.Kind == kind);
        }

        /// <summary>
        /// Распределение итогового результата (накопленные потоки + исход), если дальше узла
        /// все решения принимаются оптимально. Используется для риска: σ и вероятности убытка.
        /// </summary>
        public static List<Outcome> Distribution(Node start)
        {
            var list = new List<Outcome>();
            Collect(start, start.Flow, 1.0, list);
            return list;
        }

        private static void Collect(Node n, double acc, double p, List<Outcome> list)
        {
            if (n.Kind == NodeKind.Outcome) { list.Add(new Outcome(acc + ((OutcomeNode)n).Payoff, p)); return; }
            if (n.Kind == NodeKind.Decision)
            {
                if (n.Best < 0) return;
                Node c = n.Children[n.Best];
                Collect(c, acc + c.Flow, p, list);
                return;
            }
            foreach (Node c in n.Children) Collect(c, acc + c.Flow, p * c.Probability, list);
        }

        public static double Mean(List<Outcome> d) { return d.Sum(o => o.Value * o.Probability); }

        public static double StdDev(List<Outcome> d)
        {
            double m = Mean(d);
            return Math.Sqrt(Math.Max(0, d.Sum(o => o.Probability * (o.Value - m) * (o.Value - m))));
        }

        public static double LossProbability(List<Outcome> d) { return d.Where(o => o.Value < -1e-9).Sum(o => o.Probability); }

        /// <summary>Последовательность оптимальных решений от корня до первого случайного узла.</summary>
        public static string Policy(Node root)
        {
            var parts = new List<string>();
            Node n = root;
            while (n != null && n.Kind == NodeKind.Decision && n.Best >= 0)
            {
                n = n.Children[n.Best];
                parts.Add(n.Branch);
            }
            return string.Join(" → ", parts);
        }

        /// <summary>Убрать погрешность округления около нуля (−2,8·10⁻¹⁴ → 0).</summary>
        public static double Clean(double v) { return Math.Abs(v) < 1e-9 ? 0 : v; }

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
        public static string Fmt(double v) { return double.IsNaN(v) ? "—" : v.ToString("0.####", Ru); }
    }

    /// <summary>Формула Байеса для пересчёта вероятностей после прогноза.</summary>
    public static class Bayes
    {
        /// <summary>
        /// prior[i] — априорная вероятность состояния i; likelihood[i][j] — вероятность прогноза j при состоянии i.
        /// Возвращает полную вероятность каждого прогноза.
        /// </summary>
        public static double[] Marginal(double[] prior, double[][] likelihood)
        {
            int m = likelihood[0].Length;
            var r = new double[m];
            for (int j = 0; j < m; j++)
                for (int i = 0; i < prior.Length; i++) r[j] += prior[i] * likelihood[i][j];
            return r;
        }

        /// <summary>Апостериорные вероятности состояний при прогнозе j.</summary>
        public static double[] Posterior(double[] prior, double[][] likelihood, int j)
        {
            double total = 0;
            var r = new double[prior.Length];
            for (int i = 0; i < prior.Length; i++) { r[i] = prior[i] * likelihood[i][j]; total += r[i]; }
            for (int i = 0; i < prior.Length; i++) r[i] = total > 0 ? r[i] / total : 1.0 / prior.Length;
            return r;
        }
    }
}
