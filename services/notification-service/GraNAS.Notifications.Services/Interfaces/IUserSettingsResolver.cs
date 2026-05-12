using GraNAS.Notifications.Models;

namespace GraNAS.Notifications.Services.Interfaces;

public interface IUserSettingsResolver
{
    Task<NotificationPrefs> GetPrefsAsync(Guid userId, CancellationToken ct = default);
}
