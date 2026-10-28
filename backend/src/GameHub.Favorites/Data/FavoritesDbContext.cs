using GameHub.Favorites.Domain;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Favorites.Data;

public class FavoritesDbContext(DbContextOptions<FavoritesDbContext> options) : DbContext(options)
{
    public const string Schema = "favorites";

    public DbSet<Favorite> Favorites => Set<Favorite>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        builder.Entity<Favorite>(favorite =>
        {
            favorite.HasKey(f => new { f.UserId, f.RawgId });
            // Matches AspNetUsers.Id so the two can be joined if the modules ever share a database.
            favorite.Property(f => f.UserId).HasMaxLength(450);
            favorite.Property(f => f.Name).HasMaxLength(300).IsRequired();
            favorite.Property(f => f.ImageUrl).HasMaxLength(500);

            // Answers "how many people favorited this game?" for future social features.
            favorite.HasIndex(f => f.RawgId);
        });
    }
}
