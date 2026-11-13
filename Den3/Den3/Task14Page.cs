using System;
using System.Drawing;
using System.Windows.Forms;
using Den3.Core;
using FxKit;

namespace Den3
{
    /// <summary>Задание 1.4. Программная реализация: классы, алгоритм свёртки, проверка ввода.</summary>
    public class Task14Page : FxBox
    {
        private readonly MainForm win;
        private readonly FxTable gridChecks = Ui.Table("Задание", "Проверка", "Результат");
        private readonly FxText text = new FxText { FontSize = 9f };
        private readonly TreeCanvas demo = new TreeCanvas();

        public Task14Page(MainForm win)
        {
            this.win = win;
            Vertical = false;
            Gap = 12;
            gridChecks.Columns[0].Weight = 0.6f;
            gridChecks.Columns[1].Weight = 1.6f;
            gridChecks.Columns[2].Weight = 2f;
            gridChecks.Columns[1].Align = gridChecks.Columns[2].Align = StringAlignment.Near;
            gridChecks.WrapCells = true;

            text.Text =
                "КЛАССЫ УЗЛОВ\n" +
                "Файл Core/DecisionTree.cs.\n" +
                "Node: общий базовый класс — подпись ветви, вероятность ветви, денежный поток на ветви, список потомков, ожидаемое значение.\n" +
                "DecisionNode (квадрат): значение = max по ветвям (поток + значение потомка); запоминается выбранная ветвь.\n" +
                "ChanceNode (круг): значение = Σ p·(поток + значение потомка).\n" +
                "OutcomeNode (треугольник): значение = конечный результат.\n" +
                "АЛГОРИТМ СВЁРТКИ\n" +
                "TreeSolver.Roll(node) — один рекурсивный метод обратной индукции для дерева любой глубины: сначала вычисляются потомки, затем сам узел. " +
                "Mark() отмечает оптимальный путь: в узле решения — выбранная ветвь, в случайном — все ветви. Тем же алгоритмом решены задания 1.1, 1.2, 1.3 и пример из 2.2.\n" +
                "Distribution() строит распределение итога при выбранной стратегии — из него считаются σ и вероятность убытка (задание 1.2).\n" +
                "РИСОВАНИЕ ДЕРЕВА\n" +
                "Файл TreeCanvas.cs. " +
                "Дерево рисуется в событии Paint средствами GDI+: квадрат, круг, треугольник; на ветвях — вероятности и потоки, у узлов — EV; оптимальный путь выделен цветом, " +
                "отвергнутые ветви перечёркнуты. Рисунок вписывается в окно и масштабируется при изменении его размера; колесо мыши — увеличение. Кнопка «PNG…» сохраняет рисунок в файл.\n" +
                "ДАННЫЕ\n" +
                "Каждое задание: выбор варианта (0 — демонстрационный, 2 — вариант этого компьютера), ручной ввод в числовых полях и таблицах, " +
                "кнопка «Случайные данные» (пределы задаются на форме, вероятности в каждом узле дают сумму 1), сохранение и загрузка XML (XmlSerializer). " +
                "Над расчётом всегда показан источник данных.\n" +
                "ПРОВЕРКА ВВОДА\n" +
                "TreeSolver.Check(): вероятности от 0 до 1, сумма вероятностей ветвей каждого случайного узла равна 1, денежные значения — числа. " +
                "Ошибки показываются компонентом ErrorProvider у поля или таблицы, ошибочные ячейки подсвечиваются, текст ошибки выводится в строке состояния. " +
                "Все расчёты выполняются в try/catch, необработанные исключения перехватываются в Program.cs — программа не завершается аварийно.";

            var btnBad = new FxButton("Проверить дерево с ошибкой", false, "warn").AutoWidth();
            btnBad.Click += delegate { CheckBadTree(); };
            var right = FxBox.V(12);
            right.Fill(Ui.Card("Проверка исходных данных заданий", "check", gridChecks), 1f);
            right.Fill(Ui.Card("Узлы трёх видов: пример свёртки", "tree", demo, btnBad), 1f);
            Fill(Ui.Card("Программная реализация", "code", text), 1.15f);
            Fill(right, 1f);

            var root = new DecisionNode("Решение");
            var ch = root.Add(new ChanceNode("Событие", "Вариант А") { Flow = -20 });
            ch.Add(new OutcomeNode("Удача", 100), 0.6);
            ch.Add(new OutcomeNode("Неудача", 10), 0.4);
            root.Add(new OutcomeNode("Вариант Б", 40));
            TreeSolver.Solve(root);
            demo.ShowTotals = false;
            demo.SetTree(root);
        }

        /// <summary>Пример неверного дерева: сумма вероятностей случайного узла 0,9 — проверка должна это обнаружить.</summary>
        private void CheckBadTree()
        {
            var root = new DecisionNode("Решение");
            var ch = root.Add(new ChanceNode("Событие", "Вариант А"));
            ch.Add(new OutcomeNode("Удача", 100), 0.6);
            ch.Add(new OutcomeNode("Неудача", 10), 0.3);
            root.Add(new OutcomeNode("Вариант Б", 40));
            try
            {
                TreeSolver.Solve(root);
                win.Toast("Проверка", "Ошибок не найдено.", Tone.Good);
            }
            catch (DecisionException ex)
            {
                win.Toast("Проверка обнаружила ошибку", ex.Message, Tone.Bad);
            }
        }

        public void UpdateChecks()
        {
            gridChecks.Clear();
            AddCheck("1.1", win.Page11.Result != null ? win.Page11.Result.Root : null, win.Page11.Result != null);
            AddCheck("1.2", win.Page12.Result != null ? win.Page12.Result.Root : null, win.Page12.Result != null);
            AddCheck("1.3", win.Page13.Result != null ? win.Page13.Result.Root : null, win.Page13.Result != null);
        }

        private void AddCheck(string task, Node root, bool ok)
        {
            if (!ok || root == null)
            {
                gridChecks.AddRow(task, "Исходные данные", "есть ошибки — см. строку состояния задания").SetTone(RowTone.Bad);
                return;
            }
            int chance = TreeSolver.CountNodes(root, NodeKind.Chance);
            var errs = TreeSolver.Check(root);
            gridChecks.AddRow(task, "Случайных узлов: " + chance + ", исходов: " + TreeSolver.CountLeaves(root),
                errs.Count == 0 ? "вероятности в [0; 1], суммы равны 1, значения — числа" : errs[0]).SetTone(errs.Count == 0 ? RowTone.Good : RowTone.Bad);
        }
    }
}
