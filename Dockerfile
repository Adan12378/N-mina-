# --- ETAPA DE COMPILACIÓN ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY . .

RUN dotnet publish Nomina.csproj -c Release -o out

# 🔍 DIAGNÓSTICO TEMPORAL: lista el contenido de /app/out en los logs
# de build de Render, para confirmar si Frontend/ quedó incluido.
# (Quitaremos esta línea una vez resuelto el problema).
RUN echo "===== CONTENIDO DE /app/out =====" && ls -la /app/out && echo "===== CONTENIDO DE /app/out/Frontend =====" && ls -la /app/out/Frontend || echo "‼️ LA CARPETA Frontend NO EXISTE EN /app/out"

# --- ETAPA DE EJECUCIÓN ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV NOMINA_AUTO_OPEN=0
ENV DOTNET_hostBuilder__reloadConfigOnChange=false

EXPOSE 8080

ENTRYPOINT ASPNETCORE_URLS=http://+:${PORT:-8080} dotnet Nomina.dll
