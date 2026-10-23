using GameHub.Auth.Domain;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Auth.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Each module owns its own schema so modules can later be split into separate databases.
        builder.HasDefaultSchema(Schema);
        base.OnModelCreating(builder);
    }
}
