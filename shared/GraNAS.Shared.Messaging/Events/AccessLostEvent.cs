using System;
using GraNAS.Shared.Messaging.Routing;

namespace GraNAS.Shared.Messaging.Events;

[EventRoutingKey("access.lost")]
public record AccessLostEvent : IntegrationEventBase
{
    public override string EventType => "access.lost";
    public Guid TargetUserId { get; init; }
    public Guid OwnerId { get; init; }
    public Guid FolderId { get; init; }
    public string FolderName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
