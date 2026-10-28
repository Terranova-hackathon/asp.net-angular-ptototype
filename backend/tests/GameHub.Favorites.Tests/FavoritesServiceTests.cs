using GameHub.Favorites.Data;
using GameHub.Favorites.Services;
using GameHub.Shared.Catalog;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GameHub.Favorites.Tests;

public sealed class FavoritesServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FavoritesDbContext _db;
    private readonly FakeCatalog _catalog = new();
    private readonly FavoritesService _service;

    public FavoritesServiceTests()
    {
        _connection.Open();
        _db = new FavoritesDbContext(new DbContextOptionsBuilder<FavoritesDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        _service = new FavoritesService(_db, _catalog);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Add_StoresSnapshotFromCatalog()
    {
        Assert.Equal(AddFavoriteResult.Added, await _service.AddAsync("u1", 3498));

        var list = await _service.ListAsync("u1");
        var fav = Assert.Single(list);
        Assert.Equal(3498, fav.Id);
        Assert.Equal("Game 3498", fav.Name);
    }

    [Fact]
    public async Task Add_IsIdempotent()
    {
        await _service.AddAsync("u1", 1);
        Assert.Equal(AddFavoriteResult.AlreadyFavorite, await _service.AddAsync("u1", 1));
        Assert.Single(await _service.IdsAsync("u1"));
    }

    [Fact]
    public async Task Add_UnknownGame_ReturnsNotFound()
    {
        Assert.Equal(AddFavoriteResult.GameNotFound, await _service.AddAsync("u1", 999999));
        Assert.Empty(await _service.IdsAsync("u1"));
    }

    [Fact]
    public async Task Add_CatalogDown_Throws()
    {
        _catalog.Down = true;
        await Assert.ThrowsAsync<GameCatalogUnavailableException>(() => _service.AddAsync("u1", 1));
    }

    [Fact]
    public async Task Favorites_AreSeparatePerUser()
    {
        await _service.AddAsync("u1", 1);
        await _service.AddAsync("u2", 2);

        Assert.Equal([1], await _service.IdsAsync("u1"));
        Assert.Equal([2], await _service.IdsAsync("u2"));
    }

    [Fact]
    public async Task Remove_DeletesOnlyThatUsersFavorite_AndIsIdempotent()
    {
        await _service.AddAsync("u1", 1);
        await _service.AddAsync("u2", 1);

        Assert.Equal(1, await _service.RemoveAsync("u1", 1));
        Assert.Equal(0, await _service.RemoveAsync("u1", 1));
        Assert.Empty(await _service.IdsAsync("u1"));
        Assert.Equal([1], await _service.IdsAsync("u2"));
    }

    [Fact]
    public async Task Add_StopsAtLimit()
    {
        for (var i = 1; i <= FavoritesService.MaxFavorites; i++)
        {
            _db.Favorites.Add(new Domain.Favorite { UserId = "u1", RawgId = i, Name = "x" });
        }
        await _db.SaveChangesAsync();

        Assert.Equal(AddFavoriteResult.LimitReached, await _service.AddAsync("u1", 5000));
    }

    [Fact]
    public async Task List_NewestFirst()
    {
        await _service.AddAsync("u1", 1);
        await Task.Delay(20);
        await _service.AddAsync("u1", 2);

        Assert.Equal([2, 1], (await _service.ListAsync("u1")).Select(f => f.Id));
    }

    private sealed class FakeCatalog : IGameCatalog
    {
        public bool Down { get; set; }

        public Task<GameSnapshot?> FindAsync(int id, CancellationToken cancellationToken = default)
        {
            if (Down) throw new GameCatalogUnavailableException("down");
            return Task.FromResult<GameSnapshot?>(id == 999999 ? null : new GameSnapshot(id, $"Game {id}", null));
        }
    }
}
