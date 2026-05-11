namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>
/// Управляет P2P-подключением desktop-клиента к signaling-сервису и WebRTC-сессиями.
/// Desktop выступает в роли owner/sender: принимает входящие запросы от receiver'ов и отдаёт файлы.
/// </summary>
public interface IP2PHost
{
    /// <summary><c>true</c>, если SignalR-подключение к хабу активно в данный момент.</summary>
    bool IsOnline { get; }
    /// <summary>
    /// Намерение пользователя быть онлайн. При <c>false</c> автоматическое переподключение отключено.
    /// Сбрасывается в <c>false</c> при получении события <c>ForceDisconnect</c> от сервера.
    /// </summary>
    bool ShouldBeOnline { get; set; }

    /// <summary>
    /// Подключается к SignalR-хабу, регистрирует устройство и присоединяется ко всем папкам из <see cref="IFolderShareRegistry"/>.
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);
    /// <summary>Отключается от SignalR-хаба и завершает все активные P2P-сессии.</summary>
    Task DisconnectAsync();
    /// <summary>Вызывает <c>JoinAsOwner(folderId)</c> в хабе, объявляя эту папку доступной для P2P.</summary>
    Task JoinFolderAsync(Guid folderId, CancellationToken ct = default);
    /// <summary>Вызывает <c>LeaveAsOwner(folderId)</c> в хабе.</summary>
    Task LeaveFolderAsync(Guid folderId);
}
