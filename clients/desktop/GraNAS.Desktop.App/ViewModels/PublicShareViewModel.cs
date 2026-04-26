using System.Reactive;
using GraNAS.Desktop.App.Services.Api;
using GraNAS.Desktop.Contracts.Sharing;
using ReactiveUI;

namespace GraNAS.Desktop.App.ViewModels;

public class PublicShareViewModel : ViewModelBase
{
  private readonly ISharesApi _sharesApi;

  private string _token = string.Empty;
  private ShareDetailsResponse? _details;
  private bool _isLoading;
  private string? _errorMessage;

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

  public string? ErrorMessage
  {
    get => _errorMessage;
    set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
  }

  public ReactiveCommand<Unit, Unit> LookupCommand { get; }

  public PublicShareViewModel(ISharesApi sharesApi)
  {
    _sharesApi = sharesApi;

    var canLookup = this.WhenAnyValue(x => x.Token, x => x.IsLoading,
      (t, loading) => !string.IsNullOrWhiteSpace(t) && !loading);

    LookupCommand = ReactiveCommand.CreateFromTask(LookupAsync, canLookup);
  }

  private async Task LookupAsync()
  {
    ErrorMessage = null;
    Details = null;
    IsLoading = true;
    try
    {
      Details = await _sharesApi.GetShareDetailsAsync(Token.Trim());
    }
    catch (ApiException ex)
    {
      ErrorMessage = ex.StatusCode == 404
        ? "Ссылка не найдена или истекла."
        : ex.Message;
    }
    catch (Exception ex)
    {
      ErrorMessage = ex.Message;
    }
    finally
    {
      IsLoading = false;
    }
  }
}
