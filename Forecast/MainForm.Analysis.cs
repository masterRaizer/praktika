using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Forecast.Core;
using FxKit;

namespace Forecast
{
    /// <summary>Разделы «Сравнение на графике» (1.4), «Анализ точности» (2.1) и «Рекомендации» (2.2).</summary>
    public partial class MainForm
    {
        /// <summary>Цвет каждого метода постоянен во всех разделах: СС, эксп. сглаживание, Хольт, регрессия, комбинированный.</summary>
        private static readonly int[] MethodColor = { CSmooth, CForecast, CAlt, CPurple, CPink };

        // =====================================================================
        //  Задание 1.4 — все методы на одном графике
        // =====================================================================
        private FxSegmented segCmpSeries;
        private FxChip[] chipMethods;
        private FxNumber numCmpHorizon;
        private FxChart chartCmp;
        private FxTable gridCmp;
        private FxLabel lblCmpStatus;

        private Control BuildComparePage()
        {
            segCmpSeries = new FxSegmented("Недели · 1.2, 1.5", "Месяцы · 1.1");
            segCmpSeries.SelectedIndexChanged += delegate { UpdateCompare(); };
            string[] names =
            {
                "Скользящее среднее (k из 1.1)",
                "Эксп. сглаживание (α из 1.2)",
                "Метод Хольта (α, β из 1.5)",
                "Линейная регрессия (тренд)",
                "Комбинированный прогноз"
            };
            chipMethods = new FxChip[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                chipMethods[i] = new FxChip(names[i], MethodColor[i], i < 4);
                chipMethods[i].CheckedChanged += delegate { UpdateCompare(); };
            }
            numCmpHorizon = new FxNumber(1, 12, 4, 0, 1);
            numCmpHorizon.ValueChanged += delegate { UpdateCompare(); };
            var btnPng = new FxButton("Сохранить график в PNG…", false, "download");
            btnPng.Click += BtnSavePng_Click;
            lblCmpStatus = StatusLabel();
            gridCmp = MakeTable("Метод", "MAPE, %", "MAE");
            gridCmp.Columns[0].Weight = 2.4f;
            gridCmp.Columns[0].Align = StringAlignment.Near;
            gridCmp.WrapCells = true;
            gridCmp.Columns[1].NoWrap = gridCmp.Columns[2].NoWrap = true;
            gridCmp.FontSize = 8.5f;

            var side = new FxCard("Что показать", "layers");
            FxBox sb = FxBox.V(6);
            sb.Add(Caption("Ряд"), 16).Add(segCmpSeries, 34);
            sb.Add(Caption("Методы на графике"), 16);
            foreach (FxChip c in chipMethods) sb.Add(c, 30);
            sb.Add(LabeledRow("Горизонт прогноза", numCmpHorizon, 112), 34);
            sb.Add(btnPng, 34);
            sb.Add(lblCmpStatus);
            sb.Add(Caption("Точность на общих точках"), 16);
            sb.Fill(gridCmp);
            side.With(sb);

            chartCmp = new FxChart("Сравнение методов прогнозирования", "Период", "Продажи, тыс. руб.");
            FxBox page = FxBox.H(12);
            page.Add(side, 330).Fill(ChartCard(chartCmp));
            return page;
        }

        /// <summary>Методы с параметрами, заданными в разделах заданий.</summary>
        private List<IForecastMethod> MethodsFromControls()
        {
            var list = new List<IForecastMethod>
            {
                new MovingAverageMethod(MaPeriod),
                new ExponentialSmoothingMethod((double)numAlpha.Value, CurrentInit),
                new HoltMethod(trkHoltA.Value / 100.0, trkHoltB.Value / 100.0),
                new LinearTrendMethod()
            };
            list.Add(new CombinedMethod(list.ToList()));
            return list;
        }

        private void UpdateCompare()
        {
            if (loading || chartCmp == null || trkHoltA == null) return;
            bool ok = false;
            Safe(lblCmpStatus, delegate
            {
                gridCmp.Clear();
                double[] y;
                if (segCmpSeries.SelectedIndex == 0) { Require(weekly, weeklyError, "Ряд по неделям"); y = weekly; }
                else { Require(monthly, monthlyError, "Ряд по месяцам"); y = monthly; }
                int n = y.Length, h = (int)numCmpHorizon.Value;
                List<IForecastMethod> all = MethodsFromControls();

                var chosen = new List<int>();
                for (int i = 0; i < chipMethods.Length; i++) if (chipMethods[i].Checked) chosen.Add(i);
                if (chosen.Count == 0) throw new ForecastException("отметьте хотя бы один метод.");
                double[][] fits = chosen.Select(i => all[i].Fit(y)).ToArray();
                List<int> common = ForecastMetrics.CommonIndices(fits);

                chartCmp.XTitle = segCmpSeries.SelectedIndex == 0 ? "Неделя" : "Месяц";
                chartCmp.Title = "Сравнение методов: одношаговые прогнозы (сплошные) и прогноз на " + h + " шаг. вперёд (пунктир)";
                chartCmp.Begin();
                chartCmp.Line("Факт", Axis(1, n), y, CFact, false, true).Area = true;
                for (int c = 0; c < chosen.Count; c++)
                {
                    IForecastMethod m = all[chosen[c]];
                    int col = MethodColor[chosen[c]];
                    chartCmp.Line(m.Name, Axis(1, n), fits[c], col, false, false).Width = 2f;
                    FxSeries f = chartCmp.Line("прогноз: " + m.Name, Axis(n, h + 1),
                        new[] { y[n - 1] }.Concat(m.Forecast(y, h)).ToArray(), col, true, true);
                    f.HideFirstMarker = true;
                    f.InLegend = false;
                    ErrorStats e = ForecastMetrics.Compute(y, fits[c], common);
                    gridCmp.AddRow(m.Name, Util.F(e.Mape), Util.F(e.Mae));
                }
                chartCmp.End();
                ok = true;
            });
            if (!ok) Fail(chartCmp);
        }

        private void BtnSavePng_Click(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog { Filter = "Изображение PNG|*.png", FileName = "прогноз.png" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    using (Bitmap bmp = chartCmp.Render(1600, 900))
                        bmp.Save(dlg.FileName, ImageFormat.Png);
                    Toast("График сохранён", dlg.FileName, Tone.Good, 4200);
                }
                catch (Exception ex)
                {
                    Toast("PNG", "Не удалось сохранить файл: " + ex.Message, Tone.Bad);
                }
            }
        }

        // =====================================================================
        //  Задание 2.1 — сравнение методов
        // =====================================================================
        private FxSegmented segAnTabs, segAnSeries;
        private FxTable gridAn, gridRecommend;
        private FxChart chartAnBars, chartAnPeriod, chartAnAlpha;
        private FxLabel lblAnStatus;
        private FxText txtAnConclusion;

        private Control BuildAnalysisPage()
        {
            var inner = new FxPager { VerticalSlide = false, Duration = 360 };
            segAnTabs = new FxSegmented("Сводная таблица и диаграмма", "Влияние параметров", "Выводы и рекомендации");
            segAnTabs.SelectedIndexChanged += delegate { inner.ShowPage(segAnTabs.SelectedIndex); };
            segAnSeries = new FxSegmented("Недели · 1.2, 1.5", "Месяцы · 1.1");
            segAnSeries.SelectedIndexChanged += delegate { UpdateAnalysis(); };
            lblAnStatus = StatusLabel();

            var bar = new FxCard { Padding = new Padding(12, 10, 12, 10) };
            FxBox barRow = FxBox.H(12);
            barRow.Fill(segAnTabs, 2.2f);
            barRow.Add(new FxLabel("Ряд:", Fonts.Semi(9f), Tone.Dim) { Width = 34 }, 34);
            barRow.Fill(segAnSeries, 1f);
            bar.With(barRow);

            // сводная таблица
            gridAn = MakeTable("Метод", "Параметры (по всему ряду)", "MAPE, % (обуч.)", "MAE (обуч.)",
                               "Параметры (без 3 посл.)", "Прогноз 3 посл. набл.", "MAPE, % (отлож.)", "MAE (отлож.)");
            gridAn.Columns[0].Weight = 2.3f;
            gridAn.Columns[0].Align = StringAlignment.Near;
            gridAn.Columns[1].Weight = 1.3f;
            gridAn.Columns[4].Weight = 1.3f;
            gridAn.Columns[5].Weight = 1.6f;
            gridAn.FontSize = 8.5f;
            chartAnBars = new FxChart("MAPE методов, %", "", "MAPE, %") { YFromZero = true };
            txtAnConclusion = new FxText();
            FxBox p1Bottom = FxBox.H(12);
            p1Bottom.Fill(ChartCard(chartAnBars), 1.35f).Fill(new FxCard("Вывод", "bulb").With(txtAnConclusion), 1f);
            FxBox p1 = FxBox.V(12);
            p1.Fill(new FxCard("Сводная таблица", "data").With(gridAn), 1f).Fill(p1Bottom, 1.15f);

            // влияние параметров
            chartAnPeriod = new FxChart("Ошибка в зависимости от периода скользящего среднего", "Период k", "MAPE, % / MAE") { YFromZero = true };
            chartAnAlpha = new FxChart("Ошибка в зависимости от коэффициента сглаживания", "α", "MAPE, % / MAE") { YFromZero = true };
            FxBox p2 = FxBox.H(12);
            p2.Fill(ChartCard(chartAnPeriod)).Fill(ChartCard(chartAnAlpha));

            // выводы
            gridRecommend = MakeTable("Характер данных", "Рекомендуемый метод", "Обоснование (числа из расчётов программы)");
            gridRecommend.Columns[0].Weight = 18;
            gridRecommend.Columns[1].Weight = 24;
            gridRecommend.Columns[2].Weight = 58;
            foreach (FxColumn c in gridRecommend.Columns) c.Align = StringAlignment.Near;
            gridRecommend.WrapCells = true;
            var p3 = new FxCard("Какой метод выбирать", "bulb").With(gridRecommend);

            inner.AddPage(p1);
            inner.AddPage(p2);
            inner.AddPage(p3);
            inner.ShowPage(0, false);

            FxBox page = FxBox.V(12);
            page.Add(bar, 56).Add(lblAnStatus).Fill(inner);
            return page;
        }

        private static string Params(IForecastMethod m)
        {
            var ma = m as MovingAverageMethod;
            if (ma != null) return "k = " + ma.Period;
            var ses = m as ExponentialSmoothingMethod;
            if (ses != null) return "α = " + ses.Alpha.ToString("0.00");
            var h = m as HoltMethod;
            if (h != null) return "α = " + h.Alpha.ToString("0.00") + ", β = " + h.Beta.ToString("0.00");
            if (m is LinearTrendMethod) return "y = a + b·t";
            return "среднее 4 методов";
        }

        private void UpdateAnalysis()
        {
            if (loading || gridAn == null) return;
            bool ok = false;
            Safe(lblAnStatus, delegate
            {
                gridAn.Clear(); gridRecommend.Clear();
                double[] y;
                string what;
                if (segAnSeries.SelectedIndex == 0) { Require(weekly, weeklyError, "Ряд по неделям"); y = weekly; what = "недель"; }
                else { Require(monthly, monthlyError, "Ряд по месяцам"); y = monthly; what = "месяцев"; }

                ComparisonResult res = MethodComparer.Compare(y, 3, false);
                MethodScore best = res.BestByInSample(false);
                MethodScore bestHold = res.BestByHoldout(false);
                foreach (MethodScore s in res.Scores)
                {
                    FxRow r = gridAn.AddRow(MethodComparer.DisplayName(s.Key), Params(s.Method), Util.F(s.InSample.Mape), Util.F(s.InSample.Mae),
                        Params(s.HoldoutMethod), string.Join("; ", s.HoldoutForecast.Select(v => Util.F(v, 1))), Util.F(s.Holdout.Mape), Util.F(s.Holdout.Mae));
                    if (s == best) r.Tone = RowTone.Good;
                }

                string[] labels = res.Scores.Select(s => MethodComparer.DisplayName(s.Key)).ToArray();
                chartAnBars.Begin();
                chartAnBars.Columns("MAPE на обучающей части, %", labels, res.Scores.Select(s => s.InSample.Mape).ToArray(), CFact);
                chartAnBars.Columns("MAPE на отложенных данных, %", labels, res.Scores.Select(s => s.Holdout.Mape).ToArray(), CForecast);
                chartAnBars.End();

                int from = res.CommonIndices.First() + 1;
                txtAnConclusion.Text =
                    "Как считали: ошибки на обучающей части посчитаны на одних и тех же точках (" + what + " " + from + "–" + y.Length + "), параметры подобраны по ряду.\n" +
                    "Наиболее точный метод: " + MethodComparer.DisplayName(best.Key) + " (MAPE " + Util.F(best.InSample.Mape) + " %).\n" +
                    "Отложенные данные: последние 3 наблюдения, параметры подобраны без них; лучший — " +
                    MethodComparer.DisplayName(bestHold.Key) + " (MAPE " + Util.F(bestHold.Holdout.Mape) + " %)" +
                    (best.Key == bestHold.Key ? " — вывод подтверждается." :
                     "; у метода «" + MethodComparer.DisplayName(best.Key) + "» на отложенных данных MAPE " + Util.F(best.Holdout.Mape) + " %, вывод подтверждается частично (3 точки — малая выборка).");

                // влияние периода скользящего среднего
                var ps = MethodComparer.MaPeriodInfluence(y, 2, 6);
                double[] px = ps.Select(s => (double)s.Period).ToArray();
                chartAnPeriod.Begin();
                chartAnPeriod.Line("MAPE, % (общие точки)", px, ps.Select(s => s.Common.Mape).ToArray(), CForecast, false, true);
                chartAnPeriod.Line("MAE (общие точки)", px, ps.Select(s => s.Common.Mae).ToArray(), CFact, false, true);
                chartAnPeriod.End();

                double[] al, mape, mae, mse;
                MethodComparer.SesInfluence(y, out al, out mape, out mae, out mse);
                chartAnAlpha.Begin();
                chartAnAlpha.Line("MAPE, %", al, mape, CForecast, false, false);
                chartAnAlpha.Line("MAE", al, mae, CFact, false, false);
                chartAnAlpha.XMin = 0;
                chartAnAlpha.XMax = 1;
                chartAnAlpha.End();

                FillRecommendations();
                ok = true;
            });
            if (!ok) Fail(chartAnBars, chartAnPeriod, chartAnAlpha, txtAnConclusion);
        }

        /// <summary>Таблица «характер данных — метод — обоснование» с числами из расчётов.</summary>
        private void FillRecommendations()
        {
            var gen = new SeriesGenerator(2024);
            double[] flat = gen.Generate(SeriesKind.NoTrend, 24, 100, 0, 6);
            double[] seasonal = gen.Generate(SeriesKind.Seasonal, 24, 100, 1, 4);
            double[] trendSeries = weekly ?? gen.Generate(SeriesKind.Trend, 24, 60, 3, 4);

            AddRecommendation("Без тренда (колебания вокруг постоянного уровня)",
                "Экспоненциальное сглаживание или скользящее среднее",
                MethodComparer.Compare(flat, 3, false), "сгенерированный ряд без тренда, 24 точки, уровень 100, σ = 6");
            AddRecommendation("С трендом",
                "Метод Хольта или линейная регрессия по времени",
                MethodComparer.Compare(trendSeries, 3, false), weekly != null ? "ряд по неделям текущего варианта" : "сгенерированный ряд с трендом");
            AddRecommendation("С колебаниями (сезонность)",
                "Скользящее среднее с периодом, равным длине сезона, или сезонные модели (Хольта — Уинтерса, регрессия с сезонными фиктивными переменными)",
                MethodComparer.Compare(seasonal, 3, false), "сгенерированный сезонный ряд, сезон 4 периода, амплитуда 15 % уровня");
        }

        private void AddRecommendation(string kind, string method, ComparisonResult r, string data)
        {
            MethodScore b = r.BestByInSample(false);
            string nums = string.Join("; ", r.Scores.Select(s => MethodComparer.DisplayName(s.Key) + " — " + Util.F(s.InSample.Mape) + " % / " + Util.F(s.Holdout.Mape) + " %"));
            string text = "Данные: " + data + ". MAPE на обучающей части / на отложенных данных: " + nums +
                          ". Лучший по расчёту: " + MethodComparer.DisplayName(b.Key) + " (" + Params(b.Method) + ").";
            gridRecommend.AddRow(kind, method, text);
        }

        // =====================================================================
        //  Задание 2.2 — практические рекомендации
        // =====================================================================
        private FxSegmented segPrTabs, segGenKind, segProblem;
        private FxNumber numGenLen, numGenLevel, numGenGrowth, numGenNoise;
        private FxTable gridGen;
        private FxChart chartGen, chartProblem;
        private FxText txtGenResult, txtProblem;
        private FxLabel lblGenStatus;
        private double[] genSeries;

        private Control BuildPracticePage()
        {
            var inner = new FxPager { VerticalSlide = false, Duration = 360 };
            segPrTabs = new FxSegmented("Генерация ряда и лучший метод", "Проблемы методов", "Внешние факторы и точность");
            segPrTabs.SelectedIndexChanged += delegate { inner.ShowPage(segPrTabs.SelectedIndex); };
            var bar = new FxCard { Padding = new Padding(12, 10, 12, 10) }.With(segPrTabs);

            // генерация ряда и выбор лучшего метода
            segGenKind = new FxSegmented("Без тренда", "С трендом", "Сезонный");
            segGenKind.SelectedIndex = 1;
            numGenLen = new FxNumber(12, 200, 24, 0, 1);
            numGenLevel = new FxNumber(0, 100000, 100, 1, 5);
            numGenGrowth = new FxNumber(-1000, 1000, 3, 2, 0.5m);
            numGenNoise = new FxNumber(0, 10000, 5, 1, 0.5m);
            var btnGen = new FxButton("Сгенерировать ряд", true, "dice");
            btnGen.Click += delegate
            {
                GenerateAndEvaluate(new SeriesGenerator());
                chartGen.Replay();
                EnterAnim.Prepare(gridGen);
                EnterAnim.Play(gridGen, 200);
            };
            lblGenStatus = StatusLabel();
            txtGenResult = new FxText();

            var side = new FxCard("Параметры ряда", "dice");
            FxBox sb = FxBox.V(8);
            sb.Add(Caption("Тип ряда"), 16).Add(segGenKind, 34);
            FxBox row1 = FxBox.H(8);
            row1.Fill(Field("Длина", numGenLen)).Fill(Field("Начальный уровень", numGenLevel));
            FxBox row2 = FxBox.H(8);
            row2.Fill(Field("Прирост за период", numGenGrowth)).Fill(Field("Уровень шума σ", numGenNoise));
            sb.Add(row1, 52).Add(row2, 52).Add(btnGen, 36).Add(lblGenStatus).Fill(txtGenResult);
            side.With(sb);

            gridGen = MakeTable("Метод", "Параметры", "MAPE, % (обуч.)", "MAE (обуч.)", "MAPE, % (отлож.)", "MAE (отлож.)");
            gridGen.Columns[0].Weight = 2.3f;
            gridGen.Columns[0].Align = StringAlignment.Near;
            gridGen.Columns[1].Weight = 1.4f;
            gridGen.FontSize = 8.5f;
            chartGen = new FxChart("Сгенерированный ряд и прогнозы", "Период", "Значение");
            FxBox genMain = FxBox.V(12);
            genMain.Fill(ChartCard(chartGen), 1.35f).Fill(new FxCard("Точность методов на этом ряде", "bars").With(gridGen), 1f);
            FxBox p1 = FxBox.H(12);
            p1.Add(side, 330).Fill(genMain);

            // проблемы методов
            segProblem = new FxSegmented("Скользящее среднее", "Экспоненциальное сглаживание", "Метод Хольта", "Линейная регрессия");
            segProblem.SelectedIndexChanged += delegate { UpdateProblem(); };
            chartProblem = new FxChart("", "Период", "Значение");
            txtProblem = new FxText();
            FxBox p2 = FxBox.V(12);
            p2.Add(new FxCard { Padding = new Padding(12, 10, 12, 10) }.With(segProblem), 56);
            p2.Fill(ChartCard(chartProblem));
            p2.Add(new FxCard("В чём проблема", "warn").With(txtProblem), 132);

            // внешние факторы
            var txt = new FxText { FontSize = 10f, KeyHighlight = false };
            txt.Text = ExternalFactorsText();
            var p3 = new FxCard { Padding = new Padding(22, 18, 16, 14) }.With(txt);

            inner.AddPage(p1);
            inner.AddPage(p2);
            inner.AddPage(p3);
            inner.ShowPage(0, false);

            FxBox page = FxBox.V(12);
            page.Add(bar, 56).Fill(inner);
            return page;
        }

        private void GenerateAndEvaluate(SeriesGenerator gen)
        {
            bool ok = false;
            Safe(lblGenStatus, delegate
            {
                gridGen.Clear();
                var kind = (SeriesKind)segGenKind.SelectedIndex;
                genSeries = gen.Generate(kind, (int)numGenLen.Value, (double)numGenLevel.Value, (double)numGenGrowth.Value, (double)numGenNoise.Value);
                double[] y = genSeries;
                int n = y.Length;
                ComparisonResult r = MethodComparer.Compare(y, 3, true);
                MethodScore best = r.BestByInSample(false);
                MethodScore comb = r.Get("COMB");
                foreach (MethodScore s in r.Scores)
                {
                    FxRow row = gridGen.AddRow(MethodComparer.DisplayName(s.Key), Params(s.Method), Util.F(s.InSample.Mape), Util.F(s.InSample.Mae),
                        Util.F(s.Holdout.Mape), Util.F(s.Holdout.Mae));
                    if (s == best) row.Tone = RowTone.Good;
                    if (s == comb) row.Bold = true;
                }

                double[] x = Axis(1, n);
                chartGen.Begin();
                chartGen.Line("Ряд (" + segGenKind.SelectedText.ToLower() + ")", x, y, CFact, false, true).Area = true;
                chartGen.Line("Лучший: " + best.Method.Name, x, best.Fit, CSmooth, false, false);
                chartGen.Line("Комбинированный", x, comb.Fit, CPurple, false, false);
                chartGen.Line("Прогноз лучшего метода на 3 посл. точки (без них)", Axis(n - 2, 3), best.HoldoutForecast, CForecast, true, true);
                chartGen.Line("Комбинированный прогноз на 3 посл. точки", Axis(n - 2, 3), comb.HoldoutForecast, CAlt, true, true);
                chartGen.End();

                bool combBetterIn = comb.InSample.Mape < best.InSample.Mape;
                bool combBetterOut = comb.Holdout.Mape < best.Holdout.Mape;
                txtGenResult.Text =
                    "Лучший метод: для этого ряда (минимум MAPE на обучающей части) — " + MethodComparer.DisplayName(best.Key) +
                    ", MAPE " + Util.F(best.InSample.Mape) + " %; на отложенных данных " + Util.F(best.Holdout.Mape) + " %.\n" +
                    "Комбинированный прогноз: MAPE " + Util.F(comb.InSample.Mape) + " % (обуч.), " + Util.F(comb.Holdout.Mape) + " % (отлож.) — " +
                    (combBetterIn ? "точнее" : "не точнее") + " лучшего метода на обучающей части и " +
                    (combBetterOut ? "точнее" : "не точнее") + " на отложенных данных.";
                ok = true;
            });
            if (!ok) Fail(chartGen, txtGenResult);
        }

        private void UpdateProblem()
        {
            Safe(lblGenStatus, delegate
            {
                string key = new[] { "MA", "SES", "HOLT", "TREND" }[Math.Max(0, segProblem.SelectedIndex)];
                ProblemExample ex = ProblemExamples.Get(key);
                int n = ex.History.Length, h = ex.Continuation.Length;
                double[] fit = ex.Method.Fit(ex.History);
                double[] fc = ex.Method.Forecast(ex.History, h);
                ErrorStats inS = ForecastMetrics.Compute(ex.History, fit);
                ErrorStats outS = ForecastMetrics.Compute(ex.Continuation, fc);
                chartProblem.Title = ex.Title;
                chartProblem.Begin();
                chartProblem.Line("Ряд", Axis(1, n), ex.History, CFact, false, true).Area = true;
                chartProblem.Line("Фактическое продолжение", Axis(n + 1, h), ex.Continuation, CFact, true, true);
                chartProblem.Line(ex.Method.Name + ": одношаговый прогноз", Axis(1, n), fit, CSmooth, false, false);
                chartProblem.Line(ex.Method.Name + ": прогноз вперёд", Axis(n + 1, h), fc, CForecast, true, true);
                chartProblem.End();
                txtProblem.Text = ex.Explanation + "\nОшибки: MAPE на ряде " + Util.F(inS.Mape) + " %; MAPE прогноза на " + h + " шага вперёд " + Util.F(outS.Mape) + " %.";
            });
        }

        private static string ExternalFactorsText()
        {
            return string.Join("\n", new[]
            {
                "КАК ПОВЫСИТЬ ТОЧНОСТЬ ПРОГНОЗА",
                "1. Подбирать параметры по данным (период k, коэффициенты α и β) по минимуму ошибки и проверять на отложенных наблюдениях, а не на тех же точках, по которым подбирали.",
                "2. Выбирать метод под характер ряда: без тренда — сглаживание, с трендом — Хольт или регрессия, с сезонностью — сезонные модели.",
                "3. Комбинировать прогнозы нескольких методов (реализовано на вкладке «Генерация ряда»): ошибки разных методов частично компенсируют друг друга, поэтому средний прогноз устойчивее, хотя не всегда точнее лучшего метода.",
                "4. Очищать данные от выбросов и разовых событий, удлинять историю, регулярно обновлять модель по мере поступления новых данных.",
                "",
                "УЧЁТ ВНЕШНИХ ФАКТОРОВ",
                "• Сезонность: сезонные индексы (отношение среднего значения периода сезона к общему среднему; ряд делят на индекс, прогнозируют, затем умножают обратно), метод Хольта — Уинтерса, фиктивные переменные для месяцев или кварталов.",
                "• Акции и скидки: фиктивная переменная D = 1 в периоды акции и 0 в остальные; коэффициент при ней показывает средний прирост продаж во время акции. Для прогноза на будущее D задаётся по плану акций.",
                "• Цены конкурентов: количественная переменная (например, отношение нашей цены к цене конкурента); её прогноз берётся из мониторинга рынка.",
                "",
                "КАК ДОБАВИТЬ ФАКТОР В РЕГРЕССИОННУЮ МОДЕЛЬ",
                "Парная модель y = a + b·x превращается в множественную: y = a + b1·x + b2·D + b3·P, где x — расходы на рекламу, D — признак акции (0/1), P — относительная цена конкурента.",
                "Коэффициенты находятся тем же методом наименьших квадратов, только вместо двух уравнений решается система нормальных уравнений (X'X)·b = X'y.",
                "Значимость каждого коэффициента проверяется по t-критерию, качество модели — по скорректированному R²; фактор оставляют, если он значим и уменьшает ошибку на отложенных данных.",
                "Для сезонности по месяцам добавляют 11 фиктивных переменных (12-й месяц — базовый), чтобы не возникла полная мультиколлинеарность."
            });
        }
    }
}
