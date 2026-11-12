using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using FxKit;

namespace Den1
{
    /// <summary>
    /// Лабораторная работа № 7. Отладка отдельных модулей программного проекта.
    /// Задание 1 - форма с рисунком флага и кнопками (аналог примера на VBA).
    /// Задание 2 - модуль с ошибкой (DebugModule.cs) и его отладка.
    /// </summary>
    public class Lab7Page : FxBox
    {
        // Папка с рисунками лежит рядом с exe-файлом (bin\Debug\Images).
        private readonly string imagesDir = Path.Combine(Application.StartupPath, "Images");

        // Исходные данные для задания 2.
        private readonly int[] testData = { 4, 8, 15, 16, 23, 42 };

        private readonly FxWindow window;
        private readonly CaptionLabel lblCaption = new CaptionLabel();
        private readonly FlagStage stage = new FlagStage();
        private readonly ArrayDebugView debugView = new ArrayDebugView();
        private readonly FxLabel lblResult = new FxLabel("Результат: —", Fonts.Semi(10f), Tone.Dim);

        public Lab7Page(FxWindow window)
        {
            this.window = window;
            Vertical = true;
            Gap = 12;

            // ---------- задание 1 ----------
            var btnChangeCaption = new FxButton("Измени надпись", false, "info");
            var btnMoveRight = new FxButton("Сдвинь рисунок вправо", false, "arrow");
            var btnMosaic = new FxButton("Мозаика", false, "data");
            var btnColors = new FxButton("Измени цвет фона и формы", false, "spark");
            var btnChangeFlag = new FxButton("Смена флага и надписи", false, "flag");
            var btnReset = new FxButton("Вернуть как было", false, "refresh");
            btnChangeCaption.Click += btnChangeCaption_Click;
            btnMoveRight.Click += btnMoveRight_Click;
            btnMosaic.Click += btnMosaic_Click;
            btnColors.Click += btnColors_Click;
            btnChangeFlag.Click += btnChangeFlag_Click;
            btnReset.Click += delegate { ResetStage(true); };

            var stageCard = new FxCard("Задание 1. Форма с рисунком и кнопками", "flag");
            FxBox stageBox = FxBox.V(6);
            stageBox.Add(lblCaption, 40).Fill(stage);
            stageCard.With(stageBox);

            var actions = new FxCard("Действия", "play");
            FxBox ab = FxBox.V(8);
            ab.Add(btnChangeCaption, 36).Add(btnMoveRight, 36).Add(btnMosaic, 36).Add(btnColors, 36).Add(btnChangeFlag, 36);
            ab.Fill(new FxLabel("Каждая кнопка меняет свойства объектов формы — так же, как в примере на VBA, только плавно.",
                Fonts.Body(8.5f), Tone.Mute, true));
            ab.Add(btnReset, 34);
            actions.With(ab);

            FxBox top = FxBox.H(12);
            top.Fill(stageCard).Add(actions, 272);

            // ---------- задание 2 ----------
            var btnRunBug = new FxButton("Запустить модуль с ошибкой", true, "bug") { Danger = true };
            var btnRunFixed = new FxButton("Запустить исправленный модуль", true, "check");
            btnRunBug.Click += btnRunBug_Click;
            btnRunFixed.Click += btnRunFixed_Click;
            debugView.SetData(testData);

            var task2 = new FxCard("Задание 2. Отладка модуля расчёта среднего арифметического", "bug");
            FxBox tb = FxBox.V(8);
            FxBox row = FxBox.H(10);
            row.Add(btnRunBug, 268).Add(btnRunFixed, 286).Fill(lblResult);
            tb.Fill(debugView).Add(row, 36);
            task2.With(tb);

            Fill(top);
            Add(task2, 236);

            lblCaption.SetText("Флаг", false);
            LoadFlag("FlagRUS.png", false);
        }

        /// <summary>
        /// Загружает рисунок флага на сцену (аналог LoadPicture в VBA).
        /// ВНИМАНИЕ: путь к графическому файлу должен быть описан правильно.
        /// </summary>
        private void LoadFlag(string fileName, bool animate)
        {
            string path = Path.Combine(imagesDir, fileName);
            try
            {
                using (Image image = Image.FromFile(path))
                {
                    stage.SetFlag(new Bitmap(image), animate);
                }
            }
            catch (FileNotFoundException)
            {
                window.Toast("Ошибка", "Не найден файл рисунка:\n" + path, Tone.Bad);
            }
        }

        // Кнопка "Измени надпись".
        private void btnChangeCaption_Click(object sender, EventArgs e)
        {
            lblCaption.SetText("Флаг России");
            lblCaption.SetFace("semi");
            LoadFlag("FlagRUS.png", true);

            // В задании здесь намеренно сделана ошибка в имени свойства Size:
            //     lblCaption.Font.Sie = 14;
            // Компилятор C# выдаёт ошибку CS1061: "Font" не содержит определения "Sie".
            // Кроме того, свойство Font.Size доступно только для чтения,
            // поэтому исправленный вариант задаёт новый размер шрифта целиком:
            lblCaption.SetSize(14F);
        }

        // Кнопка "Сдвинь рисунок вправо" (PictureAlignment = 4 в VBA).
        private void btnMoveRight_Click(object sender, EventArgs e)
        {
            stage.SetMosaic(false);
            stage.MoveRight(true);
        }

        // Кнопка "Мозаика" (PictureTiling = True в VBA).
        private void btnMosaic_Click(object sender, EventArgs e)
        {
            stage.SetMosaic(true);
        }

        // Кнопка "Измени цвет фона и формы".
        private void btnColors_Click(object sender, EventArgs e)
        {
            stage.SetPink(true);       // фон рисунка: &HFF80FF
            TintWindow(true);          // цвет формы: RGB(64, 0, 0)
        }

        // Кнопка "Смена флага и надписи".
        private void btnChangeFlag_Click(object sender, EventArgs e)
        {
            lblCaption.SetText("Флаг Англии");
            lblCaption.SetFace("display");   // жирное начертание — аналог Arial Black из задания
            lblCaption.SetSize(14F);
            LoadFlag("FlagENG.png", true);
        }

        private void ResetStage(bool animate)
        {
            lblCaption.SetText("Флаг", animate);
            lblCaption.SetFace("semi");
            lblCaption.SetSize(11F, animate);
            LoadFlag("FlagRUS.png", animate);
            stage.SetMosaic(false);
            stage.MoveRight(false);
            stage.SetPink(false);
            TintWindow(false);
        }

        /// <summary>Плавная перекраска фона окна в цвет формы из задания — RGB(64, 0, 0) — и обратно.</summary>
        private void TintWindow(bool on)
        {
            float from = Backdrop.Tint.A / 200f, to = on ? 1f : 0f;
            if (Math.Abs(from - to) < 0.01f) return;
            Animator.Run(this, "tint", from, to, 800, Ease.InOutCubic, delegate(float v)
            {
                Backdrop.Tint = Color.FromArgb((int)(200 * Math.Max(0f, Math.Min(1f, v))), 64, 0, 0);
                Backdrop.Refresh();
            });
        }

        /// <summary>Страница открыта: возвращаем цвет формы, если он был изменён.</summary>
        public void OnEnter() { if (stage.IsPink) TintWindow(true); }

        /// <summary>Страница закрыта: остальные разделы показываются с обычным фоном.</summary>
        public void OnLeave() { TintWindow(false); }

        // Задание 2: запуск модуля, содержащего ошибку.
        private void btnRunBug_Click(object sender, EventArgs e)
        {
            if (debugView.Running) return;
            lblResult.Tone = Tone.Dim;
            lblResult.Text = "Выполняется по шагам…";
            debugView.Run(true, delegate
            {
                try
                {
                    double average = DebugModule.AverageWithBug(testData);
                    lblResult.Text = "Результат (с ошибкой): " + average;
                }
                catch (IndexOutOfRangeException ex)
                {
                    // Ошибка выполнения: отладчик останавливается на строке с values[i].
                    lblResult.Tone = Tone.Bad;
                    lblResult.Text = "Ошибка выполнения: " + ex.Message;
                }
            });
        }

        // Задание 2: запуск модуля после отладки.
        private void btnRunFixed_Click(object sender, EventArgs e)
        {
            if (debugView.Running) return;
            lblResult.Tone = Tone.Dim;
            lblResult.Text = "Выполняется по шагам…";
            debugView.Run(false, delegate
            {
                double average = DebugModule.AverageFixed(testData);
                lblResult.Tone = Tone.Good;
                lblResult.Text = "Результат (после отладки): " + average.ToString("F2");
            });
        }
    }
}
