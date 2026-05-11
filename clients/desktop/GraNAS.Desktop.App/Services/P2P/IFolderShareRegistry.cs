namespace GraNAS.Desktop.App.Services.P2P;

/// <summary>Реестр локальных путей для shared-папок: хранит маппинг <c>folderId → локальный путь на диске</c>.</summary>
public interface IFolderShareRegistry
{
    /// <summary>Возвращает локальный путь папки на диске или <c>null</c>, если папка не привязана локально.</summary>
    string? GetLocalPath(Guid folderId);
    /// <summary>Задаёт или обновляет локальный путь для папки. Изменение сохраняется на диск.</summary>
    void SetLocalPath(Guid folderId, string localPath);
    /// <summary>Удаляет маппинг папки. Изменение сохраняется на диск.</summary>
    void RemoveMapping(Guid folderId);
    /// <summary>Возвращает все сохранённые маппинги.</summary>
    IReadOnlyDictionary<Guid, string> GetAll();
}
