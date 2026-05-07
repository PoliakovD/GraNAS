using System;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace GraNAS.Shared.Messaging.Routing;

public static class EventRoutingConvention
{
    private static readonly ConcurrentDictionary<Type, string> _cache = new();

    public static string GetRoutingKey(Type eventType)
    {
        return _cache.GetOrAdd(eventType, t =>
        {
            var attr = (EventRoutingKeyAttribute?)Attribute.GetCustomAttribute(t, typeof(EventRoutingKeyAttribute));
            return attr?.RoutingKey ?? ToKebabCase(t.Name);
        });
    }

    public static string GetRoutingKey<T>() => GetRoutingKey(typeof(T));

    private static string ToKebabCase(string name)
    {
        return Regex.Replace(name, "([a-z])([A-Z])", "$1.$2").ToLowerInvariant();
    }
}
