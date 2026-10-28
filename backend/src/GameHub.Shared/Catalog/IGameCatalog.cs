namespace GameHub.Shared.Catalog;

/// <summary>The few facts other modules may store about a game (a snapshot, not the whole game).</summary>
public sealed record GameSnapshot(int Id, string Name, string? ImageUrl);

/// <summary>
/// Implemented by the Games module so other modules can look games up
/// without referencing it or knowing that RAWG is behind it.
/// </summary>
public interface IGameCatalog
{
    /// <summary>Returns null if the game does not exist.</summary>
    /// <exception cref="GameCatalogUnavailableException">The game data provider could not be reached.</exception>
    Task<GameSnapshot?> FindAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class GameCatalogUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
