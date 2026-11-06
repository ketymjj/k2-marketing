using K2Marketing.Api.Infrastructure;
using K2Marketing.Application.Ads;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Campaigns;
using K2Marketing.Application.Clients;
using K2Marketing.Application.Common;
using K2Marketing.Application.Creatives;
using K2Marketing.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace K2Marketing.Api.Controllers;

public record SetClientStatusRequest(ClientStatus Status);
public record CompareRequest(List<Guid> Ids, DateOnly? From, DateOnly? To);

[Route("api/v1/clients")]
public class ClientsController(ClientService clients) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<ClientListItemDto>> List([FromQuery] ClientQuery q, CancellationToken ct) => clients.ListAsync(q, ct);

    [HttpGet("lookup")]
    public Task<List<ClientLookupDto>> Lookup([FromQuery] bool activeOnly, CancellationToken ct) => clients.LookupAsync(activeOnly, ct);

    [HttpGet("industries")]
    public Task<List<string>> Industries(CancellationToken ct) => clients.IndustriesAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<ClientDto> Get(Guid id, CancellationToken ct) => clients.GetAsync(id, ct);

    [HttpGet("{id:guid}/overview")]
    public Task<ClientOverviewDto> Overview(Guid id, CancellationToken ct) => clients.OverviewAsync(id, ct);

    [HttpGet("{id:guid}/history"), Authorize(Roles = Roles.Agency)]
    public Task<List<HistoryItemDto>> History(Guid id, CancellationToken ct) => clients.HistoryAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Management)]
    public async Task<ActionResult<ClientDto>> Create(SaveClientRequest request, CancellationToken ct)
    {
        var created = await clients.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Management)]
    public Task<ClientDto> Update(Guid id, SaveClientRequest request, CancellationToken ct) => clients.UpdateAsync(id, request, ct);

    [HttpPatch("{id:guid}/status"), Authorize(Roles = Roles.Management)]
    public Task<ClientDto> SetStatus(Guid id, SetClientStatusRequest request, CancellationToken ct) => clients.SetStatusAsync(id, request.Status, ct);
}

[Route("api/v1/campaigns")]
public class CampaignsController(CampaignService campaigns, Application.Analytics.ComparisonService comparison) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<CampaignListItemDto>> List([FromQuery] CampaignQuery q, CancellationToken ct) => campaigns.ListAsync(q, ct);

    [HttpGet("lookup")]
    public Task<List<CampaignLookupDto>> Lookup([FromQuery] Guid? clientId, CancellationToken ct) => campaigns.LookupAsync(clientId, ct);

    [HttpGet("{id:guid}")]
    public Task<CampaignDto> Get(Guid id, CancellationToken ct) => campaigns.GetAsync(id, ct);

    [HttpGet("{id:guid}/performance")]
    public Task<CampaignPerformanceDto> Performance(Guid id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        campaigns.PerformanceAsync(id, from, to, ct);

    /// <summary>Compara de 2 a 4 campanhas lado a lado.</summary>
    [HttpPost("compare")]
    public Task<Application.Analytics.CompareResultDto> Compare(CompareRequest request, CancellationToken ct) =>
        comparison.CompareAsync(request.Ids ?? [], request.From, request.To, ct);

    [HttpPost, Authorize(Roles = Roles.Operations)]
    public async Task<ActionResult<CampaignDto>> Create(SaveCampaignRequest request, CancellationToken ct)
    {
        var created = await campaigns.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<CampaignDto> Update(Guid id, SaveCampaignRequest request, CancellationToken ct) => campaigns.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/duplicate"), Authorize(Roles = Roles.Operations)]
    public Task<CampaignDto> Duplicate(Guid id, CancellationToken ct) => campaigns.DuplicateAsync(id, ct);

    /// <summary>Pausar, reativar, encerrar ou cancelar — status interno do K2 (não altera a plataforma externa).</summary>
    [HttpPatch("{id:guid}/status"), Authorize(Roles = Roles.Operations)]
    public Task<CampaignDto> ChangeStatus(Guid id, ChangeCampaignStatusRequest request, CancellationToken ct) => campaigns.ChangeStatusAsync(id, request.Status, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await campaigns.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/v1/advertisements")]
public class AdvertisementsController(AdvertisementService ads) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<AdvertisementDto>> List([FromQuery] AdvertisementQuery q, CancellationToken ct) => ads.ListAsync(q, ct);

    [HttpGet("lookup")]
    public Task<List<AdLookupDto>> Lookup([FromQuery] Guid campaignId, CancellationToken ct) => ads.LookupAsync(campaignId, ct);

    [HttpGet("{id:guid}")]
    public Task<AdvertisementDto> Get(Guid id, CancellationToken ct) => ads.GetAsync(id, ct);

    [HttpPost("compare")]
    public Task<List<AdvertisementDto>> Compare(CompareRequest request, CancellationToken ct) => ads.CompareAsync(request.Ids ?? [], request.From, request.To, ct);

    [HttpPost, Authorize(Roles = Roles.Operations)]
    public async Task<ActionResult<AdvertisementDto>> Create(SaveAdvertisementRequest request, CancellationToken ct)
    {
        var created = await ads.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<AdvertisementDto> Update(Guid id, SaveAdvertisementRequest request, CancellationToken ct) => ads.UpdateAsync(id, request, ct);

    [HttpPost("{id:guid}/duplicate"), Authorize(Roles = Roles.Operations)]
    public Task<AdvertisementDto> Duplicate(Guid id, CancellationToken ct) => ads.DuplicateAsync(id, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await ads.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/creatives/{creativeId:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<AdvertisementDto> LinkCreative(Guid id, Guid creativeId, CancellationToken ct) => ads.LinkCreativeAsync(id, creativeId, ct);

    [HttpDelete("{id:guid}/creatives/{creativeId:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<AdvertisementDto> UnlinkCreative(Guid id, Guid creativeId, CancellationToken ct) => ads.UnlinkCreativeAsync(id, creativeId, ct);
}

[Route("api/v1/creatives")]
public class CreativesController(CreativeService creatives) : ApiControllerBase
{
    public class UploadForm
    {
        public Guid ClientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public CreativeType Type { get; set; }
        public string? Description { get; set; }
        public string? TextContent { get; set; }
        public IFormFile? File { get; set; }
    }

    [HttpGet]
    public Task<PagedResult<CreativeDto>> List([FromQuery] CreativeQuery q, CancellationToken ct) => creatives.ListAsync(q, ct);

    [HttpGet("{id:guid}")]
    public Task<CreativeDto> Get(Guid id, CancellationToken ct) => creatives.GetAsync(id, ct);

    /// <summary>Arquivo do criativo. Exige autenticação e acesso ao cliente dono (não há URL pública).</summary>
    [HttpGet("{id:guid}/file")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> File(Guid id, CancellationToken ct)
    {
        var file = await creatives.OpenFileAsync(id, ct);
        Response.Headers.CacheControl = "private, max-age=3600";
        return File(file.Content, file.ContentType, enableRangeProcessing: true);
    }

    [HttpPost, Authorize(Roles = Roles.Operations), Consumes("multipart/form-data")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<ActionResult<CreativeDto>> Upload([FromForm] UploadForm form, CancellationToken ct)
    {
        await using var stream = form.File?.OpenReadStream();
        var created = await creatives.CreateAsync(new UploadCreativeCommand(form.ClientId, form.Name, form.Type, form.Description, form.TextContent,
            stream, form.File?.FileName, form.File?.ContentType, form.File?.Length ?? 0), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<CreativeDto> Update(Guid id, UpdateCreativeRequest request, CancellationToken ct) => creatives.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await creatives.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/v1/budgets")]
public class BudgetsController(BudgetService budgets) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<BudgetDto>> List([FromQuery] BudgetQuery q, CancellationToken ct) => budgets.ListAsync(q, ct);

    [HttpGet("summary")]
    public Task<BudgetSummaryDto> Summary([FromQuery] BudgetQuery q, CancellationToken ct) => budgets.SummaryAsync(q, ct);

    [HttpGet("{id:guid}")]
    public Task<BudgetDetailDto> Get(Guid id, CancellationToken ct) => budgets.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Management)]
    public async Task<ActionResult<BudgetDto>> Create(SaveBudgetRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await budgets.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Management)]
    public Task<BudgetDto> Update(Guid id, SaveBudgetRequest request, CancellationToken ct) => budgets.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await budgets.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Registra gasto realizado (gravado como métrica de nível campanha do dia).</summary>
    [HttpPost("{id:guid}/spend"), Authorize(Roles = Roles.Operations)]
    public Task<BudgetDto> RegisterSpend(Guid id, RegisterSpendRequest request, CancellationToken ct) => budgets.RegisterSpendAsync(id, request, ct);
}
