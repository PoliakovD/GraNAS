namespace GraNAS.Notifications.Services.Implementations;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = "noreply@granas.local";
    public bool UseStartTls { get; set; } = true;
}
