using K2Marketing.Domain;

namespace K2Marketing.Application.Integrations;

/// <summary>Métrica diária recebida de uma plataforma externa (contrato para integrações futuras).</summary>
public record ExternalDailyMetric(string ExternalCampaignId, string? ExternalAdId, DateOnly Date, long Impressions, long Reach, long Clicks,
    decimal Spend, long Leads, long Conversions, decimal AttributedRevenue);

public enum IntegrationStatus { NotConfigured, Configured, Error }

public record IntegrationInfoDto(string Key, string Name, string Category, IntegrationStatus Status, string Description);

/// <summary>
/// Conector de plataforma de anúncios (Meta Marketing API, Google Ads API...). Nesta versão nenhum conector está
/// autenticado: as implementações apenas informam "não configurado" e nunca simulam dados.
/// </summary>
public interface IAdPlatformConnector
{
    AdPlatform Platform { get; }
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyList<ExternalDailyMetric>> FetchDailyMetricsAsync(string externalAccountId, DateOnly from, DateOnly to, CancellationToken ct);
}

public interface IAnalyticsConnector
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<IReadOnlyDictionary<DateOnly, long>> FetchSessionsAsync(string propertyId, DateOnly from, DateOnly to, CancellationToken ct);
}

/// <summary>Envio de eventos (lead criado, conversão, orçamento) para webhooks configurados pelo cliente.</summary>
public interface IWebhookPublisher
{
    bool IsConfigured { get; }
    Task PublishAsync(string eventName, object payload, CancellationToken ct);
}

/// <summary>Serviço de IA para insights e sugestões de copy (ex.: Claude). Não configurado nesta versão.</summary>
public interface IAiInsightsService
{
    bool IsConfigured { get; }
    Task<string> SummarizePerformanceAsync(object reportData, string language, CancellationToken ct);
}

public class IntegrationNotConfiguredException(string name)
    : DomainException("integration.notConfigured", $"A integração {name} não está configurada nesta versão.");

/// <summary>Adaptador "não configurado": deixa explícito que não há conexão real com a plataforma.</summary>
public class NotConfiguredAdConnector(AdPlatform platform, string name) : IAdPlatformConnector
{
    public AdPlatform Platform { get; } = platform;
    public string Name { get; } = name;
    public bool IsConfigured => false;

    public Task<IReadOnlyList<ExternalDailyMetric>> FetchDailyMetricsAsync(string externalAccountId, DateOnly from, DateOnly to, CancellationToken ct) =>
        throw new IntegrationNotConfiguredException(Name);
}

public class NotConfiguredAnalyticsConnector : IAnalyticsConnector
{
    public string Name => "Google Analytics";
    public bool IsConfigured => false;
    public Task<IReadOnlyDictionary<DateOnly, long>> FetchSessionsAsync(string propertyId, DateOnly from, DateOnly to, CancellationToken ct) =>
        throw new IntegrationNotConfiguredException(Name);
}

public class NotConfiguredWebhookPublisher : IWebhookPublisher
{
    public bool IsConfigured => false;
    // Sem webhooks configurados o evento é descartado silenciosamente (não é erro de negócio).
    public Task PublishAsync(string eventName, object payload, CancellationToken ct) => Task.CompletedTask;
}

public class NotConfiguredAiInsightsService : IAiInsightsService
{
    public bool IsConfigured => false;
    public Task<string> SummarizePerformanceAsync(object reportData, string language, CancellationToken ct) =>
        throw new IntegrationNotConfiguredException("IA");
}

public class IntegrationCatalog(IEnumerable<IAdPlatformConnector> ads, IAnalyticsConnector analytics, IWebhookPublisher webhooks, IAiInsightsService ai)
{
    public List<IntegrationInfoDto> List()
    {
        static IntegrationStatus S(bool ok) => ok ? IntegrationStatus.Configured : IntegrationStatus.NotConfigured;
        var list = ads.Select(a => new IntegrationInfoDto(a.Platform.ToString(), a.Name, "ads", S(a.IsConfigured), $"integrations.{a.Platform}")).ToList();
        list.Add(new IntegrationInfoDto("GoogleAnalytics", analytics.Name, "analytics", S(analytics.IsConfigured), "integrations.GoogleAnalytics"));
        list.Add(new IntegrationInfoDto("Webhooks", "Webhooks", "events", S(webhooks.IsConfigured), "integrations.Webhooks"));
        list.Add(new IntegrationInfoDto("AI", "AI Insights", "ai", S(ai.IsConfigured), "integrations.AI"));
        return list;
    }
}
