using System.Text.Json;
using System.Threading.Tasks;
using GraNAS.Notifications.Services.Implementations;
using GraNAS.Notifications.Services.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GraNAS.WebAPI.Tests.Unit.Notifications;

public class EmailTemplateRendererTests
{
    private readonly EmailTemplateRenderer _sut = new(NullLogger<EmailTemplateRenderer>.Instance);
    private readonly UserContact _contact = new("user@test.com", "Test User");

    private JsonElement BuildData(string folderName = "TestFolder", string ownerName = "Owner")
    {
        return JsonDocument.Parse($@"{{""FolderName"":""{folderName}"",""OwnerName"":""{ownerName}""}}").RootElement;
    }

    [Theory]
    [InlineData("access.granted")]
    [InlineData("access.revoked")]
    [InlineData("share.revoked")]
    [InlineData("access.lost")]
    public async Task RenderAsync_AllEventTypes_ReturnNonEmptyEmail(string eventType)
    {
        var result = await _sut.RenderAsync(eventType, BuildData(), _contact);

        Assert.NotEmpty(result.Subject);
        Assert.NotEmpty(result.Html);
        Assert.NotEmpty(result.Text);
    }

    [Fact]
    public async Task RenderAsync_FolderNameIsInSubject()
    {
        var result = await _sut.RenderAsync("access.granted", BuildData("MyFolder"), _contact);
        Assert.Contains("MyFolder", result.Subject);
    }

    [Fact]
    public async Task RenderAsync_UnknownEventType_ReturnsGenericEmail()
    {
        var result = await _sut.RenderAsync("unknown.event", JsonDocument.Parse("{}").RootElement, _contact);

        Assert.NotEmpty(result.Subject);
        Assert.Contains("GraNAS", result.Subject);
    }
}
