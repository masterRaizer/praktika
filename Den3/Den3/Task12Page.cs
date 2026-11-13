using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>Задание 1.2. Дерево с несколькими решениями: стратегии развития и их сочетания в пределах бюджета.</summary>
    public class Task12Page : FxBox
    {
        private readonly MainForm win;
        private readonly ErrorMarks errors = new ErrorMarks();
        private readonly SourceBar bar;
        private readonly FxTable gridInput = Ui.Table("Стратегия", "Инвестиции", "Прибыль", "P успеха");
        private readonly FxNumber numBudget = Ui.Money(280, 0);
        private readonly FxNumber numRandLo = Ui.Money(50, 0), numRandHi = Ui.Money(250, 0);
        private readonly FxLabel status = Ui.Status();
        private readonly FxTable gridResult = Ui.Table("Вариант", "Инвестиции", "EV", "σ", "P(убытка)", "EV − σ/2");
        private readonly TreeCanvas canvas = new TreeCanvas();
        private readonly FxChart chart = new FxChart("Ожидаемая полезность при общей вероятности успеха p", "вероятность успеха p", "EV, тыс. руб.");
        private readonly FxTable gridCross = Ui.Table("Сравнение", "p (общая)", "p стратегии при прочих равных");
        private readonly FxText text = new FxText();
        private readonly FxStat stEv = new FxStat("Лучшее по EV", 0) { Hero = true };
        private readonly FxStat stCr = new FxStat("Лучшее по EV − σ/2", 1);
        private readonly FxStat stCount = new FxStat("Допустимых вариантов", 2);
        private bool loading;

        public StrategyData Data { get; private set; }
        public StrategyResult Result { get; private set; }
        public event Action Changed;
        /// <summary>Переключатель вкладок правой карточки.</summary>
        public FxSegmented Tabs;

        public Task12Page(MainForm win)
        {
            this.win = win;
            Vertical = true;
            Gap = 12;

            bar = new SourceBar(MainForm.DefaultVariant);
            bar.VariantChosen += LoadVariant;
            bar.BtnRandom.Click += delegate { RandomFill(); };
            bar.BtnSave.Click += delegate { if (Data != null) Ui.SaveDialog(win, "Сохранить данные задания 1.2", "zadanie_1_2.xml", Data); };
            bar.BtnOpen.Click += delegate
            {
                string name;
                var d = Ui.OpenDialog<StrategyData>(win, "Открыть данные задания 1.2", out name);
                if (d != null) Fill(d, "файл " + name);
            };

            gridInput.Editable = true;
            gridInput.Columns[0].Weight = 1.6f;
            gridInput.Columns[0].Align = StringAlignment.Near;
            gridInput.AddRowText = "добавить стратегию";
            gridInput.CellEdited += delegate { Edited(); };
            gridInput.RowsEdited += delegate { Edited(); };
            numBudget.ValueChanged += delegate { if (!loading) { bar.Source = "ручной ввод"; Recalc(); } };
            numBudget.InputRejected += (s, m) => errors.Set(s, m);

            FxBox inputs = FxBox.V(8);
            inputs.Fill(gridInput);
            inputs.Add(Ui.Row("Бюджет компании, тыс. руб.", numBudget));
            inputs.Add(new FxLabel("При неудаче инвестиции теряются полностью; прибыль — чистая; исходы стратегий независимы.",
                Fonts.Body(8.5f), Tone.Mute, true) { Height = 34 }, 34);
            inputs.Add(Ui.Caption("Пределы случайных инвестиций, тыс. руб."), 16);
            FxBox lim = FxBox.H(8);
            lim.Fill(numRandLo).Fill(numRandHi);
            inputs.Add(lim, 32);
            inputs.Add(status, 54);
            var dataCard = Ui.Card("Исходные данные · 1.2", "data", inputs);

            gridResult.Columns[0].Weight = 2.2f;
            gridResult.Columns[0].Align = StringAlignment.Near;
            gridResult.WrapCells = true;
            gridCross.Columns[0].Weight = 1.7f;
            gridCross.WrapCells = true;
            gridCross.Columns[0].Align = StringAlignment.Near;

            FxBox resPage = FxBox.V(10);
            resPage.Add(Ui.Tiles(stEv, stCr, stCount), 74);
            resPage.Fill(gridResult, 1.3f);
            resPage.Fill(text, 1f);

            FxBox sensPage = FxBox.H(12);
            FxBox sensRight = FxBox.V(6);
            sensRight.Add(Ui.Caption("Точки, где стратегия становится лучше"), 16).Fill(gridCross);
            sensPage.Fill(chart, 1.6f).Fill(sensRight, 1f);

            var btnPng = new FxButton("PNG…", false, "image") { Size = new Size(92, 28) };
            btnPng.Click += delegate { Ui.SavePng(win, canvas, "derevo_1_2.png"); };
            FxSegmented seg;
            var tabs = Ui.TabCard("Решение", "branch", 360, new[] { "Таблица результатов", "Дерево", "Чувствительность" },
                new Control[] { resPage, canvas, sensPage }, out seg);
            Tabs = seg;
            var header = FxBox.H(8);
            header.Add(btnPng, 92).Add(seg, 360);
            header.Size = new Size(460, 28);
            tabs.WithHeader(header);

            FxBox body = FxBox.H(12);
            body.Add(dataCard, 400).Fill(tabs);
            Add(bar, 56);
            Fill(body);
        }

        public void LoadVariant(int number)
        {
            try
            {
                Fill(VariantData.Get(number).Strategy, number == 0 ? "демонстрационные данные (вариант 0)" : "вариант " + number);
                bar.ShowVariant(number);
            }
            catch (DecisionException ex) { win.Toast("Вариант", ex.Message, Tone.Warn); }
        }

        private void Fill(StrategyData d, string source)
        {
            loading = true;
            try
            {
                gridInput.Clear();
                foreach (StrategyItem s in d.Items)
                    gridInput.AddRow(s.Name, Ui.N(s.Investment), Ui.N(s.Profit), Ui.N(s.Probability, "0.###"));
                numBudget.Value = (decimal)d.Budget;
            }
            finally { loading = false; }
            bar.Source = source;
            Recalc();
        }

        private void RandomFill()
        {
            int n = Math.Max(3, Math.Min(StrategyProblem.MaxItems, gridInput.Rows.Count));
            var d = new RandomData().Strategy(n, (double)numRandLo.Value, (double)numRandHi.Value);
            Fill(d, "случайные данные (инвестиции " + Ui.N((double)numRandLo.Value) + "–" + Ui.N((double)numRandHi.Value) + ")");
        }

        private void Edited()
        {
            if (loading) return;
            bar.Source = "ручной ввод / редактирование таблицы";
            Recalc();
        }

        /// <summary>Чтение таблицы с проверкой каждой ячейки; ошибочные ячейки подсвечиваются.</summary>
        private StrategyData Read(out string error)
        {
            error = null;
            var d = new StrategyData { Budget = (double)numBudget.Value };
            for (int i = 0; i < gridInput.Rows.Count; i++)
            {
                FxRow r = gridInput.Rows[i];
                for (int c = 0; c < 4; c++) gridInput.SetCellError(r, c, null);
                if (r.Cells.Skip(1).All(string.IsNullOrWhiteSpace) && string.IsNullOrWhiteSpace(r.Cells[0])) continue;
                string name = string.IsNullOrWhiteSpace(r.Cells[0]) ? "Стратегия " + (i + 1) : r.Cells[0].Trim();
                var item = new StrategyItem { Name = name };
                bool ok = true;
                try { item.Investment = Ui.ParseNumber(r.Cells[1], name + ", инвестиции"); if (item.Investment < 0) throw new DecisionException(name + ": инвестиции не могут быть отрицательными."); }
                catch (DecisionException ex) { ok = false; gridInput.SetCellError(r, 1, ex.Message); error = error ?? ex.Message; }
                try { item.Profit = Ui.ParseNumber(r.Cells[2], name + ", прибыль"); }
                catch (DecisionException ex) { ok = false; gridInput.SetCellError(r, 2, ex.Message); error = error ?? ex.Message; }
                try { item.Probability = Ui.ParseProbability(r.Cells[3], name + ", вероятность успеха"); }
                catch (DecisionException ex) { ok = false; gridInput.SetCellError(r, 3, ex.Message); error = error ?? ex.Message; }
                if (ok) d.Items.Add(item);
            }
            if (error == null && d.Items.Count == 0) error = "Не задано ни одной стратегии.";
            if (error == null && d.Items.Count > StrategyProblem.MaxItems) error = "Допускается не более " + StrategyProblem.MaxItems + " стратегий.";
            return d;
        }

        public void Recalc()
        {
            string error;
            StrategyData d = Read(out error);
            errors.Set(gridInput, error);
            try
            {
                if (error != null) throw new DecisionException(error);
                StrategyResult r = StrategyProblem.Solve(d);
                Data = d;
                Result = r;
                Ui.SetStatus(status, null, "Данные проверены: вероятности в [0; 1], у каждого случайного узла сумма равна 1.");
                Show(d, r);
            }
            catch (DecisionException ex)
            {
                Ui.SetStatus(status, ex.Message);
                Result = null;
            }
            catch (Exception ex)
            {
                Ui.SetStatus(status, "Непредвиденная ошибка: " + ex.Message);
                Result = null;
            }
            Action h = Changed;
            if (h != null) h();
        }

        private void Show(StrategyData d, StrategyResult r)
        {
            canvas.SetTree(r.Root);

            gridResult.Clear();
            foreach (StrategyAlt a in r.Alternatives.OrderByDescending(x => x.Feasible).ThenByDescending(x => x.Ev))
            {
                FxRow row = a.Feasible
                    ? gridResult.AddRow(a.Name, Ui.N(a.Investment), Ui.N(a.Ev), Ui.N(a.Sigma), Ui.N(a.PLoss, "0.###"), Ui.N(a.Criterion))
                    : gridResult.AddRow(a.Name, Ui.N(a.Investment), "превышает бюджет", "", "", "");
                if (!a.Feasible) row.SetTone(RowTone.Bad);
                else if (a == r.BestEv) row.SetTone(RowTone.Good).SetBold();
                if (a == r.BestCriterion && a.Feasible) gridResult.SetCellTone(row, 5, RowTone.Accent);
                if (a == r.BestEv) gridResult.SetCellTone(row, 2, RowTone.Good);
            }
            stEv.SetText(r.BestEv.Name);
            stEv.Sub = "EV = " + Ui.N(r.BestEv.Ev) + " тыс. руб.";
            stCr.SetText(r.BestCriterion.Name);
            stCr.Sub = "EV − σ/2 = " + Ui.N(r.BestCriterion.Criterion);
            stCount.Set(r.Feasible, 0);
            stCount.Sub = "из " + r.Total + " (с «не инвестировать»)";

            string expl;
            if (r.BestEv == r.BestCriterion)
                expl = "Оба критерия выбирают одно и то же: «" + r.BestEv.Name + "». Риск (σ = " + Ui.N(r.BestEv.Sigma) +
                       ") велик, но ожидаемая полезность настолько выше остальных, что и после вычета σ/2 вариант остаётся лучшим.";
            else
                expl = "Критерии расходятся: по EV лучше «" + r.BestEv.Name + "» (EV = " + Ui.N(r.BestEv.Ev) + ", σ = " + Ui.N(r.BestEv.Sigma) +
                       "), а с учётом риска — «" + r.BestCriterion.Name + "» (EV = " + Ui.N(r.BestCriterion.Ev) + ", σ = " + Ui.N(r.BestCriterion.Sigma) +
                       "). Критерий EV − σ/2 штрафует разброс результата, поэтому выигрывает вариант с меньшим риском.";
            var singles = r.Alternatives.Where(a => a.Members.Length == 1 && a.Feasible).OrderByDescending(a => a.Ev).ToList();
            text.Text =
                "РАСЧЁТ\n" +
                "EV = p·Π − (1 − p)·I для одной стратегии; для сочетания — сумма по всем исходам с вероятностями-произведениями (исходы независимы).\n" +
                "σ = √Σ p·(x − EV)²; P(убытка) — сумма вероятностей исходов с отрицательным результатом.\n" +
                "Сочетания допустимы, если суммарные инвестиции не превышают бюджет " + Ui.N(d.Budget) + " тыс. руб.\n" +
                "ВЫВОД\n" + expl + "\n" +
                (singles.Count > 0 ? "Из одиночных стратегий по EV лучшая — «" + singles[0].Name + "» (" + Ui.N(singles[0].Ev) + ")." : "");

            ShowSensitivity(d);
        }

        private void ShowSensitivity(StrategyData d)
        {
            double[] xs = Sensitivity.Grid(0, 1, 100);
            chart.Begin();
            var lines = new List<FxSeries>();
            for (int i = 0; i < d.Items.Count; i++)
            {
                StrategyItem s = d.Items[i];
                lines.Add(chart.Line(s.Name, xs, xs.Select(p => s.EvAt(p)).ToArray(), i % 6, false, false));
            }
            gridCross.Clear();
            var px = new List<double>();
            var py = new List<double>();
            var labels = new List<string>();
            if (d.Items.Count >= 2)
            {
                StrategyItem baseItem = d.Items[0];
                for (int j = 1; j < d.Items.Count; j++)
                {
                    StrategyItem s = d.Items[j];
                    double pc = StrategyProblem.CrossCommon(baseItem, s);
                    double po = StrategyProblem.CrossOwn(baseItem, s);
                    gridCross.AddRow(s.Name + " лучше, чем " + baseItem.Name.ToLowerInvariant(),
                        double.IsNaN(pc) || pc < 0 || pc > 1 ? "нет на [0; 1]" : "p > " + Ui.N(pc, "0.####"),
                        double.IsNaN(po) || po > 1 ? "недостижимо" : "p > " + Ui.N(Math.Max(0, po), "0.####"));
                    if (!double.IsNaN(pc) && pc >= 0 && pc <= 1)
                    {
                        px.Add(pc); py.Add(s.EvAt(pc));
                        labels.Add(StrategyProblem.Short(s.Name) + " = " + StrategyProblem.Short(baseItem.Name) + ", p = " + Ui.N(pc, "0.###"));
                    }
                }
                for (int i = 1; i < d.Items.Count; i++)
                    for (int j = i + 1; j < d.Items.Count; j++)
                    {
                        double pc = StrategyProblem.CrossCommon(d.Items[i], d.Items[j]);
                        gridCross.AddRow(d.Items[j].Name + " лучше, чем " + d.Items[i].Name.ToLowerInvariant(),
                            double.IsNaN(pc) || pc < 0 || pc > 1 ? "нет на [0; 1]" : "p > " + Ui.N(pc, "0.####"), "—");
                    }
            }
            if (px.Count > 0)
            {
                FxSeries pts = chart.Points("Точки пересечения", px.ToArray(), py.ToArray(), -1, 9);
                for (int k = 0; k < labels.Count; k++) chart.Note(pts, k, labels[k]);
            }
            chart.End();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) errors.Dispose();
            base.Dispose(disposing);
        }
    }
}
