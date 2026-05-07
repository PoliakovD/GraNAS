using System;
using GraNAS.Shared.Messaging.Routing;

namespace GraNAS.Shared.Messaging.Events;

[EventRoutingKey("share.revoked")]
public record ShareRevokedEvent : IntegrationEventBase
{
    public override string EventType => "share.revoked";
    public Guid ShareLinkId { get; init; }
    public Guid FolderId { get; init; }
    public Guid OwnerId { get; init; }
}
