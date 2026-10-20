using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Punto de entrada del "worker". El host de Functions (func start) lo lanza y le manda
// los eventos (mensajes de cola, ticks del timer) por gRPC.
var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Los app settings (local.settings.json en tu PC, variables de entorno en Docker/Azure)
        // llegan como variables de entorno.
        var apiUrl = Environment.GetEnvironmentVariable("TaskHubApiUrl") ?? "http://localhost:5000";

        // HttpClient con nombre, para que la función de limpieza hable con la API.
        services.AddHttpClient("TaskHubApi", client => client.BaseAddress = new Uri(apiUrl));
    })
    .Build();

host.Run();
