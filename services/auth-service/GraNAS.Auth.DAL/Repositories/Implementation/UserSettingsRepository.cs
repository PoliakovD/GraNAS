using GraNAS.Auth.Models;
using GraNAS.Auth.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Auth.DAL.Repositories.Implementation;

public class UserSettingsRepository : IUserSettingsRepository
{
    private readonly AppDbContext _context;

    public UserSettingsRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserSettings.FirstOrDefaultAsync(s => s.UserId == userId, ct);

    public async Task UpsertAsync(UserSettings settings, CancellationToken ct = default)
    {
        var existing = await _context.UserSettings.FindAsync([settings.UserId], ct);
        if (existing is null)
        {
            _context.UserSettings.Add(settings);
        }
        else
        {
            existing.NotificationPrefsJson = settings.NotificationPrefsJson;
            existing.UpdatedAt = settings.UpdatedAt;
        }
        await _context.SaveChangesAsync(ct);
    }
}
