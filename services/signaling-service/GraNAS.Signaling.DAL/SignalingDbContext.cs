using Microsoft.EntityFrameworkCore;

namespace GraNAS.Signaling.DAL;

/// <summary>
/// EF Core DbContext базы данных сигналинга (<c>signalingdb</c> в PostgreSQL).
/// Содержит устройства пользователей и их привязки к папкам.
/// Миграции применяются автоматически при старте сервиса через <c>MigrateAsync()</c>.
/// </summary>
public class SignalingDbContext : DbContext
{
    /// <summary>Устройства пользователей (таблица <c>table_devices</c>).</summary>
    public DbSet<GraNAS.Signaling.Models.Device> Devices => Set<GraNAS.Signaling.Models.Device>();
    /// <summary>Привязки папок к устройствам (таблица <c>table_device_folders</c>).</summary>
    public DbSet<GraNAS.Signaling.Models.DeviceFolder> DeviceFolders => Set<GraNAS.Signaling.Models.DeviceFolder>();

    public SignalingDbContext(DbContextOptions<SignalingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SignalingDbContext).Assembly);
    }
}
