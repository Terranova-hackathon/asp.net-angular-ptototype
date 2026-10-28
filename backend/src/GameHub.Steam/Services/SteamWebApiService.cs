using System.Net;
using System.Text.Json;
using GameHub.Steam.Contracts;
using GameHub.Steam.Options;
using GameHub.Steam.SteamApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace GameHub.Steam.Services;

/// <summary>Talks to the Steam Web API and maps its responses to our own contracts.</summary>
internal sealed class SteamWebApiService(HttpClient http, IOptions<SteamOptions> options) : ISteamService
{
    // Steam accepts up to 100 ids per GetPlayerSummaries call; cap friends so one page view stays cheap.
    private const int SummaryBatchSize = 100;
    private const int MaxFriends = 500;

    private readonly string _apiKey = options.Value.ApiKey;

    public async Task<SteamProfile?> GetProfileAsync(string steamId, CancellationToken cancellationToken = default)
    {
        var players = await GetPlayersAsync([steamId], cancellationToken);
        return players.TryGetValue(steamId, out var player) ? ToProfile(player) : null;
    }

    public async Task<SteamSection<SteamGameLibrary>> GetOwnedGamesAsync(
        string steamId, CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendAsync(
            "IPlayerService/GetOwnedGames/v1/",
            new() { ["steamid"] = steamId, ["include_appinfo"] = "1", ["include_played_free_games"] = "1" },
            cancellationToken);
        EnsureSuccess(status);

        var response = Parse<GamesEnvelope>(body).Response;

        // Steam answers 200 with an empty object when the user hides their game details.
        if (response is null || (response.GameCount is null && response.Games is null))
        {
            return SteamSection<SteamGameLibrary>.Private();
        }

        var games = (response.Games ?? [])
            .Select(ToGame)
            .OrderByDescending(game => game.PlaytimeMinutes)
            .ToList();

        return SteamSection<SteamGameLibrary>.Ok(new SteamGameLibrary(
            response.GameCount ?? games.Count, games.Sum(game => game.PlaytimeMinutes), games));
    }

    public async Task<SteamSection<IReadOnlyList<SteamGame>>> GetRecentGamesAsync(
        string steamId, CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendAsync(
            "IPlayerService/GetRecentlyPlayedGames/v1/", new() { ["steamid"] = steamId }, cancellationToken);
        EnsureSuccess(status);

        var response = Parse<GamesEnvelope>(body).Response;
        if (response is null || (response.TotalCount is null && response.Games is null))
        {
            return SteamSection<IReadOnlyList<SteamGame>>.Private();
        }

        IReadOnlyList<SteamGame> games = (response.Games ?? [])
            .Select(ToGame)
            .OrderByDescending(game => game.PlaytimeLastTwoWeeksMinutes)
            .ToList();

        return SteamSection<IReadOnlyList<SteamGame>>.Ok(games);
    }

    public async Task<SteamSection<IReadOnlyList<SteamFriend>>> GetFriendsAsync(
        string steamId, CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendAsync(
            "ISteamUser/GetFriendList/v1/",
            new() { ["steamid"] = steamId, ["relationship"] = "friend" },
            cancellationToken);

        // Steam answers 401 when the friends list is not public.
        if (status == HttpStatusCode.Unauthorized)
        {
            return SteamSection<IReadOnlyList<SteamFriend>>.Private();
        }

        EnsureSuccess(status);

        var entries = (Parse<FriendsEnvelope>(body).FriendsList?.Friends ?? []).Take(MaxFriends).ToList();
        var players = await GetPlayersAsync(entries.Select(entry => entry.SteamId).ToList(), cancellationToken);

        IReadOnlyList<SteamFriend> friends = entries
            .Select(entry => players.TryGetValue(entry.SteamId, out var player)
                ? ToFriend(player, entry)
                : new SteamFriend(entry.SteamId, entry.SteamId, string.Empty,
                    $"https://steamcommunity.com/profiles/{entry.SteamId}", "offline", ToDate(entry.FriendSince)))
            .OrderBy(friend => friend.PersonaName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return SteamSection<IReadOnlyList<SteamFriend>>.Ok(friends);
    }

    public async Task<SteamSection<SteamAchievements>> GetAchievementsAsync(
        string steamId, int appId, CancellationToken cancellationToken = default)
    {
        var (status, body) = await SendAsync(
            "ISteamUserStats/GetPlayerAchievements/v1/",
            new() { ["steamid"] = steamId, ["appid"] = appId.ToString(), ["l"] = "english" },
            cancellationToken);

        if (status is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
        {
            // A rejected key also gives 403, but with an HTML body, so only a Steam JSON message counts as privacy.
            var error = TryParse<AchievementsEnvelope>(body)?.PlayerStats?.Error;
            if (error?.Contains("not public", StringComparison.OrdinalIgnoreCase) == true)
            {
                return SteamSection<SteamAchievements>.Private();
            }

            if (error?.Contains("no stats", StringComparison.OrdinalIgnoreCase) == true)
            {
                return SteamSection<SteamAchievements>.NoData();
            }
        }

        EnsureSuccess(status);

        var stats = Parse<AchievementsEnvelope>(body).PlayerStats;
        if (stats?.Achievements is not { Count: > 0 } entries)
        {
            return SteamSection<SteamAchievements>.NoData();
        }

        var achievements = entries
            .Select(entry => new SteamAchievement(
                entry.ApiName,
                entry.Name ?? entry.ApiName,
                string.IsNullOrWhiteSpace(entry.Description) ? null : entry.Description,
                entry.Achieved == 1,
                entry.Achieved == 1 ? ToDate(entry.UnlockTime) : null))
            .OrderByDescending(a => a.Achieved)
            .ThenByDescending(a => a.UnlockedAt)
            .ThenBy(a => a.Name)
            .ToList();

        return SteamSection<SteamAchievements>.Ok(new SteamAchievements(
            stats.GameName ?? $"App {appId}", achievements.Count(a => a.Achieved), achievements.Count, achievements));
    }

    private async Task<Dictionary<string, SteamPlayer>> GetPlayersAsync(
        IReadOnlyList<string> steamIds, CancellationToken cancellationToken)
    {
        var players = new Dictionary<string, SteamPlayer>();

        foreach (var batch in steamIds.Chunk(SummaryBatchSize))
        {
            var (status, body) = await SendAsync(
                "ISteamUser/GetPlayerSummaries/v2/",
                new() { ["steamids"] = string.Join(',', batch) },
                cancellationToken);
            EnsureSuccess(status);

            foreach (var player in Parse<PlayerSummariesEnvelope>(body).Response?.Players ?? [])
            {
                players[player.SteamId] = player;
            }
        }

        return players;
    }

    private async Task<(HttpStatusCode Status, string Body)> SendAsync(
        string path, Dictionary<string, string?> query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new SteamUnavailableException("The Steam API key is not configured (Steam__ApiKey).");
        }

        query["key"] = _apiKey;

        try
        {
            using var response = await http.GetAsync(QueryHelpers.AddQueryString(path, query), cancellationToken);
            return (response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // The message is replaced on purpose: the original exception text can include the request URL (and our key).
            throw new SteamUnavailableException("Steam is currently unavailable.", ex);
        }
    }

    private static void EnsureSuccess(HttpStatusCode status)
    {
        if (status != HttpStatusCode.OK)
        {
            throw new SteamUnavailableException($"Steam returned an unexpected response ({(int)status}).");
        }
    }

    private static T Parse<T>(string body) where T : class =>
        TryParse<T>(body) ?? throw new SteamUnavailableException("Steam returned an unreadable response.");

    private static T? TryParse<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SteamProfile ToProfile(SteamPlayer player) => new(
        player.SteamId,
        player.PersonaName ?? player.SteamId,
        player.ProfileUrl ?? $"https://steamcommunity.com/profiles/{player.SteamId}",
        player.AvatarFull ?? string.Empty,
        ToStatus(player.PersonaState),
        player.GameExtraInfo,
        player.CountryCode,
        player.TimeCreated is > 0 ? ToDate(player.TimeCreated.Value) : null,
        player.CommunityVisibilityState == 3);

    private static SteamFriend ToFriend(SteamPlayer player, SteamFriendEntry entry) => new(
        player.SteamId,
        player.PersonaName ?? player.SteamId,
        player.AvatarFull ?? string.Empty,
        player.ProfileUrl ?? $"https://steamcommunity.com/profiles/{player.SteamId}",
        ToStatus(player.PersonaState),
        ToDate(entry.FriendSince));

    private static SteamGame ToGame(SteamOwnedGame game) => new(
        game.AppId,
        game.Name ?? $"App {game.AppId}",
        game.PlaytimeForever,
        game.Playtime2Weeks,
        game.LastPlayed is > 0 ? ToDate(game.LastPlayed.Value) : null,
        string.IsNullOrEmpty(game.IconHash)
            ? null
            : $"https://media.steampowered.com/steamcommunity/public/images/apps/{game.AppId}/{game.IconHash}.jpg",
        $"https://cdn.cloudflare.steamstatic.com/steam/apps/{game.AppId}/header.jpg");

    private static DateTimeOffset? ToDate(long unixSeconds) =>
        unixSeconds > 0 ? DateTimeOffset.FromUnixTimeSeconds(unixSeconds) : null;

    private static string ToStatus(int personaState) => personaState switch
    {
        1 => "online",
        2 => "busy",
        3 => "away",
        4 => "snooze",
        5 => "looking to trade",
        6 => "looking to play",
        _ => "offline",
    };
}
