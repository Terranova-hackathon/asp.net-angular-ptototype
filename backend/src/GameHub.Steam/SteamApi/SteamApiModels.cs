using System.Text.Json.Serialization;

namespace GameHub.Steam.SteamApi;

// Shapes of Steam's JSON. They stay inside this module; callers only see our own contracts.

internal sealed class PlayerSummariesEnvelope
{
    [JsonPropertyName("response")] public PlayerSummariesResponse? Response { get; init; }
}

internal sealed class PlayerSummariesResponse
{
    [JsonPropertyName("players")] public List<SteamPlayer> Players { get; init; } = [];
}

internal sealed class SteamPlayer
{
    [JsonPropertyName("steamid")] public string SteamId { get; init; } = string.Empty;
    [JsonPropertyName("personaname")] public string? PersonaName { get; init; }
    [JsonPropertyName("profileurl")] public string? ProfileUrl { get; init; }
    [JsonPropertyName("avatarfull")] public string? AvatarFull { get; init; }
    [JsonPropertyName("personastate")] public int PersonaState { get; init; }
    [JsonPropertyName("communityvisibilitystate")] public int CommunityVisibilityState { get; init; }
    [JsonPropertyName("gameextrainfo")] public string? GameExtraInfo { get; init; }
    [JsonPropertyName("loccountrycode")] public string? CountryCode { get; init; }
    [JsonPropertyName("timecreated")] public long? TimeCreated { get; init; }
}

internal sealed class GamesEnvelope
{
    [JsonPropertyName("response")] public GamesResponse? Response { get; init; }
}

internal sealed class GamesResponse
{
    // Both are missing (empty response) when the user hides their game details.
    [JsonPropertyName("game_count")] public int? GameCount { get; init; }
    [JsonPropertyName("total_count")] public int? TotalCount { get; init; }
    [JsonPropertyName("games")] public List<SteamOwnedGame>? Games { get; init; }
}

internal sealed class SteamOwnedGame
{
    [JsonPropertyName("appid")] public int AppId { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("playtime_forever")] public int PlaytimeForever { get; init; }
    [JsonPropertyName("playtime_2weeks")] public int Playtime2Weeks { get; init; }
    [JsonPropertyName("img_icon_url")] public string? IconHash { get; init; }
    [JsonPropertyName("rtime_last_played")] public long? LastPlayed { get; init; }
}

internal sealed class FriendsEnvelope
{
    [JsonPropertyName("friendslist")] public FriendsList? FriendsList { get; init; }
}

internal sealed class FriendsList
{
    [JsonPropertyName("friends")] public List<SteamFriendEntry> Friends { get; init; } = [];
}

internal sealed class SteamFriendEntry
{
    [JsonPropertyName("steamid")] public string SteamId { get; init; } = string.Empty;
    [JsonPropertyName("friend_since")] public long FriendSince { get; init; }
}

internal sealed class AchievementsEnvelope
{
    [JsonPropertyName("playerstats")] public PlayerStats? PlayerStats { get; init; }
}

internal sealed class PlayerStats
{
    [JsonPropertyName("gameName")] public string? GameName { get; init; }
    [JsonPropertyName("achievements")] public List<SteamAchievementEntry>? Achievements { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
}

internal sealed class SteamAchievementEntry
{
    [JsonPropertyName("apiname")] public string ApiName { get; init; } = string.Empty;
    [JsonPropertyName("achieved")] public int Achieved { get; init; }
    [JsonPropertyName("unlocktime")] public long UnlockTime { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}
