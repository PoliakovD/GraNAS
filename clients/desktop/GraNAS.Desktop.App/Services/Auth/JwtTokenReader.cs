using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GraNAS.Desktop.App.Services.Auth;

public static class JwtTokenReader
{
  public static (Guid UserId, string Email, bool IsAdmin) Read(string token)
  {
    var handler = new JwtSecurityTokenHandler();
    var jwt = handler.ReadJwtToken(token);

    var sub = jwt.Claims.FirstOrDefault(c =>
      c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub)?.Value;

    var email = jwt.Claims.FirstOrDefault(c =>
      c.Type == ClaimTypes.Email || c.Type == JwtRegisteredClaimNames.Email)?.Value
      ?? string.Empty;

    var isAdmin = jwt.Claims.FirstOrDefault(c => c.Type == "is_admin")?.Value == "true";

    _ = Guid.TryParse(sub, out var userId);
    return (userId, email, isAdmin);
  }
}
