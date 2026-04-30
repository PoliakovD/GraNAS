using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace GraNAS.Signaling.DAL;

public class SignalingDbContextFactory : IDesignTimeDbContextFactory<SignalingDbContext>
{
    public SignalingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration["ConnectionStrings:SignalingDb"];

        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("ConnectionStrings:SignalingDb is not configured");

        var optionsBuilder = new DbContextOptionsBuilder<SignalingDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new SignalingDbContext(optionsBuilder.Options);
    }
}
