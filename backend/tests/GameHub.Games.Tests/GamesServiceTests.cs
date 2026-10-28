using System.Net;
using System.Text;
using GameHub.Games.Contracts;
using GameHub.Games.Options;
using GameHub.Games.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GameHub.Games.Tests;

public class GamesServiceTests
{
    private const string ListJson = """
        {"count": 42, "results": [
          {"id": 3498, "name": "GTA V", "released": "2013-09-17", "rating": 4.47, "metacritic": 92,
           "genres": [{"name": "Action"}],
           "platforms": [{"platform": {"name": "PC"}}, {"platform": {"name": "PlayStation 5"}}]}
        ]}
        """;

    [Fact]
    public async Task Search_MapsRawgResponseToOurContract()
    {
        var service = CreateRawgService(new StubHandler(HttpStatusCode.OK, ListJson));

        var result = await service.SearchAsync("gta", page: 2, pageSize: 10);

        Assert.Equal(42, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
        var game = Assert.Single(result.Items);
        Assert.Equal("GTA V", game.Name);
        Assert.Equal(["Action"], game.Genres);
        Assert.Equal(["PC", "PlayStation 5"], game.Platforms);
    }

    [Fact]
    public async Task Search_SendsQueryAndPagingToRawg()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ListJson);
        var service = CreateRawgService(handler);

        await service.SearchAsync("the witcher", page: 3, pageSize: 5);

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("search=the%20witcher", uri);
        Assert.Contains("page=3", uri);
        Assert.Contains("page_size=5", uri);
    }

    [Fact]
    public async Task MissingApiKey_ThrowsUnavailable()
    {
        var service = CreateRawgService(new StubHandler(HttpStatusCode.OK, ListJson), apiKey: "");

        await Assert.ThrowsAsync<GamesUnavailableException>(() => service.SearchAsync(null, 1, 10));
    }

    [Fact]
    public async Task UpstreamServerError_ThrowsUnavailable()
    {
        var service = CreateRawgService(new StubHandler(HttpStatusCode.InternalServerError, "{}"));

        await Assert.ThrowsAsync<GamesUnavailableException>(() => service.SearchAsync("x", 1, 10));
    }

    [Fact]
    public async Task GetById_ReturnsNullWhenRawgSaysNotFound()
    {
        var service = CreateRawgService(new StubHandler(HttpStatusCode.NotFound, "{}"));

        Assert.Null(await service.GetByIdAsync(999));
    }

    [Fact]
    public async Task Cache_CallsUpstreamOnceForRepeatedSearches()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ListJson);
        var cached = new CachingGamesService(
            CreateRawgService(handler), new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));

        await cached.SearchAsync("Zelda", 1, 10);
        await cached.SearchAsync(" zelda ", 1, 10);
        await cached.SearchAsync("zelda", 2, 10);

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Search_SendsFiltersToRawg()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ListJson);
        var service = CreateRawgService(handler);
        var filters = new GameSearchFilters(
            ["action", "indie"], [1, 2], ["nintendo"], ["sega"], "-rating", 70, 95);

        await service.SearchAsync(null, 1, 10, filters);

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("genres=action,indie", uri);
        Assert.Contains("parent_platforms=1,2", uri);
        Assert.Contains("developers=nintendo", uri);
        Assert.Contains("publishers=sega", uri);
        Assert.Contains("ordering=-rating", uri);
        Assert.Contains("metacritic=70,95", uri);
    }

    [Fact]
    public async Task Search_WithoutFilters_SendsNoFilterParameters()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ListJson);

        await CreateRawgService(handler).SearchAsync("zelda", 1, 10);

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.DoesNotContain("genres=", uri);
        Assert.DoesNotContain("ordering=", uri);
        Assert.DoesNotContain("metacritic=", uri);
    }

    [Fact]
    public async Task Cache_KeepsDifferentFiltersApart_ButIgnoresOrderOfValues()
    {
        var handler = new StubHandler(HttpStatusCode.OK, ListJson);
        var cached = new CachingGamesService(
            CreateRawgService(handler), new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));

        GameSearchFilters With(params string[] genres) => GameSearchFilters.None with { Genres = genres };

        await cached.SearchAsync("x", 1, 10, With("action", "indie"));
        await cached.SearchAsync("x", 1, 10, With("indie", "action"));
        await cached.SearchAsync("x", 1, 10, With("action"));

        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task FilterOptions_MapGenresPlatformsAndOrderings()
    {
        const string json = """{"results": [{"id": 4, "name": "Action", "slug": "action"}]}""";
        var options = await CreateRawgService(new StubHandler(HttpStatusCode.OK, json)).GetFilterOptionsAsync();

        Assert.Equal(new FilterOption("action", "Action"), Assert.Single(options.Genres));
        Assert.Equal(new FilterOption("4", "Action"), Assert.Single(options.Platforms));
        Assert.Contains(options.Orderings, o => o.Value == "-rating");
    }

    [Fact]
    public async Task SearchCompanies_UsesDevelopersOrPublishersEndpoint()
    {
        const string json = """{"results": [{"id": 1, "name": "Nintendo", "slug": "nintendo", "games_count": 387}]}""";
        var handler = new StubHandler(HttpStatusCode.OK, json);
        var service = CreateRawgService(handler);

        var result = await service.SearchCompaniesAsync(CompanyKind.Publisher, "nin tendo");

        Assert.Equal(new CompanyOption("nintendo", "Nintendo", 387), Assert.Single(result));
        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("/publishers?", uri);
        Assert.Contains("search=nin%20tendo", uri);
    }

    private static RawgGamesService CreateRawgService(StubHandler handler, string apiKey = "test-key")
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://rawg.test/api/") };
        return new RawgGamesService(http, Microsoft.Extensions.Options.Options.Create(new RawgOptions { ApiKey = apiKey }));
    }

    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
