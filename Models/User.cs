namespace RealEstateRental.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public int? TenantId { get; set; }
        public Tenant? Tenant { get; set; }
    }
}
