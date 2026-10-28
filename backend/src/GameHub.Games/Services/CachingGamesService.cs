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
    // Genre and platform lists almost never change, so they are kept much longer than search results.
    private static readonly TimeSpan OptionsTtl = TimeSpan.FromHours(6);

    public async Task<PagedResult<GameSummary>> SearchAsync(
        string? search, int page, int pageSize, GameSearchFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        filters ??= GameSearchFilters.None;
        var key = $"games:search:{search?.Trim().ToLowerInvariant()}:{page}:{pageSize}:{filters.ToCacheKey()}";
        return (await GetOrCreateAsync(key, async () =>
            await inner.SearchAsync(search, page, pageSize, filters, cancellationToken)))!;
    }

    public async Task<GameFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default) =>
        (await GetOrCreateAsync("games:filter-options",
            async () => await inner.GetFilterOptionsAsync(cancellationToken), OptionsTtl))!;

    public async Task<IReadOnlyList<CompanyOption>> SearchCompaniesAsync(
        CompanyKind kind, string search, CancellationToken cancellationToken = default) =>
        (await GetOrCreateAsync($"games:companies:{kind}:{search.Trim().ToLowerInvariant()}",
            async () => await inner.SearchCompaniesAsync(kind, search, cancellationToken)))!;

    public Task<GameDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync($"games:detail:{id}", () => inner.GetByIdAsync(id, cancellationToken));

    private async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? lifetime = null) where T : class
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        var value = await factory();

        // Only cache real answers; failures throw and "not found" (null) is retried next time.
        if (value is not null)
        {
            cache.Set(key, value, lifetime ?? ttl);
        }

        return value;
    }
}
