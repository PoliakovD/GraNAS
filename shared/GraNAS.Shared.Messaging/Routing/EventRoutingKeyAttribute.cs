using System;

namespace GraNAS.Shared.Messaging.Routing;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class EventRoutingKeyAttribute : Attribute
{
    public string RoutingKey { get; }

    public EventRoutingKeyAttribute(string routingKey)
    {
        RoutingKey = routingKey;
    }
}
