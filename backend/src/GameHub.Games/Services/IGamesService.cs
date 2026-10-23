using GameHub.Games.Contracts;
using GameHub.Shared.Paging;

namespace GameHub.Games.Services;

public interface IGamesService
{
    Task<PagedResult<GameSummary>> SearchAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<GameDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Raised when the upstream game API is misconfigured or unreachable.</summary>
public sealed class GamesUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
