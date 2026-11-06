using K2Marketing.Domain.Entities;

namespace K2Marketing.Application.Common;

/// <summary>Registra ações relevantes na trilha de auditoria. Gravado junto com o SaveChanges da operação.</summary>
public class AuditService(IAppDbContext db, ICurrentUser user, IClock clock)
{
    public void Log(string action, string entity, object? entityId = null, string? details = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            UserId = user.IsAuthenticated ? user.UserId : null,
            UserEmail = user.IsAuthenticated ? user.Email : "system",
            Action = action,
            Entity = entity,
            EntityId = entityId?.ToString(),
            Details = details is { Length: > 2000 } ? details[..2000] : details,
            CreatedAt = clock.UtcNow
        });
    }
}
