using GameHub.Auth.Options;
using Microsoft.AspNetCore.Http;

namespace GameHub.Auth.Endpoints;

/// <summary>
/// The JWT lives in an HttpOnly cookie so JavaScript (and therefore XSS) can never read it.
/// Any future login method (e.g. GitHub OAuth) should finish by calling <see cref="Write"/>.
/// </summary>
internal static class AuthCookie
{
    public static void Write(HttpContext http, JwtOptions options, string token) =>
        http.Response.Cookies.Append(options.CookieName, token, CreateCookieOptions(http, options));

    public static void Delete(HttpContext http, JwtOptions options) =>
        http.Response.Cookies.Delete(options.CookieName, CreateCookieOptions(http, options));

    private static CookieOptions CreateCookieOptions(HttpContext http, JwtOptions options) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes),
        Path = "/",
    };
}
