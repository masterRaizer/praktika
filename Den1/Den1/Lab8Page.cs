using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    /// <summary>
    /// Лабораторная работа № 8. Организация обработки исключений.
    /// Простые арифметические операции над двумя числами с обработкой
    /// исключительных ситуаций при вводе чисел и при делении.
    /// </summary>
    public class Lab8Page : FxBox
    {
        private readonly FxTextBox txtNum1 = new FxTextBox { Placeholder = "например, 12,5" };
        private readonly FxTextBox txtNum2 = new FxTextBox { Placeholder = "например, 0" };
        private readonly FxConsole txtResult = new FxConsole { ShowTime = false, EmptyText = "введите числа и нажмите «Вычислить»" };
        private readonly FlowDiagram flow = new FlowDiagram();
        private readonly FxStat stSum = new FxStat("Сумма  a + b", 0);
        private readonly FxStat stMul = new FxStat("Произведение  a × b", 2);
        private readonly FxStat stSub = new FxStat("Разность  a − b", 5);
        private readonly FxStat stDiv = new FxStat("Частное  a / b", 1) { Hero = true };

        public Lab8Page()
        {
            Vertical = true;
            Gap = 12;

            var btnCalc = new FxButton("Вычислить", true, "calc");
            var btnClear = new FxButton("Очистить", false, "trash");
            btnCalc.Click += btnCalc_Click;
            btnClear.Click += btnClear_Click;
            txtNum1.EnterPressed += delegate { txtNum2.FocusBox(); };
            txtNum2.EnterPressed += btnCalc_Click;

            var input = new FxCard("Исходные данные", "calc");
            FxBox ib = FxBox.V(8);
            ib.Add(Field("Первое число", txtNum1), 54).Add(Field("Второе число", txtNum2), 54);
            FxBox buttons = FxBox.H(8);
            buttons.Fill(btnCalc, 1.4f).Fill(btnClear, 1f);
            ib.Add(buttons, 36);
            ib.Fill(new FxLabel("Введите буквы или нуль во второе поле — исключение будет перехвачено.",
                Fonts.Body(8.5f), Tone.Mute, true));
            input.With(ib);

            FxBox tilesTop = FxBox.H(10), tilesBottom = FxBox.H(10), tiles = FxBox.V(10);
            tilesTop.Fill(stSum).Fill(stMul);
            tilesBottom.Fill(stSub).Fill(stDiv);
            tiles.Fill(tilesTop).Fill(tilesBottom);
            foreach (FxStat s in new[] { stSum, stMul, stSub, stDiv }) s.SetText("—");

            FxBox top = FxBox.H(12);
            top.Add(input, 340).Fill(new FxCard("Результаты", "bars").With(tiles));

            Add(top, 264);
            Add(new FxCard("Ход выполнения: try → catch → finally", "shield").With(flow), 150);
            Fill(new FxCard("Протокол вычислений", "terminal").With(txtResult));
        }

        private static FxBox Field(string caption, Control input)
        {
            FxBox b = FxBox.V(2);
            b.Add(new FxLabel(caption, Fonts.Semi(8f), Tone.Mute) { Height = 16 }, 16).Add(input, 36);
            return b;
        }

        /// <summary>
        /// Читает число из текстового поля. При ошибке ввода подставляет
        /// значение по умолчанию и записывает сообщение в протокол.
        /// </summary>
        private double ReadNumber(FxTextBox box, string name, double defaultValue, List<FlowStep> steps)
        {
            double value;
            try
            {
                // Защищённый блок: Parse генерирует исключение при неверном вводе.
                value = double.Parse(box.Value.Trim());
                steps.Add(new FlowStep("try", "чтение " + name + " числа: " + value, 0));
            }
            catch (FormatException)
            {
                value = defaultValue;
                txtResult.Add("Неправильный формат числа! В качестве значения " + name +
                              " числа будет " + defaultValue, LogKind.Error);
                steps.Add(new FlowStep("catch", "FormatException: вместо " + name + " числа взято " + defaultValue, 1));
                box.Shake();
            }
            catch (OverflowException)
            {
                value = defaultValue;
                txtResult.Add("Слишком большое число! В качестве значения " + name +
                              " числа будет " + defaultValue, LogKind.Error);
                steps.Add(new FlowStep("catch", "OverflowException: вместо " + name + " числа взято " + defaultValue, 1));
                box.Shake();
            }

            box.Value = value.ToString();
            return value;
        }

        /// <summary>
        /// Деление. Для вещественных чисел деление на нуль не вызывает
        /// исключения (получается бесконечность), поэтому исключение
        /// генерируется вручную оператором throw.
        /// </summary>
        private static double Divide(double a, double b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException("Нельзя делить на нуль!");
            }
            return a / b;
        }

        private static void Show(FxStat stat, double value)
        {
            stat.Tone = Tone.Accent2;
            stat.Sub = "";
            if (Math.Abs(value) >= 1e9 || double.IsInfinity(value)) stat.SetText(value.ToString("G6"));
            else stat.Set(value, Math.Abs(value - Math.Round(value)) < 1e-9 ? 0 : 3);
        }

        private void btnCalc_Click(object sender, EventArgs e)
        {
            var steps = new List<FlowStep>();
            txtResult.Add("— новый расчёт —");

            double num1 = ReadNumber(txtNum1, "первого", 1, steps);
            double num2 = ReadNumber(txtNum2, "второго", 2, steps);

            double sum = num1 + num2;
            double multiply = num1 * num2;
            double sub = num1 - num2;

            txtResult.Add(num1 + " + " + num2 + " = " + sum);
            txtResult.Add(num1 + " * " + num2 + " = " + multiply);
            txtResult.Add(num1 + " - " + num2 + " = " + sub);
            steps.Add(new FlowStep("расчёт", "сумма, произведение и разность найдены", 0));
            Show(stSum, sum);
            Show(stMul, multiply);
            Show(stSub, sub);

            try
            {
                double divide = Divide(num1, num2);
                txtResult.Add(num1 + " / " + num2 + " = " + divide);
                steps.Add(new FlowStep("try", "деление выполнено", 0));
                Show(stDiv, divide);
            }
            catch (DivideByZeroException ex)
            {
                txtResult.Add(num1 + " / " + num2 + " — " + ex.Message, LogKind.Error);
                steps.Add(new FlowStep("catch", "DivideByZeroException: " + ex.Message, 1));
                stDiv.SetText("—");
                stDiv.Sub = ex.Message;
                txtNum2.Shake();
            }
            finally
            {
                // Блок finally выполняется всегда - и при ошибке, и без неё.
                txtResult.Add("Вычисления завершены.", LogKind.Good);
                steps.Add(new FlowStep("finally", "выполняется всегда", 2));
            }

            flow.Play(steps);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtNum1.Value = "";
            txtNum2.Value = "";
            txtResult.Clear();
            flow.Reset();
            foreach (FxStat s in new[] { stSum, stMul, stSub, stDiv }) { s.SetText("—"); s.Sub = ""; }
            txtNum1.FocusBox();
        }
    }
}
