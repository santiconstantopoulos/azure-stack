using System.Text.Json;
using Azure.Storage.Queues;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------- Base de datos ----------
// El proveedor se elige por configuración, no por código:
//   - Local (por defecto): SQLite, archivo "taskhub.db" junto a la API.
//   - Docker: variables de entorno Database__Provider=SqlServer y ConnectionStrings__Default=...
var provider = builder.Configuration["Database:Provider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=taskhub.db";

builder.Services.AddDbContext<AppDb>(options =>
{
    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
        options.UseSqlServer(connectionString);
    else
        options.UseSqlite(connectionString);
});

// ---------- Cola de mensajes (opcional) ----------
// Si "Queue:ConnectionString" está configurada, cada tarea nueva publica un mensaje
// en una Azure Storage Queue, y una Azure Function lo procesa (ver carpeta /functions).
// Si no está configurada, la API funciona igual, sin cola.
//   - Local: Queue__ConnectionString=UseDevelopmentStorage=true  (Azurite)
var queueConnection = builder.Configuration["Queue:ConnectionString"];
var queueName = builder.Configuration["Queue:Name"] ?? "task-created";

if (!string.IsNullOrWhiteSpace(queueConnection))
{
    var queueOptions = new QueueClientOptions
    {
        // La extensión de Functions espera mensajes en Base64 por defecto.
        MessageEncoding = QueueMessageEncoding.Base64
    };
    // Que la cola caída no congele la API: pocos reintentos y timeout corto.
    queueOptions.Retry.MaxRetries = 1;
    queueOptions.Retry.NetworkTimeout = TimeSpan.FromSeconds(3);

    builder.Services.AddSingleton(new QueueClient(queueConnection, queueName, queueOptions));
}

var app = builder.Build();

// ---------- Crear la base al arrancar ----------
// EnsureCreated crea las tablas si no existen (simple, ideal para aprender).
// SQL Server en Docker tarda en arrancar, así que reintentamos unos segundos.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            db.Database.EnsureCreated();
            break;
        }
        catch (Exception ex) when (attempt < 15)
        {
            Console.WriteLine($"[db] intento {attempt}/15 falló: {ex.Message}");
            await Task.Delay(3000);
        }
    }
}

// ---------- Endpoints ----------
app.MapGet("/", () => Results.Redirect("/health"));
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    provider,
    queue = string.IsNullOrWhiteSpace(queueConnection) ? "disabled" : queueName
}));

var tasks = app.MapGroup("/api/tasks");

tasks.MapGet("/", async (AppDb db) =>
    await db.Tasks.OrderBy(t => t.Id).ToListAsync());

tasks.MapPost("/", async (AppDb db, IServiceProvider services, ILogger<Program> logger, CreateTaskRequest request) =>
{
    var title = request.Title?.Trim();

    if (string.IsNullOrEmpty(title) || title.Length > 200)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["title"] = ["El título es obligatorio y admite hasta 200 caracteres."]
        });
    }

    var task = new TaskItem { Title = title };
    db.Tasks.Add(task);
    await db.SaveChangesAsync();

    // Evento: "se creó una tarea". Si la cola no está configurada, GetService devuelve null.
    await TaskEvents.PublishCreatedAsync(services.GetService<QueueClient>(), task, logger);

    return Results.Created($"/api/tasks/{task.Id}", task);
});

tasks.MapPut("/{id:int}/toggle", async (AppDb db, int id) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();

    task.Done = !task.Done;
    await db.SaveChangesAsync();
    return Results.Ok(task);
});

// Borra todas las tareas hechas. La usa la función programada (Timer) de Azure Functions.
tasks.MapDelete("/completed", async (AppDb db) =>
{
    var deleted = await db.Tasks.Where(t => t.Done).ExecuteDeleteAsync();
    return Results.Ok(new { deleted });
});

tasks.MapDelete("/{id:int}", async (AppDb db, int id) =>
{
    var task = await db.Tasks.FindAsync(id);
    if (task is null) return Results.NotFound();

    db.Tasks.Remove(task);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();

// ---------- Modelos ----------
public record CreateTaskRequest(string? Title);

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public bool Done { get; set; }
}

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
}

// ---------- Eventos ----------
public static class TaskEvents
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task PublishCreatedAsync(QueueClient? queue, TaskItem task, ILogger logger)
    {
        if (queue is null) return;

        try
        {
            await queue.CreateIfNotExistsAsync();
            var payload = JsonSerializer.Serialize(new { task.Id, task.Title, CreatedAt = DateTime.UtcNow }, Json);
            await queue.SendMessageAsync(payload);
        }
        catch (Exception ex)
        {
            // La tarea ya se guardó: un fallo de la cola no debe romper la API.
            logger.LogWarning(ex, "No se pudo publicar el mensaje en la cola; la tarea se guardó igual.");
        }
    }
}
