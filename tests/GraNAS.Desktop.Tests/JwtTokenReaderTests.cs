using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using GraNAS.Desktop.App.Services.Auth;
using Microsoft.IdentityModel.Tokens;

namespace GraNAS.Desktop.Tests;

public class JwtTokenReaderTests
{
  private static string BuildJwt(Guid userId, string email, bool isAdmin = false)
  {
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-must-be-32-chars!"));
    var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var claims = new[]
    {
      new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
      new Claim(JwtRegisteredClaimNames.Email, email),
      new Claim("is_admin", isAdmin.ToString().ToLower())
    };

    var token = new JwtSecurityToken(
      issuer: "test",
      audience: "test",
      claims: claims,
      expires: DateTime.UtcNow.AddHours(1),
      signingCredentials: creds);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }

  [Fact]
  public void Read_ValidToken_ExtractsUserIdEmailIsAdmin()
  {
    var userId = Guid.NewGuid();
    const string email = "user@example.com";
    var token = BuildJwt(userId, email, isAdmin: false);

    var (gotId, gotEmail, gotIsAdmin) = JwtTokenReader.Read(token);

    gotId.Should().Be(userId);
    gotEmail.Should().Be(email);
    gotIsAdmin.Should().BeFalse();
  }

  [Fact]
  public void Read_AdminToken_IsAdminTrue()
  {
    var token = BuildJwt(Guid.NewGuid(), "admin@test.com", isAdmin: true);

    var (_, _, isAdmin) = JwtTokenReader.Read(token);

    isAdmin.Should().BeTrue();
  }

  [Fact]
  public void Read_MissingSubClaim_ReturnsEmptyGuid()
  {
    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-secret-key-must-be-32-chars!"));
    var token = new JwtSecurityToken(
      claims: [new Claim("email", "x@y.com")],
      signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
    var tokenStr = new JwtSecurityTokenHandler().WriteToken(token);

    var (userId, _, _) = JwtTokenReader.Read(tokenStr);

    userId.Should().Be(Guid.Empty);
  }
}
