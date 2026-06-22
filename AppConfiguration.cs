using System.IO;
using System.Text.Json;

namespace RealEstateRental
{
    public static class AppConfiguration
    {
        private const string DefaultSqlServerConnection =
            "Server=(localdb)\\MSSQLLocalDB;Database=RealEstateRentalDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true";

        public static string GetConnectionString()
        {
            try
            {
                var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(path))
                    return DefaultSqlServerConnection;

                using var stream = File.OpenRead(path);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("ConnectionStrings", out var cs) &&
                    cs.TryGetProperty("DefaultConnection", out var el))
                {
                    var s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        return s;
                }
            }
            catch
            {
                // ignored
            }

            return DefaultSqlServerConnection;
        }
    }
}
