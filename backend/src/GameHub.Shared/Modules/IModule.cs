using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameHub.Shared.Modules;

/// <summary>
/// The only thing the host (GameHub.Api) knows about a feature.
/// Each feature module registers its own services and maps its own endpoints.
/// </summary>
public interface IModule
{
    void AddServices(IServiceCollection services, IConfiguration configuration);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    /// <summary>Optional startup work such as applying migrations.</summary>
    Task InitializeAsync(IServiceProvider services, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
