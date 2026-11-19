using System;
using ShagShoes.Logic;

namespace ShagShoes.Tests
{
    /// <summary>
    /// Отладка модуля расчёта цены: проверки без внешних библиотек.
    /// Запуск: ShagShoes.Tests.exe; код возврата 0 — все проверки пройдены.
    /// </summary>
    internal static class PriceCalculatorTests
    {
        private static int failed;
        private static int passed;

        private static int Main()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Check("Нет заказов в прошлом месяце — скидка 25 %", PriceCalculator.GetPrice(5490m, 0) == 4117.50m);
            Check("Есть заказы в прошлом месяце — цена без скидки", PriceCalculator.GetPrice(5490m, 3) == 5490m);
            Check("Округление до копеек: 99,99 × 0,75 = 74,99", PriceCalculator.GetPrice(99.99m, 0) == 74.99m);
            Check("Округление половины копейки вверх: 0,02 × 0,75 = 0,02", PriceCalculator.GetPrice(0.02m, 0) == 0.02m);
            Check("Цена 0 — ошибка", Throws(() => PriceCalculator.GetPrice(0m, 0)));
            Check("Отрицательная цена — ошибка", Throws(() => PriceCalculator.GetPrice(-10m, 0)));
            Check("Отрицательное число заказов — ошибка", Throws(() => PriceCalculator.GetPrice(100m, -1)));

            CheckMonth("Середина месяца", new DateTime(2026, 11, 19), new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));
            CheckMonth("Первое число месяца", new DateTime(2026, 11, 1), new DateTime(2026, 10, 1), new DateTime(2026, 11, 1));
            CheckMonth("Последнее число месяца", new DateTime(2026, 10, 31), new DateTime(2026, 9, 1), new DateTime(2026, 10, 1));
            CheckMonth("Январь → декабрь прошлого года", new DateTime(2027, 1, 15), new DateTime(2026, 12, 1), new DateTime(2027, 1, 1));
            CheckMonth("Март → февраль високосного года", new DateTime(2028, 3, 10), new DateTime(2028, 2, 1), new DateTime(2028, 3, 1));

            Check("6 пар — «много»", PriceCalculator.GetQuantityText(6) == "много");
            Check("5 пар — «мало»", PriceCalculator.GetQuantityText(5) == "мало");
            Check("0 пар — «мало»", PriceCalculator.GetQuantityText(0) == "мало");
            Check("3 пары — подсветка", PriceCalculator.IsLowStock(3));
            Check("4 пары — без подсветки", !PriceCalculator.IsLowStock(4));
            Check("0 пар — подсветка", PriceCalculator.IsLowStock(0));

            Console.WriteLine();
            Console.WriteLine("Пройдено: " + passed + ", не пройдено: " + failed);
            return failed == 0 ? 0 : 1;
        }

        private static void CheckMonth(string name, DateTime date, DateTime expectedStart, DateTime expectedEnd)
        {
            DateTime start, end;
            PriceCalculator.GetPreviousMonth(date, out start, out end);
            Check("Предыдущий месяц: " + name, start == expectedStart && end == expectedEnd);
        }

        private static bool Throws(Action action)
        {
            try
            {
                action();
                return false;
            }
            catch (ArgumentOutOfRangeException)
            {
                return true;
            }
        }

        private static void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
            }
            else
            {
                failed++;
            }
            Console.WriteLine((ok ? "[OK]   " : "[FAIL] ") + name);
        }
    }
}
