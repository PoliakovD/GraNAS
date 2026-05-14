using System.Net.Http.Json;
using System.Text.Json;
using GraNAS.Desktop.Contracts.Common;

namespace GraNAS.Desktop.App.Services.Api;

public abstract class ApiBase
{
  private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

  protected readonly HttpClient Http;

  protected ApiBase(HttpClient http) => Http = http;

  protected async Task<T> GetAsync<T>(string url, CancellationToken ct = default)
  {
    var response = await Http.GetAsync(url, ct);
    await EnsureSuccessAsync(response);
    return (await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct))!;
  }

  protected async Task<T> PostAsync<T>(string url, object body, CancellationToken ct = default)
  {
    var response = await Http.PostAsJsonAsync(url, body, JsonOpts, ct);
    await EnsureSuccessAsync(response);
    return (await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct))!;
  }

  protected async Task PostAsync(string url, object body, CancellationToken ct = default)
  {
    var response = await Http.PostAsJsonAsync(url, body, JsonOpts, ct);
    await EnsureSuccessAsync(response);
  }

  protected async Task<T> PatchAsync<T>(string url, object body, CancellationToken ct = default)
  {
    var request = new HttpRequestMessage(HttpMethod.Patch, url)
    {
      Content = JsonContent.Create(body, options: JsonOpts),
    };
    var response = await Http.SendAsync(request, ct);
    await EnsureSuccessAsync(response);
    return (await response.Content.ReadFromJsonAsync<T>(JsonOpts, ct))!;
  }

  protected async Task DeleteAsync(string url, CancellationToken ct = default)
  {
    var response = await Http.DeleteAsync(url, ct);
    await EnsureSuccessAsync(response);
  }

  private static async Task EnsureSuccessAsync(HttpResponseMessage response)
  {
    if (response.IsSuccessStatusCode)
      return;

    ErrorResponse? error = null;
    try { error = await response.Content.ReadFromJsonAsync<ErrorResponse>(); }
    catch { /* ignore parse errors */ }

    throw new ApiException((int)response.StatusCode, error);
  }
}
