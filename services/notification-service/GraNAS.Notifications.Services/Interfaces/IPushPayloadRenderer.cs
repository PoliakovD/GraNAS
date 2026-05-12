using GraNAS.Notifications.Models.Entities;

namespace GraNAS.Notifications.Services.Interfaces;

public interface IPushPayloadRenderer
{
    string Render(NotificationEvent notification);
}
