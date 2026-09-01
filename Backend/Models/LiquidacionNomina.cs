using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nomina.Backend.Models
{
/// Desglosa Devengados y Deducciones tal como exige un comprobante
    /// de nómina legal en Colombia, y termina en el Neto Pagado + Firma.
    /// </summary>
    public class LiquidacionNomina

    {
        [Key]
        public int Id { get; set; }

        // ---------- Relación con Empleado ----------
        [ForeignKey(nameof(Empleado))]
        public int EmpleadoId { get; set; }
        public Empleado Empleado { get; set; } = null!;

        // ---------- Periodo liquidado ----------
        public DateTime PeriodoInicio { get; set; }
        public DateTime PeriodoFin { get; set; }

        /// <summary>Días efectivamente trabajados/liquidados en el periodo (ej. 30, 15).</summary>
        public int DiasLiquidados { get; set; }

        /// <summary>Fecha en la que se generó/calculó esta nómina (auditoría).</summary>
        public DateTime FechaGeneracion { get; set; } = DateTime.Now;

        // ================= DEVENGADOS =================

        /// <summary>Salario básico proporcional a los días liquidados.</summary>
        public decimal SalarioBasicoDevengado { get; set; }

        /// <summary>Cantidad de horas extra diurnas trabajadas en el periodo.</summary>
        public decimal CantidadHorasExtrasDiurnas { get; set; }

        /// <summary>Valor pagado por horas extra diurnas (25% recargo).</summary>
        public decimal HorasExtrasDiurnas { get; set; }

        /// <summary>Cantidad de horas extra nocturnas trabajadas en el periodo.</summary>
        public decimal CantidadHorasExtrasNocturnas { get; set; }

        /// <summary>Valor pagado por horas extra nocturnas (75% recargo).</summary>
        public decimal HorasExtrasNocturnas { get; set; }

        /// <summary>Cantidad de horas con recargo nocturno (no extra, ordinaria nocturna).</summary>
        public decimal CantidadRecargoNocturno { get; set; }

        /// <summary>Valor del recargo nocturno (35%), desde las 7:00 p.m.</summary>
        public decimal RecargoNocturno { get; set; }

        /// <summary>Cantidad de horas dominicales/festivas trabajadas.</summary>
        public decimal CantidadDominicalFestivo { get; set; }

        /// <summary>Valor del recargo dominical/festivo (90%).</summary>
        public decimal RecargoDominicalFestivo { get; set; }

        /// <summary>
        /// Auxilio de transporte devengado en el periodo (proporcional a días
        /// liquidados). Solo se liquida si el empleado gana <= 2 SMMLV.
        /// </summary>
        public decimal AuxilioTransporte { get; set; }

        /// <summary>Otros devengados no clasificados (bonificaciones, comisiones, etc.).</summary>
        public decimal OtrosDevengados { get; set; }

        /// <summary>Suma total de todos los devengados anteriores.</summary>
        public decimal TotalDevengado { get; set; }

        // ================= DEDUCCIONES =================

        /// <summary>Aporte a salud del empleado: 4% sobre IBC (sin incluir auxilio transporte).</summary>
        public decimal DeduccionSalud { get; set; }

        /// <summary>Aporte a pensión del empleado: 4% sobre IBC (sin incluir auxilio transporte).</summary>
        public decimal DeduccionPension { get; set; }

        /// <summary>Fondo de Solidaridad Pensional: 1% adicional si IBC >= 4 SMMLV.</summary>
        public decimal FondoSolidaridad { get; set; }

        /// <summary>Retención en la fuente por salarios (si aplica según tabla DIAN).</summary>
        public decimal Retefuente { get; set; }

        /// <summary>Otras deducciones (préstamos, embargos, libranzas, etc.).</summary>
        public decimal OtrasDeducciones { get; set; }

        /// <summary>Suma total de todas las deducciones anteriores.</summary>
        public decimal TotalDeducciones { get; set; }

        // ================= RESULTADO FINAL =================

        /// <summary>Neto Pagado = TotalDevengado - TotalDeducciones. Valor final a pagar.</summary>
        public decimal NetoPagado { get; set; }

        /// <summary>
        /// Ruta o cadena base64 de la firma digital del empleado (si el sistema
        /// captura firma en pantalla). Puede quedar null si se firma en papel físico.
        /// </summary>
        public string? FirmaEmpleado { get; set; }

        /// <summary>Fecha en la que el empleado firmó el comprobante (auditoría legal).</summary>
        public DateTime? FechaFirma { get; set; }

        /// <summary>
        /// Detalle de conceptos individuales (horas extra/recargos discriminados)
        /// que soportan los totales anteriores. Útil para el Excel exportado.
        /// </summary>
        public List<ConceptoNomina> Conceptos { get; set; } = new();
    }
}