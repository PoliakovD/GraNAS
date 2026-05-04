using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Notifications.Models;
using GraNAS.Notifications.Models.Entities;
using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Models;
using Microsoft.AspNetCore.SignalR;

namespace GraNAS.Notifications.API.Infrastructure;

public class NotificationDeliveryService
{
    private readonly IUserContactResolver _contactResolver;
    private readonly IEmailTemplateRenderer _templateRenderer;
    private readonly IEmailSender _emailSender;
    private readonly IHubContext<Hubs.NotificationsHub> _hubContext;

    public NotificationDeliveryService(
        IUserContactResolver contactResolver,
        IEmailTemplateRenderer templateRenderer,
        IEmailSender emailSender,
        IHubContext<Hubs.NotificationsHub> hubContext)
    {
        _contactResolver = contactResolver;
        _templateRenderer = templateRenderer;
        _emailSender = emailSender;
        _hubContext = hubContext;
    }

    public async Task<DeliveryResult> DeliverEmailAsync(
        NotificationOutbox row, NotificationEvent evt, CancellationToken ct = default)
    {
        var contact = await _contactResolver.GetByUserIdAsync(evt.UserId, ct);
        if (contact is null)
            return DeliveryResult.PermanentFailure;

        try
        {
            var rendered = await _templateRenderer.RenderAsync(
                evt.Type, evt.Data.RootElement, contact, ct);
            await _emailSender.SendAsync(contact.Email, rendered.Subject, rendered.Html, rendered.Text, ct);
            return DeliveryResult.Success;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ex is FormatException or ArgumentException
                ? DeliveryResult.PermanentFailure
                : DeliveryResult.TransientFailure;
        }
    }

    public async Task<DeliveryResult> DeliverSignalRAsync(
        NotificationOutbox row, NotificationEvent evt, CancellationToken ct = default)
    {
        var dto = new
        {
            id = evt.Id,
            type = evt.Type,
            data = evt.Data.RootElement,
            isRead = evt.IsRead,
            createdAt = evt.CreatedAt
        };
        await _hubContext.Clients
            .Group($"user-{evt.UserId}")
            .SendAsync("NotificationReceived", dto, ct);
        return DeliveryResult.Success;
    }
}
