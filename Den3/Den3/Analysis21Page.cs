using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>
    /// Задание 2.1. Анализ чувствительности: графики EV альтернатив от параметра с точками смены решения,
    /// критические значения аналитически и делением отрезка пополам, проверка устойчивости методом Монте-Карло.
    /// Используются текущие данные заданий 1.1–1.3 (после ввода, загрузки или генерации).
    /// </summary>
    public class Analysis21Page : FxBox
    {
        private sealed class Tab
        {
            public FxChart Chart, McChart;
            public FxTable Cross, Mc;
            public FxText Text;
            public FxButton BtnMc;
            public int Seed = 2026;
        }

        private readonly MainForm win;
        private readonly Tab[] tabs = new Tab[3];
        private FxSegmented seg;
        public const int Trials = 1000;

        public Analysis21Page(MainForm win)
        {
            this.win = win;
            Vertical = true;
            Gap = 12;
            var pages = new Control[3];
            string[] params_ = { "вероятность успеха p", "общая вероятность успеха p", "вероятность высокого спроса" };
            for (int i = 0; i < 3; i++)
            {
                int k = i;
                var t = new Tab
                {
                    Chart = new FxChart("", params_[i], "EV, тыс. руб."),
                    McChart = new FxChart("", "", "доля, %") { ShowLegend = false },
                    Cross = Ui.Table("Смена решения", "Аналитически", "Деление пополам", "Итераций", "Расхождение"),
                    Mc = Ui.Table("Оптимальная альтернатива", "Раз", "Доля"),
                    Text = new FxText(),
                    BtnMc = new FxButton("Ещё 1000 испытаний", true, "dice").AutoWidth()
                };
                t.Cross.Columns[0].Weight = 2.2f;
                t.Cross.Columns[0].Align = StringAlignment.Near;
                t.Cross.WrapCells = true;
                t.Mc.Columns[0].Weight = 3f;
                t.Mc.Columns[0].Align = StringAlignment.Near;
                t.BtnMc.Click += delegate { tabs[k].Seed++; Update(k); };
                tabs[i] = t;

                FxBox left = FxBox.V(10);
                left.Fill(new FxCard { Padding = new Padding(12, 10, 10, 8) }.With(t.Chart), 1.5f);
                left.Fill(Ui.Card("Критические значения", "target", t.Cross), 1f);
                FxBox mc = FxBox.V(8);
                mc.Add(t.Mc, 120).Fill(t.McChart);
                FxBox right = FxBox.V(10);
                right.Fill(Ui.Card("Монте-Карло: " + Trials + " испытаний, ±0,1", "dice", mc, t.BtnMc), 1.1f);
                right.Fill(Ui.Card("Выводы об устойчивости", "bulb", t.Text), 1f);
                FxBox page = FxBox.H(12);
                page.Fill(left, 1.25f).Fill(right, 1f);
                pages[i] = page;
            }
            var pager = new FxPager { VerticalSlide = false, Duration = 320 };
            foreach (Control p in pages) pager.AddPage(p);
            seg = new FxSegmented("Задание 1.1", "Задание 1.2", "Задание 1.3") { Size = new Size(360, 32) };
            seg.SelectedIndexChanged += delegate { pager.ShowPage(seg.SelectedIndex); };
            pager.ShowPage(0, false);
            var top = new FxCard { Padding = new Padding(14, 8, 14, 8) };
            FxBox row = FxBox.H(12);
            row.Add(seg, 360, true).Fill(new FxLabel("Расчёт по текущим данным заданий; при изменении данных графики и таблицы пересчитываются.",
                Fonts.Body(8.5f), Tone.Mute));
            top.With(row);
            Add(top, 50);
            Fill(pager);
        }

        public void ShowTab(int i) { if (i >= 0 && i < 3) seg.SelectedIndex = i; }

        public void UpdateAll()
        {
            for (int i = 0; i < 3; i++) Update(i);
        }

        public void Update(int i)
        {
            Tab t = tabs[i];
            try
            {
                if (i == 0) Update11(t);
                else if (i == 1) Update12(t);
                else Update13(t);
            }
            catch (Exception ex)
            {
                t.Chart.ShowMessage("Нет корректных данных: " + ex.Message);
                t.Cross.Clear();
                t.Mc.Clear();
                t.McChart.ShowMessage("");
                t.Text.Text = "Исправьте исходные данные задания " + (i == 0 ? "1.1" : i == 1 ? "1.2" : "1.3") + ".";
            }
        }

        private static void AddCross(FxTable table, CrossPoint c)
        {
            table.AddRow(c.Title, Ui.N(c.Analytic, "0.######"), Ui.N(c.Numeric, "0.######"), c.Iterations.ToString(),
                double.IsNaN(c.Analytic) || double.IsNaN(c.Numeric) ? "—" : c.Difference.ToString("0.0E+0", Ui.Ru));
        }

        private static List<MonteCarlo.Item> ShowMc(Tab t, Func<Random, string> trial, string current)
        {
            List<MonteCarlo.Item> items = MonteCarlo.Run(trial, Trials, t.Seed);
            t.Mc.Clear();
            foreach (MonteCarlo.Item it in items)
            {
                FxRow r = t.Mc.AddRow(it.Name, it.Count.ToString(), Ui.N(it.Share * 100, "0.#") + " %");
                if (it.Name == current) r.SetTone(RowTone.Good).SetBold();
            }
            t.McChart.Begin();
            t.McChart.Columns("Доля", items.Select((x, k) => "№" + (k + 1)).ToArray(), items.Select(x => x.Share * 100).ToArray(), 2);
            t.McChart.End();
            return items;
        }

        private static string Stability(List<MonteCarlo.Item> items, string current)
        {
            MonteCarlo.Item cur = items.FirstOrDefault(x => x.Name == current);
            double share = cur == null ? 0 : cur.Share;
            string verdict = share >= 0.9 ? "решение устойчиво" : share >= 0.65 ? "решение умеренно устойчиво" : "решение неустойчиво";
            return "Метод Монте-Карло: текущее оптимальное решение «" + current + "» остаётся лучшим в " + Ui.N(share * 100, "0.#") +
                   " % испытаний — " + verdict + ".";
        }

        private static string Nearest(double current, IEnumerable<double> points, string what)
        {
            var list = points.Where(p => !double.IsNaN(p)).ToList();
            if (list.Count == 0) return "На всём отрезке решение не меняется.";
            double near = list.OrderBy(p => Math.Abs(p - current)).First();
            return "Ближайшая точка смены решения " + Ui.N(near, "0.####") + ", текущее значение " + what + " = " + Ui.N(current, "0.###") +
                   " (запас " + Ui.N(Math.Abs(near - current), "0.###") + ").";
        }

        // ---------- 1.1 ----------

        private void Update11(Tab t)
        {
            LaunchData d = win.Page11.Data;
            if (d == null || win.Page11.Result == null) throw new DecisionException("данные задания 1.1 содержат ошибки");
            double[] xs = Sensitivity.Grid(0, 1, 100);
            t.Chart.Begin();
            t.Chart.Title = "Задание 1.1: ожидаемая полезность решений от вероятности успеха";
            t.Chart.Line("Запуск без исследования", xs, xs.Select(p => LaunchProblem.EvLaunchAt(d, p)).ToArray(), 0, false, false);
            t.Chart.Line("С исследованием", xs, xs.Select(p => LaunchProblem.EvResearchAt(d, p)).ToArray(), 1, false, false);
            t.Chart.Line("Не запускать", xs, xs.Select(p => 0.0).ToArray(), -1, true, false);

            var crosses = new List<CrossPoint>();
            int it;
            var c1 = new CrossPoint { Title = "Запуск ↔ отказ без исследования (p*)", Analytic = LaunchProblem.CriticalProbability(d) };
            c1.Numeric = Sensitivity.Bisect(p => LaunchProblem.EvLaunchAt(d, p), 0, 1, out it);
            c1.Iterations = it;
            crosses.Add(c1);

            // исследование выгодно там, где оно лучше лучшего решения без него; на этой области политика — «запуск при благоприятном прогнозе»
            double a, b;
            LaunchProblem.FavOnlyLine(d, out a, out b);
            var analytic = new List<double>();
            if (Math.Abs(a) > 1e-12) analytic.Add(-b / a);                                           // A·p + B = 0
            double la = d.Profit + d.Loss, lb = -d.Loss - d.LaunchCost;
            if (Math.Abs(a - la) > 1e-12) analytic.Add((lb - b) / (a - la));                         // A·p + B = EV запуска
            analytic = analytic.Where(p => p > 0 && p < 1 && FavOnly(d, p)).OrderBy(p => p).ToList();
            int total;
            Func<double, double> gain = p => LaunchProblem.EvResearchAt(d, p) - Math.Max(0, LaunchProblem.EvLaunchAt(d, p));
            List<double> numeric = Sensitivity.Roots(gain, 0, 1, 200, out total);
            int n = Math.Max(analytic.Count, numeric.Count);
            for (int k = 0; k < n; k++)
            {
                var c = new CrossPoint
                {
                    Title = "Граница выгодности исследования №" + (k + 1),
                    Analytic = k < analytic.Count ? analytic[k] : double.NaN,
                    Numeric = k < numeric.Count ? numeric[k] : double.NaN,
                    Iterations = numeric.Count > 0 ? total / numeric.Count : 0
                };
                crosses.Add(c);
            }
            t.Cross.Clear();
            foreach (CrossPoint c in crosses) AddCross(t.Cross, c);
            MarkPoints(t.Chart, crosses.Select(c => c.Numeric), p => Math.Max(0, LaunchProblem.EvLaunchAt(d, p)), "p");
            t.Chart.End();

            LaunchResult r = win.Page11.Result;
            string current = r.Root.Best == 0 ? "Без исследования: " + r.Root.Children[0].Children[r.Root.Children[0].Best].Branch.ToLowerInvariant() : "Заказать исследование";
            List<MonteCarlo.Item> mc = ShowMc(t, rnd => LaunchProblem.MonteCarloTrial(d, rnd), current);
            string region = numeric.Count >= 2
                ? "Исследование выгодно только при p от " + Ui.N(numeric[0], "0.####") + " до " + Ui.N(numeric[1], "0.####") +
                  ": там прогноз меняет решение о запуске. "
                : numeric.Count == 1 ? "Исследование меняет выгодность при p = " + Ui.N(numeric[0], "0.####") + ". " : "Исследование нигде не окупается. ";
            t.Text.Text =
                "ЗАДАНИЕ 1.1\n" +
                "Запуск без исследования выгоден при p > p* = " + Ui.N(c1.Analytic, "0.####") + ". " + region + "\n" +
                Nearest(d.PSuccess, crosses.Select(c => c.Numeric), "p") + "\n" +
                Stability(mc, current) + "\n" +
                "Аналитические и численные значения совпадают с точностью метода деления пополам (10⁻⁷).";
        }

        private static bool FavOnly(LaunchData d, double p)
        {
            LaunchData c = d.Clone();
            c.PSuccess = p;
            Node root = LaunchProblem.Build(c);
            TreeSolver.Roll(root);
            Node res = root.Children[1];
            return res.Children[0].Best == 0 && res.Children[1].Best == 1;
        }

        private static void MarkPoints(FxChart chart, IEnumerable<double> xs, Func<double, double> y, string name)
        {
            var px = xs.Where(v => !double.IsNaN(v)).ToArray();
            if (px.Length == 0) return;
            FxSeries s = chart.Points("Смена решения", px, px.Select(y).ToArray(), 4, 9);
            for (int i = 0; i < px.Length; i++) chart.Note(s, i, name + " = " + Ui.N(px[i], "0.###"));
        }

        // ---------- 1.2 ----------

        private void Update12(Tab t)
        {
            StrategyData d = win.Page12.Data;
            if (d == null || win.Page12.Result == null) throw new DecisionException("данные задания 1.2 содержат ошибки");
            double[] xs = Sensitivity.Grid(0, 1, 100);
            t.Chart.Begin();
            t.Chart.Title = "Задание 1.2: EV стратегий при общей вероятности успеха p";
            for (int i = 0; i < d.Items.Count; i++)
            {
                StrategyItem s = d.Items[i];
                t.Chart.Line(s.Name, xs, xs.Select(p => s.EvAt(p)).ToArray(), i % 6, false, false);
            }
            var crosses = new List<CrossPoint>();
            for (int i = 0; i < d.Items.Count; i++)
                for (int j = i + 1; j < d.Items.Count; j++)
                {
                    StrategyItem a = d.Items[i], b = d.Items[j];
                    int it;
                    var c = new CrossPoint
                    {
                        Title = b.Name + " ↔ " + a.Name.ToLowerInvariant(),
                        Analytic = StrategyProblem.CrossCommon(a, b),
                        Numeric = Sensitivity.Bisect(p => b.EvAt(p) - a.EvAt(p), 0, 1, out it)
                    };
                    c.Iterations = it;
                    if (c.Analytic < 0 || c.Analytic > 1) c.Analytic = double.NaN;
                    crosses.Add(c);
                    if (i == 0)
                    {
                        var own = new CrossPoint
                        {
                            Title = b.Name + " лучше, чем " + a.Name.ToLowerInvariant() + " (меняется только p " + StrategyProblem.Short(b.Name) + ")",
                            Analytic = StrategyProblem.CrossOwn(a, b)
                        };
                        double target = a.EvAt(a.Probability);
                        own.Numeric = Sensitivity.Bisect(p => b.EvAt(p) - target, 0, 1, out it);
                        own.Iterations = it;
                        if (own.Analytic < 0 || own.Analytic > 1) own.Analytic = double.NaN;
                        crosses.Add(own);
                    }
                }
            t.Cross.Clear();
            foreach (CrossPoint c in crosses) AddCross(t.Cross, c);
            var common = crosses.Where(c => c.Title.Contains("↔")).ToList();
            var px = common.Where(c => !double.IsNaN(c.Numeric)).ToList();
            if (px.Count > 0)
            {
                FxSeries s = t.Chart.Points("Пересечения", px.Select(c => c.Numeric).ToArray(),
                    px.Select(c => d.Items.First(x => c.Title.StartsWith(x.Name)).EvAt(c.Numeric)).ToArray(), 4, 9);
                for (int k = 0; k < px.Count; k++) t.Chart.Note(s, k, "p = " + Ui.N(px[k].Numeric, "0.###"));
            }
            t.Chart.End();

            string current = win.Page12.Result.BestEv.Name;
            List<MonteCarlo.Item> mc = ShowMc(t, rnd => StrategyProblem.MonteCarloTrial(d, rnd), current);
            var ownRows = crosses.Where(c => c.Title.Contains("меняется только")).ToList();
            t.Text.Text =
                "ЗАДАНИЕ 1.2\n" +
                string.Join("\n", common.Select(c => c.Title + ": " + (double.IsNaN(c.Numeric) ? "не пересекаются на [0; 1]" : "p = " + Ui.N(c.Numeric, "0.####")))) + "\n" +
                (ownRows.Count > 0 ? "Если менять только вероятность одной стратегии: " +
                    string.Join("; ", ownRows.Select(c => StrategyProblem.Short(c.Title.Split(' ')[0]) + " — при p > " + Ui.N(c.Numeric, "0.####"))) + ".\n" : "") +
                "Выше точек пересечения рискованные стратегии с большой прибылью выигрывают; ниже — консервативная.\n" +
                Stability(mc, current);
        }

        // ---------- 1.3 ----------

        private void Update13(Tab t)
        {
            ProductionData d = win.Page13.Data;
            if (d == null || win.Page13.Result == null) throw new DecisionException("данные задания 1.3 содержат ошибки");
            double[] xs = Sensitivity.Grid(0, 0.5, 100);
            t.Chart.Begin();
            t.Chart.Title = "Задание 1.3: EV вариантов без теста; спрос высокий = h, средний = 0,5, низкий = 0,5 − h";
            var slope = new double[6];
            var icpt = new double[6];
            for (int s = 0; s < 3; s++)
                for (int m = 0; m < 2; m++)
                {
                    int k = s * 2 + m;
                    int ss = s, mm = m;
                    t.Chart.Line(ProductionProblem.AltName(s, m), xs, xs.Select(h => ProductionProblem.EvAt(d, ss, mm, ProductionProblem.DemandForHigh(h))).ToArray(), k, m == 1, false);
                    // EV(h) = 0,5·Пср + (0,5 − h)·Пн + h·Пв = slope·h + icpt
                    slope[k] = d.ProfitOf(s, m, 2) - d.ProfitOf(s, m, 0);
                    icpt[k] = 0.5 * d.ProfitOf(s, m, 1) + 0.5 * d.ProfitOf(s, m, 0);
                }
            Func<double, int> best = h =>
            {
                int b = 0;
                for (int k = 1; k < 6; k++) if (slope[k] * h + icpt[k] > slope[b] * h + icpt[b] + 1e-9) b = k;
                return b;
            };
            // аналитически: пересечения прямых, лежащие на верхней огибающей
            var analytic = new List<double>();
            for (int a = 0; a < 6; a++)
                for (int b = a + 1; b < 6; b++)
                {
                    if (Math.Abs(slope[a] - slope[b]) < 1e-12) continue;
                    double h = (icpt[b] - icpt[a]) / (slope[a] - slope[b]);
                    if (h <= 0 || h >= 0.5) continue;
                    double v = slope[a] * h + icpt[a], top = Enumerable.Range(0, 6).Max(k => slope[k] * h + icpt[k]);
                    if (v >= top - 1e-7 && best(h - 1e-6) != best(h + 1e-6)) analytic.Add(h);
                }
            analytic = analytic.Distinct().OrderBy(h => h).ToList();
            // численно: смена номера лучшей альтернативы на сетке и уточнение делением пополам
            var crosses = new List<CrossPoint>();
            int steps = 500;
            int prev = best(0);
            for (int i = 1; i <= steps; i++)
            {
                double h0 = 0.5 * (i - 1) / steps, h1 = 0.5 * i / steps;
                int cur = best(h1);
                if (cur == prev) continue;
                int a = prev, b = cur, it;
                double root = Sensitivity.Bisect(h => (slope[b] * h + icpt[b]) - (slope[a] * h + icpt[a]), h0, h1, out it);
                var c = new CrossPoint
                {
                    Title = ProductionProblem.AltName(a / 2, a % 2) + " → " + ProductionProblem.AltName(b / 2, b % 2).ToLowerInvariant(),
                    Numeric = root,
                    Iterations = it
                };
                c.Analytic = analytic.Count > 0 ? analytic.OrderBy(x => Math.Abs(x - root)).First() : double.NaN;
                crosses.Add(c);
                prev = cur;
            }
            t.Cross.Clear();
            foreach (CrossPoint c in crosses) AddCross(t.Cross, c);
            if (crosses.Count == 0) t.Cross.AddRow("Решение не меняется на [0; 0,5]", "—", "—", "—", "—");
            MarkPoints(t.Chart, crosses.Select(c => c.Numeric), h => slope[best(h)] * h + icpt[best(h)], "h");
            t.Chart.End();

            ProductionResult r = win.Page13.Result;
            string current = r.TestChosen ? "Провести тест" : "Без теста: " + ProductionProblem.AltName(r.BestSize, r.BestMkt).ToLowerInvariant();
            List<MonteCarlo.Item> mc = ShowMc(t, rnd => ProductionProblem.MonteCarloTrial(d, rnd), current);
            string seq = string.Join(" → ", new[] { ProductionProblem.AltName(best(0) / 2, best(0) % 2) }
                .Concat(crosses.Select(c => "(h = " + Ui.N(c.Numeric, "0.###") + ") " + c.Title.Split('→').Last().Trim())));
            t.Text.Text =
                "ЗАДАНИЕ 1.3\n" +
                "При росте вероятности высокого спроса от 0 до 0,5 лучший вариант меняется так: " + seq + ".\n" +
                Nearest(d.Demand[2], crosses.Select(c => c.Numeric), "P(высокий)") + "\n" +
                Stability(mc, current) + " Соседние альтернативы близки по EV, и решение о тесте зависит от небольших изменений надёжности прогноза.";
        }
    }
}
