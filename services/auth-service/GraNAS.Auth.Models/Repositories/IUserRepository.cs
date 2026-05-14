using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraNAS.Auth.Models.Repositories;

public interface IUserRepository
{
  Task<User?> GetByEmailAsync(string email);
  Task<User?> GetByIdAsync(Guid id);
  Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
  Task CreateAsync(User user);
  Task<bool> EmailExistsAsync(string email);
  Task SaveAvatarAsync(Guid id, byte[]? bytes, string? contentType, CancellationToken ct);
}
