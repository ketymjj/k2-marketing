using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Tasks;

public record TaskDto(Guid Id, Guid? ClientId, string? ClientName, Guid? CampaignId, string? CampaignName, string Title, string? Description,
    Guid? AssignedToId, string? AssignedToName, TaskPriority Priority, DateOnly DueDate, MarketingTaskStatus Status, bool Overdue,
    DateTime? CompletedAt, DateTime CreatedAt);

public record SaveTaskRequest(Guid? ClientId, Guid? CampaignId, string Title, string? Description, Guid? AssignedToId,
    TaskPriority Priority, DateOnly DueDate, MarketingTaskStatus Status);

public record TaskStatsDto(int Open, int Overdue, int DueToday, int DoneThisWeek);

public class TaskQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public Guid? AssignedToId { get; set; }
    public bool Mine { get; set; }
    public MarketingTaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public bool OverdueOnly { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveTaskValidator : AbstractValidator<SaveTaskRequest>
{
    public SaveTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Priority).IsInEnum();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.DueDate).NotEmpty();
    }
}

public class TaskService(IAppDbContext db, IClock clock, ICurrentUser user, AuditService audit, IValidator<SaveTaskRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<MarketingTask, object?>>> Sort = new()
    {
        ["dueDate"] = t => t.DueDate, ["title"] = t => t.Title, ["priority"] = t => t.Priority, ["status"] = t => t.Status,
        ["assignedTo"] = t => t.AssignedTo!.FullName, ["createdAt"] = t => t.CreatedAt
    };

    public Task<PagedResult<TaskDto>> ListAsync(TaskQuery q, CancellationToken ct) =>
        Project(Filter(q).ApplySort(q, Sort, "dueDate")).ToPagedAsync(q, ct);

    /// <summary>Tarefas com prazo no intervalo (visão de calendário).</summary>
    public Task<List<TaskDto>> CalendarAsync(TaskQuery q, CancellationToken ct)
    {
        var from = q.From ?? new DateOnly(clock.Today.Year, clock.Today.Month, 1).AddDays(-7);
        var to = q.To ?? from.AddDays(45);
        q.From = from;
        q.To = to.DayNumber - from.DayNumber > 93 ? from.AddDays(93) : to;
        return Project(Filter(q).OrderBy(t => t.DueDate).ThenByDescending(t => t.Priority)).Take(1000).ToListAsync(ct);
    }

    public async Task<TaskStatsDto> StatsAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var weekStartUtc = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var open = db.MarketingTasks.Where(t => t.Status == MarketingTaskStatus.Todo || t.Status == MarketingTaskStatus.InProgress);
        return new TaskStatsDto(await open.CountAsync(ct), await open.CountAsync(t => t.DueDate < today, ct), await open.CountAsync(t => t.DueDate == today, ct),
            await db.MarketingTasks.CountAsync(t => t.Status == MarketingTaskStatus.Done && t.CompletedAt >= weekStartUtc, ct));
    }

    public async Task<TaskDto> GetAsync(Guid id, CancellationToken ct) =>
        await Project(db.MarketingTasks.AsNoTracking().Where(t => t.Id == id)).FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Tarefa", id);

    public async Task<TaskDto> CreateAsync(SaveTaskRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var clientId = await ResolveReferencesAsync(r, ct);
        var task = new MarketingTask { CreatedAt = clock.UtcNow };
        Apply(task, r, clientId);
        db.MarketingTasks.Add(task);
        audit.Log("Create", "MarketingTask", task.Id, task.Title);
        await db.SaveChangesAsync(ct);
        return await GetAsync(task.Id, ct);
    }

    public async Task<TaskDto> UpdateAsync(Guid id, SaveTaskRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var task = await db.MarketingTasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Tarefa", id);
        var clientId = await ResolveReferencesAsync(r, ct);
        Apply(task, r, clientId);
        task.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "MarketingTask", id, task.Title);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<TaskDto> SetStatusAsync(Guid id, MarketingTaskStatus status, CancellationToken ct)
    {
        if (!Enum.IsDefined(status)) throw new RequestValidationException(new Dictionary<string, string[]> { ["status"] = ["Status inválido."] });
        var task = await db.MarketingTasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Tarefa", id);
        task.Status = status;
        task.CompletedAt = status == MarketingTaskStatus.Done ? clock.UtcNow : null;
        task.UpdatedAt = clock.UtcNow;
        audit.Log("ChangeStatus", "MarketingTask", id, status.ToString());
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var task = await db.MarketingTasks.FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException("Tarefa", id);
        db.MarketingTasks.Remove(task);
        audit.Log("Delete", "MarketingTask", id, task.Title);
        await db.SaveChangesAsync(ct);
    }

    private IQueryable<MarketingTask> Filter(TaskQuery q)
    {
        var today = clock.Today;
        var query = db.MarketingTasks.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(t => t.Title.ToLower().Contains(s) || (t.Description != null && t.Description.ToLower().Contains(s)));
        }
        if (q.ClientId is { } clientId) query = query.Where(t => t.ClientId == clientId);
        if (q.CampaignId is { } campaignId) query = query.Where(t => t.CampaignId == campaignId);
        if (q.Mine) query = query.Where(t => t.AssignedToId == user.UserId);
        else if (q.AssignedToId is { } assigned) query = query.Where(t => t.AssignedToId == assigned);
        if (q.Status is { } status) query = query.Where(t => t.Status == status);
        if (q.Priority is { } priority) query = query.Where(t => t.Priority == priority);
        if (q.OverdueOnly) query = query.Where(t => t.DueDate < today && (t.Status == MarketingTaskStatus.Todo || t.Status == MarketingTaskStatus.InProgress));
        if (q.From is { } from) query = query.Where(t => t.DueDate >= from);
        if (q.To is { } to) query = query.Where(t => t.DueDate <= to);
        return query;
    }

    /// <summary>Valida vínculos; a campanha define o cliente quando informada.</summary>
    private async Task<Guid?> ResolveReferencesAsync(SaveTaskRequest r, CancellationToken ct)
    {
        var clientId = r.ClientId;
        if (r.CampaignId is { } campaignId)
        {
            var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId, ct) ?? throw new NotFoundException("Campanha", campaignId);
            if (clientId is not null && clientId != campaign.ClientId)
                throw new DomainException("task.campaignMismatch", "A campanha não pertence ao cliente informado.");
            clientId = campaign.ClientId;
        }
        else if (clientId is { } cid && !await db.Clients.AnyAsync(c => c.Id == cid, ct)) throw new NotFoundException("Cliente", cid);
        if (r.AssignedToId is { } uid && !await db.Users.AnyAsync(u => u.Id == uid && u.Active && u.Role != UserRole.ClientViewer, ct))
            throw new DomainException("task.invalidAssignee", "Responsável inválido.");
        return clientId;
    }

    private void Apply(MarketingTask t, SaveTaskRequest r, Guid? clientId)
    {
        t.ClientId = clientId;
        t.CampaignId = r.CampaignId;
        t.Title = r.Title.Trim();
        t.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        t.AssignedToId = r.AssignedToId;
        t.Priority = r.Priority;
        t.DueDate = r.DueDate;
        if (t.Status != MarketingTaskStatus.Done && r.Status == MarketingTaskStatus.Done) t.CompletedAt = clock.UtcNow;
        if (r.Status != MarketingTaskStatus.Done) t.CompletedAt = null;
        t.Status = r.Status;
    }

    private IQueryable<TaskDto> Project(IQueryable<MarketingTask> q)
    {
        var today = clock.Today;
        return q.Select(t => new TaskDto(t.Id, t.ClientId, t.Client != null ? t.Client.Name : null, t.CampaignId, t.Campaign != null ? t.Campaign.Name : null,
            t.Title, t.Description, t.AssignedToId, t.AssignedTo != null ? t.AssignedTo.FullName : null, t.Priority, t.DueDate, t.Status,
            t.DueDate < today && (t.Status == MarketingTaskStatus.Todo || t.Status == MarketingTaskStatus.InProgress), t.CompletedAt, t.CreatedAt));
    }
}
