using GameHub.Shared.Modules;
using GameHub.Steam.Endpoints;
using GameHub.Steam.Options;
using GameHub.Steam.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GameHub.Steam;

public sealed class SteamModule : IModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SteamOptions>().Bind(configuration.GetSection(SteamOptions.SectionName));
        services.AddMemoryCache();

        services.AddHttpClient<SteamWebApiService>((sp, client) =>
        {
            var baseUrl = sp.GetRequiredService<IOptions<SteamOptions>>().Value.BaseUrl;
            client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // Callers ask for ISteamService and transparently get the cached version.
        services.AddScoped<ISteamService>(sp => new CachingSteamService(
            sp.GetRequiredService<SteamWebApiService>(),
            sp.GetRequiredService<IMemoryCache>(),
            TimeSpan.FromMinutes(sp.GetRequiredService<IOptions<SteamOptions>>().Value.CacheMinutes)));
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) => SteamEndpoints.Map(endpoints);
}
