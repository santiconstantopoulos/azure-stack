using System.Net.Http.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TaskHub.Functions;

/// <summary>
/// TIMER TRIGGER: se ejecuta según un cron. Acá borra las tareas hechas llamando a la API.
/// </summary>
public class CleanupCompletedFunction(IHttpClientFactory httpClientFactory, ILogger<CleanupCompletedFunction> logger)
{
    [Function(nameof(CleanupCompletedFunction))]
    public async Task Run(
        // %CleanupSchedule% = el cron se lee de un app setting (NCRONTAB de 6 campos:
        // segundo minuto hora día mes día-de-semana). "0 */2 * * * *" = cada 2 minutos.
        [TimerTrigger("%CleanupSchedule%")] TimerInfo timer,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Limpieza disparada. Próxima ejecución: {Next}", timer.ScheduleStatus?.Next);

        var client = httpClientFactory.CreateClient("TaskHubApi");

        try
        {
            var response = await client.DeleteAsync("/api/tasks/completed", cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<CleanupResult>(cancellationToken);
            logger.LogInformation("Limpieza OK: {Deleted} tarea(s) hecha(s) eliminada(s).", result?.Deleted ?? 0);
        }
        catch (HttpRequestException ex)
        {
            // Si la API está caída, lo registramos y esperamos al próximo tick.
            logger.LogError(ex, "No se pudo llamar a la API para limpiar tareas.");
        }
    }
}

public record CleanupResult(int Deleted);
