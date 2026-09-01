/* =========================================================================
   empleados.js — Módulo de gestión de Empleados
   -------------------------------------------------------------------------
   Responsabilidades:
   1. Cargar y renderizar la tabla de empleados (vista "Empleados").
   2. Abrir/cerrar el modal de creación y edición.
   3. Enviar altas (POST), ediciones (PUT) y bajas (DELETE/PATCH estado)
      a la API, usando apiFetch() definido en app.js.

   Se expone como window.EmpleadosModule para que app.js pueda llamar
   a EmpleadosModule.init() y EmpleadosModule.cargarTabla() al navegar
   entre vistas.
   ========================================================================= */

const EmpleadosModule = (() => {

    // Cache en memoria de la última lista cargada (útil para poblar el
    // <select> de nomina.js sin volver a pedirla a la API).
    let cacheEmpleados = [];

    // =====================================================================
    // 1. CARGA Y RENDERIZADO DE LA TABLA
    // =====================================================================

    async function cargarTabla() {
        try {
            const empleados = await apiFetch("/empleados");
            cacheEmpleados = empleados;
            renderizarTabla(empleados);
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    function renderizarTabla(empleados) {
        const tbody = document.querySelector("#tabla-empleados tbody");
        if (!tbody) return;

        if (!empleados || empleados.length === 0) {
            tbody.innerHTML = `<tr><td colspan="8" class="data-table-empty">No hay empleados registrados. Crea el primero con "+ Nuevo Empleado".</td></tr>`;
            return;
        }

        tbody.innerHTML = empleados.map(emp => {
            const id = emp.id ?? emp.Id;
            const documento = emp.documento ?? emp.Documento;
            const nombreCompleto = emp.nombreCompleto ?? emp.NombreCompleto ?? `${emp.nombre ?? emp.Nombre} ${emp.apellido ?? emp.Apellido}`;
            const cargo = emp.cargo ?? emp.Cargo ?? "-";
            const tipoContrato = formatearTipoContrato(emp.tipoContrato ?? emp.TipoContrato);
            const salario = emp.salarioBasico ?? emp.SalarioBasico;
            const auxTransporte = emp.auxilioTransporte ?? emp.AuxilioTransporte;
            const estado = emp.estado ?? emp.Estado;

            return `
                <tr data-id="${id}">
                    <td>${documento}</td>
                    <td>${nombreCompleto}</td>
                    <td>${cargo}</td>
                    <td>${tipoContrato}</td>
                    <td class="cell-numeric">${formatearMoneda(salario)}</td>
                    <td>${auxTransporte ? "Sí" : "No"}</td>
                    <td><span class="badge ${claseBadgeEstado(estado)}">${estado}</span></td>
                    <td class="cell-actions">
                        <button class="btn btn-icon btn-editar" title="Editar" data-id="${id}">✏️</button>
                        <button class="btn btn-icon btn-eliminar" title="Retirar" data-id="${id}">🗑️</button>
                    </td>
                </tr>
            `;
        }).join("");

        // Conectar botones de acciones recién insertados
        tbody.querySelectorAll(".btn-editar").forEach(btn => {
            btn.addEventListener("click", () => abrirModalEdicion(btn.dataset.id));
        });

        tbody.querySelectorAll(".btn-eliminar").forEach(btn => {
            btn.addEventListener("click", () => confirmarRetiro(btn.dataset.id));
        });
    }

    function formatearTipoContrato(tipo) {
        const nombres = {
            TerminoIndefinido: "Término Indefinido",
            TerminoFijo: "Término Fijo",
            ObraOLabor: "Obra o Labor",
            PrestacionServicios: "Prestación de Servicios",
            Aprendizaje: "Aprendizaje"
        };
        return nombres[tipo] || tipo || "-";
    }

    // =====================================================================
    // 2. MODAL: ABRIR / CERRAR
    // =====================================================================

    function abrirModalNuevo() {
        document.getElementById("modal-empleado-titulo").textContent = "Nuevo Empleado";
        document.getElementById("form-empleado").reset();
        document.getElementById("empleado-id").value = "";
        document.getElementById("empleado-fecha-ingreso").value = new Date().toISOString().split("T")[0];
        mostrarModal(true);
    }

    function abrirModalEdicion(id) {
        const empleado = cacheEmpleados.find(e => String(e.id ?? e.Id) === String(id));
        if (!empleado) {
            mostrarToast("No se encontró el empleado seleccionado.", "error");
            return;
        }

        document.getElementById("modal-empleado-titulo").textContent = "Editar Empleado";
        document.getElementById("empleado-id").value = empleado.id ?? empleado.Id;
        document.getElementById("empleado-documento").value = empleado.documento ?? empleado.Documento;
        document.getElementById("empleado-nombre").value = empleado.nombre ?? empleado.Nombre;
        document.getElementById("empleado-apellido").value = empleado.apellido ?? empleado.Apellido;
        document.getElementById("empleado-cargo").value = empleado.cargo ?? empleado.Cargo ?? "";
        document.getElementById("empleado-salario").value = empleado.salarioBasico ?? empleado.SalarioBasico;

        const fechaIngreso = empleado.fechaIngreso ?? empleado.FechaIngreso;
        document.getElementById("empleado-fecha-ingreso").value = fechaIngreso ? fechaIngreso.split("T")[0] : "";

        document.getElementById("empleado-tipo-contrato").value = empleado.tipoContrato ?? empleado.TipoContrato;
        document.getElementById("empleado-estado").value = empleado.estado ?? empleado.Estado;

        mostrarModal(true);
    }

    function cerrarModal() {
        mostrarModal(false);
    }

    function mostrarModal(visible) {
        const modal = document.getElementById("modal-empleado");
        if (modal) modal.style.display = visible ? "flex" : "none";
    }

    // =====================================================================
    // 3. GUARDAR (CREAR o ACTUALIZAR según si hay Id)
    // =====================================================================

    async function manejarSubmitFormulario(evento) {
        evento.preventDefault();

        const id = document.getElementById("empleado-id").value;

        const payload = {
            Documento: document.getElementById("empleado-documento").value.trim(),
            Nombre: document.getElementById("empleado-nombre").value.trim(),
            Apellido: document.getElementById("empleado-apellido").value.trim(),
            Cargo: document.getElementById("empleado-cargo").value.trim(),
            SalarioBasico: parseFloat(document.getElementById("empleado-salario").value) || 0,
            FechaIngreso: document.getElementById("empleado-fecha-ingreso").value,
            TipoContrato: document.getElementById("empleado-tipo-contrato").value,
            Estado: document.getElementById("empleado-estado").value
        };

        try {
            if (id) {
                await apiFetch(`/empleados/${id}`, { method: "PUT", body: payload });
                mostrarToast("Empleado actualizado correctamente.", "success");
            } else {
                await apiFetch("/empleados", { method: "POST", body: payload });
                mostrarToast("Empleado creado correctamente.", "success");
            }

            cerrarModal();
            await cargarTabla();
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    // =====================================================================
    // 4. RETIRAR EMPLEADO (cambio de estado, no borrado físico)
    // =====================================================================

    async function confirmarRetiro(id) {
        const empleado = cacheEmpleados.find(e => String(e.id ?? e.Id) === String(id));
        const nombre = empleado ? (empleado.nombreCompleto ?? empleado.NombreCompleto) : "este empleado";

        const confirmado = window.confirm(`¿Confirmas marcar a "${nombre}" como Retirado? Su historial de nóminas se conservará.`);
        if (!confirmado) return;

        try {
            await apiFetch(`/empleados/${id}/estado`, {
                method: "PATCH",
                body: { NuevoEstado: "Retirado" }
            });
            mostrarToast("Empleado marcado como Retirado.", "success");
            await cargarTabla();
        } catch {
            // El error ya se notificó vía toast dentro de apiFetch
        }
    }

    // =====================================================================
    // 5. INICIALIZACIÓN: conectar eventos una sola vez
    // =====================================================================

    function init() {
        document.getElementById("btn-nuevo-empleado")?.addEventListener("click", abrirModalNuevo);
        document.getElementById("btn-cerrar-modal")?.addEventListener("click", cerrarModal);
        document.getElementById("btn-cancelar-empleado")?.addEventListener("click", cerrarModal);
        document.getElementById("form-empleado")?.addEventListener("submit", manejarSubmitFormulario);

        // Cerrar modal al hacer clic fuera de él (en el overlay oscuro)
        document.getElementById("modal-empleado")?.addEventListener("click", (evento) => {
            if (evento.target.id === "modal-empleado") cerrarModal();
        });

        // Cargar la tabla y el cache desde ya, para que nomina.js tenga
        // datos disponibles apenas arranque la app (aunque el usuario
        // no haya visitado la vista Empleados todavía).
        cargarTabla();
    }

    // =====================================================================
    // API PÚBLICA DEL MÓDULO
    // =====================================================================
    return {
        init,
        cargarTabla,
        obtenerCache: () => cacheEmpleados
    };

})();

// Exponer el módulo globalmente para que app.js y nomina.js lo usen
window.EmpleadosModule = EmpleadosModule;