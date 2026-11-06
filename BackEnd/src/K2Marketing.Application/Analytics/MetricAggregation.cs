using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Analytics;

/// <summary>Totais informados + indicadores calculados (CTR, CPC, CPM, CPL, CPA, ROAS). Nulos quando o denominador é zero.</summary>
public record MetricsDto(
    long Impressions, long Reach, long Clicks, decimal Spend, long Leads, long Conversions, decimal AttributedRevenue,
    decimal? Ctr, decimal? Cpc, decimal? Cpm, decimal? Cpl, decimal? Cpa, decimal? Roas)
{
    public static MetricsDto From(MetricTotals t) => new(
        t.Impressions, t.Reach, t.Clicks, t.Spend, t.Leads, t.Conversions, t.AttributedRevenue,
        t.Ctr, t.Cpc, t.Cpm, t.Cpl, t.Cpa, t.Roas);

    public static readonly MetricsDto Empty = From(MetricTotals.Empty);
}

/// <summary>Filtros comuns de métricas (dashboard, relatórios, comparações).</summary>
public class MetricFilter
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? AdvertisementId { get; set; }
    public AdPlatform? Platform { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

/// <summary>
/// Soma as linhas de CampaignMetrics no banco. Os indicadores derivados são calculados a partir dos totais
/// (nunca média de médias), garantindo que CTR/CPC/ROAS agregados sejam matematicamente corretos.
/// </summary>
public static class MetricAggregation
{
    public static IQueryable<CampaignMetric> Apply(this IQueryable<CampaignMetric> q, MetricFilter f)
    {
        if (f.ClientId is { } clientId) q = q.Where(m => m.Campaign!.ClientId == clientId);
        if (f.CampaignId is { } campaignId) q = q.Where(m => m.CampaignId == campaignId);
        if (f.AdvertisementId is { } adId) q = q.Where(m => m.AdvertisementId == adId);
        if (f.Platform is { } platform) q = q.Where(m => m.Campaign!.Platform == platform);
        if (f.From is { } from) q = q.Where(m => m.Date >= from);
        if (f.To is { } to) q = q.Where(m => m.Date <= to);
        return q;
    }

    public static async Task<MetricTotals> TotalsAsync(this IQueryable<CampaignMetric> q, CancellationToken ct)
    {
        var r = await q.GroupBy(_ => 1).Select(g => new
        {
            I = g.Sum(x => x.Impressions), R = g.Sum(x => x.Reach), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).FirstOrDefaultAsync(ct);
        return r is null ? MetricTotals.Empty : new MetricTotals(r.I, r.R, r.C, r.S, r.L, r.V, r.Rev);
    }

    public static async Task<Dictionary<Guid, MetricTotals>> ByCampaignAsync(this IQueryable<CampaignMetric> q, CancellationToken ct)
    {
        var rows = await q.GroupBy(m => m.CampaignId).Select(g => new
        {
            g.Key, I = g.Sum(x => x.Impressions), R = g.Sum(x => x.Reach), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new MetricTotals(r.I, r.R, r.C, r.S, r.L, r.V, r.Rev));
    }

    public static async Task<Dictionary<Guid, MetricTotals>> ByAdAsync(this IQueryable<CampaignMetric> q, CancellationToken ct)
    {
        var rows = await q.Where(m => m.AdvertisementId != null).GroupBy(m => m.AdvertisementId!.Value).Select(g => new
        {
            g.Key, I = g.Sum(x => x.Impressions), R = g.Sum(x => x.Reach), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new MetricTotals(r.I, r.R, r.C, r.S, r.L, r.V, r.Rev));
    }

    public static async Task<Dictionary<Guid, MetricTotals>> ByClientAsync(this IQueryable<CampaignMetric> q, CancellationToken ct)
    {
        var rows = await q.GroupBy(m => m.Campaign!.ClientId).Select(g => new
        {
            g.Key, I = g.Sum(x => x.Impressions), R = g.Sum(x => x.Reach), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new MetricTotals(r.I, r.R, r.C, r.S, r.L, r.V, r.Rev));
    }

    public static async Task<Dictionary<AdPlatform, MetricTotals>> ByPlatformAsync(this IQueryable<CampaignMetric> q, CancellationToken ct)
    {
        var rows = await q.GroupBy(m => m.Campaign!.Platform).Select(g => new
        {
            g.Key, I = g.Sum(x => x.Impressions), R = g.Sum(x => x.Reach), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => new MetricTotals(r.I, r.R, r.C, r.S, r.L, r.V, r.Rev));
    }

    public static async Task<List<DailyPoint>> DailyAsync(this IQueryable<CampaignMetric> q, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var rows = await q.GroupBy(m => m.Date).Select(g => new
        {
            g.Key, I = g.Sum(x => x.Impressions), C = g.Sum(x => x.Clicks), S = g.Sum(x => x.Spend),
            L = g.Sum(x => x.Leads), V = g.Sum(x => x.Conversions), Rev = g.Sum(x => x.AttributedRevenue)
        }).ToDictionaryAsync(r => r.Key, ct);

        // Série contínua: dias sem métricas aparecem com zero (sem lacunas no gráfico).
        var list = new List<DailyPoint>();
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            list.Add(rows.TryGetValue(d, out var r)
                ? new DailyPoint(d, r.I, r.C, r.S, r.L, r.V, r.Rev, MetricCalculator.Roas(r.Rev, r.S))
                : new DailyPoint(d, 0, 0, 0m, 0, 0, 0m, null));
        }
        return list;
    }
}

public record DailyPoint(DateOnly Date, long Impressions, long Clicks, decimal Spend, long Leads, long Conversions, decimal Revenue, decimal? Roas);
