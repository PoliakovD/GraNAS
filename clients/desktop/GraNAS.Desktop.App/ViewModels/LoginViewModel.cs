using System.Reactive;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Auth;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class LoginViewModel : ViewModelBase
{
  private readonly IAuthApi _authApi;
  private readonly IAuthSession _session;
  private readonly INotificationService _notifications;

  private string _email = string.Empty;
  private string _password = string.Empty;
  private bool _isLoading;

  public string Email
  {
    get => _email;
    set => this.RaiseAndSetIfChanged(ref _email, value);
  }

  public string Password
  {
    get => _password;
    set => this.RaiseAndSetIfChanged(ref _password, value);
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  public ReactiveCommand<Unit, Unit> SignInCommand { get; }
  public ReactiveCommand<Unit, Unit> GoToRegisterCommand { get; }

  public event EventHandler? NavigateToRegister;

  public LoginViewModel(IAuthApi authApi, IAuthSession session, INotificationService notifications)
  {
    _authApi = authApi;
    _session = session;
    _notifications = notifications;

    var canSignIn = this.WhenAnyValue(
      x => x.Email, x => x.Password, x => x.IsLoading,
      (e, p, loading) => !string.IsNullOrWhiteSpace(e) && !string.IsNullOrWhiteSpace(p) && !loading);

    SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
    GoToRegisterCommand = ReactiveCommand.Create(() => NavigateToRegister?.Invoke(this, EventArgs.Empty));
  }

  private async Task SignInAsync()
  {
    IsLoading = true;
    try
    {
      var tokens = await _authApi.LoginAsync(new LoginRequest { Email = Email, Password = Password });
      if (tokens is null)
      {
        _notifications.Error("Неверный email или пароль.");
        return;
      }
      await _session.SignInAsync(tokens);
    }
    catch (ApiException ex)
    {
      _notifications.Error(ex.Error?.ErrorDescription ?? ex.Message);
    }
    catch
    {
      _notifications.Error("Нет соединения с сервером. Проверьте подключение.");
    }
    finally
    {
      IsLoading = false;
    }
  }
}
