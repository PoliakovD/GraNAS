using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

/// <summary>HTTP-клиент к sharing-service для разрешения share-токенов анонимного доступа.</summary>
public interface ISharingServiceClient
{
    /// <summary>
    /// Запрашивает информацию о share-ссылке по хешу токена
    /// (<c>GET /api/internal/shares/by-token-hash/{hash}</c>).
    /// </summary>
    /// <param name="tokenHash">SHA-256 хеш токена в нижнем регистре (hex). Вычисляется в <see cref="IAccessChecker"/>.</param>
    /// <returns>Информация о ссылке или <c>null</c>, если ссылка не найдена (HTTP 404).</returns>
    Task<ShareInfo?> GetShareByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
