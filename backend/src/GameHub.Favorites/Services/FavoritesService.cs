using GameHub.Favorites.Contracts;
using GameHub.Favorites.Data;
using GameHub.Favorites.Domain;
using GameHub.Shared.Catalog;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Favorites.Services;

public enum AddFavoriteResult
{
    Added,
    AlreadyFavorite,
    GameNotFound,
    LimitReached,
}

internal sealed class FavoritesService(FavoritesDbContext db, IGameCatalog catalog)
{
    public const int MaxFavorites = 1000;

    public async Task<IReadOnlyList<FavoriteResponse>> ListAsync(string userId, CancellationToken cancellationToken = default) =>
        await db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.CreatedAtUtc)
            .Take(MaxFavorites)
            .Select(f => new FavoriteResponse(f.RawgId, f.Name, f.ImageUrl, f.CreatedAtUtc))
            .ToListAsync(cancellationToken);

    /// <summary>Just the game ids, so list pages can draw a filled or empty heart cheaply.</summary>
    public async Task<IReadOnlyList<int>> IdsAsync(string userId, CancellationToken cancellationToken = default) =>
        await db.Favorites
            .AsNoTracking()
            .Where(f => f.UserId == userId)
            .Select(f => f.RawgId)
            .ToListAsync(cancellationToken);

    /// <summary>Idempotent: favoriting something that is already a favorite succeeds without changes.</summary>
    public async Task<AddFavoriteResult> AddAsync(string userId, int rawgId, CancellationToken cancellationToken = default)
    {
        if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.RawgId == rawgId, cancellationToken))
        {
            return AddFavoriteResult.AlreadyFavorite;
        }

        if (await db.Favorites.CountAsync(f => f.UserId == userId, cancellationToken) >= MaxFavorites)
        {
            return AddFavoriteResult.LimitReached;
        }

        // The name and image come from our own catalog, never from the request, so clients cannot store arbitrary values.
        var game = await catalog.FindAsync(rawgId, cancellationToken);
        if (game is null)
        {
            return AddFavoriteResult.GameNotFound;
        }

        db.Favorites.Add(new Favorite
        {
            UserId = userId,
            RawgId = game.Id,
            Name = Truncate(game.Name, 300),
            ImageUrl = game.ImageUrl is { Length: <= 500 } ? game.ImageUrl : null,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Two requests raced and the other one won; the primary key kept it to a single row.
            db.ChangeTracker.Clear();
            if (await db.Favorites.AnyAsync(f => f.UserId == userId && f.RawgId == rawgId, cancellationToken))
            {
                return AddFavoriteResult.AlreadyFavorite;
            }

            throw;
        }

        return AddFavoriteResult.Added;
    }

    /// <summary>Idempotent: removing something that is not a favorite is not an error.</summary>
    public Task<int> RemoveAsync(string userId, int rawgId, CancellationToken cancellationToken = default) =>
        db.Favorites
            .Where(f => f.UserId == userId && f.RawgId == rawgId)
            .ExecuteDeleteAsync(cancellationToken);

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
