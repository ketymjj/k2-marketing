using FluentAssertions;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Common;
using K2Marketing.Application.Conversions;
using K2Marketing.Application.Creatives;
using K2Marketing.Application.Imports;
using K2Marketing.Application.Reports;
using K2Marketing.Domain;
using K2Marketing.Domain.Entities;

namespace K2Marketing.UnitTests;

public class CampaignStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Paused, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Completed)]
    [InlineData(CampaignStatus.Scheduled, CampaignStatus.Cancelled)]
    public void Allowed_transitions_change_status(CampaignStatus from, CampaignStatus to)
    {
        var c = new Campaign { Status = from };
        c.ChangeStatus(to, Now);
        c.Status.Should().Be(to);
        c.UpdatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(CampaignStatus.Completed, CampaignStatus.Active)]
    [InlineData(CampaignStatus.Cancelled, CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Paused)]
    [InlineData(CampaignStatus.Active, CampaignStatus.Draft)]
    public void Invalid_transitions_throw_domain_exception(CampaignStatus from, CampaignStatus to)
    {
        var c = new Campaign { Status = from };
        var act = () => c.ChangeStatus(to, Now);
        act.Should().Throw<DomainException>().Which.Code.Should().Be("campaign.invalidTransition");
        c.Status.Should().Be(from);
    }

    [Fact]
    public void Same_status_is_a_no_op()
    {
        var c = new Campaign { Status = CampaignStatus.Completed };
        c.ChangeStatus(CampaignStatus.Completed, Now);
        c.Status.Should().Be(CampaignStatus.Completed);
    }
}

public class CsvParserTests
{
    [Fact]
    public void Parses_semicolon_quotes_bom_and_normalizes_headers()
    {
        var csv = "﻿Date;Campaign Name;Spend\n2026-10-01;\"Verão; \"\"Promo\"\"\";1.234,56\n\n";
        var table = CsvParser.Parse(csv);
        table.Headers.Should().Equal("date", "campaign_name", "spend");
        table.Rows.Should().HaveCount(1);
        table.Rows[0].Get("campaign_name").Should().Be("Verão; \"Promo\"");
        table.Rows[0].LineNumber.Should().Be(2);
    }

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1234,56", 1234.56)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("R$ 99,90", 99.90)]
    public void Accepts_decimal_point_or_comma(string raw, double expected)
    {
        CsvParser.TryDecimal(raw, out var v).Should().BeTrue();
        v.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData("2026-10-05")]
    [InlineData("05/10/2026")]
    public void Accepts_iso_and_brazilian_dates(string raw)
    {
        CsvParser.TryDate(raw, out var d).Should().BeTrue();
        d.Should().Be(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void Rejects_invalid_dates() => CsvParser.TryDate("2026-13-45", out _).Should().BeFalse();

    [Fact]
    public void Escape_neutralizes_spreadsheet_formula_injection()
    {
        CsvParser.Escape("=HYPERLINK(\"x\")").Should().StartWith("\"'=");
        CsvParser.Escape("-12.5").Should().Be("-12.5");
    }
}

public class ConversionKeyTests
{
    private static readonly DateTime When = new(2026, 10, 10, 14, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void External_id_is_case_insensitive_key() =>
        ConversionKeys.For(null, ConversionType.Purchase, When, 100m, " PED-1 ").Should().Be(ConversionKeys.For(null, ConversionType.Purchase, When.AddDays(3), 999m, "ped-1"));

    [Fact]
    public void Same_lead_cannot_convert_twice_as_lead_type()
    {
        var lead = Guid.NewGuid();
        ConversionKeys.For(lead, ConversionType.Lead, When, 0m, null).Should().Be(ConversionKeys.For(lead, ConversionType.Lead, When.AddDays(1), 0m, null));
    }

    [Fact]
    public void Different_purchases_of_same_lead_are_distinct_events()
    {
        var lead = Guid.NewGuid();
        ConversionKeys.For(lead, ConversionType.Purchase, When, 100m, null).Should().NotBe(ConversionKeys.For(lead, ConversionType.Purchase, When.AddHours(2), 100m, null));
    }

    [Fact]
    public void Identical_event_fingerprint_is_stable() =>
        ConversionKeys.For(null, ConversionType.Purchase, When, 100m, null).Should().Be(ConversionKeys.For(null, ConversionType.Purchase, When.AddSeconds(20), 100m, null));
}

public class CreativeUploadPolicyTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    [Fact]
    public void Accepts_png_with_valid_signature()
    {
        var (ext, type) = CreativeUploadPolicy.Validate(CreativeType.Image, "foto.PNG", 2048, Png);
        ext.Should().Be(".png");
        type.Should().Be("image/png");
    }

    [Fact]
    public void Rejects_renamed_executable() =>
        FluentActions.Invoking(() => CreativeUploadPolicy.Validate(CreativeType.Image, "virus.png", 2048, "MZ\x90\0\0\0\0\0"u8.ToArray()))
            .Should().Throw<RequestValidationException>();

    [Fact]
    public void Rejects_extension_not_allowed_for_type() =>
        FluentActions.Invoking(() => CreativeUploadPolicy.Validate(CreativeType.Video, "a.png", 10, Png)).Should().Throw<RequestValidationException>();

    [Fact]
    public void Rejects_files_over_limit() =>
        FluentActions.Invoking(() => CreativeUploadPolicy.Validate(CreativeType.Image, "a.png", CreativeUploadPolicy.MaxImageBytes + 1, Png)).Should().Throw<RequestValidationException>();
}

public class ExecutiveObservationTests
{
    private static MetricsDto M(long clicks, decimal spend, long conv, decimal revenue, long impressions = 100_000) =>
        MetricsDto.From(new MetricTotals(impressions, impressions / 2, clicks, spend, 0, conv, revenue));

    [Fact]
    public void Best_roas_ignores_campaigns_without_volume()
    {
        var perf = new PerformanceReportDto(default, default, "campaign",
        [
            new PerformanceRowDto("a", "Pequena", null, M(clicks: 20, spend: 10m, conv: 1, revenue: 500m)),   // ROAS 50x mas só 20 cliques
            new PerformanceRowDto("b", "Robusta", null, M(clicks: 2_000, spend: 1_000m, conv: 40, revenue: 4_000m))
        ], M(2_020, 1_010m, 41, 4_500m));
        var dash = Dashboard(perf.Totals);

        var obs = ReportService.Observe(dash, perf, EmptyBudget());

        obs.Single(o => o.Code == "bestRoas").Params["campaign"].Should().Be("Robusta");
    }

    [Fact]
    public void No_data_period_yields_single_observation()
    {
        var empty = MetricsDto.Empty;
        var obs = ReportService.Observe(Dashboard(empty), new PerformanceReportDto(default, default, "campaign", [], empty), EmptyBudget());
        obs.Select(o => o.Code).Should().Equal("noData");
    }

    private static BudgetSummaryDto EmptyBudget() => new(0, 0, 0, null, 0, 0, 0, 0, []);

    private static DashboardDto Dashboard(MetricsDto m) => new(default, default, default, default, 1, 1, m, m, [], [], [], [],
        new CrmSummaryDto(0, 0, 0, 0, 0m), []);
}
