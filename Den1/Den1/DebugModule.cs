namespace Den1
{
    /// <summary>
    /// Отдельный модуль для задания 2 лабораторной работы № 7.
    /// Содержит метод с ошибками и тот же метод после отладки.
    /// </summary>
    public static class DebugModule
    {
        /// <summary>
        /// Вариант С ОШИБКАМИ (оставлен для демонстрации отладки).
        /// 1) Ошибка выполнения: условие цикла i &lt;= values.Length выходит
        ///    за границу массива - возникает IndexOutOfRangeException.
        /// 2) Логическая ошибка: sum / values.Length - целочисленное деление,
        ///    дробная часть результата теряется.
        /// </summary>
        public static double AverageWithBug(int[] values)
        {
            int sum = 0;
            for (int i = 0; i <= values.Length; i++)
            {
                sum += values[i];
            }
            return sum / values.Length;
        }

        /// <summary>
        /// Вариант после отладки: исправлены граница цикла и тип деления.
        /// </summary>
        public static double AverageFixed(int[] values)
        {
            if (values == null || values.Length == 0)
            {
                return 0;
            }

            int sum = 0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }
            return (double)sum / values.Length;
        }
    }
}
