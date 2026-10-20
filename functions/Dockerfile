# ---------- Etapa 1: build (SDK de .NET) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Primero solo el .csproj: se cachea la capa de "restore".
COPY TaskHub.Functions.csproj .
RUN dotnet restore

COPY . .
# Una Function App se publica en /home/site/wwwroot (es lo que espera la imagen base).
RUN dotnet publish -c Release -o /home/site/wwwroot --no-restore

# ---------- Etapa 2: runtime de Azure Functions ----------
# Imagen oficial con el host de Functions v4 + worker .NET 8 isolated.
# (Si el tag cambia, generá uno actualizado con: func init --worker-runtime dotnet-isolated --docker)
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true

COPY --from=build /home/site/wwwroot /home/site/wwwroot
