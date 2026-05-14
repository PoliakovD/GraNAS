using System.Reactive;
using System.Reactive.Linq;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.Services.P2P;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISignalingApi _signalingApi;
    private readonly IDeviceIdentity _deviceIdentity;
    private readonly INotificationService _notifications;

    private string _deviceName;
    private string _originalDeviceName;

    public string Email { get; }
    public string UserIdDisplay { get; }
    public string DeviceIdDisplay { get; }
    public string Platform { get; }

    public string DeviceName
    {
        get => _deviceName;
        set => this.RaiseAndSetIfChanged(ref _deviceName, value);
    }

    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetCommand { get; }

    public SettingsViewModel(
        IAuthSession session,
        ISignalingApi signalingApi,
        IDeviceIdentity deviceIdentity,
        INotificationService notifications)
    {
        _signalingApi = signalingApi;
        _deviceIdentity = deviceIdentity;
        _notifications = notifications;

        Email = session.CurrentUserEmail;
        UserIdDisplay = session.CurrentUserId.ToString();
        DeviceIdDisplay = deviceIdentity.DeviceId.ToString();
        Platform = deviceIdentity.Platform;

        _originalDeviceName = deviceIdentity.DeviceName;
        _deviceName = _originalDeviceName;

        var canSave = this.WhenAnyValue(vm => vm.DeviceName)
            .Select(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 100);

        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
        ResetCommand = ReactiveCommand.Create(() => { DeviceName = _originalDeviceName; });
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        var trimmed = DeviceName.Trim();
        if (trimmed == _originalDeviceName)
        {
            _notifications.Info("Имя не изменилось.");
            return;
        }

        try
        {
            await _signalingApi.RenameDeviceAsync(_deviceIdentity.DeviceId, trimmed, ct);
            _deviceIdentity.SetDeviceName(trimmed);
            _originalDeviceName = trimmed;
            DeviceName = trimmed;
            _notifications.Success("Имя устройства сохранено.");
        }
        catch (ApiException ex) when (ex.StatusCode == 409)
        {
            _notifications.Error("Имя уже занято другим устройством.");
            DeviceName = _originalDeviceName;
        }
        catch
        {
            _notifications.Error("Не удалось сохранить имя устройства.");
        }
    }
}
