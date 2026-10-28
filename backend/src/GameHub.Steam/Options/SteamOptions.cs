namespace GameHub.Steam.Options;

public sealed class SteamOptions
{
    public const string SectionName = "Steam";

    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.steampowered.com/";

    public int CacheMinutes { get; init; } = 5;
}
