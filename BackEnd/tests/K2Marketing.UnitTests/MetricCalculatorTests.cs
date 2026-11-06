using FluentAssertions;
using K2Marketing.Domain;

namespace K2Marketing.UnitTests;

public class MetricCalculatorTests
{
    [Theory]
    [InlineData(250, 10_000, 2.5)]
    [InlineData(1, 3, 33.3333)]
    [InlineData(0, 5_000, 0)]
    public void Ctr_is_clicks_over_impressions_times_100(long clicks, long impressions, double expected) =>
        MetricCalculator.Ctr(clicks, impressions).Should().Be((decimal)expected);

    [Fact]
    public void Cpc_is_spend_over_clicks() => MetricCalculator.Cpc(150m, 120).Should().Be(1.25m);

    [Fact]
    public void Cpm_is_spend_over_impressions_times_1000() => MetricCalculator.Cpm(45m, 18_000).Should().Be(2.5m);

    [Fact]
    public void Cpl_is_spend_over_leads() => MetricCalculator.Cpl(1_000m, 40).Should().Be(25m);

    [Fact]
    public void Cpa_is_spend_over_conversions() => MetricCalculator.Cpa(900m, 12).Should().Be(75m);

    [Fact]
    public void Roas_is_revenue_over_spend() => MetricCalculator.Roas(4_500m, 1_200m).Should().Be(3.75m);

    [Fact]
    public void Division_by_zero_returns_null_instead_of_zero_or_infinity()
    {
        MetricCalculator.Ctr(10, 0).Should().BeNull();
        MetricCalculator.Cpc(100m, 0).Should().BeNull();
        MetricCalculator.Cpm(100m, 0).Should().BeNull();
        MetricCalculator.Cpl(100m, 0).Should().BeNull();
        MetricCalculator.Cpa(100m, 0).Should().BeNull();
        MetricCalculator.Roas(100m, 0m).Should().BeNull();
    }

    [Fact]
    public void Zero_spend_with_clicks_gives_zero_cost_not_null() => MetricCalculator.Cpc(0m, 50).Should().Be(0m);

    [Fact]
    public void Monetary_values_keep_decimal_precision() =>
        MetricCalculator.Cpc(0.1m + 0.2m, 3).Should().Be(0.1m); // com double seria 0,10000000000000002

    [Fact]
    public void Aggregated_kpis_use_summed_totals_not_average_of_averages()
    {
        // Dia 1: CTR 10% (10/100). Dia 2: CTR 1% (100/10.000). Média simples seria 5,5%; o correto é 110/10.100.
        var total = new MetricTotals(100, 0, 10, 10m, 0, 0, 0m).Add(new MetricTotals(10_000, 0, 100, 90m, 0, 0, 0m));
        total.Ctr.Should().Be(1.0891m);
        total.Cpc.Should().Be(0.9091m);
    }

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(799.99, 1000, 0)]
    [InlineData(800, 1000, 80)]
    [InlineData(950, 1000, 90)]
    [InlineData(1000, 1000, 100)]
    [InlineData(1500, 1000, 100)]
    [InlineData(10, 0, 0)]
    public void Budget_threshold_reports_highest_alert_level(double spent, double planned, int expected) =>
        MetricCalculator.BudgetThreshold((decimal)spent, (decimal)planned).Should().Be(expected);

    [Fact]
    public void Budget_usage_is_percentage_and_null_without_plan()
    {
        MetricCalculator.BudgetUsage(375m, 1_500m).Should().Be(25m);
        MetricCalculator.BudgetUsage(375m, 0m).Should().BeNull();
    }
}
