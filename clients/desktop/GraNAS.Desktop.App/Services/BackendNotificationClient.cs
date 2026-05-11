using System.Collections.ObjectModel;
using System.Text.Json;
using GraNAS.Desktop.App.Models;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace GraNAS.Desktop.App.Services;

/// <summary>
/// Клиент SignalR-хаба уведомлений (<c>/hubs/notifications</c>).
/// Получает push-события от backend в реальном времени и обновляет <see cref="Notifications"/>
/// на UI-потоке. При недоступности хаба <c>MarkReadAsync</c> падает обратно на REST API.
/// </summary>
public class BackendNotificationClient : IBackendNotificationService, IAsyncDisposable
{
    private readonly string _hubUrl;
    private readonly HttpClient _http;
    private readonly ILogger<BackendNotificationClient> _logger;
    private HubConnection? _connection;

    public ObservableCollection<BackendNotificationVm> Notifications { get; } = new();
    public int UnreadCount => Notifications.Count(n => !n.IsRead);
    public event EventHandler? UnreadCountChanged;

    public BackendNotificationClient(string hubUrl, HttpClient http, ILogger<BackendNotificationClient> logger)
    {
        _hubUrl = hubUrl;
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(string accessToken, CancellationToken ct = default)
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<JsonElement>("NotificationReceived", item =>
        {
            var vm = MapToVm(item);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Notifications.Insert(0, vm);
                UnreadCountChanged?.Invoke(this, EventArgs.Empty);
            });
        });

        _connection.On<string>("NotificationRead", id =>
        {
            if (!Guid.TryParse(id, out var guid)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var item = Notifications.FirstOrDefault(n => n.Id == guid);
                if (item is not null)
                {
                    item.IsRead = true;
                    UnreadCountChanged?.Invoke(this, EventArgs.Empty);
                }
            });
        });

        try
        {
            await _connection.StartAsync(ct);
            _logger.LogInformation("BackendNotification hub connected");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BackendNotification hub connection failed");
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        if (_connection is null) return;
        await _connection.StopAsync();
        await _connection.DisposeAsync();
        _connection = null;
    }

    /// <inheritdoc/>
    public async Task MarkReadAsync(Guid id)
    {
        if (_connection?.State == HubConnectionState.Connected)
        {
            await _connection.InvokeAsync("MarkRead", id);
        }
        else
        {
            await _http.PostAsync($"/api/notifications/{id}/read", null);
        }
    }

    /// <inheritdoc/>
    public async Task LoadHistoryAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/notifications");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Notifications.Clear();
                foreach (var item in items.EnumerateArray())
                    Notifications.Add(MapToVm(item));
                UnreadCountChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load notification history");
        }
    }

    /// <summary>
    /// Преобразует JSON-элемент уведомления из API в UI-модель.
    /// Локализует заголовок по типу события и извлекает имя папки из поля <c>data.FolderName</c>.
    /// </summary>
    private static BackendNotificationVm MapToVm(JsonElement el)
    {
        var type = el.TryGetProperty("type", out var t) ? t.GetString() ?? "" : "";
        var data = el.TryGetProperty("data", out var d) ? d : default;
        var folderName = data.ValueKind == JsonValueKind.Object && data.TryGetProperty("FolderName", out var fn)
            ? fn.GetString() ?? "папка"
            : "папка";

        var title = type switch
        {
            "access.granted" => "Предоставлен доступ",
            "access.revoked" => "Доступ отозван",
            "share.revoked"  => "Ссылка недействительна",
            "access.lost"    => "Папка удалена",
            _ => "Уведомление"
        };

        return new BackendNotificationVm
        {
            Id = el.TryGetProperty("id", out var idEl) && Guid.TryParse(idEl.GetString(), out var id) ? id : Guid.Empty,
            Type = type,
            Title = title,
            Body = $"«{folderName}»",
            CreatedAt = el.TryGetProperty("createdAt", out var ca) ? ca.GetDateTime() : DateTime.UtcNow,
            IsRead = el.TryGetProperty("isRead", out var ir) && ir.GetBoolean()
        };
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }
}
