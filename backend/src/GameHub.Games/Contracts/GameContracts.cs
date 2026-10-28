namespace GameHub.Games.Contracts;

public sealed record GameSummary(
    int Id,
    string Name,
    string? Released,
    string? BackgroundImage,
    double Rating,
    int? Metacritic,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms);

public sealed record GameDetails(
    int Id,
    string Name,
    string? Released,
    string? BackgroundImage,
    double Rating,
    int? Metacritic,
    string? Description,
    string? Website,
    IReadOnlyList<string> Genres,
    IReadOnlyList<string> Platforms,
    IReadOnlyList<string> Developers,
    IReadOnlyList<string> Publishers);

/// <summary>
/// Already-validated search filters. Lists are OR-ed together within one filter (any selected genre matches);
/// different filters are AND-ed (genre AND platform AND ...).
/// </summary>
public sealed record GameSearchFilters(
    IReadOnlyList<string> Genres,
    IReadOnlyList<int> Platforms,
    IReadOnlyList<string> Developers,
    IReadOnlyList<string> Publishers,
    string? Ordering,
    int? MetacriticMin,
    int? MetacriticMax)
{
    public static GameSearchFilters None { get; } = new([], [], [], [], null, null, null);

    /// <summary>A stable text form used in cache keys, so different filters never share a cached page.</summary>
    public string ToCacheKey() => string.Join('|',
        string.Join(',', Genres.Order()),
        string.Join(',', Platforms.Order()),
        string.Join(',', Developers.Order()),
        string.Join(',', Publishers.Order()),
        Ordering,
        MetacriticMin,
        MetacriticMax);
}

/// <summary>One choice in a filter control. Value is what the client sends back; Name is what the user sees.</summary>
public sealed record FilterOption(string Value, string Name);

public sealed record GameFilterOptions(
    IReadOnlyList<FilterOption> Genres,
    IReadOnlyList<FilterOption> Platforms,
    IReadOnlyList<FilterOption> Orderings);

public sealed record CompanyOption(string Slug, string Name, int GamesCount);

public enum CompanyKind
{
    Developer,
    Publisher,
}

/// <summary>The only sort orders we accept. A leading "-" means descending.</summary>
public static class GameOrderings
{
    public static readonly IReadOnlyList<FilterOption> All =
    [
        new("-rating", "Rating (high to low)"),
        new("-metacritic", "Metacritic (high to low)"),
        new("-released", "Release date (newest)"),
        new("released", "Release date (oldest)"),
        new("-added", "Most popular"),
        new("name", "Name (A–Z)"),
    ];

    public static bool IsValid(string value) => All.Any(o => o.Value == value);
}
