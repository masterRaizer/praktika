using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Den1
{
    /// <summary>
    /// Журнал учёта рабочего времени. Показывает применение
    /// отладочных классов Debug и Trace внутри модуля.
    /// </summary>
    public class WorkTimeJournal
    {
        private readonly List<WorkRecord> records = new List<WorkRecord>();

        public int Count
        {
            get { return records.Count; }
        }

        /// <summary>Суммарное отработанное время по всем записям.</summary>
        public double TotalHours
        {
            get
            {
                double total = 0;
                foreach (WorkRecord record in records)
                {
                    total += record.Hours;
                }
                return total;
            }
        }

        /// <summary>
        /// Добавляет запись. При неверных данных генерирует ArgumentException.
        /// </summary>
        public WorkRecord Add(string employee, DateTime start, DateTime end)
        {
            Trace.WriteLine("WorkTimeJournal.Add: начало");
            Trace.Indent();
            Debug.WriteLine("employee = \"" + employee + "\", start = " +
                            start.ToString("HH:mm") + ", end = " + end.ToString("HH:mm"));

            try
            {
                if (string.IsNullOrWhiteSpace(employee))
                {
                    throw new ArgumentException("Не указано ФИО сотрудника.");
                }
                if (end.TimeOfDay <= start.TimeOfDay)
                {
                    throw new ArgumentException("Время окончания работы должно быть позже времени начала.");
                }

                WorkRecord record = new WorkRecord(employee.Trim(), start, end);

                // Проверка внутреннего условия: после проверок выше часы всегда положительны.
                Debug.Assert(record.Hours > 0, "Количество часов должно быть положительным.");

                records.Add(record);
                Trace.WriteLine("Запись добавлена, всего записей: " + records.Count +
                                ", всего часов: " + TotalHours.ToString("F2"));
                return record;
            }
            finally
            {
                Trace.Unindent();
                Trace.WriteLine("WorkTimeJournal.Add: конец");
            }
        }
    }
}
