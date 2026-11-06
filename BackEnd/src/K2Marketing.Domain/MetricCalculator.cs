namespace K2Marketing.Domain;

/// <summary>Totais brutos (somados a partir de CampaignMetrics). Os indicadores derivados são sempre calculados aqui.</summary>
public record MetricTotals(
    long Impressions, long Reach, long Clicks, decimal Spend, long Leads, long Conversions, decimal AttributedRevenue)
{
    public static readonly MetricTotals Empty = new(0, 0, 0, 0m, 0, 0, 0m);

    public decimal? Ctr => MetricCalculator.Ctr(Clicks, Impressions);
    public decimal? Cpc => MetricCalculator.Cpc(Spend, Clicks);
    public decimal? Cpm => MetricCalculator.Cpm(Spend, Impressions);
    public decimal? Cpl => MetricCalculator.Cpl(Spend, Leads);
    public decimal? Cpa => MetricCalculator.Cpa(Spend, Conversions);
    public decimal? Roas => MetricCalculator.Roas(AttributedRevenue, Spend);

    public MetricTotals Add(MetricTotals o) => new(
        Impressions + o.Impressions, Reach + o.Reach, Clicks + o.Clicks, Spend + o.Spend,
        Leads + o.Leads, Conversions + o.Conversions, AttributedRevenue + o.AttributedRevenue);
}

/// <summary>
/// Fórmulas oficiais do K2 Marketing. Quando o denominador é zero o indicador é indefinido e retorna
/// <c>null</c> (exibido como "—"), em vez de 0 ou infinito, para não sugerir um resultado inexistente.
/// </summary>
public static class MetricCalculator
{
    /// <summary>CTR (%) = (Cliques / Impressões) × 100</summary>
    public static decimal? Ctr(long clicks, long impressions) =>
        impressions <= 0 ? null : Round((decimal)clicks / impressions * 100m, 4);

    /// <summary>CPC = Investimento / Cliques</summary>
    public static decimal? Cpc(decimal spend, long clicks) => clicks <= 0 ? null : Round(spend / clicks, 4);

    /// <summary>CPM = (Investimento / Impressões) × 1.000</summary>
    public static decimal? Cpm(decimal spend, long impressions) =>
        impressions <= 0 ? null : Round(spend / impressions * 1000m, 4);

    /// <summary>CPL = Investimento / Leads</summary>
    public static decimal? Cpl(decimal spend, long leads) => leads <= 0 ? null : Round(spend / leads, 4);

    /// <summary>CPA = Investimento / Conversões</summary>
    public static decimal? Cpa(decimal spend, long conversions) => conversions <= 0 ? null : Round(spend / conversions, 4);

    /// <summary>ROAS = Receita atribuída / Investimento</summary>
    public static decimal? Roas(decimal revenue, decimal spend) => spend <= 0 ? null : Round(revenue / spend, 4);

    /// <summary>Percentual consumido de um orçamento (0–∞). Orçamento zero retorna null.</summary>
    public static decimal? BudgetUsage(decimal spent, decimal planned) =>
        planned <= 0 ? null : Round(spent / planned * 100m, 2);

    /// <summary>Maior limite de alerta (80/90/100) atingido pelo consumo, ou 0.</summary>
    /// <remarks>Compara valores exatos (sem arredondar): 79,999% não dispara o alerta de 80%.</remarks>
    public static int BudgetThreshold(decimal spent, decimal planned)
    {
        if (planned <= 0) return 0;
        if (spent >= planned) return 100;
        if (spent >= planned * 0.9m) return 90;
        return spent >= planned * 0.8m ? 80 : 0;
    }

    private static decimal Round(decimal v, int digits) => Math.Round(v, digits, MidpointRounding.AwayFromZero);
}
