using System;
using System.Drawing;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>
    /// Главное окно: обзор и все задания дня 3 «Выбор оптимального решения с помощью дерева решений».
    /// Расчёты — пространство имён Den3.Core; оформление — библиотека FxKit (папка Fx).
    /// </summary>
    public class MainForm : FxWindow
    {
        /// <summary>Вариант, загружаемый при запуске (номер компьютера).</summary>
        public const int DefaultVariant = 2;

        public readonly Task11Page Page11;
        public readonly Task12Page Page12;
        public readonly Task13Page Page13;
        private readonly Task14Page page14;
        private readonly Analysis21Page page21;
        private readonly Practice22Page page22;
        private readonly FxTable summary = Ui.Table("Задание", "Оптимальное решение", "Ключевые значения, тыс. руб.");
        private bool analysisDirty = true, started;

        /// <summary>Раздел и внутренняя вкладка, открываемые при запуске.</summary>
        public int StartPage, StartTab = -1;

        public MainForm()
        {
            Text = "День 3. Выбор оптимального решения с помощью дерева решений";
            AppTitle = "День 3";
            AppCaption = "дерево решений · вариант " + DefaultVariant;
            AppIcon = "tree";
            SplashTitle = "Практика · день 3";
            SplashSubtitle = "дерево решений · формула Байеса · цена информации · чувствительность";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1440, wa.Width - 24), Math.Min(900, wa.Height - 24));
            MinimumSize = new Size(Math.Min(1180, wa.Width), Math.Min(680, wa.Height));

            Page11 = new Task11Page(this);
            Page12 = new Task12Page(this);
            Page13 = new Task13Page(this);
            page14 = new Task14Page(this);
            page21 = new Analysis21Page(this);
            page22 = new Practice22Page(this);

            AddPage("home", "Обзор", "задания и итоги", "День 3. Дерево решений",
                "Все задания в одном окне; данные варианта " + DefaultVariant + " загружены при запуске", BuildHome());
            AddPage("tree", "Запуск продукта", "задание 1.1", "Задание 1.1. Простое дерево решений",
                "Запуск нового продукта, маркетинговое исследование, формула Байеса и точка безразличия", Page11);
            AddPage("branch", "Стратегии", "задание 1.2", "Задание 1.2. Дерево с несколькими решениями",
                "Стратегии развития и их сочетания в пределах бюджета, риск и чувствительность", Page12);
            AddPage("steps", "Производство", "задание 1.3", "Задание 1.3. Последовательные решения",
                "Тест рынка → размер производства → маркетинг → спрос; цена информации", Page13);
            AddPage("code", "Реализация", "задание 1.4", "Задание 1.4. Программная реализация",
                "Классы узлов, рекурсивная свёртка, рисование GDI+, сохранение и проверка ввода", page14);
            AddPage("trend", "Чувствительность", "задание 2.1", "Задание 2.1. Анализ чувствительности",
                "Критические значения аналитически и делением пополам, метод Монте-Карло", page21);
            AddPage("bulb", "Применение", "задание 2.2", "Задание 2.2. Практические приложения",
                "Своё дерево, критерии Вальда, Сэвиджа, Гурвица и Лапласа, ограничения метода", page22);

            Page11.Changed += DataChanged;
            Page12.Changed += DataChanged;
            Page13.Changed += DataChanged;
            PageShown += OnPageShown;
            Load += delegate
            {
                Page11.LoadVariant(DefaultVariant);
                Page12.LoadVariant(DefaultVariant);
                Page13.LoadVariant(DefaultVariant);
                started = true;
                DataChanged();
                Navigate(StartPage, false);
                if (StartTab >= 0)
                {
                    page21.ShowTab(StartTab);
                    page22.ShowTab(StartTab);
                    foreach (FxSegmented s in new[] { Page11.Tabs, Page12.Tabs, Page13.Tabs })
                        if (s != null && StartTab < s.Items.Length) s.SelectedIndex = StartTab;
                }
            };
        }

        private void DataChanged()
        {
            if (!started) return;
            analysisDirty = true;
            UpdateSummary();
            page14.UpdateChecks();
            if (PageIndex == 5 || PageIndex == 6) RefreshAnalysis();
        }

        private void RefreshAnalysis()
        {
            if (!analysisDirty) return;
            analysisDirty = false;
            page21.UpdateAll();
            page22.UpdateAll();
        }

        private void OnPageShown(int index)
        {
            if (index == 5 || index == 6) RefreshAnalysis();
        }

        private Control BuildHome()
        {
            var hero = new HeroBanner
            {
                Heading = "Практика · день 3",
                Sub = "Выбор оптимального решения с помощью дерева решений: обратная индукция, формула Байеса, цена информации, риск и устойчивость решения.",
                Chips = new[] { "C#", ".NET Framework 4.7.2", "Windows Forms", "GDI+", "вариант " + DefaultVariant }
            };
            var t1 = new LabTile
            {
                Number = "1.1", IconName = "tree", Art = 1,
                Heading = "Запуск продукта",
                Body = "Заказывать ли исследование рынка и запускать ли продукт; пересчёт по Байесу и точка безразличия на ползунке."
            };
            var t2 = new LabTile
            {
                Number = "1.2", IconName = "branch", Art = 0,
                Heading = "Стратегии развития",
                Body = "Три стратегии и их сочетания в пределах бюджета: ожидаемая полезность, σ, вероятность убытка."
            };
            var t3 = new LabTile
            {
                Number = "1.3", IconName = "steps", Art = 3,
                Heading = "Планирование производства",
                Body = "Тест рынка, размер производства и маркетинг: 72 исхода, EVPI и ценность теста."
            };
            t1.Click += delegate { Navigate(1); };
            t2.Click += delegate { Navigate(2); };
            t3.Click += delegate { Navigate(3); };

            summary.Columns[0].Weight = 0.5f;
            summary.Columns[1].Weight = 1.6f;
            summary.Columns[2].Weight = 2.4f;
            summary.Columns[1].Align = summary.Columns[2].Align = StringAlignment.Near;
            summary.WrapCells = true;

            FxBox tiles = FxBox.H(14);
            tiles.Fill(t1).Fill(t2).Fill(t3);
            FxBox page = FxBox.V(14);
            page.Add(hero, 172).Fill(tiles).Add(Ui.Card("Итоги по текущим данным", "check", summary), 180);
            return page;
        }

        private void UpdateSummary()
        {
            summary.Clear();
            LaunchResult a = Page11.Result;
            if (a != null)
                summary.AddRow("1.1", a.Policy,
                    "EV = " + Ui.N(a.Root.Value) + "; с исследованием " + Ui.N(a.EvResearch) + "; макс. цена исследования " + Ui.N(a.MaxPrice) +
                    "; p* = " + Ui.N(a.CriticalP, "0.####"));
            else summary.AddRow("1.1", "ошибка в данных", "").SetTone(RowTone.Bad);
            StrategyResult b = Page12.Result;
            if (b != null)
                summary.AddRow("1.2", b.BestEv.Name + (b.BestEv == b.BestCriterion ? " (по EV и по EV − σ/2)" : "; с учётом риска — " + b.BestCriterion.Name),
                    "EV = " + Ui.N(b.BestEv.Ev) + "; σ = " + Ui.N(b.BestEv.Sigma) + "; P(убытка) = " + Ui.N(b.BestEv.PLoss, "0.###"));
            else summary.AddRow("1.2", "ошибка в данных", "").SetTone(RowTone.Bad);
            ProductionResult c = Page13.Result;
            if (c != null)
                summary.AddRow("1.3", c.Policy,
                    "EV без теста " + Ui.N(c.EvNoTest, "0.###") + "; с тестом " + Ui.N(c.EvTest, "0.###") + "; EVSI = " + Ui.N(c.Evsi, "0.###") +
                    "; EVPI = " + Ui.N(c.Evpi, "0.###") + "; исходов " + c.Leaves);
            else summary.AddRow("1.3", "ошибка в данных", "").SetTone(RowTone.Bad);
        }
    }
}
