using K2Marketing.Domain;
using K2Marketing.Domain.Entities;

namespace K2Marketing.Application.Common;

/// <summary>
/// Isolamento de dados por cliente. Usuários da agência (Admin/Manager/Analyst) veem todos os clientes;
/// CLIENT_VIEWER vê somente o próprio cliente. Recursos de outro cliente respondem 404 (não revelam existência).
/// </summary>
public class AccessScope(ICurrentUser user)
{
    public bool IsClientViewer => user.Role == UserRole.ClientViewer;

    /// <summary>Cliente ao qual o usuário está restrito, ou null quando pode ver todos.</summary>
    public Guid? RestrictedClientId => IsClientViewer ? user.ClientId ?? Guid.Empty : null;

    public bool CanSee(Guid clientId) => RestrictedClientId is not { } id || id == clientId;

    public void EnsureClient(Guid clientId, string entity = "Cliente")
    {
        if (!CanSee(clientId)) throw new NotFoundException(entity, clientId);
    }

    public IQueryable<Client> Clients(IQueryable<Client> q) =>
        RestrictedClientId is { } id ? q.Where(c => c.Id == id) : q;

    public IQueryable<Campaign> Campaigns(IQueryable<Campaign> q) =>
        RestrictedClientId is { } id ? q.Where(c => c.ClientId == id) : q;

    public IQueryable<Advertisement> Ads(IQueryable<Advertisement> q) =>
        RestrictedClientId is { } id ? q.Where(a => a.Campaign!.ClientId == id) : q;

    public IQueryable<CreativeAsset> Creatives(IQueryable<CreativeAsset> q) =>
        RestrictedClientId is { } id ? q.Where(a => a.ClientId == id) : q;

    public IQueryable<CampaignBudget> Budgets(IQueryable<CampaignBudget> q) =>
        RestrictedClientId is { } id ? q.Where(b => b.Campaign!.ClientId == id) : q;

    public IQueryable<Lead> Leads(IQueryable<Lead> q) =>
        RestrictedClientId is { } id ? q.Where(l => l.ClientId == id) : q;

    public IQueryable<Conversion> Conversions(IQueryable<Conversion> q) =>
        RestrictedClientId is { } id ? q.Where(c => c.Campaign!.ClientId == id) : q;

    public IQueryable<CampaignMetric> Metrics(IQueryable<CampaignMetric> q) =>
        RestrictedClientId is { } id ? q.Where(m => m.Campaign!.ClientId == id) : q;

    public IQueryable<MarketingTask> Tasks(IQueryable<MarketingTask> q) =>
        RestrictedClientId is { } id ? q.Where(t => t.ClientId == id) : q;
}
