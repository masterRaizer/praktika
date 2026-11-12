using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    /// <summary>Поле времени «часы : минуты» из двух числовых полей.</summary>
    public class TimeBox : FxBox
    {
        private readonly FxNumber hours = new FxNumber(0, 23, 9, 0, 1) { Digits = 2 };
        private readonly FxNumber minutes = new FxNumber(0, 59, 0, 0, 5) { Digits = 2 };

        public TimeBox(int hour, int minute)
        {
            Vertical = false;
            Gap = 2;
            hours.Value = hour;
            minutes.Value = minute;
            Fill(hours).Add(new FxLabel(":", Fonts.Bold(12f), Tone.Dim) { Align = StringAlignment.Center }, 12).Fill(minutes);
        }

        /// <summary>Выбранное время (сегодняшняя дата, как у DateTimePicker).</summary>
        public DateTime Value
        {
            get { return DateTime.Today.AddHours((double)hours.Value).AddMinutes((double)minutes.Value); }
        }
    }

    /// <summary>
    /// Лабораторная работа № 9. Применение отладочных классов в проекте.
    /// Программа "Учёт рабочего времени" (тема из технического задания).
    /// Для отладки используются классы System.Diagnostics.Debug и Trace,
    /// их вывод показывается прямо на форме через свой слушатель.
    /// </summary>
    public class Lab9Page : FxBox
    {
        private readonly WorkTimeJournal journal = new WorkTimeJournal();
        private readonly LogTraceListener listener;
        private readonly FxWindow window;

        private readonly FxTextBox txtEmployee = new FxTextBox { Placeholder = "Иванов Иван Иванович" };
        private readonly TimeBox dtpStart = new TimeBox(9, 0);
        private readonly TimeBox dtpEnd = new TimeBox(18, 0);
        private readonly ShiftTimeline lstRecords = new ShiftTimeline();
        private readonly FxConsole txtLog = new FxConsole { EmptyText = "здесь появится вывод Debug и Trace" };
        private readonly FxStat lblTotal = new FxStat("Всего отработано", 0) { Hero = true };
        private readonly FxStat lblCount = new FxStat("Записей в журнале", 2);
        private readonly FxStat lblOver = new FxStat("Смен дольше 8 часов", 3);
        private int overtime;

        public Lab9Page(FxWindow window)
        {
            this.window = window;
            Vertical = true;
            Gap = 12;

            var btnAdd = new FxButton("Добавить запись", true, "plus");
            var btnClearLog = new FxButton("Очистить", false, "trash") { Size = new Size(110, 28) };
            btnAdd.Click += btnAdd_Click;
            btnClearLog.Click += btnClearLog_Click;
            txtEmployee.EnterPressed += btnAdd_Click;

            var input = new FxCard("Новая запись", "clock");
            FxBox row = FxBox.H(12);
            row.Fill(Field("Сотрудник (ФИО)", txtEmployee));
            row.Add(Field("Начало работы", dtpStart), 196);
            row.Add(Field("Конец работы", dtpEnd), 196);
            row.Add(Field("", btnAdd), 190);
            input.With(row);

            FxBox stats = FxBox.V(10);
            stats.Fill(lblTotal).Fill(lblCount).Fill(lblOver);
            lblTotal.Set(0, 2, " ч");
            lblCount.Set(0, 0);
            lblOver.Set(0, 0);
            var btnDemo = new FxButton("Пример", false, "play") { Size = new Size(104, 28) };
            btnDemo.Click += btnDemo_Click;
            FxCard records = new FxCard("Записи учёта рабочего времени", "data").With(lstRecords);
            records.WithHeader(btnDemo);
            FxBox middle = FxBox.H(12);
            middle.Fill(records).Add(stats, 230);

            var log = new FxCard("Отладочный вывод классов Debug и Trace", "terminal").With(txtLog);
            log.WithHeader(btnClearLog);

            Add(input, 124);
            Fill(middle, 1.15f);
            Fill(log, 1f);

            // Подключаем слушатель: всё, что пишут Debug и Trace, попадёт в журнал на форме
            // (и одновременно в окно "Вывод" Visual Studio).
            listener = new LogTraceListener(txtLog);
            Trace.Listeners.Add(listener);
        }

        private static FxBox Field(string caption, Control input)
        {
            FxBox b = FxBox.V(2);
            b.Add(new FxLabel(caption, Fonts.Semi(8f), Tone.Mute) { Height = 16 }, 16).Add(input, 36);
            return b;
        }

        /// <summary>Страница открыта.</summary>
        public void OnOpened()
        {
            Trace.WriteLine("Форма учёта рабочего времени открыта.");
        }

        /// <summary>Страница закрыта.</summary>
        public void OnClosed()
        {
            Trace.WriteLine("Форма учёта рабочего времени закрыта.");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Trace.Listeners.Remove(listener);
            base.Dispose(disposing);
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddRecord(txtEmployee.Value, dtpStart.Value, dtpEnd.Value, true);
        }

        // Кнопка "Пример": три смены для демонстрации, добавляются по очереди тем же путём, что и вручную.
        private void btnDemo_Click(object sender, EventArgs e)
        {
            DateTime d = DateTime.Today;
            AddRecord("Иванов И. И.", d.AddHours(9), d.AddHours(17), false);
            Animator.After(this, "demo1", 650, delegate { AddRecord("Петрова А. С.", d.AddHours(8), d.AddHours(15.5), false); });
            Animator.After(this, "demo2", 1300, delegate { AddRecord("Сидоров П. К.", d.AddHours(10), d.AddHours(21), false); });
        }

        private void AddRecord(string employee, DateTime start, DateTime end, bool fromForm)
        {
            // Debug.WriteLine работает только в конфигурации Debug.
            Debug.WriteLine("Нажата кнопка \"Добавить запись\".");
            Debug.Indent();

            try
            {
                WorkRecord record = journal.Add(employee, start, end);
                lstRecords.Add(record);
                lblTotal.Set(journal.TotalHours, 2, " ч");
                lblCount.Set(journal.Count, 0);
                if (record.Hours > 8) lblOver.Set(++overtime, 0);

                // Вывод по условию: предупреждение о переработке.
                Debug.WriteLineIf(record.Hours > 8, "Внимание: смена длиннее 8 часов.");
                if (fromForm)
                {
                    window.Toast("Запись добавлена", record.ToString(), Tone.Good, 3200);
                    txtEmployee.Value = "";
                    txtEmployee.FocusBox();
                }
            }
            catch (ArgumentException ex)
            {
                // Trace работает и в Debug, и в Release.
                Trace.WriteLine("Ошибка ввода: " + ex.Message);
                window.Toast("Ошибка ввода", ex.Message, Tone.Warn);
                if (string.IsNullOrWhiteSpace(employee)) txtEmployee.Shake();
            }
            finally
            {
                Debug.Unindent();
            }
        }

        private void btnClearLog_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
        }
    }
}
