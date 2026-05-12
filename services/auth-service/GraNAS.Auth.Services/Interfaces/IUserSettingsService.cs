using GraNAS.Auth.Models.DTO;

namespace GraNAS.Auth.Services.Interfaces;

public interface IUserSettingsService
{
    Task<NotificationPrefsDto> GetPrefsAsync(Guid userId, CancellationToken ct = default);
    Task UpdatePrefsAsync(Guid userId, NotificationPrefsDto prefs, CancellationToken ct = default);
}
