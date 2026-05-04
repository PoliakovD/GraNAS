namespace GraNAS.Desktop.App.Services.P2P;

public interface IFolderShareRegistry
{
    string? GetLocalPath(Guid folderId);
    void SetLocalPath(Guid folderId, string localPath);
    void RemoveMapping(Guid folderId);
    IReadOnlyDictionary<Guid, string> GetAll();
}
