using GameHub.Steam.Contracts;
using Microsoft.Extensions.Caching.Memory;

namespace GameHub.Steam.Services;

/// <summary>
/// Decorator: same interface as the real service, but remembers answers for a while.
/// A profile page fires several requests at once and Steam rate-limits, so this keeps repeat views cheap.
/// </summary>
internal sealed class CachingSteamService(ISteamService inner, IMemoryCache cache, TimeSpan ttl) : ISteamService
{
    public Task<SteamProfile?> GetProfileAsync(string steamId, CancellationToken cancellationToken = default) =>
        GetOrCreateAsync($"steam:profile:{steamId}", () => inner.GetProfileAsync(steamId, cancellationToken));

    public Task<SteamSection<SteamGameLibrary>> GetOwnedGamesAsync(string steamId, CancellationToken cancellationToken = default) =>
        GetOrCreateSectionAsync($"steam:games:{steamId}", () => inner.GetOwnedGamesAsync(steamId, cancellationToken));

    public Task<SteamSection<IReadOnlyList<SteamGame>>> GetRecentGamesAsync(string steamId, CancellationToken cancellationToken = default) =>
        GetOrCreateSectionAsync($"steam:recent:{steamId}", () => inner.GetRecentGamesAsync(steamId, cancellationToken));

    public Task<SteamSection<IReadOnlyList<SteamFriend>>> GetFriendsAsync(string steamId, CancellationToken cancellationToken = default) =>
        GetOrCreateSectionAsync($"steam:friends:{steamId}", () => inner.GetFriendsAsync(steamId, cancellationToken));

    public Task<SteamSection<SteamAchievements>> GetAchievementsAsync(string steamId, int appId, CancellationToken cancellationToken = default) =>
        GetOrCreateSectionAsync($"steam:achievements:{steamId}:{appId}", () => inner.GetAchievementsAsync(steamId, appId, cancellationToken));

    // Private sections are not cached, so a user who just changed their privacy settings sees the result right away.
    private async Task<SteamSection<T>> GetOrCreateSectionAsync<T>(string key, Func<Task<SteamSection<T>>> factory)
    {
        var section = await GetOrCreateAsync(key, async () =>
        {
            var result = await factory();
            return result.Status == SteamSectionStatus.Private ? null : result;
        });

        return section ?? SteamSection<T>.Private();
    }

    private async Task<T?> GetOrCreateAsync<T>(string key, Func<Task<T?>> factory) where T : class
    {
        if (cache.TryGetValue(key, out T? cached))
        {
            return cached;
        }

        var value = await factory();
        if (value is not null)
        {
            cache.Set(key, value, ttl);
        }

        return value;
    }
}
