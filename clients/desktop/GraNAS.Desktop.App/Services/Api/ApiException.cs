using GraNAS.Desktop.Contracts.Common;

namespace GraNAS.Desktop.App.Services.Api;

public class ApiException : Exception
{
  public int StatusCode { get; }
  public ErrorResponse? Error { get; }

  public ApiException(int statusCode, ErrorResponse? error)
    : base(error?.ErrorDescription ?? error?.Error ?? $"HTTP {statusCode}")
  {
    StatusCode = statusCode;
    Error = error;
  }
}
