using System.ComponentModel.DataAnnotations;

namespace GameHub.Auth.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required, MinLength(32)]
    public string Key { get; init; } = string.Empty;

    [Required]
    public string Issuer { get; init; } = "gamehub";

    [Required]
    public string Audience { get; init; } = "gamehub";

    [Range(1, 60 * 24 * 30)]
    public int ExpiryMinutes { get; init; } = 60;

    [Required]
    public string CookieName { get; init; } = "gamehub_auth";
}
