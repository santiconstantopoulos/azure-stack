using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace TaskHub.Functions;

/// <summary>
/// QUEUE TRIGGER: se ejecuta cada vez que llega un mensaje a la cola "task-created".
/// La API publica el mensaje al crear una tarea; esta función lo procesa.
/// </summary>
public class TaskCreatedFunction(ILogger<TaskCreatedFunction> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Function(nameof(TaskCreatedFunction))]
    public void Run(
        // "QueueConnection" es el NOMBRE de un app setting que contiene la cadena de conexión.
        [QueueTrigger("task-created", Connection = "QueueConnection")] string message)
    {
        var evt = JsonSerializer.Deserialize<TaskCreatedMessage>(message, Json);

        if (evt is null)
        {
            logger.LogWarning("Mensaje vacío o inválido: {Message}", message);
            return;
        }

        logger.LogInformation(
            "Nueva tarea #{Id}: \"{Title}\" (creada {CreatedAt:u})",
            evt.Id, evt.Title, evt.CreatedAt);

        // Acá iría la lógica real: mandar un email, avisar por Teams/Slack, indexar, etc.
        // Si esta función lanza una excepción, el mensaje se reintenta y, tras 5 intentos
        // (maxDequeueCount), pasa a la cola "task-created-poison".
    }
}

public record TaskCreatedMessage(int Id, string Title, DateTime CreatedAt);
