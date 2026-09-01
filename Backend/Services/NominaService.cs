using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Nomina.Backend.Data;
using Nomina.Backend.Models;

namespace Nomina.Backend.Services
{
    /// <summary>
    /// Cerebro de todo
    /// principal del sistema que calcula la liquidacion de nomina Partede un empleador
    /// Núcleo del sistema: calcula la liquidación de nómina de un empleadocolombiana vigente en 2026.
    /// Todos los valores monetarios se calculan en decimal y se redondean al  colombiano  mas cercano ya que el COP no maneja centavos en la practica bancaria y en la nomina real esto ayuda a mantener los calculos mas exactos y evita que se acumulen errores por usar coma flotante

    /// </summary>
    public class NominaService
    {
        // ================= CONSTANTES LEGALES 2026 =================

        /// <summary>Salario Mínimo Mensual Legal Vigente 2026.</summary>
        public const decimal SMMLV_2026 = 1_750_905m;

        /// <summary>Auxilio de Transporte 2026 (valor mensual completo).</summary>
        public const decimal AUXILIO_TRANSPORTE_2026 = 249_095m;

        /// <summary>Tope de salario para tener derecho a auxilio de transporte: 2 SMMLV.</summary>
        public const decimal TOPE_AUXILIO_TRANSPORTE = SMMLV_2026 * 2;

        /// <summary>Tope de salario para aportar Fondo de Solidaridad Pensional: 4 SMMLV.</summary>
        public const decimal TOPE_FONDO_SOLIDARIDAD = SMMLV_2026 * 4;

        /// <summary>Porcentaje de aporte a salud a cargo del empleado.</summary>
        public const decimal PORCENTAJE_SALUD = 0.04m;

        /// <summary>Porcentaje de aporte a pensión a cargo del empleado.</summary>
        public const decimal PORCENTAJE_PENSION = 0.04m;

        /// <summary>Porcentaje adicional del Fondo de Solidaridad Pensional.</summary>
        public const decimal PORCENTAJE_FONDO_SOLIDARIDAD = 0.01m;

        /// <summary>Días base del mes para efectos de liquidación laboral en Colombia (norma laboral, no calendario real).</summary>
        public const int DIAS_MES_LABORAL = 30;

        /// <summary>Horas de la jornada laboral base mensual.</summary>
        public const decimal HORAS_JORNADA_MENSUAL = 220m;

        private readonly EmpleadoService _empleadoService = new();

        // ================= MÉTODO PRINCIPAL =================

        /// <summary>
        /// Calcula la nómina completa de un empleado para un periodo específico.
        /// </summary>
        /// <param name="empleadoId">Id del empleado a liquidar.</param>
        /// <param name="periodoInicio">Fecha de inicio del periodo.</param>
        /// <param name="periodoFin">Fecha de fin del periodo.</param>
        /// <param name="diasLiquidados">Días a liquidar (ej. 15 para quincena, 30 para mes completo).</param>
        /// <param name="horasExtrasDiurnas">Cantidad de horas extra diurnas trabajadas.</param>
        /// <param name="horasExtrasNocturnas">Cantidad de horas extra nocturnas trabajadas.</param>
        /// <param name="horasRecargoNocturno">Cantidad de horas ordinarias trabajadas en horario nocturno (no extra).</param>
        /// <param name="horasDominicalFestivo">Cantidad de horas ordinarias trabajadas en dominical/festivo.</param>
        /// <param name="otrasDeducciones">Deducciones adicionales (préstamos, embargos, etc.).</param>
        /// <param name="otrosDevengados">Devengados adicionales (bonificaciones, comisiones, etc.).</param>
        public LiquidacionNomina CalcularNomina(
            int empleadoId,
            DateTime periodoInicio,
            DateTime periodoFin,
            int diasLiquidados,
            decimal horasExtrasDiurnas = 0,
            decimal horasExtrasNocturnas = 0,
            decimal horasRecargoNocturno = 0,
            decimal horasDominicalFestivo = 0,
            decimal otrasDeducciones = 0,
            decimal otrosDevengados = 0)
        {
            var empleado = _empleadoService.ObtenerPorId(empleadoId)
                ?? throw new InvalidOperationException($"No se encontró el empleado con Id {empleadoId}.");

            if (diasLiquidados <= 0 || diasLiquidados > DIAS_MES_LABORAL)
                throw new ArgumentException("Los días liquidados deben estar entre 1 y 30.");

            var liquidacion = new LiquidacionNomina
            {
                EmpleadoId = empleado.Id,
                PeriodoInicio = periodoInicio,
                PeriodoFin = periodoFin,
                DiasLiquidados = diasLiquidados
            };

            // ---------- 1. Valor día y valor hora ordinaria ----------
            decimal valorDia = empleado.SalarioBasico / DIAS_MES_LABORAL;
            decimal valorHoraOrdinaria = empleado.SalarioBasico / HORAS_JORNADA_MENSUAL;

            // ---------- 2. Salario básico devengado (proporcional a días liquidados) ----------
            liquidacion.SalarioBasicoDevengado = RedondearPeso(valorDia * diasLiquidados);

            // ---------- 3. Horas extra diurnas: 25% de recargo ----------
            liquidacion.CantidadHorasExtrasDiurnas = horasExtrasDiurnas;
            decimal porcentajeExtraDiurna = ConceptoNomina.ObtenerPorcentaje(TipoRecargo.ExtraDiurna);
            liquidacion.HorasExtrasDiurnas = RedondearPeso(
                valorHoraOrdinaria * (1 + porcentajeExtraDiurna) * horasExtrasDiurnas);

            // ---------- 4. Horas extra nocturnas: 75% de recargo ----------
            liquidacion.CantidadHorasExtrasNocturnas = horasExtrasNocturnas;
            decimal porcentajeExtraNocturna = ConceptoNomina.ObtenerPorcentaje(TipoRecargo.ExtraNocturna);
            liquidacion.HorasExtrasNocturnas = RedondearPeso(
                valorHoraOrdinaria * (1 + porcentajeExtraNocturna) * horasExtrasNocturnas);

            // ---------- 5. Recargo nocturno (Ley 2466): 35%, desde las 7:00 p.m. ----------
            liquidacion.CantidadRecargoNocturno = horasRecargoNocturno;
            decimal porcentajeRecargoNocturno = ConceptoNomina.ObtenerPorcentaje(TipoRecargo.RecargoNocturno);
            liquidacion.RecargoNocturno = RedondearPeso(
                valorHoraOrdinaria * porcentajeRecargoNocturno * horasRecargoNocturno);

            // ---------- 6. Recargo dominical y festivo: 90% ----------
            liquidacion.CantidadDominicalFestivo = horasDominicalFestivo;
            decimal porcentajeDominicalFestivo = ConceptoNomina.ObtenerPorcentaje(TipoRecargo.RecargoDominicalFestivo);
            liquidacion.RecargoDominicalFestivo = RedondearPeso(
                valorHoraOrdinaria * porcentajeDominicalFestivo * horasDominicalFestivo);

            // ---------- 7. Auxilio de Transporte ----------
            bool tieneDerechoAuxilio = empleado.SalarioBasico <= TOPE_AUXILIO_TRANSPORTE;
            liquidacion.AuxilioTransporte = tieneDerechoAuxilio
                ? RedondearPeso((AUXILIO_TRANSPORTE_2026 / DIAS_MES_LABORAL) * diasLiquidados)
                : 0m;

            // ---------- 8. Otros devengados ----------
            liquidacion.OtrosDevengados = RedondearPeso(otrosDevengados);

            // ---------- 9. Total Devengado ----------
            liquidacion.TotalDevengado = RedondearPeso(
                liquidacion.SalarioBasicoDevengado +
                liquidacion.HorasExtrasDiurnas +
                liquidacion.HorasExtrasNocturnas +
                liquidacion.RecargoNocturno +
                liquidacion.RecargoDominicalFestivo +
                liquidacion.AuxilioTransporte +
                liquidacion.OtrosDevengados);

            // ================= DEDUCCIONES =================

            // ---------- 10. Base (IBC) para Salud y Pensión ----------
            // No incluye Auxilio de Transporte (norma expresa).
            decimal baseIBC = liquidacion.SalarioBasicoDevengado +
                               liquidacion.HorasExtrasDiurnas +
                               liquidacion.HorasExtrasNocturnas +
                               liquidacion.RecargoNocturno +
                               liquidacion.RecargoDominicalFestivo +
                               liquidacion.OtrosDevengados;

            // ---------- 11. Salud: 4% sobre IBC ----------
            liquidacion.DeduccionSalud = RedondearPeso(baseIBC * PORCENTAJE_SALUD);

            // ---------- 12. Pensión: 4% sobre IBC ----------
            liquidacion.DeduccionPension = RedondearPeso(baseIBC * PORCENTAJE_PENSION);

            // ---------- 13. Fondo de Solidaridad Pensional: 1% si salario >= 4 SMMLV ----------
            bool aportaFondoSolidaridad = empleado.SalarioBasico >= TOPE_FONDO_SOLIDARIDAD;
            liquidacion.FondoSolidaridad = aportaFondoSolidaridad
                ? RedondearPeso(baseIBC * PORCENTAJE_FONDO_SOLIDARIDAD)
                : 0m;

            // ---------- 14. Retención en la Fuente ----------
            // Simplificado a 0 por defecto (ver nota en la respuesta anterior sobre tabla DIAN 2026).
            liquidacion.Retefuente = 0m;

            // ---------- 15. Otras deducciones ----------
            liquidacion.OtrasDeducciones = RedondearPeso(otrasDeducciones);

            // ---------- 16. Total Deducciones ----------
            liquidacion.TotalDeducciones = RedondearPeso(
                liquidacion.DeduccionSalud +
                liquidacion.DeduccionPension +
                liquidacion.FondoSolidaridad +
                liquidacion.Retefuente +
                liquidacion.OtrasDeducciones);

            // ---------- 17. Neto Pagado ----------
            liquidacion.NetoPagado = RedondearPeso(liquidacion.TotalDevengado - liquidacion.TotalDeducciones);

            // ---------- 18. Guardar en base de datos ----------
            GuardarNomina(liquidacion);

            return liquidacion;
        }

        /// <summary>Persiste la nómina calculada en la base de datos.</summary>
        private void GuardarNomina(LiquidacionNomina nomina)
        {
            using var context = new AppDbContext();
            context.Nominas.Add(nomina);
            context.SaveChanges();
        }

        /// <summary>Obtiene el historial de nóminas de un empleado, más recientes primero.</summary>
        public System.Collections.Generic.List<LiquidacionNomina> ObtenerHistorial(int empleadoId)
        {
            using var context = new AppDbContext();
            return context.Nominas
                .Where(n => n.EmpleadoId == empleadoId)
                .OrderByDescending(n => n.PeriodoFin)
                .ToList();
        }

        /// <summary>Obtiene una nómina específica por su Id, incluyendo los datos del empleado.</summary>
        public LiquidacionNomina? ObtenerPorId(int nominaId)
        {
            using var context = new AppDbContext();
            return context.Nominas
                .Include(n => n.Empleado)
                .FirstOrDefault(n => n.Id == nominaId);
        }

        /// <summary>
        /// Redondea un valor monetario al peso entero más cercano
        /// (COP no maneja centavos en desembolsos reales de nómina),
        /// usando redondeo bancario para evitar sesgos acumulativos.
        /// </summary>
        private decimal RedondearPeso(decimal valor)
        {
            return Math.Round(valor, 0, MidpointRounding.ToEven);
        }
    }
}