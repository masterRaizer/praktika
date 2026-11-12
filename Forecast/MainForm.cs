using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using Forecast.Core;
using FxKit;

namespace Forecast
{
    /// <summary>
    /// Главная форма: ввод данных и отображение результатов.
    /// Формулы расчёта находятся в классах пространства имён Forecast.Core,
    /// форма только передаёт им данные и показывает результат.
    /// Оформление (окно, элементы управления, графики, анимации) — библиотека FxKit, папка Fx.
    /// </summary>
    public partial class MainForm : FxWindow
    {
        /// <summary>Вариант, загружаемый при запуске (номер компьютера).</summary>
        public const int DefaultVariant = 2;

        // номера цветов рядов в палитре темы
        private const int CFact = 0, CForecast = 1, CSmooth = 2, CAlt = 3, CPink = 4, CPurple = 5;

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        // ---------- текущие данные ----------
        private double[] monthly, weekly, adX, adY;
        private string monthlyError, weeklyError, pairsError;
        private string monthlySource = "", weeklySource = "", pairsSource = "";
        private bool loading;

        // ---------- раздел «Исходные данные» ----------
        private FxNumber numVariant;
        private FxTable gridMonthly, gridWeekly, gridPairs;
        private FxLabel lblSourceMonthly, lblSourceWeekly, lblSourcePairs, lblDataErrors;
        private FxChart sparkMonthly, sparkWeekly, sparkPairs;
        private FxSegmented segRandTarget;
        private FxNumber numRandLength, numRandLevel, numRandGrowth, numRandNoise, numRandXMin, numRandXMax;

        public MainForm()
        {
            Text = "Моделирование прогноза — учебная практика №2";
            AppTitle = "Forecast";
            AppCaption = "учебная практика №2";
            AppIcon = "trend";
            SplashTitle = "Моделирование прогноза";
            SplashSubtitle = "скользящее среднее · сглаживание · регрессия · метод Хольта";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1420, wa.Width - 24), Math.Min(880, wa.Height - 24));
            MinimumSize = new Size(Math.Min(1180, wa.Width), Math.Min(660, wa.Height));

            AddPage("data", "Исходные данные", "вариант · CSV · генерация", "Исходные данные",
                "Ряды продаж и пары «реклама — продажи»: вариант, ручной ввод, файл CSV или генерация", BuildDataPage());
            AddPage("pulse", "Скользящее среднее", "задание 1.1", "Скользящее среднее",
                "Прогноз продаж на 3 месяца; прогнозы 2-го и 3-го месяца используют уже полученные прогнозы", BuildMaPage());
            AddPage("wave", "Эксп. сглаживание", "задание 1.2", "Экспоненциальное сглаживание",
                "Прогноз на 4 недели, подбор коэффициента α и сравнение со скользящим средним", BuildSesPage());
            AddPage("scatter", "Регрессия", "задание 1.3", "Линейная регрессия",
                "Зависимость продаж от расходов на рекламу, значимость связи и интервал прогноза", BuildRegressionPage());
            AddPage("trend", "Метод Хольта", "задание 1.5", "Метод Хольта",
                "Уровень и тренд: L1 = y1, T1 = y2 − y1, прогноз L(n) + h·T(n); перебор α и β по сетке с шагом 0,05", BuildHoltPage());
            AddPage("layers", "Все методы", "задание 1.4", "Сравнение на графике",
                "Одношаговые прогнозы всех методов и прогноз вперёд на одном графике", BuildComparePage());
            AddPage("bars", "Анализ точности", "задание 2.1", "Сравнение методов",
                "Сводная таблица ошибок, влияние параметров, выводы и рекомендации", BuildAnalysisPage());
            AddPage("bulb", "Рекомендации", "задание 2.2", "Практические рекомендации",
                "Выбор метода под характер ряда, типичные проблемы и учёт внешних факторов", BuildPracticePage());

            Load += delegate
            {
                LoadVariant(DefaultVariant);
                GenerateAndEvaluate(new SeriesGenerator(7));
                segProblem.SelectedIndex = 0;
                UpdateProblem();
                Navigate(StartPage, false);
            };
        }

        /// <summary>Раздел, открываемый при запуске.</summary>
        public int StartPage;

        /// <summary>Загрузка варианта по номеру извне формы.</summary>
        public void ShowVariant(int number) { LoadVariant(number); }

        /// <summary>Открыть внутреннюю вкладку разделов «Анализ точности» и «Рекомендации».</summary>
        public void ShowInnerTab(int index)
        {
            if (segAnTabs != null && index < segAnTabs.Items.Length) segAnTabs.SelectedIndex = index;
            if (segPrTabs != null && index < segPrTabs.Items.Length) segPrTabs.SelectedIndex = index;
        }

        // =====================================================================
        //  Общие элементы оформления
        // =====================================================================

        /// <summary>Мелкая подпись над полем.</summary>
        private static FxLabel Caption(string text)
        {
            return new FxLabel(text, Fonts.Semi(8f), Tone.Mute) { Height = 16 };
        }

        /// <summary>Абзац пояснительного текста (высота подстраивается под ширину).</summary>
        private static FxLabel Para(string text, Tone tone = Tone.Dim)
        {
            return new FxLabel(text, Fonts.Body(9f), tone, true);
        }

        /// <summary>Поле с подписью сверху.</summary>
        private static FxBox Field(string caption, Control input)
        {
            FxBox b = FxBox.V(2);
            b.Add(Caption(caption), 16).Add(input, 34);
            b.Height = 52;
            return b;
        }

        /// <summary>Подпись слева, элемент справа.</summary>
        private static FxBox LabeledRow(string caption, Control input, int inputWidth)
        {
            FxBox b = FxBox.H(8);
            b.Fill(new FxLabel(caption, Fonts.Medium(9f), Tone.Dim));
            b.Add(input, inputWidth, true);
            b.Height = 34;
            return b;
        }

        private static FxLabel StatusLabel()
        {
            return new FxLabel("", Fonts.Semi(9f), Tone.Bad, true) { Icon = "warn" };
        }

        private static FxTable MakeTable(params string[] headers)
        {
            var t = new FxTable();
            t.SetColumns(headers);
            return t;
        }

        private static FxCard ChartCard(FxChart chart)
        {
            return new FxCard { Padding = new Padding(12, 10, 10, 8) }.With(chart);
        }

        private static FxBox Tiles(params FxStat[] stats)
        {
            FxBox b = FxBox.H(10);
            foreach (FxStat s in stats) b.Fill(s);
            b.Height = 74;
            return b;
        }

        /// <summary>Карточка с вкладками: переключатель в заголовке и плавная смена содержимого.</summary>
        private static FxCard TabCard(string title, string icon, int segWidth, string[] tabs, Control[] pages, out FxSegmented seg)
        {
            var card = new FxCard(title, icon);
            var pager = new FxPager { VerticalSlide = false, Duration = 320 };
            foreach (Control p in pages) pager.AddPage(p);
            var s = new FxSegmented(tabs) { Size = new Size(segWidth, 28) };
            s.SelectedIndexChanged += delegate { pager.ShowPage(s.SelectedIndex); };
            card.With(pager);
            card.WithHeader(s);
            pager.ShowPage(0, false);
            seg = s;
            return card;
        }

        private static string Num(double v) { return v.ToString("0.####", Ru); }

        /// <summary>
        /// Выполнение расчёта с перехватом ошибок: программа не завершается аварийно,
        /// сообщение выводится в строку состояния раздела.
        /// </summary>
        private void Safe(FxLabel status, Action action)
        {
            try
            {
                status.Text = "";
                action();
            }
            catch (ForecastException ex)
            {
                status.Text = "Ошибка: " + ex.Message;
            }
            catch (Exception ex)
            {
                status.Text = "Непредвиденная ошибка: " + ex.Message;
            }
        }

        private static void Require(double[] data, string error, string what)
        {
            if (error != null) throw new ForecastException(what + ": " + error);
            if (data == null) throw new ForecastException(what + ": данные не заданы.");
        }

        // =====================================================================
        //  Раздел «Исходные данные»
        // =====================================================================

        private Control BuildDataPage()
        {
            FxBox page = FxBox.V(12);

            // вариант задания
            var top = new FxCard { Padding = new Padding(16, 12, 16, 12) };
            numVariant = new FxNumber(0, VariantData.MaxNumber, DefaultVariant, 0, 1);
            var btnLoad = new FxButton("Загрузить вариант", true, "download").AutoWidth();
            btnLoad.Click += delegate { LoadVariant((int)numVariant.Value); };
            numVariant.ValueChanged += delegate { if (!loading) LoadVariant((int)numVariant.Value); };
            lblDataErrors = new FxLabel("", Fonts.Semi(9f), Tone.Good, true) { Icon = "check", VAlign = StringAlignment.Center, AutoHeight = false };
            FxBox topRow = FxBox.H(14);
            topRow.Add(new FxLabel("Номер варианта", Fonts.Semi(10f)) { Width = 128 }, 128);
            topRow.Add(numVariant, 118, true);
            topRow.Add(btnLoad, btnLoad.Width, true);
            topRow.Add(new FxLabel("номер компьютера, 0 — демонстрационный", Fonts.Body(8.5f), Tone.Mute) { Width = 250 }, 250);
            topRow.Fill(lblDataErrors);
            top.With(topRow);
            page.Add(top, 62);

            // три таблицы
            gridMonthly = MakeTable("Месяц", "Продажи, тыс. руб.");
            gridWeekly = MakeTable("Неделя", "Продажи, тыс. руб.");
            gridPairs = MakeTable("№", "Реклама, тыс. руб.", "Продажи, тыс. руб.");
            foreach (FxTable g in new[] { gridMonthly, gridWeekly, gridPairs })
            {
                FxTable grid = g;
                grid.Editable = true;
                grid.Columns[0].ReadOnly = true;
                grid.Columns[0].Weight = 0.45f;
                grid.CellEdited += delegate { Grid_Changed(grid); };
                grid.RowsEdited += delegate { Grid_Changed(grid); };
            }
            lblSourceMonthly = new FxLabel("", Fonts.Body(8.5f), Tone.Mute);
            lblSourceWeekly = new FxLabel("", Fonts.Body(8.5f), Tone.Mute);
            lblSourcePairs = new FxLabel("", Fonts.Body(8.5f), Tone.Mute);
            sparkMonthly = new FxChart { Sparkline = true };
            sparkWeekly = new FxChart { Sparkline = true };
            sparkPairs = new FxChart { Sparkline = true };

            FxBox grids = FxBox.H(12);
            grids.Fill(DataColumn("Продажи по месяцам · 1.1", "pulse", gridMonthly, lblSourceMonthly, sparkMonthly, "M"));
            grids.Fill(DataColumn("Продажи по неделям · 1.2, 1.5", "wave", gridWeekly, lblSourceWeekly, sparkWeekly, "W"));
            grids.Fill(DataColumn("Реклама и продажи · 1.3", "scatter", gridPairs, lblSourcePairs, sparkPairs, "P"));
            page.Fill(grids);

            // генерация случайных данных
            var rand = new FxCard("Случайные данные", "dice");
            segRandTarget = new FxSegmented("Месяцы", "Недели", "Реклама");
            numRandLength = new FxNumber(6, 200, 12, 0, 1);
            numRandLevel = new FxNumber(0, 100000, 150, 1, 5);
            numRandGrowth = new FxNumber(-1000, 1000, 5, 2, 0.5m);
            numRandNoise = new FxNumber(0, 10000, 6, 1, 0.5m);
            numRandXMin = new FxNumber(0, 100000, 10, 1, 1);
            numRandXMax = new FxNumber(0, 100000, 55, 1, 1);
            segRandTarget.SelectedIndexChanged += delegate
            {
                numRandLength.Value = segRandTarget.SelectedIndex == 0 ? 12 : segRandTarget.SelectedIndex == 1 ? 15 : 10;
            };
            var btnRand = new FxButton("Сгенерировать", true, "dice").AutoWidth();
            btnRand.Click += BtnRandom_Click;
            FxBox randRow = FxBox.H(10);
            FxBox target = FxBox.V(2);
            target.Add(Caption("Куда записать"), 16).Add(segRandTarget, 34);
            randRow.Fill(target, 2.6f);
            randRow.Fill(Field("Длина", numRandLength));
            randRow.Fill(Field("Уровень (a)", numRandLevel));
            randRow.Fill(Field("Прирост (b)", numRandGrowth));
            randRow.Fill(Field("Шум σ", numRandNoise));
            randRow.Fill(Field("Реклама от", numRandXMin));
            randRow.Fill(Field("до", numRandXMax));
            FxBox btnBox = FxBox.V(2);
            btnBox.Add(new FxLabel(""), 16).Add(btnRand, 34);
            randRow.Add(btnBox, btnRand.Width);
            rand.With(randRow);
            page.Add(rand, 112);
            return page;
        }

        private Control DataColumn(string title, string icon, FxTable grid, FxLabel source, FxChart spark, string tag)
        {
            var card = new FxCard(title, icon);
            var btnCsv = new FxButton("CSV…", false, "upload") { Size = new Size(86, 28), Tag = tag };
            btnCsv.Click += BtnCsv_Click;
            FxBox box = FxBox.V(6);
            box.Add(source, 18).Add(spark, 44).Fill(grid);
            card.With(box);
            card.WithHeader(btnCsv);
            return card;
        }

        /// <summary>Загрузка всех рядов варианта в таблицы.</summary>
        private void LoadVariant(int number)
        {
            try
            {
                VariantData v = VariantData.Get(number);
                loading = true;
                if (numVariant.Value != number) numVariant.Value = number;
                FillSeriesGrid(gridMonthly, v.Monthly);
                FillSeriesGrid(gridWeekly, v.Weekly);
                FillPairsGrid(v.AdX, v.AdY);
                numX0.Value = (decimal)v.ForecastX;
                string src = number == 0 ? "демонстрационные данные (вариант 0)" : "вариант " + number;
                monthlySource = weeklySource = pairsSource = src;
            }
            catch (Exception ex)
            {
                Toast("Загрузка варианта", ex.Message, Tone.Warn);
            }
            finally
            {
                loading = false;
            }
            ReadAllGrids();
            RecalcAll();
        }

        private void FillSeriesGrid(FxTable g, double[] y)
        {
            g.Clear();
            for (int i = 0; i < y.Length; i++) g.AddRow((i + 1).ToString(), Num(y[i]));
        }

        private void FillPairsGrid(double[] x, double[] y)
        {
            gridPairs.Clear();
            for (int i = 0; i < x.Length; i++) gridPairs.AddRow((i + 1).ToString(), Num(x[i]), Num(y[i]));
        }

        private void Grid_Changed(FxTable sender)
        {
            if (loading) return;
            if (sender == gridMonthly) monthlySource = "ручной ввод / редактирование";
            if (sender == gridWeekly) weeklySource = "ручной ввод / редактирование";
            if (sender == gridPairs) pairsSource = "ручной ввод / редактирование";
            for (int i = 0; i < sender.Rows.Count; i++) sender.Rows[i].Cells[0] = (i + 1).ToString();
            sender.Touch();
            ReadAllGrids();
            RecalcAll();
        }

        private static bool IsBlankRow(FxRow r)
        {
            for (int c = 1; c < r.Cells.Length; c++)
                if (!string.IsNullOrWhiteSpace(r.Cells[c])) return false;
            return true;
        }

        /// <summary>Чтение и проверка всех таблиц. Ошибочные ячейки подсвечиваются.</summary>
        private void ReadAllGrids()
        {
            monthly = ReadSeriesGrid(gridMonthly, "Ряд по месяцам", out monthlyError);
            weekly = ReadSeriesGrid(gridWeekly, "Ряд по неделям", out weeklyError);
            ReadPairsGrid();

            var messages = new List<string>();
            if (monthlyError != null) messages.Add(monthlyError);
            if (weeklyError != null) messages.Add(weeklyError);
            if (pairsError != null) messages.Add(pairsError);
            bool errors = messages.Count > 0;
            if (monthly != null) messages.AddRange(DataValidator.Warnings(monthly, "Ряд по месяцам"));
            if (weekly != null) messages.AddRange(DataValidator.Warnings(weekly, "Ряд по неделям"));
            lblDataErrors.Tone = messages.Count == 0 ? Tone.Good : errors ? Tone.Bad : Tone.Warn;
            lblDataErrors.Icon = messages.Count == 0 ? "check" : "warn";
            lblDataErrors.Text = messages.Count == 0 ? "Данные проверены: ошибок нет." : string.Join("   ", messages);

            lblSourceMonthly.Text = "Источник: " + monthlySource;
            lblSourceWeekly.Text = "Источник: " + weeklySource;
            lblSourcePairs.Text = "Источник: " + pairsSource;

            Spark(sparkMonthly, monthly, CFact);
            Spark(sparkWeekly, weekly, CSmooth);
            sparkPairs.Begin();
            if (adX != null) sparkPairs.Points("", adX, adY, CPurple, 6);
            sparkPairs.End();
        }

        private static void Spark(FxChart chart, double[] y, int color)
        {
            chart.Begin();
            if (y != null && y.Length > 1)
            {
                FxSeries s = chart.Line("", Enumerable.Range(1, y.Length).Select(i => (double)i).ToArray(), y, color, false, false);
                s.Area = true;
            }
            chart.End();
        }

        private double[] ReadSeriesGrid(FxTable g, string what, out string error)
        {
            error = null;
            var list = new List<double>();
            for (int i = 0; i < g.Rows.Count; i++)
            {
                FxRow r = g.Rows[i];
                g.SetCellError(r, 1, null);
                if (IsBlankRow(r)) continue;   // незаполненная новая строка не участвует в расчёте
                try
                {
                    double v = DataValidator.ParseValue(r.Cells[1], what + ", строка " + (i + 1));
                    if (v < 0) throw new ForecastException(what + ", строка " + (i + 1) + ": отрицательные продажи.");
                    list.Add(v);
                }
                catch (ForecastException ex)
                {
                    g.SetCellError(r, 1, ex.Message);
                    if (error == null) error = ex.Message;
                }
            }
            if (error == null && list.Count == 0) error = what + ": нет данных.";
            return error == null ? list.ToArray() : null;
        }

        private void ReadPairsGrid()
        {
            pairsError = null;
            var xs = new List<double>();
            var ys = new List<double>();
            for (int i = 0; i < gridPairs.Rows.Count; i++)
            {
                FxRow r = gridPairs.Rows[i];
                gridPairs.SetCellError(r, 1, null);
                gridPairs.SetCellError(r, 2, null);
                if (IsBlankRow(r)) continue;
                bool ok = true;
                double x = 0, y = 0;
                try { x = DataValidator.ParseValue(r.Cells[1], "Реклама, строка " + (i + 1)); }
                catch (ForecastException ex)
                {
                    ok = false;
                    gridPairs.SetCellError(r, 1, ex.Message);
                    if (pairsError == null) pairsError = ex.Message;
                }
                try { y = DataValidator.ParseValue(r.Cells[2], "Продажи, строка " + (i + 1)); }
                catch (ForecastException ex)
                {
                    ok = false;
                    gridPairs.SetCellError(r, 2, ex.Message);
                    if (pairsError == null) pairsError = ex.Message;
                }
                if (ok) { xs.Add(x); ys.Add(y); }
            }
            if (pairsError == null)
            {
                try { DataValidator.CheckPairs(xs.ToArray(), ys.ToArray()); }
                catch (ForecastException ex) { pairsError = ex.Message; }
            }
            adX = pairsError == null ? xs.ToArray() : null;
            adY = pairsError == null ? ys.ToArray() : null;
        }

        private void BtnCsv_Click(object sender, EventArgs e)
        {
            string tag = (string)((Control)sender).Tag;
            using (var dlg = new OpenFileDialog { Filter = "CSV (*.csv;*.txt)|*.csv;*.txt|Все файлы|*.*", Title = "Загрузка данных" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    string name = System.IO.Path.GetFileName(dlg.FileName);
                    loading = true;
                    if (tag == "P")
                    {
                        double[] x, y;
                        CsvLoader.LoadPairs(dlg.FileName, out x, out y);
                        FillPairsGrid(x, y);
                        pairsSource = "файл " + name;
                        EnterAnim.Prepare(gridPairs); EnterAnim.Play(gridPairs);
                    }
                    else
                    {
                        double[] y = CsvLoader.LoadSeries(dlg.FileName);
                        FxTable g = tag == "M" ? gridMonthly : gridWeekly;
                        FillSeriesGrid(g, y);
                        if (tag == "M") monthlySource = "файл " + name; else weeklySource = "файл " + name;
                        EnterAnim.Prepare(g); EnterAnim.Play(g);
                    }
                    loading = false;
                    ReadAllGrids();
                    RecalcAll();
                    Toast("Данные загружены", "Файл " + name + " прочитан, расчёты обновлены.", Tone.Good, 3200);
                }
                catch (ForecastException ex)
                {
                    Toast("Ошибка загрузки CSV", ex.Message, Tone.Bad);
                }
                catch (Exception ex)
                {
                    Toast("Ошибка загрузки CSV", "Не удалось прочитать файл: " + ex.Message, Tone.Bad);
                }
                finally
                {
                    loading = false;
                }
            }
        }

        private void BtnRandom_Click(object sender, EventArgs e)
        {
            try
            {
                var gen = new SeriesGenerator();
                int n = (int)numRandLength.Value;
                string src = "случайные данные (σ шума = " + numRandNoise.Value + ")";
                FxTable changed;
                if (segRandTarget.SelectedIndex == 2)
                {
                    double[] x, y;
                    gen.GeneratePairs(n, (double)numRandXMin.Value, (double)numRandXMax.Value,
                        (double)numRandLevel.Value, (double)numRandGrowth.Value, (double)numRandNoise.Value, out x, out y);
                    FillPairsGrid(x, y);
                    pairsSource = src;
                    changed = gridPairs;
                }
                else
                {
                    double[] y = gen.Generate(SeriesKind.Trend, n, (double)numRandLevel.Value,
                        (double)numRandGrowth.Value, (double)numRandNoise.Value);
                    if (segRandTarget.SelectedIndex == 0) { FillSeriesGrid(gridMonthly, y); monthlySource = src; changed = gridMonthly; }
                    else { FillSeriesGrid(gridWeekly, y); weeklySource = src; changed = gridWeekly; }
                }
                EnterAnim.Prepare(changed);
                EnterAnim.Play(changed);
                ReadAllGrids();
                RecalcAll();
            }
            catch (ForecastException ex)
            {
                Toast("Случайные данные", ex.Message, Tone.Warn);
            }
        }

        /// <summary>Пересчёт всех разделов после изменения данных.</summary>
        private void RecalcAll()
        {
            if (loading) return;
            UpdateMa();
            UpdateSes();
            UpdateRegression();
            UpdateHolt();
            UpdateCompare();
            UpdateAnalysis();
        }
    }
}
