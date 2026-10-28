using System.Security.Claims;
using GameHub.Shared.Identity;
using GameHub.Steam.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameHub.Steam.Endpoints;

internal static class SteamEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/steam")
            .WithTags("Steam")
            .RequireAuthorization();

        // The SteamID always comes from the logged-in user's token, never from the URL,
        // so nobody can ask for another user's data through this API.
        group.MapGet("/profile", (ClaimsPrincipal user, ISteamService steam, CancellationToken ct) =>
            Run(user, async steamId =>
            {
                var profile = await steam.GetProfileAsync(steamId, ct);
                return profile is null ? Results.NotFound() : Results.Ok(profile);
            }));

        group.MapGet("/games", (ClaimsPrincipal user, ISteamService steam, CancellationToken ct) =>
            Run(user, async steamId => Results.Ok(await steam.GetOwnedGamesAsync(steamId, ct))));

        group.MapGet("/games/recent", (ClaimsPrincipal user, ISteamService steam, CancellationToken ct) =>
            Run(user, async steamId => Results.Ok(await steam.GetRecentGamesAsync(steamId, ct))));

        group.MapGet("/friends", (ClaimsPrincipal user, ISteamService steam, CancellationToken ct) =>
            Run(user, async steamId => Results.Ok(await steam.GetFriendsAsync(steamId, ct))));

        group.MapGet("/games/{appId:int}/achievements",
            (int appId, ClaimsPrincipal user, ISteamService steam, CancellationToken ct) =>
                Run(user, async steamId => Results.Ok(await steam.GetAchievementsAsync(steamId, appId, ct))));
    }

    private static async Task<IResult> Run(ClaimsPrincipal user, Func<string, Task<IResult>> action)
    {
        var steamId = user.FindFirstValue(GameHubClaims.SteamId);
        if (steamId is null)
        {
            return Results.Problem(
                title: "No Steam account is linked.",
                detail: "Sign in with Steam to link your account.",
                statusCode: StatusCodes.Status409Conflict);
        }

        try
        {
            return await action(steamId);
        }
        catch (SteamUnavailableException ex)
        {
            return Results.Problem(
                title: "Steam data is currently unavailable.",
                detail: ex.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
