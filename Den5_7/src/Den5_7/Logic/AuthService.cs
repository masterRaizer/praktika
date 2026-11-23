using Den5_7.Data;
using Den5_7.Models;

namespace Den5_7.Logic
{
    /// <summary>Вход и выход пользователя.</summary>
    public sealed class AuthService
    {
        public const string WrongCredentials = "Неверный e-mail или пароль.";

        private readonly UserRepository users = new UserRepository();
        private readonly JournalRepository journal = new JournalRepository();

        /// <summary>
        /// Проверка учётных данных. Для несуществующего e-mail и неверного пароля сообщение одинаковое,
        /// чтобы по нему нельзя было узнать, зарегистрирован ли адрес.
        /// </summary>
        public User Login(string email, string password)
        {
            email = (email ?? "").Trim();
            if (email.Length == 0 || string.IsNullOrEmpty(password))
            {
                throw new ValidationException("Введите e-mail и пароль.");
            }
            if (email.Length > 100 || password.Length > 200)
            {
                throw new ValidationException(WrongCredentials);
            }
            UserRepository.UserRecord record = users.FindByEmail(email);
            if (record == null || !PasswordHasher.Verify(password, record.PasswordHash))
            {
                journal.Add(record == null ? (int?)null : record.User.Id, "Неудачная попытка входа", email);
                throw new ValidationException(WrongCredentials);
            }
            if (record.IsBlocked)
            {
                journal.Add(record.User.Id, "Попытка входа заблокированного пользователя");
                throw new ValidationException("Учётная запись заблокирована. Обратитесь к администратору.");
            }
            journal.Add(record.User.Id, "Вход в систему");
            return record.User;
        }

        public void Logout(User user)
        {
            if (user != null) journal.Add(user.Id, "Выход из системы");
        }
    }
}
