# TaskHub — proyecto para aprender Docker y Azure Functions

App de tareas con **API .NET 8**, **frontend Angular 20** y **Azure Functions** (cola + temporizador). Se puede correr de dos maneras: directo en tu PC (sin Docker, todo gratis) o completa con Docker Compose.

```
taskhub/
├── api/          # .NET 8 minimal API + EF Core (SQLite local / SQL Server en Docker)
├── functions/    # Azure Functions (.NET 8 isolated): Queue trigger + Timer trigger
├── web/          # Angular 20 (+ Dockerfile y nginx.conf)
├── docker-compose.yml   # db + azurite + api + functions + web
└── .github/workflows/ci.yml   # CI: compila todo y publica imágenes en ghcr.io
```

## Cómo se conecta todo

```
 Angular ──/api──▶ API ──▶ base de datos (SQLite / SQL Server)
                    │
                    └─ al crear una tarea: mensaje ──▶ Cola "task-created" (Azurite)
                                                            │
                                                            ▼
                                    Function 1 (Queue trigger): registra "Nueva tarea #N"

 Function 2 (Timer trigger, cada 2 min) ──DELETE /api/tasks/completed──▶ API
```

## Conceptos de Azure Functions (lo que este proyecto practica)

- **Function App**: el proyecto `functions/`. Es un contenedor lógico de funciones que se despliegan y escalan juntas.
- **Trigger**: lo que dispara una función. Cada función tiene exactamente uno. Acá: `QueueTrigger` (llega un mensaje) y `TimerTrigger` (llega la hora del cron).
- **Binding**: forma declarativa de leer o escribir datos (colas, blobs, bases) sin escribir el código cliente. El trigger es un binding de entrada. Un buen ejercicio es agregar un *output binding* que escriba en otra cola.
- **Host y worker (isolated)**: `func start` levanta el *host* de Functions, que escucha los eventos y se los manda por gRPC a tu código .NET, que corre en un proceso aparte (el *worker*). Por eso `Program.cs` se parece a una app normal y permite inyección de dependencias.
- **Configuración**: `local.settings.json` en tu PC; en Docker o Azure son *app settings* (variables de entorno). Por eso el cron está como `%CleanupSchedule%` y no escrito en el código. **Nunca se sube a git** (ver `.gitignore`).
- **Storage**: el host guarda su estado (por ejemplo, qué ticks del timer ya ejecutó) en una cuenta de Azure Storage (`AzureWebJobsStorage`). Local lo reemplaza **Azurite**.
- **Reintentos y poison queue**: si la función falla, el mensaje se reintenta; tras `maxDequeueCount` (5, ver `host.json`) pasa a `task-created-poison`.
- **Modelo de costos**: en el plan *Consumption* se paga por ejecución y el escalado es automático, incluso a cero instancias. Por eso un diseño basado en eventos (cola) es barato para cargas esporádicas.

## Opción A — Correr en tu PC, sin Docker (gratis, sin cuenta de Azure)

Requisitos: .NET 8 SDK, Node 20.19+ o 22, **Azure Functions Core Tools v4** y **Azurite**.

```bash
npm i -g azure-functions-core-tools@4 --unsafe-perm true
npm i -g azurite
```

Abrí 4 terminales, en este orden:

```bash
# 1) Emulador de Azure Storage (cola). Dejalo corriendo.
azurite --location .azurite --skipApiVersionCheck

# 2) API con la cola activada (perfil "TaskHub.Api.Queue" en Visual Studio, o:)
cd api
dotnet run --launch-profile TaskHub.Api.Queue

# 3) Azure Functions
cd functions
func start

# 4) Frontend
cd web
npm install
npm start
```

Cómo probar:
1. Abrí `http://localhost:4200` y creá una tarea. En la terminal de `func start` debería aparecer `Nueva tarea #1: "..."`: es el **Queue trigger**.
2. Marcá la tarea como hecha. Dentro de 2 minutos máximo el **Timer trigger** dispara la limpieza y aparece `Limpieza OK: 1 tarea(s)...`. Recargá la página y la tarea ya no está.
3. Probá apagar Azurite y crear una tarea: la API la guarda igual y registra un warning (la cola no es crítica).
4. Apagá la API y esperá un tick del timer: la función registra el error y sigue viva.

Si la API se ejecuta con el perfil normal (sin `.Queue`), funciona igual, pero sin publicar eventos.

> En Visual Studio también podés ejecutar el proyecto `functions` con F5 si tenés instalada la carga de trabajo "Desarrollo de Azure".

## Opción B — Todo con Docker Compose

Requisito: Docker Desktop (o Docker Engine en WSL2).

```bash
cp .env.example .env        # y cambiá la contraseña de SQL Server
docker compose up --build
```

Abrí `http://localhost:4200`. Para ver las funciones: `docker compose logs -f functions`.
Servicios: `db` (SQL Server), `azurite`, `api`, `functions`, `web`.

**Ejercicios**
1. `docker images`: ¿cuánto pesa cada imagen? ¿Por qué la de `api` es mucho más chica que el SDK con el que se compila?
2. Cambiá una línea de `Program.cs` y reconstruí: ¿qué capas se reutilizan del caché?
3. `docker compose stop azurite`, creá una tarea y mirá `docker compose logs api`.
4. `docker compose down` y `up`: ¿siguen los datos? Ahora con `down -v`: ¿y ahora?
5. Cambiá `CleanupSchedule` a cada 10 segundos (`"*/10 * * * * *"`) y observá.

## Plan gratuito de Azure Functions (si más adelante querés desplegar en Azure)

- Practicar **local** (Core Tools + Azurite) es 100% gratis y no pide cuenta.
- La **cuenta gratuita de Azure** da USD 200 de crédito por 30 días y Azure Functions figura como "siempre gratis" con 1 millón de requests por mes; pide tarjeta de crédito o débito (no prepaga).
- Según la página de precios, el plan *Consumption* incluye 1 millón de ejecuciones y 400.000 GB-s por mes; *Flex Consumption*, 250.000 ejecuciones y 100.000 GB-s. La cuenta de storage de la Function no entra en esa cuota.
- Si desplegás, creá una **alerta de presupuesto** primero. Despliegue: `func azure functionapp publish <nombre>`.

Los valores pueden cambiar: verificalos en [Azure Functions pricing](https://azure.microsoft.com/pricing/details/functions/).

## Subirlo a GitHub

```bash
git init -b main
git add .
git status        # verificá que NO aparezcan .env, local.settings.json, *.db, bin/ ni obj/
git commit -m "TaskHub: API .NET + Angular + Azure Functions + Docker"

# con GitHub CLI:
gh repo create taskhub --private --source . --push
# o creá el repo vacío en github.com y:
git remote add origin https://github.com/<tu-usuario>/taskhub.git
git push -u origin main
```

Al hacer push a `main`, GitHub Actions compila la API, las funciones y el front, y publica las tres imágenes en `ghcr.io/<tu-usuario>/taskhub-api`, `-web` y `-functions`.

## Problemas típicos

- **`func start` dice que no puede conectar con el storage**: Azurite no está corriendo.
- **Azurite responde "API version not supported"**: arrancalo con `--skipApiVersionCheck` (ya incluido arriba y en el compose).
- **El mensaje cae en `task-created-poison`**: el contenido no es JSON válido o la función lanzó una excepción 5 veces. El mensaje de la API va en Base64, que es lo que espera el trigger.
- **La API no conecta a la base en Docker**: el host es `db`, no `localhost`.
- **SQL Server se reinicia solo**: la contraseña no cumple la política (8+ caracteres, mayúscula, minúscula, número y símbolo) o falta RAM (~2 GB).
- **.NET 8**: según el calendario de soporte de Microsoft, su soporte termina en noviembre de 2026. Para un proyecto nuevo y duradero conviene migrar a la versión LTS siguiente.
