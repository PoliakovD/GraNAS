using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Models.Repositories;
using GraNAS.Sharing.Services.Interfaces;

namespace GraNAS.Sharing.Services.Implementations;

public class ShareService : IShareService
{
    private readonly IShareLinkRepository _repository;
    private readonly ITokenGenerator _tokenGenerator;
    private readonly IMetadataServiceClient _metadataClient;
    private readonly IShareEventPublisher _eventPublisher;

    public ShareService(
        IShareLinkRepository repository,
        ITokenGenerator tokenGenerator,
        IMetadataServiceClient metadataClient,
        IShareEventPublisher eventPublisher)
    {
        _repository = repository;
        _tokenGenerator = tokenGenerator;
        _metadataClient = metadataClient;
        _eventPublisher = eventPublisher;
    }

    public async Task<CreateShareResult> CreateAsync(
        Guid ownerId, Guid folderId, CreateShareRequest request, CancellationToken ct = default)
    {
        var folder = await _metadataClient.GetFolderForOwnerAsync(folderId, ownerId, ct);
        if (folder is null)
            return CreateShareResult.FolderNotFoundOrForbidden();

        var token = _tokenGenerator.GenerateToken();
        var tokenHash = _tokenGenerator.ComputeHash(token);

        var shareLink = new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            OwnerId = ownerId,
            TokenHash = tokenHash,
            Path = request.Path,
            ExpiresAt = request.ExpiresAt,
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        };

        await _repository.CreateAsync(shareLink);

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

        if (shareLink is null || shareLink.ExpiresAt < DateTime.UtcNow)
            return null;

        var folder = await _metadataClient.GetFolderAsync(shareLink.FolderId, ct);
        if (folder is null)
            return null;

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
            ExpiresAt = s.ExpiresAt,
            Revoked = s.Revoked,
            CreatedAt = s.CreatedAt
        });
    }

    public async Task<RevokeShareResult> RevokeByTokenAsync(Guid ownerId, string token)
    {
        var tokenHash = _tokenGenerator.ComputeHash(token);
        var shareLink = await _repository.GetByTokenHashAsync(tokenHash);

        if (shareLink is null || shareLink.OwnerId != ownerId)
            return new RevokeShareResult(RevokeShareError.NotFoundOrForbidden);

        return await RevokeShareLinkAsync(shareLink);
    }

    public async Task<RevokeShareResult> RevokeByIdAsync(Guid ownerId, Guid id)
    {
        var shareLink = await _repository.GetByIdForOwnerAsync(id, ownerId);
        if (shareLink is null)
            return new RevokeShareResult(RevokeShareError.NotFoundOrForbidden);

        return await RevokeShareLinkAsync(shareLink);
    }

    public Task<int> DeleteExpiredAsync(CancellationToken ct = default)
    {
        return _repository.DeleteExpiredAsync(DateTime.UtcNow);
    }

    private async Task<RevokeShareResult> RevokeShareLinkAsync(ShareLink shareLink)
    {
        shareLink.Revoked = true;
        shareLink.UpdatedAt = DateTime.UtcNow;
        await _repository.UpdateAsync(shareLink);

        await _eventPublisher.PublishShareRevokedAsync(shareLink.Id, shareLink.FolderId, shareLink.OwnerId);

        return new RevokeShareResult(RevokeShareError.None);
    }
}
