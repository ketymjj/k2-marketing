using FluentValidation;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Common;
using K2Marketing.Application.Notifications;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Budgets;

public record BudgetDto(Guid Id, Guid CampaignId, string CampaignName, Guid ClientId, string ClientName, AdPlatform Platform,
    decimal PlannedAmount, decimal ActualSpend, decimal Remaining, decimal? UsagePercent, int Threshold,
    DateOnly StartDate, DateOnly EndDate, string? Notes, DateTime CreatedAt);

public record BudgetHistoryPoint(DateOnly Date, decimal Spend, decimal Cumulative, decimal PlannedCumulative);
public record BudgetDetailDto(BudgetDto Budget, List<BudgetHistoryPoint> History);

public record BudgetSummaryDto(decimal Planned, decimal Spent, decimal Remaining, decimal? UsagePercent,
    int Count, int Over80, int Over90, int Over100, List<BudgetDto> Items);

public record SaveBudgetRequest(Guid CampaignId, decimal PlannedAmount, DateOnly StartDate, DateOnly EndDate, string? Notes);

/// <summary>Gasto realizado em um dia. É gravado como métrica de nível campanha (fonte única do investimento).</summary>
public record RegisterSpendRequest(DateOnly Date, decimal Amount);

public class BudgetQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    /// <summary>Orçamentos vigentes em alguma data de [From, To].</summary>
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SaveBudgetValidator : AbstractValidator<SaveBudgetRequest>
{
    public SaveBudgetValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.PlannedAmount).GreaterThan(0).LessThan(1_000_000_000m);
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).WithMessage("A data final deve ser igual ou posterior à inicial.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

/// <summary>
/// Recalcula ActualSpend dos orçamentos a partir de CampaignMetrics.Spend no período e dispara alertas de 80/90/100%.
/// Deve ser chamado após qualquer gravação de métricas (manual, CSV) ou alteração de orçamento.
/// </summary>
public class BudgetSyncService(IAppDbContext db, NotificationService notifications)
{
    public async Task SyncAsync(IEnumerable<Guid> campaignIds, CancellationToken ct)
    {
        var ids = campaignIds.Distinct().ToList();
        if (ids.Count == 0) return;
        var budgets = await db.CampaignBudgets.Include(b => b.Campaign).Where(b => ids.Contains(b.CampaignId)).ToListAsync(ct);
        foreach (var b in budgets)
        {
            b.ActualSpend = await db.CampaignMetrics
                .Where(m => m.CampaignId == b.CampaignId && m.Date >= b.StartDate && m.Date <= b.EndDate)
                .SumAsync(m => (decimal?)m.Spend, ct) ?? 0m;

            var threshold = MetricCalculator.BudgetThreshold(b.ActualSpend, b.PlannedAmount);
            if (threshold > b.LastAlertThreshold)
            {
                var usage = MetricCalculator.BudgetUsage(b.ActualSpend, b.PlannedAmount);
                await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager, UserRole.Analyst],
                    NotificationType.BudgetThreshold, $"Orçamento de {b.Campaign!.Name} atingiu {threshold}%",
                    new { campaign = b.Campaign.Name, campaignId = b.CampaignId, budgetId = b.Id, threshold, usage, spent = b.ActualSpend, planned = b.PlannedAmount },
                    $"/budgets?campaignId={b.CampaignId}", $"budget:{b.Id}:{threshold}", null, ct);
            }
            // Se o gasto cair (métrica corrigida), o limite volta a poder ser notificado.
            b.LastAlertThreshold = threshold;
        }
        await db.SaveChangesAsync(ct);
    }
}

public class BudgetService(
    IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, BudgetSyncService sync, IValidator<SaveBudgetRequest> validator)
{
    public async Task<PagedResult<BudgetDto>> ListAsync(BudgetQuery q, CancellationToken ct)
    {
        var query = Filter(q);
        var page = await Project(query.OrderByDescending(b => b.StartDate).ThenBy(b => b.Campaign!.Name)).ToPagedAsync(q, ct);
        return page;
    }

    public async Task<BudgetSummaryDto> SummaryAsync(BudgetQuery q, CancellationToken ct)
    {
        var items = await Project(Filter(q).OrderByDescending(b => b.ActualSpend)).ToListAsync(ct);
        var planned = items.Sum(i => i.PlannedAmount);
        var spent = items.Sum(i => i.ActualSpend);
        return new BudgetSummaryDto(planned, spent, planned - spent, MetricCalculator.BudgetUsage(spent, planned), items.Count,
            items.Count(i => i.Threshold >= 80), items.Count(i => i.Threshold >= 90), items.Count(i => i.Threshold >= 100), items);
    }

    /// <summary>Histórico diário do orçamento: gasto, acumulado e ritmo planejado linear.</summary>
    public async Task<BudgetDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var budget = await Project(scope.Budgets(db.CampaignBudgets.AsNoTracking()).Where(b => b.Id == id)).FirstOrDefaultAsync(ct)
                     ?? throw new NotFoundException("Orçamento", id);
        var end = budget.EndDate < clock.Today ? budget.EndDate : clock.Today;
        var daily = await db.CampaignMetrics.AsNoTracking()
            .Where(m => m.CampaignId == budget.CampaignId && m.Date >= budget.StartDate && m.Date <= budget.EndDate)
            .GroupBy(m => m.Date).Select(g => new { g.Key, S = g.Sum(m => m.Spend) }).ToDictionaryAsync(x => x.Key, x => x.S, ct);

        var totalDays = budget.EndDate.DayNumber - budget.StartDate.DayNumber + 1;
        var history = new List<BudgetHistoryPoint>();
        decimal cumulative = 0;
        for (var d = budget.StartDate; d <= end; d = d.AddDays(1))
        {
            var spend = daily.GetValueOrDefault(d);
            cumulative += spend;
            var elapsed = d.DayNumber - budget.StartDate.DayNumber + 1;
            history.Add(new BudgetHistoryPoint(d, spend, cumulative, Math.Round(budget.PlannedAmount * elapsed / totalDays, 2)));
        }
        return new BudgetDetailDto(budget, history);
    }

    public async Task<BudgetDto> CreateAsync(SaveBudgetRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureCampaignAsync(r.CampaignId, ct);
        await EnsureNoOverlapAsync(r, null, ct);
        var budget = new CampaignBudget
        {
            CampaignId = r.CampaignId, PlannedAmount = r.PlannedAmount, StartDate = r.StartDate, EndDate = r.EndDate,
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(), CreatedAt = clock.UtcNow
        };
        db.CampaignBudgets.Add(budget);
        audit.Log("Create", "CampaignBudget", budget.Id, $"{r.PlannedAmount:F2} ({r.StartDate}–{r.EndDate})");
        await db.SaveChangesAsync(ct);
        await sync.SyncAsync([r.CampaignId], ct);
        return await GetDtoAsync(budget.Id, ct);
    }

    public async Task<BudgetDto> UpdateAsync(Guid id, SaveBudgetRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var budget = await db.CampaignBudgets.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("Orçamento", id);
        if (budget.CampaignId != r.CampaignId) throw new DomainException("budget.campaignLocked", "Não é possível mover o orçamento para outra campanha.");
        await EnsureNoOverlapAsync(r, id, ct);
        budget.PlannedAmount = r.PlannedAmount;
        budget.StartDate = r.StartDate;
        budget.EndDate = r.EndDate;
        budget.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
        budget.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "CampaignBudget", id, $"{r.PlannedAmount:F2} ({r.StartDate}–{r.EndDate})");
        await db.SaveChangesAsync(ct);
        await sync.SyncAsync([budget.CampaignId], ct);
        return await GetDtoAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var budget = await db.CampaignBudgets.FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("Orçamento", id);
        db.CampaignBudgets.Remove(budget);
        audit.Log("Delete", "CampaignBudget", id);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Registra gasto realizado somando ao Spend da métrica de nível campanha do dia (cria a linha se não existir),
    /// em transação, e ressincroniza os orçamentos. Assim o investimento nunca é contado em dois lugares.
    /// </summary>
    public async Task<BudgetDto> RegisterSpendAsync(Guid id, RegisterSpendRequest r, CancellationToken ct)
    {
        var budget = await db.CampaignBudgets.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, ct) ?? throw new NotFoundException("Orçamento", id);
        var errors = new Dictionary<string, string[]>();
        if (r.Amount <= 0 || r.Amount > 100_000_000m) errors["amount"] = ["Informe um valor maior que zero."];
        if (r.Date < budget.StartDate || r.Date > budget.EndDate) errors["date"] = ["A data deve estar dentro do período do orçamento."];
        if (r.Date > clock.Today) errors["date"] = ["Não é possível registrar gasto em data futura."];
        if (errors.Count > 0) throw new RequestValidationException(errors);

        await using var tx = await db.BeginTransactionAsync(ct);
        var metric = await db.CampaignMetrics.FirstOrDefaultAsync(m => m.CampaignId == budget.CampaignId && m.AdvertisementId == null && m.Date == r.Date, ct);
        if (metric is null)
        {
            metric = new CampaignMetric { CampaignId = budget.CampaignId, Date = r.Date, Source = DataSource.Manual, CreatedAt = clock.UtcNow };
            db.CampaignMetrics.Add(metric);
        }
        else metric.UpdatedAt = clock.UtcNow;
        metric.Spend += r.Amount;
        audit.Log("RegisterSpend", "CampaignBudget", id, $"{r.Date}: +{r.Amount:F2}");
        await db.SaveChangesAsync(ct);
        await sync.SyncAsync([budget.CampaignId], ct);
        await tx.CommitAsync(ct);
        return await GetDtoAsync(id, ct);
    }

    private IQueryable<CampaignBudget> Filter(BudgetQuery q)
    {
        var query = scope.Budgets(db.CampaignBudgets.AsNoTracking());
        if (q.ClientId is { } clientId) query = query.Where(b => b.Campaign!.ClientId == clientId);
        if (q.CampaignId is { } campaignId) query = query.Where(b => b.CampaignId == campaignId);
        if (q.From is { } from) query = query.Where(b => b.EndDate >= from);
        if (q.To is { } to) query = query.Where(b => b.StartDate <= to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(b => b.Campaign!.Name.ToLower().Contains(s) || b.Campaign.Client!.Name.ToLower().Contains(s));
        }
        return query;
    }

    private async Task<BudgetDto> GetDtoAsync(Guid id, CancellationToken ct) =>
        await Project(db.CampaignBudgets.AsNoTracking().Where(b => b.Id == id)).FirstAsync(ct);

    private async Task EnsureCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        if (!await db.Campaigns.AnyAsync(c => c.Id == campaignId, ct)) throw new NotFoundException("Campanha", campaignId);
    }

    /// <summary>Períodos de orçamento da mesma campanha não podem se sobrepor (o gasto seria contado duas vezes).</summary>
    private async Task EnsureNoOverlapAsync(SaveBudgetRequest r, Guid? exceptId, CancellationToken ct)
    {
        if (await db.CampaignBudgets.AnyAsync(b => b.CampaignId == r.CampaignId && b.Id != exceptId
                                                  && b.StartDate <= r.EndDate && b.EndDate >= r.StartDate, ct))
            throw new ConflictException("budget.overlap", "Já existe um orçamento desta campanha que se sobrepõe ao período informado.");
    }

    private static IQueryable<BudgetDto> Project(IQueryable<CampaignBudget> q) =>
        q.Select(b => new BudgetDto(b.Id, b.CampaignId, b.Campaign!.Name, b.Campaign.ClientId, b.Campaign.Client!.Name, b.Campaign.Platform,
            b.PlannedAmount, b.ActualSpend, b.PlannedAmount - b.ActualSpend,
            b.PlannedAmount > 0 ? (decimal?)Math.Round(b.ActualSpend / b.PlannedAmount * 100m, 2) : null,
            b.PlannedAmount > 0 && b.ActualSpend >= b.PlannedAmount ? 100
                : b.PlannedAmount > 0 && b.ActualSpend >= b.PlannedAmount * 0.9m ? 90
                : b.PlannedAmount > 0 && b.ActualSpend >= b.PlannedAmount * 0.8m ? 80 : 0,
            b.StartDate, b.EndDate, b.Notes, b.CreatedAt));
}
