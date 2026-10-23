using System.Net;
using System.Net.Http.Json;
using GameHub.Games.Contracts;
using GameHub.Games.Options;
using GameHub.Shared.Paging;
using Microsoft.Extensions.Options;

namespace GameHub.Games.Services;

/// <summary>Talks to RAWG and maps its responses to our own contracts.</summary>
internal sealed class RawgGamesService(HttpClient http, IOptions<RawgOptions> options) : IGamesService
{
    private readonly string _apiKey = options.Value.ApiKey;

    public async Task<PagedResult<GameSummary>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var url = $"games?key={Uri.EscapeDataString(RequireKey())}&page={page}&page_size={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        var response = await GetAsync<Rawg.RawgListResponse>(url, cancellationToken)
                       ?? new Rawg.RawgListResponse();

        return new PagedResult<GameSummary>(
            response.Results.Select(ToSummary).ToList(), page, pageSize, response.Count);
    }

    public async Task<GameDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var game = await GetAsync<Rawg.RawgGame>(
            $"games/{id}?key={Uri.EscapeDataString(RequireKey())}", cancellationToken);

        return game is null ? null : ToDetails(game);
    }

    private string RequireKey() =>
        string.IsNullOrWhiteSpace(_apiKey)
            ? throw new GamesUnavailableException("The RAWG API key is not configured (Rawg__ApiKey).")
            : _apiKey;

    private async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken) where T : class
    {
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new GamesUnavailableException("The game data provider is unavailable.", ex);
        }
    }

    private static IReadOnlyList<string> Names(List<Rawg.RawgNamed>? items) =>
        items?.Select(i => i.Name).ToList() ?? [];

    private static IReadOnlyList<string> PlatformNames(List<Rawg.RawgPlatformEntry>? items) =>
        items?.Where(p => p.Platform is not null).Select(p => p.Platform!.Name).ToList() ?? [];

    private static GameSummary ToSummary(Rawg.RawgGame g) => new(
        g.Id, g.Name, g.Released, g.BackgroundImage, g.Rating, g.Metacritic,
        Names(g.Genres), PlatformNames(g.Platforms));

    private static GameDetails ToDetails(Rawg.RawgGame g) => new(
        g.Id, g.Name, g.Released, g.BackgroundImage, g.Rating, g.Metacritic,
        g.DescriptionRaw, g.Website, Names(g.Genres), PlatformNames(g.Platforms),
        Names(g.Developers), Names(g.Publishers));
}
