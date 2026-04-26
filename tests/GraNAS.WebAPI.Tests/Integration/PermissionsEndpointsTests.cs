using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using GraNAS.Metadata.DAL;
using GraNAS.Metadata.Models.DTO;
using GraNAS.Metadata.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GraNAS.WebAPI.Tests.Integration;

public class PermissionsEndpointsTests : IClassFixture<MetadataWebApplicationFactory>
{
    private readonly MetadataWebApplicationFactory _factory;

    public PermissionsEndpointsTests(MetadataWebApplicationFactory factory)
    {
        _factory = factory;
    }

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

    private void SetupAuthStub(string email, Guid userId)
    {
        _factory.AuthClientMock
            .Setup(c => c.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfo(userId, email));
    }

    private void SetupAuthStubNotFound(string email)
    {
        _factory.AuthClientMock
            .Setup(c => c.GetUserByEmailAsync(email, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserInfo?)null);
    }

    // ──────────────── POST /api/folders/{id}/permissions ────────────────

    [Fact]
    public async Task Grant_AsOwner_Returns201WithPermissionResponse()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);

        var folder = await CreateFolderAsync(clientA, "GrantRoot");
        SetupAuthStub("b@test.com", userB);

        var resp = await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "b@test.com", accessLevel = "View" });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.NotNull(body);
        Assert.Equal(userB, body!.UserId);
    }

    [Fact]
    public async Task Grant_AsNonOwner_Returns404FolderNotFound()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "NotMineFolder");
        SetupAuthStub("c@test.com", Guid.NewGuid());

        var resp = await clientB.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "c@test.com", accessLevel = "View" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("folder_not_found", err!.Error);
    }

    [Fact]
    public async Task Grant_UnknownEmail_Returns404UserNotFound()
    {
        var ownerId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        var folder = await CreateFolderAsync(client, "EmailNotFoundFolder");
        SetupAuthStubNotFound("ghost@nowhere.com");

        var resp = await client.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "ghost@nowhere.com", accessLevel = "View" });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("user_not_found", err!.Error);
    }

    [Fact]
    public async Task Grant_UpdatesExisting_Returns201Again()
    {
        var ownerId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        var folder = await CreateFolderAsync(client, "UpsertFolder");
        SetupAuthStub("target@test.com", targetId);

        // First grant: View
        var r1 = await client.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "target@test.com", accessLevel = "View" });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);

        // Second grant: Full (upsert)
        var r2 = await client.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "target@test.com", accessLevel = "Full" });
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        var body2 = await r2.Content.ReadFromJsonAsync<PermissionResponse>();
        Assert.Equal("Full", body2!.AccessLevel.ToString());

        // Verify DB has single entry with Full
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MetadataDbContext>();
        var perms = await db.Permissions
            .Where(p => p.FolderId == folder.Id && p.UserId == targetId)
            .ToListAsync();
        Assert.Single(perms);
        Assert.Equal(GraNAS.Metadata.Models.AccessLevel.Full, perms[0].AccessLevel);
    }

    // ──────────────── GET /api/folders (shared visibility) ────────────────

    [Fact]
    public async Task SecondUser_WithView_SeesFolderInList_WithAccessLevelView()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "ViewableFolder");
        SetupAuthStub("viewuser@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "viewuser@test.com", accessLevel = "View" });

        var listResp = await clientB.GetAsync("/api/folders");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var list = await listResp.Content.ReadFromJsonAsync<FolderResponse[]>();
        var shared = Array.Find(list!, f => f.Id == folder.Id);
        Assert.NotNull(shared);
        Assert.Equal("View", shared!.AccessLevel.ToString());
        Assert.Equal(ownerA, shared.OwnerId);
    }

    [Fact]
    public async Task SecondUser_WithView_CreateSubfolder_Returns404()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "ViewOnlyParent");
        SetupAuthStub("viewer@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "viewer@test.com", accessLevel = "View" });

        // userB with View tries to create a subfolder — should be denied
        var resp = await clientB.PostAsJsonAsync("/api/folders",
            new { name = "SubfolderByViewer", parentFolderId = folder.Id });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("parent_folder_not_found", err!.Error);
    }

    [Fact]
    public async Task SecondUser_WithFull_CreateSubfolder_Returns201_OwnerIsSelf()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "FullAccessParent");
        SetupAuthStub("fulluser@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "fulluser@test.com", accessLevel = "Full" });

        var resp = await clientB.PostAsJsonAsync("/api/folders",
            new { name = "SubByFullUser", parentFolderId = folder.Id });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var sub = await resp.Content.ReadFromJsonAsync<FolderResponse>();
        Assert.Equal(userB, sub!.OwnerId); // new subfolder owned by userB
        Assert.Equal(folder.Id, sub.ParentFolderId);
    }

    [Fact]
    public async Task SecondUser_WithFull_CannotDeleteSharedRootFolder_Returns403()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "FullButCantDelete");
        SetupAuthStub("fulldelete@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "fulldelete@test.com", accessLevel = "Full" });

        var resp = await clientB.DeleteAsync($"/api/folders/{folder.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    // ──────────────── DELETE /api/folders/{id}/permissions/{userId} ────────────────

    [Fact]
    public async Task Revoke_AsOwner_Returns200_SecondUserNoLongerSeesFolder()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "RevokeFolder");
        SetupAuthStub("revoke@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "revoke@test.com", accessLevel = "View" });

        // Confirm userB sees it
        var before = await clientB.GetAsync("/api/folders");
        var listBefore = await before.Content.ReadFromJsonAsync<FolderResponse[]>();
        Assert.Contains(listBefore!, f => f.Id == folder.Id);

        // Revoke
        var revokeResp = await clientA.DeleteAsync($"/api/folders/{folder.Id}/permissions/{userB}");
        Assert.Equal(HttpStatusCode.OK, revokeResp.StatusCode);

        // Confirm userB no longer sees it
        var after = await clientB.GetAsync("/api/folders");
        var listAfter = await after.Content.ReadFromJsonAsync<FolderResponse[]>();
        Assert.DoesNotContain(listAfter!, f => f.Id == folder.Id);
    }

    [Fact]
    public async Task Revoke_AsNonOwner_Returns404FolderNotFound()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var userC = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientC = ClientFor(userC);

        var folder = await CreateFolderAsync(clientA, "NonOwnerRevokeFolder");

        var resp = await clientC.DeleteAsync($"/api/folders/{folder.Id}/permissions/{userB}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("folder_not_found", err!.Error);
    }

    [Fact]
    public async Task Revoke_NonExistentPermission_Returns404PermissionNotFound()
    {
        var ownerId = Guid.NewGuid();
        var randomUserId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        var folder = await CreateFolderAsync(client, "NoPermFolder");

        var resp = await client.DeleteAsync($"/api/folders/{folder.Id}/permissions/{randomUserId}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("permission_not_found", err!.Error);
    }

    [Fact]
    public async Task DeletingOwnedFolder_CascadesPermissions()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);

        var folder = await CreateFolderAsync(clientA, "CascadeFolder");
        SetupAuthStub("cascade@test.com", userB);

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "cascade@test.com", accessLevel = "View" });

        // Verify permission exists in DB
        using var scopeBefore = _factory.Services.CreateScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<MetadataDbContext>();
        Assert.True(await dbBefore.Permissions.AnyAsync(p => p.FolderId == folder.Id));

        // Delete the folder
        await clientA.DeleteAsync($"/api/folders/{folder.Id}");

        // Verify permissions cascaded
        using var scopeAfter = _factory.Services.CreateScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<MetadataDbContext>();
        Assert.False(await dbAfter.Permissions.AnyAsync(p => p.FolderId == folder.Id));
    }

    // ──────────────── GET /api/folders/{id}/permissions ────────────────

    [Fact]
    public async Task ListPermissions_AsOwner_EmptyFolder_Returns200EmptyArray()
    {
        var ownerId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        var folder = await CreateFolderAsync(client, "EmptyPermsFolder");

        var resp = await client.GetAsync($"/api/folders/{folder.Id}/permissions");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<PermissionResponse[]>();
        Assert.NotNull(list);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task ListPermissions_AsOwner_WithGrantedUser_Returns200WithEmailAndLevel()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);

        var folder = await CreateFolderAsync(clientA, "ListPermsFolder");
        SetupAuthStub("listed@test.com", userB);
        SetupAuthStubById(userB, "listed@test.com");

        await clientA.PostAsJsonAsync($"/api/folders/{folder.Id}/permissions",
            new { email = "listed@test.com", accessLevel = "View" });

        var resp = await clientA.GetAsync($"/api/folders/{folder.Id}/permissions");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<PermissionResponse[]>();
        Assert.NotNull(list);
        Assert.Single(list!);
        Assert.Equal(userB, list![0].UserId);
        Assert.Equal("listed@test.com", list[0].Email);
        Assert.Equal("View", list[0].AccessLevel.ToString());
    }

    [Fact]
    public async Task ListPermissions_AsNonOwner_Returns404()
    {
        var ownerA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var clientA = ClientFor(ownerA);
        var clientB = ClientFor(userB);

        var folder = await CreateFolderAsync(clientA, "ForbiddenListFolder");

        var resp = await clientB.GetAsync($"/api/folders/{folder.Id}/permissions");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("folder_not_found", err!.Error);
    }

    [Fact]
    public async Task ListPermissions_WithoutAuth_Returns401()
    {
        var ownerId = Guid.NewGuid();
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        var resp = await client.GetAsync($"/api/folders/{Guid.NewGuid()}/permissions");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private void SetupAuthStubById(Guid userId, string email)
    {
        _factory.AuthClientMock
            .Setup(c => c.GetUserByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserInfo(userId, email));
    }
}
