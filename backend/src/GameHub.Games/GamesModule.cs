using GameHub.Games.Contracts;
using GameHub.Games.Endpoints;
using GameHub.Games.Options;
using GameHub.Games.Services;
using GameHub.Shared.Catalog;
using GameHub.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameHub.Games;

public sealed class GamesModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RawgOptions>().Bind(configuration.GetSection(RawgOptions.SectionName));
        services.AddMemoryCache();

        services.AddHttpClient<RawgGamesService>((sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IOptions<RawgOptions>>().Value.BaseUrl;
            client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Callers ask for IGamesService and transparently get the cached version.
        services.AddScoped<IGamesService>(sp => new CachingGamesService(
            sp.GetRequiredService<RawgGamesService>(),
            sp.GetRequiredService<IMemoryCache>(),
            TimeSpan.FromMinutes(sp.GetRequiredService<IOptions<RawgOptions>>().Value.CacheMinutes)));

        services.AddScoped<IGameCatalog, GameCatalog>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => GamesEndpoints.Map(endpoints);

    /// <summary>
    /// Pre-fills the cache with what the first visitor asks for (filter lists and the default first page),
    /// so their first load is instant. Runs in the background: startup never waits for RAWG and never fails because of it.
    /// </summary>
    public Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger<GamesModule>();
            try
            {
                await using var scope = services.CreateAsyncScope();
                var games = scope.ServiceProvider.GetRequiredService<IGamesService>();

                // Same arguments the games page sends on first load, so the cache keys match.
                await Task.WhenAll(
                    games.GetFilterOptionsAsync(),
                    games.SearchAsync(null, 1, 20, GameSearchFilters.None));
                logger.LogInformation("Games cache warmed.");
            }
            catch (Exception ex)
            {
                // Missing API key or RAWG down: the first real request will simply fetch (and report) as usual.
                logger.LogWarning(ex, "Games cache warm-up skipped.");
            }
        });

        return Task.CompletedTask;
    }
}
