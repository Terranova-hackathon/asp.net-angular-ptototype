using GameHub.Games.Endpoints;
using GameHub.Games.Options;
using GameHub.Games.Services;
using GameHub.Shared.Modules;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => GamesEndpoints.Map(endpoints);
}
