using System.Linq.Expressions;
using FluentValidation;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Common;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Ads;

public record LinkedCreativeDto(Guid Id, string Name, CreativeType Type, string? ContentType, bool HasFile, string? TextContent);

public record AdvertisementDto(Guid Id, Guid CampaignId, string CampaignName, Guid ClientId, string ClientName, AdPlatform Platform,
    string Name, string Headline, string PrimaryText, string? Description, string CallToAction, string DestinationUrl,
    AdStatus Status, DateTime CreatedAt, DateTime? UpdatedAt, List<LinkedCreativeDto> Creatives, MetricsDto Metrics);

public record SaveAdvertisementRequest(Guid CampaignId, string Name, string Headline, string PrimaryText, string? Description,
    string CallToAction, string DestinationUrl, AdStatus Status);

public record AdLookupDto(Guid Id, string Name, Guid CampaignId);

public class AdvertisementQuery : PageQuery
{
    public Guid? ClientId { get; set; }
    public Guid? CampaignId { get; set; }
    public AdStatus? Status { get; set; }
}

public class SaveAdvertisementValidator : AbstractValidator<SaveAdvertisementRequest>
{
    public SaveAdvertisementValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Headline).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PrimaryText).NotEmpty().MaximumLength(2200);
        RuleFor(x => x.Description).MaximumLength(300);
        RuleFor(x => x.CallToAction).NotEmpty().MaximumLength(50);
        RuleFor(x => x.DestinationUrl).NotEmpty().MaximumLength(500)
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Informe uma URL de destino http(s) válida.");
        RuleFor(x => x.Status).IsInEnum();
    }
}

public class AdvertisementService(
    IAppDbContext db, AccessScope scope, IClock clock, AuditService audit, IValidator<SaveAdvertisementRequest> validator)
{
    private static readonly Dictionary<string, Expression<Func<Advertisement, object?>>> Sort = new()
    {
        ["name"] = a => a.Name, ["campaign"] = a => a.Campaign!.Name, ["status"] = a => a.Status, ["createdAt"] = a => a.CreatedAt
    };

    public async Task<PagedResult<AdvertisementDto>> ListAsync(AdvertisementQuery q, CancellationToken ct)
    {
        var query = scope.Ads(db.Advertisements.AsNoTracking());
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(a => a.Name.ToLower().Contains(s) || a.Headline.ToLower().Contains(s));
        }
        if (q.ClientId is { } clientId) query = query.Where(a => a.Campaign!.ClientId == clientId);
        if (q.CampaignId is { } campaignId) query = query.Where(a => a.CampaignId == campaignId);
        if (q.Status is { } status) query = query.Where(a => a.Status == status);

        var page = await Project(query.ApplySort(q, Sort, "createdAt", defaultDescending: true)).ToPagedAsync(q, ct);
        var ids = page.Items.Select(a => a.Id).ToList();
        var totals = await db.CampaignMetrics.AsNoTracking().Where(m => m.AdvertisementId != null && ids.Contains(m.AdvertisementId.Value)).ByAdAsync(ct);
        var items = page.Items.Select(a => a with { Metrics = MetricsDto.From(totals.GetValueOrDefault(a.Id, MetricTotals.Empty)) }).ToList();
        return new PagedResult<AdvertisementDto>(items, page.Page, page.PageSize, page.TotalCount);
    }

    public Task<List<AdLookupDto>> LookupAsync(Guid campaignId, CancellationToken ct) =>
        scope.Ads(db.Advertisements.AsNoTracking()).Where(a => a.CampaignId == campaignId).OrderBy(a => a.Name)
            .Select(a => new AdLookupDto(a.Id, a.Name, a.CampaignId)).ToListAsync(ct);

    public async Task<AdvertisementDto> GetAsync(Guid id, CancellationToken ct)
    {
        var ad = await Project(scope.Ads(db.Advertisements.AsNoTracking()).Where(a => a.Id == id)).FirstOrDefaultAsync(ct)
                 ?? throw new NotFoundException("Anúncio", id);
        var totals = await db.CampaignMetrics.AsNoTracking().Where(m => m.AdvertisementId == id).TotalsAsync(ct);
        return ad with { Metrics = MetricsDto.From(totals) };
    }

    /// <summary>Comparação lado a lado de 2 a 4 anúncios (métricas no nível do anúncio).</summary>
    public async Task<List<AdvertisementDto>> CompareAsync(IReadOnlyCollection<Guid> ids, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count is < 2 or > 4)
            throw new DomainException("compare.count", "Selecione de 2 a 4 itens para comparar.");
        var ads = await Project(scope.Ads(db.Advertisements.AsNoTracking()).Where(a => distinct.Contains(a.Id))).ToListAsync(ct);
        if (ads.Count != distinct.Count) throw new NotFoundException("Anúncio", string.Join(",", distinct.Except(ads.Select(a => a.Id))));
        var totals = await db.CampaignMetrics.AsNoTracking().Apply(new MetricFilter { From = from, To = to })
            .Where(m => m.AdvertisementId != null && distinct.Contains(m.AdvertisementId.Value)).ByAdAsync(ct);
        return distinct.Select(id => ads.First(a => a.Id == id))
            .Select(a => a with { Metrics = MetricsDto.From(totals.GetValueOrDefault(a.Id, MetricTotals.Empty)) }).ToList();
    }

    public async Task<AdvertisementDto> CreateAsync(SaveAdvertisementRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        await EnsureCampaignAsync(r.CampaignId, ct);
        var ad = new Advertisement { CampaignId = r.CampaignId, CreatedAt = clock.UtcNow };
        Apply(ad, r);
        db.Advertisements.Add(ad);
        audit.Log("Create", "Advertisement", ad.Id, ad.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ad.Id, ct);
    }

    public async Task<AdvertisementDto> UpdateAsync(Guid id, SaveAdvertisementRequest r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var ad = await db.Advertisements.Include(a => a.Campaign).FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Anúncio", id);
        if (ad.CampaignId != r.CampaignId)
        {
            if (await db.CampaignMetrics.AnyAsync(m => m.AdvertisementId == id, ct))
                throw new DomainException("ad.campaignLocked", "Não é possível mover um anúncio que já possui métricas.");
            var target = await EnsureCampaignAsync(r.CampaignId, ct);
            if (target.ClientId != ad.Campaign!.ClientId)
                throw new DomainException("ad.otherClient", "O anúncio só pode ser movido para campanhas do mesmo cliente.");
            ad.CampaignId = r.CampaignId;
        }
        Apply(ad, r);
        ad.UpdatedAt = clock.UtcNow;
        audit.Log("Update", "Advertisement", id, ad.Name);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<AdvertisementDto> DuplicateAsync(Guid id, CancellationToken ct)
    {
        var source = await db.Advertisements.AsNoTracking().Include(a => a.Creatives).FirstOrDefaultAsync(a => a.Id == id, ct)
                     ?? throw new NotFoundException("Anúncio", id);
        var now = clock.UtcNow;
        var copy = new Advertisement
        {
            CampaignId = source.CampaignId, Name = $"{source.Name} (cópia)"[..Math.Min(source.Name.Length + 8, 200)],
            Headline = source.Headline, PrimaryText = source.PrimaryText, Description = source.Description,
            CallToAction = source.CallToAction, DestinationUrl = source.DestinationUrl, Status = AdStatus.Draft, CreatedAt = now
        };
        copy.Creatives.AddRange(source.Creatives.Select(c => new AdvertisementCreative { CreativeAssetId = c.CreativeAssetId, LinkedAt = now }));
        db.Advertisements.Add(copy);
        audit.Log("Duplicate", "Advertisement", copy.Id, $"Origem: {id}");
        await db.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var ad = await db.Advertisements.FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Anúncio", id);
        if (await db.CampaignMetrics.AnyAsync(m => m.AdvertisementId == id, ct))
            throw new DomainException("ad.hasMetrics", "O anúncio possui métricas registradas. Arquive-o em vez de excluir.");
        db.Advertisements.Remove(ad);
        audit.Log("Delete", "Advertisement", id, ad.Name);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Vincula um criativo ao anúncio. O criativo deve pertencer ao mesmo cliente da campanha.</summary>
    public async Task<AdvertisementDto> LinkCreativeAsync(Guid id, Guid creativeId, CancellationToken ct)
    {
        var ad = await db.Advertisements.Include(a => a.Campaign).FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException("Anúncio", id);
        var creative = await db.CreativeAssets.FirstOrDefaultAsync(c => c.Id == creativeId, ct) ?? throw new NotFoundException("Criativo", creativeId);
        if (creative.ClientId != ad.Campaign!.ClientId)
            throw new DomainException("creative.otherClient", "O criativo pertence a outro cliente.");
        if (!await db.AdvertisementCreatives.AnyAsync(x => x.AdvertisementId == id && x.CreativeAssetId == creativeId, ct))
        {
            db.AdvertisementCreatives.Add(new AdvertisementCreative { AdvertisementId = id, CreativeAssetId = creativeId, LinkedAt = clock.UtcNow });
            audit.Log("LinkCreative", "Advertisement", id, creative.Name);
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    public async Task<AdvertisementDto> UnlinkCreativeAsync(Guid id, Guid creativeId, CancellationToken ct)
    {
        var link = await db.AdvertisementCreatives.FirstOrDefaultAsync(x => x.AdvertisementId == id && x.CreativeAssetId == creativeId, ct);
        if (link is not null)
        {
            db.AdvertisementCreatives.Remove(link);
            audit.Log("UnlinkCreative", "Advertisement", id, creativeId.ToString());
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    private async Task<Campaign> EnsureCampaignAsync(Guid campaignId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == campaignId, ct) ?? throw new NotFoundException("Campanha", campaignId);
        if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled)
            throw new DomainException("campaign.closed", "A campanha está encerrada ou cancelada.");
        return campaign;
    }

    private static IQueryable<AdvertisementDto> Project(IQueryable<Advertisement> q) =>
        q.Select(a => new AdvertisementDto(a.Id, a.CampaignId, a.Campaign!.Name, a.Campaign.ClientId, a.Campaign.Client!.Name, a.Campaign.Platform,
            a.Name, a.Headline, a.PrimaryText, a.Description, a.CallToAction, a.DestinationUrl, a.Status, a.CreatedAt, a.UpdatedAt,
            a.Creatives.OrderBy(c => c.LinkedAt).Select(c => new LinkedCreativeDto(c.CreativeAssetId, c.CreativeAsset!.Name, c.CreativeAsset.Type,
                c.CreativeAsset.ContentType, c.CreativeAsset.FilePath != null, c.CreativeAsset.TextContent)).ToList(),
            null!)); // preenchido após a consulta (métricas agregadas por anúncio)

    private static void Apply(Advertisement a, SaveAdvertisementRequest r)
    {
        a.Name = r.Name.Trim();
        a.Headline = r.Headline.Trim();
        a.PrimaryText = r.PrimaryText.Trim();
        a.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        a.CallToAction = r.CallToAction.Trim();
        a.DestinationUrl = r.DestinationUrl.Trim();
        a.Status = r.Status;
    }
}
