using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameHub.Favorites.Data;

/// <summary>Used only by `dotnet ef` so migrations can be created without starting the whole app.</summary>
internal sealed class FavoritesDbContextFactory : IDesignTimeDbContextFactory<FavoritesDbContext>
{
    public FavoritesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<FavoritesDbContext>()
            .UseSqlServer(
                "Server=.;Database=GameHub;",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", FavoritesDbContext.Schema))
            .Options;

        return new FavoritesDbContext(options);
    }
}
