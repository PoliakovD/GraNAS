using GraNAS.Signaling.Models;
using GraNAS.Signaling.Models.Repositories;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.Signaling.DAL.Repositories.Implementation;

/// <summary>Репозиторий привязок «папка ↔ устройство» на базе EF Core / PostgreSQL.</summary>
public class DeviceFolderRepository : IDeviceFolderRepository
{
    private readonly SignalingDbContext _db;

    public DeviceFolderRepository(SignalingDbContext db) => _db = db;

    /// <inheritdoc/>
    public Task<DeviceFolder?> GetByFolderIdAsync(Guid folderId, CancellationToken ct = default)
        => _db.DeviceFolders.Include(df => df.Device).FirstOrDefaultAsync(df => df.FolderId == folderId, ct);

    /// <inheritdoc/>
    /// <remarks>
    /// Использует raw SQL с <c>ON CONFLICT (folder_id) DO UPDATE</c> для атомарной перепривязки.
    /// EF Core не поддерживает такой upsert напрямую, поэтому применяется <c>ExecuteSqlInterpolatedAsync</c>.
    /// </remarks>
    public async Task ClaimAsync(Guid deviceId, Guid folderId, CancellationToken ct = default)
    {
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO table_device_folders (folder_id, device_id, claimed_at)
            VALUES ({folderId}, {deviceId}, NOW())
            ON CONFLICT (folder_id) DO UPDATE SET device_id = EXCLUDED.device_id, claimed_at = NOW()
            """, ct);
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(Guid deviceId, Guid folderId, CancellationToken ct = default)
    {
        await _db.DeviceFolders
            .Where(df => df.FolderId == folderId && df.DeviceId == deviceId)
            .ExecuteDeleteAsync(ct);
    }

    /// <inheritdoc/>
    public Task<List<DeviceFolder>> GetByFolderIdsAsync(IEnumerable<Guid> folderIds, Guid userId, CancellationToken ct = default)
        => _db.DeviceFolders
            .Include(df => df.Device)
            .Where(df => folderIds.Contains(df.FolderId) && df.Device.UserId == userId)
            .ToListAsync(ct);

    /// <inheritdoc/>
    public async Task RemoveAllByDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        await _db.DeviceFolders
            .Where(df => df.DeviceId == deviceId)
            .ExecuteDeleteAsync(ct);
    }
}
