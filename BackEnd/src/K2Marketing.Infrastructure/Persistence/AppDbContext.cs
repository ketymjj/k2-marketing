using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace K2Marketing.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Advertisement> Advertisements => Set<Advertisement>();
    public DbSet<CreativeAsset> CreativeAssets => Set<CreativeAsset>();
    public DbSet<AdvertisementCreative> AdvertisementCreatives => Set<AdvertisementCreative>();
    public DbSet<CampaignBudget> CampaignBudgets => Set<CampaignBudget>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadInteraction> LeadInteractions => Set<LeadInteraction>();
    public DbSet<Conversion> Conversions => Set<Conversion>();
    public DbSet<CampaignMetric> CampaignMetrics => Set<CampaignMetric>();
    public DbSet<MarketingTask> MarketingTasks => Set<MarketingTask>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AgencySettings> AgencySettings => Set<AgencySettings>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken ct = default) => Database.BeginTransactionAsync(ct);

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // Valores monetários: decimal(18,2). Enums de negócio gravados como texto (legíveis em SQL e relatórios).
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<ClientStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<CampaignObjective>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<AdPlatform>().HaveConversion<string>().HaveMaxLength(30);
        b.Properties<CampaignStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<AdStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<CreativeType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<LeadStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<LeadInteractionType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<ConversionType>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<DataSource>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<TaskPriority>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<MarketingTaskStatus>().HaveConversion<string>().HaveMaxLength(20);
        b.Properties<NotificationType>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Role>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasConversion<int>().ValueGeneratedNever();
            e.Property(x => x.Name).HasMaxLength(30).IsRequired();
            e.Property(x => x.Description).HasMaxLength(200);
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Role { Id = UserRole.Admin, Name = "ADMIN", Description = "Acesso completo" },
                new Role { Id = UserRole.Manager, Name = "MANAGER", Description = "Gestão de clientes, campanhas e relatórios" },
                new Role { Id = UserRole.Analyst, Name = "ANALYST", Description = "Operação de campanhas, anúncios, criativos e métricas" },
                new Role { Id = UserRole.ClientViewer, Name = "CLIENT_VIEWER", Description = "Somente os próprios dados e relatórios" });
        });

        m.Entity<User>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.PreferredLanguage).HasMaxLength(10);
            e.Property(x => x.Role).HasConversion<int>();
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne<Role>().WithMany().HasForeignKey(x => x.Role).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Users_ClientViewer_Client", "\"Role\" <> 4 OR \"ClientId\" IS NOT NULL"));
        });

        m.Entity<RefreshToken>(e =>
        {
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.Property(x => x.ReplacedByHash).HasMaxLength(128);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.UserId);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Client>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.CompanyName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Document).HasMaxLength(20);
            e.Property(x => x.Industry).HasMaxLength(80);
            e.Property(x => x.Website).HasMaxLength(300);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Document).IsUnique().HasFilter("\"Document\" IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("CK_Clients_MonthlyBudget", "\"MonthlyBudget\" >= 0"));
        });

        m.Entity<Campaign>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.HasIndex(x => new { x.ClientId, x.Status });
            e.HasIndex(x => x.Platform);
            e.HasIndex(x => x.StartDate);
            e.HasOne(x => x.Client).WithMany(c => c.Campaigns).HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Campaigns_Dates", "\"EndDate\" IS NULL OR \"EndDate\" >= \"StartDate\"");
                t.HasCheckConstraint("CK_Campaigns_PlannedBudget", "\"PlannedBudget\" >= 0");
            });
        });

        m.Entity<Advertisement>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Headline).HasMaxLength(150).IsRequired();
            e.Property(x => x.PrimaryText).HasMaxLength(2200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.Property(x => x.CallToAction).HasMaxLength(50).IsRequired();
            e.Property(x => x.DestinationUrl).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.CampaignId);
            e.HasOne(x => x.Campaign).WithMany(c => c.Advertisements).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<CreativeAsset>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.FilePath).HasMaxLength(300);
            e.Property(x => x.OriginalFileName).HasMaxLength(255);
            e.Property(x => x.ContentType).HasMaxLength(100);
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.TextContent).HasMaxLength(5000);
            e.HasIndex(x => new { x.ClientId, x.Type });
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
        });

        m.Entity<AdvertisementCreative>(e =>
        {
            e.HasKey(x => new { x.AdvertisementId, x.CreativeAssetId });
            e.HasOne(x => x.Advertisement).WithMany(a => a.Creatives).HasForeignKey(x => x.AdvertisementId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.CreativeAsset).WithMany(c => c.Advertisements).HasForeignKey(x => x.CreativeAssetId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<CampaignBudget>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Ignore(x => x.Remaining);
            e.HasIndex(x => new { x.CampaignId, x.StartDate });
            e.HasOne(x => x.Campaign).WithMany(c => c.Budgets).HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Budgets_Dates", "\"EndDate\" >= \"StartDate\"");
                t.HasCheckConstraint("CK_Budgets_Amounts", "\"PlannedAmount\" >= 0 AND \"ActualSpend\" >= 0");
            });
        });

        m.Entity<Lead>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.Source).HasMaxLength(80);
            e.HasIndex(x => new { x.ClientId, x.Status });
            e.HasIndex(x => x.CampaignId);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.CreatedAt);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<LeadInteraction>(e =>
        {
            e.Property(x => x.Notes).HasMaxLength(2000);
            e.Property(x => x.UserName).HasMaxLength(150);
            e.HasIndex(x => new { x.LeadId, x.CreatedAt });
            e.HasOne<Lead>().WithMany(l => l.Interactions).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<Conversion>(e =>
        {
            e.Property(x => x.ExternalId).HasMaxLength(100);
            e.Property(x => x.EventKey).HasMaxLength(128).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => new { x.CampaignId, x.EventKey }).IsUnique();
            e.HasIndex(x => x.ConversionDate);
            e.HasIndex(x => x.LeadId);
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Lead).WithMany().HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.SetNull);
            e.ToTable(t => t.HasCheckConstraint("CK_Conversions_Revenue", "\"RevenueAmount\" >= 0"));
        });

        m.Entity<CampaignMetric>(e =>
        {
            // Uma linha por campanha + anúncio (ou nível campanha) + dia: impede métricas duplicadas.
            e.HasIndex(x => new { x.CampaignId, x.AdvertisementId, x.Date }).IsUnique().AreNullsDistinct(false);
            e.HasIndex(x => x.Date);
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Advertisement).WithMany().HasForeignKey(x => x.AdvertisementId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Metrics_NonNegative",
                "\"Impressions\" >= 0 AND \"Reach\" >= 0 AND \"Clicks\" >= 0 AND \"Spend\" >= 0 AND \"Leads\" >= 0 AND \"Conversions\" >= 0 AND \"AttributedRevenue\" >= 0"));
        });

        m.Entity<MarketingTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.HasIndex(x => new { x.DueDate, x.Status });
            e.HasIndex(x => x.AssignedToId);
            e.HasIndex(x => x.CampaignId);
            e.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Campaign).WithMany().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.SetNull);
        });

        m.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Data).HasColumnType("jsonb");
            e.Property(x => x.Link).HasMaxLength(300);
            e.Property(x => x.DedupKey).HasMaxLength(150);
            e.HasIndex(x => new { x.UserId, x.ReadAt });
            e.HasIndex(x => new { x.UserId, x.DedupKey }).IsUnique().HasFilter("\"DedupKey\" IS NOT NULL");
            e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        m.Entity<AuditLog>(e =>
        {
            e.Property(x => x.UserEmail).HasMaxLength(200);
            e.Property(x => x.Action).HasMaxLength(60).IsRequired();
            e.Property(x => x.Entity).HasMaxLength(60).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(60);
            e.Property(x => x.Details).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAt);
            e.HasIndex(x => new { x.Entity, x.EntityId });
        });

        m.Entity<AgencySettings>(e =>
        {
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.AgencyName).HasMaxLength(150).IsRequired();
            e.Property(x => x.PrimaryColor).HasMaxLength(9);
            e.Property(x => x.ContactEmail).HasMaxLength(200);
            e.Property(x => x.Website).HasMaxLength(300);
            e.Property(x => x.ReportFooter).HasMaxLength(500);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.HasData(new AgencySettings
            {
                Id = 1, AgencyName = "K2 Digital", PrimaryColor = "#6941C6", ContactEmail = "contato@k2digital.com.br",
                Website = "https://k2digital.com.br", ReportFooter = "K2 Digital · Marketing de performance", Currency = "BRL"
            });
        });
    }
}
