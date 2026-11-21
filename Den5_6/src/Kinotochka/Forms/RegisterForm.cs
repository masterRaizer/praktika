using System.Drawing;
using System.Windows.Forms;
using FxKit;
using Kinotochka.Data;
using Kinotochka.Logic;
using Kinotochka.Models;

namespace Kinotochka.Forms
{
    /// <summary>Форма 2. Регистрация зрителя.</summary>
    public sealed class RegisterForm : EditDialog
    {
        private readonly AuthService auth;
        private readonly FxTextBox fullName, email, phone, password, confirm;

        public User Registered { get; private set; }

        public RegisterForm(AuthService auth)
        {
            this.auth = auth;
            Title = "Регистрация";
            Subtitle = "Новая учётная запись зрителя";
            IconName = "user";
            Text = "Киноточка — регистрация";
            Size = new Size(560, 470);

            fullName = Register("fullName", Input("Иванов Иван Иванович"));
            email = Register("email", Input("ivanov@example.com"));
            phone = Register("phone", Input("+7 900 123-45-67 (необязательно)"));
            password = Register("password", Input("не короче 6 символов, буквы и цифры"));
            confirm = Register("confirm", Input("ещё раз"));
            password.Box.UseSystemPasswordChar = true;
            confirm.Box.UseSystemPasswordChar = true;

            Body.Add(Field("ФИО *", fullName), 0);
            Body.Add(Row(Field("Электронная почта *", email), Field("Телефон", phone)), 0);
            Body.Add(Row(Field("Пароль *", password), Field("Повтор пароля *", confirm)), 0);
            Body.Add(new FxLabel("Поля со звёздочкой обязательны. Пароль хранится в базе только в виде хеша PBKDF2-SHA256 с солью.",
                Fonts.Body(8.5f), Tone.Mute, true), 0);

            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Зарегистрироваться", true, "check", delegate { Save(); });
            confirm.EnterPressed += delegate { Save(); };
            Shown += delegate { fullName.FocusBox(); };
        }

        private void Save()
        {
            User u = null;
            if (!Try("регистрация", delegate { u = auth.Register(fullName.Value, email.Value, phone.Value, password.Value, confirm.Value); }))
                return;
            Registered = u;
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>Форма 3. Параметры подключения к SQL Server и проверка связи.</summary>
    public sealed class ConnectionForm : EditDialog
    {
        private readonly FxTextBox server;

        public ConnectionForm()
        {
            Title = "Подключение к базе данных";
            Subtitle = "База «Киноточка» на Microsoft SQL Server";
            IconName = "database";
            Text = "Киноточка — подключение";
            Size = new Size(560, 400);

            server = Register("server", Input(@"(localdb)\MSSQLLocalDB", Db.ServerName));
            Body.Add(Field("Сервер SQL Server", server), 0);
            Body.Add(new FxLabel(
                "Примеры: (localdb)\\MSSQLLocalDB — LocalDB из Visual Studio; .\\SQLEXPRESS — SQL Server Express; " +
                "localhost — полная версия SQL Server. Вход — учётной записью Windows. База должна быть создана " +
                "скриптами db\\01_create.sql и db\\02_data.sql.", Fonts.Body(8.8f), Tone.Dim, true), 0);

            var test = new FxButton("Проверить", false, "refresh").AutoWidth();
            test.Click += delegate { Test(); };
            AddFooterLeft(test);
            AddButton("Отмена", false, null, delegate { DialogResult = DialogResult.Cancel; Close(); });
            AddButton("Сохранить", true, "check", delegate { Save(); });
        }

        private string Server()
        {
            return Validator.Required("server", "Сервер SQL Server", server.Value, 200);
        }

        private void Test()
        {
            string previous = Db.LoadServer();
            string result = null;
            Cursor = Cursors.WaitCursor;
            bool ok = Try("проверка подключения", delegate
            {
                Db.SaveServer(Server());
                try { result = Db.TestConnection(); }
                finally { Db.SaveServer(previous); }
            });
            Cursor = Cursors.Default;
            if (ok) ShowMessage("Подключение установлено: " + result + ".", Tone.Good);
        }

        private void Save()
        {
            if (!Try("сохранение настроек подключения", delegate { Db.SaveServer(Server()); })) return;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
