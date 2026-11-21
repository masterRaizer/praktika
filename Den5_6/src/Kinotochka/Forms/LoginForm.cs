using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Data;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Forms
{
    /// <summary>Форма 1. Авторизация: вход по электронной почте и паролю, проверка подключения к базе.</summary>
    public sealed class LoginForm : EditDialog
    {
        private readonly AuthService auth;
        private readonly FxTextBox email, password;
        private readonly FxSwitch showPassword;
        private readonly FxLabel status;
        private readonly FxButton loginButton;
        private Timer lockTimer;

        /// <summary>Пользователь, успешно вошедший в систему.</summary>
        public User LoggedUser { get; private set; }

        public LoginForm(AuthService auth, string presetEmail)
        {
            this.auth = auth;
            Title = "Вход в систему";
            Subtitle = "Киноточка — продажа билетов в кинотеатр";
            IconName = "film";
            Text = "Киноточка — вход в систему";
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            Ui.ApplyIcon(this);
            Size = new Size(500, 500);

            status = new FxLabel("Проверка подключения к базе данных…", Fonts.Medium(8.8f), Tone.Dim, true) { Icon = "database" };
            email = Register("email", Input("ivanov@example.com", presetEmail));
            password = Register("password", Input("пароль"));
            password.Box.UseSystemPasswordChar = true;
            showPassword = new FxSwitch { IconOn = "check" };
            showPassword.CheckedChanged += delegate { password.Box.UseSystemPasswordChar = !showPassword.Checked; };

            var showRow = FxBox.H(10);
            showRow.Add(showPassword, 46, true);
            showRow.Fill(new FxLabel("Показать пароль", Fonts.Medium(9f), Tone.Dim));
            showRow.Height = 26;

            Body.Add(status, 0);
            Body.Add(Field("Электронная почта", email), 0);
            Body.Add(Field("Пароль", password), 0);
            Body.Add(showRow, 0);
            Body.Add(new FxLabel("Нет учётной записи? Нажмите «Регистрация» — будет создан аккаунт зрителя. " +
                                 "Учётные записи кассиров и администраторов заводит администратор.",
                                 Fonts.Body(8.5f), Tone.Mute, true), 0);

            var register = new FxButton("Регистрация", false, "user").AutoWidth();
            register.Click += delegate { OpenRegister(); };
            AddFooterLeft(register);
            var connection = new FxButton("Подключение", false, "database").AutoWidth();
            connection.Click += delegate { OpenConnection(); };
            AddFooterLeft(connection);
            loginButton = AddButton("Войти", true, "arrow", delegate { DoLogin(); });

            email.EnterPressed += delegate { password.FocusBox(); };
            password.EnterPressed += delegate { DoLogin(); };
            Shown += delegate
            {
                if (email.Value.Length > 0) password.FocusBox();
                else email.FocusBox();
                CheckConnection();
            };
        }

        /// <summary>Проверка базы в фоне, чтобы окно не «замирало», пока сервер не отвечает.</summary>
        private void CheckConnection()
        {
            status.Tone = Tone.Dim;
            status.Icon = "database";
            status.Text = "Проверка подключения к базе данных (" + Db.ServerName + ")…";
            Task.Run(() => Db.TestConnection()).ContinueWith(t =>
            {
                if (IsDisposed || !IsHandleCreated) return;
                BeginInvoke((Action)delegate
                {
                    if (t.IsFaulted)
                    {
                        status.Tone = Tone.Bad;
                        status.Icon = "warn";
                        status.Text = "Нет подключения к базе данных (сервер " + Db.ServerName + "). Нажмите «Подключение», чтобы указать сервер и проверить связь.";
                    }
                    else
                    {
                        status.Tone = Tone.Good;
                        status.Icon = "check";
                        status.Text = "База данных подключена: " + t.Result + ".";
                    }
                    Body.PerformLayout();
                });
            });
        }

        private void DoLogin()
        {
            if (auth.SecondsLocked > 0)
            {
                ShowMessage("Слишком много неудачных попыток. Повторите вход через " + auth.SecondsLocked + " с.");
                return;
            }
            User user = null;
            bool ok = Try("вход в систему", delegate { user = auth.Login(email.Value, password.Value); });
            if (!ok)
            {
                password.Value = "";
                if (auth.SecondsLocked > 0) StartLockCountdown();
                return;
            }
            LoggedUser = user;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void StartLockCountdown()
        {
            loginButton.Enabled = false;
            if (lockTimer == null)
            {
                lockTimer = new Timer { Interval = 1000 };
                lockTimer.Tick += delegate
                {
                    int s = auth.SecondsLocked;
                    if (s > 0)
                    {
                        loginButton.Text = "Подождите " + s + " с";
                        return;
                    }
                    lockTimer.Stop();
                    loginButton.Text = "Войти";
                    loginButton.Enabled = true;
                    ShowMessage(null);
                };
            }
            loginButton.Text = "Подождите " + auth.SecondsLocked + " с";
            lockTimer.Start();
        }

        private void OpenRegister()
        {
            using (var f = new RegisterForm(auth))
            {
                if (f.ShowDialog(this) != DialogResult.OK) return;
                email.Value = f.Registered.Email;
                password.Value = "";
                ShowMessage("Учётная запись создана. Войдите с указанной почтой и паролем.", Tone.Good);
                password.FocusBox();
            }
        }

        private void OpenConnection()
        {
            using (var f = new ConnectionForm())
            {
                if (f.ShowDialog(this) == DialogResult.OK) CheckConnection();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && lockTimer != null) lockTimer.Dispose();
            base.Dispose(disposing);
        }
    }
}
