using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace GraNAS.Desktop.App.Services;

public class ClipboardService : IClipboardService
{
    public async Task CopyAsync(string text)
    {
        var mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
        if (mainWindow == null) return;
        var clipboard = TopLevel.GetTopLevel(mainWindow)?.Clipboard;
        if (clipboard == null) return;
        await clipboard.SetTextAsync(text);
    }
}
