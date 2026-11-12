using System;

namespace Forecast.Core
{
    /// <summary>Пример ряда, на котором метод даёт плохой прогноз (задание 2.2, п. 3).</summary>
    public class ProblemExample
    {
        public string Title;
        public string Explanation;
        public double[] History;      // по этой части строится прогноз
        public double[] Continuation; // фактическое продолжение ряда для проверки
        public IForecastMethod Method;
    }

    public static class ProblemExamples
    {
        public const int Horizon = 4;

        public static ProblemExample Get(string key)
        {
            var g = new SeriesGenerator(17);
            int n = 16;
            double[] all = new double[n + Horizon];
            var ex = new ProblemExample();
            switch (key)
            {
                case "MA":
                    for (int t = 0; t < all.Length; t++) all[t] = Math.Round(100 + 9 * t + g.Gauss(2));
                    ex.Method = new MovingAverageMethod(5);
                    ex.Title = "Скользящее среднее на ряде с сильным трендом";
                    ex.Explanation = "Среднее последних k значений всегда отстаёт от растущего ряда примерно на (k+1)/2 периода прироста, " +
                                     "а прогноз вперёд остаётся почти горизонтальным. Чем больше период и сильнее тренд, тем больше систематическое занижение.";
                    break;
                case "SES":
                    for (int t = 0; t < all.Length; t++) all[t] = Math.Round(100 + 25 * Math.Sin(2 * Math.PI * t / 4) + g.Gauss(2));
                    ex.Method = new ExponentialSmoothingMethod(0.3);
                    ex.Title = "Экспоненциальное сглаживание на сезонном ряде";
                    ex.Explanation = "Модель знает только уровень: пики и провалы сезонной волны она сглаживает, " +
                                     "а прогноз на все шаги — одна горизонтальная линия, поэтому ошибка в пиках и провалах составляет почти всю амплитуду.";
                    break;
                case "HOLT":
                    for (int t = 0; t < all.Length; t++)
                        all[t] = Math.Round((t < 13 ? 60 + 8 * t : 60 + 8 * 12 - 14 * (t - 12)) + g.Gauss(2));
                    ex.Method = new HoltMethod(0.5, 0.3);
                    ex.Title = "Метод Хольта при смене направления тренда";
                    ex.Explanation = "Компонента тренда накопила рост и меняется с запаздыванием: после разворота ряда " +
                                     "прогноз L + h·T продолжает линию вверх (или почти горизонтально), и ошибка растёт с горизонтом.";
                    break;
                default: // TREND
                    for (int t = 0; t < all.Length; t++) all[t] = Math.Round(40 * Math.Pow(1.14, t) + g.Gauss(2));
                    ex.Method = new LinearTrendMethod();
                    ex.Title = "Линейная регрессия на нелинейном (экспоненциальном) росте";
                    ex.Explanation = "Прямая, подобранная по всему ряду, проходит через «середину» кривой: в конце ряда она уже ниже факта, " +
                                     "и при экстраполяции разрыв только увеличивается.";
                    break;
            }
            ex.History = Util.Take(all, n);
            ex.Continuation = Util.Skip(all, n);
            return ex;
        }
    }
}
