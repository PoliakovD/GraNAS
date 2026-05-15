namespace GraNAS.Desktop.App.Services;

public interface IClipboardService
{
    Task CopyAsync(string text);
}
