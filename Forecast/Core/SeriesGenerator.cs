using System;

namespace Forecast.Core
{
    public enum SeriesKind
    {
        NoTrend,
        Trend,
        Seasonal
    }

    /// <summary>Генерация случайных рядов и пар «реклама — продажи».</summary>
    public class SeriesGenerator
    {
        private readonly Random rnd;

        public SeriesGenerator() { rnd = new Random(); }
        public SeriesGenerator(int seed) { rnd = new Random(seed); }

        /// <summary>Нормально распределённая величина (преобразование Бокса — Мюллера).</summary>
        public double Gauss(double sigma)
        {
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = rnd.NextDouble();
            return sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>
        /// Ряд длины n: уровень + прирост·t (+ сезонная волна) + шум со стандартным отклонением noise.
        /// Для ряда без тренда прирост не используется. Значения округляются до целых и не бывают отрицательными.
        /// </summary>
        public double[] Generate(SeriesKind kind, int n, double level, double growth, double noise)
        {
            if (n < 6) throw new ForecastException("Длина генерируемого ряда должна быть не меньше 6.");
            if (n > 500) throw new ForecastException("Длина генерируемого ряда не должна превышать 500.");
            if (noise < 0) throw new ForecastException("Уровень шума не может быть отрицательным.");
            double[] y = new double[n];
            const int seasonLength = 4;
            double amplitude = Math.Max(Math.Abs(level) * 0.15, 3 * noise + 1);
            for (int t = 0; t < n; t++)
            {
                double v = level;
                if (kind != SeriesKind.NoTrend) v += growth * t;
                if (kind == SeriesKind.Seasonal) v += amplitude * Math.Sin(2 * Math.PI * t / seasonLength);
                v += Gauss(noise);
                y[t] = Math.Max(0, Math.Round(v));
            }
            return y;
        }

        /// <summary>Пары «реклама — продажи»: x равномерно в [xMin; xMax], y = a + b·x + шум.</summary>
        public void GeneratePairs(int n, double xMin, double xMax, double a, double b, double noise,
                                  out double[] x, out double[] y)
        {
            if (n < 3) throw new ForecastException("Нужно не меньше 3 пар.");
            if (xMax <= xMin) throw new ForecastException("Верхняя граница диапазона рекламы должна быть больше нижней.");
            if (xMin < 0) throw new ForecastException("Расходы на рекламу не могут быть отрицательными.");
            x = new double[n]; y = new double[n];
            double step = (xMax - xMin) / (n - 1);
            for (int i = 0; i < n; i++)
            {
                x[i] = Math.Round(xMin + step * i, 1);
                y[i] = Math.Max(0, Math.Round(a + b * x[i] + Gauss(noise)));
            }
        }
    }
}
