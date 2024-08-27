using Microsoft.AspNetCore.Identity;

namespace MyWebApi.Data
{
    public class ApplicationUser : IdentityUser
    {
        public string FirstName { get; set; } = null!;
        public string LastName { get; set; } = null!;

        public ICollection<RefreshToken> RefreshTokens { get; set; }
    }
}
