namespace GameHub.Favorites.Domain;

/// <summary>One user's bookmark of one game. The pair (UserId, RawgId) is the key, so duplicates are impossible.</summary>
public class Favorite
{
    /// <summary>The GameHub user id. A plain column, not a foreign key, so this module owns its data alone.</summary>
    public string UserId { get; set; } = string.Empty;

    public int RawgId { get; set; }

    // Snapshot taken when saved, so the favorites list does not need one RAWG call per game.
    public string Name { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
