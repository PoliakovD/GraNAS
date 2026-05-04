using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Signaling.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace GraNAS.Signaling.Services.Implementations;

public class AccessChecker : IAccessChecker
{
    private readonly IMetadataServiceClient _metadata;
    private readonly ISharingServiceClient _sharing;
    private readonly ILogger<AccessChecker> _logger;

    public AccessChecker(IMetadataServiceClient metadata, ISharingServiceClient sharing, ILogger<AccessChecker> logger)
    {
        _metadata = metadata;
        _sharing = sharing;
        _logger = logger;
    }

    public async Task<FolderAccessResult?> CheckJwtAccessAsync(Guid folderId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            var info = await _metadata.GetFolderAccessAsync(folderId, userId, ct);
            if (info is null)
            {
                _logger.LogDebug("AccessChecker: user {UserId} has no JWT access to folder {FolderId}", userId, folderId);
                return null;
            }
            return new FolderAccessResult(info.FolderId, info.OwnerId, info.ScopePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AccessChecker: metadata-service unavailable (folder={FolderId} user={UserId})",
                folderId, userId);
            return null;
        }
    }

    public async Task<FolderAccessResult?> CheckShareTokenAsync(Guid folderId, string shareToken, CancellationToken ct = default)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shareToken))).ToLowerInvariant();
        var info = await _sharing.GetShareByTokenHashAsync(hash, ct);

        if (info is null)
        {
            _logger.LogWarning("AccessChecker: share token not found (folder={FolderId})", folderId);
            return null;
        }

        if (info.Revoked)
        {
            _logger.LogWarning("AccessChecker: share token revoked (folder={FolderId})", folderId);
            return null;
        }

        if (info.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("AccessChecker: share token expired (folder={FolderId})", folderId);
            return null;
        }

        if (info.FolderId != folderId)
        {
            _logger.LogWarning("AccessChecker: share token folder mismatch (expected={FolderId} got={TokenFolderId})",
                folderId, info.FolderId);
            return null;
        }

        return new FolderAccessResult(info.FolderId, info.OwnerId, info.ScopePath);
    }
}
