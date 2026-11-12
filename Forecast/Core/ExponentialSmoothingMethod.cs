namespace Forecast.Core
{
    /// <summary>Способ задания начального значения экспоненциального сглаживания.</summary>
    public enum SesInit
    {
        /// <summary>S1 = y1</summary>
        FirstValue,
        /// <summary>S1 = (y1 + y2 + y3) / 3</summary>
        MeanOfFirstThree
    }

    /// <summary>Простое экспоненциальное сглаживание: S_t = a*y_t + (1-a)*S_(t-1).</summary>
    public class ExponentialSmoothingMethod : IForecastMethod
    {
        public double Alpha { get; private set; }
        public SesInit Init { get; private set; }

        public ExponentialSmoothingMethod(double alpha, SesInit init)
        {
            if (double.IsNaN(alpha) || alpha <= 0 || alpha >= 1)
                throw new ForecastException("Коэффициент сглаживания должен лежать в интервале (0; 1), получено " + Util.F(alpha) + ".");
            Alpha = alpha;
            Init = init;
        }

        public ExponentialSmoothingMethod(double alpha) : this(alpha, SesInit.FirstValue) { }

        public string Name
        {
            get
            {
                return "Эксп. сглаживание (α=" + Alpha.ToString("0.00") + (Init == SesInit.MeanOfFirstThree ? ", S1=ср.3" : "") + ")";
            }
        }

        public double[] Smooth(double[] y)
        {
            DataValidator.CheckSeries(y, Init == SesInit.MeanOfFirstThree ? 3 : 2, "ряд для экспоненциального сглаживания");
            double[] s = new double[y.Length];
            s[0] = Init == SesInit.FirstValue ? y[0] : (y[0] + y[1] + y[2]) / 3.0;
            for (int t = 1; t < y.Length; t++)
                s[t] = Alpha * y[t] + (1 - Alpha) * s[t - 1];
            return s;
        }

        public double[] Fit(double[] y)
        {
            double[] s = Smooth(y);
            double[] f = Util.NaNArray(y.Length);
            for (int t = 1; t < y.Length; t++) f[t] = s[t - 1];
            return f;
        }

        /// <summary>
        /// Прогноз на любое число шагов равен последнему сглаженному значению S_n:
        /// у модели нет компоненты тренда, новых наблюдений нет, поэтому уровень не меняется.
        /// </summary>
        public double[] Forecast(double[] y, int horizon)
        {
            double[] s = Smooth(y);
            double[] r = new double[horizon];
            for (int h = 0; h < horizon; h++) r[h] = s[s.Length - 1];
            return r;
        }
    }
}
