namespace GraNAS.Signaling.Models.DTO;

public record TurnCredentialsResponse(
    string Username,
    string Credential,
    string[] Uris,
    int Ttl);
