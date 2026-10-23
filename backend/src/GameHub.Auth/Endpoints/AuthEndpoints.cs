using GameHub.Auth.Contracts;
using GameHub.Auth.Domain;
using GameHub.Auth.Options;
using GameHub.Auth.Services;
using GameHub.Shared.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GameHub.Auth.Endpoints;

internal static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/logout", Logout);
        group.MapGet("/me", Me).RequireAuthorization();
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
        return Results.Created("/api/auth/me", ToResponse(user));
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
        AuthCookie.Write(http, jwt.Value, tokens.CreateToken(user));
        return Results.Ok(ToResponse(user));
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
        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
    }

    // Same message for unknown user and wrong password so attackers cannot probe for accounts.
    private static IResult InvalidCredentials() =>
        Results.Problem(
            title: "Invalid email or password.",
            statusCode: StatusCodes.Status401Unauthorized);

    private static UserResponse ToResponse(AppUser user) =>
        new(user.Id, user.Email ?? string.Empty, user.DisplayName);
}
