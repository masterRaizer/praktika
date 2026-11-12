namespace Forecast.Core
{
    /// <summary>
    /// Общий интерфейс методов прогнозирования временного ряда.
    /// Индексы массивов: 0 — первое наблюдение (месяц/неделя 1).
    /// </summary>
    public interface IForecastMethod
    {
        /// <summary>Название метода с параметрами (для легенды и таблиц).</summary>
        string Name { get; }

        /// <summary>
        /// Одношаговые прогнозы внутри ряда: result[i] — прогноз наблюдения i,
        /// построенный только по наблюдениям 0..i-1. Где прогноз невозможен — double.NaN.
        /// </summary>
        double[] Fit(double[] series);

        /// <summary>Сглаженный (расчётный) ряд той же длины; NaN там, где значения нет.</summary>
        double[] Smooth(double[] series);

        /// <summary>Прогноз на horizon шагов после последнего наблюдения.</summary>
        double[] Forecast(double[] series, int horizon);
    }
}
