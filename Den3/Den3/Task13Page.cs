using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>Задание 1.3. Последовательные решения: тест рынка → прогноз → размер → маркетинг → спрос.</summary>
    public class Task13Page : FxBox
    {
        private readonly MainForm win;
        private readonly ErrorMarks errors = new ErrorMarks();
        private readonly SourceBar bar;
        private readonly FxNumber[] numProd = { Ui.Money(20, 0), Ui.Money(40, 0), Ui.Money(60, 0) };
        private readonly FxNumber[] numMkt = { Ui.Money(10, 0), Ui.Money(20, 0) };
        private readonly FxNumber[] numDemand = { Ui.Prob(0.25), Ui.Prob(0.5), Ui.Prob(0.25) };
        private readonly FxNumber numTest = Ui.Money(8, 0);
        private readonly FxNumber numRandLo = Ui.Money(30, 0), numRandHi = Ui.Money(150, 0);
        private readonly FxTable gridRevenue = Ui.Table("Размер", "Маркетинг", "Низкий", "Средний", "Высокий");
        private readonly FxTable gridRel = Ui.Table("Факт \\ прогноз", "«низкий»", "«средний»", "«высокий»");
        private readonly FxLabel status = Ui.Status();
        private readonly TreeCanvas canvas = new TreeCanvas { CollapseRejected = true };
        private readonly FxSwitch swCollapse = new FxSwitch { Size = new Size(44, 24) };
        private readonly FxTable gridEv = Ui.Table("Размер / маркетинг", "Прибыль: низкий", "средний", "высокий", "EV без теста");
        private readonly FxTable gridPlan = Ui.Table("Прогноз", "P(прогноза)", "P(спрос | прогноз)", "Решение", "EV");
        private readonly FxText text = new FxText();
        private readonly FxStat stNo = new FxStat("Без теста", 0) { Hero = true };
        private readonly FxStat stTest = new FxStat("С тестом", 1);
        private readonly FxStat stEvsi = new FxStat("EVSI теста", 2);
        private readonly FxStat stEvpi = new FxStat("EVPI", 3);
        private readonly FxStat stLeaves = new FxStat("Исходов", 4);
        private bool loading;

        public ProductionData Data { get; private set; }
        public ProductionResult Result { get; private set; }
        public event Action Changed;
        /// <summary>Переключатель вкладок правой карточки.</summary>
        public FxSegmented Tabs;

        public Task13Page(MainForm win)
        {
            this.win = win;
            Vertical = true;
            Gap = 12;

            bar = new SourceBar(MainForm.DefaultVariant);
            bar.VariantChosen += LoadVariant;
            bar.BtnRandom.Click += delegate { RandomFill(); };
            bar.BtnSave.Click += delegate { if (Data != null) Ui.SaveDialog(win, "Сохранить данные задания 1.3", "zadanie_1_3.xml", Data); };
            bar.BtnOpen.Click += delegate
            {
                string name;
                var d = Ui.OpenDialog<ProductionData>(win, "Открыть данные задания 1.3", out name);
                if (d != null) Fill(d, "файл " + name);
            };

            foreach (FxNumber n in numProd.Concat(numMkt).Concat(numDemand).Concat(new[] { numTest }))
            {
                n.Size = new Size(96, 32);
                n.ValueChanged += delegate { Edited(); };
                n.InputRejected += (s, m) => errors.Set(s, m);
            }
            gridRevenue.Editable = gridRel.Editable = true;
            gridRevenue.FixedRows = gridRel.FixedRows = true;
            gridRevenue.Columns[0].ReadOnly = gridRevenue.Columns[1].ReadOnly = gridRel.Columns[0].ReadOnly = true;
            gridRevenue.RowHeight = gridRel.RowHeight = 25;
            gridRevenue.CellEdited += delegate { Edited(); };
            gridRel.CellEdited += delegate { Edited(); };

            FxBox inputs = FxBox.V(6);
            inputs.Add(Triple("Производство М / С / Б", numProd), 52);
            inputs.Add(Triple("Маркетинг Н / В  ·  тест рынка", new[] { numMkt[0], numMkt[1], numTest }), 52);
            inputs.Add(Triple("Вероятности спроса Н / С / В (сумма = 1)", numDemand), 52);
            inputs.Add(Ui.Caption("Выручка при спросе низком / среднем / высоком, тыс. руб."), 16);
            inputs.Add(gridRevenue, 32 + 6 * 25 + 8);
            inputs.Add(Ui.Caption("Надёжность теста: P(прогноз | фактический спрос)"), 16);
            inputs.Add(gridRel, 32 + 3 * 25 + 8);
            FxBox lim = FxBox.H(8);
            lim.Add(new FxLabel("Случайная выручка", Fonts.Medium(8.5f), Tone.Dim) { Width = 110 }, 110).Fill(numRandLo).Fill(numRandHi);
            inputs.Add(lim, 32);
            inputs.Fill(status);
            var dataCard = Ui.Card("Исходные данные · 1.3", "data", inputs);

            gridEv.Columns[0].Weight = 2f;
            gridEv.Columns[0].Align = StringAlignment.Near;
            gridPlan.Columns[2].Weight = 1.8f;
            gridPlan.Columns[3].Weight = 2f;
            gridPlan.WrapCells = true;

            FxBox resPage = FxBox.V(10);
            resPage.Add(Ui.Tiles(stNo, stTest, stEvsi, stEvpi, stLeaves), 74);
            resPage.Add(gridEv, 32 + 6 * 28 + 6);
            resPage.Fill(gridPlan);

            FxBox treePage = FxBox.V(4);
            FxBox treeTop = FxBox.H(8);
            treeTop.Add(swCollapse, 44, true).Fill(new FxLabel("свернуть отвергнутые ветви (выключите, чтобы увидеть все 72 исхода)", Fonts.Body(8.5f), Tone.Dim));
            swCollapse.SetSilently(true);
            swCollapse.CheckedChanged += delegate { canvas.CollapseRejected = swCollapse.Checked; };
            treePage.Add(treeTop, 24).Fill(canvas);

            var btnPng = new FxButton("PNG…", false, "image") { Size = new Size(92, 28) };
            btnPng.Click += delegate { Ui.SavePng(win, canvas, "derevo_1_3.png"); };
            FxSegmented seg;
            var tabs = Ui.TabCard("Решение", "steps", 310, new[] { "Результаты", "Дерево", "Вывод" },
                new Control[] { resPage, treePage, text }, out seg);
            Tabs = seg;
            var header = FxBox.H(8);
            header.Add(btnPng, 92).Add(seg, 310);
            header.Size = new Size(410, 28);
            tabs.WithHeader(header);

            FxBox body = FxBox.H(12);
            body.Add(dataCard, 420).Fill(tabs);
            Add(bar, 56);
            Fill(body);
        }

        private static FxBox Triple(string caption, FxNumber[] nums)
        {
            FxBox row = FxBox.H(8);
            foreach (FxNumber n in nums) row.Fill(n);
            return Ui.Field(caption, row, 32);
        }

        public void LoadVariant(int number)
        {
            try
            {
                Fill(VariantData.Get(number).Production, number == 0 ? "демонстрационные данные (вариант 0)" : "вариант " + number);
                bar.ShowVariant(number);
            }
            catch (DecisionException ex) { win.Toast("Вариант", ex.Message, Tone.Warn); }
        }

        private void Fill(ProductionData d, string source)
        {
            loading = true;
            try
            {
                if (d.ProdCost == null || d.ProdCost.Length != 3 || d.MktCost == null || d.MktCost.Length != 2 || d.Demand == null || d.Demand.Length != 3 ||
                    d.Revenue == null || d.Revenue.Length != 6 || d.Reliability == null || d.Reliability.Length != 3)
                    throw new DecisionException("В данных задания 1.3 не хватает значений.");
                for (int i = 0; i < 3; i++) numProd[i].Value = (decimal)d.ProdCost[i];
                for (int i = 0; i < 2; i++) numMkt[i].Value = (decimal)d.MktCost[i];
                for (int i = 0; i < 3; i++) numDemand[i].Value = (decimal)d.Demand[i];
                numTest.Value = (decimal)d.TestCost;
                gridRevenue.Clear();
                for (int s = 0; s < 3; s++)
                    for (int m = 0; m < 2; m++)
                    {
                        double[] r = d.Revenue[s * 2 + m];
                        gridRevenue.AddRow(ProductionProblem.Sizes[s], ProductionProblem.Mkts[m], Ui.N(r[0]), Ui.N(r[1]), Ui.N(r[2]));
                    }
                gridRel.Clear();
                for (int i = 0; i < 3; i++)
                    gridRel.AddRow(ProductionProblem.Levels[i], Ui.N(d.Reliability[i][0], "0.###"), Ui.N(d.Reliability[i][1], "0.###"), Ui.N(d.Reliability[i][2], "0.###"));
                bar.Source = source;
            }
            catch (DecisionException ex) { win.Toast("Загрузка данных", ex.Message, Tone.Bad); }
            finally { loading = false; }
            Recalc();
        }

        private void RandomFill()
        {
            var d = new RandomData().Production((double)numRandLo.Value, (double)numRandHi.Value);
            Fill(d, "случайные данные (выручка " + Ui.N((double)numRandLo.Value) + "–" + Ui.N((double)numRandHi.Value) + ")");
        }

        private void Edited()
        {
            if (loading) return;
            bar.Source = "ручной ввод / редактирование";
            Recalc();
        }

        private ProductionData Read(out string error)
        {
            error = null;
            var d = new ProductionData
            {
                ProdCost = numProd.Select(n => (double)n.Value).ToArray(),
                MktCost = numMkt.Select(n => (double)n.Value).ToArray(),
                Demand = numDemand.Select(n => (double)n.Value).ToArray(),
                TestCost = (double)numTest.Value,
                Revenue = new double[6][],
                Reliability = new double[3][]
            };
            // проверка суммы вероятностей спроса — ошибка показывается у каждого из трёх полей
            double sum = d.Demand.Sum();
            string demErr = Math.Abs(sum - 1) > 1e-4 ? "Сумма вероятностей спроса равна " + Ui.P(sum) + ", а должна быть 1." : null;
            foreach (FxNumber n in numDemand) errors.Set(n, demErr);
            if (demErr != null) error = demErr;

            for (int i = 0; i < 6; i++)
            {
                FxRow r = gridRevenue.Rows[i];
                d.Revenue[i] = new double[3];
                for (int k = 0; k < 3; k++)
                {
                    gridRevenue.SetCellError(r, k + 2, null);
                    try { d.Revenue[i][k] = Ui.ParseNumber(r.Cells[k + 2], "Выручка (" + r.Cells[0] + ", " + r.Cells[1].ToLowerInvariant() + ")"); }
                    catch (DecisionException ex) { gridRevenue.SetCellError(r, k + 2, ex.Message); error = error ?? ex.Message; }
                }
            }
            string relErr = null;
            for (int i = 0; i < 3; i++)
            {
                FxRow r = gridRel.Rows[i];
                d.Reliability[i] = new double[3];
                bool ok = true;
                for (int k = 0; k < 3; k++)
                {
                    gridRel.SetCellError(r, k + 1, null);
                    try { d.Reliability[i][k] = Ui.ParseProbability(r.Cells[k + 1], "Надёжность теста"); }
                    catch (DecisionException ex) { ok = false; gridRel.SetCellError(r, k + 1, ex.Message); relErr = relErr ?? ex.Message; }
                }
                double s = d.Reliability[i].Sum();
                if (ok && Math.Abs(s - 1) > 1e-4)
                {
                    string m = "Строка «" + r.Cells[0] + "»: сумма вероятностей прогнозов " + Ui.P(s) + " ≠ 1.";
                    for (int k = 0; k < 3; k++) gridRel.SetCellError(r, k + 1, m);
                    relErr = relErr ?? m;
                }
            }
            errors.Set(gridRel, relErr);
            error = error ?? relErr;
            errors.Set(gridRevenue, gridRevenue.Rows.Any(r => r.CellError != null && r.CellError.Any(e => e != null)) ? "В таблице выручки есть ошибки." : null);
            return d;
        }

        public void Recalc()
        {
            if (gridRevenue.Rows.Count != 6 || gridRel.Rows.Count != 3) return;
            string error;
            ProductionData d = Read(out error);
            try
            {
                if (error != null) throw new DecisionException(error);
                ProductionResult r = ProductionProblem.Solve(d);
                Data = d;
                Result = r;
                Ui.SetStatus(status, null, "Данные проверены: суммы вероятностей спроса и строк надёжности равны 1.");
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

        private void Show(ProductionData d, ProductionResult r)
        {
            canvas.SetTree(r.Root);
            gridEv.Clear();
            for (int s = 0; s < 3; s++)
                for (int m = 0; m < 2; m++)
                {
                    FxRow row = gridEv.AddRow(ProductionProblem.AltName(s, m), Ui.N(d.ProfitOf(s, m, 0)), Ui.N(d.ProfitOf(s, m, 1)),
                        Ui.N(d.ProfitOf(s, m, 2)), Ui.N(r.EvTable[s, m], "0.###"));
                    if (s == r.BestSize && m == r.BestMkt) row.SetTone(RowTone.Good).SetBold();
                }
            gridPlan.Clear();
            foreach (ForecastPlan p in r.Plans)
                gridPlan.AddRow("«" + p.Forecast.ToLowerInvariant() + "»", Ui.N(p.Probability, "0.####"),
                    string.Join(" / ", p.Posterior.Select(v => Ui.N(v, "0.###"))), ProductionProblem.AltName(p.Size, p.Mkt), Ui.N(p.Ev, "0.###"));
            gridPlan.AddRow("Итого с тестом", "", "", "до вычета теста / после", Ui.N(r.EvTestGross, "0.###") + " / " + Ui.N(r.EvTest, "0.###")).SetTone(RowTone.Total);

            stNo.Set(r.EvNoTest, 2, " т.р.");
            stNo.Sub = (ProductionProblem.Sizes[r.BestSize] + " / " + ProductionProblem.Mkts[r.BestMkt]).ToLowerInvariant();
            stTest.Set(r.EvTest, 3, " т.р.");
            stTest.Sub = "до вычета " + Ui.N(r.EvTestGross, "0.###");
            stEvsi.Set(r.Evsi, 3, " т.р.");
            stEvsi.Sub = (r.TestWorth ? "> цены " : "< цены ") + Ui.N(d.TestCost);
            stEvpi.Set(r.Evpi, 2, " т.р.");
            stEvpi.Sub = "идеальный прогноз";
            stLeaves.Set(r.Leaves, 0);
            stLeaves.Sub = r.DecisionNodes + " реш. · " + r.ChanceNodes + " случ.";

            string plans = string.Join("\n", r.Plans.Select(p => "Прогноз «" + p.Forecast.ToLowerInvariant() + "» (P = " + Ui.N(p.Probability, "0.####") +
                "): P(спрос) = " + string.Join(" / ", p.Posterior.Select(v => Ui.N(v, "0.####"))) + " → " +
                ProductionProblem.AltName(p.Size, p.Mkt).ToLowerInvariant() + ", EV = " + Ui.N(p.Ev, "0.###") + "."));
            text.Text =
                "СТРУКТУРА ДЕРЕВА\n" +
                "Тест → прогноз (3) → размер (3) → маркетинг (2) → спрос (3). Без теста 3·2·3 = 18 исходов, с тестом 3·18 = 54, всего " + r.Leaves + " конечных узлов.\n" +
                "БЕЗ ТЕСТА (ОБРАТНАЯ ИНДУКЦИЯ)\n" +
                "EV(размер, маркетинг) = Σ P(спрос)·(выручка − производство − маркетинг). Максимум: " +
                ProductionProblem.AltName(r.BestSize, r.BestMkt).ToLowerInvariant() + ", EV = " + Ui.N(r.EvNoTest, "0.###") + " тыс. руб.\n" +
                "С ТЕСТОМ (ФОРМУЛА БАЙЕСА)\n" +
                "P(прогноз j) = Σ P(спрос i)·P(j | i);  P(спрос i | j) = P(спрос i)·P(j | i) / P(j).\n" + plans + "\n" +
                "EV с тестом = " + Ui.N(r.EvTestGross, "0.###") + " − " + Ui.N(d.TestCost) + " = " + Ui.N(r.EvTest, "0.###") + " тыс. руб.\n" +
                "ЦЕНА ИНФОРМАЦИИ\n" +
                "EVPI = Σ P(спрос)·max прибыли − EV без теста = " + Ui.N(r.EvPerfect, "0.###") + " − " + Ui.N(r.EvNoTest, "0.###") + " = " + Ui.N(r.Evpi, "0.###") + " тыс. руб.\n" +
                "Ценность теста EVSI = " + Ui.N(r.EvTestGross, "0.###") + " − " + Ui.N(r.EvNoTest, "0.###") + " = " + Ui.N(r.Evsi, "0.###") + " тыс. руб. (не больше EVPI).\n" +
                "ВЫВОД\n" +
                (r.TestWorth
                    ? "Тест за " + Ui.N(d.TestCost) + " тыс. руб. стоит проводить: он приносит больше, чем стоит. "
                    : "Тест за " + Ui.N(d.TestCost) + " тыс. руб. проводить не стоит: его ценность " + Ui.N(r.Evsi, "0.###") + " тыс. руб. меньше цены. ") +
                "Оптимальная последовательность: " + r.Policy.ToLowerInvariant() + "; ожидаемая прибыль " + Ui.N(r.Root.Value, "0.###") + " тыс. руб.";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) errors.Dispose();
            base.Dispose(disposing);
        }
    }
}
