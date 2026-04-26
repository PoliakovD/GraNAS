using System.Reactive;
using System.Reactive.Linq;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.App.Services.Auth;
using GraNAS.Desktop.Contracts.Auth;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class LoginViewModel : ViewModelBase
{
  private readonly IAuthApi _authApi;
  private readonly IAuthSession _session;

  private string _email = string.Empty;
  private string _password = string.Empty;
  private string? _errorMessage;
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

  public string? ErrorMessage
  {
    get => _errorMessage;
    set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  public ReactiveCommand<Unit, Unit> SignInCommand { get; }
  public ReactiveCommand<Unit, Unit> GoToRegisterCommand { get; }

  public event EventHandler? NavigateToRegister;

  public LoginViewModel(IAuthApi authApi, IAuthSession session)
  {
    _authApi = authApi;
    _session = session;

    var canSignIn = this.WhenAnyValue(
      x => x.Email, x => x.Password, x => x.IsLoading,
      (e, p, loading) => !string.IsNullOrWhiteSpace(e)
                      && !string.IsNullOrWhiteSpace(p)
                      && !loading);

    SignInCommand = ReactiveCommand.CreateFromTask(SignInAsync, canSignIn);
    GoToRegisterCommand = ReactiveCommand.Create(() => NavigateToRegister?.Invoke(this, EventArgs.Empty));
  }

  private async Task SignInAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      var tokens = await _authApi.LoginAsync(new LoginRequest { Email = Email, Password = Password });
      if (tokens is null)
      {
        ErrorMessage = "Неверный email или пароль.";
        return;
      }
      await _session.SignInAsync(tokens);
    }
    catch (ApiException ex)
    {
      ErrorMessage = ex.Error?.ErrorDescription ?? ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }
}
