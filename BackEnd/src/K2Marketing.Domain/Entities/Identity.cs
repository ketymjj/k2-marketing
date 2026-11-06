namespace K2Marketing.Domain.Entities;

public class Role
{
    public UserRole Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    /// <summary>Obrigatório para CLIENT_VIEWER: o único cliente cujos dados o usuário pode ver.</summary>
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public bool Active { get; set; } = true;
    public string PreferredLanguage { get; set; } = "pt-BR";
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByHash { get; set; }

    public bool IsActive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    /// <summary>Parâmetros (JSON) usados pelo frontend para montar a mensagem traduzida.</summary>
    public string Data { get; set; } = "{}";
    public string Title { get; set; } = string.Empty;
    public string? Link { get; set; }
    /// <summary>Evita gerar o mesmo aviso repetidamente (ex.: "budget:{id}:90").</summary>
    public string? DedupKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Identidade visual da agência, usada em relatórios e no cabeçalho (registro único).</summary>
public class AgencySettings
{
    public int Id { get; set; } = 1;
    public string AgencyName { get; set; } = "K2 Digital";
    public string PrimaryColor { get; set; } = "#6941C6";
    public string? ContactEmail { get; set; }
    public string? Website { get; set; }
    public string? ReportFooter { get; set; }
    public string Currency { get; set; } = "BRL";
}
