# ---------- Etapa 1: build (imagen grande, con SDK) ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiamos primero SOLO el .csproj y restauramos: así Docker cachea la capa
# de "restore" y no la repite si solo cambió el código.
COPY TaskHub.Api.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Etapa 2: runtime (imagen chica, sin SDK) ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# .NET 8 escucha en el 8080 por defecto dentro del contenedor
EXPOSE 8080

# Buena práctica: no correr como root
USER $APP_UID

ENTRYPOINT ["dotnet", "TaskHub.Api.dll"]
