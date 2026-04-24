using GraNAS.Sharing.Services.Implementations;

namespace GraNAS.WebAPI.Tests.Unit;

public class TokenGeneratorTests
{
    private readonly TokenGenerator _sut = new();

    [Fact]
    public void GenerateToken_Returns43CharBase64Url()
    {
        // 32 bytes → 43 chars base64url (no padding)
        var token = _sut.GenerateToken();
        Assert.Equal(43, token.Length);
    }

    [Fact]
    public void GenerateToken_ContainsOnlyBase64UrlChars()
    {
        var token = _sut.GenerateToken();
        Assert.Matches(@"^[A-Za-z0-9\-_]+$", token);
    }

    [Fact]
    public void GenerateToken_NoEqualsPlusSlash()
    {
        for (int i = 0; i < 100; i++)
        {
            var token = _sut.GenerateToken();
            Assert.DoesNotContain("=", token);
            Assert.DoesNotContain("+", token);
            Assert.DoesNotContain("/", token);
        }
    }

    [Fact]
    public void GenerateToken_Unique_Over1000Iterations()
    {
        var tokens = new HashSet<string>();
        for (int i = 0; i < 1000; i++)
            tokens.Add(_sut.GenerateToken());
        Assert.Equal(1000, tokens.Count);
    }

    [Fact]
    public void ComputeHash_Returns64CharLowerHex()
    {
        var hash = _sut.ComputeHash("any_token_value");
        Assert.Equal(64, hash.Length);
        Assert.Matches(@"^[0-9a-f]+$", hash);
    }

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        const string token = "same_token";
        var h1 = _sut.ComputeHash(token);
        var h2 = _sut.ComputeHash(token);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void ComputeHash_DiffersForDifferentInputs()
    {
        var h1 = _sut.ComputeHash("token_a");
        var h2 = _sut.ComputeHash("token_b");
        Assert.NotEqual(h1, h2);
    }
}
