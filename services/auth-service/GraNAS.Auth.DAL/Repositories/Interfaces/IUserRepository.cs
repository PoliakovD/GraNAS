using GraNAS.Models;

namespace GraNAS.WebAPI.DAL.Repositories.Interfaces;

public interface IUserRepository
{
  Task<User?> GetByEmailAsync(string email);
  Task<User?> GetByIdAsync(Guid id);
  Task CreateAsync(User user);
  Task<bool> EmailExistsAsync(string email);
}
