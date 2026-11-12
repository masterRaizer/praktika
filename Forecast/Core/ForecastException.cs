using System;

namespace Forecast.Core
{
    /// <summary>Ошибка исходных данных или параметров прогноза (показывается пользователю).</summary>
    public class ForecastException : Exception
    {
        public ForecastException(string message) : base(message) { }
        public ForecastException(string message, Exception inner) : base(message, inner) { }
    }
}
