using System.Collections.Generic;
using System.Linq;

namespace Forecast.Core
{
    /// <summary>Комбинированный прогноз — среднее арифметическое прогнозов нескольких методов.</summary>
    public class CombinedMethod : IForecastMethod
    {
        private readonly List<IForecastMethod> methods;

        public CombinedMethod(IEnumerable<IForecastMethod> parts)
        {
            methods = parts.ToList();
            if (methods.Count == 0) throw new ForecastException("Для комбинированного прогноза нужен хотя бы один метод.");
        }

        public string Name { get { return "Комбинированный (среднее " + methods.Count + " методов)"; } }

        private static double[] Average(List<double[]> arrays)
        {
            int n = arrays[0].Length;
            double[] r = Util.NaNArray(n);
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                bool ok = true;
                foreach (double[] a in arrays)
                {
                    if (double.IsNaN(a[i])) { ok = false; break; }
                    s += a[i];
                }
                if (ok) r[i] = s / arrays.Count;
            }
            return r;
        }

        public double[] Fit(double[] y) { return Average(methods.Select(m => m.Fit(y)).ToList()); }
        public double[] Smooth(double[] y) { return Average(methods.Select(m => m.Smooth(y)).ToList()); }
        public double[] Forecast(double[] y, int horizon) { return Average(methods.Select(m => m.Forecast(y, horizon)).ToList()); }
    }
}
