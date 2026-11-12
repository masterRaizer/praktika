using System;

namespace Forecast.Core
{
    /// <summary>
    /// Парная линейная регрессия y = a + b*x методом наименьших квадратов.
    /// Все формулы реализованы вручную, без сторонних библиотек.
    /// </summary>
    public class LinearRegression
    {
        public int N { get; private set; }
        public double A { get; private set; }          // свободный член
        public double B { get; private set; }          // угловой коэффициент
        public double MeanX { get; private set; }
        public double MeanY { get; private set; }
        public double Sxx { get; private set; }        // Σ(x - x̄)²
        public double Syy { get; private set; }        // Σ(y - ȳ)²
        public double Sxy { get; private set; }        // Σ(x - x̄)(y - ȳ)
        public double R { get; private set; }          // коэффициент корреляции
        public double R2 { get; private set; }         // коэффициент детерминации
        public double SSE { get; private set; }        // сумма квадратов остатков
        public double StdError { get; private set; }   // s = sqrt(SSE / (n - 2))
        public double TStat { get; private set; }      // t = |R|*sqrt(n-2)/sqrt(1-R²)
        public double MinX { get; private set; }
        public double MaxX { get; private set; }
        public double[] X { get; private set; }
        public double[] Y { get; private set; }

        public static LinearRegression Fit(double[] x, double[] y)
        {
            if (x == null || y == null || x.Length != y.Length)
                throw new ForecastException("Для регрессии нужны пары значений x и y одинаковой длины.");
            if (x.Length < 3)
                throw new ForecastException("Для регрессии нужно не меньше 3 пар наблюдений (n - 2 > 0).");
            for (int i = 0; i < x.Length; i++)
                if (double.IsNaN(x[i]) || double.IsNaN(y[i]) || double.IsInfinity(x[i]) || double.IsInfinity(y[i]))
                    throw new ForecastException("В строке " + (i + 1) + " регрессионных данных некорректное значение.");

            var m = new LinearRegression();
            int n = x.Length;
            m.N = n;
            m.X = (double[])x.Clone();
            m.Y = (double[])y.Clone();
            double sx = 0, sy = 0;
            for (int i = 0; i < n; i++) { sx += x[i]; sy += y[i]; }
            m.MeanX = sx / n;
            m.MeanY = sy / n;
            double sxx = 0, syy = 0, sxy = 0;
            m.MinX = double.MaxValue; m.MaxX = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - m.MeanX, dy = y[i] - m.MeanY;
                sxx += dx * dx; syy += dy * dy; sxy += dx * dy;
                m.MinX = Math.Min(m.MinX, x[i]); m.MaxX = Math.Max(m.MaxX, x[i]);
            }
            if (sxx == 0)
                throw new ForecastException("Все значения x одинаковы — угловой коэффициент вычислить нельзя (деление на ноль).");
            m.Sxx = sxx; m.Syy = syy; m.Sxy = sxy;
            m.B = sxy / sxx;
            m.A = m.MeanY - m.B * m.MeanX;
            m.R = syy == 0 ? 0 : sxy / Math.Sqrt(sxx * syy);
            m.R2 = m.R * m.R;
            double sse = 0;
            for (int i = 0; i < n; i++) { double e = y[i] - m.Predict(x[i]); sse += e * e; }
            m.SSE = sse;
            m.StdError = Math.Sqrt(sse / (n - 2));
            m.TStat = m.R2 >= 1 ? double.PositiveInfinity : Math.Abs(m.R) * Math.Sqrt(n - 2) / Math.Sqrt(1 - m.R2);
            return m;
        }

        public double Predict(double x) { return A + B * x; }

        public double[] Residuals()
        {
            double[] e = new double[N];
            for (int i = 0; i < N; i++) e[i] = Y[i] - Predict(X[i]);
            return e;
        }

        /// <summary>Степени свободы n - 2.</summary>
        public int DegreesOfFreedom { get { return N - 2; } }

        /// <summary>Критическое значение t (двусторонний уровень 0,05) для n - 2 степеней свободы.</summary>
        public double TCritical { get { return StudentT.Critical95(DegreesOfFreedom); } }

        public bool IsSignificant { get { return TStat > TCritical; } }

        /// <summary>
        /// Полуширина 95%-го доверительного интервала индивидуального прогноза в точке x0:
        /// t * s * sqrt(1 + 1/n + (x0 - x̄)² / Σ(x - x̄)²).
        /// </summary>
        public double PredictionHalfWidth(double x0)
        {
            return TCritical * StdError * Math.Sqrt(1.0 + 1.0 / N + (x0 - MeanX) * (x0 - MeanX) / Sxx);
        }

        public bool IsExtrapolation(double x0) { return x0 < MinX || x0 > MaxX; }

        /// <summary>Сила связи по шкале Чеддока.</summary>
        public static string Strength(double r)
        {
            double a = Math.Abs(r);
            string dir = r >= 0 ? "прямая" : "обратная";
            if (a < 0.1) return "связь практически отсутствует";
            if (a < 0.3) return dir + " слабая связь";
            if (a < 0.5) return dir + " умеренная связь";
            if (a < 0.7) return dir + " заметная связь";
            if (a < 0.9) return dir + " высокая связь";
            return dir + " весьма высокая связь";
        }

        public string Equation
        {
            get
            {
                return "y = " + A.ToString("0.000") + (B >= 0 ? " + " : " − ") + Math.Abs(B).ToString("0.000") + "·x";
            }
        }
    }

    /// <summary>Критические значения распределения Стьюдента для уровня значимости 0,05 (двусторонний).</summary>
    public static class StudentT
    {
        private static readonly double[] table =
        {
            double.NaN, 12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228,
            2.201, 2.179, 2.160, 2.145, 2.131, 2.120, 2.110, 2.101, 2.093, 2.086,
            2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045, 2.042
        };

        public static double Critical95(int df)
        {
            if (df < 1) throw new ForecastException("Недостаточно наблюдений: число степеней свободы должно быть не меньше 1.");
            if (df < table.Length) return table[df];
            if (df <= 40) return 2.021;
            if (df <= 60) return 2.000;
            if (df <= 120) return 1.980;
            return 1.960;
        }
    }
}
