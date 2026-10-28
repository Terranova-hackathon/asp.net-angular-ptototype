using GameHub.Shared.Catalog;

namespace GameHub.Games.Services;

/// <summary>Exposes game lookups to other modules through the Shared contract.</summary>
internal sealed class GameCatalog(IGamesService games) : IGameCatalog
{
    public async Task<GameSnapshot?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var game = await games.GetByIdAsync(id, cancellationToken);
            return game is null ? null : new GameSnapshot(game.Id, game.Name, game.BackgroundImage);
        }
        catch (GamesUnavailableException ex)
        {
            throw new GameCatalogUnavailableException(ex.Message, ex);
        }
    }
}
