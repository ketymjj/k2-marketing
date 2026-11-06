using System.Globalization;
using System.Text;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Common;
using K2Marketing.Application.Imports;
using K2Marketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Reports;

public class ReportQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public AdPlatform? Platform { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>client | campaign | platform</summary>
    public string? GroupBy { get; set; }
}

public record PerformanceRowDto(string Key, string Label, string? Detail, MetricsDto Metrics);
public record PerformanceReportDto(DateOnly From, DateOnly To, string GroupBy, List<PerformanceRowDto> Rows, MetricsDto Totals);

public record CountItem(string Key, string? Label, int Count, int Converted);
public record DayCount(DateOnly Date, int Count, decimal Revenue);
public record LeadsReportDto(DateOnly From, DateOnly To, int Total, int Converted, decimal? ConversionRate, List<CountItem> ByStatus,
    List<CountItem> BySource, List<CountItem> ByCampaign, List<DayCount> Daily, long PlatformLeads);
public record ConversionsReportDto(DateOnly From, DateOnly To, int Total, int UniqueLeads, int Sales, decimal SalesRevenue, decimal TotalRevenue,
    List<CountItem> ByType, List<CountItem> ByCampaign, List<DayCount> Daily);

public record ObservationDto(string Code, Dictionary<string, object?> Params, string Tone);
public record AgencyBrandDto(string AgencyName, string PrimaryColor, string? ContactEmail, string? Website, string? ReportFooter);

public record ExecutiveReportDto(AgencyBrandDto Agency, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo,
    string? ClientName, MetricsDto Metrics, MetricsDto Previous, List<DailyPoint> Daily, List<PlatformBreakdownDto> ByPlatform,
    List<PerformanceRowDto> TopCampaigns, BudgetSummaryDto Budget, CrmSummaryDto Crm, List<ObservationDto> Observations, DateTime GeneratedAt);

public class ReportService(IAppDbContext db, AccessScope scope, IClock clock, DashboardService dashboard, BudgetService budgets)
{
    public async Task<PerformanceReportDto> PerformanceAsync(ReportQuery q, CancellationToken ct)
    {
        var (range, metrics) = Prepare(q);
        var group = q.GroupBy?.ToLowerInvariant() switch { "client" => "client", "platform" => "platform", _ => "campaign" };
        List<PerformanceRowDto> rows;
        switch (group)
        {
            case "client":
                var byClient = await metrics.ByClientAsync(ct);
                var clientIds = byClient.Keys.ToList();
                var clients = await db.Clients.AsNoTracking().Where(c => clientIds.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name, c.Industry }).ToDictionaryAsync(c => c.Id, ct);
                rows = byClient.Select(x => new PerformanceRowDto(x.Key.ToString(), clients[x.Key].Name, clients[x.Key].Industry, MetricsDto.From(x.Value))).ToList();
                break;
            case "platform":
                rows = (await metrics.ByPlatformAsync(ct)).Select(x => new PerformanceRowDto(x.Key.ToString(), x.Key.ToString(), null, MetricsDto.From(x.Value))).ToList();
                break;
            default:
                var byCampaign = await metrics.ByCampaignAsync(ct);
                var ids = byCampaign.Keys.ToList();
                var info = await db.Campaigns.AsNoTracking().Where(c => ids.Contains(c.Id))
                    .Select(c => new { c.Id, c.Name, Client = c.Client!.Name, c.Platform }).ToDictionaryAsync(c => c.Id, ct);
                rows = byCampaign.Select(x => new PerformanceRowDto(x.Key.ToString(), info[x.Key].Name, $"{info[x.Key].Client} · {info[x.Key].Platform}",
                    MetricsDto.From(x.Value))).ToList();
                break;
        }
        var totals = await metrics.TotalsAsync(ct);
        return new PerformanceReportDto(range.From, range.To, group, rows.OrderByDescending(r => r.Metrics.Spend).ToList(), MetricsDto.From(totals));
    }

    public Task<BudgetSummaryDto> BudgetAsync(ReportQuery q, CancellationToken ct)
    {
        var range = DateRange.Resolve(q.From, q.To, clock.Today);
        return budgets.SummaryAsync(new BudgetQuery { ClientId = q.ClientId, CampaignId = q.CampaignId, From = range.From, To = range.To, PageSize = 500 }, ct);
    }

    public async Task<LeadsReportDto> LeadsAsync(ReportQuery q, CancellationToken ct)
    {
        var range = DateRange.Resolve(q.From, q.To, clock.Today);
        var from = range.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = range.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var leads = scope.Leads(db.Leads.AsNoTracking()).Where(l => l.CreatedAt >= from && l.CreatedAt < to);
        if (q.ClientId is { } c) leads = leads.Where(l => l.ClientId == c);
        if (q.CampaignId is { } k) leads = leads.Where(l => l.CampaignId == k);
        if (q.Platform is { } p) leads = leads.Where(l => l.Campaign != null && l.Campaign.Platform == p);

        var total = await leads.CountAsync(ct);
        var converted = await leads.CountAsync(l => l.Status == LeadStatus.Converted, ct);
        var byStatus = await leads.GroupBy(l => l.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        var bySource = await leads.GroupBy(l => l.Source ?? "—")
            .Select(g => new CountItem(g.Key, null, g.Count(), g.Count(l => l.Status == LeadStatus.Converted))).ToListAsync(ct);
        var byCampaign = await leads.GroupBy(l => new { l.CampaignId, Name = l.Campaign != null ? l.Campaign.Name : null })
            .Select(g => new CountItem(g.Key.CampaignId.HasValue ? g.Key.CampaignId.Value.ToString() : "-", g.Key.Name, g.Count(), g.Count(l => l.Status == LeadStatus.Converted)))
            .ToListAsync(ct);
        var dailyRaw = await leads.Select(l => l.CreatedAt).ToListAsync(ct);
        var daily = Enumerable.Range(0, range.Days).Select(i => range.From.AddDays(i))
            .Select(d => new DayCount(d, dailyRaw.Count(x => DateOnly.FromDateTime(x) == d), 0m)).ToList();

        var (_, metrics) = Prepare(q);
        var platformLeads = (await metrics.TotalsAsync(ct)).Leads;

        return new LeadsReportDto(range.From, range.To, total, converted, total == 0 ? null : Math.Round(converted * 100m / total, 2),
            Enum.GetValues<LeadStatus>().Select(s => new CountItem(s.ToString(), null, byStatus.FirstOrDefault(x => x.Key == s)?.N ?? 0, 0)).ToList(),
            bySource.OrderByDescending(x => x.Count).ToList(), byCampaign.OrderByDescending(x => x.Count).ToList(), daily, platformLeads);
    }

    public async Task<ConversionsReportDto> ConversionsAsync(ReportQuery q, CancellationToken ct)
    {
        var range = DateRange.Resolve(q.From, q.To, clock.Today);
        var from = range.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = range.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var conv = scope.Conversions(db.Conversions.AsNoTracking()).Where(c => c.ConversionDate >= from && c.ConversionDate < to);
        if (q.ClientId is { } c1) conv = conv.Where(c => c.Campaign!.ClientId == c1);
        if (q.CampaignId is { } k) conv = conv.Where(c => c.CampaignId == k);
        if (q.Platform is { } p) conv = conv.Where(c => c.Campaign!.Platform == p);

        var byType = await conv.GroupBy(c => c.ConversionType).Select(g => new { g.Key, N = g.Count(), R = g.Sum(c => c.RevenueAmount) }).ToListAsync(ct);
        var byCampaign = await conv.GroupBy(c => new { c.CampaignId, c.Campaign!.Name })
            .Select(g => new CountItem(g.Key.CampaignId.ToString(), g.Key.Name, g.Count(), g.Count(c => c.ConversionType == ConversionType.Purchase))).ToListAsync(ct);
        var unique = await conv.Where(c => c.LeadId != null).Select(c => c.LeadId).Distinct().CountAsync(ct);
        var raw = await conv.Select(c => new { c.ConversionDate, c.RevenueAmount }).ToListAsync(ct);
        var daily = Enumerable.Range(0, range.Days).Select(i => range.From.AddDays(i)).Select(d =>
        {
            var day = raw.Where(x => DateOnly.FromDateTime(x.ConversionDate) == d).ToList();
            return new DayCount(d, day.Count, day.Sum(x => x.RevenueAmount));
        }).ToList();
        var sales = byType.FirstOrDefault(t => t.Key == ConversionType.Purchase);
        return new ConversionsReportDto(range.From, range.To, byType.Sum(t => t.N), unique, sales?.N ?? 0, sales?.R ?? 0m, byType.Sum(t => t.R),
            byType.OrderByDescending(t => t.N).Select(t => new CountItem(t.Key.ToString(), null, t.N, 0)).ToList(),
            byCampaign.OrderByDescending(x => x.Count).ToList(), daily);
    }

    public async Task<ExecutiveReportDto> ExecutiveAsync(ReportQuery q, CancellationToken ct)
    {
        var dash = await dashboard.GetAsync(new DashboardQuery { ClientId = q.ClientId, CampaignId = q.CampaignId, Platform = q.Platform, From = q.From, To = q.To }, ct);
        var perf = await PerformanceAsync(new ReportQuery { ClientId = q.ClientId, CampaignId = q.CampaignId, Platform = q.Platform, From = dash.From, To = dash.To, GroupBy = "campaign" }, ct);
        var budget = await BudgetAsync(new ReportQuery { ClientId = q.ClientId, CampaignId = q.CampaignId, From = dash.From, To = dash.To }, ct);
        var settings = await db.AgencySettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new Domain.Entities.AgencySettings();
        var clientName = q.ClientId is { } cid ? await db.Clients.Where(c => c.Id == cid).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;

        return new ExecutiveReportDto(
            new AgencyBrandDto(settings.AgencyName, settings.PrimaryColor, settings.ContactEmail, settings.Website, settings.ReportFooter),
            dash.From, dash.To, dash.PreviousFrom, dash.PreviousTo, clientName, dash.Metrics, dash.Previous, dash.Daily, dash.ByPlatform,
            perf.Rows.Take(10).ToList(), budget, dash.Crm, Observe(dash, perf, budget), clock.UtcNow);
    }

    /// <summary>Observações objetivas geradas por regras sobre os números do período (sem juízo subjetivo).</summary>
    public static List<ObservationDto> Observe(DashboardDto dash, PerformanceReportDto perf, BudgetSummaryDto budget)
    {
        var obs = new List<ObservationDto>();
        var m = dash.Metrics;
        if (m.Impressions == 0 && m.Spend == 0)
        {
            obs.Add(new ObservationDto("noData", new(), "neutral"));
            return obs;
        }

        decimal? Change(decimal cur, decimal prev) => prev == 0 ? null : Math.Round((cur - prev) / prev * 100m, 1);
        if (Change(m.Spend, dash.Previous.Spend) is { } spendChange && Math.Abs(spendChange) >= 10)
            obs.Add(new ObservationDto("spendChange", new() { ["pct"] = spendChange }, "neutral"));
        if (m.Roas is { } roas && dash.Previous.Roas is { } prevRoas && prevRoas > 0)
        {
            var pct = Math.Round((roas - prevRoas) / prevRoas * 100m, 1);
            if (Math.Abs(pct) >= 10) obs.Add(new ObservationDto("roasChange", new() { ["pct"] = pct, ["roas"] = roas }, pct > 0 ? "positive" : "negative"));
        }
        if (m.Ctr is { } ctr && ctr < 1m && m.Impressions >= 10_000)
            obs.Add(new ObservationDto("lowCtr", new() { ["ctr"] = ctr }, "negative"));

        var eligible = perf.Rows.Where(r => r.Metrics.Clicks >= ComparisonService.MinClicks && r.Metrics.Spend > 0).ToList();
        if (eligible.Where(r => r.Metrics.Roas.HasValue && r.Metrics.AttributedRevenue > 0).MaxBy(r => r.Metrics.Roas) is { } best)
            obs.Add(new ObservationDto("bestRoas", new() { ["campaign"] = best.Label, ["roas"] = best.Metrics.Roas, ["spend"] = best.Metrics.Spend }, "positive"));
        if (eligible.Count >= 2 && eligible.Where(r => r.Metrics.Cpa.HasValue).MaxBy(r => r.Metrics.Cpa) is { } worst && m.Cpa is { } avgCpa && worst.Metrics.Cpa > avgCpa * 1.5m)
            obs.Add(new ObservationDto("highCpa", new() { ["campaign"] = worst.Label, ["cpa"] = worst.Metrics.Cpa, ["avg"] = avgCpa }, "negative"));
        var zeroConv = perf.Rows.Count(r => r.Metrics.Spend > 0 && r.Metrics.Conversions == 0);
        if (zeroConv > 0) obs.Add(new ObservationDto("noConversions", new() { ["count"] = zeroConv }, "negative"));
        if (budget.Over90 > 0) obs.Add(new ObservationDto("budgetsAtRisk", new() { ["count"] = budget.Over90, ["over100"] = budget.Over100 }, "negative"));
        if (m.Leads > 0 && dash.Crm.LeadsRegistered > 0)
        {
            var gap = Math.Round((decimal)(m.Leads - dash.Crm.LeadsRegistered) / m.Leads * 100m, 1);
            if (Math.Abs(gap) >= 20)
                obs.Add(new ObservationDto("crmGap", new() { ["platform"] = m.Leads, ["crm"] = dash.Crm.LeadsRegistered, ["pct"] = gap }, "neutral"));
        }
        if (dash.DataSources.Any(s => s.Source == DataSource.Demo))
            obs.Add(new ObservationDto("demoData", new(), "neutral"));
        return obs;
    }

    // ------------------------------------------------------------------ CSV

    public async Task<(string FileName, string Content)> ExportCsvAsync(string type, ReportQuery q, CancellationToken ct)
    {
        var sb = new StringBuilder();
        string name;
        switch (type.ToLowerInvariant())
        {
            case "performance":
                var perf = await PerformanceAsync(q, ct);
                name = $"desempenho-{perf.GroupBy}-{perf.From:yyyyMMdd}-{perf.To:yyyyMMdd}.csv";
                sb.AppendLine("item,detalhe,impressoes,alcance,cliques,investimento,leads,conversoes,receita_atribuida,ctr_pct,cpc,cpm,cpl,cpa,roas");
                foreach (var r in perf.Rows) sb.AppendLine(MetricsLine(r.Label, r.Detail, r.Metrics));
                sb.AppendLine(MetricsLine("TOTAL", null, perf.Totals));
                break;
            case "budget":
                var b = await BudgetAsync(q, ct);
                name = $"orcamentos-{DateTime.UtcNow:yyyyMMdd}.csv";
                sb.AppendLine("campanha,cliente,plataforma,inicio,fim,planejado,realizado,saldo,consumo_pct");
                foreach (var i in b.Items)
                    sb.AppendLine(string.Join(',', CsvParser.Escape(i.CampaignName), CsvParser.Escape(i.ClientName), i.Platform, i.StartDate.ToString("yyyy-MM-dd"),
                        i.EndDate.ToString("yyyy-MM-dd"), D(i.PlannedAmount), D(i.ActualSpend), D(i.Remaining), D(i.UsagePercent)));
                break;
            case "leads":
                var l = await LeadsAsync(q, ct);
                name = $"leads-{l.From:yyyyMMdd}-{l.To:yyyyMMdd}.csv";
                sb.AppendLine("dimensao,item,leads,convertidos");
                foreach (var s in l.ByStatus) sb.AppendLine($"status,{s.Key},{s.Count},");
                foreach (var s in l.BySource) sb.AppendLine($"origem,{CsvParser.Escape(s.Key)},{s.Count},{s.Converted}");
                foreach (var s in l.ByCampaign) sb.AppendLine($"campanha,{CsvParser.Escape(s.Label ?? "(sem campanha)")},{s.Count},{s.Converted}");
                break;
            case "conversions":
                var c = await ConversionsAsync(q, ct);
                name = $"conversoes-{c.From:yyyyMMdd}-{c.To:yyyyMMdd}.csv";
                sb.AppendLine("data,conversoes,receita");
                foreach (var d in c.Daily) sb.AppendLine($"{d.Date:yyyy-MM-dd},{d.Count},{D(d.Revenue)}");
                break;
            default:
                throw new NotFoundException("Relatório", type);
        }
        return (name, sb.ToString());
    }

    private static string MetricsLine(string label, string? detail, MetricsDto m) => string.Join(',',
        CsvParser.Escape(label), CsvParser.Escape(detail), m.Impressions, m.Reach, m.Clicks, D(m.Spend), m.Leads, m.Conversions, D(m.AttributedRevenue),
        D(m.Ctr), D(m.Cpc), D(m.Cpm), D(m.Cpl), D(m.Cpa), D(m.Roas));

    private static string D(decimal? v) => v?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;

    private (DateRange Range, IQueryable<Domain.Entities.CampaignMetric> Metrics) Prepare(ReportQuery q)
    {
        if (q.ClientId is { } cid) scope.EnsureClient(cid);
        var range = DateRange.Resolve(q.From, q.To, clock.Today);
        var metrics = scope.Metrics(db.CampaignMetrics.AsNoTracking())
            .Apply(new MetricFilter { ClientId = q.ClientId, CampaignId = q.CampaignId, Platform = q.Platform, From = range.From, To = range.To });
        return (range, metrics);
    }
}
