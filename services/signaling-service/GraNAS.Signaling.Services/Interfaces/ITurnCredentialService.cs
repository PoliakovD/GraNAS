namespace GraNAS.Signaling.Services.Interfaces;

public record TurnCredentials(string Username, string Credential, string[] Uris, int Ttl);

public interface ITurnCredentialService
{
    TurnCredentials Generate(string userId);
}
