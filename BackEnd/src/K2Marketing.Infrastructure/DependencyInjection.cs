using K2Marketing.Application.Common;
using K2Marketing.Infrastructure.Jobs;
using K2Marketing.Infrastructure.Persistence;
using K2Marketing.Infrastructure.Security;
using K2Marketing.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K2Marketing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        var connection = config.GetConnectionString("Default")
                         ?? throw new InvalidOperationException("ConnectionStrings:Default não configurada.");
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<DataSeeder>();

        if (config.GetValue("Jobs:NotificationsEnabled", true))
            services.AddHostedService<NotificationBackgroundService>();
        return services;
    }
}

/// <summary>Usado pelo dotnet-ef em tempo de design.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("K2_DESIGN_CONNECTION")
                 ?? "Host=localhost;Port=5435;Database=k2marketing;Username=k2marketing;Password=design-time-only";
        return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(cs).Options);
    }
}
