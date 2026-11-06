using System.Globalization;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Conversions;

public record ConversionDto(Guid Id, Guid CampaignId, string CampaignName, Guid ClientId, string ClientName, Guid? LeadId, string? LeadName,
    ConversionType ConversionType, DateTime ConversionDate, decimal RevenueAmount, DataSource Source, string? ExternalId, string? Notes, DateTime CreatedAt);

public record ConversionTypeSummary(ConversionType Type, int Count, decimal Revenue);

/// <summary>
/// Resumo que distingue: conversões gerais (todos os eventos), leads únicos convertidos (cada lead conta uma vez)
/// e vendas (somente Compra, com receita).
/// </summary>
public record ConversionSummaryDto(int TotalConversions, int UniqueLeadsConverted, int Sales, decimal SalesRevenue,
    decimal TotalRevenue, List<ConversionTypeSummary> ByType);

public record SaveConversionRequest(Guid CampaignId, Guid? LeadId, ConversionType ConversionType, DateTime ConversionDate,
    decimal RevenueAmount, string? ExternalId, string? Notes);

public class ConversionQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? LeadId { get; set; }
    public ConversionType? Type { get; set; }
    public DataSource? Source { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveConversionValidator : AbstractValidator<SaveConversionRequest>
{
    public SaveConversionValidator(IClock clock)
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ConversionType).IsInEnum();
        RuleFor(x => x.ConversionDate).NotEmpty().LessThanOrEqualTo(_ => clock.UtcNow.AddDays(1))
            .WithMessage("A data da conversão não pode estar no futuro.");
        RuleFor(x => x.RevenueAmount).GreaterThanOrEqualTo(0).LessThan(1_000_000_000m);
        RuleFor(x => x.RevenueAmount).GreaterThan(0).When(x => x.ConversionType == ConversionType.Purchase)
            .WithMessage("Conversões do tipo Compra devem ter receita.");
        RuleFor(x => x.ExternalId).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

/// <summary>Regras de deduplicação de conversões.</summary>
public static class ConversionKeys
{
    /// <summary>
    /// Chave única por campanha:
    /// <list type="bullet">
    /// <item>com ExternalId (pedido/transação): <c>ext:{id}</c>;</item>
    /// <item>tipos Lead/Cadastro/Contato com lead vinculado: <c>lead:{leadId}:{tipo}</c> (o mesmo lead não converte duas vezes no mesmo tipo);</item>
    /// <item>demais: impressão digital SHA-256 de lead + tipo + data/hora (minuto) + receita.</item>
    /// </list>
    /// </summary>
    public static string For(Guid? leadId, ConversionType type, DateTime date, decimal revenue, string? externalId)
    {
        if (!string.IsNullOrWhiteSpace(externalId)) return $"ext:{externalId.Trim().ToLowerInvariant()}";
        if (leadId is { } id && type is ConversionType.Lead or ConversionType.SignUp or ConversionType.Contact)
            return $"lead:{id:N}:{type}";
        var raw = string.Join('|', leadId?.ToString("N") ?? "-", type, date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            revenue.ToString("F2", CultureInfo.InvariantCulture));
        return "fp:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..40].ToLowerInvariant();
    }
}

public class ConversionService(IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, IValidator<SaveConversionRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<Conversion, object?>>> Sort = new()
    {
        ["conversionDate"] = c => c.ConversionDate, ["revenueAmount"] = c => c.RevenueAmount, ["type"] = c => c.ConversionType,
        ["campaign"] = c => c.Campaign!.Name, ["source"] = c => c.Source
    };

    public Task<PagedResult<ConversionDto>> ListAsync(ConversionQuery q, CancellationToken ct) =>
        Project(Filter(q).ApplySort(q, Sort, "conversionDate", defaultDescending: true)).ToPagedAsync(q, ct);

    public async Task<ConversionSummaryDto> SummaryAsync(ConversionQuery q, CancellationToken ct)
    {
        var query = Filter(q);
        var byType = await query.GroupBy(c => c.ConversionType)
            .Select(g => new ConversionTypeSummary(g.Key, g.Count(), g.Sum(c => c.RevenueAmount))).ToListAsync(ct);
        var uniqueLeads = await query.Where(c => c.LeadId != null).Select(c => c.LeadId).Distinct().CountAsync(ct);
        var sales = byType.FirstOrDefault(t => t.Type == ConversionType.Purchase);
        return new ConversionSummaryDto(byType.Sum(t => t.Count), uniqueLeads, sales?.Count ?? 0, sales?.Revenue ?? 0m,
            byType.Sum(t => t.Revenue), byType.OrderByDescending(t => t.Count).ToList());
    }

    public async Task<ConversionDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(scope.Conversions(db.Conversions.AsNoTracking()).Where(c => c.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Conversão", id);

    public async Task<ConversionDto> CreateAsync(SaveConversionRequest r, CancellationToken ct, DataSource source = DataSource.Manual)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == r.CampaignId, ct) ?? throw new NotFoundException("Campanha", r.CampaignId);
        scope.EnsureClient(campaign.ClientId, "Campanha");
        if (r.LeadId is { } leadId && !await db.Leads.AnyAsync(l => l.Id == leadId && l.ClientId == campaign.ClientId, ct))
            throw new DomainException("conversion.leadMismatch", "O lead não pertence ao cliente da campanha.");

        var date = DateTime.SpecifyKind(r.ConversionDate, r.ConversionDate.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : r.ConversionDate.Kind).ToUniversalTime();
        var key = ConversionKeys.For(r.LeadId, r.ConversionType, date, r.RevenueAmount, r.ExternalId);
        if (await db.Conversions.AnyAsync(c => c.CampaignId == r.CampaignId && c.EventKey == key, ct))
            throw new ConflictException("conversion.duplicate", "Esta conversão já foi registrada (mesmo evento).");

        var conversion = new Conversion
        {
            CampaignId = r.CampaignId, LeadId = r.LeadId, ConversionType = r.ConversionType, ConversionDate = date,
            RevenueAmount = r.RevenueAmount, Source = source, ExternalId = string.IsNullOrWhiteSpace(r.ExternalId) ? null : r.ExternalId.Trim(),
            EventKey = key, Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(), CreatedAt = clock.UtcNow
        };
        db.Conversions.Add(conversion);

        // Uma compra/agendamento vinculado a um lead marca o lead como convertido.
        if (r.LeadId is { } lid && r.ConversionType is ConversionType.Purchase or ConversionType.Appointment)
        {
            var lead = await db.Leads.FirstAsync(l => l.Id == lid, ct);
            if (lead.Status != LeadStatus.Converted)
            {
                db.LeadInteractions.Add(new LeadInteraction
                {
                    LeadId = lid, Type = LeadInteractionType.StatusChange, FromStatus = lead.Status, ToStatus = LeadStatus.Converted,
                    Notes = $"Conversão registrada ({r.ConversionType}).", CreatedAt = clock.UtcNow
                });
                lead.Status = LeadStatus.Converted;
                lead.UpdatedAt = clock.UtcNow;
            }
        }
        audit.Log("Create", "Conversion", conversion.Id, $"{r.ConversionType} {r.RevenueAmount:F2}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(conversion.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var conversion = await db.Conversions.FirstOrDefaultAsync(c => c.Id == id, ct) ?? throw new NotFoundException("Conversão", id);
        db.Conversions.Remove(conversion);
        audit.Log("Delete", "Conversion", id);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Conversion> Filter(ConversionQuery q)
    {
        var query = scope.Conversions(db.Conversions.AsNoTracking());
        if (q.ClientId is { } clientId) query = query.Where(c => c.Campaign!.ClientId == clientId);
        if (q.CampaignId is { } campaignId) query = query.Where(c => c.CampaignId == campaignId);
        if (q.LeadId is { } leadId) query = query.Where(c => c.LeadId == leadId);
        if (q.Type is { } type) query = query.Where(c => c.ConversionType == type);
        if (q.Source is { } source) query = query.Where(c => c.Source == source);
        if (q.From is { } from) query = query.Where(c => c.ConversionDate >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (q.To is { } to) query = query.Where(c => c.ConversionDate < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(c => c.Campaign!.Name.ToLower().Contains(s) || (c.Lead != null && c.Lead.Name.ToLower().Contains(s))
                                     || (c.ExternalId != null && c.ExternalId.ToLower().Contains(s)));
        }
        return query;
    }

    private static IQueryable<ConversionDto> Project(IQueryable<Conversion> q) =>
        q.Select(c => new ConversionDto(c.Id, c.CampaignId, c.Campaign!.Name, c.Campaign.ClientId, c.Campaign.Client!.Name, c.LeadId,
            c.Lead != null ? c.Lead.Name : null, c.ConversionType, c.ConversionDate, c.RevenueAmount, c.Source, c.ExternalId, c.Notes, c.CreatedAt));
}
