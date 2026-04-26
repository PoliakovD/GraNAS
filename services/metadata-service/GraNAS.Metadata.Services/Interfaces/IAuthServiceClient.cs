using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraNAS.Metadata.Services.Interfaces;

public record UserInfo(Guid Id, string Email);

public interface IAuthServiceClient
{
  Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default);
  Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default);
}
