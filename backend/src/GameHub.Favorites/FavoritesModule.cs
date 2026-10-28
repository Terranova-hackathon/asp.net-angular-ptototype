using GameHub.Favorites.Data;
using GameHub.Favorites.Endpoints;
using GameHub.Favorites.Services;
using GameHub.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameHub.Favorites;

public sealed class FavoritesModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<FavoritesDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("Default"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", FavoritesDbContext.Schema)));

        services.AddScoped<FavoritesService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => FavoritesEndpoints.Map(endpoints);

    public async Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        // Modules start in order and Auth already waited for SQL Server, so a plain migrate is enough here.
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<FavoritesDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }
}
