namespace GraNAS.Desktop.App.Services.Api;

public record TurnCredentials(string Username, string Credential, string[] Uris, int Ttl);

public interface ISignalingApi
{
    Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default);
}
