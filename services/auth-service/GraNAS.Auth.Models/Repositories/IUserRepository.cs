using System;
using System.Threading.Tasks;

namespace GraNAS.Auth.Models.Repositories;

public interface IUserRepository
{
  Task<User?> GetByEmailAsync(string email);
  Task<User?> GetByIdAsync(Guid id);
  Task CreateAsync(User user);
  Task<bool> EmailExistsAsync(string email);
}
