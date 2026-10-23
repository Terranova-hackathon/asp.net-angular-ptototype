using GameHub.Games.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameHub.Games.Endpoints;

internal static class GamesEndpoints
{
    private const int MaxPageSize = 40;

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/games")
            .WithTags("Games")
            .RequireAuthorization();

        group.MapGet("/", Search);
        group.MapGet("/{id:int}", GetById);
    }

    private static async Task<IResult> Search(
        IGamesService games,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = 20)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        try
        {
            return Results.Ok(await games.SearchAsync(search, page, pageSize, cancellationToken));
        }
        catch (GamesUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static async Task<IResult> GetById(
        int id, IGamesService games, CancellationToken cancellationToken)
    {
        try
        {
            var game = await games.GetByIdAsync(id, cancellationToken);
            return game is null ? Results.NotFound() : Results.Ok(game);
        }
        catch (GamesUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static IResult Unavailable(GamesUnavailableException ex) =>
        Results.Problem(
            title: "Game data is currently unavailable.",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
}
