using GraNAS.Desktop.App.Services.Auth;

namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>
/// Реализация идентификации устройства на основе Windows Credential Manager.
/// </summary>
/// <remarks>
/// <c>DeviceId</c> хранится под ключом <c>GraNAS:deviceId</c> в Credential Manager.
/// При отсутствии — генерируется новый UUID и сохраняется.
/// Факт регистрации устройства для каждого пользователя хранится отдельно:
/// <c>GraNAS:deviceRegistered:{deviceId}:{userId}</c>.
/// </remarks>
public class DeviceIdentity : IDeviceIdentity
{
    private const string DeviceIdKey = "deviceId";

    private readonly ICredentialStore _store;

    public Guid DeviceId { get; }
    public string DeviceName { get; }
    public string Platform => "windows";

    /// <summary>
    /// Инициализирует идентификацию устройства: читает или генерирует <c>DeviceId</c>,
    /// использует <c>Environment.MachineName</c> как имя устройства.
    /// </summary>
    public DeviceIdentity(ICredentialStore store)
    {
        _store = store;
        var saved = store.Get(DeviceIdKey);
        if (Guid.TryParse(saved, out var existing))
        {
            DeviceId = existing;
        }
        else
        {
            DeviceId = Guid.NewGuid();
            store.Save(DeviceIdKey, DeviceId.ToString());
        }
        DeviceName = Environment.MachineName;
    }

    /// <inheritdoc/>
    public bool IsRegisteredForUser(Guid userId) =>
        _store.Get($"deviceRegistered:{DeviceId}:{userId}") == "1";

    /// <inheritdoc/>
    public void MarkRegisteredForUser(Guid userId) =>
        _store.Save($"deviceRegistered:{DeviceId}:{userId}", "1");
}
