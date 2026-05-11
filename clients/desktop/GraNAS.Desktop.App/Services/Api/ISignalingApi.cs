namespace GraNAS.Desktop.App.Services.Api;

/// <summary>TURN-учётные данные для конфигурации ICE-серверов <c>RTCPeerConnection</c>.</summary>
public record TurnCredentials(string Username, string Credential, string[] Uris, int Ttl);

/// <summary>Запрос на регистрацию устройства в signaling-service.</summary>
public record DeviceRegistrationRequest(Guid DeviceId, string DeviceName, string Platform);

/// <summary>Информация об устройстве с актуальным онлайн-статусом.</summary>
public record DeviceResponse(Guid DeviceId, string DeviceName, string Platform,
    DateTime CreatedAt, DateTime LastSeenAt, bool IsOnline);

/// <summary>Информация об активной SignalR-сессии устройства.</summary>
public record ActiveSessionResponse(Guid DeviceId, string DeviceName, string Platform,
    string Ip, DateTime ConnectedAt);

/// <summary>
/// Информация о конфликте при привязке папки: папка уже занята другим устройством того же пользователя.
/// Возвращается при HTTP 409 из <c>ClaimFolderAsync</c>.
/// </summary>
public record FolderDeviceConflict(Guid FolderId, Guid DeviceId, string DeviceName, string Platform);

/// <summary>REST-клиент к signaling-service для управления устройствами, сессиями и привязками папок.</summary>
public interface ISignalingApi
{
    /// <summary>Получает краткосрочные TURN-учётные данные (TTL ≈ 10 мин). Используется перед созданием <c>RTCPeerConnection</c>.</summary>
    Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default);
    /// <summary>Регистрирует или обновляет устройство в signaling-service.</summary>
    Task<DeviceResponse?> RegisterDeviceAsync(DeviceRegistrationRequest req, CancellationToken ct = default);
    /// <summary>Возвращает все устройства текущего пользователя.</summary>
    Task<List<DeviceResponse>> GetDevicesAsync(CancellationToken ct = default);
    /// <summary>Возвращает список активных онлайн-сессий устройств пользователя.</summary>
    Task<List<ActiveSessionResponse>> GetActiveSessionsAsync(CancellationToken ct = default);
    /// <summary>Принудительно завершает сессию устройства (вызывает <c>ForceDisconnect</c> на стороне сервера).</summary>
    Task TerminateSessionAsync(Guid deviceId, CancellationToken ct = default);
    /// <summary>
    /// Привязывает папку к устройству.
    /// </summary>
    /// <param name="force">Если <c>true</c> — перезаписывает существующую привязку.</param>
    /// <returns><c>null</c> при успехе (HTTP 204); <see cref="FolderDeviceConflict"/> при конфликте (HTTP 409).</returns>
    Task<FolderDeviceConflict?> ClaimFolderAsync(Guid deviceId, Guid folderId, bool force = false, CancellationToken ct = default);
    /// <summary>Освобождает привязку папки от устройства.</summary>
    Task ReleaseFolderAsync(Guid deviceId, Guid folderId, CancellationToken ct = default);
}
