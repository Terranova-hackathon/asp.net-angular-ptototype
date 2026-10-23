using System.Text.Json.Serialization;

namespace GameHub.Games.Rawg;

// Shapes of the RAWG JSON responses. Internal so the rest of the app never depends on RAWG's format.
internal sealed class RawgListResponse
{
    [JsonPropertyName("count")] public int Count { get; set; }
    [JsonPropertyName("results")] public List<RawgGame> Results { get; set; } = [];
}

internal sealed class RawgGame
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("released")] public string? Released { get; set; }
    [JsonPropertyName("background_image")] public string? BackgroundImage { get; set; }
    [JsonPropertyName("rating")] public double Rating { get; set; }
    [JsonPropertyName("metacritic")] public int? Metacritic { get; set; }
    [JsonPropertyName("description_raw")] public string? DescriptionRaw { get; set; }
    [JsonPropertyName("website")] public string? Website { get; set; }
    [JsonPropertyName("genres")] public List<RawgNamed>? Genres { get; set; }
    [JsonPropertyName("developers")] public List<RawgNamed>? Developers { get; set; }
    [JsonPropertyName("publishers")] public List<RawgNamed>? Publishers { get; set; }
    [JsonPropertyName("platforms")] public List<RawgPlatformEntry>? Platforms { get; set; }
}

internal sealed class RawgNamed
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
}

internal sealed class RawgPlatformEntry
{
    [JsonPropertyName("platform")] public RawgNamed? Platform { get; set; }
}
