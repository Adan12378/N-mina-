
using System.Text.Json.Serialization;
using Nomina.Backend.Data;
using Nomina.Backend.Models;
using Nomina.Backend.Services;

// =====================================================================
// 1. CONFIGURACIÓN DEL WEBROOT
// El frontend vive en /Frontend (fuera de la convención estándar
// "wwwroot"), así que se lo indicamos a ASP.NET Core desde el momento
// de crear el builder (WebApplicationOptions), ya que en versiones
// recientes de .NET no se puede cambiar el WebRoot después de creado
// el builder con builder.WebHost.UseWebRoot().
// =====================================================================
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Frontend"
});

// =====================================================================
// 2. SERIALIZACIÓN JSON
// - Los enums (TipoContrato, EstadoEmpleado, TipoRecargo) se envían
//   como texto ("Activo") en vez de números (0,1,2) para que el
//   Frontend los lea de forma legible sin mapear índices a mano.
// - PropertyNamingPolicy null respeta el PascalCase de C# tal cual
//   está en los modelos (Empleado.Nombre -> "Nombre" en el JSON).
// =====================================================================
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.PropertyNamingPolicy = null;
});

// =====================================================================
// 3. INYECCIÓN DE DEPENDENCIAS
// Los servicios abren su propio DbContext por método (ver EmpleadoService
// y NominaService), así que aquí basta con registrarlos como Scoped/
// Transient simples para que los endpoints los reciban por parámetro.
// =====================================================================
builder.Services.AddScoped<EmpleadoService>();
builder.Services.AddScoped<NominaService>();
builder.Services.AddScoped<ExcelService>();

// =====================================================================
// 4. CORS (solo relevante en desarrollo, ej. si sirves el Frontend
// con Live Server en otro puerto mientras programas). En producción,
// como el Frontend se sirve desde el mismo servidor, no se necesita.
// =====================================================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("DesarrolloLocal", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// =====================================================================
// 5. INICIALIZACIÓN DE LA BASE DE DATOS
// Crea el archivo SQLite y las tablas si no existen (ver Database.cs).
// Se ejecuta una sola vez, al arrancar la aplicación.
// =====================================================================
AppDbContext.Inicializar();

app.UseCors("DesarrolloLocal");

// =====================================================================
// 6. ARCHIVOS ESTÁTICOS (EL FRONTEND)
// UseDefaultFiles busca automáticamente "index.html" cuando se visita "/".
// UseStaticFiles sirve css, js e íconos desde la carpeta Frontend.
// =====================================================================
app.UseDefaultFiles();
app.UseStaticFiles();

// =====================================================================
// 7. ENDPOINTS DE LA API — EMPLEADOS
// =====================================================================
var empleadosApi = app.MapGroup("/api/empleados").WithTags("Empleados");

empleadosApi.MapGet("/", (EmpleadoService service, bool soloActivos = false) =>
{
    var empleados = service.ObtenerTodos(soloActivos);
    return Results.Ok(empleados);
});

empleadosApi.MapGet("/{id:int}", (int id, EmpleadoService service) =>
{
    var empleado = service.ObtenerPorId(id);
    return empleado is not null ? Results.Ok(empleado) : Results.NotFound(new { mensaje = $"Empleado {id} no encontrado." });
});

empleadosApi.MapPost("/", (Empleado nuevoEmpleado, EmpleadoService service) =>
{
    try
    {
        var creado = service.Crear(nuevoEmpleado);
        return Results.Created($"/api/empleados/{creado.Id}", creado);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
});

empleadosApi.MapPut("/{id:int}", (int id, Empleado empleadoActualizado, EmpleadoService service) =>
{
    try
    {
        empleadoActualizado.Id = id; // Se fuerza el Id de la ruta, por seguridad
        var actualizado = service.Actualizar(empleadoActualizado);
        return Results.Ok(actualizado);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
});

empleadosApi.MapPatch("/{id:int}/estado", (int id, CambiarEstadoRequest body, EmpleadoService service) =>
{
    try
    {
        service.CambiarEstado(id, body.NuevoEstado);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
});

empleadosApi.MapDelete("/{id:int}", (int id, EmpleadoService service) =>
{
    try
    {
        service.Eliminar(id);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
});

// =====================================================================
// 8. ENDPOINTS DE LA API — NÓMINA
// =====================================================================
var nominaApi = app.MapGroup("/api/nomina").WithTags("Nomina");

nominaApi.MapPost("/calcular", (CalcularNominaRequest req, NominaService service) =>
{
    try
    {
        var resultado = service.CalcularNomina(
            empleadoId: req.EmpleadoId,
            periodoInicio: req.PeriodoInicio,
            periodoFin: req.PeriodoFin,
            diasLiquidados: req.DiasLiquidados,
            horasExtrasDiurnas: req.HorasExtrasDiurnas,
            horasExtrasNocturnas: req.HorasExtrasNocturnas,
            horasRecargoNocturno: req.HorasRecargoNocturno,
            horasDominicalFestivo: req.HorasDominicalFestivo,
            otrasDeducciones: req.OtrasDeducciones,
            otrosDevengados: req.OtrosDevengados);

        return Results.Ok(resultado);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { mensaje = ex.Message });
    }
});

nominaApi.MapGet("/{id:int}", (int id, NominaService service) =>
{
    var nomina = service.ObtenerPorId(id);
    return nomina is not null ? Results.Ok(nomina) : Results.NotFound(new { mensaje = $"Nómina {id} no encontrada." });
});

nominaApi.MapGet("/empleado/{empleadoId:int}", (int empleadoId, NominaService service) =>
{
    var historial = service.ObtenerHistorial(empleadoId);
    return Results.Ok(historial);
});

// =====================================================================
// 9. ENDPOINT DE LA API — EXPORTACIÓN A EXCEL
// Genera el .xlsx en una carpeta temporal y lo devuelve como descarga.
// =====================================================================
app.MapGet("/api/nomina/{id:int}/excel", (int id, NominaService nominaService, ExcelService excelService) =>
{
    var nomina = nominaService.ObtenerPorId(id);
    if (nomina is null)
        return Results.NotFound(new { mensaje = $"Nómina {id} no encontrada." });

    string nombreArchivo = $"Nomina_{nomina.Empleado.Documento}_{nomina.PeriodoFin:yyyyMMdd}.xlsx";
    string rutaTemporal = Path.Combine(Path.GetTempPath(), "NominaExports", nombreArchivo);

    excelService.ExportarNomina(nomina, rutaTemporal);

    return Results.File(
        File.ReadAllBytes(rutaTemporal),
        contentType: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        fileDownloadName: nombreArchivo);
}).WithTags("Nomina");

// =====================================================================
// 10. ABRIR EL NAVEGADOR AUTOMÁTICAMENTE (experiencia de "app de escritorio")
// Como no usamos WebView2, esto simula el comportamiento de una app
// nativa: al ejecutar la app, se abre sola en el navegador predeterminado.
// =====================================================================
if (app.Environment.IsDevelopment() || Environment.GetEnvironmentVariable("NOMINA_AUTO_OPEN") != "0")
{
    var lifetime = app.Lifetime;
    lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            string url = app.Urls.FirstOrDefault() ?? "http://localhost:5175";
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Si falla (ej. en un servidor sin entorno gráfico), simplemente no abre el navegador.
        }
    });
}

app.Run();

// =====================================================================
// DTOs auxiliares para los endpoints (records: inmutables y livianos).
// Se definen aquí por simplicidad; si el proyecto crece, muévelos a
// Backend/Models/Dtos/.
// =====================================================================
record CalcularNominaRequest(
    int EmpleadoId,
    DateTime PeriodoInicio,
    DateTime PeriodoFin,
    int DiasLiquidados,
    decimal HorasExtrasDiurnas = 0,
    decimal HorasExtrasNocturnas = 0,
    decimal HorasRecargoNocturno = 0,
    decimal HorasDominicalFestivo = 0,
    decimal OtrasDeducciones = 0,
    decimal OtrosDevengados = 0);

record CambiarEstadoRequest(EstadoEmpleado NuevoEstado);