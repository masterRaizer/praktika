namespace Forecast.Core
{
    /// <summary>
    /// Метод Хольта (двойное экспоненциальное сглаживание).
    /// L1 = y1, T1 = y2 - y1;
    /// L_t = a*y_t + (1-a)*(L_(t-1) + T_(t-1));
    /// T_t = b*(L_t - L_(t-1)) + (1-b)*T_(t-1);
    /// прогноз на h шагов: L_n + h*T_n.
    /// </summary>
    public class HoltMethod : IForecastMethod
    {
        public double Alpha { get; private set; }
        public double Beta { get; private set; }

        public HoltMethod(double alpha, double beta)
        {
            if (double.IsNaN(alpha) || alpha <= 0 || alpha >= 1)
                throw new ForecastException("Коэффициент α метода Хольта должен лежать в интервале (0; 1).");
            if (double.IsNaN(beta) || beta <= 0 || beta >= 1)
                throw new ForecastException("Коэффициент β метода Хольта должен лежать в интервале (0; 1).");
            Alpha = alpha;
            Beta = beta;
        }

        public string Name { get { return "Хольт (α=" + Alpha.ToString("0.00") + ", β=" + Beta.ToString("0.00") + ")"; } }

        private void Run(double[] y, out double[] level, out double[] trend)
        {
            DataValidator.CheckSeries(y, 3, "ряд для метода Хольта");
            level = new double[y.Length];
            trend = new double[y.Length];
            level[0] = y[0];
            trend[0] = y[1] - y[0];
            for (int t = 1; t < y.Length; t++)
            {
                level[t] = Alpha * y[t] + (1 - Alpha) * (level[t - 1] + trend[t - 1]);
                trend[t] = Beta * (level[t] - level[t - 1]) + (1 - Beta) * trend[t - 1];
            }
        }

        /// <summary>
        /// Прогноз для недели 2 не считается: он равен y2 по построению (T1 = y2 - y1),
        /// поэтому первая честная одношаговая ошибка — на неделе 3.
        /// </summary>
        public double[] Fit(double[] y)
        {
            double[] l, tr;
            Run(y, out l, out tr);
            double[] f = Util.NaNArray(y.Length);
            for (int t = 2; t < y.Length; t++) f[t] = l[t - 1] + tr[t - 1];
            return f;
        }

        public double[] Smooth(double[] y)
        {
            double[] l, tr;
            Run(y, out l, out tr);
            return l;
        }

        public double[] Forecast(double[] y, int horizon)
        {
            double[] l, tr;
            Run(y, out l, out tr);
            int n = y.Length - 1;
            double[] r = new double[horizon];
            for (int h = 1; h <= horizon; h++) r[h - 1] = l[n] + h * tr[n];
            return r;
        }

        /// <summary>Уровень и тренд на последнем шаге (для вывода).</summary>
        public void LastState(double[] y, out double level, out double trend)
        {
            double[] l, tr;
            Run(y, out l, out tr);
            level = l[l.Length - 1];
            trend = tr[tr.Length - 1];
        }
    }
}
