using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Common;
using K2Marketing.Application.Notifications;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Leads;

public record LeadDto(Guid Id, Guid ClientId, string ClientName, Guid? CampaignId, string? CampaignName, string Name,
    string? Email, string? Phone, string? Source, LeadStatus Status, bool ConsentGiven, DateTime CreatedAt, DateTime? UpdatedAt);

public record LeadInteractionDto(Guid Id, LeadInteractionType Type, string Notes, LeadStatus? FromStatus, LeadStatus? ToStatus,
    string? UserName, DateTime CreatedAt);

public record LeadDetailDto(LeadDto Lead, List<LeadInteractionDto> Interactions, int ConversionsCount, decimal ConversionsRevenue);

public record BoardColumnDto(LeadStatus Status, int Count, List<LeadDto> Items);

public record SaveLeadRequest(Guid ClientId, Guid? CampaignId, string Name, string? Email, string? Phone, string? Source, bool ConsentGiven);
public record ChangeLeadStatusRequest(LeadStatus Status, string? Notes);
public record AddInteractionRequest(LeadInteractionType Type, string Notes);

public class LeadQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public LeadStatus? Status { get; set; }
    public string? Source { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveLeadValidator : AbstractValidator<SaveLeadRequest>
{
    public SaveLeadValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Source).MaximumLength(80);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Email) || !string.IsNullOrWhiteSpace(x.Phone))
            .WithName("contact").WithMessage("Informe e-mail ou telefone.");
    }
}

public class LeadService(
    IAppDbContext db, AccessScope scope, IClock clock, ICurrentUser user, AuditService audit, NotificationService notifications,
    IValidator<SaveLeadRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<Lead, object?>>> Sort = new()
    {
        ["name"] = l => l.Name, ["status"] = l => l.Status, ["source"] = l => l.Source, ["client"] = l => l.Client!.Name,
        ["campaign"] = l => l.Campaign!.Name, ["createdAt"] = l => l.CreatedAt
    };

    public Task<PagedResult<LeadDto>> ListAsync(LeadQuery q, CancellationToken ct) =>
        Project(Filter(q).ApplySort(q, Sort, "createdAt", defaultDescending: true)).ToPagedAsync(q, ct);

    /// <summary>Kanban: colunas por status, com os leads mais recentes de cada coluna.</summary>
    public async Task<List<BoardColumnDto>> BoardAsync(LeadQuery q, int perColumn, CancellationToken ct)
    {
        q.Status = null;
        var baseQuery = Filter(q);
        var columns = new List<BoardColumnDto>();
        foreach (var status in Enum.GetValues<LeadStatus>())
        {
            var col = baseQuery.Where(l => l.Status == status);
            var count = await col.CountAsync(ct);
            var items = await Project(col.OrderByDescending(l => l.UpdatedAt ?? l.CreatedAt)).Take(Math.Clamp(perColumn, 1, 200)).ToListAsync(ct);
            columns.Add(new BoardColumnDto(status, count, items));
        }
        return columns;
    }

    public Task<List<string>> SourcesAsync(CancellationToken ct) =>
        scope.Leads(db.Leads.AsNoTracking()).Where(l => l.Source != null).Select(l => l.Source!).Distinct().OrderBy(s => s).ToListAsync(ct);

    public async Task<LeadDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var lead = await Project(scope.Leads(db.Leads.AsNoTracking()).Where(l => l.Id == id)).FirstOrDefaultAsync(ct)
                   ?? throw new NotFoundException("Lead", id);
        var interactions = await db.LeadInteractions.AsNoTracking().Where(i => i.LeadId == id).OrderByDescending(i => i.CreatedAt)
            .Select(i => new LeadInteractionDto(i.Id, i.Type, i.Notes, i.FromStatus, i.ToStatus, i.UserName, i.CreatedAt)).ToListAsync(ct);
        var conv = await db.Conversions.AsNoTracking().Where(c => c.LeadId == id)
            .GroupBy(_ => 1).Select(g => new { N = g.Count(), R = g.Sum(c => c.RevenueAmount) }).FirstOrDefaultAsync(ct);
        return new LeadDetailDto(lead, interactions, conv?.N ?? 0, conv?.R ?? 0m);
    }

    public async Task<LeadDto> CreateAsync(SaveLeadRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureReferencesAsync(r, ct);
        var lead = new Lead { ClientId = r.ClientId, CreatedAt = clock.UtcNow };
        Apply(lead, r);
        db.Leads.Add(lead);
        db.LeadInteractions.Add(new LeadInteraction
        {
            LeadId = lead.Id, Type = LeadInteractionType.StatusChange, ToStatus = LeadStatus.New, Notes = "Lead cadastrado.",
            UserId = user.UserId, UserName = user.Name, CreatedAt = lead.CreatedAt
        });
        var clientName = await db.Clients.Where(c => c.Id == r.ClientId).Select(c => c.Name).FirstAsync(ct);
        await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager, UserRole.ClientViewer], NotificationType.NewLead,
            $"Novo lead: {lead.Name}", new { lead = lead.Name, leadId = lead.Id, client = clientName, source = lead.Source },
            $"/leads?leadId={lead.Id}", $"lead:{lead.Id}", r.ClientId, ct);
        audit.Log("Create", "Lead", lead.Id);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(lead.Id, ct);
    }

    public async Task<LeadDto> UpdateAsync(Guid id, SaveLeadRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Lead", id);
        if (lead.ClientId != r.ClientId) throw new DomainException("lead.clientLocked", "Não é possível mover o lead para outro cliente.");
        await EnsureReferencesAsync(r, ct);
        Apply(lead, r);
        lead.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "Lead", id);
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    /// <summary>Altera o status (usado pelo Kanban) e registra a mudança no histórico de contato.</summary>
    public async Task<LeadDto> ChangeStatusAsync(Guid id, ChangeLeadStatusRequest r, CancellationToken ct)
    {
        if (!Enum.IsDefined(r.Status)) throw new RequestValidationException(new Dictionary<string, string[]> { ["status"] = ["Status inválido."] });
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Lead", id);
        if (lead.Status == r.Status) return await GetDtoAsync(id, ct);
        var from = lead.Status;
        lead.Status = r.Status;
        lead.UpdatedAt = clock.UtcNow;
        db.LeadInteractions.Add(new LeadInteraction
        {
            LeadId = id, Type = LeadInteractionType.StatusChange, FromStatus = from, ToStatus = r.Status,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? string.Empty : r.Notes.Trim()[..Math.Min(r.Notes.Trim().Length, 2000)],
            UserId = user.UserId, UserName = user.Name, CreatedAt = clock.UtcNow
        });
        audit.Log("ChangeStatus", "Lead", id, $"{from} → {r.Status}");
        await db.SaveChangesAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task<LeadInteractionDto> AddInteractionAsync(Guid id, AddInteractionRequest r, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(r.Notes) || r.Notes.Length > 2000 || r.Type == LeadInteractionType.StatusChange || !Enum.IsDefined(r.Type))
            throw new RequestValidationException(new Dictionary<string, string[]> { ["notes"] = ["Informe o tipo e uma anotação de até 2000 caracteres."] });
        if (!await db.Leads.AnyAsync(l => l.Id == id, ct)) throw new NotFoundException("Lead", id);
        var interaction = new LeadInteraction
        {
            LeadId = id, Type = r.Type, Notes = r.Notes.Trim(), UserId = user.UserId, UserName = user.Name, CreatedAt = clock.UtcNow
        };
        db.LeadInteractions.Add(interaction);
        await db.Leads.Where(l => l.Id == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.UpdatedAt, clock.UtcNow), ct);
        await db.SaveChangesAsync(ct);
        return new LeadInteractionDto(interaction.Id, interaction.Type, interaction.Notes, null, null, interaction.UserName, interaction.CreatedAt);
    }

    /// <summary>Exclusão definitiva (direito de eliminação — LGPD). Conversões vinculadas perdem a referência ao lead.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException("Lead", id);
        db.Leads.Remove(lead);
        audit.Log("Delete", "Lead", id, "Dados pessoais removidos");
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<Lead> Filter(LeadQuery q)
    {
        var query = scope.Leads(db.Leads.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(l => l.Name.ToLower().Contains(s) || (l.Email != null && l.Email.Contains(s)) || (l.Phone != null && l.Phone.Contains(s)));
        }
        if (q.ClientId is { } clientId) query = query.Where(l => l.ClientId == clientId);
        if (q.CampaignId is { } campaignId) query = query.Where(l => l.CampaignId == campaignId);
        if (q.Status is { } status) query = query.Where(l => l.Status == status);
        if (!string.IsNullOrWhiteSpace(q.Source)) query = query.Where(l => l.Source == q.Source);
        if (q.From is { } from) query = query.Where(l => l.CreatedAt >= from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (q.To is { } to) query = query.Where(l => l.CreatedAt < to.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        return query;
    }

    private async Task EnsureReferencesAsync(SaveLeadRequest r, CancellationToken ct)
    {
        scope.EnsureClient(r.ClientId);
        if (!await db.Clients.AnyAsync(c => c.Id == r.ClientId, ct)) throw new NotFoundException("Cliente", r.ClientId);
        if (r.CampaignId is { } campaignId && !await db.Campaigns.AnyAsync(c => c.Id == campaignId && c.ClientId == r.ClientId, ct))
            throw new DomainException("lead.campaignMismatch", "A campanha não pertence ao cliente informado.");
    }

    private async Task<LeadDto> GetDtoAsync(Guid id, CancellationToken ct) => await Project(db.Leads.AsNoTracking().Where(l => l.Id == id)).FirstAsync(ct);

    private static void Apply(Lead l, SaveLeadRequest r)
    {
        l.CampaignId = r.CampaignId;
        l.Name = r.Name.Trim();
        l.Email = string.IsNullOrWhiteSpace(r.Email) ? null : r.Email.Trim().ToLowerInvariant();
        l.Phone = string.IsNullOrWhiteSpace(r.Phone) ? null : r.Phone.Trim();
        l.Source = string.IsNullOrWhiteSpace(r.Source) ? null : r.Source.Trim();
        l.ConsentGiven = r.ConsentGiven;
    }

    private static IQueryable<LeadDto> Project(IQueryable<Lead> q) =>
        q.Select(l => new LeadDto(l.Id, l.ClientId, l.Client!.Name, l.CampaignId, l.Campaign != null ? l.Campaign.Name : null, l.Name,
            l.Email, l.Phone, l.Source, l.Status, l.ConsentGiven, l.CreatedAt, l.UpdatedAt));
}
