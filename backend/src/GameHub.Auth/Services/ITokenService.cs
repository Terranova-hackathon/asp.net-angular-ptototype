using GameHub.Auth.Domain;

namespace GameHub.Auth.Services;

public interface ITokenService
{
    string CreateToken(AppUser user, string? steamId = null);
}
