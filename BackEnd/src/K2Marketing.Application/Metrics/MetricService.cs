using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Metrics;

/// <summary>Linha de métrica: valores informados (manual/CSV) + indicadores calculados na leitura.</summary>
public record MetricRowDto(Guid Id, Guid CampaignId, string CampaignName, Guid ClientId, string ClientName, Guid? AdvertisementId,
    string? AdvertisementName, DateOnly Date, long Impressions, long Reach, long Clicks, decimal Spend, long Leads, long Conversions,
    decimal AttributedRevenue, DataSource Source, decimal? Ctr, decimal? Cpc, decimal? Cpm, decimal? Cpl, decimal? Cpa, decimal? Roas,
    DateTime CreatedAt, DateTime? UpdatedAt);

public record SaveMetricRequest(Guid CampaignId, Guid? AdvertisementId, DateOnly Date, long Impressions, long Reach, long Clicks,
    decimal Spend, long Leads, long Conversions, decimal AttributedRevenue);

public class MetricQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? AdvertisementId { get; set; }
    public AdPlatform? Platform { get; set; }
    public DataSource? Source { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveMetricValidator : AbstractValidator<SaveMetricRequest>
{
    public SaveMetricValidator(IClock clock)
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.Date).LessThanOrEqualTo(_ => clock.Today).WithMessage("Não é possível registrar métricas em data futura.");
        RuleFor(x => x.Impressions).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Reach).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.Impressions).WithMessage("Alcance não pode ser maior que impressões.");
        RuleFor(x => x.Clicks).GreaterThanOrEqualTo(0).LessThanOrEqualTo(x => x.Impressions).WithMessage("Cliques não podem ser maiores que impressões.");
        RuleFor(x => x.Spend).GreaterThanOrEqualTo(0).LessThan(100_000_000m);
        RuleFor(x => x.Leads).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Conversions).GreaterThanOrEqualTo(0);
        RuleFor(x => x.AttributedRevenue).GreaterThanOrEqualTo(0).LessThan(1_000_000_000m);
    }
}

public class MetricService(
    IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, BudgetSyncService budgetSync, IValidator<SaveMetricRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<CampaignMetric, object?>>> Sort = new()
    {
        ["date"] = m => m.Date, ["campaign"] = m => m.Campaign!.Name, ["impressions"] = m => m.Impressions, ["clicks"] = m => m.Clicks,
        ["spend"] = m => m.Spend, ["leads"] = m => m.Leads, ["conversions"] = m => m.Conversions, ["revenue"] = m => m.AttributedRevenue
    };

    public async Task<PagedResult<MetricRowDto>> ListAsync(MetricQuery q, CancellationToken ct)
    {
        var query = Filter(q);
        var page = await query.ApplySort(q, Sort, "date", defaultDescending: true)
            .Select(m => new { M = m, CampaignName = m.Campaign!.Name, m.Campaign.ClientId, ClientName = m.Campaign.Client!.Name,
                AdName = m.Advertisement != null ? m.Advertisement.Name : null })
            .ToPagedAsync(q, ct);
        var items = page.Items.Select(x => ToDto(x.M, x.CampaignName, x.ClientId, x.ClientName, x.AdName)).ToList();
        return new PagedResult<MetricRowDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public async Task<MetricsDto> TotalsAsync(MetricQuery q, CancellationToken ct) => MetricsDto.From(await Filter(q).TotalsAsync(ct));

    public async Task<MetricRowDto> GetAsync(Guid id, CancellationToken ct)
    {
        var x = await scope.Metrics(db.CampaignMetrics.AsNoTracking()).Where(m => m.Id == id)
            .Select(m => new { M = m, CampaignName = m.Campaign!.Name, m.Campaign.ClientId, ClientName = m.Campaign.Client!.Name,
                AdName = m.Advertisement != null ? m.Advertisement.Name : null })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Métrica", id);
        return ToDto(x.M, x.CampaignName, x.ClientId, x.ClientName, x.AdName);
    }

    /// <summary>Registra métricas de um dia. Já existindo linha para campanha + anúncio + data, responde 409 (use PUT para corrigir).</summary>
    public async Task<MetricRowDto> CreateAsync(SaveMetricRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureReferencesAsync(r, ct);
        if (await db.CampaignMetrics.AnyAsync(m => m.CampaignId == r.CampaignId && m.AdvertisementId == r.AdvertisementId && m.Date == r.Date, ct))
            throw new ConflictException("metric.duplicate", "Já existem métricas para esta campanha/anúncio nesta data. Edite o registro existente.");
        var metric = new CampaignMetric { CampaignId = r.CampaignId, AdvertisementId = r.AdvertisementId, Source = DataSource.Manual, CreatedAt = clock.UtcNow };
        Apply(metric, r);
        db.CampaignMetrics.Add(metric);
        audit.Log("Create", "CampaignMetric", metric.Id, $"{r.Date} spend={r.Spend:F2}");
        await db.SaveChangesAsync(ct);
        await budgetSync.SyncAsync([r.CampaignId], ct);
        return await GetAsync(metric.Id, ct);
    }

    public async Task<MetricRowDto> UpdateAsync(Guid id, SaveMetricRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var metric = await db.CampaignMetrics.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException("Métrica", id);
        if (metric.CampaignId != r.CampaignId || metric.AdvertisementId != r.AdvertisementId || metric.Date != r.Date)
        {
            await EnsureReferencesAsync(r, ct);
            if (await db.CampaignMetrics.AnyAsync(m => m.Id != id && m.CampaignId == r.CampaignId && m.AdvertisementId == r.AdvertisementId && m.Date == r.Date, ct))
                throw new ConflictException("metric.duplicate", "Já existem métricas para esta campanha/anúncio nesta data.");
        }
        var previousCampaign = metric.CampaignId;
        metric.CampaignId = r.CampaignId;
        metric.AdvertisementId = r.AdvertisementId;
        Apply(metric, r);
        metric.Source = DataSource.Manual;
        metric.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "CampaignMetric", id, $"{r.Date} spend={r.Spend:F2}");
        await db.SaveChangesAsync(ct);
        await budgetSync.SyncAsync([previousCampaign, r.CampaignId], ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var metric = await db.CampaignMetrics.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException("Métrica", id);
        db.CampaignMetrics.Remove(metric);
        audit.Log("Delete", "CampaignMetric", id, metric.Date.ToString("yyyy-MM-dd"));
        await db.SaveChangesAsync(ct);
        await budgetSync.SyncAsync([metric.CampaignId], ct);
    }

    private IQueryable<CampaignMetric> Filter(MetricQuery q)
    {
        var query = scope.Metrics(db.CampaignMetrics.AsNoTracking()).Apply(new MetricFilter
        {
            ClientId = q.ClientId, CampaignId = q.CampaignId, AdvertisementId = q.AdvertisementId, Platform = q.Platform, From = q.From, To = q.To
        });
        if (q.Source is { } source) query = query.Where(m => m.Source == source);
        return query;
    }

    private async Task EnsureReferencesAsync(SaveMetricRequest r, CancellationToken ct)
    {
        var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CampaignId, ct) ?? throw new NotFoundException("Campanha", r.CampaignId);
        scope.EnsureClient(campaign.ClientId, "Campanha");
        if (r.AdvertisementId is { } adId && !await db.Advertisements.AnyAsync(a => a.Id == adId && a.CampaignId == r.CampaignId, ct))
            throw new DomainException("metric.adMismatch", "O anúncio não pertence à campanha informada.");
    }

    private static void Apply(CampaignMetric m, SaveMetricRequest r)
    {
        m.Date = r.Date;
        m.Impressions = r.Impressions;
        m.Reach = r.Reach;
        m.Clicks = r.Clicks;
        m.Spend = r.Spend;
        m.Leads = r.Leads;
        m.Conversions = r.Conversions;
        m.AttributedRevenue = r.AttributedRevenue;
    }

    private static MetricRowDto ToDto(CampaignMetric m, string campaignName, Guid clientId, string clientName, string? adName) =>
        new(m.Id, m.CampaignId, campaignName, clientId, clientName, m.AdvertisementId, adName, m.Date, m.Impressions, m.Reach, m.Clicks,
            m.Spend, m.Leads, m.Conversions, m.AttributedRevenue, m.Source,
            MetricCalculator.Ctr(m.Clicks, m.Impressions), MetricCalculator.Cpc(m.Spend, m.Clicks), MetricCalculator.Cpm(m.Spend, m.Impressions),
            MetricCalculator.Cpl(m.Spend, m.Leads), MetricCalculator.Cpa(m.Spend, m.Conversions), MetricCalculator.Roas(m.AttributedRevenue, m.Spend),
            m.CreatedAt, m.UpdatedAt);
}
