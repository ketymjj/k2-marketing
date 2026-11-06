using K2Marketing.Application.Common;
using K2Marketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Analytics;

public record CompareItemDto(Guid CampaignId, string Name, string ClientName, AdPlatform Platform, CampaignObjective Objective,
    CampaignStatus Status, MetricsDto Metrics, int DaysWithData, bool SufficientVolume, List<string> QualityFlags, List<DailyPoint> Daily);

/// <summary>Melhor valor objetivo de um indicador entre as campanhas elegíveis (com volume suficiente).</summary>
public record CompareHighlightDto(string Metric, Guid CampaignId, decimal Value, bool HigherIsBetter);

public record CompareWarningDto(string Code, string? Campaign);

public record CompareResultDto(DateOnly From, DateOnly To, List<CompareItemDto> Items, List<CompareHighlightDto> Highlights, List<CompareWarningDto> Warnings);

/// <summary>
/// Comparação de 2 a 4 campanhas. Os destaques são calculados apenas entre campanhas com volume mínimo
/// (≥ 100 cliques, ≥ 7 dias com dados e investimento &gt; 0). ROAS só é destacado quando todas têm receita atribuída,
/// e há avisos quando objetivos/plataformas diferem — maior ROAS não significa automaticamente "melhor campanha".
/// </summary>
public class ComparisonService(IAppDbContext db, AccessScope scope, IClock clock)
{
    public const int MinClicks = 100;
    public const int MinDays = 7;

    public async Task<CompareResultDto> CompareAsync(IReadOnlyCollection<Guid> ids, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count is < 2 or > 4) throw new DomainException("compare.count", "Selecione de 2 a 4 campanhas para comparar.");

        var campaigns = await scope.Campaigns(db.Campaigns.AsNoTracking()).Where(c => distinct.Contains(c.Id))
            .Select(c => new { c.Id, c.Name, ClientName = c.Client!.Name, c.Platform, c.Objective, c.Status, c.StartDate, c.EndDate }).ToListAsync(ct);
        if (campaigns.Count != distinct.Count) throw new NotFoundException("Campanha", string.Join(",", distinct.Except(campaigns.Select(c => c.Id))));

        // Sem período: da primeira data de início até hoje (máximo 180 dias).
        var end = to ?? clock.Today;
        var start = from ?? campaigns.Min(c => c.StartDate);
        if (end.DayNumber - start.DayNumber > 180 && from is null) start = end.AddDays(-179);
        var range = DateRange.Resolve(start, end, clock.Today);

        var metrics = db.CampaignMetrics.AsNoTracking().Where(m => distinct.Contains(m.CampaignId) && m.Date >= range.From && m.Date <= range.To);
        var totals = await metrics.ByCampaignAsync(ct);
        var days = await metrics.GroupBy(m => m.CampaignId).Select(g => new { g.Key, Days = g.Select(m => m.Date).Distinct().Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Days, ct);

        var items = new List<CompareItemDto>();
        foreach (var id in distinct)
        {
            var c = campaigns.First(x => x.Id == id);
            var t = totals.GetValueOrDefault(id, MetricTotals.Empty);
            var d = days.GetValueOrDefault(id);
            var flags = new List<string>();
            if (t.Clicks < MinClicks) flags.Add("lowClicks");
            if (d < MinDays) flags.Add("fewDays");
            if (t.Spend <= 0) flags.Add("noSpend");
            if (t.AttributedRevenue <= 0) flags.Add("noRevenue");
            if (t.Conversions == 0) flags.Add("noConversions");
            var sufficient = t.Clicks >= MinClicks && d >= MinDays && t.Spend > 0;
            var daily = await metrics.Where(m => m.CampaignId == id).DailyAsync(range.From, range.To, ct);
            items.Add(new CompareItemDto(id, c.Name, c.ClientName, c.Platform, c.Objective, c.Status, MetricsDto.From(t), d, sufficient, flags, daily));
        }

        var eligible = items.Where(i => i.SufficientVolume).ToList();
        var highlights = new List<CompareHighlightDto>();
        if (eligible.Count >= 2)
        {
            void Best(string metric, Func<MetricsDto, decimal?> value, bool higherIsBetter)
            {
                var candidates = eligible.Select(i => (i, v: value(i.Metrics))).Where(x => x.v.HasValue).ToList();
                if (candidates.Count < 2) return;
                var best = higherIsBetter ? candidates.MaxBy(x => x.v) : candidates.MinBy(x => x.v);
                // Empate não gera destaque.
                if (candidates.Count(x => x.v == best.v) > 1) return;
                highlights.Add(new CompareHighlightDto(metric, best.i.CampaignId, best.v!.Value, higherIsBetter));
            }
            Best("ctr", m => m.Ctr, true);
            Best("cpc", m => m.Cpc, false);
            Best("conversions", m => m.Conversions, true);
            Best("cpa", m => m.Cpa, false);
            if (eligible.All(i => i.Metrics.AttributedRevenue > 0)) Best("roas", m => m.Roas, true);
            Best("leads", m => m.Leads, true);
        }

        var warnings = new List<CompareWarningDto>();
        if (items.Select(i => i.Objective).Distinct().Count() > 1) warnings.Add(new CompareWarningDto("differentObjectives", null));
        if (items.Select(i => i.Platform).Distinct().Count() > 1) warnings.Add(new CompareWarningDto("differentPlatforms", null));
        warnings.AddRange(items.Where(i => !i.SufficientVolume).Select(i => new CompareWarningDto("lowVolume", i.Name)));
        if (items.Any(i => i.Metrics.AttributedRevenue <= 0)) warnings.Add(new CompareWarningDto("roasNotComparable", null));
        if (eligible.Count < 2) warnings.Add(new CompareWarningDto("noHighlights", null));

        return new CompareResultDto(range.From, range.To, items, highlights, warnings);
    }
}
