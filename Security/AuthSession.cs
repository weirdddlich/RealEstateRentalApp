using RealEstateRental.Models;

namespace RealEstateRental.Security
{
    public sealed class AuthSession
    {
        public int UserId { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public UserRole Role { get; private set; }
        public int? TenantId { get; private set; }

        public bool IsLandlord => Role == UserRole.Landlord;

        public void Set(User user)
        {
            UserId = user.Id;
            Username = user.Username;
            Role = user.Role;
            TenantId = user.TenantId;
        }

        public void Clear()
        {
            UserId = 0;
            Username = string.Empty;
            Role = UserRole.Landlord;
            TenantId = null;
        }
    }
}
