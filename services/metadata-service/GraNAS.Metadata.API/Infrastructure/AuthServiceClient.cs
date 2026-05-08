using System;
using System.Collections.Generic;
using System.Linq;
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

    ForwardAuthorization(request);

    var response = await _http.SendAsync(request, ct);

    if (response.StatusCode == HttpStatusCode.NotFound)
      return null;

    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<UserInfo>(ct);
  }

  public async Task<UserInfo?> GetUserByIdAsync(Guid id, CancellationToken ct = default)
  {
    var request = new HttpRequestMessage(HttpMethod.Get, $"api/internal/users/{id}");
    ForwardAuthorization(request);

    var response = await _http.SendAsync(request, ct);

    if (response.StatusCode == HttpStatusCode.NotFound)
      return null;

    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<UserInfo>(ct);
  }

  public async Task<IReadOnlyDictionary<Guid, string>> GetUserEmailsAsync(
    IEnumerable<Guid> userIds, CancellationToken ct = default)
  {
    var ids = userIds.Distinct().ToArray();
    if (ids.Length == 0) return new Dictionary<Guid, string>();

    var query = string.Join("&", ids.Select(id => $"ids={id}"));
    var request = new HttpRequestMessage(HttpMethod.Get, $"api/internal/users/batch?{query}");
    ForwardAuthorization(request);

    var response = await _http.SendAsync(request, ct);
    response.EnsureSuccessStatusCode();

    var users = await response.Content.ReadFromJsonAsync<UserInfo[]>(ct);
    return users?.ToDictionary(u => u.Id, u => u.Email)
           ?? (IReadOnlyDictionary<Guid, string>)new Dictionary<Guid, string>();
  }

  private void ForwardAuthorization(HttpRequestMessage request)
  {
    var authorization = _accessor.HttpContext?.Request.Headers["Authorization"].ToString();
    if (!string.IsNullOrEmpty(authorization))
      request.Headers.TryAddWithoutValidation("Authorization", authorization);
  }
}
