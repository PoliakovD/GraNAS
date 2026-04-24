namespace GraNAS.Sharing.Services.Interfaces;

public interface ITokenGenerator
{
    string GenerateToken();
    string ComputeHash(string token);
}
