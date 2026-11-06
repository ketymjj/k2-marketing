using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Campaigns;

public record CampaignListItemDto(Guid Id, Guid ClientId, string ClientName, string Name, CampaignObjective Objective,
    AdPlatform Platform, CampaignStatus Status, DateOnly StartDate, DateOnly? EndDate, decimal PlannedBudget,
    int AdsCount, decimal Spend, long Clicks, long Conversions, decimal? Roas, DateTime UpdatedAt);

public record CampaignDto(Guid Id, Guid ClientId, string ClientName, string Name, CampaignObjective Objective,
    AdPlatform Platform, CampaignStatus Status, DateOnly StartDate, DateOnly? EndDate, decimal PlannedBudget,
    DateTime CreatedAt, DateTime UpdatedAt, IReadOnlyCollection<CampaignStatus> AllowedTransitions,
    int AdsCount, int LeadsRegistered, int ConversionsRegistered, MetricsDto Metrics,
    decimal BudgetPlanned, decimal BudgetSpent, decimal? BudgetUsage);

public record CampaignLookupDto(Guid Id, string Name, Guid ClientId, AdPlatform Platform, CampaignStatus Status);

public record AdPerformanceDto(Guid AdvertisementId, string Name, AdStatus Status, MetricsDto Metrics);

public record CampaignPerformanceDto(Guid CampaignId, DateOnly From, DateOnly To, MetricsDto Totals,
    List<DailyPoint> Daily, List<AdPerformanceDto> ByAd, decimal CampaignLevelSpend);

public record SaveCampaignRequest(Guid ClientId, string Name, CampaignObjective Objective, AdPlatform Platform,
    DateOnly StartDate, DateOnly? EndDate, decimal PlannedBudget, CampaignStatus? Status);

public record ChangeCampaignStatusRequest(CampaignStatus Status);

public class CampaignQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public AdPlatform? Platform { get; set; }
    public CampaignStatus? Status { get; set; }
    public CampaignObjective? Objective { get; set; }
    /// <summary>Campanhas cujo período de veiculação intersecta [From, To].</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveCampaignValidator : AbstractValidator<SaveCampaignRequest>
{
    public SaveCampaignValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Objective).IsInEnum();
        RuleFor(x => x.Platform).IsInEnum();
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue)
            .WithMessage("A data final deve ser igual ou posterior à data inicial.");
        RuleFor(x => x.PlannedBudget).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Status).Must(s => s is null or CampaignStatus.Draft or CampaignStatus.Scheduled or CampaignStatus.Active)
            .WithMessage("Uma nova campanha deve iniciar como Rascunho, Agendada ou Ativa.");
    }
}

public class CampaignService(
    IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, IValidator<SaveCampaignRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<Campaign, object?>>> Sort = new()
    {
        ["name"] = c => c.Name, ["client"] = c => c.Client!.Name, ["platform"] = c => c.Platform, ["status"] = c => c.Status,
        ["objective"] = c => c.Objective, ["startDate"] = c => c.StartDate, ["endDate"] = c => c.EndDate,
        ["plannedBudget"] = c => c.PlannedBudget, ["updatedAt"] = c => c.UpdatedAt
    };

    public async Task<PagedResult<CampaignListItemDto>> ListAsync(CampaignQuery q, CancellationToken ct)
    {
        var query = Filter(q);
        var page = await query.ApplySort(q, Sort, "startDate", defaultDescending: true)
            .Select(c => new { c.Id, c.ClientId, ClientName = c.Client!.Name, c.Name, c.Objective, c.Platform, c.Status,
                c.StartDate, c.EndDate, c.PlannedBudget, Ads = c.Advertisements.Count, c.UpdatedAt })
            .ToPagedAsync(q, ct);

        var ids = page.Items.Select(i => i.Id).ToList();
        var totals = await db.CampaignMetrics.AsNoTracking().Where(m => ids.Contains(m.CampaignId)).ByCampaignAsync(ct);
        var items = page.Items.Select(c =>
        {
            var t = totals.GetValueOrDefault(c.Id, MetricTotals.Empty);
            return new CampaignListItemDto(c.Id, c.ClientId, c.ClientName, c.Name, c.Objective, c.Platform, c.Status, c.StartDate,
                c.EndDate, c.PlannedBudget, c.Ads, t.Spend, t.Clicks, t.Conversions, t.Roas, c.UpdatedAt);
        }).ToList();
        return new PagedResult<CampaignListItemDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public Task<List<CampaignLookupDto>> LookupAsync(Guid? clientId, CancellationToken ct) =>
        scope.Campaigns(db.Campaigns.AsNoTracking())
            .Where(c => clientId == null || c.ClientId == clientId)
            .OrderBy(c => c.Name).Select(c => new CampaignLookupDto(c.Id, c.Name, c.ClientId, c.Platform, c.Status)).ToListAsync(ct);

    public async Task<CampaignDto> GetAsync(Guid id, CancellationToken ct)
    {
        var c = await scope.Campaigns(db.Campaigns.AsNoTracking()).Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Campanha", id);
        var totals = await db.CampaignMetrics.AsNoTracking().Where(m => m.CampaignId == id).TotalsAsync(ct);
        var ads = await db.Advertisements.CountAsync(a => a.CampaignId == id, ct);
        var leads = await db.Leads.CountAsync(l => l.CampaignId == id, ct);
        var conversions = await db.Conversions.CountAsync(x => x.CampaignId == id, ct);
        var budgets = await db.CampaignBudgets.AsNoTracking().Where(b => b.CampaignId == id)
            .GroupBy(_ => 1).Select(g => new { P = g.Sum(b => b.PlannedAmount), S = g.Sum(b => b.ActualSpend) }).FirstOrDefaultAsync(ct);
        var planned = budgets?.P ?? 0m;
        var spent = budgets?.S ?? 0m;
        return new CampaignDto(c.Id, c.ClientId, c.Client!.Name, c.Name, c.Objective, c.Platform, c.Status, c.StartDate, c.EndDate,
            c.PlannedBudget, c.CreatedAt, c.UpdatedAt, Campaign.AllowedTransitions(c.Status), ads, leads, conversions,
            MetricsDto.From(totals), planned, spent, MetricCalculator.BudgetUsage(spent, planned));
    }

    public async Task<CampaignPerformanceDto> PerformanceAsync(Guid id, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var campaign = await scope.Campaigns(db.Campaigns.AsNoTracking()).FirstOrDefaultAsync(x => x.Id == id, ct)
                       ?? throw new NotFoundException("Campanha", id);
        // Sem período informado, usa a vigência da campanha (limitada a hoje).
        var end = to ?? Min(campaign.EndDate ?? clock.Today, clock.Today);
        var start = from ?? campaign.StartDate;
        if (start > end) start = end.AddDays(-29);
        var range = new DateRange(start, end);

        var metrics = db.CampaignMetrics.AsNoTracking().Where(m => m.CampaignId == id && m.Date >= range.From && m.Date <= range.To);
        var totals = await metrics.TotalsAsync(ct);
        var daily = await metrics.DailyAsync(range.From, range.To, ct);
        var byAd = await metrics.ByAdAsync(ct);
        var campaignLevelSpend = await metrics.Where(m => m.AdvertisementId == null).SumAsync(m => (decimal?)m.Spend, ct) ?? 0m;
        var ads = await db.Advertisements.AsNoTracking().Where(a => a.CampaignId == id).OrderBy(a => a.Name)
            .Select(a => new { a.Id, a.Name, a.Status }).ToListAsync(ct);
        return new CampaignPerformanceDto(id, range.From, range.To, MetricsDto.From(totals), daily,
            ads.Select(a => new AdPerformanceDto(a.Id, a.Name, a.Status, MetricsDto.From(byAd.GetValueOrDefault(a.Id, MetricTotals.Empty)))).ToList(),
            campaignLevelSpend);
    }

    public async Task<CampaignDto> CreateAsync(SaveCampaignRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureActiveClientAsync(r.ClientId, ct);
        var now = clock.UtcNow;
        var campaign = new Campaign { ClientId = r.ClientId, Status = r.Status ?? CampaignStatus.Draft, CreatedAt = now, UpdatedAt = now };
        Apply(campaign, r);
        db.Campaigns.Add(campaign);
        audit.Log("Create", "Campaign", campaign.Id, campaign.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(campaign.Id, ct);
    }

    public async Task<CampaignDto> UpdateAsync(Guid id, SaveCampaignRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r with { Status = null }, ct);
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Campanha", id);
        if (campaign.ClientId != r.ClientId)
        {
            if (await db.CampaignMetrics.AnyAsync(m => m.CampaignId == id, ct))
                throw new DomainException("campaign.clientLocked", "Não é possível trocar o cliente de uma campanha que já possui métricas.");
            await EnsureActiveClientAsync(r.ClientId, ct);
            campaign.ClientId = r.ClientId;
        }
        Apply(campaign, r);
        campaign.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "Campaign", id, campaign.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Duplica a campanha como Rascunho, copiando os anúncios e vínculos de criativos (sem métricas, leads ou orçamentos).</summary>
    public async Task<CampaignDto> DuplicateAsync(Guid id, CancellationToken ct)
    {
        var source = await db.Campaigns.AsNoTracking().Include(c => c.Advertisements).ThenInclude(a => a.Creatives)
            .FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Campanha", id);
        var now = clock.UtcNow;
        var copy = new Campaign
        {
            ClientId = source.ClientId, Name = Truncate($"{source.Name} (cópia)", 200), Objective = source.Objective,
            Platform = source.Platform, Status = CampaignStatus.Draft, StartDate = source.StartDate, EndDate = source.EndDate,
            PlannedBudget = source.PlannedBudget, CreatedAt = now, UpdatedAt = now
        };
        foreach (var ad in source.Advertisements)
        {
            var adCopy = new Advertisement
            {
                Name = ad.Name, Headline = ad.Headline, PrimaryText = ad.PrimaryText, Description = ad.Description,
                CallToAction = ad.CallToAction, DestinationUrl = ad.DestinationUrl, Status = AdStatus.Draft, CreatedAt = now
            };
            adCopy.Creatives.AddRange(ad.Creatives.Select(cr => new AdvertisementCreative { CreativeAssetId = cr.CreativeAssetId, LinkedAt = now }));
            copy.Advertisements.Add(adCopy);
        }
        db.Campaigns.Add(copy);
        audit.Log("Duplicate", "Campaign", copy.Id, $"Origem: {source.Id}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    /// <summary>Status interno do K2 Marketing (não altera a campanha na plataforma de anúncios).</summary>
    public async Task<CampaignDto> ChangeStatusAsync(Guid id, CampaignStatus status, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Campanha", id);
        var previous = campaign.Status;
        campaign.ChangeStatus(status, clock.UtcNow);
        if (status == CampaignStatus.Completed && (campaign.EndDate is null || campaign.EndDate > clock.Today))
            campaign.EndDate = clock.Today < campaign.StartDate ? campaign.StartDate : clock.Today;
        audit.Log("ChangeStatus", "Campaign", id, $"{previous} → {status}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Exclui apenas campanhas sem métricas, leads ou conversões; as demais devem ser canceladas/encerradas.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Campanha", id);
        var hasData = await db.CampaignMetrics.AnyAsync(m => m.CampaignId == id, ct)
                      || await db.Conversions.AnyAsync(c => c.CampaignId == id, ct)
                      || await db.Leads.AnyAsync(l => l.CampaignId == id, ct);
        if (hasData)
            throw new DomainException("campaign.hasData", "A campanha possui métricas, leads ou conversões. Cancele ou encerre em vez de excluir.");
        db.Campaigns.Remove(campaign);
        audit.Log("Delete", "Campaign", id, campaign.Name);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Campaign> Filter(CampaignQuery q)
    {
        var query = scope.Campaigns(db.Campaigns.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(s) || c.Client!.Name.ToLower().Contains(s));
        }
        if (q.ClientId is { } clientId) query = query.Where(c => c.ClientId == clientId);
        if (q.Platform is { } platform) query = query.Where(c => c.Platform == platform);
        if (q.Status is { } status) query = query.Where(c => c.Status == status);
        if (q.Objective is { } objective) query = query.Where(c => c.Objective == objective);
        if (q.From is { } from) query = query.Where(c => c.EndDate == null || c.EndDate >= from);
        if (q.To is { } to) query = query.Where(c => c.StartDate <= to);
        return query;
    }

    private async Task EnsureActiveClientAsync(Guid clientId, CancellationToken ct)
    {
        var client = await db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clientId, ct) ?? throw new NotFoundException("Cliente", clientId);
        if (client.Status != ClientStatus.Active)
            throw new DomainException("client.inactive", "O cliente está inativo.");
    }

    private static void Apply(Campaign c, SaveCampaignRequest r)
    {
        c.Name = r.Name.Trim();
        c.Objective = r.Objective;
        c.Platform = r.Platform;
        c.StartDate = r.StartDate;
        c.EndDate = r.EndDate;
        c.PlannedBudget = r.PlannedBudget;
    }

    private static DateOnly Min(DateOnly a, DateOnly b) => a < b ? a : b;
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
