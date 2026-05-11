namespace GraNAS.Desktop.App.Models;

/// <summary>UI-модель уведомления от backend, отображаемого в колокольчике на панели.</summary>
public sealed class BackendNotificationVm
{
    public Guid Id { get; set; }
    /// <summary>Тип события: <c>access.granted</c>, <c>access.revoked</c>, <c>share.revoked</c>, <c>access.lost</c>.</summary>
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool IsRead { get; set; }
}
