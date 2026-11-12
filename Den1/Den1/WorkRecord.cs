using System;

namespace Den1
{
    /// <summary>
    /// Одна запись учёта рабочего времени сотрудника.
    /// </summary>
    public class WorkRecord
    {
        public string Employee { get; private set; }
        public DateTime Start { get; private set; }
        public DateTime End { get; private set; }

        public WorkRecord(string employee, DateTime start, DateTime end)
        {
            Employee = employee;
            Start = start;
            End = end;
        }

        /// <summary>Отработано часов за смену.</summary>
        public double Hours
        {
            get { return (End.TimeOfDay - Start.TimeOfDay).TotalHours; }
        }

        public override string ToString()
        {
            return string.Format("{0}: {1:HH:mm} – {2:HH:mm}, отработано {3:F2} ч",
                Employee, Start, End, Hours);
        }
    }
}
