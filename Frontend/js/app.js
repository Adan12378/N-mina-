/* =========================================================================
   app.js — Orquestador principal del Frontend
   -------------------------------------------------------------------------
   Responsabilidades:
   1. Definir utilidades compartidas (fetch a la API, formato de moneda/fecha,
      notificaciones toast) que usan empleados.js, nomina.js y excel.js.
   2. Controlar la navegación entre vistas (Dashboard, Empleados, Nómina,
      Historial) simulando una SPA sin frameworks.
   3. Cargar y refrescar las estadísticas del Dashboard.
   4. Inicializar los demás módulos cuando el DOM está listo.
   ========================================================================= */

// =========================================================================
// 1. CONFIGURACIÓN GLOBAL
// =========================================================================

/**
 * URL base de la API. Como el Frontend se sirve desde el mismo servidor
 * de ASP.NET Core (ver Program.cs -> UseStaticFiles), las rutas son
 * relativas: no hace falta indicar host ni puerto.
 */
const API_BASE = "/api";

// =========================================================================
// 2. HELPER: LLAMADAS A LA API (fetch envuelto con manejo de errores)
// =========================================================================

/**
 * Envuelve fetch() para centralizar manejo de errores, parseo de JSON
 * y encabezados comunes. Todos los módulos (empleados.js, nomina.js,
 * excel.js) deben usar esta función en vez de fetch() directo.
 *
 * @param {string} endpoint - Ruta relativa a la API (ej: "/empleados").
 * @param {object} opciones - Opciones estándar de fetch (method, body, etc.)
 * @returns {Promise<any>} - El cuerpo de la respuesta ya parseado (JSON),
 *                            o null si la respuesta no tiene contenido (204).
 */
async function apiFetch(endpoint, opciones = {}) {
    const config = {
        headers: { "Content-Type": "application/json" },
        ...opciones
    };

    // Si el body es un objeto, lo convertimos a JSON automáticamente
    if (config.body && typeof config.body !== "string") {
        config.body = JSON.stringify(config.body);
    }

    let respuesta;
    try {
        respuesta = await fetch(`${API_BASE}${endpoint}`, config);
    } catch (errorRed) {
        // Error de red: el servidor no respondió en absoluto
        mostrarToast("No se pudo conectar con el servidor. Verifica que la app esté corriendo.", "error");
        throw errorRed;
    }

    // Respuesta sin contenido (ej. DELETE exitoso -> 204 No Content)
    if (respuesta.status === 204) {
        return null;
    }

    let cuerpo = null;
    try {
        cuerpo = await respuesta.json();
    } catch {
        // La respuesta no traía JSON (puede pasar en algunos errores 500 crudos)
        cuerpo = null;
    }

    if (!respuesta.ok) {
        const mensajeError = cuerpo?.mensaje || `Error ${respuesta.status} al procesar la solicitud.`;
        mostrarToast(mensajeError, "error");
        throw new Error(mensajeError);
    }

    return cuerpo;
}

// =========================================================================
// 3. HELPERS DE FORMATO (moneda y fecha, estilo Colombia)
// =========================================================================

/**
 * Formatea un número como pesos colombianos: $1.750.905 (sin decimales,
 * que es como se maneja el dinero en los comprobantes de nómina reales).
 */
function formatearMoneda(valor) {
    const numero = Number(valor) || 0;
    return numero.toLocaleString("es-CO", {
        style: "currency",
        currency: "COP",
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
    });
}

/**
 * Formatea una fecha ISO (la que envía el Backend) a formato legible
 * colombiano: dd/mm/aaaa.
 */
function formatearFecha(fechaIso) {
    if (!fechaIso) return "-";
    const fecha = new Date(fechaIso);
    return fecha.toLocaleDateString("es-CO", { day: "2-digit", month: "2-digit", year: "numeric" });
}

/**
 * Convierte un valor de enum de Estado (ej. "Activo") en la clase CSS
 * de badge correspondiente (ver components.css).
 */
function claseBadgeEstado(estado) {
    switch (estado) {
        case "Activo":
            return "badge-activo";
        case "Inactivo":
        case "Retirado":
            return "badge-inactivo";
        default:
            return "badge-neutro";
    }
}

// =========================================================================
// 4. NOTIFICACIONES TOAST
// =========================================================================

/**
 * Muestra una notificación flotante en la esquina inferior derecha.
 * @param {string} mensaje
 * @param {"success"|"error"|"warning"|"info"} tipo
 */
function mostrarToast(mensaje, tipo = "info") {
    const contenedor = document.getElementById("toast-container");
    if (!contenedor) return;

    const toast = document.createElement("div");
    toast.className = `toast toast-${tipo}`;
    toast.textContent = mensaje;

    contenedor.appendChild(toast);

    // Auto-eliminar después de 4 segundos
    setTimeout(() => {
        toast.style.opacity = "0";
        toast.style.transition = "opacity 0.3s ease";
        setTimeout(() => toast.remove(), 300);
    }, 4000);
}

// =========================================================================
// 5. NAVEGACIÓN ENTRE VISTAS (Dashboard / Empleados / Nómina / Historial)
// =========================================================================

const TITULOS_VISTA = {
    dashboard: "Dashboard",
    empleados: "Gestión de Empleados",
    nomina: "Liquidar Nómina",
    historial: "Historial de Nóminas"
};

/**
 * Cambia la vista activa: oculta todas las secciones .view y muestra
 * solo la correspondiente al botón del sidebar que se presionó.
 * También actualiza el título de la barra superior y resalta el
 * botón activo en el sidebar.
 */
function navegarAVista(nombreVista) {
    // Ocultar todas las vistas
    document.querySelectorAll(".view").forEach(vista => vista.classList.remove("active"));
    document.querySelectorAll(".nav-item").forEach(boton => boton.classList.remove("active"));

    // Mostrar la vista solicitada
    const vistaDestino = document.getElementById(`view-${nombreVista}`);
    const botonDestino = document.querySelector(`.nav-item[data-view="${nombreVista}"]`);

    if (vistaDestino) vistaDestino.classList.add("active");
    if (botonDestino) botonDestino.classList.add("active");

    // Actualizar título de la topbar
    const titulo = document.getElementById("topbar-title");
    if (titulo) titulo.textContent = TITULOS_VISTA[nombreVista] || "";

    // Refrescar datos según la vista a la que se entra
    if (nombreVista === "dashboard") {
        cargarDashboard();
    } else if (nombreVista === "empleados" && window.EmpleadosModule) {
        window.EmpleadosModule.cargarTabla();
    } else if (nombreVista === "nomina" && window.NominaModule) {
        window.NominaModule.cargarSelectEmpleados();
    } else if (nombreVista === "historial" && window.NominaModule) {
        window.NominaModule.cargarHistorial();
    }
}

/**
 * Conecta los botones del sidebar con navegarAVista().
 */
function inicializarNavegacion() {
    document.querySelectorAll(".nav-item").forEach(boton => {
        boton.addEventListener("click", () => {
            const vista = boton.getAttribute("data-view");
            navegarAVista(vista);
        });
    });
}

// =========================================================================
// 6. FECHA EN LA BARRA SUPERIOR
// =========================================================================

function mostrarFechaActual() {
    const elementoFecha = document.getElementById("topbar-date");
    if (!elementoFecha) return;

    const hoy = new Date();
    elementoFecha.textContent = hoy.toLocaleDateString("es-CO", {
        weekday: "long",
        day: "numeric",
        month: "long",
        year: "numeric"
    });
}

// =========================================================================
// 7. DASHBOARD: estadísticas y tabla de empleados recientes
// =========================================================================

/**
 * Carga las estadísticas principales del Dashboard consultando la API
 * de empleados. (El total de nómina del mes se deja en 0 por ahora,
 * ya que requeriría un endpoint de agregación específico en el Backend
 * — se puede añadir más adelante si lo necesitas).
 */
async function cargarDashboard() {
    try {
        const empleados = await apiFetch("/empleados");

        const activos = empleados.filter(e => e.estado === "Activo" || e.Estado === "Activo");

        document.getElementById("stat-empleados-activos").textContent = activos.length;

        // Tabla de empleados recientes: los últimos 5 por Id (asumiendo Id autoincremental)
        const recientes = [...empleados]
            .sort((a, b) => (b.id ?? b.Id) - (a.id ?? a.Id))
            .slice(0, 5);

        renderizarTablaRecientes(recientes);
    } catch {
        // El error ya se notificó vía toast dentro de apiFetch
    }
}

function renderizarTablaRecientes(empleados) {
    const tbody = document.querySelector("#tabla-empleados-recientes tbody");
    if (!tbody) return;

    if (empleados.length === 0) {
        tbody.innerHTML = `<tr><td colspan="5" class="data-table-empty">Aún no hay empleados registrados.</td></tr>`;
        return;
    }

    tbody.innerHTML = empleados.map(emp => {
        const nombreCompleto = emp.nombreCompleto || emp.NombreCompleto || `${emp.nombre ?? emp.Nombre} ${emp.apellido ?? emp.Apellido}`;
        const documento = emp.documento ?? emp.Documento;
        const cargo = emp.cargo ?? emp.Cargo ?? "-";
        const salario = emp.salarioBasico ?? emp.SalarioBasico;
        const estado = emp.estado ?? emp.Estado;

        return `
            <tr>
                <td>${documento}</td>
                <td>${nombreCompleto}</td>
                <td>${cargo}</td>
                <td class="cell-numeric">${formatearMoneda(salario)}</td>
                <td><span class="badge ${claseBadgeEstado(estado)}">${estado}</span></td>
            </tr>
        `;
    }).join("");
}

// =========================================================================
// 8. INICIALIZACIÓN GENERAL
// =========================================================================

document.addEventListener("DOMContentLoaded", () => {
    inicializarNavegacion();
    mostrarFechaActual();
    cargarDashboard();

    // Inicializar módulos si están disponibles (definidos en empleados.js y nomina.js)
    if (window.EmpleadosModule?.init) window.EmpleadosModule.init();
    if (window.NominaModule?.init) window.NominaModule.init();

    // Vista inicial: Dashboard
    navegarAVista("dashboard");
});