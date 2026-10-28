using System.ComponentModel.DataAnnotations;

namespace GameHub.Auth.Contracts;

public sealed record RegisterRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required, MinLength(8)] string Password,
    string? DisplayName);

public sealed record LoginRequest(
    [property: Required, EmailAddress] string Email,
    [property: Required] string Password);

public sealed record UserResponse(string Id, string Email, string? DisplayName, string? SteamId);
