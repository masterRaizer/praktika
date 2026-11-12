using System;
using System.Drawing;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    /// <summary>
    /// Главное окно: обзорная страница и три лабораторные работы.
    /// Работы открываются в этом же окне с плавным переходом; оформление — библиотека FxKit (папка Fx).
    /// </summary>
    public class MainForm : FxWindow
    {
        private readonly Lab7Page lab7;
        private readonly Lab8Page lab8;
        private readonly Lab9Page lab9;
        private int lastPage = -1;

        /// <summary>Раздел, открываемый при запуске.</summary>
        public int StartPage;

        public MainForm()
        {
            Text = "День 1. Лабораторные работы № 7–9";
            AppTitle = "День 1";
            AppCaption = "лабораторные № 7–9";
            AppIcon = "spark";
            SplashTitle = "Практика · день 1";
            SplashSubtitle = "отладка модулей · исключения · Debug и Trace";

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1280, wa.Width - 24), Math.Min(820, wa.Height - 24));
            MinimumSize = new Size(Math.Min(1080, wa.Width), Math.Min(660, wa.Height));

            lab7 = new Lab7Page(this);
            lab8 = new Lab8Page();
            lab9 = new Lab9Page(this);

            AddPage("home", "Обзор", "все работы дня", "День 1. Лабораторные работы",
                "Три работы в одном окне — выберите плитку или пункт слева", BuildHome());
            AddPage("bug", "Отладка модулей", "лабораторная № 7", "Лабораторная работа № 7",
                "Отладка отдельных модулей: форма с рисунком и кнопками, поиск ошибки в цикле", lab7);
            AddPage("shield", "Исключения", "лабораторная № 8", "Лабораторная работа № 8",
                "Организация обработки исключений: try, catch, finally и throw", lab8);
            AddPage("clock", "Учёт времени", "лабораторная № 9", "Лабораторная работа № 9",
                "Отладочные классы Debug и Trace в программе «Учёт рабочего времени»", lab9);

            PageShown += OnPageShown;
            Load += delegate { Navigate(StartPage, false); };
        }

        private void OnPageShown(int index)
        {
            if (lastPage == 1 && index != 1) lab7.OnLeave();
            if (lastPage == 3 && index != 3) lab9.OnClosed();
            if (index == 1) lab7.OnEnter();
            if (index == 3) lab9.OnOpened();
            lastPage = index;
        }

        private Control BuildHome()
        {
            var hero = new HeroBanner
            {
                Heading = "Практика · день 1",
                Sub = "Отладка модулей, обработка исключений и отладочные классы — три лабораторные работы с наглядной анимацией каждого шага.",
                Chips = new[] { "C#", ".NET Framework 4.7.2", "Windows Forms", "собственные элементы управления" }
            };

            var t7 = new LabTile
            {
                Number = "07", IconName = "bug", Art = 0,
                Heading = "Отладка отдельных модулей",
                Body = "Форма с флагом и кнопками, меняющими свойства объектов, и пошаговый разбор ошибки выхода за границу массива."
            };
            var t8 = new LabTile
            {
                Number = "08", IconName = "shield", Art = 1,
                Heading = "Обработка исключений",
                Body = "Арифметика над двумя числами: неверный ввод и деление на нуль перехватываются, а схема показывает путь выполнения."
            };
            var t9 = new LabTile
            {
                Number = "09", IconName = "clock", Art = 2,
                Heading = "Отладочные классы Debug и Trace",
                Body = "Программа «Учёт рабочего времени» по техническому заданию: журнал смен и живой отладочный вывод."
            };
            t7.Click += delegate { Navigate(1); };
            t8.Click += delegate { Navigate(2); };
            t9.Click += delegate { Navigate(3); };

            FxBox tiles = FxBox.H(14);
            tiles.Fill(t7).Fill(t8).Fill(t9);
            FxBox page = FxBox.V(14);
            page.Add(hero, 176).Fill(tiles);
            return page;
        }
    }
}
