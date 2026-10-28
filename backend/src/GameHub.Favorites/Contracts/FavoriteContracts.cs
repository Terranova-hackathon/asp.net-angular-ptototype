namespace GameHub.Favorites.Contracts;

/// <summary>Id is the RAWG game id, so the frontend can link straight to /games/{id}.</summary>
public sealed record FavoriteResponse(int Id, string Name, string? ImageUrl, DateTime AddedAtUtc);
