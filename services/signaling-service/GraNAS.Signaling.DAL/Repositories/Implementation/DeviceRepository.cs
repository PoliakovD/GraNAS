using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Signaling.DAL.Repositories.Implementation;

public class DeviceRepository : IDeviceRepository
{
    private readonly SignalingDbContext _db;

    public DeviceRepository(SignalingDbContext db) => _db = db;

    public Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Devices.FirstOrDefaultAsync(d => d.Id == id, ct);

    public Task<List<Device>> GetByUserAsync(Guid userId, CancellationToken ct = default)
        => _db.Devices.Where(d => d.UserId == userId).ToListAsync(ct);

    public async Task<Device> UpsertAsync(Device device, CancellationToken ct = default)
    {
        var existing = await _db.Devices.FirstOrDefaultAsync(d => d.Id == device.Id, ct);

        if (existing is null)
        {
            device.CreatedAt = DateTime.UtcNow;
            device.LastSeenAt = DateTime.UtcNow;
            _db.Devices.Add(device);
            await _db.SaveChangesAsync(ct);
            return device;
        }

        if (existing.UserId != device.UserId)
            throw new InvalidOperationException("Device belongs to a different user.");

        existing.DeviceName = device.DeviceName;
        existing.Platform = device.Platform;
        existing.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public Task<bool> BelongsToUserAsync(Guid deviceId, Guid userId, CancellationToken ct = default)
        => _db.Devices.AnyAsync(d => d.Id == deviceId && d.UserId == userId, ct);

    public async Task TouchLastSeenAsync(Guid id, CancellationToken ct = default)
    {
        await _db.Devices
            .Where(d => d.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.LastSeenAt, DateTime.UtcNow), ct);
    }
}
