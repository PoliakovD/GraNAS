using Meziantou.Framework.Win32;

namespace GraNAS.Desktop.App.Services.Auth;

public class WindowsCredentialStore : ICredentialStore
{
  private const string CredentialNamespace = "GraNAS";

  public string? Get(string key)
  {
    var cred = CredentialManager.ReadCredential($"{CredentialNamespace}:{key}");
    return cred?.Password;
  }

  public void Save(string key, string value)
  {
    CredentialManager.WriteCredential(
      $"{CredentialNamespace}:{key}",
      userName: Environment.UserName,
      secret: value,
      CredentialPersistence.LocalMachine);
  }

  public void Delete(string key)
  {
    try { CredentialManager.DeleteCredential($"{CredentialNamespace}:{key}"); }
    catch { /* already deleted */ }
  }
}
