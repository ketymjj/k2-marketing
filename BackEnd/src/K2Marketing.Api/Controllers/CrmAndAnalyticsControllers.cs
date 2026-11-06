using System.Text;
using K2Marketing.Api.Infrastructure;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Common;
using K2Marketing.Application.Conversions;
using K2Marketing.Application.Imports;
using K2Marketing.Application.Leads;
using K2Marketing.Application.Metrics;
using K2Marketing.Application.Notifications;
using K2Marketing.Application.Reports;
using K2Marketing.Application.Tasks;
using K2Marketing.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace K2Marketing.Api.Controllers;

public record SetTaskStatusRequest(MarketingTaskStatus Status);

[Route("api/v1/leads")]
public class LeadsController(LeadService leads) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<LeadDto>> List([FromQuery] LeadQuery q, CancellationToken ct) => leads.ListAsync(q, ct);

    [HttpGet("board")]
    public Task<List<BoardColumnDto>> Board([FromQuery] LeadQuery q, [FromQuery] int perColumn = 50, CancellationToken ct = default) =>
        leads.BoardAsync(q, perColumn, ct);

    [HttpGet("sources")]
    public Task<List<string>> Sources(CancellationToken ct) => leads.SourcesAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<LeadDetailDto> Get(Guid id, CancellationToken ct) => leads.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Operations)]
    public async Task<ActionResult<LeadDto>> Create(SaveLeadRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await leads.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<LeadDto> Update(Guid id, SaveLeadRequest request, CancellationToken ct) => leads.UpdateAsync(id, request, ct);

    [HttpPatch("{id:guid}/status"), Authorize(Roles = Roles.Operations)]
    public Task<LeadDto> ChangeStatus(Guid id, ChangeLeadStatusRequest request, CancellationToken ct) => leads.ChangeStatusAsync(id, request, ct);

    [HttpPost("{id:guid}/interactions"), Authorize(Roles = Roles.Operations)]
    public Task<LeadInteractionDto> AddInteraction(Guid id, AddInteractionRequest request, CancellationToken ct) => leads.AddInteractionAsync(id, request, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await leads.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/v1/conversions")]
public class ConversionsController(ConversionService conversions) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<ConversionDto>> List([FromQuery] ConversionQuery q, CancellationToken ct) => conversions.ListAsync(q, ct);

    [HttpGet("summary")]
    public Task<ConversionSummaryDto> Summary([FromQuery] ConversionQuery q, CancellationToken ct) => conversions.SummaryAsync(q, ct);

    [HttpGet("{id:guid}")]
    public Task<ConversionDto> Get(Guid id, CancellationToken ct) => conversions.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Operations)]
    public async Task<ActionResult<ConversionDto>> Create(SaveConversionRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await conversions.CreateAsync(request, ct));

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Management)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await conversions.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/v1/metrics")]
public class MetricsController(MetricService metrics) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<MetricRowDto>> List([FromQuery] MetricQuery q, CancellationToken ct) => metrics.ListAsync(q, ct);

    [HttpGet("totals")]
    public Task<MetricsDto> Totals([FromQuery] MetricQuery q, CancellationToken ct) => metrics.TotalsAsync(q, ct);

    [HttpGet("{id:guid}")]
    public Task<MetricRowDto> Get(Guid id, CancellationToken ct) => metrics.GetAsync(id, ct);

    [HttpPost, Authorize(Roles = Roles.Operations)]
    public async Task<ActionResult<MetricRowDto>> Create(SaveMetricRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await metrics.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public Task<MetricRowDto> Update(Guid id, SaveMetricRequest request, CancellationToken ct) => metrics.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}"), Authorize(Roles = Roles.Operations)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await metrics.DeleteAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Importação CSV em duas etapas: preview (sem gravar) e confirm (grava em transação).</summary>
[Route("api/v1/imports"), Authorize(Roles = Roles.Operations)]
public class ImportsController(ImportService imports) : ApiControllerBase
{
    [HttpGet("metrics/template")]
    public async Task<IActionResult> MetricsTemplate(CancellationToken ct) =>
        File(Utf8Bom(await imports.MetricsTemplateAsync(ct)), "text/csv", "k2-modelo-metricas.csv");

    [HttpGet("conversions/template")]
    public IActionResult ConversionsTemplate() => File(Utf8Bom(imports.ConversionsTemplate()), "text/csv", "k2-modelo-conversoes.csv");

    [HttpPost("metrics/preview"), Consumes("multipart/form-data")]
    public async Task<ImportPreviewDto> PreviewMetrics(IFormFile file, CancellationToken ct) => await imports.PreviewMetricsAsync(await ReadAsync(file, ct), ct);

    [HttpPost("metrics/confirm"), Consumes("multipart/form-data")]
    public async Task<ImportResultDto> ConfirmMetrics(IFormFile file, [FromForm] DuplicateMode mode, CancellationToken ct) =>
        await imports.ConfirmMetricsAsync(await ReadAsync(file, ct), mode, ct);

    [HttpPost("conversions/preview"), Consumes("multipart/form-data")]
    public async Task<ImportPreviewDto> PreviewConversions(IFormFile file, CancellationToken ct) => await imports.PreviewConversionsAsync(await ReadAsync(file, ct), ct);

    [HttpPost("conversions/confirm"), Consumes("multipart/form-data")]
    public async Task<ImportResultDto> ConfirmConversions(IFormFile file, CancellationToken ct) => await imports.ConfirmConversionsAsync(await ReadAsync(file, ct), ct);

    private static async Task<string> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = ["Selecione um arquivo CSV."] });
        if (file.Length > ImportService.MaxFileBytes)
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = ["O arquivo excede 5 MB."] });
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not ".csv" and not ".txt")
            throw new RequestValidationException(new Dictionary<string, string[]> { ["file"] = ["Envie um arquivo .csv."] });
        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct);
    }

    internal static byte[] Utf8Bom(string content) => [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(content)];
}

[Route("api/v1/dashboard")]
public class DashboardController(DashboardService dashboard) : ApiControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get([FromQuery] DashboardQuery q, CancellationToken ct) => dashboard.GetAsync(q, ct);
}

[Route("api/v1/reports")]
public class ReportsController(ReportService reports) : ApiControllerBase
{
    [HttpGet("performance")]
    public Task<PerformanceReportDto> Performance([FromQuery] ReportQuery q, CancellationToken ct) => reports.PerformanceAsync(q, ct);

    [HttpGet("budget")]
    public Task<Application.Budgets.BudgetSummaryDto> Budget([FromQuery] ReportQuery q, CancellationToken ct) => reports.BudgetAsync(q, ct);

    [HttpGet("leads")]
    public Task<LeadsReportDto> Leads([FromQuery] ReportQuery q, CancellationToken ct) => reports.LeadsAsync(q, ct);

    [HttpGet("conversions")]
    public Task<ConversionsReportDto> Conversions([FromQuery] ReportQuery q, CancellationToken ct) => reports.ConversionsAsync(q, ct);

    [HttpGet("executive")]
    public Task<ExecutiveReportDto> Executive([FromQuery] ReportQuery q, CancellationToken ct) => reports.ExecutiveAsync(q, ct);

    /// <summary>Exportação CSV: performance | budget | leads | conversions.</summary>
    [HttpGet("{type}/export")]
    public async Task<IActionResult> Export(string type, [FromQuery] ReportQuery q, CancellationToken ct)
    {
        var (name, content) = await reports.ExportCsvAsync(type, q, ct);
        return File(ImportsController.Utf8Bom(content), "text/csv", name);
    }
}

[Route("api/v1/tasks"), Authorize(Roles = Roles.Operations)]
public class TasksController(TaskService tasks) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<TaskDto>> List([FromQuery] TaskQuery q, CancellationToken ct) => tasks.ListAsync(q, ct);

    [HttpGet("calendar")]
    public Task<List<TaskDto>> Calendar([FromQuery] TaskQuery q, CancellationToken ct) => tasks.CalendarAsync(q, ct);

    [HttpGet("stats")]
    public Task<TaskStatsDto> Stats(CancellationToken ct) => tasks.StatsAsync(ct);

    [HttpGet("{id:guid}")]
    public Task<TaskDto> Get(Guid id, CancellationToken ct) => tasks.GetAsync(id, ct);

    [HttpPost]
    public async Task<ActionResult<TaskDto>> Create(SaveTaskRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await tasks.CreateAsync(request, ct));

    [HttpPut("{id:guid}")]
    public Task<TaskDto> Update(Guid id, SaveTaskRequest request, CancellationToken ct) => tasks.UpdateAsync(id, request, ct);

    [HttpPatch("{id:guid}/status")]
    public Task<TaskDto> SetStatus(Guid id, SetTaskStatusRequest request, CancellationToken ct) => tasks.SetStatusAsync(id, request.Status, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await tasks.DeleteAsync(id, ct);
        return NoContent();
    }
}

[Route("api/v1/notifications")]
public class NotificationsController(NotificationService notifications) : ApiControllerBase
{
    [HttpGet]
    public Task<PagedResult<NotificationDto>> List([FromQuery] NotificationQuery q, CancellationToken ct) => notifications.ListMineAsync(q, ct);

    [HttpGet("unread-count")]
    public async Task<object> UnreadCount(CancellationToken ct) => new { count = await notifications.UnreadCountAsync(ct) };

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await notifications.MarkReadAsync(id, ct);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notifications.MarkAllReadAsync(ct);
        return NoContent();
    }
}
