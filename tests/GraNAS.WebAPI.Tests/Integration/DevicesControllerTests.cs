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

    // ─────────────────────── PATCH /api/devices/{deviceId} ───────────────────────

    [Fact]
    public async Task Patch_RenamesDevice_Returns200()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);
        var deviceId = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "OriginalName", platform = "Windows" });

        var resp = await client.PatchAsJsonAsync($"/api/devices/{deviceId}",
            new { deviceName = "RenamedPC" });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<DeviceResponse>();
        Assert.Equal("RenamedPC", result!.DeviceName);
    }

    [Fact]
    public async Task Patch_OtherUsersDevice_Returns403()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await WithJwt(owner).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "OwnerPC", platform = "Windows" });

        var resp = await WithJwt(other).PatchAsJsonAsync($"/api/devices/{deviceId}",
            new { deviceName = "Hacked" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_DuplicateNameSameUser_Returns409()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/devices",
            new { deviceId = device1, deviceName = $"PC-A-{Guid.NewGuid():N}", platform = "Windows" });
        await client.PostAsJsonAsync("/api/devices",
            new { deviceId = device2, deviceName = $"PC-B-{Guid.NewGuid():N}", platform = "Windows" });

        // Rename device2 to the same name as device1
        var d1resp = await client.GetAsync("/api/devices");
        var devices = await d1resp.Content.ReadFromJsonAsync<List<DeviceResponse>>();
        var name1 = devices!.First(d => d.DeviceId == device1).DeviceName;

        var resp = await client.PatchAsJsonAsync($"/api/devices/{device2}",
            new { deviceName = name1 });
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Patch_EmptyDeviceName_Returns400()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);
        var deviceId = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = "ValidName", platform = "Windows" });

        var resp = await client.PatchAsJsonAsync($"/api/devices/{deviceId}",
            new { deviceName = "" });
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ─────────────────────── GET /api/devices/{deviceId}/folders ───────────────────────

    [Fact]
    public async Task GetDeviceFolders_OwnDevice_ReturnsBindings()
    {
        var userId = Guid.NewGuid();
        var client = WithJwt(userId);
        var deviceId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        await client.PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = $"PC-{Guid.NewGuid():N}", platform = "Windows" });
        await client.PostAsync($"/api/devices/{deviceId}/folders/{folderId}", null);

        var resp = await client.GetAsync($"/api/devices/{deviceId}/folders");
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<List<DeviceFolderResponse>>();
        Assert.NotNull(result);
        Assert.Contains(result!, r => r.FolderId == folderId);
    }

    [Fact]
    public async Task GetDeviceFolders_OtherUserDevice_Returns403()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await WithJwt(owner).PostAsJsonAsync("/api/devices",
            new { deviceId, deviceName = $"PC-{Guid.NewGuid():N}", platform = "Windows" });

        var resp = await WithJwt(other).GetAsync($"/api/devices/{deviceId}/folders");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private HttpClient WithJwt(Guid userId)
    {
        var c = _factory.CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _factory.GenerateJwt(userId));
        return c;
    }
}
