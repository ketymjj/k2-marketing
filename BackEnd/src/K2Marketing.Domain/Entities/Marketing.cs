namespace K2Marketing.Domain.Entities;

public class Client
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Document { get; set; }
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public ClientStatus Status { get; set; } = ClientStatus.Active;
    /// <summary>Verba mensal de mídia do cliente (investimento em anúncios, não receita da agência).</summary>
    public decimal MonthlyBudget { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<Campaign> Campaigns { get; set; } = [];
}

public class Campaign
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public string Name { get; set; } = string.Empty;
    public CampaignObjective Objective { get; set; }
    public AdPlatform Platform { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public decimal PlannedBudget { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<Advertisement> Advertisements { get; set; } = [];
    public List<CampaignBudget> Budgets { get; set; } = [];

    private static readonly Dictionary<CampaignStatus, CampaignStatus[]> Transitions = new()
    {
        [CampaignStatus.Draft] = [CampaignStatus.Scheduled, CampaignStatus.Active, CampaignStatus.Cancelled],
        [CampaignStatus.Scheduled] = [CampaignStatus.Draft, CampaignStatus.Active, CampaignStatus.Cancelled],
        [CampaignStatus.Active] = [CampaignStatus.Paused, CampaignStatus.Completed, CampaignStatus.Cancelled],
        [CampaignStatus.Paused] = [CampaignStatus.Active, CampaignStatus.Completed, CampaignStatus.Cancelled],
        [CampaignStatus.Completed] = [],
        [CampaignStatus.Cancelled] = []
    };

    public static IReadOnlyCollection<CampaignStatus> AllowedTransitions(CampaignStatus from) => Transitions[from];

    /// <summary>
    /// Altera o status interno da campanha no K2 Marketing. Não reflete em plataformas externas
    /// (não há integração autenticada nesta versão).
    /// </summary>
    public void ChangeStatus(CampaignStatus target, DateTime now)
    {
        if (target == Status) return;
        if (!Transitions[Status].Contains(target))
            throw new DomainException("campaign.invalidTransition", $"Não é possível alterar o status de {Status} para {target}.");
        Status = target;
        UpdatedAt = now;
    }
}

public class Advertisement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string PrimaryText { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CallToAction { get; set; } = string.Empty;
    public string DestinationUrl { get; set; } = string.Empty;
    public AdStatus Status { get; set; } = AdStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<AdvertisementCreative> Creatives { get; set; } = [];
}

public class CreativeAsset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public string Name { get; set; } = string.Empty;
    public CreativeType Type { get; set; }
    /// <summary>Caminho relativo no armazenamento de arquivos (nunca exposto diretamente; servido por endpoint autorizado).</summary>
    public string? FilePath { get; set; }
    public string? OriginalFileName { get; set; }
    public string? ContentType { get; set; }
    public long FileSize { get; set; }
    public string? Description { get; set; }
    /// <summary>Conteúdo textual, para criativos do tipo Texto.</summary>
    public string? TextContent { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<AdvertisementCreative> Advertisements { get; set; } = [];
}

public class AdvertisementCreative
{
    public Guid AdvertisementId { get; set; }
    public Advertisement? Advertisement { get; set; }
    public Guid CreativeAssetId { get; set; }
    public CreativeAsset? CreativeAsset { get; set; }
    public DateTime LinkedAt { get; set; }
}

/// <summary>
/// Orçamento de mídia de uma campanha para um período. <see cref="ActualSpend"/> é sincronizado a partir de
/// CampaignMetrics.Spend no período (fonte única de verdade para investimento).
/// </summary>
public class CampaignBudget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public decimal PlannedAmount { get; set; }
    public decimal ActualSpend { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Notes { get; set; }
    /// <summary>Maior limite (80/90/100) já notificado, para não alertar duas vezes.</summary>
    public int LastAlertThreshold { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public decimal Remaining => PlannedAmount - ActualSpend;
}

public class Lead
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Source { get; set; }
    public LeadStatus Status { get; set; } = LeadStatus.New;
    /// <summary>Base legal / consentimento LGPD informado na captação.</summary>
    public bool ConsentGiven { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<LeadInteraction> Interactions { get; set; } = [];
}

public class LeadInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeadId { get; set; }
    public LeadInteractionType Type { get; set; }
    public string Notes { get; set; } = string.Empty;
    public LeadStatus? FromStatus { get; set; }
    public LeadStatus? ToStatus { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Conversion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }
    public ConversionType ConversionType { get; set; }
    public DateTime ConversionDate { get; set; }
    public decimal RevenueAmount { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    /// <summary>Identificador do evento na origem (pedido, ID de transação). Opcional.</summary>
    public string? ExternalId { get; set; }
    /// <summary>Chave de deduplicação única por campanha: ExternalId ou impressão digital do evento.</summary>
    public string EventKey { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Métricas diárias informadas (manual/CSV). Indicadores derivados (CTR, CPC...) nunca são persistidos.</summary>
public class CampaignMetric
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public Guid? AdvertisementId { get; set; }
    public Advertisement? Advertisement { get; set; }
    public DateOnly Date { get; set; }
    public long Impressions { get; set; }
    public long Reach { get; set; }
    public long Clicks { get; set; }
    public decimal Spend { get; set; }
    public long Leads { get; set; }
    public long Conversions { get; set; }
    public decimal AttributedRevenue { get; set; }
    public DataSource Source { get; set; } = DataSource.Manual;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class MarketingTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }
    public Guid? CampaignId { get; set; }
    public Campaign? Campaign { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateOnly DueDate { get; set; }
    public MarketingTaskStatus Status { get; set; } = MarketingTaskStatus.Todo;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
