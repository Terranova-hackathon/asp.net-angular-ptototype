namespace GameHub.Games.Options;

public sealed class RawgOptions
{
    public const string SectionName = "Rawg";

    public string ApiKey { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = "https://api.rawg.io/api/";

    public int CacheMinutes { get; init; } = 10;
}
