using System.Text.Json;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.Services.Implementations;

public class EmailTemplateRenderer : IEmailTemplateRenderer
{
    private readonly ILogger<EmailTemplateRenderer> _logger;

    public EmailTemplateRenderer(ILogger<EmailTemplateRenderer> logger)
    {
        _logger = logger;
    }

    public Task<RenderedEmail> RenderAsync(string eventType, JsonElement data, UserContact contact, CancellationToken ct = default)
    {
        var folderName = GetString(data, "FolderName", "folderName") ?? "папка";
        var ownerName = GetString(data, "OwnerName", "ownerName") ?? "Владелец";

        var rendered = eventType switch
        {
            "access.granted" => RenderGranted(folderName, ownerName, contact),
            "access.revoked" => RenderRevoked(folderName, ownerName, contact),
            "share.revoked"  => RenderShareRevoked(folderName, contact),
            "access.lost"    => RenderAccessLost(folderName, contact),
            _ => RenderGeneric(eventType, contact)
        };

        return Task.FromResult(rendered);
    }

    private static RenderedEmail RenderGranted(string folderName, string ownerName, UserContact contact)
    {
        var subject = $"Вам предоставлен доступ к папке «{folderName}»";
        var html = $"""
            <h2>Привет, {contact.DisplayName}!</h2>
            <p>Пользователь <strong>{ownerName}</strong> предоставил вам доступ к папке
            <strong>«{folderName}»</strong>.</p>
            <p>Войдите в GraNAS, чтобы просмотреть содержимое.</p>
            """;
        var text = $"Привет, {contact.DisplayName}!\n{ownerName} предоставил вам доступ к папке «{folderName}».";
        return new RenderedEmail(subject, html, text);
    }

    private static RenderedEmail RenderRevoked(string folderName, string ownerName, UserContact contact)
    {
        var subject = $"Ваш доступ к папке «{folderName}» был отозван";
        var html = $"""
            <h2>Привет, {contact.DisplayName}!</h2>
            <p>Пользователь <strong>{ownerName}</strong> отозвал ваш доступ к папке
            <strong>«{folderName}»</strong>.</p>
            """;
        var text = $"Привет, {contact.DisplayName}!\nДоступ к папке «{folderName}» был отозван.";
        return new RenderedEmail(subject, html, text);
    }

    private static RenderedEmail RenderShareRevoked(string folderName, UserContact contact)
    {
        var subject = $"Ссылка на папку «{folderName}» больше не действует";
        var html = $"""
            <h2>Привет, {contact.DisplayName}!</h2>
            <p>Ссылка, по которой вы получили доступ к папке <strong>«{folderName}»</strong>,
            была отозвана владельцем.</p>
            """;
        var text = $"Привет, {contact.DisplayName}!\nСсылка на папку «{folderName}» была отозвана.";
        return new RenderedEmail(subject, html, text);
    }

    private static RenderedEmail RenderAccessLost(string folderName, UserContact contact)
    {
        var subject = $"Папка «{folderName}» была удалена";
        var html = $"""
            <h2>Привет, {contact.DisplayName}!</h2>
            <p>Папка <strong>«{folderName}»</strong>, к которой у вас был доступ,
            была удалена владельцем.</p>
            """;
        var text = $"Привет, {contact.DisplayName}!\nПапка «{folderName}» была удалена.";
        return new RenderedEmail(subject, html, text);
    }

    private RenderedEmail RenderGeneric(string eventType, UserContact contact)
    {
        _logger.LogWarning("EmailTemplateRenderer: unknown event type {EventType} — using generic template", eventType);
        return new RenderedEmail(
            "Уведомление от GraNAS",
            $"<p>Привет, {contact.DisplayName}! Произошло событие в GraNAS.</p>",
            $"Привет, {contact.DisplayName}! Произошло событие в GraNAS.");
    }

    private static string? GetString(JsonElement el, params string[] keys)
    {
        foreach (var key in keys)
            if (el.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String)
                return prop.GetString();
        return null;
    }
}
