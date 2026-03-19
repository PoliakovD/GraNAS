using System.Security.Claims;
using GraNAS.Models;
using GraNAS.Models.DTO;
using GraNAS.WebAPI.DAL.Repositories.Interfaces;
using GraNAS.WebAPI.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace GraNAS.WebAPI.Tests.Controllers.AuthController;

 public class AuthControllerTests
    {
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IPasswordHasher> _hasherMock;
        private readonly Mock<ITokenService> _tokenServiceMock;
        private readonly WebAPI.Controllers.AuthController _controller;

        public AuthControllerTests()
        {
            _userRepoMock = new Mock<IUserRepository>();
            _hasherMock = new Mock<IPasswordHasher>();
            _tokenServiceMock = new Mock<ITokenService>();
            _controller = new WebAPI.Controllers.AuthController(_userRepoMock.Object, _hasherMock.Object, _tokenServiceMock.Object);
        }

        [Fact]
        public async Task Register_ValidRequest_ReturnsOkWithUserId()
        {
            // Arrange
            var request = new Models.DTO.RegisterRequest { Email = "test@test.com", Password = "StrongP@ss1" };
            _userRepoMock.Setup(r => r.EmailExistsAsync(request.Email)).ReturnsAsync(false);
            _hasherMock.Setup(h => h.HashPassword(request.Password)).Returns("hashed");
            _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<User>())).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<RegisterResponse>(okResult.Value);
            Assert.NotEqual(Guid.Empty, response.UserId);
            Assert.Equal("Registration successful.", response.Message);
        }

        [Fact]
        public async Task Register_ExistingEmail_ReturnsConflict()
        {
            // Arrange
            var request = new Models.DTO.RegisterRequest { Email = "existing@test.com", Password = "StrongP@ss1" };
            _userRepoMock.Setup(r => r.EmailExistsAsync(request.Email)).ReturnsAsync(true);

            // Act
            var result = await _controller.Register(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(result);
            var error = Assert.IsType<ErrorResponse>(conflictResult.Value);
            Assert.Equal("email_already_exists", error.Error);
        }

        [Fact]
        public async Task Register_WeakPassword_ReturnsBadRequest()
        {
            // Arrange
            var request = new Models.DTO.RegisterRequest { Email = "test@test.com", Password = "weak" };
            _controller.ModelState.AddModelError("Password", "Password must contain at least one uppercase letter, one lowercase letter, and one digit.");

            // Act
            var result = await _controller.Register(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsTokens()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", PasswordHash = "hash" };
            var request = new Models.DTO.LoginRequest { Email = "test@test.com", Password = "pass" };
            var tokens = new TokenResponse { AccessToken = "access", RefreshToken = "refresh", ExpiresIn = 900, TokenType = "bearer" };

            _userRepoMock.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(user);
            _hasherMock.Setup(h => h.VerifyPassword(request.Password, user.PasswordHash)).Returns(true);
            _tokenServiceMock.Setup(t => t.GenerateTokensAsync(user)).ReturnsAsync(tokens);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = okResult.Value; // анонимный тип
            Assert.NotNull(response);
        }

        [Fact]
        public async Task Login_InvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            var user = new User { Id = Guid.NewGuid(), Email = "test@test.com", PasswordHash = "hash" };
            var request = new Models.DTO.LoginRequest { Email = "test@test.com", Password = "wrong" };
            _userRepoMock.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(user);
            _hasherMock.Setup(h => h.VerifyPassword(request.Password, user.PasswordHash)).Returns(false);

            // Act
            var result = await _controller.Login(request);

            // Assert
            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var error = Assert.IsType<ErrorResponse>(unauthResult.Value);
            Assert.Equal("invalid_grant", error.Error);
        }

        [Fact]
        public async Task Refresh_ValidToken_ReturnsNewTokens()
        {
            // Arrange
            var request = new Models.DTO.RefreshRequest { RefreshToken = "valid" };
            var tokens = new TokenResponse { AccessToken = "new_access", RefreshToken = "new_refresh", ExpiresIn = 900 };
            _tokenServiceMock.Setup(t => t.RefreshTokensAsync(request.RefreshToken)).ReturnsAsync(tokens);

            // Act
            var result = await _controller.Refresh(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Refresh_InvalidToken_ReturnsUnauthorized()
        {
            // Arrange
            var request = new Models.DTO.RefreshRequest { RefreshToken = "invalid" };
            _tokenServiceMock.Setup(t => t.RefreshTokensAsync(request.RefreshToken)).ReturnsAsync((TokenResponse)null);

            // Act
            var result = await _controller.Refresh(request);

            // Assert
            var unauthResult = Assert.IsType<UnauthorizedObjectResult>(result);
            var error = Assert.IsType<ErrorResponse>(unauthResult.Value);
            Assert.Equal("invalid_grant", error.Error);
        }

        [Fact]
        public async Task Logout_WithValidToken_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = TestClaimsProvider.GetUser(userId) }
            };
            var request = new LogoutRequest { RefreshToken = "some-token", AllSessions = false };
            _tokenServiceMock.Setup(t => t.RevokeRefreshTokenAsync(request.RefreshToken, userId)).ReturnsAsync(true);

            // Act
            var result = await _controller.Logout(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.NotNull(okResult.Value);
        }

        [Fact]
        public async Task Logout_AllSessions_ReturnsOk()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = TestClaimsProvider.GetUser(userId) }
            };
            var request = new LogoutRequest { AllSessions = true };
            _tokenServiceMock.Setup(t => t.RevokeAllUserRefreshTokensAsync(userId)).Returns(Task.CompletedTask);

            // Act
            var result = await _controller.Logout(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Fact]
        public async Task Logout_InvalidUserId_ReturnsUnauthorized()
        {
            // Arrange
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal() }
            };
            var request = new LogoutRequest();

            // Act
            var result = await _controller.Logout(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
