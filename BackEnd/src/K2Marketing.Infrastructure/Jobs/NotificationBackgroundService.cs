using K2Marketing.Application.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K2Marketing.Infrastructure.Jobs;

/// <summary>Executa o <see cref="NotificationScanner"/> na inicialização e depois a cada N minutos (padrão: 30).</summary>
public class NotificationBackgroundService(IServiceScopeFactory scopes, IConfiguration config, ILogger<NotificationBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, config.GetValue("Jobs:NotificationIntervalMinutes", 30)));
        try
        {
            // Aguarda a aplicação de migrations/seed na inicialização.
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            using var timer = new PeriodicTimer(interval);
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var created = await scope.ServiceProvider.GetRequiredService<NotificationScanner>().RunAsync(stoppingToken);
                    if (created > 0) logger.LogInformation("Verificação de notificações gerou {Count} aviso(s)", created);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Falha na verificação periódica de notificações");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal da aplicação (ou falha ao iniciar): não é erro.
        }
    }
}
