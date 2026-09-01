/* =========================================================================
   nomina.js — Módulo de Liquidación de Nómina e Historial
   -------------------------------------------------------------------------
   Responsabilidades:
   1. Poblar el <select> de empleados en el formulario de liquidación
      (reutilizando el cache de EmpleadosModule cuando esté disponible).
   2. Enviar el cálculo de nómina a la API y renderizar el comprobante
      desglosado (Devengados / Deducciones / Neto Pagado).
   3. Cargar y renderizar el historial de nóminas, con filtro por empleado.
   4. Exponer la última nómina calculada para que excel.js pueda
      descargarla sin tener que volver a consultarla.

   Se expone como window.NominaModule.
   ========================================================================= */

const NominaModule = (() => {

    // Guarda el Id de la última nómina calculada/seleccionada, para que
    // excel.js sepa cuál descargar cuando se pulse "Descargar Excel".
    let ultimaNominaId = null;

    // =====================================================================
    // 1. POBLAR EL <select> DE EMPLEADOS (formulario de liquidación)
    // =====================================================================

    async function cargarSelectEmpleados() {
        const select = document.getElementById("nomina-empleado");
        const selectFiltroHistorial = document.getElementById("historial-filtro-empleado");
        if (!select) return;

        // Reutiliza el cache de EmpleadosModule si ya está cargado;
        // si no, pide la lista directamente a la API.
        let empleados = window.EmpleadosModule?.obtenerCache?.() ?? [];
        if (!empleados || empleados.length === 0) {
            try {
                empleados = await apiFetch("/empleados?soloActivos=true");
            } catch {
                return; // El error ya se notificó vía toast dentro de apiFetch
            }
        }

        // Solo empleados activos pueden liquidarse
        const activos = empleados.filter(e => (e.estado ?? e.Estado) === "Activo");

        const opciones = activos.map(emp => {
            const id = emp.id ?? emp.Id;
            const nombre = emp.nombreCompleto ?? emp.NombreCompleto ?? `${emp.nombre ?? emp.Nombre} ${emp.apellido ?? emp.Apellido}`;
            const documento = emp.documento ?? emp.Documento;
            return `<option value="${id}">${nombre} — C.C. ${documento}</option>`;
        }).join("");

        select.innerHTML = `<option value="">Seleccione un empleado...</option>${opciones}`;

        if (selectFiltroHistorial) {
            selectFiltroHistorial.innerHTML = `<option value="">Todos los empleados</option>${opciones}`;
        }
    }

    // =====================================================================
    // 2. CALCULAR NÓMINA (envío del formulario)
    // =====================================================================

    async function manejarSubmitFormulario(evento) {
        evento.preventDefault();

        const empleadoId = document.getElementById("nomina-empleado").value;
        if (!empleadoId) {
            mostrarToast("Selecciona un empleado antes de calcular.", "warning");
            return;
        }

        const payload = {
            EmpleadoId: parseInt(empleadoId, 10),
            PeriodoInicio: document.getElementById("nomina-periodo-inicio").value,
            PeriodoFin: document.getElementById("nomina-periodo-fin").value,
            DiasLiquidados: parseInt(document.getElementById("nomina-dias").value, 10) || 0,
            HorasExtrasDiurnas: parseFloat(document.getElementById("nomina-extra-diurna").value) || 0,
            HorasExtrasNocturnas: parseFloat(document.getElementById("nomina-extra-nocturna").value) || 0,
            HorasRecargoNocturno: parseFloat(document.getElementById("nomina-recargo-nocturno").value) || 0,
            HorasDominicalFestivo: parseFloat(document.getElementById("nomina-dominical").value) || 0,
            OtrosDevengados: parseFloat(document.getElementById("nomina-otros-devengados").value) || 0,
            OtrasDeducciones: parseFloat(document.getElementById("nomina-otras-deducciones").value) || 0
        };

        if (!payload.PeriodoInicio || !payload.PeriodoFin) {
            mostrarToast("Debes indicar el periodo de inicio y fin.", "warning");
            return;
        }

        try {
            const resultado = await apiFetch("/nomina/calcular", { method: "POST", body: payload });
            ultimaNominaId = resultado.id ?? resultado.Id;
            renderizarComprobante(resultado);
            mostrarToast("Nómina calculada correctamente.", "success");

            // Refrescar el historial en segundo plano, por si el usuario navega ahí después
            cargarHistorial();
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    // =====================================================================
    // 3. RENDERIZAR EL COMPROBANTE (Devengados / Deducciones / Neto)
    // =====================================================================

    function renderizarComprobante(nomina) {
        const panel = document.getElementById("panel-resultado-nomina");
        if (panel) panel.style.display = "block";

        const devengados = [
            ["Salario Básico", nomina.salarioBasicoDevengado ?? nomina.SalarioBasicoDevengado],
            [`Horas Extra Diurnas (${nomina.cantidadHorasExtrasDiurnas ?? nomina.CantidadHorasExtrasDiurnas}h)`, nomina.horasExtrasDiurnas ?? nomina.HorasExtrasDiurnas],
            [`Horas Extra Nocturnas (${nomina.cantidadHorasExtrasNocturnas ?? nomina.CantidadHorasExtrasNocturnas}h)`, nomina.horasExtrasNocturnas ?? nomina.HorasExtrasNocturnas],
            [`Recargo Nocturno (${nomina.cantidadRecargoNocturno ?? nomina.CantidadRecargoNocturno}h)`, nomina.recargoNocturno ?? nomina.RecargoNocturno],
            [`Recargo Dominical/Festivo (${nomina.cantidadDominicalFestivo ?? nomina.CantidadDominicalFestivo}h)`, nomina.recargoDominicalFestivo ?? nomina.RecargoDominicalFestivo],
            ["Auxilio de Transporte", nomina.auxilioTransporte ?? nomina.AuxilioTransporte],
            ["Otros Devengados", nomina.otrosDevengados ?? nomina.OtrosDevengados]
        ];

        const deducciones = [
            ["Salud (4%)", nomina.deduccionSalud ?? nomina.DeduccionSalud],
            ["Pensión (4%)", nomina.deduccionPension ?? nomina.DeduccionPension],
            ["Fondo de Solidaridad Pensional (1%)", nomina.fondoSolidaridad ?? nomina.FondoSolidaridad],
            ["Retención en la Fuente", nomina.retefuente ?? nomina.Retefuente],
            ["Otras Deducciones", nomina.otrasDeducciones ?? nomina.OtrasDeducciones]
        ];

        document.getElementById("tabla-devengados").innerHTML = renderizarFilasConcepto(devengados, "TOTAL DEVENGADO", nomina.totalDevengado ?? nomina.TotalDevengado);
        document.getElementById("tabla-deducciones").innerHTML = renderizarFilasConcepto(deducciones, "TOTAL DEDUCCIONES", nomina.totalDeducciones ?? nomina.TotalDeducciones);

        document.getElementById("valor-neto-pagado").textContent = formatearMoneda(nomina.netoPagado ?? nomina.NetoPagado);

        // Desplazar la vista hasta el comprobante recién generado
        panel?.scrollIntoView({ behavior: "smooth", block: "start" });
    }

    function renderizarFilasConcepto(items, etiquetaTotal, valorTotal) {
        const filas = items.map(([nombre, valor]) => `
            <tr>
                <td>${nombre}</td>
                <td class="cell-numeric">${formatearMoneda(valor)}</td>
            </tr>
        `).join("");

        return `
            <tbody>
                ${filas}
                <tr style="font-weight:700; border-top: 2px solid var(--color-border);">
                    <td>${etiquetaTotal}</td>
                    <td class="cell-numeric">${formatearMoneda(valorTotal)}</td>
                </tr>
            </tbody>
        `;
    }

    // =====================================================================
    // 4. HISTORIAL DE NÓMINAS
    // =====================================================================

    async function cargarHistorial() {
        const tbody = document.querySelector("#tabla-historial tbody");
        if (!tbody) return;

        const empleadoIdFiltro = document.getElementById("historial-filtro-empleado")?.value;

        try {
            let historial = [];

            if (empleadoIdFiltro) {
                historial = await apiFetch(`/nomina/empleado/${empleadoIdFiltro}`);
            } else {
                // No hay endpoint de "todas las nóminas" en el Backend todavía,
                // así que agregamos el historial de todos los empleados activos.
                const empleados = window.EmpleadosModule?.obtenerCache?.() ?? await apiFetch("/empleados");
                const historiales = await Promise.all(
                    empleados.map(emp => apiFetch(`/nomina/empleado/${emp.id ?? emp.Id}`).catch(() => []))
                );
                historial = historiales.flat();
            }

            renderizarHistorial(historial);
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    function renderizarHistorial(historial) {
        const tbody = document.querySelector("#tabla-historial tbody");
        if (!tbody) return;

        if (!historial || historial.length === 0) {
            tbody.innerHTML = `<tr><td colspan="6" class="data-table-empty">Aún no se han liquidado nóminas.</td></tr>`;
            return;
        }

        // Ordenar por fecha de fin de periodo, más reciente primero
        const ordenado = [...historial].sort((a, b) => {
            const fechaA = new Date(a.periodoFin ?? a.PeriodoFin);
            const fechaB = new Date(b.periodoFin ?? b.PeriodoFin);
            return fechaB - fechaA;
        });

        tbody.innerHTML = ordenado.map(nom => {
            const id = nom.id ?? nom.Id;
            const inicio = formatearFecha(nom.periodoInicio ?? nom.PeriodoInicio);
            const fin = formatearFecha(nom.periodoFin ?? nom.PeriodoFin);
            const empleado = nom.empleado ?? nom.Empleado;
            const nombreEmpleado = empleado ? (empleado.nombreCompleto ?? empleado.NombreCompleto) : "-";
            const totalDevengado = nom.totalDevengado ?? nom.TotalDevengado;
            const totalDeducciones = nom.totalDeducciones ?? nom.TotalDeducciones;
            const netoPagado = nom.netoPagado ?? nom.NetoPagado;

            return `
                <tr>
                    <td>${inicio} - ${fin}</td>
                    <td>${nombreEmpleado}</td>
                    <td class="cell-numeric">${formatearMoneda(totalDevengado)}</td>
                    <td class="cell-numeric">${formatearMoneda(totalDeducciones)}</td>
                    <td class="cell-numeric">${formatearMoneda(netoPagado)}</td>
                    <td class="cell-actions">
                        <button class="btn btn-icon btn-ver-detalle" title="Ver detalle" data-id="${id}">👁️</button>
                        <button class="btn btn-icon btn-descargar-historial" title="Descargar Excel" data-id="${id}">⬇️</button>
                    </td>
                </tr>
            `;
        }).join("");

        tbody.querySelectorAll(".btn-ver-detalle").forEach(btn => {
            btn.addEventListener("click", () => verDetalleDesdeHistorial(btn.dataset.id));
        });

        tbody.querySelectorAll(".btn-descargar-historial").forEach(btn => {
            btn.addEventListener("click", () => {
                ultimaNominaId = btn.dataset.id;
                window.ExcelModule?.descargarNomina?.(btn.dataset.id);
            });
        });
    }

    /**
     * Al hacer clic en "Ver detalle" desde el historial, trae la nómina
     * completa y la muestra en la vista de Liquidar Nómina, reutilizando
     * el mismo panel de comprobante.
     */
    async function verDetalleDesdeHistorial(id) {
        try {
            const nomina = await apiFetch(`/nomina/${id}`);
            ultimaNominaId = nomina.id ?? nomina.Id;

            // Cambiar a la vista de Liquidar Nómina para mostrar el comprobante
            document.querySelector('.nav-item[data-view="nomina"]')?.click();
            renderizarComprobante(nomina);
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    // =====================================================================
    // 5. INICIALIZACIÓN
    // =====================================================================

    function init() {
        document.getElementById("form-nomina")?.addEventListener("submit", manejarSubmitFormulario);

        document.getElementById("historial-filtro-empleado")?.addEventListener("change", cargarHistorial);

        document.getElementById("btn-descargar-excel")?.addEventListener("click", () => {
            if (!ultimaNominaId) {
                mostrarToast("Primero calcula una nómina para poder descargarla.", "warning");
                return;
            }
            window.ExcelModule?.descargarNomina?.(ultimaNominaId);
        });

        // Sugerir el mes actual como periodo por defecto en el formulario
        establecerPeriodoPorDefecto();
    }

    function establecerPeriodoPorDefecto() {
        const hoy = new Date();
        const primerDia = new Date(hoy.getFullYear(), hoy.getMonth(), 1);
        const ultimoDia = new Date(hoy.getFullYear(), hoy.getMonth() + 1, 0);

        const inputInicio = document.getElementById("nomina-periodo-inicio");
        const inputFin = document.getElementById("nomina-periodo-fin");

        if (inputInicio) inputInicio.value = primerDia.toISOString().split("T")[0];
        if (inputFin) inputFin.value = ultimoDia.toISOString().split("T")[0];
    }

    // =====================================================================
    // API PÚBLICA DEL MÓDULO
    // =====================================================================
    return {
        init,
        cargarSelectEmpleados,
        cargarHistorial,
        obtenerUltimaNominaId: () => ultimaNominaId
    };

})();

// Exponer el módulo globalmente para que app.js y excel.js lo usen
window.NominaModule = NominaModule;