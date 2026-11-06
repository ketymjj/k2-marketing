using FluentValidation;
using K2Marketing.Application.Ads;
using K2Marketing.Application.Analytics;
using K2Marketing.Application.Auth;
using K2Marketing.Application.Budgets;
using K2Marketing.Application.Campaigns;
using K2Marketing.Application.Clients;
using K2Marketing.Application.Common;
using K2Marketing.Application.Conversions;
using K2Marketing.Application.Creatives;
using K2Marketing.Application.Imports;
using K2Marketing.Application.Integrations;
using K2Marketing.Application.Leads;
using K2Marketing.Application.Metrics;
using K2Marketing.Application.Notifications;
using K2Marketing.Application.Reports;
using K2Marketing.Application.Settings;
using K2Marketing.Application.Tasks;
using K2Marketing.Application.Users;
using K2Marketing.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace K2Marketing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);

        services.AddScoped<AccessScope>();
        services.AddScoped<AuditService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
        services.AddScoped<ClientService>();
        services.AddScoped<CampaignService>();
        services.AddScoped<AdvertisementService>();
        services.AddScoped<CreativeService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<NotificationScanner>();
        services.AddScoped<BudgetSyncService>();
        services.AddScoped<BudgetService>();
        services.AddScoped<LeadService>();
        services.AddScoped<ConversionService>();
        services.AddScoped<MetricService>();
        services.AddScoped<ImportService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ComparisonService>();
        services.AddScoped<ReportService>();
        services.AddScoped<TaskService>();
        services.AddScoped<SettingsService>();

        // Integrações futuras: adaptadores explícitos de "não configurado" (sem credenciais e sem dados simulados).
        services.AddSingleton<IAdPlatformConnector>(new NotConfiguredAdConnector(AdPlatform.MetaAds, "Meta Marketing API"));
        services.AddSingleton<IAdPlatformConnector>(new NotConfiguredAdConnector(AdPlatform.GoogleAds, "Google Ads API"));
        services.AddSingleton<IAdPlatformConnector>(new NotConfiguredAdConnector(AdPlatform.TikTokAds, "TikTok Marketing API"));
        services.AddSingleton<IAdPlatformConnector>(new NotConfiguredAdConnector(AdPlatform.LinkedInAds, "LinkedIn Marketing API"));
        services.AddSingleton<IAnalyticsConnector, NotConfiguredAnalyticsConnector>();
        services.AddSingleton<IWebhookPublisher, NotConfiguredWebhookPublisher>();
        services.AddSingleton<IAiInsightsService, NotConfiguredAiInsightsService>();
        services.AddSingleton<IntegrationCatalog>();
        return services;
    }
}
