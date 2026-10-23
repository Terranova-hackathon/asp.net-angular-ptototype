using GameHub.Games.Contracts;
using GameHub.Shared.Paging;
using Microsoft.Extensions.Caching.Memory;

namespace GameHub.Games.Services;

/// <summary>
/// Decorator: same interface as the real service, but remembers answers for a while.
/// This saves RAWG's rate-limited quota and makes repeat searches instant.
/// </summary>
public sealed class CachingGamesService(IGamesService inner, IMemoryCache cache, TimeSpan ttl) : IGamesService
{
    public async Task<PagedResult<GameSummary>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var key = $"games:search:{search?.Trim().ToLowerInvariant()}:{page}:{pageSize}";
        return (await GetOrCreateAsync(key, async () => await inner.SearchAsync(search, page, pageSize, cancellationToken)))!;
    }

    public Task<GameDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync($"games:detail:{id}", () => inner.GetByIdAsync(id, cancellationToken));

    private async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory) where T : class
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        var value = await factory();

        // Only cache real answers; failures throw and "not found" (null) is retried next time.
        if (value is not null)
        {
            cache.Set(key, value, ttl);
        }

        return value;
    }
}
