using System.Text.Json;
using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Interfaces;

namespace GraNAS.Auth.Services.Implementations;

public class UserSettingsService : IUserSettingsService
{
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    private readonly IUserSettingsRepository _repo;

    public UserSettingsService(IUserSettingsRepository repo)
    {
        _repo = repo;
    }

    public async Task<NotificationPrefsDto> GetPrefsAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await _repo.GetByUserIdAsync(userId, ct);
        if (settings is null)
        {
            var defaults = BuildDefaults(emailEnabled: true);
            await _repo.UpsertAsync(ToEntity(userId, defaults), ct);
            return defaults;
        }
        return Deserialize(settings.NotificationPrefsJson);
    }

    public async Task UpdatePrefsAsync(Guid userId, NotificationPrefsDto prefs, CancellationToken ct = default)
    {
        FillMissingWithDefaults(prefs);
        await _repo.UpsertAsync(ToEntity(userId, prefs), ct);
    }

    internal static NotificationPrefsDto BuildDefaults(bool emailEnabled)
    {
        var email = new NotificationChannelPrefs
        {
            AccessGranted = emailEnabled,
            AccessRevoked = emailEnabled,
            ShareRevoked  = emailEnabled,
            AccessLost    = emailEnabled,
        };
        return new NotificationPrefsDto
        {
            Email   = email,
            InApp   = new NotificationChannelPrefs(),  // all true
            WebPush = new NotificationChannelPrefs { AccessGranted = false, AccessRevoked = false, ShareRevoked = false, AccessLost = false },
        };
    }

    private static void FillMissingWithDefaults(NotificationPrefsDto prefs)
    {
        prefs.Email   ??= new NotificationChannelPrefs();
        prefs.InApp   ??= new NotificationChannelPrefs();
        prefs.WebPush ??= new NotificationChannelPrefs { AccessGranted = false, AccessRevoked = false, ShareRevoked = false, AccessLost = false };
    }

    private static NotificationPrefsDto Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<NotificationPrefsDto>(json, _json) ?? BuildDefaults(true);
        }
        catch
        {
            return BuildDefaults(true);
        }
    }

    private static UserSettings ToEntity(Guid userId, NotificationPrefsDto prefs) => new()
    {
        UserId = userId,
        NotificationPrefsJson = JsonSerializer.Serialize(prefs, _json),
        UpdatedAt = DateTime.UtcNow,
    };
}
