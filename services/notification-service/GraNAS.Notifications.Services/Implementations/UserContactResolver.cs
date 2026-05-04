using GraNAS.Notifications.Services.Interfaces;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.Logging;

namespace GraNAS.Notifications.Services.Implementations;

public class UserContactResolver : IUserContactResolver
{
    private readonly AuthServiceClient _authClient;
    private readonly ILogger<UserContactResolver> _logger;

    public UserContactResolver(AuthServiceClient authClient, ILogger<UserContactResolver> logger)
    {
        _authClient = authClient;
        _logger = logger;
    }

    public async Task<UserContact?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var contact = await _authClient.GetContactAsync(userId, ct);
        if (contact is null)
            _logger.LogWarning("UserContact: user {UserId} not found in auth-service", userId);
        return contact;
    }
}
