using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Forecast.Core;
using FxKit;

namespace Forecast
{
    /// <summary>Разделы заданий 1.1, 1.2, 1.3 и 1.5.</summary>
    public partial class MainForm
    {
        /// <summary>Индексы 1..n (номера месяцев/недель) для оси X.</summary>
        private static double[] Axis(int from, int count)
        {
            double[] a = new double[count];
            for (int i = 0; i < count; i++) a[i] = from + i;
            return a;
        }

        /// <summary>Сброс элементов раздела, если расчёт невозможен.</summary>
        private static void Fail(params object[] items)
        {
            foreach (object o in items)
            {
                var chart = o as FxChart;
                if (chart != null) { chart.ShowMessage("Нет данных для расчёта — исправьте ошибку в исходных данных"); continue; }
                var stat = o as FxStat;
                if (stat != null) { stat.SetText("—"); stat.Sub = ""; continue; }
                var table = o as FxTable;
                if (table != null) { table.Clear(); continue; }
                var text = o as FxText;
                if (text != null) { text.Text = ""; continue; }
                var heat = o as FxHeat;
                if (heat != null) heat.Clear();
            }
        }

        // =====================================================================
        //  Задание 1.1 — скользящее среднее
        // =====================================================================
        private FxSegmented segMaPeriod;
        private FxChart chartMa;
        private FxTable gridMa, gridMaPeriods, gridMaHoldout;
        private FxText txtMaInfo;
        private FxLabel lblMaStatus;
        private FxStat stMa1, stMa2, stMa3, stMaMape, stMaBest;

        private int MaPeriod { get { return segMaPeriod.SelectedIndex + 2; } }

        private Control BuildMaPage()
        {
            segMaPeriod = new FxSegmented("2", "3", "4", "5", "6");
            segMaPeriod.SelectedIndex = 1;
            segMaPeriod.SelectedIndexChanged += delegate { UpdateMa(); UpdateCompare(); };
            lblMaStatus = StatusLabel();
            txtMaInfo = new FxText();

            var side = new FxCard("Параметры и результат", "pulse");
            FxBox sb = FxBox.V(8);
            sb.Add(Caption("Период скользящего среднего k"), 16);
            sb.Add(segMaPeriod, 34);
            sb.Add(lblMaStatus);
            sb.Fill(txtMaInfo);
            side.With(sb);

            stMa1 = new FxStat("Месяц +1", CForecast) { Hero = true };
            stMa2 = new FxStat("Месяц +2", CForecast);
            stMa3 = new FxStat("Месяц +3", CForecast);
            stMaMape = new FxStat("MAPE", CSmooth);
            stMaBest = new FxStat("Лучший период", CPurple);

            chartMa = new FxChart("Продажи и прогноз методом скользящего среднего", "Месяц", "Продажи, тыс. руб.");
            gridMa = MakeTable("Месяц", "Факт", "Прогноз", "Ошибка", "Ошибка, %");
            gridMaPeriods = MakeTable("k", "MAPE, % (свои мес.)", "MAE (свои мес.)", "MAPE, % (общие мес.)", "MAE (общие мес.)");
            gridMaHoldout = MakeTable("Месяц", "Факт", "Прогноз", "Ошибка", "Ошибка, %");
            FxSegmented seg;
            FxCard tables = TabCard("Таблицы", "data", 400, new[] { "Расчёт", "Периоды 3, 4, 5", "Отложенные данные" },
                new Control[] { gridMa, gridMaPeriods, gridMaHoldout }, out seg);

            FxBox main = FxBox.V(12);
            main.Add(Tiles(stMa1, stMa2, stMa3, stMaMape, stMaBest), 74);
            main.Fill(ChartCard(chartMa), 1.2f);
            main.Fill(tables, 1f);

            FxBox page = FxBox.H(12);
            page.Add(side, 318).Fill(main);
            return page;
        }

        private void UpdateMa()
        {
            if (loading || chartMa == null) return;
            bool ok = false;
            Safe(lblMaStatus, delegate
            {
                gridMa.Clear(); gridMaPeriods.Clear(); gridMaHoldout.Clear();
                Require(monthly, monthlyError, "Ряд по месяцам");
                double[] y = monthly;
                int n = y.Length, k = MaPeriod;
                var m = new MovingAverageMethod(k);
                double[] fit = m.Fit(y), smooth = m.Smooth(y), fc = m.Forecast(y, 3);
                ErrorStats st = ForecastMetrics.Compute(y, fit);

                // проверка на отложенных данных: первые n-3 месяца -> последние 3
                int train = n - 3;
                double[] trainY = Util.Take(y, train);
                double[] test = Util.Skip(y, train);
                double[] hf = m.Forecast(trainY, 3);
                ErrorStats hs = ForecastMetrics.Compute(test, hf);

                double[] x = Axis(1, n);
                chartMa.Begin();
                chartMa.Line("Факт", x, y, CFact, false, true).Area = true;
                chartMa.Line("Сглаженный ряд (k=" + k + ")", x, smooth, CSmooth, false, false);
                chartMa.Line("Прогноз на 3 месяца", Axis(n, 4), new[] { y[n - 1] }.Concat(fc).ToArray(), CForecast, true, true).HideFirstMarker = true;
                chartMa.Line("Прогноз по 1–" + train + " мес. (проверка)", Axis(train + 1, 3), hf, CPurple, true, true);
                chartMa.End();

                for (int i = 0; i < n; i++)
                {
                    double pct = ForecastMetrics.PercentError(y[i], fit[i]);
                    FxRow r = gridMa.AddRow(i + 1, Util.F(y[i], 0), Util.F(fit[i]),
                        double.IsNaN(fit[i]) ? "—" : Util.F(y[i] - fit[i]), Util.F(pct));
                    if (pct > 10) r.Tone = RowTone.Bad;
                }
                for (int h = 0; h < 3; h++)
                    gridMa.AddRow(n + h + 1, "—", Util.F(fc[h]), "—", "—").Tone = RowTone.Forecast;

                // сравнение периодов 3, 4, 5
                var scores = Optimizer.CompareMaPeriods(y, MethodComparer.MaPeriods);
                int best = Optimizer.BestMaPeriod(y, MethodComparer.MaPeriods);
                foreach (var s in scores)
                {
                    FxRow r = gridMaPeriods.AddRow(s.Period, Util.F(s.Own.Mape), Util.F(s.Own.Mae), Util.F(s.Common.Mape), Util.F(s.Common.Mae));
                    if (s.Period == best) r.Tone = RowTone.Good;
                }

                for (int h = 0; h < 3; h++)
                {
                    double pct = ForecastMetrics.PercentError(test[h], hf[h]);
                    FxRow r = gridMaHoldout.AddRow(train + h + 1, Util.F(test[h], 0), Util.F(hf[h]), Util.F(test[h] - hf[h]), Util.F(pct));
                    if (pct > 10) r.Tone = RowTone.Bad;
                }

                stMa1.Caption = "Месяц " + (n + 1); stMa1.Set(fc[0]); stMa1.Sub = "тыс. руб.";
                stMa2.Caption = "Месяц " + (n + 2); stMa2.Set(fc[1]); stMa2.Sub = "тыс. руб.";
                stMa3.Caption = "Месяц " + (n + 3); stMa3.Set(fc[2]); stMa3.Sub = "тыс. руб.";
                stMaMape.Set(st.Mape, 2, " %"); stMaMape.Sub = "месяцы " + (k + 1) + "–" + n;
                stMaBest.SetText("k = " + best); stMaBest.Sub = "по MAPE на общих мес.";

                txtMaInfo.Text =
                    "Прогноз (k = " + k + "): месяц " + (n + 1) + " — " + Util.F(fc[0]) + "; месяц " + (n + 2) + " — " + Util.F(fc[1]) +
                    "; месяц " + (n + 3) + " — " + Util.F(fc[2]) + " тыс. руб.\n" +
                    "Точность на месяцах " + (k + 1) + "–" + n + ": MAPE = " + Util.F(st.Mape) + " %, MAE = " + Util.F(st.Mae) + " тыс. руб.\n" +
                    "Лучший период: по MAPE на общих месяцах k = " + best + ".\n" +
                    "Отложенные данные: прогноз по месяцам 1–" + train + " на " + (train + 1) + "–" + n +
                    " (k = " + k + ") даёт MAPE = " + Util.F(hs.Mape) + " %, MAE = " + Util.F(hs.Mae) + ".\n" +
                    "Строки таблицы с ошибкой больше 10 % выделены красным.";
                ok = true;
            });
            if (!ok) Fail(chartMa, txtMaInfo, stMa1, stMa2, stMa3, stMaMape, stMaBest);
        }

        // =====================================================================
        //  Задание 1.2 — экспоненциальное сглаживание
        // =====================================================================
        private FxSlider trkAlpha;
        private FxNumber numAlpha, numSesMaPeriod;
        private FxSegmented segInit;
        private FxChart chartSes, chartAlpha;
        private FxTable gridSesCompare, gridSesInit;
        private FxText txtSesInfo;
        private FxLabel lblSesStatus;
        private FxStat stSesAlpha, stSesMape, stSesMse, stSesFc, stSesOpt;
        private bool syncing;

        private SesInit CurrentInit { get { return segInit.SelectedIndex == 1 ? SesInit.MeanOfFirstThree : SesInit.FirstValue; } }

        private Control BuildSesPage()
        {
            trkAlpha = new FxSlider(1, 99, 30);
            numAlpha = new FxNumber(0.01m, 0.99m, 0.30m, 2, 0.01m);
            trkAlpha.ValueChanged += delegate
            {
                if (syncing) return;
                syncing = true; numAlpha.Value = trkAlpha.Value / 100m; syncing = false;
                UpdateSes(); UpdateHolt(); UpdateCompare();
            };
            numAlpha.ValueChanged += delegate
            {
                if (syncing) return;
                syncing = true; trkAlpha.Value = (int)Math.Round(numAlpha.Value * 100); syncing = false;
                UpdateSes(); UpdateHolt(); UpdateCompare();
            };
            segInit = new FxSegmented("S1 = y1", "S1 = среднее трёх");
            segInit.SelectedIndexChanged += delegate { UpdateSes(); UpdateCompare(); };
            numSesMaPeriod = new FxNumber(2, 6, 3, 0, 1);
            numSesMaPeriod.ValueChanged += delegate { UpdateSes(); };
            var btnOpt = new FxButton("Подобрать оптимальный α", true, "target");
            btnOpt.Click += delegate
            {
                Safe(lblSesStatus, delegate
                {
                    Require(weekly, weeklyError, "Ряд по неделям");
                    AlphaSearchResult r = Optimizer.SearchSesAlpha(weekly, CurrentInit);
                    numAlpha.Value = (decimal)r.BestAlpha;   // найденное значение устанавливается в элементе управления
                });
            };
            lblSesStatus = StatusLabel();
            txtSesInfo = new FxText();

            var side = new FxCard("Параметры и результат", "wave");
            FxBox sb = FxBox.V(8);
            sb.Add(Caption("Коэффициент сглаживания α"), 16);
            FxBox alphaRow = FxBox.H(8);
            alphaRow.Fill(trkAlpha).Add(numAlpha, 112, true);
            sb.Add(alphaRow, 34);
            sb.Add(Caption("Начальное значение"), 16);
            sb.Add(segInit, 34);
            sb.Add(LabeledRow("Период СС для сравнения", numSesMaPeriod, 112), 34);
            sb.Add(btnOpt, 36);
            sb.Add(lblSesStatus);
            sb.Fill(txtSesInfo);
            side.With(sb);

            stSesAlpha = new FxStat("Текущий α", CAlt);
            stSesMape = new FxStat("MAPE", CSmooth);
            stSesMse = new FxStat("MSE", CSmooth);
            stSesFc = new FxStat("Прогноз", CForecast) { Hero = true };
            stSesOpt = new FxStat("Оптимальный α", CForecast);

            chartSes = new FxChart("Экспоненциальное сглаживание и прогноз на 4 недели", "Неделя", "Продажи, тыс. руб.");
            chartAlpha = new FxChart("Зависимость MSE от коэффициента сглаживания", "α", "MSE");
            gridSesCompare = MakeTable("Неделя", "Факт", "Эксп. сглаж.", "Ошибка, %", "Скольз. среднее", "Ошибка, %");
            gridSesInit = MakeTable("Начальное значение", "S1", "MAPE, %", "MAE", "MSE", "Прогноз");
            gridSesInit.Columns[0].Weight = 1.7f;
            FxSegmented seg;
            FxCard tables = TabCard("Таблицы", "data", 290, new[] { "Сравнение с СС", "Начальное S1" },
                new Control[] { gridSesCompare, gridSesInit }, out seg);
            FxBox bottom = FxBox.H(12);
            bottom.Fill(tables, 1.1f).Fill(ChartCard(chartAlpha), 1f);

            FxBox main = FxBox.V(12);
            main.Add(Tiles(stSesAlpha, stSesMape, stSesMse, stSesFc, stSesOpt), 74);
            main.Fill(ChartCard(chartSes), 1.1f);
            main.Fill(bottom, 1f);

            FxBox page = FxBox.H(12);
            page.Add(side, 318).Fill(main);
            return page;
        }

        private void UpdateSes()
        {
            if (loading || chartSes == null) return;
            bool ok = false;
            Safe(lblSesStatus, delegate
            {
                gridSesCompare.Clear(); gridSesInit.Clear();
                Require(weekly, weeklyError, "Ряд по неделям");
                double[] y = weekly;
                int n = y.Length;
                double alpha = (double)numAlpha.Value;
                SesInit init = CurrentInit;
                var ses = new ExponentialSmoothingMethod(alpha, init);
                double[] fit = ses.Fit(y), smooth = ses.Smooth(y), fc = ses.Forecast(y, 4);
                ErrorStats st = ForecastMetrics.Compute(y, fit);

                double[] x = Axis(1, n);
                chartSes.Begin();
                chartSes.Line("Факт", x, y, CFact, false, true).Area = true;
                chartSes.Line("Сглаженный ряд S (α=" + alpha.ToString("0.00") + ")", x, smooth, CSmooth, false, false);
                chartSes.Line("Прогноз на 4 недели", Axis(n, 5), new[] { smooth[n - 1] }.Concat(fc).ToArray(), CForecast, true, true).HideFirstMarker = true;
                chartSes.End();

                string info = "Точность: при α = " + alpha.ToString("0.00") + " MAPE = " + Util.F(st.Mape) + " %, MAE = " + Util.F(st.Mae) +
                              ", MSE = " + Util.F(st.Mse) + " (недели 2–" + n + ").\n" +
                              "Прогноз: на недели " + (n + 1) + "–" + (n + 4) + " — " + Util.F(fc[0]) + " тыс. руб.";

                foreach (SesInit mode in new[] { SesInit.FirstValue, SesInit.MeanOfFirstThree })
                {
                    var m = new ExponentialSmoothingMethod(alpha, mode);
                    ErrorStats e = ForecastMetrics.Compute(y, m.Fit(y));
                    FxRow r = gridSesInit.AddRow(mode == SesInit.FirstValue ? "y1" : "(y1+y2+y3)/3", Util.F(m.Smooth(y)[0]),
                        Util.F(e.Mape), Util.F(e.Mae), Util.F(e.Mse), Util.F(m.Forecast(y, 1)[0]));
                    if (mode == init) r.Tone = RowTone.Accent;
                }

                // сравнение со скользящим средним на одних и тех же неделях
                int k = (int)numSesMaPeriod.Value;
                double[] maFit = new MovingAverageMethod(k).Fit(y);
                List<int> common = ForecastMetrics.CommonIndices(fit, maFit);
                gridSesCompare.Columns[4].Header = "Скольз. среднее (k=" + k + ")";
                foreach (int i in common)
                {
                    double p1 = ForecastMetrics.PercentError(y[i], fit[i]);
                    double p2 = ForecastMetrics.PercentError(y[i], maFit[i]);
                    FxRow r = gridSesCompare.AddRow(i + 1, Util.F(y[i], 0), Util.F(fit[i]), Util.F(p1), Util.F(maFit[i]), Util.F(p2));
                    gridSesCompare.SetCellTone(r, p1 <= p2 ? 3 : 5, RowTone.Good);
                }
                ErrorStats es = ForecastMetrics.Compute(y, fit, common);
                ErrorStats em = ForecastMetrics.Compute(y, maFit, common);
                gridSesCompare.AddRow("MAPE, %", "", Util.F(es.Mape), "", Util.F(em.Mape), "").Tone = RowTone.Total;
                gridSesCompare.AddRow("MAE", "", Util.F(es.Mae), "", Util.F(em.Mae), "").Tone = RowTone.Total;
                info += "\nСравнение: на неделях " + (common.First() + 1) + "–" + n + " эксп. сглаживание даёт MAPE " + Util.F(es.Mape) +
                        " %, скользящее среднее (k=" + k + ") — " + Util.F(em.Mape) + " %; точнее " +
                        (es.Mape <= em.Mape ? "экспоненциальное сглаживание." : "скользящее среднее.");

                // оптимальный α
                AlphaSearchResult opt = Optimizer.SearchSesAlpha(y, init);
                chartAlpha.Begin();
                chartAlpha.Line("MSE(α)", opt.Alphas, opt.Mse, CFact, false, false).Area = true;
                FxSeries best = chartAlpha.Points("Минимум: α = " + opt.BestAlpha.ToString("0.00"), new[] { opt.BestAlpha }, new[] { opt.BestMse }, CForecast, 11);
                chartAlpha.Points("Текущий α", new[] { alpha }, new[] { st.Mse }, CAlt, 9);
                chartAlpha.Note(best, 0, "α* = " + opt.BestAlpha.ToString("0.00") + "\nMSE = " + Util.F(opt.BestMse));
                chartAlpha.XMin = 0;
                chartAlpha.XMax = 1;
                chartAlpha.End();
                info += "\nОптимум: по минимуму MSE α = " + opt.BestAlpha.ToString("0.00") + " (MSE = " + Util.F(opt.BestMse) + ").";

                info += "\nПочему прогноз на все 4 недели одинаков: прогноз простого экспоненциального сглаживания равен последнему " +
                        "сглаженному значению S" + n + " = " + Util.F(smooth[n - 1]) + ". Для следующих шагов новых фактов нет, " +
                        "а модель содержит только уровень, без тренда и сезонности, поэтому уровень не меняется и прогноз — горизонтальная линия.";
                txtSesInfo.Text = info;

                stSesAlpha.Set(alpha); stSesAlpha.Sub = init == SesInit.FirstValue ? "S1 = y1" : "S1 = среднее трёх";
                stSesMape.Set(st.Mape, 2, " %"); stSesMape.Sub = "недели 2–" + n;
                stSesMse.Set(st.Mse); stSesMse.Sub = "MAE = " + Util.F(st.Mae);
                stSesFc.Set(fc[0]); stSesFc.Sub = "недели " + (n + 1) + "–" + (n + 4);
                stSesOpt.Set(opt.BestAlpha); stSesOpt.Sub = "MSE = " + Util.F(opt.BestMse);
                ok = true;
            });
            if (!ok) Fail(chartSes, chartAlpha, txtSesInfo, stSesAlpha, stSesMape, stSesMse, stSesFc, stSesOpt);
        }

        // =====================================================================
        //  Задание 1.3 — линейная регрессия
        // =====================================================================
        private FxNumber numX0;
        private FxChart chartReg, chartResid;
        private FxTable gridReg;
        private FxText txtRegInfo;
        private FxLabel lblRegStatus;
        private FxStat stRegY, stRegR2, stRegB, stRegInt, stRegT;
        private LinearRegression currentModel;

        private Control BuildRegressionPage()
        {
            numX0 = new FxNumber(0, 100000, 60, 1, 1);
            numX0.ValueChanged += delegate { if (!loading) UpdateRegression(); };
            lblRegStatus = StatusLabel();
            txtRegInfo = new FxText();

            var side = new FxCard("Параметры и результат", "scatter");
            FxBox sb = FxBox.V(8);
            sb.Add(Caption("Расходы на рекламу для прогноза, тыс. руб."), 16);
            sb.Add(numX0, 34);
            sb.Add(Para("Наведите курсор на график регрессии — рядом появится прогноз для соответствующих расходов.", Tone.Mute));
            sb.Add(lblRegStatus);
            sb.Fill(txtRegInfo);
            side.With(sb);

            stRegY = new FxStat("Прогноз продаж", CAlt) { Hero = true };
            stRegInt = new FxStat("95 % интервал", CAlt);
            stRegR2 = new FxStat("R²", CForecast);
            stRegB = new FxStat("Наклон b", CForecast);
            stRegT = new FxStat("t-статистика", CPurple);

            chartReg = new FxChart("Зависимость продаж от расходов на рекламу", "Расходы на рекламу, тыс. руб.", "Продажи, тыс. руб.");
            chartReg.HoverInfo = delegate(double x)
            {
                LinearRegression m = currentModel;
                if (m == null) return null;
                return "Реклама " + Util.F(x, 1) + " тыс. руб.\nпрогноз продаж " + Util.F(m.Predict(x)) + " тыс. руб.\n± " + Util.F(m.PredictionHalfWidth(x)) + " (95 %)";
            };
            chartResid = new FxChart("Остатки (факт − расчёт)", "Расходы на рекламу, тыс. руб.", "Остаток");
            gridReg = MakeTable("x", "y", "ŷ", "e = y − ŷ", "e²");

            FxBox bottom = FxBox.H(12);
            bottom.Fill(ChartCard(chartResid), 1f).Fill(new FxCard("Расчётная таблица", "data").With(gridReg), 1f);
            FxBox main = FxBox.V(12);
            main.Add(Tiles(stRegY, stRegInt, stRegR2, stRegB, stRegT), 74);
            main.Fill(ChartCard(chartReg), 1.3f);
            main.Fill(bottom, 1f);

            FxBox page = FxBox.H(12);
            page.Add(side, 330).Fill(main);
            return page;
        }

        private void UpdateRegression()
        {
            if (loading || chartReg == null) return;
            bool ok = false;
            Safe(lblRegStatus, delegate
            {
                gridReg.Clear(); currentModel = null;
                if (pairsError != null) throw new ForecastException(pairsError);
                if (adX == null) throw new ForecastException("Пары «реклама — продажи» не заданы.");
                LinearRegression m = LinearRegression.Fit(adX, adY);
                currentModel = m;
                double x0 = (double)numX0.Value;
                double y0 = m.Predict(x0);
                double hw = m.PredictionHalfWidth(x0);

                double lo = Math.Min(m.MinX, x0), hi = Math.Max(m.MaxX, x0);
                int steps = 40;
                double[] lx = new double[steps + 1], ly = new double[steps + 1], up = new double[steps + 1], dn = new double[steps + 1];
                for (int i = 0; i <= steps; i++)
                {
                    lx[i] = lo + (hi - lo) * i / steps;
                    ly[i] = m.Predict(lx[i]);
                    double w = m.PredictionHalfWidth(lx[i]);
                    up[i] = ly[i] + w; dn[i] = ly[i] - w;
                }
                chartReg.Begin();
                chartReg.Band("Границы 95 % интервала прогноза", lx, up, dn, -1);
                FxSeries line = chartReg.Line("Регрессия: " + m.Equation, lx, ly, CForecast, false, false);
                chartReg.Points("Наблюдения", adX, adY, CFact, 9);
                chartReg.Line("95 % интервал в точке прогноза", new[] { x0, x0 }, new[] { y0 - hw, y0 + hw }, CAlt, false, true);
                FxSeries fp = chartReg.Points("Прогноз при x = " + Util.F(x0, 1), new[] { x0 }, new[] { y0 }, CAlt, 12);
                chartReg.Note(line, steps / 3, m.Equation + "\nR² = " + m.R2.ToString("0.000"));
                chartReg.Note(fp, 0, "ŷ = " + Util.F(y0));
                chartReg.End();

                double[] res = m.Residuals();
                chartResid.Begin();
                chartResid.Stems("Остатки", adX, res, CPurple);
                chartResid.End();

                for (int i = 0; i < m.N; i++)
                    gridReg.AddRow(Util.F(adX[i], 1), Util.F(adY[i], 1), Util.F(m.Predict(adX[i])), Util.F(res[i]), Util.F(res[i] * res[i]));

                string ext = m.IsExtrapolation(x0)
                    ? "Экстраполяция: точка x = " + Util.F(x0, 1) + " лежит вне диапазона исходных данных [" + Util.F(m.MinX, 1) + "; " + Util.F(m.MaxX, 1) +
                      "]. Прогноз менее надёжен: слагаемое (x0 − x ср.)² / Sxx увеличивает ширину интервала, " +
                      "а сохранение линейной зависимости за пределами наблюдений ничем не подтверждено (возможны насыщение рынка, другие факторы)."
                    : "Интерполяция: точка прогноза лежит внутри диапазона исходных данных.";
                txtRegInfo.Text =
                    "Уравнение: " + m.Equation + "\n" +
                    "Коэффициенты: a = " + m.A.ToString("0.0000") + ", b = " + m.B.ToString("0.0000") + " (рост рекламы на 1 тыс. руб. даёт +" + Util.F(m.B, 3) + " тыс. руб. продаж)\n" +
                    "Суммы: x ср. = " + Util.F(m.MeanX) + ", y ср. = " + Util.F(m.MeanY) + ", Sxx = Σ(x − x ср.)² = " + Util.F(m.Sxx) +
                    ", Sxy = Σ(x − x ср.)(y − y ср.) = " + Util.F(m.Sxy) + "\n" +
                    "Корреляция: R = " + m.R.ToString("0.0000") + " — " + LinearRegression.Strength(m.R) + "\n" +
                    "Значимость: t = " + Util.F(m.TStat, 3) + ", t крит = " + Util.F(m.TCritical, 3) + " (α = 0,05; " + m.DegreesOfFreedom + " степ. свободы) — " +
                    (m.IsSignificant ? "t > t крит, связь статистически значима." : "t ≤ t крит, связь незначима.") + "\n" +
                    "Детерминация: R² = " + m.R2.ToString("0.0000") + " — модель объясняет " + Util.F(m.R2 * 100, 1) + " % разброса продаж.\n" +
                    "Стандартная ошибка: s = " + Util.F(m.StdError, 3) + "\n" +
                    "Прогноз: при x = " + Util.F(x0, 1) + " ŷ = " + Util.F(y0) + " тыс. руб.\n" +
                    "Интервал 95 %: " + Util.F(y0 - hw) + " … " + Util.F(y0 + hw) + " (± " + Util.F(hw) + ")\n" + ext;

                stRegY.Set(y0); stRegY.Sub = "при рекламе " + Util.F(x0, 1) + " тыс. руб.";
                stRegInt.Set(hw, 2); stRegInt.Sub = Util.F(y0 - hw, 1) + " … " + Util.F(y0 + hw, 1);
                stRegR2.Set(m.R2, 4); stRegR2.Sub = "объяснено " + Util.F(m.R2 * 100, 1) + " %";
                stRegB.Set(m.B, 3); stRegB.Sub = "a = " + Util.F(m.A);
                stRegT.Set(m.TStat, 2); stRegT.Sub = m.IsSignificant ? "связь значима" : "связь незначима";
                stRegInt.Caption = "95 % интервал, ±";
                ok = true;
            });
            if (!ok) Fail(chartReg, chartResid, txtRegInfo, stRegY, stRegInt, stRegR2, stRegB, stRegT);
        }

        // =====================================================================
        //  Задание 1.5 — метод Хольта
        // =====================================================================
        private FxSlider trkHoltA, trkHoltB;
        private FxLabel lblHoltA, lblHoltB, lblHoltStatus;
        private FxText txtHoltInfo;
        private FxChart chartHolt;
        private FxHeat heatHolt;
        private FxTable gridHoltCompare;
        private FxStat stHoltL, stHoltT, stHoltMse, stHoltMape, stHoltOpt;
        private double[] heatFor;
        private HoltGridResult heat;

        private Control BuildHoltPage()
        {
            trkHoltA = new FxSlider(1, 99, 30);
            trkHoltB = new FxSlider(1, 99, 30);
            lblHoltA = new FxLabel("", Fonts.Semi(9f), Tone.Text) { Height = 16 };
            lblHoltB = new FxLabel("", Fonts.Semi(9f), Tone.Text) { Height = 16 };
            trkHoltA.ValueChanged += delegate { UpdateHolt(); UpdateCompare(); };
            trkHoltB.ValueChanged += delegate { UpdateHolt(); UpdateCompare(); };
            var btnOpt = new FxButton("Найти оптимальные α и β", true, "target");
            btnOpt.Click += delegate
            {
                Safe(lblHoltStatus, delegate
                {
                    Require(weekly, weeklyError, "Ряд по неделям");
                    HoltGridResult g = Optimizer.SearchHolt(weekly);
                    trkHoltA.Value = (int)Math.Round(g.BestAlpha * 100);
                    trkHoltB.Value = (int)Math.Round(g.BestBeta * 100);
                });
            };
            lblHoltStatus = StatusLabel();
            txtHoltInfo = new FxText();

            var side = new FxCard("Параметры и результат", "trend");
            FxBox sb = FxBox.V(6);
            sb.Add(new FxLabel("L(t) = α·y(t) + (1−α)·(L(t−1) + T(t−1))\nT(t) = β·(L(t) − L(t−1)) + (1−β)·T(t−1)", Fonts.Mono(8f), Tone.Mute, true));
            sb.Add(lblHoltA, 16).Add(trkHoltA, 28);
            sb.Add(lblHoltB, 16).Add(trkHoltB, 28);
            sb.Add(btnOpt, 36);
            sb.Add(lblHoltStatus);
            sb.Fill(txtHoltInfo);
            side.With(sb);

            stHoltL = new FxStat("Уровень L", CSmooth);
            stHoltT = new FxStat("Тренд T", CSmooth);
            stHoltMse = new FxStat("MSE", CAlt);
            stHoltMape = new FxStat("MAPE", CAlt);
            stHoltOpt = new FxStat("Оптимум α / β", CForecast) { Hero = true };

            chartHolt = new FxChart("Метод Хольта и простое экспоненциальное сглаживание", "Неделя", "Продажи, тыс. руб.");
            heatHolt = new FxHeat();
            heatHolt.CellClicked += delegate(double a, double b)
            {
                trkHoltA.Value = (int)Math.Round(a * 100);
                trkHoltB.Value = (int)Math.Round(b * 100);
            };
            gridHoltCompare = MakeTable("Метод", "MSE", "MAPE, %", "MAE", "Прогноз +1…+4");
            gridHoltCompare.WrapCells = true;
            gridHoltCompare.FontSize = 8.5f;
            gridHoltCompare.Columns[0].Weight = 2.1f;
            gridHoltCompare.Columns[0].Align = StringAlignment.Near;
            gridHoltCompare.Columns[4].Weight = 2.2f;
            for (int i = 1; i <= 3; i++) gridHoltCompare.Columns[i].NoWrap = true;

            FxBox bottom = FxBox.H(12);
            bottom.Fill(new FxCard("MSE для всех сочетаний α и β — щёлкните по ячейке", "target").With(heatHolt), 1.15f);
            bottom.Fill(new FxCard("Сравнение методов", "layers").With(gridHoltCompare), 1f);
            FxBox main = FxBox.V(12);
            main.Add(Tiles(stHoltL, stHoltT, stHoltMse, stHoltMape, stHoltOpt), 74);
            main.Fill(ChartCard(chartHolt), 1f);
            main.Fill(bottom, 1.05f);

            FxBox page = FxBox.H(12);
            page.Add(side, 330).Fill(main);
            return page;
        }

        private void UpdateHolt()
        {
            if (loading || chartHolt == null) return;
            double a = trkHoltA.Value / 100.0, b = trkHoltB.Value / 100.0;
            lblHoltA.Text = "α = " + a.ToString("0.00") + " — сглаживание уровня";
            lblHoltB.Text = "β = " + b.ToString("0.00") + " — сглаживание тренда";
            bool ok = false;
            Safe(lblHoltStatus, delegate
            {
                gridHoltCompare.Clear();
                Require(weekly, weeklyError, "Ряд по неделям");
                double[] y = weekly;
                int n = y.Length;
                var holt = new HoltMethod(a, b);
                double[] fit = holt.Fit(y), fc = holt.Forecast(y, 4);
                double level, trend;
                holt.LastState(y, out level, out trend);

                if (heatFor != y)
                {
                    heat = Optimizer.SearchHolt(y);
                    heatFor = y;
                    heatHolt.SetData(heat.Values, heat.Mse, heat.MinMse, heat.MaxMse, heat.BestAlpha, heat.BestBeta);
                }
                heatHolt.SetCurrent(a, b);

                double sesAlpha = (double)numAlpha.Value;
                AlphaSearchResult sesOpt = Optimizer.SearchSesAlpha(y, SesInit.FirstValue);
                var methods = new List<IForecastMethod>
                {
                    holt,
                    new HoltMethod(heat.BestAlpha, heat.BestBeta),
                    new ExponentialSmoothingMethod(sesAlpha),
                    new ExponentialSmoothingMethod(sesOpt.BestAlpha)
                };
                string[] names = { "Хольт (текущие α, β)", "Хольт (оптим. α=" + heat.BestAlpha.ToString("0.00") + ", β=" + heat.BestBeta.ToString("0.00") + ")",
                                   "Эксп. сглаж. (α=" + sesAlpha.ToString("0.00") + ")", "Эксп. сглаж. (оптим. α=" + sesOpt.BestAlpha.ToString("0.00") + ")" };
                double[][] fits = methods.Select(m => m.Fit(y)).ToArray();
                List<int> common = ForecastMetrics.CommonIndices(fits);
                var stats = new List<ErrorStats>();
                for (int i = 0; i < methods.Count; i++)
                {
                    ErrorStats e = ForecastMetrics.Compute(y, fits[i], common);
                    stats.Add(e);
                    double[] f4 = methods[i].Forecast(y, 4);
                    FxRow r = gridHoltCompare.AddRow(names[i], Util.F(e.Mse), Util.F(e.Mape), Util.F(e.Mae), string.Join("; ", f4.Select(v => Util.F(v, 1))));
                    if (i == 0) r.Tone = RowTone.Accent;
                }

                double[] x = Axis(1, n);
                double[] sesFc = methods[2].Forecast(y, 4);
                chartHolt.Begin();
                chartHolt.Line("Факт", x, y, CFact, false, true).Area = true;
                chartHolt.Line("Хольт: одношаговый прогноз", x, fit, CSmooth, false, false);
                chartHolt.Line("Хольт: прогноз на 4 недели", Axis(n, 5), new[] { y[n - 1] }.Concat(fc).ToArray(), CForecast, true, true).HideFirstMarker = true;
                chartHolt.Line("Эксп. сглаживание: прогноз", Axis(n, 5), new[] { y[n - 1] }.Concat(sesFc).ToArray(), CAlt, true, true).HideFirstMarker = true;
                chartHolt.End();

                txtHoltInfo.Text =
                    "Состояние: последний уровень L" + n + " = " + Util.F(level) + ", тренд T" + n + " = " + Util.F(trend) + " тыс. руб./нед.\n" +
                    "Прогноз: " + string.Join("; ", fc.Select((v, i) => "нед. " + (n + i + 1) + " — " + Util.F(v))) + "\n" +
                    "Ошибки: на неделях " + (common.First() + 1) + "–" + n + " MSE = " + Util.F(stats[0].Mse) + ", MAPE = " + Util.F(stats[0].Mape) + " %.\n" +
                    "Оптимум перебора: α = " + heat.BestAlpha.ToString("0.00") + ", β = " + heat.BestBeta.ToString("0.00") + ", MSE = " + Util.F(heat.BestMse) + ".\n" +
                    HoltVsSesExplanation(y, methods[1], methods[3], stats[1], stats[3]);

                stHoltL.Caption = "Уровень L" + n; stHoltL.Set(level); stHoltL.Sub = "тыс. руб.";
                stHoltT.Caption = "Тренд T" + n; stHoltT.Set(trend); stHoltT.Sub = "тыс. руб. за неделю";
                stHoltMse.Set(stats[0].Mse); stHoltMse.Sub = "текущие α, β";
                stHoltMape.Set(stats[0].Mape, 2, " %"); stHoltMape.Sub = "недели " + (common.First() + 1) + "–" + n;
                stHoltOpt.SetText(heat.BestAlpha.ToString("0.00", Ru) + " / " + heat.BestBeta.ToString("0.00", Ru));
                stHoltOpt.Sub = "MSE = " + Util.F(heat.BestMse);
                ok = true;
            });
            if (!ok) Fail(chartHolt, heatHolt, txtHoltInfo, stHoltL, stHoltT, stHoltMse, stHoltMape, stHoltOpt);
        }

        /// <summary>Сравнение Хольта и простого сглаживания: одношаговая ошибка и скользящая проверка на 1–4 недели вперёд.</summary>
        private string HoltVsSesExplanation(double[] y, IForecastMethod holtOpt, IForecastMethod sesOpt, ErrorStats holtOne, ErrorStats sesOne)
        {
            const int minTrain = 6, maxH = 4;
            double[] mh = MethodComparer.RollingOriginMape(holtOpt, y, minTrain, maxH);
            double[] ms = MethodComparer.RollingOriginMape(sesOpt, y, minTrain, maxH);
            string one = holtOne.Mse < sesOne.Mse
                ? "Одношаговая ошибка: на всём ряде Хольт точнее (MSE " + Util.F(holtOne.Mse) + " против " + Util.F(sesOne.Mse) + ")."
                : "Одношаговая ошибка: на всём ряде Хольт не точнее (MSE " + Util.F(holtOne.Mse) + " против " + Util.F(sesOne.Mse) +
                  "): колебания вокруг тренда сбивают оценку наклона T, а сглаживание с большим α почти повторяет последнее значение.";
            int wins = 0;
            for (int h = 0; h < maxH; h++) if (mh[h] < ms[h]) wins++;
            string rolling = "Скользящая проверка: прогноз от каждой недели начиная с " + (minTrain + 1) + "-й, MAPE по горизонтам 1–4 нед.: " +
                             "Хольт " + string.Join(" / ", mh.Select(v => Util.F(v))) + " %; простое сглаживание " + string.Join(" / ", ms.Select(v => Util.F(v))) + " %.";
            string why = wins >= 3
                ? "Вывод: Хольт точнее на " + wins + " горизонтах из 4 — простое сглаживание не имеет компоненты тренда, его прогноз — горизонтальная линия S(n), и на растущем ряде ошибка увеличивается с каждым шагом, а Хольт продлевает рост на T за неделю."
                : "Вывод: преимущество Хольта проявилось только на " + wins + " горизонтах из 4 — тренд слабый по сравнению с колебаниями ряда.";
            return one + "\n" + rolling + "\n" + why;
        }
    }
}
