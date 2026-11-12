namespace Forecast.Core
{
    /// <summary>
    /// Линейная регрессия по времени (y = a + b*t, t = 1..n) как метод прогноза ряда.
    /// Одношаговый прогноз наблюдения i строится по регрессии на первых i точках
    /// (расширяющееся окно), чтобы сравнение с другими методами было честным.
    /// </summary>
    public class LinearTrendMethod : IForecastMethod
    {
        /// <summary>Минимальное число точек для построения регрессии внутри ряда.</summary>
        public const int MinPoints = 3;

        public string Name { get { return "Линейная регрессия (тренд)"; } }

        private static double[] TimeAxis(int n)
        {
            double[] t = new double[n];
            for (int i = 0; i < n; i++) t[i] = i + 1;
            return t;
        }

        public LinearRegression Model(double[] y)
        {
            DataValidator.CheckSeries(y, MinPoints, "ряд для линейной регрессии");
            return LinearRegression.Fit(TimeAxis(y.Length), y);
        }

        public double[] Fit(double[] y)
        {
            DataValidator.CheckSeries(y, MinPoints + 1, "ряд для линейной регрессии");
            double[] f = Util.NaNArray(y.Length);
            for (int i = MinPoints; i < y.Length; i++)
            {
                double[] part = new double[i];
                System.Array.Copy(y, part, i);
                LinearRegression m = LinearRegression.Fit(TimeAxis(i), part);
                f[i] = m.Predict(i + 1);
            }
            return f;
        }

        public double[] Smooth(double[] y)
        {
            LinearRegression m = Model(y);
            double[] s = new double[y.Length];
            for (int i = 0; i < y.Length; i++) s[i] = m.Predict(i + 1);
            return s;
        }

        public double[] Forecast(double[] y, int horizon)
        {
            LinearRegression m = Model(y);
            double[] r = new double[horizon];
            for (int h = 1; h <= horizon; h++) r[h - 1] = m.Predict(y.Length + h);
            return r;
        }
    }
}
