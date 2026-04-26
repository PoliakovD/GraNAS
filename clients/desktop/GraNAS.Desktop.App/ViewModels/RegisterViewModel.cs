using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Auth;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class RegisterViewModel : ViewModelBase
{
  private readonly IAuthApi _authApi;

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

  public ReactiveCommand<Unit, Unit> RegisterCommand { get; }
  public ReactiveCommand<Unit, Unit> GoToLoginCommand { get; }

  public event EventHandler? NavigateToLogin;
  public event EventHandler? RegistrationSuccess;

  public RegisterViewModel(IAuthApi authApi)
  {
    _authApi = authApi;

    var canRegister = this.WhenAnyValue(
      x => x.Email, x => x.Password, x => x.IsLoading,
      (e, p, loading) => !string.IsNullOrWhiteSpace(e)
                      && p.Length >= 6
                      && !loading);

    RegisterCommand = ReactiveCommand.CreateFromTask(DoRegisterAsync, canRegister);
    GoToLoginCommand = ReactiveCommand.Create(() => NavigateToLogin?.Invoke(this, EventArgs.Empty));
  }

  private async Task DoRegisterAsync()
  {
    ErrorMessage = null;
    IsLoading = true;
    try
    {
      await _authApi.RegisterAsync(new RegisterRequest { Email = Email, Password = Password });
      RegistrationSuccess?.Invoke(this, EventArgs.Empty);
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
