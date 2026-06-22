using System.Windows;
using RealEstateRental.Security;

namespace RealEstateRental
{
    public partial class App : Application
    {
        public static AuthSession Auth { get; } = new AuthSession();

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Иначе при закрытии окна входа приложение завершится до открытия MainWindow (OnLastWindowClose).
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var login = new LoginWindow();
            var ok = login.ShowDialog() == true;
            if (!ok)
            {
                Shutdown();
                return;
            }

            var main = new MainWindow();
            main.Show();
        }
    }
}