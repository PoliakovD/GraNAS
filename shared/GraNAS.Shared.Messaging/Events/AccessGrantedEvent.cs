using System;
using GraNAS.Shared.Messaging.Routing;

namespace GraNAS.Shared.Messaging.Events;

[EventRoutingKey("access.granted")]
public record AccessGrantedEvent : IntegrationEventBase
{
    public override string EventType => "access.granted";
    public Guid TargetUserId { get; init; }
    public Guid OwnerId { get; init; }
    public Guid FolderId { get; init; }
    public string FolderName { get; init; } = string.Empty;
    public string AccessLevel { get; init; } = string.Empty;
    public string? Path { get; init; }
}
