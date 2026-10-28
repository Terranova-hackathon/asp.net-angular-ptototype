using System.Text.RegularExpressions;
using GameHub.Games.Contracts;
using GameHub.Games.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace GameHub.Games.Endpoints;

internal static partial class GamesEndpoints
{
    private const int MaxPageSize = 40;
    private const int MaxValuesPerFilter = 10;

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,79}$")]
    private static partial Regex SlugPattern();

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/games")
            .WithTags("Games")
            .RequireAuthorization();

        group.MapGet("/", Search);
        group.MapGet("/filters", GetFilters);
        group.MapGet("/companies", SearchCompanies);
        group.MapGet("/{id:int}", GetById);
    }

    private static async Task<IResult> Search(
        IGamesService games,
        CancellationToken cancellationToken,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        string? genres = null,
        string? platforms = null,
        string? developers = null,
        string? publishers = null,
        string? ordering = null,
        int? metacriticMin = null,
        int? metacriticMax = null)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var errors = new Dictionary<string, string[]>();
        var filters = new GameSearchFilters(
            ParseSlugs(nameof(genres), genres, errors),
            ParsePlatformIds(platforms, errors),
            ParseSlugs(nameof(developers), developers, errors),
            ParseSlugs(nameof(publishers), publishers, errors),
            ParseOrdering(ordering, errors),
            ParseScore(nameof(metacriticMin), metacriticMin, errors),
            ParseScore(nameof(metacriticMax), metacriticMax, errors));

        if (filters.MetacriticMin > filters.MetacriticMax)
        {
            errors["metacriticMin"] = ["metacriticMin cannot be greater than metacriticMax."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            return Results.Ok(await games.SearchAsync(search, page, pageSize, filters, cancellationToken));
        }
        catch (GamesUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static async Task<IResult> GetFilters(IGamesService games, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await games.GetFilterOptionsAsync(cancellationToken));
        }
        catch (GamesUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static async Task<IResult> SearchCompanies(
        IGamesService games, CancellationToken cancellationToken, string? kind = null, string? search = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<CompanyKind>(kind, ignoreCase: true, out var companyKind) || !Enum.IsDefined(companyKind))
        {
            errors["kind"] = ["kind must be 'developer' or 'publisher'."];
        }

        search = search?.Trim();
        if (search is null || search.Length < 2 || search.Length > 60)
        {
            errors["search"] = ["search must be between 2 and 60 characters."];
        }

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        try
        {
            return Results.Ok(await games.SearchCompaniesAsync(companyKind, search!, cancellationToken));
        }
        catch (GamesUnavailableException ex)
        {
            return Unavailable(ex);
        }
    }

    private static string[] Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(v => v.ToLowerInvariant()).Distinct().ToArray();

    private static List<string> ParseSlugs(string name, string? csv, Dictionary<string, string[]> errors)
    {
        var values = Split(csv);
        if (values.Length > MaxValuesPerFilter)
        {
            errors[name] = [$"{name} accepts at most {MaxValuesPerFilter} values."];
        }
        else if (values.Any(v => !SlugPattern().IsMatch(v)))
        {
            errors[name] = [$"{name} must be a comma-separated list of slugs (letters, digits, '-' or '_')."];
        }

        return [.. values];
    }

    private static List<int> ParsePlatformIds(string? csv, Dictionary<string, string[]> errors)
    {
        var values = Split(csv);
        var ids = new List<int>();
        foreach (var v in values)
        {
            if (!int.TryParse(v, out var id) || id <= 0)
            {
                errors["platforms"] = ["platforms must be a comma-separated list of platform ids."];
                return [];
            }

            ids.Add(id);
        }

        if (ids.Count > MaxValuesPerFilter)
        {
            errors["platforms"] = [$"platforms accepts at most {MaxValuesPerFilter} values."];
        }

        return ids;
    }

    private static string? ParseOrdering(string? ordering, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(ordering))
        {
            return null;
        }

        if (!GameOrderings.IsValid(ordering))
        {
            errors["ordering"] = ["ordering is not one of the supported sort orders."];
            return null;
        }

        return ordering;
    }

    private static int? ParseScore(string name, int? value, Dictionary<string, string[]> errors)
    {
        if (value is < 0 or > 100)
        {
            errors[name] = [$"{name} must be between 0 and 100."];
            return null;
        }

        return value;
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
