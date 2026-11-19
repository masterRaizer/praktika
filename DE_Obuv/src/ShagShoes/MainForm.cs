using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FxKit;
using ShagShoes.Controls;
using ShagShoes.Data;
using ShagShoes.Logic;
using ShagShoes.Models;

namespace ShagShoes
{
    /// <summary>
    /// Главное окно приложения компании «Шаг»: каталог моделей обуви и расчёт цены со скидкой.
    /// Оформление (окно, элементы, анимации, темы) — библиотека FxKit, папка Fx.
    /// </summary>
    public sealed class MainForm : FxWindow
    {
        public const string Company = "Шаг";

        private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

        private readonly ProductRepository repository = new ProductRepository();
        private readonly DateTime calcDate;

        private ProductList productList;
        private FxStat statModels, statDiscount, statLowStock, statDate;
        private FxStat statPeriod, statWithDiscount, statWithoutDiscount, statPercent;
        private FxTable discountTable;
        private FxText algorithmText;
        private string startupError;

        /// <summary>Раздел, открываемый при запуске.</summary>
        public int StartPage { get; set; }

        public MainForm(DateTime calcDate)
        {
            this.calcDate = calcDate.Date;
            Text = Company + " — каталог обуви";
            AppTitle = Company;
            AppCaption = "оформление заказа обуви";
            AppIcon = "shoe";
            AppLogo = LoadResourceImage("logo.png");
            SplashTitle = Company;
            SplashSubtitle = "каталог и оформление заказа обуви";
            LoadResourceIcon("icon.ico");

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1360, area.Width - 24), Math.Min(860, area.Height - 24));
            MinimumSize = new Size(Math.Min(1080, area.Width), Math.Min(640, area.Height));

            AddPage("shoe", "Каталог", "модели обуви", "Каталог обуви",
                "Модели из базы данных: цена со скидкой и наличие по всем размерам", BuildCatalogPage());
            AddPage("percent", "Расчёт скидки", "алгоритм цены", "Расчёт цены со скидкой",
                "Скидка 25 % на модели, которых не заказывали в предыдущем календарном месяце", BuildDiscountPage());

            PageShown += delegate(int index) { Text = Company + " — " + Items[index].Header; };
            Load += delegate
            {
                LoadCatalog(false);
                Navigate(StartPage, false);
            };
            // ошибка при запуске показывается, когда окно уже на экране
            Shown += delegate
            {
                if (startupError == null) return;
                string text = startupError;
                startupError = null;
                // небольшая пауза, чтобы окно успело отрисоваться под сообщением
                var timer = new Timer { Interval = 450 };
                timer.Tick += delegate
                {
                    timer.Stop();
                    timer.Dispose();
                    ShowDatabaseError(text);
                };
                timer.Start();
            };
        }

        // =====================================================================
        //  Ресурсы
        // =====================================================================

        private static string ResourcePath(string file)
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", file);
        }

        private static Image LoadResourceImage(string file)
        {
            try
            {
                string path = ResourcePath(file);
                if (!File.Exists(path)) return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
            catch (Exception)
            {
                return null;   // без логотипа окно рисует значок
            }
        }

        private void LoadResourceIcon(string file)
        {
            try
            {
                string path = ResourcePath(file);
                if (File.Exists(path)) Icon = new Icon(path);
            }
            catch (Exception)
            {
                // остаётся значок по умолчанию
            }
        }

        // =====================================================================
        //  Раздел «Каталог»
        // =====================================================================

        private Control BuildCatalogPage()
        {
            statModels = new FxStat("Моделей в каталоге", 0) { Tone = Tone.Accent2 };
            statDiscount = new FxStat("Со скидкой 25 %", 2) { Tone = Tone.Good };
            statLowStock = new FxStat("Осталось 3 пары и меньше", 4) { Tone = Tone.Bad };
            statDate = new FxStat("Дата расчёта цен", 1) { Tone = Tone.Accent };

            var tiles = FxBox.H(10);
            tiles.Fill(statModels).Fill(statDiscount).Fill(statLowStock).Fill(statDate);
            tiles.Height = 78;

            productList = new ProductList
            {
                ImagesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images"),
                PlaceholderFile = ResourcePath("picture.png")
            };

            var refresh = new FxButton("Обновить", false, "refresh").AutoWidth();
            refresh.Height = 28;
            refresh.Click += delegate { LoadCatalog(true); };

            var card = new FxCard("Каталог моделей обуви", "shoe") { Padding = new Padding(14, 12, 8, 10) };
            card.With(productList);
            card.WithHeader(refresh);

            var page = FxBox.V(12).Pad(0, 6, 6, 6);
            page.Add(tiles, 78).Fill(card);
            return page;
        }

        // =====================================================================
        //  Раздел «Расчёт скидки»
        // =====================================================================

        private Control BuildDiscountPage()
        {
            statPeriod = new FxStat("Предыдущий месяц", 1) { Tone = Tone.Accent };
            statWithDiscount = new FxStat("Без заказов — скидка", 2) { Tone = Tone.Good };
            statWithoutDiscount = new FxStat("С заказами — без скидки", 0) { Tone = Tone.Accent2 };
            statPercent = new FxStat("Размер скидки", 4) { Tone = Tone.Warn };

            var tiles = FxBox.H(10);
            tiles.Fill(statPeriod, 1.3f).Fill(statWithDiscount).Fill(statWithoutDiscount).Fill(statPercent);
            tiles.Height = 78;

            algorithmText = new FxText { FontSize = 9.5f };
            var algorithmCard = new FxCard("Алгоритм расчёта цены", "calc") { Padding = new Padding(16, 12, 12, 12) };
            algorithmCard.With(algorithmText);

            discountTable = new FxTable { RowHeight = 34 };
            discountTable.SetColumns("Артикул", "Модель", "Заказов в прошлом месяце", "Цена, руб.", "Скидка", "Цена со скидкой, руб.");
            discountTable.WrapCells = true;
            discountTable.Columns[0].Weight = 0.8f;
            discountTable.Columns[1].Weight = 2.2f;
            discountTable.Columns[1].Align = StringAlignment.Near;
            discountTable.Columns[2].Weight = 1.1f;
            discountTable.Columns[4].Weight = 0.7f;
            discountTable.Columns[5].Weight = 1.2f;
            var tableCard = new FxCard("Расчёт по моделям каталога", "data") { Padding = new Padding(14, 12, 10, 10) };
            tableCard.With(discountTable);

            var row = FxBox.H(12);
            row.Fill(algorithmCard, 0.8f).Fill(tableCard, 1.35f);

            var page = FxBox.V(12).Pad(0, 6, 6, 6);
            page.Add(tiles, 78).Fill(row);
            return page;
        }

        // =====================================================================
        //  Загрузка данных
        // =====================================================================

        private void LoadCatalog(bool notify)
        {
            List<Product> products;
            try
            {
                products = repository.GetCatalog(calcDate);
            }
            catch (DatabaseException ex)
            {
                productList.ShowMessage("Каталог недоступен: нет подключения к базе данных.\nПроверьте подключение и нажмите «Обновить».");
                ShowStatsUnavailable();
                if (Visible)
                {
                    ShowDatabaseError(ex.Message);
                }
                else
                {
                    startupError = ex.Message;
                }
                return;
            }

            productList.SetProducts(products);
            FillStats(products);
            FillDiscountPage(products);
            if (notify)
            {
                Toast("Каталог обновлён", "Загружено моделей: " + products.Count + ". Цены рассчитаны на " + calcDate.ToString("dd.MM.yyyy", Ru) + ".", Tone.Good);
            }
        }

        private void ShowDatabaseError(string text)
        {
            MessageBox.Show(this, text, "Ошибка подключения к базе данных", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void ShowStatsUnavailable()
        {
            foreach (FxStat stat in new[] { statModels, statDiscount, statLowStock, statWithDiscount, statWithoutDiscount })
            {
                stat.SetText("—");
                stat.Sub = "нет данных";
            }
            statDate.SetText(calcDate.ToString("dd.MM.yyyy", Ru));
            discountTable.Clear();
        }

        private void FillStats(List<Product> products)
        {
            int discounted = products.Count(p => p.HasDiscount);
            int lowStock = products.Count(p => PriceCalculator.IsLowStock(p.TotalPairs));
            statModels.Set(products.Count, 0);
            statModels.Sub = "пар в наличии: " + products.Sum(p => p.TotalPairs);
            statDiscount.Set(discounted, 0);
            statDiscount.Sub = "не заказывали в прошлом месяце";
            statLowStock.Set(lowStock, 0);
            statLowStock.Sub = "подсвечены в списке";
            statDate.SetText(calcDate.ToString("dd.MM.yyyy", Ru));
            statDate.Sub = "сегодня";
        }

        private void FillDiscountPage(List<Product> products)
        {
            DateTime start, end;
            PriceCalculator.GetPreviousMonth(calcDate, out start, out end);
            string period = start.ToString("dd.MM.yyyy", Ru) + " – " + end.AddDays(-1).ToString("dd.MM.yyyy", Ru);

            int discounted = products.Count(p => p.HasDiscount);
            statPeriod.SetText(start.ToString("MMMM yyyy", Ru));
            statPeriod.Sub = period;
            statWithDiscount.Set(discounted, 0);
            statWithDiscount.Sub = "моделей";
            statWithoutDiscount.Set(products.Count - discounted, 0);
            statWithoutDiscount.Sub = "моделей";
            statPercent.Set((double)PriceCalculator.DiscountPercent, 0, " %");
            statPercent.Sub = "цена × 0,75";

            algorithmText.Text =
                "Дата расчёта: " + calcDate.ToString("dd.MM.yyyy", Ru) + ".\n" +
                "1. Предыдущий календарный месяц: с " + period.Replace(" – ", " по ") + ".\n" +
                "2. Для каждой модели считаются заказы с этой моделью любого размера, оформленные в этом месяце.\n" +
                "3. Нет ни одного заказа — цена со скидкой 25 %: цена × (100 − 25) / 100, округление до копеек.\n" +
                "4. Есть хотя бы один заказ — цена без изменений.\n" +
                "Количество пар: сумма остатков по всем размерам модели; больше пяти — «много», пять и меньше — «мало».\n" +
                "Подсветка: модели, которых осталось три пары и меньше, выделяются в каталоге светло-красным цветом.\n" +
                "Код алгоритма: класс PriceCalculator (папка Logic).";

            discountTable.Clear();
            foreach (Product p in products.OrderBy(p => p.Article))
            {
                FxRow row = discountTable.AddRow(
                    p.Article,
                    p.Name,
                    p.OrdersInPreviousMonth.ToString(Ru),
                    p.Price.ToString("N2", Ru),
                    p.HasDiscount ? "25 %" : "—",
                    p.FinalPrice.ToString("N2", Ru));
                if (p.HasDiscount) discountTable.SetCellTone(row, 5, RowTone.Good);
            }
        }
    }
}
