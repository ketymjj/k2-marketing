using System.Globalization;
using System.Text;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Common;
using K2Marketing.Application.Conversions;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Imports;

public enum ImportRowStatus { New, Duplicate, DuplicateInFile, Invalid }

/// <summary>Skip: mantém registros existentes. Overwrite: substitui métricas já existentes (conversões nunca são sobrescritas).</summary>
public enum DuplicateMode { Skip, Overwrite }

public record ImportRowDto(int Line, ImportRowStatus Status, string? Campaign, string? Advertisement, string? Date,
    IReadOnlyDictionary<string, string> Values, List<string> Errors);

public record ImportPreviewDto(string Kind, int TotalRows, int NewRows, int Duplicates, int DuplicatesInFile, int Invalid,
    List<string> MissingColumns, List<string> IgnoredColumns, List<ImportRowDto> Rows, MetricsDto? ValidTotals, decimal? ValidRevenue);

public record ImportResultDto(string Kind, int Inserted, int Updated, int Skipped, int Invalid);

/// <summary>
/// Importação CSV em duas etapas: <c>preview</c> (sem gravar) mostra validações e duplicidades;
/// <c>confirm</c> reprocessa o mesmo arquivo e grava as linhas válidas em uma única transação.
/// </summary>
public class ImportService(IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, BudgetSyncService budgetSync)
{
    public const long MaxFileBytes = 5 * 1024 * 1024;
    private const int PreviewRowLimit = 500;

    public static readonly string[] MetricColumns =
        ["date", "campaign_id", "campaign_name", "ad_id", "ad_name", "impressions", "reach", "clicks", "spend", "leads", "conversions", "attributed_revenue"];

    public static readonly string[] ConversionColumns =
        ["campaign_id", "campaign_name", "conversion_type", "conversion_date", "revenue_amount", "external_id", "lead_email"];

    private static readonly Dictionary<string, ConversionType> TypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lead"] = ConversionType.Lead, ["cadastro"] = ConversionType.SignUp, ["signup"] = ConversionType.SignUp,
        ["contato"] = ConversionType.Contact, ["contact"] = ConversionType.Contact, ["compra"] = ConversionType.Purchase,
        ["purchase"] = ConversionType.Purchase, ["venda"] = ConversionType.Purchase, ["agendamento"] = ConversionType.Appointment,
        ["appointment"] = ConversionType.Appointment, ["outro"] = ConversionType.Other, ["other"] = ConversionType.Other
    };

    // ------------------------------------------------------------------ Métricas

    private record ParsedMetric(CsvParser.CsvRow Row, Campaign? Campaign, Advertisement? Ad, SaveValues? Values, List<string> Errors)
    {
        public (Guid, Guid?, DateOnly)? Key => Campaign is not null && Values is not null ? (Campaign.Id, Ad?.Id, Values.Date) : null;
    }

    private record SaveValues(DateOnly Date, long Impressions, long Reach, long Clicks, decimal Spend, long Leads, long Conversions, decimal Revenue);

    public async Task<ImportPreviewDto> PreviewMetricsAsync(string csv, CancellationToken ct)
    {
        var (table, missing, ignored) = Read(csv, MetricColumns, ["date", "impressions", "clicks", "spend"], requireCampaign: true);
        if (missing.Count > 0) return new ImportPreviewDto("metrics", table.Rows.Count, 0, 0, 0, table.Rows.Count, missing, ignored, [], null, null);

        var parsed = await ParseMetricsAsync(table, ct);
        var statuses = await ClassifyMetricsAsync(parsed, ct);
        var valid = parsed.Where((p, i) => statuses[i] is ImportRowStatus.New or ImportRowStatus.Duplicate).Select(p => p.Values!).ToList();
        var totals = valid.Aggregate(MetricTotals.Empty, (acc, v) => acc.Add(new MetricTotals(v.Impressions, v.Reach, v.Clicks, v.Spend, v.Leads, v.Conversions, v.Revenue)));

        var rows = parsed.Take(PreviewRowLimit).Select((p, i) => new ImportRowDto(p.Row.LineNumber, statuses[i], p.Campaign?.Name, p.Ad?.Name,
            p.Values?.Date.ToString("yyyy-MM-dd") ?? p.Row.Get("date"), p.Row.Values, p.Errors)).ToList();
        return new ImportPreviewDto("metrics", parsed.Count, statuses.Count(s => s == ImportRowStatus.New), statuses.Count(s => s == ImportRowStatus.Duplicate),
            statuses.Count(s => s == ImportRowStatus.DuplicateInFile), statuses.Count(s => s == ImportRowStatus.Invalid), missing, ignored, rows,
            MetricsDto.From(totals), null);
    }

    public async Task<ImportResultDto> ConfirmMetricsAsync(string csv, DuplicateMode mode, CancellationToken ct)
    {
        var (table, missing, _) = Read(csv, MetricColumns, ["date", "impressions", "clicks", "spend"], requireCampaign: true);
        if (missing.Count > 0)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = [$"Colunas obrigatórias ausentes: {string.Join(", ", missing)}"] });

        var parsed = await ParseMetricsAsync(table, ct);
        var statuses = await ClassifyMetricsAsync(parsed, ct);
        int inserted = 0, updated = 0, skipped = 0;
        var now = clock.UtcNow;

        await using var tx = await db.BeginTransactionAsync(ct);
        for (var i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];
            switch (statuses[i])
            {
                case ImportRowStatus.New:
                    var metric = new CampaignMetric { CampaignId = p.Campaign!.Id, AdvertisementId = p.Ad?.Id, Source = DataSource.CsvImport, CreatedAt = now };
                    Fill(metric, p.Values!);
                    db.CampaignMetrics.Add(metric);
                    inserted++;
                    break;
                case ImportRowStatus.Duplicate when mode == DuplicateMode.Overwrite:
                    var (campaignId, adId, date) = p.Key!.Value;
                    var existing = await db.CampaignMetrics.FirstAsync(m => m.CampaignId == campaignId && m.AdvertisementId == adId && m.Date == date, ct);
                    Fill(existing, p.Values!);
                    existing.Source = DataSource.CsvImport;
                    existing.UpdatedAt = now;
                    updated++;
                    break;
                case ImportRowStatus.Duplicate or ImportRowStatus.DuplicateInFile:
                    skipped++;
                    break;
            }
        }
        var invalid = statuses.Count(s => s == ImportRowStatus.Invalid);
        audit.Log("ImportCsv", "CampaignMetric", null, $"inseridas={inserted} atualizadas={updated} ignoradas={skipped} inválidas={invalid} modo={mode}");
        await db.SaveChangesAsync(ct);
        await budgetSync.SyncAsync(parsed.Where(p => p.Campaign is not null).Select(p => p.Campaign!.Id), ct);
        await tx.CommitAsync(ct);
        return new ImportResultDto("metrics", inserted, updated, skipped, invalid);
    }

    /// <summary>CSV de exemplo com campanhas reais do banco (visíveis ao usuário) e 7 dias de valores ilustrativos.</summary>
    public async Task<string> MetricsTemplateAsync(CancellationToken ct)
    {
        var campaigns = await scope.Campaigns(db.Campaigns.AsNoTracking())
            .Where(c => c.Status == CampaignStatus.Active).OrderBy(c => c.Name).Take(3)
            .Select(c => new { c.Id, c.Name }).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("date,campaign_id,campaign_name,ad_name,impressions,reach,clicks,spend,leads,conversions,attributed_revenue");
        var today = clock.Today;
        var rnd = new Random(42);
        foreach (var c in campaigns.DefaultIfEmpty(new { Id = Guid.Empty, Name = "Nome da campanha" }))
        {
            for (var d = 7; d >= 1; d--)
            {
                var impressions = rnd.Next(4000, 20000);
                var clicks = (int)(impressions * (0.008 + rnd.NextDouble() * 0.02));
                var spend = Math.Round(clicks * (0.6m + (decimal)rnd.NextDouble() * 1.4m), 2);
                var leads = clicks / rnd.Next(8, 20);
                var conversions = leads / rnd.Next(2, 5);
                sb.AppendLine(string.Join(',', today.AddDays(-d).ToString("yyyy-MM-dd"), c.Id == Guid.Empty ? "" : c.Id.ToString(), CsvParser.Escape(c.Name), "",
                    impressions, (int)(impressions * 0.7), clicks, spend.ToString(CultureInfo.InvariantCulture), leads, conversions,
                    (conversions * rnd.Next(80, 400)).ToString(CultureInfo.InvariantCulture)));
            }
        }
        return sb.ToString();
    }

    private async Task<List<ParsedMetric>> ParseMetricsAsync(CsvParser.CsvTable table, CancellationToken ct)
    {
        var (byId, byName) = await LoadCampaignsAsync(ct);
        var campaignIds = byId.Keys.ToList();
        var ads = await db.Advertisements.AsNoTracking().Where(a => campaignIds.Contains(a.CampaignId)).ToListAsync(ct);
        var today = clock.Today;
        var result = new List<ParsedMetric>();

        foreach (var row in table.Rows)
        {
            var errors = new List<string>();
            var campaign = ResolveCampaign(row, byId, byName, errors);
            Advertisement? ad = null;
            if (campaign is not null)
            {
                if (Guid.TryParse(row.Get("ad_id"), out var adId))
                {
                    ad = ads.FirstOrDefault(a => a.Id == adId && a.CampaignId == campaign.Id);
                    if (ad is null) errors.Add("Anúncio (ad_id) não encontrado nesta campanha.");
                }
                else if (row.Get("ad_name") is { } adName)
                {
                    var matches = ads.Where(a => a.CampaignId == campaign.Id && string.Equals(a.Name, adName, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (matches.Count == 1) ad = matches[0];
                    else errors.Add(matches.Count == 0 ? $"Anúncio \"{adName}\" não encontrado nesta campanha." : $"Mais de um anúncio chamado \"{adName}\"; use ad_id.");
                }
            }

            if (!CsvParser.TryDate(row.Get("date"), out var date)) errors.Add("Data inválida (use yyyy-MM-dd ou dd/MM/yyyy).");
            else if (date > today) errors.Add("Data futura não é permitida.");

            long Num(string col, bool required)
            {
                var raw = row.Get(col);
                if (raw is null) { if (required) errors.Add($"{col} é obrigatório."); return 0; }
                if (!CsvParser.TryLong(raw, out var v) || v < 0) { errors.Add($"{col} deve ser um inteiro ≥ 0."); return 0; }
                return v;
            }
            decimal Money(string col, bool required)
            {
                var raw = row.Get(col);
                if (raw is null) { if (required) errors.Add($"{col} é obrigatório."); return 0; }
                if (!CsvParser.TryDecimal(raw, out var v) || v < 0) { errors.Add($"{col} deve ser um valor ≥ 0."); return 0; }
                return Math.Round(v, 2);
            }

            var impressions = Num("impressions", true);
            var reach = Num("reach", false);
            var clicks = Num("clicks", true);
            var spend = Money("spend", true);
            var leads = Num("leads", false);
            var conversions = Num("conversions", false);
            var revenue = Money("attributed_revenue", false);
            if (clicks > impressions) errors.Add("Cliques maiores que impressões.");
            if (reach > impressions) errors.Add("Alcance maior que impressões.");

            result.Add(new ParsedMetric(row, campaign, ad,
                errors.Count == 0 ? new SaveValues(date, impressions, reach, clicks, spend, leads, conversions, revenue) : null, errors));
        }
        return result;
    }

    private async Task<List<ImportRowStatus>> ClassifyMetricsAsync(List<ParsedMetric> parsed, CancellationToken ct)
    {
        var campaignIds = parsed.Where(p => p.Key is not null).Select(p => p.Campaign!.Id).Distinct().ToList();
        var dates = parsed.Where(p => p.Key is not null).Select(p => p.Values!.Date).ToList();
        var existing = new HashSet<(Guid, Guid?, DateOnly)>();
        if (campaignIds.Count > 0)
        {
            var min = dates.Min();
            var max = dates.Max();
            var rows = await db.CampaignMetrics.AsNoTracking()
                .Where(m => campaignIds.Contains(m.CampaignId) && m.Date >= min && m.Date <= max)
                .Select(m => new { m.CampaignId, m.AdvertisementId, m.Date }).ToListAsync(ct);
            foreach (var r in rows) existing.Add((r.CampaignId, r.AdvertisementId, r.Date));
        }
        var seen = new HashSet<(Guid, Guid?, DateOnly)>();
        return parsed.Select(p =>
        {
            if (p.Key is not { } key) return ImportRowStatus.Invalid;
            if (!seen.Add(key)) return ImportRowStatus.DuplicateInFile;
            return existing.Contains(key) ? ImportRowStatus.Duplicate : ImportRowStatus.New;
        }).ToList();
    }

    private static void Fill(CampaignMetric m, SaveValues v)
    {
        m.Date = v.Date;
        m.Impressions = v.Impressions;
        m.Reach = v.Reach;
        m.Clicks = v.Clicks;
        m.Spend = v.Spend;
        m.Leads = v.Leads;
        m.Conversions = v.Conversions;
        m.AttributedRevenue = v.Revenue;
    }

    // ------------------------------------------------------------------ Conversões

    private record ParsedConversion(CsvParser.CsvRow Row, Campaign? Campaign, Guid? LeadId, ConversionType Type, DateTime Date,
        decimal Revenue, string? ExternalId, string? Key, List<string> Errors);

    public async Task<ImportPreviewDto> PreviewConversionsAsync(string csv, CancellationToken ct)
    {
        var (table, missing, ignored) = Read(csv, ConversionColumns, ["conversion_type", "conversion_date"], requireCampaign: true);
        if (missing.Count > 0) return new ImportPreviewDto("conversions", table.Rows.Count, 0, 0, 0, table.Rows.Count, missing, ignored, [], null, null);
        var parsed = await ParseConversionsAsync(table, ct);
        var statuses = await ClassifyConversionsAsync(parsed, ct);
        var rows = parsed.Take(PreviewRowLimit).Select((p, i) => new ImportRowDto(p.Row.LineNumber, statuses[i], p.Campaign?.Name, null,
            p.Errors.Count == 0 ? p.Date.ToString("yyyy-MM-dd HH:mm") : p.Row.Get("conversion_date"), p.Row.Values, p.Errors)).ToList();
        var revenue = parsed.Where((_, i) => statuses[i] == ImportRowStatus.New).Sum(p => p.Revenue);
        return new ImportPreviewDto("conversions", parsed.Count, statuses.Count(s => s == ImportRowStatus.New), statuses.Count(s => s == ImportRowStatus.Duplicate),
            statuses.Count(s => s == ImportRowStatus.DuplicateInFile), statuses.Count(s => s == ImportRowStatus.Invalid), missing, ignored, rows, null, revenue);
    }

    public async Task<ImportResultDto> ConfirmConversionsAsync(string csv, CancellationToken ct)
    {
        var (table, missing, _) = Read(csv, ConversionColumns, ["conversion_type", "conversion_date"], requireCampaign: true);
        if (missing.Count > 0)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = [$"Colunas obrigatórias ausentes: {string.Join(", ", missing)}"] });
        var parsed = await ParseConversionsAsync(table, ct);
        var statuses = await ClassifyConversionsAsync(parsed, ct);
        var now = clock.UtcNow;
        var inserted = 0;

        await using var tx = await db.BeginTransactionAsync(ct);
        for (var i = 0; i < parsed.Count; i++)
        {
            if (statuses[i] != ImportRowStatus.New) continue;
            var p = parsed[i];
            db.Conversions.Add(new Conversion
            {
                CampaignId = p.Campaign!.Id, LeadId = p.LeadId, ConversionType = p.Type, ConversionDate = p.Date, RevenueAmount = p.Revenue,
                Source = DataSource.CsvImport, ExternalId = p.ExternalId, EventKey = p.Key!, CreatedAt = now
            });
            inserted++;
        }
        var invalid = statuses.Count(s => s == ImportRowStatus.Invalid);
        var skipped = statuses.Count(s => s is ImportRowStatus.Duplicate or ImportRowStatus.DuplicateInFile);
        audit.Log("ImportCsv", "Conversion", null, $"inseridas={inserted} ignoradas={skipped} inválidas={invalid}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new ImportResultDto("conversions", inserted, 0, skipped, invalid);
    }

    public string ConversionsTemplate() =>
        "campaign_id,campaign_name,conversion_type,conversion_date,revenue_amount,external_id,lead_email\n" +
        $",Nome da campanha,Purchase,{clock.Today.AddDays(-1):yyyy-MM-dd} 14:30,349.90,PED-1001,\n" +
        $",Nome da campanha,Lead,{clock.Today.AddDays(-1):yyyy-MM-dd},0,,cliente@exemplo.com\n";

    private async Task<List<ParsedConversion>> ParseConversionsAsync(CsvParser.CsvTable table, CancellationToken ct)
    {
        var (byId, byName) = await LoadCampaignsAsync(ct);
        var clientIds = byId.Values.Select(c => c.ClientId).Distinct().ToList();
        var emails = table.Rows.Select(r => r.Get("lead_email")?.ToLowerInvariant()).Where(e => e is not null).Distinct().ToList();
        var leads = await db.Leads.AsNoTracking().Where(l => l.Email != null && emails.Contains(l.Email) && clientIds.Contains(l.ClientId))
            .Select(l => new { l.Id, l.Email, l.ClientId }).ToListAsync(ct);
        var maxDate = clock.UtcNow.AddDays(1);
        var result = new List<ParsedConversion>();

        foreach (var row in table.Rows)
        {
            var errors = new List<string>();
            var campaign = ResolveCampaign(row, byId, byName, errors);
            var typeRaw = row.Get("conversion_type");
            var type = ConversionType.Other;
            if (typeRaw is null || !(TypeAliases.TryGetValue(typeRaw, out type) || Enum.TryParse(typeRaw, true, out type)))
                errors.Add("conversion_type inválido (Lead, SignUp, Contact, Purchase, Appointment, Other).");
            if (!CsvParser.TryDateTime(row.Get("conversion_date"), out var date)) errors.Add("conversion_date inválida.");
            else if (date > maxDate) errors.Add("Data futura não é permitida.");
            decimal revenue = 0;
            if (row.Get("revenue_amount") is { } rev && (!CsvParser.TryDecimal(rev, out revenue) || revenue < 0)) errors.Add("revenue_amount deve ser ≥ 0.");
            if (type == ConversionType.Purchase && revenue <= 0 && errors.Count == 0) errors.Add("Compra sem receita.");
            Guid? leadId = null;
            if (row.Get("lead_email") is { } email && campaign is not null)
            {
                var lead = leads.FirstOrDefault(l => l.ClientId == campaign.ClientId && string.Equals(l.Email, email, StringComparison.OrdinalIgnoreCase));
                if (lead is null) errors.Add($"Lead com e-mail {email} não encontrado para o cliente.");
                else leadId = lead.Id;
            }
            var externalId = row.Get("external_id");
            if (externalId?.Length > 100) errors.Add("external_id excede 100 caracteres.");
            revenue = Math.Round(revenue, 2);
            var key = errors.Count == 0 ? ConversionKeys.For(leadId, type, date, revenue, externalId) : null;
            result.Add(new ParsedConversion(row, campaign, leadId, type, date, revenue, externalId, key, errors));
        }
        return result;
    }

    private async Task<List<ImportRowStatus>> ClassifyConversionsAsync(List<ParsedConversion> parsed, CancellationToken ct)
    {
        var keys = parsed.Where(p => p.Key is not null).Select(p => p.Key!).Distinct().ToList();
        var campaignIds = parsed.Where(p => p.Key is not null).Select(p => p.Campaign!.Id).Distinct().ToList();
        var existing = (await db.Conversions.AsNoTracking().Where(c => campaignIds.Contains(c.CampaignId) && keys.Contains(c.EventKey))
            .Select(c => new { c.CampaignId, c.EventKey }).ToListAsync(ct)).Select(x => (x.CampaignId, x.EventKey)).ToHashSet();
        var seen = new HashSet<(Guid, string)>();
        return parsed.Select(p =>
        {
            if (p.Key is null) return ImportRowStatus.Invalid;
            var k = (p.Campaign!.Id, p.Key);
            if (!seen.Add(k)) return ImportRowStatus.DuplicateInFile;
            return existing.Contains(k) ? ImportRowStatus.Duplicate : ImportRowStatus.New;
        }).ToList();
    }

    // ------------------------------------------------------------------ Comum

    private static (CsvParser.CsvTable Table, List<string> Missing, List<string> Ignored) Read(string csv, string[] known, string[] required, bool requireCampaign)
    {
        CsvParser.CsvTable table;
        try { table = CsvParser.Parse(csv); }
        catch (FormatException ex) { throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = [ex.Message] }); }
        if (table.Headers.Count == 0)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = ["Arquivo vazio ou sem cabeçalho."] });
        var missing = required.Where(r => !table.Headers.Contains(r)).ToList();
        if (requireCampaign && !table.Headers.Contains("campaign_id") && !table.Headers.Contains("campaign_name")) missing.Add("campaign_id|campaign_name");
        var ignored = table.Headers.Where(h => !known.Contains(h)).ToList();
        return (table, missing, ignored);
    }

    private async Task<(Dictionary<Guid, Campaign> ById, ILookup<string, Campaign> ByName)> LoadCampaignsAsync(CancellationToken ct)
    {
        var campaigns = await scope.Campaigns(db.Campaigns.AsNoTracking()).ToListAsync(ct);
        return (campaigns.ToDictionary(c => c.Id), campaigns.ToLookup(c => c.Name.Trim().ToLowerInvariant()));
    }

    private static Campaign? ResolveCampaign(CsvParser.CsvRow row, Dictionary<Guid, Campaign> byId, ILookup<string, Campaign> byName, List<string> errors)
    {
        if (row.Get("campaign_id") is { } idRaw)
        {
            if (Guid.TryParse(idRaw, out var id) && byId.TryGetValue(id, out var c)) return c;
            errors.Add("campaign_id não encontrado.");
            return null;
        }
        if (row.Get("campaign_name") is { } name)
        {
            var matches = byName[name.Trim().ToLowerInvariant()].ToList();
            if (matches.Count == 1) return matches[0];
            errors.Add(matches.Count == 0 ? $"Campanha \"{name}\" não encontrada." : $"Mais de uma campanha chamada \"{name}\"; use campaign_id.");
            return null;
        }
        errors.Add("Informe campaign_id ou campaign_name.");
        return null;
    }
}
