using System;

namespace Forecast.Core
{
    /// <summary>Метод скользящего среднего с периодом k.</summary>
    public class MovingAverageMethod : IForecastMethod
    {
        public int Period { get; private set; }

        public MovingAverageMethod(int period)
        {
            if (period < 1) throw new ForecastException("Период скользящего среднего должен быть не меньше 1.");
            Period = period;
        }

        public string Name { get { return "Скользящее среднее (k=" + Period + ")"; } }

        private void Check(double[] y)
        {
            DataValidator.CheckSeries(y, Period + 1, "ряд для скользящего среднего");
        }

        public double[] Fit(double[] y)
        {
            Check(y);
            double[] f = Util.NaNArray(y.Length);
            for (int i = Period; i < y.Length; i++)
                f[i] = Util.Mean(y, i - Period, Period);
            return f;
        }

        public double[] Smooth(double[] y)
        {
            Check(y);
            double[] s = Util.NaNArray(y.Length);
            for (int i = Period - 1; i < y.Length; i++)
                s[i] = Util.Mean(y, i - Period + 1, Period);
            return s;
        }

        /// <summary>
        /// Прогноз вперёд: второй и следующие шаги используют уже полученные прогнозные значения.
        /// </summary>
        public double[] Forecast(double[] y, int horizon)
        {
            Check(y);
            double[] ext = new double[y.Length + horizon];
            Array.Copy(y, ext, y.Length);
            double[] result = new double[horizon];
            for (int h = 0; h < horizon; h++)
            {
                int pos = y.Length + h;
                ext[pos] = Util.Mean(ext, pos - Period, Period);
                result[h] = ext[pos];
            }
            return result;
        }
    }
}
