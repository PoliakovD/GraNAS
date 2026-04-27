namespace GraNAS.Desktop.App.Services.Api;

public class SignalingApi : ApiBase, ISignalingApi
{
    public SignalingApi(HttpClient http) : base(http) { }

    public async Task<TurnCredentials?> GetTurnCredentialsAsync(CancellationToken ct = default)
    {
        try { return await GetAsync<TurnCredentials>("api/signaling/turn/credentials", ct); }
        catch { return null; }
    }
}
