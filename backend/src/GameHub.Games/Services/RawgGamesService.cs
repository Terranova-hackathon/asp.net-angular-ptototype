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
        string? search, int page, int pageSize, GameSearchFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"games?key={Uri.EscapeDataString(RequireKey())}&page={page}&page_size={pageSize}";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"&search={Uri.EscapeDataString(search.Trim())}";
        }

        url += FilterQuery(filters);

        var response = await GetAsync<Rawg.RawgListResponse>(url, cancellationToken)
                       ?? new Rawg.RawgListResponse();

        return new PagedResult<GameSummary>(
            response.Results.Select(ToSummary).ToList(), page, pageSize, response.Count);
    }

    public async Task<GameFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken = default)
    {
        var key = Uri.EscapeDataString(RequireKey());
        var genres = await GetAsync<Rawg.RawgOptionList>($"genres?key={key}", cancellationToken);
        var platforms = await GetAsync<Rawg.RawgOptionList>($"platforms/lists/parents?key={key}", cancellationToken);

        return new GameFilterOptions(
            (genres?.Results ?? []).Select(g => new FilterOption(g.Slug, g.Name)).ToList(),
            (platforms?.Results ?? []).Select(p => new FilterOption(p.Id.ToString(), p.Name)).ToList(),
            GameOrderings.All);
    }

    public async Task<IReadOnlyList<CompanyOption>> SearchCompaniesAsync(
        CompanyKind kind, string search, CancellationToken cancellationToken = default)
    {
        var resource = kind == CompanyKind.Developer ? "developers" : "publishers";
        var response = await GetAsync<Rawg.RawgOptionList>(
            $"{resource}?key={Uri.EscapeDataString(RequireKey())}&page_size=10&search={Uri.EscapeDataString(search.Trim())}",
            cancellationToken);

        return (response?.Results ?? []).Select(c => new CompanyOption(c.Slug, c.Name, c.GamesCount)).ToList();
    }

    /// <summary>Builds the filter part of the RAWG query. Values were validated earlier and are escaped here as well.</summary>
    private static string FilterQuery(GameSearchFilters? f)
    {
        if (f is null)
        {
            return string.Empty;
        }

        var query = new System.Text.StringBuilder();

        void Add(string name, IEnumerable<string> values)
        {
            var joined = string.Join(',', values.Select(Uri.EscapeDataString));
            if (joined.Length > 0)
            {
                query.Append('&').Append(name).Append('=').Append(joined);
            }
        }

        Add("genres", f.Genres);
        Add("parent_platforms", f.Platforms.Select(p => p.ToString()));
        Add("developers", f.Developers);
        Add("publishers", f.Publishers);

        if (f.Ordering is not null)
        {
            query.Append("&ordering=").Append(Uri.EscapeDataString(f.Ordering));
        }

        if (f.MetacriticMin is not null || f.MetacriticMax is not null)
        {
            query.Append("&metacritic=").Append(f.MetacriticMin ?? 0).Append(',').Append(f.MetacriticMax ?? 100);
        }

        return query.ToString();
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
