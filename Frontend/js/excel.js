/* =========================================================================
   excel.js — Módulo de exportación a Excel
   -------------------------------------------------------------------------
   Responsabilidad única: solicitar al Backend el comprobante de nómina
   en formato .xlsx (endpoint GET /api/nomina/{id}/excel) y forzar su
   descarga en el navegador con el nombre de archivo correcto.

   No usa apiFetch() de app.js porque esa función espera JSON; aquí la
   respuesta es un archivo binario, así que se maneja como Blob aparte.

   Se expone como window.ExcelModule.
   ========================================================================= */

const ExcelModule = (() => {

    /**
     * Descarga el comprobante de nómina en Excel para una liquidación
     * específica.
     * @param {number|string} nominaId - Id de la LiquidacionNomina a exportar.
     */
    async function descargarNomina(nominaId) {
        if (!nominaId) {
            mostrarToast("No hay una nómina seleccionada para exportar.", "warning");
            return;
        }

        mostrarToast("Generando archivo Excel...", "info");

        let respuesta;
        try {
            respuesta = await fetch(`/api/nomina/${nominaId}/excel`);
        } catch {
            mostrarToast("No se pudo conectar con el servidor para generar el Excel.", "error");
            return;
        }

        if (!respuesta.ok) {
            // Intenta leer un mensaje de error en JSON; si no hay, usa uno genérico
            let mensaje = `No se pudo generar el Excel (error ${respuesta.status}).`;
            try {
                const cuerpoError = await respuesta.json();
                mensaje = cuerpoError?.mensaje || mensaje;
            } catch {
                // La respuesta de error no traía JSON; se usa el mensaje genérico
            }
            mostrarToast(mensaje, "error");
            return;
        }

        // Extraer el nombre de archivo sugerido por el Backend
        // (Content-Disposition: attachment; filename="Nomina_123456_20260131.xlsx")
        const nombreArchivo = extraerNombreArchivo(respuesta.headers.get("Content-Disposition"))
            || `Nomina_${nominaId}.xlsx`;

        try {
            const blob = await respuesta.blob();
            forzarDescarga(blob, nombreArchivo);
            mostrarToast("Excel descargado correctamente.", "success");
        } catch {
            mostrarToast("Se generó el archivo, pero hubo un error al descargarlo.", "error");
        }
    }

    /**
     * Extrae el nombre de archivo del encabezado Content-Disposition
     * que envía Results.File() en el Backend.
     */
    function extraerNombreArchivo(headerContentDisposition) {
        if (!headerContentDisposition) return null;

        const coincidencia = headerContentDisposition.match(/filename="?([^"]+)"?/);
        return coincidencia ? coincidencia[1] : null;
    }

    /**
     * Crea un enlace temporal invisible para forzar la descarga del Blob
     * con el nombre de archivo indicado, sin necesidad de librerías externas.
     */
    function forzarDescarga(blob, nombreArchivo) {
        const url = window.URL.createObjectURL(blob);

        const enlaceTemporal = document.createElement("a");
        enlaceTemporal.href = url;
        enlaceTemporal.download = nombreArchivo;
        document.body.appendChild(enlaceTemporal);
        enlaceTemporal.click();

        // Limpieza: quitar el enlace del DOM y liberar la URL del Blob
        document.body.removeChild(enlaceTemporal);
        window.URL.revokeObjectURL(url);
    }

    // =====================================================================
    // API PÚBLICA DEL MÓDULO
    // =====================================================================
    return {
        descargarNomina
    };

})();

// Exponer el módulo globalmente para que nomina.js lo use
window.ExcelModule = ExcelModule;