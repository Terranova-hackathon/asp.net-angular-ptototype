using GameHub.Steam.Contracts;

namespace GameHub.Steam.Services;

internal interface ISteamService
{
    Task<SteamProfile?> GetProfileAsync(string steamId, CancellationToken cancellationToken = default);

    Task<SteamSection<SteamGameLibrary>> GetOwnedGamesAsync(string steamId, CancellationToken cancellationToken = default);

    Task<SteamSection<IReadOnlyList<SteamGame>>> GetRecentGamesAsync(string steamId, CancellationToken cancellationToken = default);

    Task<SteamSection<IReadOnlyList<SteamFriend>>> GetFriendsAsync(string steamId, CancellationToken cancellationToken = default);

    Task<SteamSection<SteamAchievements>> GetAchievementsAsync(string steamId, int appId, CancellationToken cancellationToken = default);
}

/// <summary>Steam could not be reached, rejected our key, or is not configured.</summary>
internal sealed class SteamUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
