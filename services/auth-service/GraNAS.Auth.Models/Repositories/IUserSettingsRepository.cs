namespace GraNAS.Auth.Models.Repositories;

public interface IUserSettingsRepository
{
    Task<UserSettings?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpsertAsync(UserSettings settings, CancellationToken ct = default);
}
