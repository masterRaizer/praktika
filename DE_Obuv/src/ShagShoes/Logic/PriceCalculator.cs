using System;

namespace ShagShoes.Logic
{
    /// <summary>
    /// Правила каталога: расчёт цены со скидкой и оценка количества пар.
    /// Скидка 25 % действует на модели, по которым нет заказов в предыдущем календарном месяце
    /// относительно даты расчёта.
    /// </summary>
    public static class PriceCalculator
    {
        public const decimal DiscountPercent = 25m;

        /// <summary>Больше этого числа пар — «много», иначе — «мало».</summary>
        public const int ManyPairsThreshold = 5;

        /// <summary>Столько пар или меньше — модель подсвечивается в каталоге.</summary>
        public const int LowStockThreshold = 3;

        /// <summary>
        /// Границы предыдущего календарного месяца: начало включается, конец (первое число текущего месяца) — нет.
        /// </summary>
        public static void GetPreviousMonth(DateTime calcDate, out DateTime start, out DateTime end)
        {
            var currentMonthStart = new DateTime(calcDate.Year, calcDate.Month, 1);
            start = currentMonthStart.AddMonths(-1);
            end = currentMonthStart;
        }

        /// <summary>Цена пары в рублях с учётом скидки, округлённая до копеек.</summary>
        /// <param name="basePrice">Цена модели из каталога.</param>
        /// <param name="ordersInPreviousMonth">Число заказов модели в предыдущем календарном месяце.</param>
        public static decimal GetPrice(decimal basePrice, int ordersInPreviousMonth)
        {
            if (basePrice <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(basePrice), "Цена товара должна быть больше нуля.");
            }
            if (ordersInPreviousMonth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ordersInPreviousMonth), "Число заказов не может быть отрицательным.");
            }
            if (ordersInPreviousMonth > 0)
            {
                return basePrice;
            }
            decimal discounted = basePrice * (100m - DiscountPercent) / 100m;
            return Math.Round(discounted, 2, MidpointRounding.AwayFromZero);
        }

        public static bool HasDiscount(int ordersInPreviousMonth)
        {
            return ordersInPreviousMonth == 0;
        }

        /// <summary>«много», если пар больше пяти, иначе «мало».</summary>
        public static string GetQuantityText(int totalPairs)
        {
            return totalPairs > ManyPairsThreshold ? "много" : "мало";
        }

        public static bool IsLowStock(int totalPairs)
        {
            return totalPairs <= LowStockThreshold;
        }
    }
}
