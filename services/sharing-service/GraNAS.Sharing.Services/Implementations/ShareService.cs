using GraNAS.Shared.Messaging.Abstractions;
using GraNAS.Shared.Messaging.Events;
using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Models.Repositories;
using GraNAS.Sharing.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GraNAS.Sharing.Services.Implementations;

public class ShareService : IShareService
{
    private readonly IShareLinkRepository _repository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly ITokenEncryptionService _encryption;
    private readonly IMetadataServiceClient _metadataClient;
    private readonly IEventPublisher _eventPublisher;
    private readonly ILogger<ShareService> _logger;
    private readonly string _baseUrl;

    public ShareService(
        IShareLinkRepository repository,
        ITokenGenerator tokenGenerator,
        ITokenEncryptionService encryption,
        IMetadataServiceClient metadataClient,
        IEventPublisher eventPublisher,
        ILogger<ShareService> logger,
        IConfiguration configuration)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _encryption = encryption;
        _metadataClient = metadataClient;
        _eventPublisher = eventPublisher;
        _logger = logger;
        _baseUrl = (configuration["App:BaseUrl"]
            ?? throw new InvalidOperationException("App:BaseUrl is not configured."))
            .TrimEnd('/');
    }

    public async Task<CreateShareResult> CreateAsync(
        Guid ownerId, Guid folderId, CreateShareRequest request, CancellationToken ct = default)
    {
        var folder = await _metadataClient.GetFolderForOwnerAsync(folderId, ownerId, ct);
        if (folder is null)
        {
            _logger.LogWarning("CreateShare: folder {FolderId} not owned by {OwnerId}", folderId, ownerId);
            return CreateShareResult.FolderNotFoundOrForbidden();
        }

        var token = _tokenGenerator.GenerateToken();
        var tokenHash = _tokenGenerator.ComputeHash(token);
        var tokenEncrypted = _encryption.Encrypt(token);

        var shareLink = new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            OwnerId = ownerId,
            TokenHash = tokenHash,
            TokenEncrypted = tokenEncrypted,
            Path = request.Path,
            ExpiresAt = request.ExpiresAt,
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(shareLink);
        _logger.LogInformation(
            "CreateShare: created share link {ShareLinkId} folder={FolderId} owner={OwnerId} expires={ExpiresAt}",
            shareLink.Id, folderId, ownerId, shareLink.ExpiresAt);

        return CreateShareResult.Success(new CreateShareResponse
        {
            Id = shareLink.Id,
            FolderId = shareLink.FolderId,
            Token = token,
            Path = shareLink.Path,
            ExpiresAt = shareLink.ExpiresAt,
            CreatedAt = shareLink.CreatedAt
        });
    }

    public async Task<ShareDetailsResponse?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        var tokenHash = _tokenGenerator.ComputeHash(token);
        var shareLink = await _repository.GetByTokenHashAsync(tokenHash);

        if (shareLink is null)
        {
            _logger.LogDebug("GetShareByToken: not found");
            return null;
        }

        if (shareLink.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("GetShareByToken: expired (id={ShareLinkId})", shareLink.Id);
            return null;
        }

        var folder = await _metadataClient.GetFolderAsync(shareLink.FolderId, ct);
        if (folder is null)
        {
            _logger.LogWarning("GetShareByToken: folder {FolderId} not found for share link {ShareLinkId}",
                shareLink.FolderId, shareLink.Id);
            return null;
        }

        return new ShareDetailsResponse
        {
            FolderId = shareLink.FolderId,
            FolderName = folder.Name,
            OwnerId = shareLink.OwnerId,
            Path = shareLink.Path,
            ExpiresAt = shareLink.ExpiresAt
        };
    }

    public async Task<bool> IsRevokedAsync(string token)
    {
        var tokenHash = _tokenGenerator.ComputeHash(token);
        var shareLink = await _repository.GetByTokenHashAsync(tokenHash);
        return shareLink is not null && shareLink.Revoked;
    }

    public async Task<IEnumerable<ShareLinkResponse>> ListByFolderAsync(Guid ownerId, Guid folderId)
    {
        var links = await _repository.ListByFolderForOwnerAsync(folderId, ownerId);
        return links.Select(s => new ShareLinkResponse
        {
            Id = s.Id,
            FolderId = s.FolderId,
            Path = s.Path,
            ShareUrl = BuildShareUrl(s.TokenEncrypted),
            ExpiresAt = s.ExpiresAt,
            Revoked = s.Revoked,
            CreatedAt = s.CreatedAt
        });
    }

    public async Task<IEnumerable<ShareLinkOwnerResponse>> ListByOwnerAsync(
        Guid ownerId, bool activeOnly, int take, CancellationToken ct)
    {
        var links = (await _repository.ListByOwnerAsync(ownerId, activeOnly, take, ct)).ToList();

        // Batch-fetch folder names via Task.WhenAll (TODO: replace with batch endpoint when available)
        var folderIds = links.Select(l => l.FolderId).Distinct().ToList();
        var folderTasks = folderIds.ToDictionary(
            id => id,
            id => _metadataClient.GetFolderAsync(id, ct));
        await Task.WhenAll(folderTasks.Values);
        var folderNames = folderTasks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Result?.Name ?? "—");

        return links.Select(l => new ShareLinkOwnerResponse
        {
            Id = l.Id,
            FolderId = l.FolderId,
            FolderName = folderNames.GetValueOrDefault(l.FolderId, "—"),
            Path = l.Path,
            ShareUrl = BuildShareUrl(l.TokenEncrypted),
            ExpiresAt = l.ExpiresAt,
            Revoked = l.Revoked,
            CreatedAt = l.CreatedAt,
            OpenCount = 0,
        });
    }

    private string BuildShareUrl(string tokenEncrypted)
    {
        if (string.IsNullOrEmpty(tokenEncrypted))
            return string.Empty;
        try
        {
            var token = _encryption.Decrypt(tokenEncrypted);
            return $"{_baseUrl}/s/{token}";
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<RevokeShareResult> RevokeByTokenAsync(Guid ownerId, string token)
    {
        var tokenHash = _tokenGenerator.ComputeHash(token);
        var shareLink = await _repository.GetByTokenHashAsync(tokenHash);

        if (shareLink is null || shareLink.OwnerId != ownerId)
        {
            _logger.LogWarning("Revoke: share link not found or not owned by {OwnerId}", ownerId);
            return new RevokeShareResult(RevokeShareError.NotFoundOrForbidden);
        }

        return await RevokeShareLinkAsync(shareLink);
    }

    public async Task<RevokeShareResult> RevokeByIdAsync(Guid ownerId, Guid id)
    {
        var shareLink = await _repository.GetByIdForOwnerAsync(id, ownerId);
        if (shareLink is null)
        {
            _logger.LogWarning("Revoke: share link {ShareLinkId} not found or not owned by {OwnerId}", id, ownerId);
            return new RevokeShareResult(RevokeShareError.NotFoundOrForbidden);
        }

        return await RevokeShareLinkAsync(shareLink);
    }

    public Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        return _repository.DeleteExpiredAsync(DateTime.UtcNow);
    }

    public Task<ShareLink?> GetByTokenHashInternalAsync(string tokenHash, CancellationToken ct = default)
        => _repository.GetByTokenHashAsync(tokenHash);

    private async Task<RevokeShareResult> RevokeShareLinkAsync(ShareLink shareLink)
    {
        shareLink.Revoked = true;
        shareLink.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(shareLink);

        _logger.LogInformation("Revoke: share link {ShareLinkId} revoked by {OwnerId}", shareLink.Id, shareLink.OwnerId);

        try
        {
            await _eventPublisher.PublishAsync(new ShareRevokedEvent
            {
                ShareLinkId = shareLink.Id,
                FolderId = shareLink.FolderId,
                OwnerId = shareLink.OwnerId
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Revoke: failed to publish share_revoked event for {ShareLinkId}", shareLink.Id);
        }

        return new RevokeShareResult(RevokeShareError.None);
    }
}
