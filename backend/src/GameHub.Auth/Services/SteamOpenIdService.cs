using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace GameHub.Auth.Services;

/// <summary>
/// Steam sign-in uses OpenID 2.0. We send the user to Steam, and Steam sends them back with a signed
/// assertion. We never trust that assertion directly: we ask Steam to confirm its own signature.
/// </summary>
internal sealed partial class SteamOpenIdService(HttpClient http)
{
    public const string LoginProvider = "Steam";
    public const string PlaceholderEmailDomain = "steam.invalid";

    private const string Endpoint = "https://steamcommunity.com/openid/login";
    private const string OpenIdNamespace = "http://specs.openid.net/auth/2.0";
    private const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";

    // These must be covered by Steam's signature, otherwise an attacker could swap them freely.
    private static readonly string[] RequiredSignedFields = ["claimed_id", "return_to", "op_endpoint", "response_nonce"];

    [GeneratedRegex(@"^https://steamcommunity\.com/openid/id/(\d{17})$")]
    private static partial Regex ClaimedIdPattern();

    public static Uri BuildLoginUrl(string returnTo, string realm) =>
        new(QueryHelpers.AddQueryString(Endpoint, new Dictionary<string, string?>
        {
            ["openid.ns"] = OpenIdNamespace,
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnTo,
            ["openid.realm"] = realm,
            ["openid.identity"] = IdentifierSelect,
            ["openid.claimed_id"] = IdentifierSelect,
        }));

    /// <summary>Returns the verified SteamID, or null if the response is not a genuine Steam login for us.</summary>
    public async Task<string?> VerifyAsync(
        IQueryCollection query, string expectedReturnTo, CancellationToken cancellationToken = default)
    {
        if (query["openid.mode"] != "id_res" ||
            query["openid.op_endpoint"] != Endpoint ||
            query["openid.return_to"] != expectedReturnTo)
        {
            return null;
        }

        var signed = ((string?)query["openid.signed"] ?? string.Empty).Split(',');
        if (RequiredSignedFields.Any(field => !signed.Contains(field)))
        {
            return null;
        }

        var match = ClaimedIdPattern().Match(query["openid.claimed_id"].ToString());
        if (!match.Success)
        {
            return null;
        }

        var form = query
            .Where(pair => pair.Key.StartsWith("openid.", StringComparison.Ordinal))
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
        form["openid.mode"] = "check_authentication";

        using var response = await http.PostAsync(Endpoint, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var isValid = body.Split('\n').Any(line => line.Trim() == "is_valid:true");

        return isValid ? match.Groups[1].Value : null;
    }
}
