using System.Globalization;

namespace Forecast.Core
{
    /// <summary>Вспомогательные функции.</summary>
    public static class Util
    {
        public static double[] NaNArray(int n)
        {
            double[] a = new double[n];
            for (int i = 0; i < n; i++) a[i] = double.NaN;
            return a;
        }

        /// <summary>Среднее count элементов массива начиная с индекса start.</summary>
        public static double Mean(double[] a, int start, int count)
        {
            double s = 0;
            for (int i = start; i < start + count; i++) s += a[i];
            return s / count;
        }

        public static double[] Take(double[] a, int count)
        {
            double[] r = new double[count];
            System.Array.Copy(a, r, count);
            return r;
        }

        public static double[] Skip(double[] a, int count)
        {
            double[] r = new double[a.Length - count];
            System.Array.Copy(a, count, r, 0, r.Length);
            return r;
        }

        private static readonly CultureInfo ru = new CultureInfo("ru-RU");

        /// <summary>Число с двумя знаками после запятой (русский формат).</summary>
        public static string F(double v) { return F(v, 2); }

        public static string F(double v, int digits)
        {
            if (double.IsNaN(v)) return "—";
            if (double.IsPositiveInfinity(v)) return "∞";
            return v.ToString("F" + digits, ru);
        }
    }
}
