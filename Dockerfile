# --- ETAPA DE COMPILACIÓN ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

# Copiamos todo el código fuente
COPY . .

# Publicamos la aplicación en modo Release
RUN dotnet publish Nomina.csproj -c Release -o out

# --- ETAPA DE EJECUCIÓN ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

# Render asigna dinámicamente el puerto mediante la variable de entorno PORT.
# No fijamos un puerto fijo: dejamos que ASPNETCORE_URLS se arme en tiempo
# de ejecución usando esa variable (ver ENTRYPOINT abajo).
ENV ASPNETCORE_ENVIRONMENT=Production
ENV NOMINA_AUTO_OPEN=0

EXPOSE 8080

# Usamos la forma "shell" del ENTRYPOINT para poder expandir la variable
# $PORT que Render inyecta en tiempo de ejecución (no está disponible
# durante el build, así que no puede fijarse antes con ENV).
ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Nomina.dll