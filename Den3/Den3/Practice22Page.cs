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
    /// Задание 2.2. Практические приложения: собственное дерево с двумя уровнями решений, критерии выбора
    /// в условиях неопределённости для задания 1.3, ограничения метода и способы улучшения решений.
    /// </summary>
    public class Practice22Page : FxBox
    {
        private readonly MainForm win;
        private readonly TreeCanvas canvas = new TreeCanvas();
        private readonly FxNumber numPHigh = Ui.Prob(0.5), numBig = Ui.Money(150, 0), numExpand = Ui.Money(110, 0), numKeep = Ui.Prob(0.8);
        private readonly FxText cafeText = new FxText();
        private readonly FxTable gridCrit = Ui.Table("Альтернатива", "Низкий", "Средний", "Высокий", "Вальд", "Сэвидж", "Гурвиц 0,6", "Лаплас", "EV");
        private readonly FxText critText = new FxText();
        private readonly FxText limitText = new FxText();
        private FxSegmented seg;

        public Practice22Page(MainForm win)
        {
            this.win = win;
            Vertical = true;
            Gap = 12;

            // собственный пример
            foreach (FxNumber n in new[] { numPHigh, numBig, numExpand, numKeep })
                n.ValueChanged += delegate { UpdateCafe(); };
            FxBox prm = FxBox.V(6);
            prm.Add(Ui.Row("P(высокий спрос, 1-й год)", numPHigh, 104));
            prm.Add(Ui.Row("Большая кофейня, вложения", numBig, 104));
            prm.Add(Ui.Row("Расширение малой, вложения", numExpand, 104));
            prm.Add(Ui.Row("P(спрос сохранится)", numKeep, 104));
            prm.Fill(cafeText);
            var btnPng = new FxButton("PNG…", false, "image") { Size = new Size(92, 28) };
            btnPng.Click += delegate { Ui.SavePng(win, canvas, "derevo_kofeinya.png"); };
            FxBox cafe = FxBox.H(12);
            cafe.Add(Ui.Card("Параметры примера", "data", prm), 330);
            cafe.Fill(Ui.Card("Своё дерево: открытие кофейни (два уровня решений)", "tree", canvas, btnPng));

            gridCrit.Columns[0].Weight = 2.4f;
            gridCrit.Columns[0].Align = StringAlignment.Near;
            FxBox crit = FxBox.V(12);
            crit.Add(Ui.Card("Задание 1.3 без учёта вероятностей спроса: прибыль, тыс. руб.", "scale", gridCrit), 32 + 30 + 7 * 28 + 30);
            crit.Fill(Ui.Card("Сравнение с деревом решений", "bulb", critText));

            var pager = new FxPager { VerticalSlide = false, Duration = 320 };
            pager.AddPage(cafe);
            pager.AddPage(crit);
            pager.AddPage(Ui.Card("Применение, ограничения и улучшения", "bulb", limitText));
            seg = new FxSegmented("Примеры и своё дерево", "Критерии неопределённости", "Ограничения и улучшения") { Size = new Size(560, 32) };
            seg.SelectedIndexChanged += delegate { pager.ShowPage(seg.SelectedIndex); };
            pager.ShowPage(0, false);
            var top = new FxCard { Padding = new Padding(14, 8, 14, 8) }.With(seg);
            Add(top, 50);
            Fill(pager);
            UpdateCafe();
        }

        public void ShowTab(int i) { if (i >= 0 && i < 3) seg.SelectedIndex = i; }

        private void UpdateCafe()
        {
            var d = new CafeData
            {
                PHigh = (double)numPHigh.Value,
                BigCost = (double)numBig.Value,
                ExpandCost = (double)numExpand.Value,
                PKeep = (double)numKeep.Value
            };
            try
            {
                Node root = CafeProblem.Build(d);
                TreeSolver.Solve(root);
                canvas.SetTree(root);
                Node big = root.Children[0], small = root.Children[1];
                Node up = small.Children[0], down = small.Children[1];
                cafeText.Text =
                    "ПРИМЕРЫ ЗАДАЧ\n" +
                    "Запуск продукта и заказ исследования рынка; выбор мощности завода; покупка или аренда оборудования; " +
                    "медицинская диагностика (обследование → лечение); выход на тендер; инвестиционные проекты с поэтапным финансированием.\n" +
                    "РАСЧЁТ ПРИМЕРА\n" +
                    "Большая кофейня: EV = " + Ui.N(big.BranchValue) + ".\n" +
                    "Малая: после удачного года — " + up.Children[up.Best].Branch.ToLowerInvariant() + " (" + Ui.N(up.Value) +
                    "), после неудачного — " + down.Children[down.Best].Branch.ToLowerInvariant() + " (" + Ui.N(down.Value) + "); EV = " + Ui.N(small.BranchValue) + ".\n" +
                    "Решение: " + TreeSolver.Policy(root).ToLowerInvariant() + ", EV = " + Ui.N(root.Value) + " тыс. руб. " +
                    "Гибкость (возможность расширить позже) имеет ценность: малая кофейня с опционом расширения сравнивается с большой.";
            }
            catch (Exception ex) { cafeText.Text = "Ошибка: " + ex.Message; }
        }

        public void UpdateAll()
        {
            UpdateCriteria();
            UpdateLimits();
        }

        private void UpdateCriteria()
        {
            ProductionData d = win.Page13.Data;
            ProductionResult r = win.Page13.Result;
            gridCrit.Clear();
            if (d == null || r == null) { critText.Text = "Исправьте данные задания 1.3."; return; }
            var names = new List<string>();
            var m = new double[6, 3];
            for (int s = 0; s < 3; s++)
                for (int k = 0; k < 2; k++)
                {
                    names.Add(ProductionProblem.AltName(s, k));
                    for (int j = 0; j < 3; j++) m[s * 2 + k, j] = d.ProfitOf(s, k, j);
                }
            CriteriaResult c = UncertaintyCriteria.Compute(names.ToArray(), m, 0.6);
            int bestEv = r.BestSize * 2 + r.BestMkt;
            for (int i = 0; i < 6; i++)
            {
                FxRow row = gridCrit.AddRow(names[i], Ui.N(m[i, 0]), Ui.N(m[i, 1]), Ui.N(m[i, 2]),
                    Ui.N(c.Wald[i]), Ui.N(c.Savage[i]), Ui.N(c.Hurwicz[i]), Ui.N(c.Laplace[i], "0.##"), Ui.N(r.EvTable[i / 2, i % 2], "0.##"));
                if (i == c.BestWald) gridCrit.SetCellTone(row, 4, RowTone.Good);
                if (i == c.BestSavage) gridCrit.SetCellTone(row, 5, RowTone.Good);
                if (i == c.BestHurwicz) gridCrit.SetCellTone(row, 6, RowTone.Good);
                if (i == c.BestLaplace) gridCrit.SetCellTone(row, 7, RowTone.Good);
                if (i == bestEv) gridCrit.SetCellTone(row, 8, RowTone.Accent);
            }
            gridCrit.AddRow("Лучшее по критерию", "", "", "", "max min", "min max сожал.", "max", "max", "max").SetTone(RowTone.Total);
            Func<int, string> nm = i => "«" + names[i].ToLowerInvariant() + "»";
            critText.Text =
                "КРИТЕРИИ\n" +
                "Вальд (максимин, осторожный): " + nm(c.BestWald) + ", гарантированно " + Ui.N(c.Wald[c.BestWald]) + " тыс. руб.\n" +
                "Сэвидж (минимакс сожаления): " + nm(c.BestSavage) + ", наибольшее сожаление " + Ui.N(c.Savage[c.BestSavage]) + ".\n" +
                "Гурвиц, α = 0,6 (0,6·max + 0,4·min): " + nm(c.BestHurwicz) + ", " + Ui.N(c.Hurwicz[c.BestHurwicz]) + ".\n" +
                "Лаплас (все состояния равновероятны): " + nm(c.BestLaplace) + ", " + Ui.N(c.Laplace[c.BestLaplace], "0.##") + ".\n" +
                "Дерево решений (с вероятностями спроса): " + nm(bestEv) + ", EV = " + Ui.N(r.EvNoTest, "0.##") + ".\n" +
                "КОГДА ЧТО ИСПОЛЬЗОВАТЬ\n" +
                "Дерево решений применяют, когда вероятности состояний известны или их можно оценить, а решения принимаются последовательно, " +
                "с возможностью получить информацию (тест, исследование) между этапами. Критерии неопределённости не используют вероятности: " +
                "Вальд подходит для крайне осторожного ЛПР, Сэвидж — когда важно не упустить выгоду, Гурвиц — компромисс с выбранной степенью оптимизма, " +
                "Лаплас — при полном отсутствии сведений о вероятностях. Различие рекомендаций показывает, что выбор зависит от отношения к риску.";
        }

        private void UpdateLimits()
        {
            ProductionResult r13 = win.Page13.Result;
            StrategyResult r12 = win.Page12.Result;
            int leaves = r13 != null ? r13.Leaves : 72;
            string ce = "";
            if (r12 != null)
            {
                const double R = 200;
                ce = "Пример для задания 1.2 (U(x) = 1 − e^(−x/R), R = " + Ui.N(R) + " тыс. руб.): " +
                     string.Join("; ", r12.Alternatives.Where(a => a.Feasible && a.Members.Length > 0)
                         .Select(a => a.Name + " — " + Ui.N(RiskUtility.CertaintyEquivalent(TreeSolver.Distribution(a.Node), R)))) +
                     ". Детерминированный эквивалент меньше EV тем сильнее, чем больше риск.";
            }
            limitText.Text =
                "ОГРАНИЧЕНИЯ МЕТОДА\n" +
                "Рост дерева: в задании 1.3 сейчас " + leaves + " конечных узлов. Ещё один этап с тремя исходами умножает их число в 3 раза: " +
                leaves + "·3 = " + leaves * 3 + " (рост в 3 раза; при k этапах — в 3^k раз). Дерево быстро становится необозримым («проклятие размерности»).\n" +
                "Вероятности и выигрыши — оценки: ошибка в них меняет решение (см. анализ чувствительности 2.1, где решение о тесте неустойчиво).\n" +
                "Критерий EV не учитывает отношение к риску: при одинаковом EV вариант с большим разбросом может быть неприемлем.\n" +
                "Предполагается, что исходы независимы, а набор альтернатив и состояний полон; непрерывные величины приходится дискретизировать.\n" +
                "СПОСОБЫ УЛУЧШЕНИЯ РЕШЕНИЙ\n" +
                "Учёт отношения к риску: функция полезности вместо денег, критерий «EV − λ·σ», ограничение на вероятность убытка (как в задании 1.2).\n" + ce + "\n" +
                "Уточнение вероятностей по новым данным: формула Байеса (как в заданиях 1.1 и 1.3) — каждое наблюдение пересчитывает априорные вероятности в апостериорные.\n" +
                "Анализ чувствительности и Монте-Карло: проверять, насколько далеко текущие оценки от точек смены решения.\n" +
                "Сокращение дерева: отсечение заведомо худших ветвей, объединение одинаковых поддеревьев, моделирование вместо полного перебора.";
        }
    }
}
