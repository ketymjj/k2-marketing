using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Clients;

public record ClientListItemDto(Guid Id, string Name, string CompanyName, string Email, string? Phone, string? Industry,
    ClientStatus Status, decimal MonthlyBudget, int ActiveCampaigns, int TotalCampaigns, DateTime CreatedAt);

public record ClientDto(Guid Id, string Name, string CompanyName, string Email, string? Phone, string? Document,
    string? Industry, string? Website, ClientStatus Status, decimal MonthlyBudget, DateTime CreatedAt, DateTime? UpdatedAt);

public record ClientOverviewDto(
    ClientDto Client, int TotalCampaigns, int ActiveCampaigns, int TotalAds, int TotalCreatives,
    int LeadsRegistered, int ConversionsRegistered, decimal SalesRevenue,
    MetricsDto AllTime, MetricsDto CurrentMonth, decimal? MonthlyBudgetUsage);

public record ClientLookupDto(Guid Id, string Name, ClientStatus Status);
public record HistoryItemDto(DateTime CreatedAt, string UserEmail, string Action, string Entity, string? EntityId, string? Details);

public record SaveClientRequest(string Name, string CompanyName, string Email, string? Phone, string? Document,
    string? Industry, string? Website, decimal MonthlyBudget);

public class ClientQuery : PageQuery
{
    public ClientStatus? Status { get; set; }
    public string? Industry { get; set; }
}

public class SaveClientValidator : AbstractValidator<SaveClientRequest>
{
    public SaveClientValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.CompanyName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Document).MaximumLength(20).Matches(@"^[0-9./-]*$").WithMessage("Documento deve conter apenas números e pontuação.");
        RuleFor(x => x.Industry).MaximumLength(80);
        RuleFor(x => x.Website).MaximumLength(300)
            .Must(BeHttpUrl!).When(x => !string.IsNullOrWhiteSpace(x.Website)).WithMessage("Informe uma URL http(s) válida.");
        RuleFor(x => x.MonthlyBudget).GreaterThanOrEqualTo(0);
    }

    internal static bool BeHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}

public class ClientService(IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, IValidator<SaveClientRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<Client, object?>>> Sort = new()
    {
        ["name"] = c => c.Name, ["companyName"] = c => c.CompanyName, ["industry"] = c => c.Industry,
        ["status"] = c => c.Status, ["monthlyBudget"] = c => c.MonthlyBudget, ["createdAt"] = c => c.CreatedAt
    };

    public async Task<PagedResult<ClientListItemDto>> ListAsync(ClientQuery q, CancellationToken ct)
    {
        var query = scope.Clients(db.Clients.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.CompanyName.ToLower().Contains(s)
                                     || c.Email.ToLower().Contains(s) || (c.Document != null && c.Document.Contains(s)));
        }
        if (q.Status is { } status) query = query.Where(c => c.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Industry)) query = query.Where(c => c.Industry == q.Industry);

        return await query.ApplySort(q, Sort, "name")
            .Select(c => new ClientListItemDto(c.Id, c.Name, c.CompanyName, c.Email, c.Phone, c.Industry, c.Status, c.MonthlyBudget,
                c.Campaigns.Count(x => x.Status == CampaignStatus.Active), c.Campaigns.Count, c.CreatedAt))
            .ToPagedAsync(q, ct);
    }

    public Task<List<ClientLookupDto>> LookupAsync(bool activeOnly, CancellationToken ct) =>
        scope.Clients(db.Clients.AsNoTracking())
            .Where(c => !activeOnly || c.Status == ClientStatus.Active)
            .OrderBy(c => c.Name).Select(c => new ClientLookupDto(c.Id, c.Name, c.Status)).ToListAsync(ct);

    public Task<List<string>> IndustriesAsync(CancellationToken ct) =>
        scope.Clients(db.Clients.AsNoTracking()).Where(c => c.Industry != null)
            .Select(c => c.Industry!).Distinct().OrderBy(i => i).ToListAsync(ct);

    public async Task<ClientDto> GetAsync(Guid id, CancellationToken ct)
    {
        scope.EnsureClient(id);
        var c = await db.Clients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Cliente", id);
        return ToDto(c);
    }

    public async Task<ClientOverviewDto> OverviewAsync(Guid id, CancellationToken ct)
    {
        var client = await GetAsync(id, ct);
        var today = clock.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var metrics = db.CampaignMetrics.AsNoTracking().Where(m => m.Campaign!.ClientId == id);
        var allTime = await metrics.TotalsAsync(ct);
        var month = await metrics.Where(m => m.Date >= monthStart && m.Date <= today).TotalsAsync(ct);

        var campaigns = db.Campaigns.AsNoTracking().Where(c => c.ClientId == id);
        var totalCampaigns = await campaigns.CountAsync(ct);
        var activeCampaigns = await campaigns.CountAsync(c => c.Status == CampaignStatus.Active, ct);
        var totalAds = await db.Advertisements.CountAsync(a => a.Campaign!.ClientId == id, ct);
        var totalCreatives = await db.CreativeAssets.CountAsync(a => a.ClientId == id, ct);
        var leads = await db.Leads.CountAsync(l => l.ClientId == id, ct);
        var conversions = db.Conversions.Where(c => c.Campaign!.ClientId == id);
        var conversionCount = await conversions.CountAsync(ct);
        var sales = await conversions.Where(c => c.ConversionType == ConversionType.Purchase).SumAsync(c => (decimal?)c.RevenueAmount, ct) ?? 0m;

        return new ClientOverviewDto(client, totalCampaigns, activeCampaigns, totalAds, totalCreatives, leads, conversionCount, sales,
            MetricsDto.From(allTime), MetricsDto.From(month), MetricCalculator.BudgetUsage(month.Spend, client.MonthlyBudget));
    }

    /// <summary>Histórico: ações auditadas sobre o cliente e suas campanhas.</summary>
    public async Task<List<HistoryItemDto>> HistoryAsync(Guid id, CancellationToken ct)
    {
        scope.EnsureClient(id);
        var key = id.ToString();
        var campaignIds = await db.Campaigns.Where(c => c.ClientId == id).Select(c => c.Id.ToString()).ToListAsync(ct);
        return await db.AuditLogs.AsNoTracking()
            .Where(a => (a.Entity == "Client" && a.EntityId == key) || (a.Entity == "Campaign" && campaignIds.Contains(a.EntityId!)))
            .OrderByDescending(a => a.CreatedAt).Take(100)
            .Select(a => new HistoryItemDto(a.CreatedAt, a.UserEmail, a.Action, a.Entity, a.EntityId, a.Details))
            .ToListAsync(ct);
    }

    public async Task<ClientDto> CreateAsync(SaveClientRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureDocumentFreeAsync(r.Document, null, ct);
        var client = new Client { CreatedAt = clock.UtcNow };
        Apply(client, r);
        db.Clients.Add(client);
        audit.Log("Create", "Client", client.Id, client.Name);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    public async Task<ClientDto> UpdateAsync(Guid id, SaveClientRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cliente", id);
        await EnsureDocumentFreeAsync(r.Document, id, ct);
        Apply(client, r);
        client.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "Client", id, client.Name);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    /// <summary>Ativa/desativa o cliente. Clientes não são excluídos para preservar o histórico de investimentos.</summary>
    public async Task<ClientDto> SetStatusAsync(Guid id, ClientStatus status, CancellationToken ct)
    {
        var client = await db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Cliente", id);
        if (client.Status == status) return ToDto(client);
        client.Status = status;
        client.UpdatedAt = clock.UtcNow;
        audit.Log(status == ClientStatus.Active ? "Activate" : "Deactivate", "Client", id, client.Name);
        await db.SaveChangesAsync(ct);
        return ToDto(client);
    }

    private async Task EnsureDocumentFreeAsync(string? document, Guid? exceptId, CancellationToken ct)
    {
        var doc = string.IsNullOrWhiteSpace(document) ? null : document.Trim();
        if (doc is not null && await db.Clients.AnyAsync(c => c.Document == doc && c.Id != exceptId, ct))
            throw new ConflictException("client.documentTaken", "Já existe um cliente com este documento.");
    }

    private static void Apply(Client c, SaveClientRequest r)
    {
        c.Name = r.Name.Trim();
        c.CompanyName = r.CompanyName.Trim();
        c.Email = r.Email.Trim().ToLowerInvariant();
        c.Phone = Clean(r.Phone);
        c.Document = Clean(r.Document);
        c.Industry = Clean(r.Industry);
        c.Website = Clean(r.Website);
        c.MonthlyBudget = r.MonthlyBudget;
    }

    internal static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static ClientDto ToDto(Client c) => new(c.Id, c.Name, c.CompanyName, c.Email, c.Phone, c.Document, c.Industry,
        c.Website, c.Status, c.MonthlyBudget, c.CreatedAt, c.UpdatedAt);
}
