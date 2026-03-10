using BCrypt.Net;
using GraNAS.WebAPI.Services.Interfaces;

namespace GraNAS.WebAPI.Services.Implementations;

  public class BCryptPasswordHasher : IPasswordHasher
  {
    public string HashPassword(string password)
    {
      return BCrypt.Net.BCrypt.HashPassword(password,workFactor: 12);
      }

    public bool VerifyPassword(string password, string hashedPassword)
    {
      return BCrypt.Net.BCrypt.Verify(password, hashedPassword);
    }
  }

