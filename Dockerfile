# --- ETAPA DE COMPILACIÓN ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY . .

RUN dotnet publish Nomina.csproj -c Release -o out

# --- ETAPA DE EJECUCIÓN ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV NOMINA_AUTO_OPEN=0

# Evita que .NET intente usar FileSystemWatcher (inotify) para vigilar
# cambios en appsettings.json. Necesario porque contenedores en la nube
# (Render, y muchos entornos Docker) tienen un límite muy bajo de
# instancias de inotify, y esa vigilancia no aporta nada útil en
# producción de todas formas (no vas a editar appsettings.json en vivo
# dentro de un contenedor desplegado).
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

EXPOSE 8080

ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Nomina.dll