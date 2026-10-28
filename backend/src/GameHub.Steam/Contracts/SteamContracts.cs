namespace GameHub.Steam.Contracts;

public static class SteamSectionStatus
{
    /// <summary>Data returned normally.</summary>
    public const string Ok = "ok";

    /// <summary>The user's Steam privacy settings hide this data from us.</summary>
    public const string Private = "private";

    /// <summary>Steam has nothing to show (for example, a game without achievements).</summary>
    public const string NoData = "nodata";
}

/// <summary>
/// Steam hides data based on each user's privacy settings. That is a normal outcome, not an error,
/// so every section says whether it was available and the UI can explain it.
/// </summary>
public sealed record SteamSection<T>(string Status, T? Data)
{
    public static SteamSection<T> Ok(T data) => new(SteamSectionStatus.Ok, data);

    public static SteamSection<T> Private() => new(SteamSectionStatus.Private, default);

    public static SteamSection<T> NoData() => new(SteamSectionStatus.NoData, default);
}

public sealed record SteamProfile(
    string SteamId,
    string PersonaName,
    string ProfileUrl,
    string AvatarUrl,
    string Status,
    string? CurrentGame,
    string? CountryCode,
    DateTimeOffset? MemberSince,
    bool IsPublic);

public sealed record SteamGame(
    int AppId,
    string Name,
    int PlaytimeMinutes,
    int PlaytimeLastTwoWeeksMinutes,
    DateTimeOffset? LastPlayed,
    string? IconUrl,
    string HeaderUrl);

public sealed record SteamGameLibrary(int Count, int TotalPlaytimeMinutes, IReadOnlyList<SteamGame> Games);

public sealed record SteamFriend(
    string SteamId,
    string PersonaName,
    string AvatarUrl,
    string ProfileUrl,
    string Status,
    DateTimeOffset? FriendSince);

public sealed record SteamAchievement(
    string ApiName,
    string Name,
    string? Description,
    bool Achieved,
    DateTimeOffset? UnlockedAt);

public sealed record SteamAchievements(
    string GameName,
    int Unlocked,
    int Total,
    IReadOnlyList<SteamAchievement> Achievements);
