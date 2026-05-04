namespace GraNAS.Desktop.App.Services.Auth;

public interface ICredentialStore
{
  string? Get(string key);
  void Save(string key, string value);
  void Delete(string key);
}
