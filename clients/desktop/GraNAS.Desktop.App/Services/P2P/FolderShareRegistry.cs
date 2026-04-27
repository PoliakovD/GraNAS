using System.Text.Json;

namespace GraNAS.Desktop.App.Services.P2P;

public class FolderShareRegistry : IFolderShareRegistry
{
    private readonly string _filePath;
    private Dictionary<Guid, string> _mappings;
    private static readonly JsonSerializerOptions Opts = new(JsonSerializerDefaults.Web);

    public FolderShareRegistry()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GraNAS");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "folder-mappings.json");
        _mappings = Load();
    }

    public string? GetLocalPath(Guid folderId)
        => _mappings.TryGetValue(folderId, out var path) ? path : null;

    public void SetLocalPath(Guid folderId, string localPath)
    {
        _mappings[folderId] = localPath;
        Save();
    }

    public void RemoveMapping(Guid folderId)
    {
        _mappings.Remove(folderId);
        Save();
    }

    public IReadOnlyDictionary<Guid, string> GetAll() => _mappings;

    private Dictionary<Guid, string> Load()
    {
        if (!File.Exists(_filePath)) return [];
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<Dictionary<Guid, string>>(json, Opts) ?? [];
        }
        catch { return []; }
    }

    private void Save()
    {
        try { File.WriteAllText(_filePath, JsonSerializer.Serialize(_mappings, Opts)); }
        catch { /* best-effort */ }
    }
}
