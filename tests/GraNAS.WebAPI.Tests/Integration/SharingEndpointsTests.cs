using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GraNAS.Sharing.DAL;
using GraNAS.Sharing.Models;
using GraNAS.Sharing.Models.DTO;
using GraNAS.Sharing.Services.Interfaces;
using GraNAS.Shared.Models.DTO;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace GraNAS.WebAPI.Tests.Integration;

public class SharingEndpointsTests : IClassFixture<SharingWebApplicationFactory>
{
    private readonly SharingWebApplicationFactory _factory;

    public SharingEndpointsTests(SharingWebApplicationFactory factory)
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

    private HttpClient AnonymousClient() =>
        _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

    private void SetupOwnershipCheck(Guid folderId, Guid ownerId, string folderName = "Test Folder")
    {
        _factory.MetadataClientMock
            .Setup(c => c.GetFolderForOwnerAsync(folderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderInfo(folderId, folderName, ownerId));
    }

    private void SetupOwnershipNotFound(Guid folderId, Guid ownerId)
    {
        _factory.MetadataClientMock
            .Setup(c => c.GetFolderForOwnerAsync(folderId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FolderInfo?)null);
    }

    private void SetupFolderGet(Guid folderId, Guid ownerId, string folderName = "Test Folder")
    {
        _factory.MetadataClientMock
            .Setup(c => c.GetFolderAsync(folderId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FolderInfo(folderId, folderName, ownerId));
    }

    // ──────────────── POST /api/folders/{id}/share ────────────────

    [Fact]
    public async Task CreateShare_AsOwner_Returns201WithToken()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId);

        var resp = await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<CreateShareResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Token);
        Assert.Equal(folderId, body.FolderId);
    }

    [Fact]
    public async Task CreateShare_NonOwner_Returns404()
    {
        var ownerId = Guid.NewGuid();
        var nonOwner = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var client = ClientFor(nonOwner);

        SetupOwnershipNotFound(folderId, nonOwner);

        var resp = await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var err = await resp.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Equal("folder_not_found", err!.Error);
    }

    [Fact]
    public async Task CreateShare_Unauthenticated_Returns401()
    {
        var folderId = Guid.NewGuid();
        var client = AnonymousClient();

        var resp = await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ──────────────── GET /api/share/{token} ────────────────

    [Fact]
    public async Task GetByToken_ValidToken_Returns200WithFolderDetails()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var ownerClient = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId, "My Shared Folder");
        SetupFolderGet(folderId, ownerId, "My Shared Folder");

        var createResp = await ownerClient.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResp.Content.ReadFromJsonAsync<CreateShareResponse>();

        var anonClient = AnonymousClient();
        var getResp = await anonClient.GetAsync($"/api/share/{created!.Token}");

        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        var details = await getResp.Content.ReadFromJsonAsync<ShareDetailsResponse>();
        Assert.NotNull(details);
        Assert.Equal(folderId, details!.FolderId);
        Assert.Equal("My Shared Folder", details.FolderName);
        Assert.Equal(ownerId, details.OwnerId);
    }

    [Fact]
    public async Task GetByToken_UnknownToken_Returns404()
    {
        var client = AnonymousClient();
        var resp = await client.GetAsync("/api/share/nonexistent_token_xyz");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetByToken_RevokedToken_Returns410Gone()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var ownerClient = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId);
        _factory.EventPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<GraNAS.Shared.Messaging.Events.ShareRevokedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createResp = await ownerClient.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResp.Content.ReadFromJsonAsync<CreateShareResponse>();

        await ownerClient.DeleteAsync($"/api/share/{created!.Token}");

        var anonClient = AnonymousClient();
        var getResp = await anonClient.GetAsync($"/api/share/{created.Token}");
        Assert.Equal(HttpStatusCode.Gone, getResp.StatusCode);
    }

    [Fact]
    public async Task GetByToken_ExpiredToken_Returns404()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        // Insert an expired link directly into DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SharingDbContext>();
        var tokenGen = scope.ServiceProvider.GetRequiredService<ITokenGenerator>();

        var token = tokenGen.GenerateToken();
        var tokenHash = tokenGen.ComputeHash(token);
        db.ShareLinks.Add(new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            OwnerId = ownerId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddHours(-1),
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var client = AnonymousClient();
        var resp = await client.GetAsync($"/api/share/{token}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ──────────────── GET /api/folders/{id}/shares ────────────────

    [Fact]
    public async Task ListShares_OwnerGetsListWithoutTokens()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId);

        await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });

        var listResp = await client.GetAsync($"/api/folders/{folderId}/shares");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        var links = await listResp.Content.ReadFromJsonAsync<ShareLinkResponse[]>();
        Assert.NotNull(links);
        Assert.NotEmpty(links!);
        Assert.Equal(folderId, links[0].FolderId);
    }

    // ──────────────── DELETE /api/share-links/{id} ────────────────

    [Fact]
    public async Task RevokeById_AsOwner_Returns204AndPublishesEvent()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId);
        _factory.EventPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<GraNAS.Shared.Messaging.Events.ShareRevokedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createResp = await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResp.Content.ReadFromJsonAsync<CreateShareResponse>();

        var revokeResp = await client.DeleteAsync($"/api/share-links/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);

        _factory.EventPublisherMock.Verify(
            p => p.PublishAsync(It.IsAny<GraNAS.Shared.Messaging.Events.ShareRevokedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeById_NonOwner_Returns404()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var ownerClient = ClientFor(ownerId);
        var nonOwnerClient = ClientFor(Guid.NewGuid());

        SetupOwnershipCheck(folderId, ownerId);

        var createResp = await ownerClient.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResp.Content.ReadFromJsonAsync<CreateShareResponse>();

        var resp = await nonOwnerClient.DeleteAsync($"/api/share-links/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ──────────────── DELETE /api/share/{token} ────────────────

    [Fact]
    public async Task RevokeByToken_AsOwner_Returns204()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var client = ClientFor(ownerId);

        SetupOwnershipCheck(folderId, ownerId);
        _factory.EventPublisherMock
            .Setup(p => p.PublishAsync(It.IsAny<GraNAS.Shared.Messaging.Events.ShareRevokedEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var createResp = await client.PostAsJsonAsync($"/api/folders/{folderId}/share", new
        {
            expiresAt = DateTime.UtcNow.AddDays(7)
        });
        var created = await createResp.Content.ReadFromJsonAsync<CreateShareResponse>();

        var revokeResp = await client.DeleteAsync($"/api/share/{created!.Token}");
        Assert.Equal(HttpStatusCode.NoContent, revokeResp.StatusCode);
    }

    // ──────────────── Cleanup job ────────────────

    [Fact]
    public async Task DeleteExpired_RemovesExpiredButNotActive()
    {
        var ownerId = Guid.NewGuid();
        var folderId = Guid.NewGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SharingDbContext>();
        var tokenGen = scope.ServiceProvider.GetRequiredService<ITokenGenerator>();

        // Insert 2 expired + 1 active
        for (int i = 0; i < 2; i++)
        {
            var t = tokenGen.GenerateToken();
            db.ShareLinks.Add(new ShareLink
            {
                Id = Guid.NewGuid(),
                FolderId = folderId,
                OwnerId = ownerId,
                TokenHash = tokenGen.ComputeHash(t),
                ExpiresAt = DateTime.UtcNow.AddHours(-1),
                Revoked = false,
                CreatedAt = DateTime.UtcNow
            });
        }
        var activeToken = tokenGen.GenerateToken();
        db.ShareLinks.Add(new ShareLink
        {
            Id = Guid.NewGuid(),
            FolderId = folderId,
            OwnerId = ownerId,
            TokenHash = tokenGen.ComputeHash(activeToken),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Revoked = false,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var shareService = scope.ServiceProvider.GetRequiredService<IShareService>();
        var deleted = await shareService.DeleteExpiredAsync();

        Assert.True(deleted >= 2);
        var activeRemains = await db.ShareLinks.AnyAsync(s => s.TokenHash == tokenGen.ComputeHash(activeToken));
        Assert.True(activeRemains);
    }
}
