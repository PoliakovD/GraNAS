namespace GraNAS.Desktop.App.Services.P2P;

public interface IP2PHost
{
    bool IsOnline { get; }
    bool ShouldBeOnline { get; set; }

    Task ConnectAsync(CancellationToken ct = default);
    Task DisconnectAsync();
    Task JoinFolderAsync(Guid folderId, CancellationToken ct = default);
    Task LeaveFolderAsync(Guid folderId);
}
