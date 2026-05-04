using System.Text.Json;
using GraNAS.Notifications.Services.Models;

namespace GraNAS.Notifications.Services.Interfaces;

public interface IEmailTemplateRenderer
{
    Task<RenderedEmail> RenderAsync(string eventType, JsonElement data, UserContact contact, CancellationToken ct = default);
}
