using Microsoft.AspNetCore.Identity;

namespace GameHub.Auth.Domain;

public class AppUser : IdentityUser
{
    public string? DisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
