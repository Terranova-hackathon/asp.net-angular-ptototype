namespace GameHub.Shared.Identity;

/// <summary>
/// Claims that Auth puts in the login token and other modules read,
/// so modules can share "who is this user" without referencing each other.
/// </summary>
public static class GameHubClaims
{
    /// <summary>The 64-bit SteamID of the user's linked Steam account (absent if not linked).</summary>
    public const string SteamId = "steam_id";
}
