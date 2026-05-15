using System.Reactive;
using System.Reactive.Disposables;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Metadata;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class FolderPropertiesViewModel : ViewModelBase
{
    private readonly ISignalingApi _signalingApi;
    private readonly FolderResponse _folder;
    private readonly bool _isOwner;

    private string? _deviceName;
    private bool _deviceIsOnline;
    private bool _hasDevice;
    private Guid? _boundDeviceId;

    public string FolderIdShort => _folder.Id.ToString()[..8] + "…";
    public string CreatedAt => _folder.CreatedAt.ToString("dd.MM.yyyy HH:mm");
    public string UpdatedAt => _folder.UpdatedAt.HasValue
        ? RelTime(_folder.UpdatedAt.Value) : "—";
    public string OwnerDisplay => _folder.OwnerEmail ?? _folder.OwnerId.ToString()[..8] + "…";

    public bool IsOwner => _isOwner;
    public bool HasDevice { get => _hasDevice; private set => this.RaiseAndSetIfChanged(ref _hasDevice, value); }
    public string? DeviceName { get => _deviceName; private set => this.RaiseAndSetIfChanged(ref _deviceName, value); }
    public bool DeviceIsOnline { get => _deviceIsOnline; private set => this.RaiseAndSetIfChanged(ref _deviceIsOnline, value); }

    public ReactiveCommand<Unit, Unit> LoadCommand { get; }
    public ReactiveCommand<Unit, Unit> ReleaseDeviceCommand { get; }

    public FolderPropertiesViewModel(FolderResponse folder, ISignalingApi signalingApi, IAuthSession session, INotificationService notifications)
    {
        _folder = folder;
        _signalingApi = signalingApi;
        _isOwner = folder.OwnerId == session.CurrentUserId;

        LoadCommand = ReactiveCommand.CreateFromTask(LoadDeviceAsync);
        ReleaseDeviceCommand = ReactiveCommand.CreateFromTask(ReleaseDeviceAsync);

        this.WhenActivated((CompositeDisposable _) =>
        {
            if (_isOwner) LoadCommand.Execute().Subscribe();
        });
    }

    private async Task LoadDeviceAsync(CancellationToken ct)
    {
        var bindings = await _signalingApi.GetFolderDevicesAsync(new[] { _folder.Id }, ct);
        var binding = bindings.FirstOrDefault(b => b.FolderId == _folder.Id);
        if (binding is not null)
        {
            _boundDeviceId = binding.DeviceId;
            DeviceName = binding.DeviceName;
            DeviceIsOnline = binding.IsOnline;
            HasDevice = true;
        }
        else
        {
            HasDevice = false;
            _boundDeviceId = null;
        }
    }

    private async Task ReleaseDeviceAsync(CancellationToken ct)
    {
        if (_boundDeviceId is null) return;
        await _signalingApi.ReleaseFolderAsync(_boundDeviceId.Value, _folder.Id, ct);
        HasDevice = false;
        DeviceName = null;
        _boundDeviceId = null;
    }

    private static string RelTime(DateTime dt)
    {
        var diff = DateTime.UtcNow - dt.ToUniversalTime();
        if (diff.TotalMinutes < 2) return "только что";
        if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} мин назад";
        if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} ч назад";
        if (diff.TotalDays < 30) return $"{(int)diff.TotalDays} д назад";
        return dt.ToString("dd.MM.yyyy");
    }
}
