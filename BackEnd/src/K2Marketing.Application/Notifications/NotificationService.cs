using System.Text.Json;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Notifications;

public record NotificationDto(Guid Id, NotificationType Type, string Title, JsonElement Data, string? Link, DateTime CreatedAt, DateTime? ReadAt);

public class NotificationQuery : PageQuery
{
    public bool UnreadOnly { get; set; }
}

/// <summary>
/// Central de notificações. O título é texto de apoio (pt-BR); o frontend monta a mensagem traduzida a partir de
/// <see cref="Notification.Type"/> + <see cref="Notification.Data"/>.
/// </summary>
public class NotificationService(IAppDbContext db, ICurrentUser user, IClock clock)
{
    public async Task<PagedResult<NotificationDto>> ListMineAsync(NotificationQuery q, CancellationToken ct)
    {
        var query = db.Notifications.AsNoTracking().Where(n => n.UserId == user.UserId);
        if (q.UnreadOnly) query = query.Where(n => n.ReadAt == null);
        var page = await query.OrderByDescending(n => n.CreatedAt).ToPagedAsync(q, ct);
        return new PagedResult<NotificationDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    public Task<int> UnreadCountAsync(CancellationToken ct) =>
        db.Notifications.CountAsync(n => n.UserId == user.UserId && n.ReadAt == null, ct);

    public async Task MarkReadAsync(Guid id, CancellationToken ct)
    {
        var n = await db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.UserId, ct)
                ?? throw new NotFoundException("Notificação", id);
        n.ReadAt ??= clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(CancellationToken ct)
    {
        var now = clock.UtcNow;
        await db.Notifications.Where(n => n.UserId == user.UserId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    /// <summary>
    /// Adiciona notificações (sem salvar) para os usuários ativos dos perfis informados. Com <paramref name="dedupKey"/>,
    /// usuários que já receberam a mesma chave são ignorados. Para CLIENT_VIEWER, somente quem pertence a <paramref name="clientId"/>.
    /// </summary>
    public async Task<int> QueueForRolesAsync(UserRole[] roles, NotificationType type, string title, object data, string? link,
        string? dedupKey, Guid? clientId, CancellationToken ct)
    {
        var recipients = await db.Users.AsNoTracking()
            .Where(u => u.Active && roles.Contains(u.Role) && (u.Role != UserRole.ClientViewer || (clientId != null && u.ClientId == clientId)))
            .Select(u => u.Id).ToListAsync(ct);
        return await QueueForUsersAsync(recipients, type, title, data, link, dedupKey, ct);
    }

    public async Task<int> QueueForUsersAsync(IReadOnlyCollection<Guid> userIds, NotificationType type, string title, object data,
        string? link, string? dedupKey, CancellationToken ct)
    {
        var targets = userIds.Distinct().ToList();
        if (dedupKey is not null)
        {
            var already = await db.Notifications.Where(n => n.DedupKey == dedupKey && targets.Contains(n.UserId)).Select(n => n.UserId).ToListAsync(ct);
            targets = targets.Except(already).ToList();
        }
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var now = clock.UtcNow;
        foreach (var id in targets)
        {
            db.Notifications.Add(new Notification
            {
                UserId = id, Type = type, Title = title.Length > 200 ? title[..200] : title, Data = json, Link = link,
                DedupKey = dedupKey, CreatedAt = now
            });
        }
        return targets.Count;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static NotificationDto ToDto(Notification n)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(n.Data) ? "{}" : n.Data);
        return new NotificationDto(n.Id, n.Type, n.Title, doc.RootElement.Clone(), n.Link, n.CreatedAt, n.ReadAt);
    }
}
