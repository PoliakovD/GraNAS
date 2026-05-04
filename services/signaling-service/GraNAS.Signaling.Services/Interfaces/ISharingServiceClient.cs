using GraNAS.Signaling.Models.DTO;

namespace GraNAS.Signaling.Services.Interfaces;

public interface ISharingServiceClient
{
    Task<ShareInfo?> GetShareByTokenHashAsync(string tokenHash, CancellationToken ct = default);
}
