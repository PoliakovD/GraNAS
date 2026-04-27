using System.Reactive.Linq;
using FluentAssertions;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.App.ViewModels;
using GraNAS.Desktop.Contracts.Auth;
using Moq;

namespace GraNAS.Desktop.Tests;

public class LoginViewModelTests
{
  private readonly Mock<IAuthApi> _authApi = new();
  private readonly Mock<IAuthSession> _session = new();
  private readonly Mock<INotificationService> _notifications = new();

  private LoginViewModel CreateVm() => new(_authApi.Object, _session.Object, _notifications.Object);

  [Fact]
  public async Task SignIn_ValidCredentials_CallsSignInAsync()
  {
    var tokens = new TokenResponse { AccessToken = "acc", RefreshToken = "ref", ExpiresIn = 3600 };
    _authApi.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), default))
            .ReturnsAsync(tokens);

    var vm = CreateVm();
    vm.Email = "user@test.com";
    vm.Password = "Password1";

    await vm.SignInCommand.Execute().FirstAsync();

    _session.Verify(s => s.SignInAsync(tokens, default), Times.Once);
    _notifications.Verify(n => n.Error(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
  }

  [Fact]
  public async Task SignIn_InvalidCredentials_ShowsErrorNotification()
  {
    _authApi.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), default))
            .ReturnsAsync((TokenResponse?)null);

    var vm = CreateVm();
    vm.Email = "user@test.com";
    vm.Password = "WrongPass1";

    await vm.SignInCommand.Execute().FirstAsync();

    _notifications.Verify(n => n.Error(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    _session.Verify(s => s.SignInAsync(It.IsAny<TokenResponse>(), default), Times.Never);
  }

  [Fact]
  public async Task SignIn_ApiThrows_ShowsErrorNotification()
  {
    _authApi.Setup(a => a.LoginAsync(It.IsAny<LoginRequest>(), default))
            .ThrowsAsync(new ApiException(429, null));

    var vm = CreateVm();
    vm.Email = "user@test.com";
    vm.Password = "Password1";

    await vm.SignInCommand.Execute().FirstAsync();

    _notifications.Verify(n => n.Error(It.IsAny<string>(), It.IsAny<string>()), Times.Once);
  }

  [Fact]
  public void SignInCommand_DisabledWhenFieldsEmpty()
  {
    var vm = CreateVm();

    bool canExecute = false;
    using var _ = vm.SignInCommand.CanExecute.Subscribe(v => canExecute = v);

    canExecute.Should().BeFalse();
  }
}
