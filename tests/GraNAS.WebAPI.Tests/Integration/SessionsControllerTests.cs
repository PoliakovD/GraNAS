using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GraNAS.Signaling.Models.DTO;

namespace GraNAS.WebAPI.Tests.Integration;

public class SessionsControllerTests : IClassFixture<SignalingWebApplicationFactory>
{
    private readonly SignalingWebApplicationFactory _factory;

    public SessionsControllerTests(SignalingWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_WithoutJwt_Returns401()
    {
        var resp = await _factory.CreateClient().GetAsync("/api/sessions");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_NoOnlineSessions_ReturnsEmptyList()
    {
        var resp = await WithJwt(Guid.NewGuid()).GetAsync("/api/sessions");
        resp.EnsureSuccessStatusCode();

        var sessions = await resp.Content.ReadFromJsonAsync<List<ActiveSessionResponse>>();
        Assert.NotNull(sessions);
        Assert.Empty(sessions!);
    }

    [Fact]
    public async Task Delete_WithForeignDeviceId_Returns404()
    {
        var ownerId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        // Register device for owner
        await WithJwt(ownerId).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "OwnerPC", platform = "Windows" });

        // Try to terminate it as another user
        var resp = await WithJwt(Guid.NewGuid()).DeleteAsync($"/api/sessions/{deviceId}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_OwnDeviceNotOnline_Returns204()
    {
        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await WithJwt(userId).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "MyPC", platform = "Windows" });

        // Device registered but no hub connection → no session info → still 204
        var resp = await WithJwt(userId).DeleteAsync($"/api/sessions/{deviceId}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    private HttpClient WithJwt(Guid userId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.GenerateJwt(userId));
        return c;
    }
}
