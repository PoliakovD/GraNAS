using System.Text.Json;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Options;
using Microsoft.Extensions.Options;

namespace GraNAS.Notifications.Services.Implementations;

public class PushPayloadRenderer : IPushPayloadRenderer
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly string _webClientBaseUrl;

    public PushPayloadRenderer(IOptions<WebClientOptions> opts)
    {
        _webClientBaseUrl = opts.Value.BaseUrl.TrimEnd('/');
    }

    public string Render(NotificationEvent notification)
    {
        var data = notification.Data.RootElement;
        var folderName = GetString(data, "FolderName", "folderName") ?? "папка";
        var folderId   = GetString(data, "FolderId",   "folderId");

        var url = folderId is not null
            ? $"{_webClientBaseUrl}/folders/{folderId}"
            : _webClientBaseUrl;

        var (title, body) = notification.Type switch
        {
            "access.granted" => ("Доступ к папке предоставлен", $"Вам открыли доступ к «{folderName}»"),
            "access.revoked" => ("Доступ к папке отозван",      $"Доступ к «{folderName}» закрыт"),
            "share.revoked"  => ("Ссылка на папку отозвана",    $"Ссылка на «{folderName}» больше не действует"),
            "access.lost"    => ("Доступ к папке потерян",      $"Папка «{folderName}» недоступна"),
            _ => ("Уведомление GraNAS", "У вас новое уведомление")
        };

        return JsonSerializer.Serialize(new
        {
            title,
            body,
            url,
            type = notification.Type
        }, _json);
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
            if (el.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.String)
                return p.GetString();
        return null;
    }
}
