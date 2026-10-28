using System.Net;
using System.Text;
using GameHub.Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace GameHub.Auth.Tests;

public class SteamOpenIdServiceTests
{
    private const string ReturnTo = "https://gamehub.test/api/auth/steam/callback?state=abc";
    private const string SteamId = "76561198000000001";

    [Fact]
    public async Task ValidAssertion_ReturnsSteamId_AndAsksSteamToConfirmIt()
    {
        var handler = new StubHandler("ns:http://specs.openid.net/auth/2.0\nis_valid:true\n");

        var result = await Create(handler).VerifyAsync(Query(), ReturnTo);

        Assert.Equal(SteamId, result);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("openid.mode=check_authentication", handler.LastBody);
    }

    [Fact]
    public async Task SteamSaysInvalid_ReturnsNull()
    {
        var result = await Create(new StubHandler("is_valid:false\n")).VerifyAsync(Query(), ReturnTo);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReturnToThatIsNotOurs_IsRejectedWithoutCallingSteam()
    {
        var handler = new StubHandler("is_valid:true\n");

        var result = await Create(handler).VerifyAsync(Query(), "https://gamehub.test/api/auth/steam/callback?state=other");

        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task ClaimedIdNotCoveredBySignature_IsRejected()
    {
        var handler = new StubHandler("is_valid:true\n");

        var result = await Create(handler).VerifyAsync(
            Query(("openid.signed", "signed,op_endpoint,return_to,response_nonce")), ReturnTo);

        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }

    [Theory]
    [InlineData("https://evil.example/openid/id/76561198000000001")]
    [InlineData("https://steamcommunity.com/openid/id/123")]
    [InlineData("https://steamcommunity.com/openid/id/76561198000000001/extra")]
    public async Task ClaimedIdThatIsNotASteamId_IsRejected(string claimedId)
    {
        var result = await Create(new StubHandler("is_valid:true\n"))
            .VerifyAsync(Query(("openid.claimed_id", claimedId)), ReturnTo);

        Assert.Null(result);
    }

    [Fact]
    public async Task WrongOpEndpoint_IsRejected()
    {
        var result = await Create(new StubHandler("is_valid:true\n"))
            .VerifyAsync(Query(("openid.op_endpoint", "https://evil.example/openid/login")), ReturnTo);

        Assert.Null(result);
    }

    [Fact]
    public void LoginUrl_PointsAtSteamAndCarriesReturnUrl()
    {
        var url = SteamOpenIdService.BuildLoginUrl(ReturnTo, "https://gamehub.test").AbsoluteUri;

        Assert.StartsWith("https://steamcommunity.com/openid/login?", url);
        Assert.Contains("openid.mode=checkid_setup", url);
        Assert.Contains(Uri.EscapeDataString(ReturnTo), url);
    }

    private static QueryCollection Query(params (string Key, string Value)[] overrides)
    {
        var values = new Dictionary<string, StringValues>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "id_res",
            ["openid.op_endpoint"] = "https://steamcommunity.com/openid/login",
            ["openid.claimed_id"] = $"https://steamcommunity.com/openid/id/{SteamId}",
            ["openid.identity"] = $"https://steamcommunity.com/openid/id/{SteamId}",
            ["openid.return_to"] = ReturnTo,
            ["openid.response_nonce"] = "2026-01-01T00:00:00Zabc",
            ["openid.assoc_handle"] = "1234567890",
            ["openid.signed"] = "signed,op_endpoint,claimed_id,identity,return_to,response_nonce,assoc_handle",
            ["openid.sig"] = "c2lnbmF0dXJl",
            ["state"] = "abc",
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        return new QueryCollection(values);
    }

    private static SteamOpenIdService Create(StubHandler handler) => new(new HttpClient(handler));

    private sealed class StubHandler(string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "text/plain") };
        }
    }
}
