using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Den5_7.Pages;
using FxKit;

namespace Den5_7
{
    /// <summary>
    /// Главное окно: дни 5, 6 и 7 учебной практики по порядку в боковой панели,
    /// разделы каждого дня — вкладками сверху. Кнопка «три полоски» сворачивает панель.
    /// </summary>
    public sealed class MainForm : FxWindow
    {
        public readonly Day5Page Day5;
        public readonly Day6Page Day6;
        public readonly Day7Page Day7;

        public int StartPage { get; set; }
        public int StartTab { get; set; }

        public MainForm()
        {
            Text = "Киноточка — учебная практика, дни 5–7";
            AppTitle = "Киноточка";
            AppCaption = "учебная практика · дни 5–7";
            AppIcon = "film";
            AppLogo = LoadImage("logo.png");
            SplashTitle = "Киноточка";
            SplashSubtitle = "интерфейс и база данных · тестирование · интеграция REST API";
            try
            {
                string icon = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "icon.ico");
                if (File.Exists(icon)) Icon = new Icon(icon);
            }
            catch (Exception)
            {
                // остаётся значок по умолчанию
            }

            Rectangle area = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1440, area.Width - 24), Math.Min(900, area.Height - 24));
            MinimumSize = new Size(Math.Min(1160, area.Width), Math.Min(660, area.Height));

            Day5 = new Day5Page();
            Day6 = new Day6Page();
            Day7 = new Day7Page();
            AddPage("film", "День 5", "интерфейс и база данных", "День 5 · Интерфейс, база данных и приложение",
                "Вход, справочник с поиском, фильтром и сортировкой, карточка, 13 SQL-запросов и словарь данных", Day5);
            AddPage("check", "День 6", "функции и тестирование", "День 6 · Дополнительные функции и тестирование",
                "Отчёты, экспорт в CSV, журнал действий, 21 тест-кейс и документация", Day6);
            AddPage("plug", "День 7", "интеграция REST API", "День 7 · Интеграция модулей через REST API",
                "Модули «Клиенты» и «Заявки» — отдельные процессы на своих портах, обмен JSON по HTTP", Day7);

            PageShown += delegate(int i) { Text = "Киноточка — " + Items[i].Title; };
            Load += delegate
            {
                Navigate(StartPage, false);
                if (StartPage == 0) Day5.ShowTab(StartTab);
                if (StartPage == 1) Day6.ShowTab(StartTab);
                if (StartPage == 2) Day7.ShowTab(StartTab);
            };
            FormClosing += delegate { Day7.StopAll(); };
        }

        private static Image LoadImage(string file)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", file);
                if (!File.Exists(path)) return null;
                using (var stream = new MemoryStream(File.ReadAllBytes(path)))
                using (Image source = Image.FromStream(stream))
                {
                    return new Bitmap(source);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
