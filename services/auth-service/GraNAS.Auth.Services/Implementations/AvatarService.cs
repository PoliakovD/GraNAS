using System;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Interfaces;

namespace GraNAS.Auth.Services.Implementations;

/// <summary>Сервис хранения аватаров пользователей в PostgreSQL (BYTEA).</summary>
public class AvatarService : IAvatarService
{
    private readonly IUserRepository _users;

    public AvatarService(IUserRepository users) => _users = users;

    public Task SetAsync(Guid userId, byte[]? bytes, string? contentType, CancellationToken ct)
        => _users.SaveAvatarAsync(userId, bytes, contentType, ct);

    public async Task<AvatarData?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user?.Avatar is null || user.AvatarContentType is null || user.AvatarUpdatedAt is null)
            return null;
        return new AvatarData(user.Avatar, user.AvatarContentType, user.AvatarUpdatedAt.Value);
    }
}
