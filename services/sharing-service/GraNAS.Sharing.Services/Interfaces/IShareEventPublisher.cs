namespace GraNAS.Sharing.Services.Interfaces;

public interface IShareEventPublisher
{
    Task PublishShareRevokedAsync(Guid shareLinkId, Guid folderId, Guid ownerId);
}
