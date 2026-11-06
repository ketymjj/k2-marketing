using K2Marketing.Application.Common;
using K2Marketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Analytics;

public record CampaignRoasDto(Guid CampaignId, string Name, string ClientName, AdPlatform Platform, decimal Spend, decimal Revenue, long Conversions, decimal? Roas);
public record PlatformBreakdownDto(AdPlatform Platform, MetricsDto Metrics);
public record BudgetSliceDto(string Key, decimal Planned, decimal Spent);
/// <summary>Dados do CRM (cadastros no K2), exibidos separados dos números da plataforma para não haver dupla contagem.</summary>
public record CrmSummaryDto(int LeadsRegistered, int UniqueLeadsConverted, int ConversionsRegistered, int Sales, decimal SalesRevenue);
public record SourceCountDto(DataSource Source, int Rows);

public record DashboardDto(
    DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo,
    int ActiveClients, int ActiveCampaigns,
    MetricsDto Metrics, MetricsDto Previous,
    List<DailyPoint> Daily, List<CampaignRoasDto> RoasByCampaign, List<PlatformBreakdownDto> ByPlatform,
    List<BudgetSliceDto> BudgetDistribution, CrmSummaryDto Crm, List<SourceCountDto> DataSources);

public class DashboardQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public AdPlatform? Platform { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>Indicadores executivos calculados exclusivamente a partir dos registros persistidos.</summary>
public class DashboardService(IAppDbContext db, AccessScope scope, IClock clock)
{
    public async Task<DashboardDto> GetAsync(DashboardQuery q, CancellationToken ct)
    {
        if (q.ClientId is { } cid) scope.EnsureClient(cid);
        var range = DateRange.Resolve(q.From, q.To, clock.Today);
        var prev = new DateRange(range.From.AddDays(-range.Days), range.From.AddDays(-1));
        var filter = new MetricFilter { ClientId = q.ClientId, CampaignId = q.CampaignId, Platform = q.Platform };

        var baseMetrics = scope.Metrics(db.CampaignMetrics.AsNoTracking()).Apply(filter);
        var current = baseMetrics.Where(m => m.Date >= range.From && m.Date <= range.To);
        var totals = await current.TotalsAsync(ct);
        var previous = await baseMetrics.Where(m => m.Date >= prev.From && m.Date <= prev.To).TotalsAsync(ct);
        var daily = await current.DailyAsync(range.From, range.To, ct);

        var campaigns = scope.Campaigns(db.Campaigns.AsNoTracking());
        if (q.ClientId is { } clientId) campaigns = campaigns.Where(c => c.ClientId == clientId);
        if (q.CampaignId is { } campaignId) campaigns = campaigns.Where(c => c.Id == campaignId);
        if (q.Platform is { } platform) campaigns = campaigns.Where(c => c.Platform == platform);
        var activeCampaigns = await campaigns.CountAsync(c => c.Status == CampaignStatus.Active, ct);
        var activeClients = await scope.Clients(db.Clients.AsNoTracking())
            .Where(c => c.Status == ClientStatus.Active && (q.ClientId == null || c.Id == q.ClientId))
            .Where(c => q.Platform == null && q.CampaignId == null || c.Campaigns.Any(x => (q.Platform == null || x.Platform == q.Platform) && (q.CampaignId == null || x.Id == q.CampaignId)))
            .CountAsync(ct);

        var byCampaign = await current.ByCampaignAsync(ct);
        var campaignIds = byCampaign.Keys.ToList();
        var campaignInfo = await campaigns.Where(c => campaignIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, ClientName = c.Client!.Name, c.Platform }).ToListAsync(ct);
        var roas = campaignInfo
            .Select(c => (c, t: byCampaign[c.Id]))
            .OrderByDescending(x => x.t.Spend).Take(10)
            .Select(x => new CampaignRoasDto(x.c.Id, x.c.Name, x.c.ClientName, x.c.Platform, x.t.Spend, x.t.AttributedRevenue, x.t.Conversions, x.t.Roas))
            .ToList();

        var byPlatform = (await current.ByPlatformAsync(ct))
            .OrderByDescending(p => p.Value.Spend).Select(p => new PlatformBreakdownDto(p.Key, MetricsDto.From(p.Value))).ToList();

        // Distribuição do orçamento planejado (orçamentos vigentes no período) por plataforma.
        var budgets = scope.Budgets(db.CampaignBudgets.AsNoTracking()).Where(b => b.StartDate <= range.To && b.EndDate >= range.From);
        if (q.ClientId is { } bc) budgets = budgets.Where(b => b.Campaign!.ClientId == bc);
        if (q.CampaignId is { } bcamp) budgets = budgets.Where(b => b.CampaignId == bcamp);
        if (q.Platform is { } bp) budgets = budgets.Where(b => b.Campaign!.Platform == bp);
        var distribution = (await budgets.GroupBy(b => b.Campaign!.Platform)
                .Select(g => new { g.Key, P = g.Sum(b => b.PlannedAmount), S = g.Sum(b => b.ActualSpend) }).ToListAsync(ct))
            .OrderByDescending(x => x.P).Select(x => new BudgetSliceDto(x.Key.ToString(), x.P, x.S)).ToList();

        var crm = await CrmAsync(q, range, ct);

        var sources = await current.GroupBy(m => m.Source).Select(g => new SourceCountDto(g.Key, g.Count())).ToListAsync(ct);

        return new DashboardDto(range.From, range.To, prev.From, prev.To, activeClients, activeCampaigns,
            MetricsDto.From(totals), MetricsDto.From(previous), daily, roas, byPlatform, distribution, crm, sources);
    }

    public async Task<CrmSummaryDto> CrmAsync(DashboardQuery q, DateRange range, CancellationToken ct)
    {
        var from = range.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = range.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var leads = scope.Leads(db.Leads.AsNoTracking()).Where(l => l.CreatedAt >= from && l.CreatedAt < to);
        if (q.ClientId is { } c1) leads = leads.Where(l => l.ClientId == c1);
        if (q.CampaignId is { } k1) leads = leads.Where(l => l.CampaignId == k1);
        if (q.Platform is { } p1) leads = leads.Where(l => l.Campaign != null && l.Campaign.Platform == p1);

        var conversions = scope.Conversions(db.Conversions.AsNoTracking()).Where(c => c.ConversionDate >= from && c.ConversionDate < to);
        if (q.ClientId is { } c2) conversions = conversions.Where(c => c.Campaign!.ClientId == c2);
        if (q.CampaignId is { } k2) conversions = conversions.Where(c => c.CampaignId == k2);
        if (q.Platform is { } p2) conversions = conversions.Where(c => c.Campaign!.Platform == p2);

        var leadsCount = await leads.CountAsync(ct);
        var conversionsCount = await conversions.CountAsync(ct);
        var unique = await conversions.Where(c => c.LeadId != null).Select(c => c.LeadId).Distinct().CountAsync(ct);
        var sales = conversions.Where(c => c.ConversionType == ConversionType.Purchase);
        var salesCount = await sales.CountAsync(ct);
        var salesRevenue = await sales.SumAsync(c => (decimal?)c.RevenueAmount, ct) ?? 0m;
        return new CrmSummaryDto(leadsCount, unique, conversionsCount, salesCount, salesRevenue);
    }
}
