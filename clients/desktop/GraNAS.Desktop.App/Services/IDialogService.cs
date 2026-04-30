using GraNAS.Desktop.Contracts.Metadata;

namespace GraNAS.Desktop.App.Services;

public interface IDialogService
{
  Task<string?> ShowCreateFolderAsync();
  Task<(string Email, AccessLevel Level)?> ShowGrantPermissionAsync();
  Task<DateTime?> ShowCreateShareAsync();
  Task ShowShareCreatedAsync(string token);
  Task<string?> ShowFolderPickerAsync(string title = "Выберите папку");
}
