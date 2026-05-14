namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>Предоставляет стабильную идентификацию устройства и отслеживает факт регистрации по пользователям.</summary>
public interface IDeviceIdentity
{
    /// <summary>Уникальный идентификатор устройства. Генерируется при первом запуске и сохраняется в Credential Manager.</summary>
    Guid DeviceId { get; }
    string DeviceName { get; }
    string Platform { get; }

    /// <summary>Задаёт пользовательское имя устройства и сохраняет его в Credential Manager.</summary>
    void SetDeviceName(string deviceName);

    /// <summary>Проверяет, было ли устройство уже зарегистрировано в signaling-service для указанного пользователя.</summary>
    bool IsRegisteredForUser(Guid userId);
    /// <summary>Отмечает устройство как зарегистрированное для указанного пользователя.</summary>
    void MarkRegisteredForUser(Guid userId);
}
