using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace GraNAS.WebAPI.Tests.Controllers.AuthController;

public static class TestClaimsProvider
{
  public static ClaimsPrincipal GetUser(Guid userId)
  {
    var claims = new List<Claim>
    {
      new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
      new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
    };
    var identity = new ClaimsIdentity(claims, "TestAuth");
    return new ClaimsPrincipal(identity);
  }
}
