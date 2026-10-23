using System.Net;
using System.Text;
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
