using System;
using GraNAS.Shared.Messaging.Routing;

namespace GraNAS.Shared.Messaging.Events;

[EventRoutingKey("access.revoked")]
public record AccessRevokedEvent : IntegrationEventBase
{
    public override string EventType => "access.revoked";
    public Guid TargetUserId { get; init; }
    public Guid OwnerId { get; init; }
    public Guid FolderId { get; init; }
    public string FolderName { get; init; } = string.Empty;
    public string? Path { get; init; }
}
