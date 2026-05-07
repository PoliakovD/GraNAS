using GraNAS.Notifications.Services.Models;

namespace GraNAS.Notifications.Services.Interfaces;

public interface IUserContactResolver
{
    Task<UserContact?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
