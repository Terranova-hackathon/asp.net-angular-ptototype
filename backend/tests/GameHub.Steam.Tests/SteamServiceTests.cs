using System.Net;
using System.Text;
using GameHub.Steam.Contracts;
using GameHub.Steam.Options;
using GameHub.Steam.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GameHub.Steam.Tests;

public class SteamServiceTests
{
    private const string SteamId = "76561198000000001";

    [Fact]
    public async Task OwnedGames_MapsAndSortsByPlaytime()
    {
        var service = Create(new StubHandler(HttpStatusCode.OK, """
            {"response": {"game_count": 2, "games": [
              {"appid": 10, "name": "Counter-Strike", "playtime_forever": 60, "img_icon_url": "abc"},
              {"appid": 440, "name": "Team Fortress 2", "playtime_forever": 600, "rtime_last_played": 1700000000}
            ]}}
            """));

        var section = await service.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamSectionStatus.Ok, section.Status);
        var library = section.Data!;
        Assert.Equal(2, library.Count);
        Assert.Equal(660, library.TotalPlaytimeMinutes);
        Assert.Equal(["Team Fortress 2", "Counter-Strike"], library.Games.Select(g => g.Name));
        Assert.NotNull(library.Games[0].LastPlayed);
        Assert.Contains("/apps/10/abc.jpg", library.Games[1].IconUrl);
    }

    [Fact]
    public async Task OwnedGames_EmptyResponse_MeansPrivate()
    {
        var service = Create(new StubHandler(HttpStatusCode.OK, """{"response": {}}"""));

        var section = await service.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamSectionStatus.Private, section.Status);
        Assert.Null(section.Data);
    }

    [Fact]
    public async Task OwnedGames_PublicButEmptyLibrary_IsNotPrivate()
    {
        var service = Create(new StubHandler(HttpStatusCode.OK, """{"response": {"game_count": 0}}"""));

        var section = await service.GetOwnedGamesAsync(SteamId);

        Assert.Equal(SteamSectionStatus.Ok, section.Status);
        Assert.Empty(section.Data!.Games);
    }

    [Fact]
    public async Task Friends_Unauthorized_MeansPrivate()
    {
        var service = Create(new StubHandler(HttpStatusCode.Unauthorized, "Unauthorized"));

        var section = await service.GetFriendsAsync(SteamId);

        Assert.Equal(SteamSectionStatus.Private, section.Status);
    }

    [Fact]
    public async Task Friends_AreEnrichedWithProfilesAndSortedByName()
    {
        const string friendList = """
            {"friendslist": {"friends": [
              {"steamid": "2", "friend_since": 1600000000}, {"steamid": "1", "friend_since": 1600000001}]}}
            """;
        const string summaries = """
            {"response": {"players": [
              {"steamid": "1", "personaname": "zed", "personastate": 1, "avatarfull": "https://a/1.jpg"},
              {"steamid": "2", "personaname": "Amy", "personastate": 0, "avatarfull": "https://a/2.jpg"}]}}
            """;
        var handler = new StubHandler(request =>
            Json(request.RequestUri!.AbsolutePath.Contains("GetFriendList") ? friendList : summaries));

        var section = await Create(handler).GetFriendsAsync(SteamId);

        Assert.Equal(["Amy", "zed"], section.Data!.Select(f => f.PersonaName));
        Assert.Equal("online", section.Data![1].Status);
    }

    [Fact]
    public async Task Achievements_PrivateProfile_MeansPrivate()
    {
        var service = Create(new StubHandler(
            HttpStatusCode.Forbidden, """{"playerstats": {"error": "Profile is not public", "success": false}}"""));

        var section = await service.GetAchievementsAsync(SteamId, 440);

        Assert.Equal(SteamSectionStatus.Private, section.Status);
    }

    [Fact]
    public async Task Achievements_GameWithoutStats_MeansNoData()
    {
        var service = Create(new StubHandler(
            HttpStatusCode.BadRequest, """{"playerstats": {"error": "Requested app has no stats", "success": false}}"""));

        var section = await service.GetAchievementsAsync(SteamId, 440);

        Assert.Equal(SteamSectionStatus.NoData, section.Status);
    }

    [Fact]
    public async Task Achievements_RejectedKey_IsUnavailableNotPrivate()
    {
        var service = Create(new StubHandler(HttpStatusCode.Forbidden, "<html>Forbidden</html>"));

        await Assert.ThrowsAsync<SteamUnavailableException>(() => service.GetAchievementsAsync(SteamId, 440));
    }

    [Fact]
    public async Task Achievements_CountsUnlockedAndListsUnlockedFirst()
    {
        var service = Create(new StubHandler(HttpStatusCode.OK, """
            {"playerstats": {"gameName": "TF2", "achievements": [
              {"apiname": "A", "achieved": 0, "unlocktime": 0, "name": "Locked", "description": "Not yet"},
              {"apiname": "B", "achieved": 1, "unlocktime": 1700000000, "name": "Done", "description": ""}
            ]}}
            """));

        var data = (await service.GetAchievementsAsync(SteamId, 440)).Data!;

        Assert.Equal("TF2", data.GameName);
        Assert.Equal(1, data.Unlocked);
        Assert.Equal(2, data.Total);
        Assert.Equal("Done", data.Achievements[0].Name);
        Assert.Null(data.Achievements[0].Description);
        Assert.NotNull(data.Achievements[0].UnlockedAt);
    }

    [Fact]
    public async Task MissingApiKey_ThrowsUnavailable()
    {
        var service = Create(new StubHandler(HttpStatusCode.OK, "{}"), apiKey: "");

        await Assert.ThrowsAsync<SteamUnavailableException>(() => service.GetOwnedGamesAsync(SteamId));
    }

    [Fact]
    public async Task ApiKeyAndSteamId_AreSentToSteam()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"response": {"game_count": 0}}""");

        await Create(handler).GetOwnedGamesAsync(SteamId);

        var uri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("key=test-key", uri);
        Assert.Contains($"steamid={SteamId}", uri);
    }

    [Fact]
    public async Task Caching_ReusesOkAnswers_ButNotPrivateOnes()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """{"response": {}}""");
        var caching = new CachingSteamService(
            Create(handler), new MemoryCache(new MemoryCacheOptions()), TimeSpan.FromMinutes(5));

        await caching.GetOwnedGamesAsync(SteamId);
        await caching.GetOwnedGamesAsync(SteamId);
        Assert.Equal(2, handler.CallCount);

        handler.Respond(HttpStatusCode.OK, """{"response": {"game_count": 0}}""");
        await caching.GetOwnedGamesAsync(SteamId);
        await caching.GetOwnedGamesAsync(SteamId);
        Assert.Equal(3, handler.CallCount);
    }

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static SteamWebApiService Create(StubHandler handler, string apiKey = "test-key") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.steampowered.com/") },
            Microsoft.Extensions.Options.Options.Create(new SteamOptions { ApiKey = apiKey }));

    private sealed class StubHandler : HttpMessageHandler
    {
        private Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(HttpStatusCode status, string body) => _respond = _ => Build(status, body);

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public HttpRequestMessage? LastRequest { get; private set; }

        public int CallCount { get; private set; }

        public void Respond(HttpStatusCode status, string body) => _respond = _ => Build(status, body);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            CallCount++;
            return Task.FromResult(_respond(request));
        }

        private static HttpResponseMessage Build(HttpStatusCode status, string body) =>
            new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
