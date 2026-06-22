using System.Linq;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using RealEstateRental.Data;
using RealEstateRental.Models;
using RealEstateRental.Security;

namespace RealEstateRental
{
    public partial class LoginWindow : Window
    {
        private readonly AppDbContext _context = new AppDbContext();

        public LoginWindow()
        {
            InitializeComponent();
            UsernameBox.Focus();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            var username = UsernameBox.Text?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                MessageBox.Show("Введите имя пользователя.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var password = PasswordBox.Password ?? string.Empty;
            if (password.Length == 0)
            {
                MessageBox.Show("Введите пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var user = _context.Users
                .Include(u => u.Tenant)
                .FirstOrDefault(u => u.Username == username);

            if (user == null || !PasswordHasher.Verify(password, user.PasswordHash))
            {
                MessageBox.Show("Неверное имя пользователя или пароль.", "Вход", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (user.Role == UserRole.Tenant && !user.TenantId.HasValue)
            {
                MessageBox.Show("Учётная запись арендатора не привязана к карточке арендатора. Обратитесь к администратору.", "Вход",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            App.Auth.Set(user);
            DialogResult = true;
            Close();
        }

        private void Register_Click(object sender, RoutedEventArgs e)
        {
            var tenantName = RegTenantNameBox.Text?.Trim() ?? string.Empty;
            if (tenantName.Length == 0)
            {
                MessageBox.Show("Введите название организации или ФИО.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var username = RegUsernameBox.Text?.Trim() ?? string.Empty;
            if (username.Length == 0)
            {
                MessageBox.Show("Введите логин.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var password = RegPasswordBox.Password ?? string.Empty;
            var confirm = RegConfirmPasswordBox.Password ?? string.Empty;
            if (password.Length == 0)
            {
                MessageBox.Show("Введите пароль.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!string.Equals(password, confirm, StringComparison.Ordinal))
            {
                MessageBox.Show("Пароли не совпадают.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_context.Users.Any(u => u.Username == username))
            {
                MessageBox.Show("Пользователь с таким логином уже существует.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using var tx = _context.Database.BeginTransaction();

                var tenant = new Tenant { Name = tenantName };
                _context.Tenants.Add(tenant);
                _context.SaveChanges();

                var user = new User
                {
                    Username = username,
                    PasswordHash = PasswordHasher.Hash(password),
                    Role = UserRole.Tenant,
                    TenantId = tenant.Id
                };
                _context.Users.Add(user);
                _context.SaveChanges();

                tx.Commit();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось зарегистрироваться: " + ex.Message, "Регистрация", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show("Регистрация выполнена. Войдите под своим логином.", "Регистрация", MessageBoxButton.OK, MessageBoxImage.Information);
            RegTenantNameBox.Clear();
            RegUsernameBox.Clear();
            RegPasswordBox.Password = string.Empty;
            RegConfirmPasswordBox.Password = string.Empty;
            MainTabs.SelectedIndex = 0;
            UsernameBox.Text = username;
            PasswordBox.Password = string.Empty;
            UsernameBox.Focus();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            _context.Dispose();
        }
    }
}
