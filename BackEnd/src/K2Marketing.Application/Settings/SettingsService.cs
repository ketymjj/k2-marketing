using System.Text.RegularExpressions;
using FluentValidation;
using K2Marketing.Application.Common;
using K2Marketing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace K2Marketing.Application.Settings;

public record AgencySettingsDto(string AgencyName, string PrimaryColor, string? ContactEmail, string? Website, string? ReportFooter, string Currency);
public record AuditLogDto(long Id, DateTime CreatedAt, string UserEmail, string Action, string Entity, string? EntityId, string? Details);

public class AuditQuery : PageQuery
{
    public string? Entity { get; set; }
    public string? Action { get; set; }
}

public partial class AgencySettingsValidator : AbstractValidator<AgencySettingsDto>
{
    public AgencySettingsValidator()
    {
        RuleFor(x => x.AgencyName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.PrimaryColor).NotEmpty().Matches(HexColor()).WithMessage("Use uma cor hexadecimal (#RRGGBB).");
        RuleFor(x => x.ContactEmail).EmailAddress().MaximumLength(200).When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
        RuleFor(x => x.Website).MaximumLength(300);
        RuleFor(x => x.ReportFooter).MaximumLength(500);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}

public class SettingsService(IAppDbContext db, AuditService audit, IValidator<AgencySettingsDto> validator)
{
    public async Task<AgencySettingsDto> GetAsync(CancellationToken ct)
    {
        var s = await db.AgencySettings.AsNoTracking().FirstOrDefaultAsync(ct) ?? new AgencySettings();
        return new AgencySettingsDto(s.AgencyName, s.PrimaryColor, s.ContactEmail, s.Website, s.ReportFooter, s.Currency);
    }

    public async Task<AgencySettingsDto> UpdateAsync(AgencySettingsDto r, CancellationToken ct)
    {
        await validator.ValidateOrThrowAsync(r, ct);
        var s = await db.AgencySettings.FirstOrDefaultAsync(ct);
        if (s is null) db.AgencySettings.Add(s = new AgencySettings { Id = 1 });
        s.AgencyName = r.AgencyName.Trim();
        s.PrimaryColor = r.PrimaryColor;
        s.ContactEmail = string.IsNullOrWhiteSpace(r.ContactEmail) ? null : r.ContactEmail.Trim();
        s.Website = string.IsNullOrWhiteSpace(r.Website) ? null : r.Website.Trim();
        s.ReportFooter = string.IsNullOrWhiteSpace(r.ReportFooter) ? null : r.ReportFooter.Trim();
        s.Currency = r.Currency.ToUpperInvariant();
        audit.Log("Update", "AgencySettings", 1, s.AgencyName);
        await db.SaveChangesAsync(ct);
        return await GetAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> AuditAsync(AuditQuery q, CancellationToken ct)
    {
        var query = db.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.Entity)) query = query.Where(a => a.Entity == q.Entity);
        if (!string.IsNullOrWhiteSpace(q.Action)) query = query.Where(a => a.Action == q.Action);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(a => a.UserEmail.Contains(s) || (a.Details != null && a.Details.ToLower().Contains(s)));
        }
        return await query.OrderByDescending(a => a.Id)
            .Select(a => new AuditLogDto(a.Id, a.CreatedAt, a.UserEmail, a.Action, a.Entity, a.EntityId, a.Details)).ToPagedAsync(q, ct);
    }
}
