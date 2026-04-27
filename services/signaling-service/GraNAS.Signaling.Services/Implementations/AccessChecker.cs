using System.Security.Cryptography;
using System.Text;
using GraNAS.Signaling.Services.Interfaces;

namespace GraNAS.Signaling.Services.Implementations;

public class AccessChecker : IAccessChecker
{
    private readonly IMetadataServiceClient _metadata;
    private readonly ISharingServiceClient _sharing;

    public AccessChecker(IMetadataServiceClient metadata, ISharingServiceClient sharing)
    {
        _metadata = metadata;
        _sharing = sharing;
    }

    public async Task<FolderAccessResult?> CheckJwtAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        var info = await _metadata.GetFolderAccessAsync(folderId, userId, ct);
        if (info is null) return null;
        return new FolderAccessResult(info.FolderId, info.OwnerId, info.ScopePath);
    }

    public async Task<FolderAccessResult?> CheckShareTokenAsync(Guid folderId, string shareToken, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shareToken))).ToLowerInvariant();
        var info = await _sharing.GetShareByTokenHashAsync(hash, ct);
        if (info is null || info.Revoked || info.ExpiresAt < DateTime.UtcNow) return null;
        if (info.FolderId != folderId) return null;
        return new FolderAccessResult(info.FolderId, info.OwnerId, info.ScopePath);
    }
}
