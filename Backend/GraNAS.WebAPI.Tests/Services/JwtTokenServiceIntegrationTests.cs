using System;
using System.Threading.Tasks;
using FluentAssertions;
using GraNAS.Models;
using GraNAS.WebAPI.DAL;
using GraNAS.WebAPI.Services.Implementations;
using GraNAS.WebAPI.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.EntityFrameworkCore;

namespace GraNAS.WebAPI.Tests.Services;

/// <summary>
/// Интеграционные тесты для JwtTokenService.
/// Проверяют реальное поведение с БД, без моков.
/// </summary>
[Collection("Sequential")]
public class JwtTokenServiceIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly AppDbContext _context;
    private readonly ITokenService _tokenService;

    public JwtTokenServiceIntegrationTests(TestWebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _context = factory.CreateContext();
        var scope = factory.Services.CreateScope();
        _tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
    }

    [Fact]
    public async Task GenerateTokensAsync_ShouldCreateValidAccessTokenAndRefreshToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"test{Guid.NewGuid()}@test.com",
            PasswordHash = "hash",
            IsAdmin = false,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _tokenService.GenerateTokensAsync(user);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.ExpiresIn.Should().Be(900); // 15 минут в секундах (из appsettings.test.json)

        // Проверим, что refresh token сохранён в БД
        var refreshTokenInDb = await _context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == result.RefreshToken);
        refreshTokenInDb.Should().NotBeNull();
        refreshTokenInDb!.UserId.Should().Be(user.Id);
        refreshTokenInDb.Revoked.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokensAsync_InvalidToken_ReturnsNull()
    {
        // Act
        var result = await _tokenService.RefreshTokensAsync("invalid_token");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RevokeAllUserRefreshTokensAsync_ShouldRevokeAllTokens()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"revokeall_{Guid.NewGuid()}@test.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();

        for (int i = 0; i < 3; i++)
        {
            await _tokenService.GenerateTokensAsync(user);
        }

        // Act
        await _tokenService.RevokeAllUserRefreshTokensAsync(user.Id);

        // Assert
        var activeTokens = await _context.RefreshTokens
            .Where(r => r.UserId == user.Id && r.Revoked == null)
            .ToListAsync();

        activeTokens.Should().BeEmpty();
    }
    [Fact]
    public async Task RefreshTokensAsync_RevokedRefreshToken_ReturnsNull()
    {
      // Arrange: создаём пользователя и получаем refresh token
      var user = new User
      {
        Id = Guid.NewGuid(),
        Email = $"revoked_{Guid.NewGuid()}@test.com",
        PasswordHash = "hash",
        IsAdmin = false,
        CreatedAt = DateTime.UtcNow
      };

      await _context.Users.AddAsync(user);
      await _context.SaveChangesAsync();

      var firstTokens = await _tokenService.GenerateTokensAsync(user);
      var refreshToken = firstTokens.RefreshToken;

      // Act 1: первый раз используем refresh token (должно сработать)
      var secondResult = await _tokenService.RefreshTokensAsync(refreshToken);

      await Task.Delay(300);

      secondResult.Should().NotBeNull();
      secondResult!.AccessToken.Should().NotBe(firstTokens.AccessToken);

      // Act 2: повторное использование ТОГО ЖЕ самого refresh token (уже отозванного)
      var thirdResult = await _tokenService.RefreshTokensAsync(refreshToken); // тот же токен

      // Assert: должен вернуть null, потому что refresh token уже использован и отозван
      thirdResult.Should().BeNull();

      // Дополнительно: проверим в БД, что токен отмечен как отозванный
      var revokedTokenInDb = await _context.RefreshTokens
        .FirstOrDefaultAsync(r => r.Token == refreshToken);

      revokedTokenInDb.Should().NotBeNull();
      revokedTokenInDb!.Revoked.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshTokensAsync_DifferentRefreshTokens_ShouldGenerateDifferentAccessTokens()
    {
      // Arrange: создаём пользователя и генерируем два НЕЗАВИСИМЫХ сеанса (два refresh token)
      var user = new User
      {
        Id = Guid.NewGuid(),
        Email = $"different_rt_{Guid.NewGuid()}@test.com",
        PasswordHash = "hash",
        IsAdmin = false,
        CreatedAt = DateTime.UtcNow
      };

      await _context.Users.AddAsync(user);
      await _context.SaveChangesAsync();

      // Генерируем первый refresh token (сессия 1)
      var firstTokens = await _tokenService.GenerateTokensAsync(user);
      var firstRefreshToken = firstTokens.RefreshToken;

      // Генерируем второй refresh token (сессия 2) — например, с другого устройства
      var secondTokens = await _tokenService.GenerateTokensAsync(user);
      var secondRefreshToken = secondTokens.RefreshToken;

      // Убедимся, что refresh токены разные
      firstRefreshToken.Should().NotBe(secondRefreshToken, "Два разных сеанса должны иметь разные refresh токены");

      // Act: обновляем access token для каждого сеанса
      var resultFromFirstRefresh = await _tokenService.RefreshTokensAsync(firstRefreshToken);
      var resultFromSecondRefresh = await _tokenService.RefreshTokensAsync(secondRefreshToken);

      // Assert: проверяем, что access токены тоже разные
      resultFromFirstRefresh.Should().NotBeNull();
      resultFromSecondRefresh.Should().NotBeNull();

      var accessFromFirst = resultFromFirstRefresh!.AccessToken;
      var accessFromSecond = resultFromSecondRefresh!.AccessToken;

      accessFromFirst.Should().NotBe(accessFromSecond,
        "Разные сессии (разные refresh токены) не должны генерировать одинаковые access токены");
    }
}
