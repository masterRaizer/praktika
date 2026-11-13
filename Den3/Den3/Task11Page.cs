using System;
using System.Drawing;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>
    /// Задание 1.1. Простое дерево решений: запуск продукта и маркетинговое исследование.
    /// Расчёт — классы Den3.Core (LaunchProblem, TreeSolver), страница только вводит данные и показывает результат.
    /// </summary>
    public class Task11Page : FxBox
    {
        private readonly MainForm win;
        private readonly ErrorMarks errors = new ErrorMarks();
        private readonly SourceBar bar;
        private readonly FxNumber numCost = Ui.Money(110, 0), numP = Ui.Prob(0.7), numProfit = Ui.Money(375),
            numLoss = Ui.Money(40), numResearch = Ui.Money(10, 0), numFav = Ui.Prob(0.8), numUnfav = Ui.Prob(0.7);
        private readonly FxNumber numRandLo = Ui.Money(50, 0), numRandHi = Ui.Money(400, 0);
        private readonly FxSlider slider = new FxSlider(0, 100, 70);
        private readonly FxLabel lblSlider = new FxLabel("", Fonts.Semi(9.5f), Tone.Text);
        private readonly FxLabel status = Ui.Status();
        private readonly TreeCanvas canvas = new TreeCanvas { Unit = "" };
        private readonly FxTreeList list = new FxTreeList();
        private readonly FxText text = new FxText();
        private readonly FxStat stNo = new FxStat("Без исследования", 0) { Hero = true };
        private readonly FxStat stR = new FxStat("С исследованием", 1);
        private readonly FxStat stPrice = new FxStat("Макс. цена исследования", 2);
        private readonly FxStat stCrit = new FxStat("Точка безразличия p*", 3);
        private bool loading;

        public LaunchData Data { get; private set; }
        public LaunchResult Result { get; private set; }
        public event Action Changed;
        /// <summary>Переключатель вкладок правой карточки.</summary>
        public FxSegmented Tabs;

        public Task11Page(MainForm win)
        {
            this.win = win;
            Vertical = true;
            Gap = 12;

            bar = new SourceBar(MainForm.DefaultVariant);
            bar.VariantChosen += LoadVariant;
            bar.BtnRandom.Click += delegate { RandomFill(); };
            bar.BtnSave.Click += delegate { if (Data != null) Ui.SaveDialog(win, "Сохранить данные задания 1.1", "zadanie_1_1.xml", Data); };
            bar.BtnOpen.Click += delegate
            {
                string name;
                var d = Ui.OpenDialog<LaunchData>(win, "Открыть данные задания 1.1", out name);
                if (d != null) Fill(d, "файл " + name);
            };

            // исходные данные
            FxBox inputs = FxBox.V(6);
            inputs.Add(Ui.Row("Стоимость запуска", numCost));
            inputs.Add(Ui.Row("Вероятность успеха p", numP));
            inputs.Add(Ui.Row("Прибыль при успехе", numProfit));
            inputs.Add(Ui.Row("Убыток при неудаче", numLoss));
            inputs.Add(Ui.Row("Стоимость исследования", numResearch));
            inputs.Add(Ui.Row("P(благопр. | успех)", numFav));
            inputs.Add(Ui.Row("P(неблагопр. | неудача)", numUnfav));
            inputs.Add(new FxLabel("Все суммы — тыс. руб.", Fonts.Body(8.5f), Tone.Mute) { Height = 18 }, 18);
            inputs.Add(Ui.Caption("Пределы случайных денежных значений, тыс. руб."), 16);
            FxBox lim = FxBox.H(8);
            lim.Fill(numRandLo).Fill(numRandHi);
            inputs.Add(lim, 32);
            inputs.Fill(status);
            foreach (FxNumber n in new[] { numCost, numP, numProfit, numLoss, numResearch, numFav, numUnfav })
            {
                n.ValueChanged += delegate { if (!loading) { bar.Source = "ручной ввод"; Recalc(); } };
                n.InputRejected += (s, msg) => errors.Set(s, msg);
            }
            numP.ValueChanged += delegate { if (!loading) SyncSlider(); };
            var dataCard = Ui.Card("Исходные данные · 1.1", "data", inputs);

            // ползунок вероятности (TrackBar)
            slider.Format = v => (v / 100.0).ToString("0.00", Ui.Ru);
            slider.ValueChanged += delegate
            {
                if (loading) return;
                loading = true;
                numP.Value = slider.Value / 100m;
                loading = false;
                bar.Source = "ручной ввод (ползунок)";
                Recalc();
            };
            FxBox sl = FxBox.H(14);
            sl.Add(lblSlider, 250).Fill(slider);
            var sliderCard = new FxCard { Padding = new Padding(16, 10, 16, 10) }.With(sl);

            var btnPng = new FxButton("PNG…", false, "image") { Size = new Size(92, 28) };
            btnPng.Click += delegate { Ui.SavePng(win, canvas, "derevo_1_1.png"); };
            FxSegmented seg;
            var tabs = Ui.TabCard("Дерево решений", "tree", 330, new[] { "Рисунок", "TreeView", "Расчёт и вывод" },
                new Control[] { canvas, list, text }, out seg);
            Tabs = seg;
            var tabsHeader = FxBox.H(8);
            tabsHeader.Add(btnPng, 92).Add(seg, 330);
            tabsHeader.Size = new Size(430, 28);
            tabs.Controls.Remove(seg);
            tabs.WithHeader(tabsHeader);

            FxBox right = FxBox.V(12);
            right.Add(Ui.Tiles(stNo, stR, stPrice, stCrit), 74);
            right.Add(sliderCard, 58);
            right.Fill(tabs);

            FxBox body = FxBox.H(12);
            body.Add(dataCard, 330).Fill(right);
            Add(bar, 56);
            Fill(body);
        }

        public void LoadVariant(int number)
        {
            try
            {
                Fill(VariantData.Get(number).Launch, number == 0 ? "демонстрационные данные (вариант 0)" : "вариант " + number);
                bar.ShowVariant(number);
            }
            catch (DecisionException ex) { win.Toast("Вариант", ex.Message, Tone.Warn); }
        }

        private void Fill(LaunchData d, string source)
        {
            loading = true;
            try
            {
                numCost.Value = (decimal)d.LaunchCost;
                numP.Value = (decimal)d.PSuccess;
                numProfit.Value = (decimal)d.Profit;
                numLoss.Value = (decimal)d.Loss;
                numResearch.Value = (decimal)d.ResearchCost;
                numFav.Value = (decimal)d.PFavGivenSuccess;
                numUnfav.Value = (decimal)d.PUnfavGivenFailure;
                SyncSlider();
            }
            finally { loading = false; }
            bar.Source = source;
            errors.ClearAll();
            Recalc();
        }

        private void RandomFill()
        {
            var d = new RandomData().Launch((double)numRandLo.Value, (double)numRandHi.Value);
            Fill(d, "случайные данные (" + Ui.N((double)numRandLo.Value) + "–" + Ui.N((double)numRandHi.Value) + " тыс. руб.)");
        }

        private void SyncSlider()
        {
            bool keep = loading;
            loading = true;
            slider.Value = (int)Math.Round(numP.Value * 100);
            loading = keep;
        }

        private LaunchData Read()
        {
            return new LaunchData
            {
                LaunchCost = (double)numCost.Value,
                PSuccess = (double)numP.Value,
                Profit = (double)numProfit.Value,
                Loss = (double)numLoss.Value,
                ResearchCost = (double)numResearch.Value,
                PFavGivenSuccess = (double)numFav.Value,
                PUnfavGivenFailure = (double)numUnfav.Value
            };
        }

        public void Recalc()
        {
            LaunchData d = Read();
            lblSlider.Text = "Вероятность успеха p = " + d.PSuccess.ToString("0.00", Ui.Ru);
            try
            {
                errors.Set(numP, d.PSuccess < 0 || d.PSuccess > 1 ? "Вероятность должна быть от 0 до 1." : null);
                errors.Clear(new Control[] { numCost, numProfit, numLoss, numResearch, numFav, numUnfav });
                LaunchResult r = LaunchProblem.Solve(d);
                Data = d;
                Result = r;
                Ui.SetStatus(status, null, "Данные проверены: вероятности в [0; 1], в каждом случайном узле сумма равна 1.");
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

        private void Show(LaunchData d, LaunchResult r)
        {
            canvas.SetTree(r.Root);
            list.SetTree(r.Root);
            stNo.Set(r.EvNoResearch, 2, " т.р.");
            stNo.Sub = r.LaunchNoResearch ? "запускать" : "не запускать";
            stR.Set(r.EvResearch, 2, " т.р.");
            stR.Sub = r.ResearchWorth ? "исследование выгодно" : "исследование не окупается";
            stPrice.Set(r.MaxPrice, 2, " т.р.");
            stPrice.Sub = "EVSI; цена сейчас " + Ui.N(d.ResearchCost);
            stCrit.Set(r.CriticalP, 4);
            stCrit.Sub = d.PSuccess > r.CriticalP ? "p > p*: запуск выгоден" : "p ≤ p*: запуск невыгоден";

            string bestFirst = r.Root.Best == 0 ? "не заказывать исследование" : "заказать исследование";
            text.Text =
                "ДЕРЕВО И ОЖИДАЕМЫЕ ЗНАЧЕНИЯ\n" +
                "Запуск без исследования: EV = p·Π − (1 − p)·L − C = " + Ui.N(d.PSuccess) + "·" + Ui.N(d.Profit) + " − " +
                Ui.N(1 - d.PSuccess) + "·" + Ui.N(d.Loss) + " − " + Ui.N(d.LaunchCost) + " = " + Ui.N(r.EvLaunch) + " тыс. руб.\n" +
                "Не запускать: EV = 0. Лучшее решение без исследования — " + (r.LaunchNoResearch ? "запускать" : "не запускать") +
                ", EV = " + Ui.N(r.EvNoResearch) + " тыс. руб.\n" +
                "ФОРМУЛА БАЙЕСА\n" +
                "P(благоприятный) = p·" + Ui.N(d.PFavGivenSuccess) + " + (1 − p)·" + Ui.N(1 - d.PUnfavGivenFailure) + " = " + Ui.P(r.PFav) +
                "; P(неблагоприятный) = " + Ui.P(r.PUnfav) + ".\n" +
                "P(успех | благоприятный) = " + Ui.N(d.PSuccess) + "·" + Ui.N(d.PFavGivenSuccess) + " / " + Ui.P(r.PFav) + " = " + Ui.P(r.PSuccessFav) + ".\n" +
                "P(успех | неблагоприятный) = " + Ui.N(d.PSuccess) + "·" + Ui.N(1 - d.PFavGivenSuccess) + " / " + Ui.P(r.PUnfav) + " = " + Ui.P(r.PSuccessUnfav) + ".\n" +
                "После благоприятного прогноза: EV запуска = " + Ui.N(r.EvLaunchFav) + " → " + (r.LaunchFav ? "запускать" : "не запускать") + ".\n" +
                "После неблагоприятного прогноза: EV запуска = " + Ui.N(r.EvLaunchUnfav) + " → " + (r.LaunchUnfav ? "запускать" : "не запускать") + ".\n" +
                "С исследованием: EV = " + Ui.P(r.PFav) + "·" + Ui.N(Math.Max(0, r.EvLaunchFav)) + " + " + Ui.P(r.PUnfav) + "·" +
                Ui.N(Math.Max(0, r.EvLaunchUnfav)) + " − " + Ui.N(d.ResearchCost) + " = " + Ui.N(r.EvResearch) + " тыс. руб.\n" +
                "ЦЕНА ИНФОРМАЦИИ\n" +
                "Ценность выборочной информации EVSI = " + Ui.N(r.EvResearchGross) + " − " + Ui.N(r.EvNoResearch) + " = " + Ui.N(r.Evsi) +
                " тыс. руб. — это наибольшая разумная цена исследования.\n" +
                "Ценность совершенной информации EVPI = " + Ui.N(r.EvPerfect) + " − " + Ui.N(r.EvNoResearch) + " = " + Ui.N(r.Evpi) + " тыс. руб.\n" +
                (r.Evsi <= 1e-9
                    ? "Исследование не меняет решения (при любом прогнозе выбирается одно и то же), поэтому его ценность равна нулю и платить за него не стоит.\n"
                    : "Исследование " + (r.ResearchWorth ? "стоит заказать: EVSI больше его стоимости " : "не стоит заказывать: EVSI не превышает его стоимость ") + Ui.N(d.ResearchCost) + " тыс. руб.\n") +
                "ТОЧКА БЕЗРАЗЛИЧИЯ\n" +
                "p·Π − (1 − p)·L − C = 0  ⇒  p* = (C + L) / (Π + L) = (" + Ui.N(d.LaunchCost) + " + " + Ui.N(d.Loss) + ") / (" +
                Ui.N(d.Profit) + " + " + Ui.N(d.Loss) + ") = " + Ui.P(r.CriticalP) + ".\n" +
                "Проверка: передвиньте ползунок через p* — решение без исследования меняется с «не запускать» на «запускать», дерево пересчитывается сразу.\n" +
                "ВЫВОД\n" +
                "Оптимально: " + bestFirst + "; путь — " + r.Policy + ". Ожидаемая полезность " + Ui.N(r.Root.Value) + " тыс. руб.";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) errors.Dispose();
            base.Dispose(disposing);
        }
    }
}
