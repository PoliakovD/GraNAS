using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GraNAS.Signaling.Models.DTO;
using GraNAS.Signaling.Models.Enums;

namespace GraNAS.WebAPI.Tests.Integration;

public class DevicesControllerTests : IClassFixture<SignalingWebApplicationFactory>
{
    private readonly SignalingWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DevicesControllerTests(SignalingWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_WithoutJwt_Returns401()
    {
        var req = new { deviceId = Guid.NewGuid(), deviceName = "TestPC", platform = "Windows" };
        var resp = await _client.PostAsJsonAsync("/api/devices", req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Post_ValidDevice_CreatesAndReturns200()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);

        var deviceId = Guid.NewGuid();
        var resp = await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "MyPC", platform = "Windows" });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<DeviceResponse>();
        Assert.NotNull(result);
        Assert.Equal(deviceId, result!.DeviceId);
        Assert.Equal("MyPC", result.DeviceName);
        Assert.Equal(DevicePlatform.Windows, result.Platform);
    }

    [Fact]
    public async Task Post_SameDeviceIdSameUser_UpdatesAndReturns200()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);
        var deviceId = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "OldName", platform = "Windows" });

        var resp = await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "NewName", platform = "Linux" });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<DeviceResponse>();
        Assert.Equal("NewName", result!.DeviceName);
        Assert.Equal(DevicePlatform.Linux, result.Platform);
    }

    [Fact]
    public async Task Post_SameDeviceIdDifferentUser_Returns409()
    {
        var deviceId = Guid.NewGuid();

        await WithJwt(Guid.NewGuid()).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "User1PC", platform = "Windows" });

        var resp = await WithJwt(Guid.NewGuid()).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "User2PC", platform = "Windows" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOnlyOwnDevices()
    {
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await WithJwt(user1).PostAsJsonAsync("/api/devices",
            new { deviceId = Guid.NewGuid(), deviceName = $"U1-{Guid.NewGuid():N}", platform = "Windows" });
        await WithJwt(user2).PostAsJsonAsync("/api/devices",
            new { deviceId = Guid.NewGuid(), deviceName = $"U2-{Guid.NewGuid():N}", platform = "MacOS" });

        var resp = await WithJwt(user2).GetAsync("/api/devices");
        resp.EnsureSuccessStatusCode();
        var devices = await resp.Content.ReadFromJsonAsync<List<DeviceResponse>>();

        Assert.NotNull(devices);
        Assert.All(devices!, d => Assert.Equal(DevicePlatform.MacOS, d.Platform));
    }

    private HttpClient WithJwt(Guid userId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.GenerateJwt(userId));
        return c;
    }
}
