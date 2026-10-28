using GameHub.Auth.Contracts;
using GameHub.Auth.Domain;
using GameHub.Auth.Options;
using GameHub.Auth.Services;
using GameHub.Shared.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace GameHub.Auth.Endpoints;

internal static class AuthEndpoints
{
    private const string SteamStateCookie = "gamehub_steam_state";
    private const string SteamPath = "/api/auth/steam";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapGet("/steam", SteamLogin);
        group.MapGet("/steam/callback", SteamCallback);
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        UserManager<AppUser> users,
        ITokenService tokens,
        IOptions<JwtOptions> jwt,
        HttpContext http)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.DisplayName,
        };

        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(
                result.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()));
        }

        AuthCookie.Write(http, jwt.Value, tokens.CreateToken(user));
        return Results.Created("/api/auth/me", ToResponse(user, steamId: null));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<AppUser> users,
        ITokenService tokens,
        IOptions<JwtOptions> jwt,
        HttpContext http)
    {
        if (!RequestValidator.TryValidate(request, out var errors))
        {
            return Results.ValidationProblem(errors);
        }

        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || await users.IsLockedOutAsync(user))
        {
            return InvalidCredentials();
        }

        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            return InvalidCredentials();
        }

        await users.ResetAccessFailedCountAsync(user);
        var steamId = await FindSteamIdAsync(users, user);
        AuthCookie.Write(http, jwt.Value, tokens.CreateToken(user, steamId));
        return Results.Ok(ToResponse(user, steamId));
    }

    private static IResult Logout(IOptions<JwtOptions> jwt, HttpContext http)
    {
        AuthCookie.Delete(http, jwt.Value);
        return Results.NoContent();
    }

    private static async Task<IResult> Me(ClaimsPrincipal principal, UserManager<AppUser> users)
    {
        var id = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var user = id is null ? null : await users.FindByIdAsync(id);
        return user is null
            ? Results.Unauthorized()
            : Results.Ok(ToResponse(user, await FindSteamIdAsync(users, user)));
    }

    private static IResult SteamLogin(HttpContext http)
    {
        // The state ties the Steam response to the browser that started the login (login CSRF protection).
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        http.Response.Cookies.Append(SteamStateCookie, state, new CookieOptions
        {
            HttpOnly = true,
            Secure = http.Request.IsHttps,
            // Lax so the cookie still arrives when Steam redirects the browser back to us.
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = SteamPath,
        });

        var realm = $"{http.Request.Scheme}://{http.Request.Host}";
        return Results.Redirect(SteamOpenIdService.BuildLoginUrl(CallbackUrl(realm, state), realm).AbsoluteUri);
    }

    private static async Task<IResult> SteamCallback(
        HttpContext http,
        SteamOpenIdService steam,
        UserManager<AppUser> users,
        ITokenService tokens,
        IOptions<JwtOptions> jwt,
        CancellationToken cancellationToken)
    {
        // A logged-in user who comes through here is linking Steam to their account;
        // an anonymous visitor is signing in (or signing up) with Steam.
        var session = await http.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        var currentUserId = session.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var failure = currentUserId is null ? "/login?steam=failed" : "/profile?steam=failed";

        var state = http.Request.Cookies[SteamStateCookie];
        http.Response.Cookies.Delete(SteamStateCookie, new CookieOptions { Path = SteamPath });
        if (string.IsNullOrEmpty(state))
        {
            return Results.Redirect(failure);
        }

        string? steamId;
        try
        {
            var realm = $"{http.Request.Scheme}://{http.Request.Host}";
            steamId = await steam.VerifyAsync(http.Request.Query, CallbackUrl(realm, state), cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return Results.Redirect(failure);
        }

        if (steamId is null)
        {
            return Results.Redirect(failure);
        }

        var owner = await users.FindByLoginAsync(SteamOpenIdService.LoginProvider, steamId);
        AppUser? user;

        if (currentUserId is not null)
        {
            user = await users.FindByIdAsync(currentUserId);
            if (user is null)
            {
                return Results.Redirect(failure);
            }

            var linked = await FindSteamIdAsync(users, user);
            if ((owner is not null && owner.Id != user.Id) || (linked is not null && linked != steamId))
            {
                return Results.Redirect("/profile?steam=conflict");
            }

            if (owner is null && !await TryLinkAsync(users, user, steamId))
            {
                return Results.Redirect(failure);
            }
        }
        else
        {
            user = owner ?? await CreateSteamUserAsync(users, steamId);
            if (user is null || await users.IsLockedOutAsync(user))
            {
                return Results.Redirect(failure);
            }
        }

        AuthCookie.Write(http, jwt.Value, tokens.CreateToken(user, steamId));
        return Results.Redirect("/profile");
    }

    private static string CallbackUrl(string realm, string state) =>
        $"{realm}{SteamPath}/callback?state={state}";

    private static async Task<bool> TryLinkAsync(UserManager<AppUser> users, AppUser user, string steamId) =>
        (await users.AddLoginAsync(user, new UserLoginInfo(SteamOpenIdService.LoginProvider, steamId, "Steam"))).Succeeded;

    private static async Task<AppUser?> CreateSteamUserAsync(UserManager<AppUser> users, string steamId)
    {
        // Identity requires a unique email; Steam does not share one, so use an address that can never be real.
        var user = new AppUser
        {
            UserName = $"steam_{steamId}",
            Email = $"{steamId}@{SteamOpenIdService.PlaceholderEmailDomain}",
            DisplayName = "Steam player",
        };

        if (!(await users.CreateAsync(user)).Succeeded)
        {
            return null;
        }

        if (await TryLinkAsync(users, user, steamId))
        {
            return user;
        }

        await users.DeleteAsync(user);
        return null;
    }

    private static async Task<string?> FindSteamIdAsync(UserManager<AppUser> users, AppUser user) =>
        (await users.GetLoginsAsync(user))
        .FirstOrDefault(login => login.LoginProvider == SteamOpenIdService.LoginProvider)?.ProviderKey;

    // Same message for unknown user and wrong password so attackers cannot probe for accounts.
    private static IResult InvalidCredentials() =>
        Results.Problem(
            title: "Invalid email or password.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static UserResponse ToResponse(AppUser user, string? steamId)
    {
        var email = user.Email ?? string.Empty;
        return new(
            user.Id,
            email.EndsWith('@' + SteamOpenIdService.PlaceholderEmailDomain, StringComparison.Ordinal) ? string.Empty : email,
            user.DisplayName,
            steamId);
    }
}
