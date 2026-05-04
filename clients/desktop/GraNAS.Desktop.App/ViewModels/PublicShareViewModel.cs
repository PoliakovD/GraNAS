using System.Reactive;
using GraNAS.Desktop.App.Services;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Sharing;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class PublicShareViewModel : ViewModelBase
{
  private readonly ISharesApi _sharesApi;
  private readonly INotificationService _notifications;

  private string _token = string.Empty;
  private ShareDetailsResponse? _details;
  private bool _isLoading;

  public string Token
  {
    get => _token;
    set => this.RaiseAndSetIfChanged(ref _token, value);
  }

  public ShareDetailsResponse? Details
  {
    get => _details;
    set => this.RaiseAndSetIfChanged(ref _details, value);
  }

  public bool IsLoading
  {
    get => _isLoading;
    set => this.RaiseAndSetIfChanged(ref _isLoading, value);
  }

  public ReactiveCommand<Unit, Unit> LookupCommand { get; }

  public PublicShareViewModel(ISharesApi sharesApi, INotificationService notifications)
  {
    _sharesApi = sharesApi;
    _notifications = notifications;

    var canLookup = this.WhenAnyValue(x => x.Token, x => x.IsLoading,
      (t, loading) => !string.IsNullOrWhiteSpace(t) && !loading);

    LookupCommand = ReactiveCommand.CreateFromTask(LookupAsync, canLookup);
  }

  private async Task LookupAsync()
  {
    Details = null;
    IsLoading = true;
    try
    {
      Details = await _sharesApi.GetShareDetailsAsync(Token.Trim());
    }
    catch (ApiException ex) when (ex.StatusCode == 404)
    {
      _notifications.Error("Ссылка не найдена или истекла.");
    }
    catch (ApiException ex) when (ex.StatusCode == 410)
    {
      _notifications.Error("Ссылка была отозвана владельцем.");
    }
    catch
    {
      _notifications.Error("Нет соединения с сервером.");
    }
    finally
    {
      IsLoading = false;
    }
  }
}
