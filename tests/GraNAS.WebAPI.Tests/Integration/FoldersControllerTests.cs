using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using GraNAS.Metadata.DAL;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Shared.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GraNAS.WebAPI.Tests.Integration;

public class FoldersControllerTests : IClassFixture<MetadataWebApplicationFactory>
{
    private readonly MetadataWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FoldersControllerTests(MetadataWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    // ──────────────── helpers ────────────────

    private HttpClient ClientFor(Guid userId)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.GenerateJwt(userId));
        return client;
    }

    private async Task<FolderResponse> CreateFolderAsync(HttpClient client, string name, Guid? parentFolderId = null)
    {
        var resp = await client.PostAsJsonAsync("/api/folders", new { name, parentFolderId });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FolderResponse>())!;
    }

    // ──────────────── POST /api/folders ────────────────

    [Fact]
    public async Task CreateFolder_Root_Returns201WithNullParent()
    {
        var client = ClientFor(Guid.NewGuid());

        var resp = await client.PostAsJsonAsync("/api/folders", new { name = "MyRoot" });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Equal("MyRoot", body!.Name);
        Assert.Null(body.ParentFolderId);
    }

    [Fact]
    public async Task CreateFolder_Subfolder_Returns201WithParentFolderId()
    {
        var client = ClientFor(Guid.NewGuid());
        var root = await CreateFolderAsync(client, "Root");

        var resp = await client.PostAsJsonAsync("/api/folders", new { name = "Child", parentFolderId = root.Id });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Equal(root.Id, body!.ParentFolderId);
    }

    [Fact]
    public async Task CreateFolder_NoAuth_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/folders", new { name = "X" });
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task CreateFolder_ParentNotMine_Returns404()
    {
        var userA = ClientFor(Guid.NewGuid());
        var userB = ClientFor(Guid.NewGuid());
        var aRoot = await CreateFolderAsync(userA, "A-Root");

        var resp = await userB.PostAsJsonAsync("/api/folders",
            new { name = "B-Child", parentFolderId = aRoot.Id });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("parent_folder_not_found", body!.Error);
    }

    [Fact]
    public async Task CreateFolder_ParentDoesNotExist_Returns404()
    {
        var client = ClientFor(Guid.NewGuid());

        var resp = await client.PostAsJsonAsync("/api/folders",
            new { name = "Ghost-Child", parentFolderId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("parent_folder_not_found", body!.Error);
    }

    // ──────────────── DELETE /api/folders/{id} ────────────────

    [Fact]
    public async Task DeleteFolder_OwnRoot_Returns204()
    {
        var client = ClientFor(Guid.NewGuid());
        var root = await CreateFolderAsync(client, "ToDelete");

        var resp = await client.DeleteAsync($"/api/folders/{root.Id}");

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteFolder_Root_CascadesTwoLevelsDeep()
    {
        var userId = Guid.NewGuid();
        var client = ClientFor(userId);
        var root = await CreateFolderAsync(client, "Root");
        var child = await CreateFolderAsync(client, "Child", root.Id);
        var grandchild = await CreateFolderAsync(client, "Grandchild", child.Id);

        var resp = await client.DeleteAsync($"/api/folders/{root.Id}");
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        // Проверяем состояние БД напрямую — именно это гарантирует каскад на уровне FK
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var ids = new[] { root.Id, child.Id, grandchild.Id };
        var remaining = await db.Folders.Where(f => ids.Contains(f.Id)).CountAsync();
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task DeleteFolder_OnlyChild_LeavesRootAndSibling()
    {
        var client = ClientFor(Guid.NewGuid());
        var root = await CreateFolderAsync(client, "Root");
        var child1 = await CreateFolderAsync(client, "Child1", root.Id);
        var child2 = await CreateFolderAsync(client, "Child2", root.Id);

        await client.DeleteAsync($"/api/folders/{child1.Id}");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        Assert.True(await db.Folders.AnyAsync(f => f.Id == root.Id));
        Assert.False(await db.Folders.AnyAsync(f => f.Id == child1.Id));
        Assert.True(await db.Folders.AnyAsync(f => f.Id == child2.Id));
    }

    [Fact]
    public async Task DeleteFolder_AnotherUsersFolder_Returns403()
    {
        var userA = ClientFor(Guid.NewGuid());
        var userB = ClientFor(Guid.NewGuid());
        var aFolder = await CreateFolderAsync(userA, "A-Folder");

        var resp = await userB.DeleteAsync($"/api/folders/{aFolder.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);

        // Папка A не затронута
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        Assert.True(await db.Folders.AnyAsync(f => f.Id == aFolder.Id));
    }

    [Fact]
    public async Task DeleteFolder_NonExistent_Returns404()
    {
        var client = ClientFor(Guid.NewGuid());

        var resp = await client.DeleteAsync($"/api/folders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ──────────────── GET /api/folders ────────────────

    [Fact]
    public async Task GetFolders_NoFolders_Returns200WithEmptyArray()
    {
        var client = ClientFor(Guid.NewGuid());

        var resp = await client.GetAsync("/api/folders");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<FolderResponse[]>();
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task GetFolders_ReturnsFlatListWithParentFolderIds()
    {
        var client = ClientFor(Guid.NewGuid());
        var root = await CreateFolderAsync(client, "ListRoot");
        var child = await CreateFolderAsync(client, "ListChild", root.Id);

        var resp = await client.GetAsync("/api/folders");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var list = await resp.Content.ReadFromJsonAsync<FolderResponse[]>();
        Assert.NotNull(list);
        var rootDto = Array.Find(list!, f => f.Id == root.Id);
        var childDto = Array.Find(list!, f => f.Id == child.Id);
        Assert.NotNull(rootDto);
        Assert.NotNull(childDto);
        Assert.Null(rootDto!.ParentFolderId);
        Assert.Equal(root.Id, childDto!.ParentFolderId);
    }

    // ──────────────── PATCH /api/folders/{id}/touch ────────────────

    [Fact]
    public async Task Touch_AsOwner_Returns204AndSetsLastAccessedAt()
    {
        var ownerId = Guid.NewGuid();
        var client = ClientFor(ownerId);
        var folder = await CreateFolderAsync(client, "TouchTest");

        var resp = await client.PatchAsync($"/api/folders/{folder.Id}/touch", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var updated = await db.Folders.FindAsync(folder.Id);
        Assert.NotNull(updated!.LastAccessedAt);
    }

    [Fact]
    public async Task Touch_UnknownFolder_Returns404()
    {
        var client = ClientFor(Guid.NewGuid());
        var resp = await client.PatchAsync($"/api/folders/{Guid.NewGuid()}/touch", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Touch_Unauthenticated_Returns401()
    {
        var resp = await _client.PatchAsync($"/api/folders/{Guid.NewGuid()}/touch", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetFolders_AfterTouch_LastAccessedAtIsSet()
    {
        var ownerId = Guid.NewGuid();
        var client = ClientFor(ownerId);
        var folder = await CreateFolderAsync(client, "TouchedFolder");

        await client.PatchAsync($"/api/folders/{folder.Id}/touch", null);

        var listResp = await client.GetAsync("/api/folders");
        var list = await listResp.Content.ReadFromJsonAsync<FolderResponse[]>();
        var dto = Array.Find(list!, f => f.Id == folder.Id);
        Assert.NotNull(dto!.LastAccessedAt);
    }
}
