using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace K2Marketing.Application.Common;

public interface IAppDbContext
{
    DbSet<Role> Roles { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Client> Clients { get; }
    DbSet<Campaign> Campaigns { get; }
    DbSet<Advertisement> Advertisements { get; }
    DbSet<CreativeAsset> CreativeAssets { get; }
    DbSet<AdvertisementCreative> AdvertisementCreatives { get; }
    DbSet<CampaignBudget> CampaignBudgets { get; }
    DbSet<Lead> Leads { get; }
    DbSet<LeadInteraction> LeadInteractions { get; }
    DbSet<Conversion> Conversions { get; }
    DbSet<CampaignMetric> CampaignMetrics { get; }
    DbSet<MarketingTask> MarketingTasks { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AgencySettings> AgencySettings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

public interface ICurrentUser
{
    Guid UserId { get; }
    string Email { get; }
    string Name { get; }
    UserRole Role { get; }
    Guid? ClientId { get; }
    bool IsAuthenticated { get; }
}

public interface IClock
{
    DateTime UtcNow { get; }
    /// <summary>Data local da agência (fuso America/Sao_Paulo).</summary>
    DateOnly Today { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    string CreateRefreshToken();
    string HashToken(string token);
    TimeSpan RefreshTokenLifetime { get; }
}

/// <summary>Armazenamento de arquivos de criativos (disco local / volume Docker nesta versão).</summary>
public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension, CancellationToken ct);
    Stream? OpenRead(string relativePath);
    void Delete(string relativePath);
}
