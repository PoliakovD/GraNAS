using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using GraNAS.Metadata.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace GraNAS.Metadata.API.Infrastructure;

public class AuthServiceClient : IAuthServiceClient
{
  private readonly HttpClient _http;
  private readonly IHttpContextAccessor _accessor;

  public AuthServiceClient(HttpClient http, IHttpContextAccessor accessor)
  {
    _http = http;
    _accessor = accessor;
  }

  public async Task<UserInfo?> GetUserByEmailAsync(string email, CancellationToken ct = default)
  {
    var request = new HttpRequestMessage(
      HttpMethod.Get,
      $"api/internal/users/by-email/{Uri.EscapeDataString(email)}");

    var authorization = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
    if (!string.IsNullOrEmpty(authorization))
      request.Headers.TryAddWithoutValidation("Authorization", authorization);

    var response = await _http.SendAsync(request, ct);

    if (response.StatusCode == HttpStatusCode.NotFound)
      return null;

    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<UserInfo>(ct);
  }
}
