using System.Globalization;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Notifications;

/// <summary>
/// Verificações periódicas (executadas por um serviço em segundo plano): campanhas próximas do encerramento,
/// tarefas atrasadas e relatório mensal disponível. Todas idempotentes por chave de deduplicação.
/// </summary>
public class NotificationScanner(IAppDbContext db, IClock clock, NotificationService notifications)
{
    public const int CampaignEndingDays = 3;

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var created = 0;

        var ending = await db.Campaigns.AsNoTracking()
            .Where(c => c.Status == CampaignStatus.Active && c.EndDate != null && c.EndDate >= today && c.EndDate <= today.AddDays(CampaignEndingDays))
            .Select(c => new { c.Id, c.Name, c.EndDate, c.ClientId, Client = c.Client!.Name }).ToListAsync(ct);
        foreach (var c in ending)
        {
            var days = c.EndDate!.Value.DayNumber - today.DayNumber;
            created += await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager, UserRole.Analyst], NotificationType.CampaignEnding,
                $"Campanha {c.Name} encerra em {days} dia(s)", new { campaign = c.Name, campaignId = c.Id, client = c.Client, days, endDate = c.EndDate },
                $"/campaigns/{c.Id}", $"campaign-ending:{c.Id}:{c.EndDate:yyyyMMdd}", null, ct);
        }

        var overdue = await db.MarketingTasks.AsNoTracking()
            .Where(t => t.DueDate < today && (t.Status == MarketingTaskStatus.Todo || t.Status == MarketingTaskStatus.InProgress))
            .Select(t => new { t.Id, t.Title, t.DueDate, t.AssignedToId }).ToListAsync(ct);
        foreach (var t in overdue)
        {
            var data = new { task = t.Title, taskId = t.Id, dueDate = t.DueDate, days = today.DayNumber - t.DueDate.DayNumber };
            var key = $"task-overdue:{t.Id}:{t.DueDate:yyyyMMdd}";
            created += t.AssignedToId is { } uid
                ? await notifications.QueueForUsersAsync([uid], NotificationType.TaskOverdue, $"Tarefa atrasada: {t.Title}", data, "/tasks", key, ct)
                : await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager], NotificationType.TaskOverdue, $"Tarefa atrasada: {t.Title}", data, "/tasks", key, null, ct);
        }

        // Relatório do mês anterior disponível a partir do dia 1º (para a agência e para cada cliente com dados).
        var previousMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var monthEnd = previousMonth.AddMonths(1).AddDays(-1);
        var monthLabel = previousMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        var link = $"/reports?from={previousMonth:yyyy-MM-dd}&to={monthEnd:yyyy-MM-dd}";
        if (await db.CampaignMetrics.AnyAsync(m => m.Date >= previousMonth && m.Date <= monthEnd, ct))
        {
            created += await notifications.QueueForRolesAsync([UserRole.Admin, UserRole.Manager], NotificationType.ReportAvailable,
                $"Relatório de {monthLabel} disponível", new { month = monthLabel, from = previousMonth, to = monthEnd }, link, $"report:{monthLabel}", null, ct);
            var clientsWithData = await db.CampaignMetrics.Where(m => m.Date >= previousMonth && m.Date <= monthEnd)
                .Select(m => m.Campaign!.ClientId).Distinct().ToListAsync(ct);
            foreach (var clientId in clientsWithData)
            {
                created += await notifications.QueueForRolesAsync([UserRole.ClientViewer], NotificationType.ReportAvailable,
                    $"Relatório de {monthLabel} disponível", new { month = monthLabel, from = previousMonth, to = monthEnd }, link,
                    $"report:{monthLabel}:{clientId}", clientId, ct);
            }
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return created;
    }
}
