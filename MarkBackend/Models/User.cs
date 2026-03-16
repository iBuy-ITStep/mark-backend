using Microsoft.AspNetCore.Identity;

namespace MarkBackend.Models
{
    public class User : IdentityUser
    {
        // API Refresh Token support
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}