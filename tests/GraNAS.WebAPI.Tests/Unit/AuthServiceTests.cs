using GraNAS.Auth.Models;
using GraNAS.Auth.Models.DTO;
using GraNAS.Auth.Models.Repositories;
using GraNAS.Auth.Services.Implementations;
using GraNAS.Auth.Services.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GraNAS.WebAPI.Tests.Unit;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository>         _userRepo     = new();
    private readonly Mock<IUserSettingsRepository> _userSettings = new();
    private readonly Mock<IPasswordHasher>         _hasher       = new();
    private readonly Mock<ITokenService>           _tokenService = new();
    private readonly AuthService                   _sut;

    public AuthServiceTests()
    {
        _userSettings.Setup(r => r.UpsertAsync(It.IsAny<UserSettings>(), default)).Returns(Task.CompletedTask);
        _sut = new AuthService(_userRepo.Object, _userSettings.Object, _hasher.Object, _tokenService.Object, NullLogger<AuthService>.Instance);
    }

    // ──────────────────────────────── RegisterAsync ────────────────────────────────

    [Theory]
    [InlineData("abc")]      // < 6 символов
    [InlineData("abcdef")]   // нет заглавной + нет цифры
    [InlineData("ABCDEF")]   // нет строчной + нет цифры
    [InlineData("ABCdef")]   // нет цифры
    [InlineData("ABC123")]   // нет строчной
    [InlineData("abc123")]   // нет заглавной
    public async Task Register_WeakPassword_ReturnsWeakPasswordError(string password)
    {
        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Email    = "test@example.com",
            Password = password
        });

        Assert.Equal(RegisterError.WeakPassword, result.Error);
        Assert.Null(result.Response);

        // До репозитория доходить не должно
        _userRepo.Verify(r => r.EmailExistsAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Register_EmailAlreadyExists_ReturnsEmailAlreadyExistsError()
    {
        _userRepo.Setup(r => r.EmailExistsAsync("dup@example.com")).ReturnsAsync(true);

        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Email    = "dup@example.com",
            Password = "ValidPass1"
        });

        Assert.Equal(RegisterError.EmailAlreadyExists, result.Error);
        Assert.Null(result.Response);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Register_ValidRequest_CreatesUserAndReturnsSuccess()
    {
        const string email    = "new@example.com";
        const string password = "ValidPass1";

        _userRepo.Setup(r => r.EmailExistsAsync(email)).ReturnsAsync(false);
        _hasher.Setup(h => h.HashPassword(password)).Returns("hashed_pw");
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

        var result = await _sut.RegisterAsync(new RegisterRequest
        {
            Email    = email,
            Password = password
        });

        Assert.Equal(RegisterError.None, result.Error);
        Assert.NotNull(result.Response);
        Assert.NotEqual(Guid.Empty, result.Response!.UserId);
        Assert.Equal("Registration successful.", result.Response.Message);

        _userRepo.Verify(r => r.CreateAsync(It.Is<User>(u =>
            u.Email        == email    &&
            u.PasswordHash == "hashed_pw" &&
            u.IsAdmin      == false
        )), Times.Once);
    }

    // ──────────────────────────────── LoginAsync ────────────────────────────────

    [Fact]
    public async Task Login_UserNotFound_ReturnsNull()
    {
        _userRepo.Setup(r => r.GetByEmailAsync("ghost@example.com"))
                 .ReturnsAsync((User?)null);

        var result = await _sut.LoginAsync(new LoginRequest
        {
            Email    = "ghost@example.com",
            Password = "ValidPass1"
        });

        Assert.Null(result);
        _tokenService.Verify(t => t.GenerateTokensAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsNull()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash" };
        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword("wrong", "hash")).Returns(false);

        var result = await _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "wrong"
        });

        Assert.Null(result);
        _tokenService.Verify(t => t.GenerateTokensAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenResponse()
    {
        var user = new User { Id = Guid.NewGuid(), Email = "user@example.com", PasswordHash = "hash" };
        var tokens = new TokenResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 900 };

        _userRepo.Setup(r => r.GetByEmailAsync(user.Email)).ReturnsAsync(user);
        _hasher.Setup(h => h.VerifyPassword("ValidPass1", "hash")).Returns(true);
        _tokenService.Setup(t => t.GenerateTokensAsync(user)).ReturnsAsync(tokens);

        var result = await _sut.LoginAsync(new LoginRequest
        {
            Email    = user.Email,
            Password = "ValidPass1"
        });

        Assert.NotNull(result);
        Assert.Equal("access",  result!.AccessToken);
        Assert.Equal("refresh", result.RefreshToken);
    }

    // ──────────────────────────────── RefreshAsync ────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_DelegatesToTokenServiceAndReturnsTokens()
    {
        var tokens = new TokenResponse { AccessToken = "new_access", RefreshToken = "new_refresh", ExpiresIn = 900 };
        _tokenService.Setup(t => t.RefreshTokensAsync("old_token")).ReturnsAsync(tokens);

        var result = await _sut.RefreshAsync("old_token");

        Assert.NotNull(result);
        Assert.Equal("new_access", result!.AccessToken);
        _tokenService.Verify(t => t.RefreshTokensAsync("old_token"), Times.Once);
    }

    [Fact]
    public async Task Refresh_InvalidToken_ReturnsNull()
    {
        _tokenService.Setup(t => t.RefreshTokensAsync("bad")).ReturnsAsync((TokenResponse?)null);

        var result = await _sut.RefreshAsync("bad");

        Assert.Null(result);
    }

    // ──────────────────────────────── LogoutAsync ────────────────────────────────

    [Fact]
    public async Task Logout_AllSessions_True_RevokesAllAndReturnsNone()
    {
        var userId = Guid.NewGuid();
        _tokenService.Setup(t => t.RevokeAllUserRefreshTokensAsync(userId)).Returns(Task.CompletedTask);

        var result = await _sut.LogoutAsync(userId, new LogoutRequest { AllSessions = true });

        Assert.Equal(LogoutError.None, result.Error);
        Assert.Equal("All sessions have been terminated.", result.Message);
        _tokenService.Verify(t => t.RevokeAllUserRefreshTokensAsync(userId), Times.Once);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_ValidToken_ReturnsNone()
    {
        var userId = Guid.NewGuid();
        _tokenService.Setup(t => t.RevokeRefreshTokenAsync("valid_rt", userId)).ReturnsAsync(true);

        var result = await _sut.LogoutAsync(userId, new LogoutRequest { RefreshToken = "valid_rt" });

        Assert.Equal(LogoutError.None, result.Error);
        Assert.Equal("Session terminated successfully.", result.Message);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_InvalidOrRevokedToken_ReturnsInvalidToken()
    {
        var userId = Guid.NewGuid();
        _tokenService.Setup(t => t.RevokeRefreshTokenAsync("bad_rt", userId)).ReturnsAsync(false);

        var result = await _sut.LogoutAsync(userId, new LogoutRequest { RefreshToken = "bad_rt" });

        Assert.Equal(LogoutError.InvalidToken, result.Error);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task Logout_NoRefreshToken_NoAllSessions_ReturnsMissingParameters()
    {
        var result = await _sut.LogoutAsync(Guid.NewGuid(), new LogoutRequest());

        Assert.Equal(LogoutError.MissingParameters, result.Error);
        Assert.Null(result.Message);
    }

    [Fact]
    public async Task Logout_AllSessionsFalse_NoRefreshToken_ReturnsMissingParameters()
    {
        // AllSessions = false не является true → не проходит первый if, RefreshToken пуст → MissingParameters
        var result = await _sut.LogoutAsync(Guid.NewGuid(), new LogoutRequest { AllSessions = false });

        Assert.Equal(LogoutError.MissingParameters, result.Error);
    }
}
