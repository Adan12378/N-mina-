using System;
using System.IO;
using ClosedXML.Excel;
using Nomina.Backend.Models;

namespace Nomina.Backend.Services
{
    /// <summary>
    /// Genera un archivo .xlsx con formato de comprobante de nómina contable,
    /// listo para imprimir o archivar. Usa la librería ClosedXML.
    /// </summary>
    public class ExcelService
    {
        // Paleta corporativa reutilizada del Frontend, para consistencia visual
        private static readonly XLColor ColorEncabezado = XLColor.FromHtml("#1F3A5F");
        private static readonly XLColor ColorSubtotal = XLColor.FromHtml("#EAF0F6");
        private static readonly XLColor ColorTotalNeto = XLColor.FromHtml("#2E7D32");
        private static readonly XLColor ColorTextoBlanco = XLColor.White;

        /// <summary>
        /// Exporta una LiquidacionNomina a un archivo .xlsx en la ruta indicada.
        /// Retorna la ruta absoluta del archivo generado.
        /// </summary>
        public string ExportarNomina(LiquidacionNomina nomina, string rutaDestino)
        {
            if (nomina.Empleado == null)
                throw new ArgumentException("La nómina debe incluir los datos del empleado (Empleado no puede ser null).");

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Comprobante Nomina");

            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.Style.Font.FontName = "Calibri";

            int fila = 1;

            // ---------- Encabezado principal ----------
            ws.Range(fila, 1, fila, 5).Merge().Value = "COMPROBANTE DE PAGO DE NÓMINA";
            ws.Range(fila, 1, fila, 5).Style.Font.SetBold().Font.SetFontSize(16);
            ws.Range(fila, 1, fila, 5).Style.Fill.SetBackgroundColor(ColorEncabezado);
            ws.Range(fila, 1, fila, 5).Style.Font.SetFontColor(ColorTextoBlanco);
            ws.Range(fila, 1, fila, 5).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
            ws.Row(fila).Height = 28;
            fila += 2;

            // ---------- Datos del empleado ----------
            EscribirEtiquetaValor(ws, ref fila, "Empleado:", nomina.Empleado.NombreCompleto);
            EscribirEtiquetaValor(ws, ref fila, "Documento:", nomina.Empleado.Documento);
            EscribirEtiquetaValor(ws, ref fila, "Cargo:", nomina.Empleado.Cargo);
            EscribirEtiquetaValor(ws, ref fila, "Tipo de Contrato:", nomina.Empleado.TipoContrato.ToString());
            EscribirEtiquetaValor(ws, ref fila, "Periodo Liquidado:",
                $"{nomina.PeriodoInicio:dd/MM/yyyy} - {nomina.PeriodoFin:dd/MM/yyyy}  ({nomina.DiasLiquidados} días)");
            fila++;

            // ---------- Sección DEVENGADOS ----------
            fila = EscribirEncabezadoSeccion(ws, fila, "DEVENGADOS");
            fila = EscribirFilaConcepto(ws, fila, "Salario Básico", nomina.SalarioBasicoDevengado);
            fila = EscribirFilaConcepto(ws, fila, $"Horas Extra Diurnas ({nomina.CantidadHorasExtrasDiurnas}h)", nomina.HorasExtrasDiurnas);
            fila = EscribirFilaConcepto(ws, fila, $"Horas Extra Nocturnas ({nomina.CantidadHorasExtrasNocturnas}h)", nomina.HorasExtrasNocturnas);
            fila = EscribirFilaConcepto(ws, fila, $"Recargo Nocturno ({nomina.CantidadRecargoNocturno}h)", nomina.RecargoNocturno);
            fila = EscribirFilaConcepto(ws, fila, $"Recargo Dominical/Festivo ({nomina.CantidadDominicalFestivo}h)", nomina.RecargoDominicalFestivo);
            fila = EscribirFilaConcepto(ws, fila, "Auxilio de Transporte", nomina.AuxilioTransporte);
            fila = EscribirFilaConcepto(ws, fila, "Otros Devengados", nomina.OtrosDevengados);
            fila = EscribirFilaTotal(ws, fila, "TOTAL DEVENGADO", nomina.TotalDevengado, esPositivo: true);
            fila++;

            // ---------- Sección DEDUCCIONES ----------
            fila = EscribirEncabezadoSeccion(ws, fila, "DEDUCCIONES");
            fila = EscribirFilaConcepto(ws, fila, "Salud (4%)", nomina.DeduccionSalud);
            fila = EscribirFilaConcepto(ws, fila, "Pensión (4%)", nomina.DeduccionPension);
            fila = EscribirFilaConcepto(ws, fila, "Fondo de Solidaridad Pensional (1%)", nomina.FondoSolidaridad);
            fila = EscribirFilaConcepto(ws, fila, "Retención en la Fuente", nomina.Retefuente);
            fila = EscribirFilaConcepto(ws, fila, "Otras Deducciones", nomina.OtrasDeducciones);
            fila = EscribirFilaTotal(ws, fila, "TOTAL DEDUCCIONES", nomina.TotalDeducciones, esPositivo: false);
            fila++;

            // ---------- NETO PAGADO ----------
            ws.Range(fila, 1, fila, 3).Merge().Value = "NETO PAGADO";
            ws.Range(fila, 1, fila, 3).Style.Font.SetBold().Font.SetFontSize(13);
            ws.Cell(fila, 4).Value = nomina.NetoPagado;
            ws.Cell(fila, 4).Style.NumberFormat.Format = "$ #,##0";
            ws.Range(fila, 1, fila, 4).Style.Fill.SetBackgroundColor(ColorTotalNeto);
            ws.Range(fila, 1, fila, 4).Style.Font.SetFontColor(ColorTextoBlanco);
            ws.Range(fila, 1, fila, 4).Style.Font.SetBold();
            ws.Row(fila).Height = 24;
            fila += 3;

            // ---------- Espacio para firma ----------
            ws.Cell(fila, 1).Value = "_______________________________";
            fila++;
            ws.Cell(fila, 1).Value = $"Firma del Empleado - {nomina.Empleado.NombreCompleto}";
            ws.Cell(fila, 1).Style.Font.SetItalic();
            fila++;
            ws.Cell(fila, 1).Value = $"C.C. {nomina.Empleado.Documento}";

            if (nomina.FechaFirma.HasValue)
            {
                fila++;
                ws.Cell(fila, 1).Value = $"Firmado el: {nomina.FechaFirma:dd/MM/yyyy HH:mm}";
            }

            // ---------- Ajustes visuales finales ----------
            ws.Columns(1, 5).AdjustToContents();
            ws.Column(1).Width = 32;
            ws.Column(2).Width = 20;

            // ---------- Guardar archivo ----------
            Directory.CreateDirectory(Path.GetDirectoryName(rutaDestino)!);
            workbook.SaveAs(rutaDestino);

            return Path.GetFullPath(rutaDestino);
        }

        /// <summary>Escribe una fila tipo "Etiqueta: Valor" (para los datos del empleado).</summary>
        private void EscribirEtiquetaValor(IXLWorksheet ws, ref int fila, string etiqueta, string valor)
        {
            ws.Cell(fila, 1).Value = etiqueta;
            ws.Cell(fila, 1).Style.Font.SetBold();
            ws.Cell(fila, 2).Value = valor;
            fila++;
        }

        /// <summary>Escribe el encabezado de una sección (DEVENGADOS / DEDUCCIONES) con estilo.</summary>
        private int EscribirEncabezadoSeccion(IXLWorksheet ws, int fila, string titulo)
        {
            ws.Range(fila, 1, fila, 4).Merge().Value = titulo;
            ws.Range(fila, 1, fila, 4).Style.Font.SetBold().Font.SetFontSize(12);
            ws.Range(fila, 1, fila, 4).Style.Fill.SetBackgroundColor(ColorEncabezado);
            ws.Range(fila, 1, fila, 4).Style.Font.SetFontColor(ColorTextoBlanco);
            return fila + 1;
        }

        /// <summary>Escribe una fila de concepto individual con su valor monetario formateado.</summary>
        private int EscribirFilaConcepto(IXLWorksheet ws, int fila, string concepto, decimal valor)
        {
            ws.Cell(fila, 1).Value = concepto;
            ws.Cell(fila, 4).Value = valor;
            ws.Cell(fila, 4).Style.NumberFormat.Format = "$ #,##0";
            ws.Range(fila, 1, fila, 4).Style.Border.SetBottomBorder(XLBorderStyleValues.Hair);
            return fila + 1;
        }

        /// <summary>Escribe la fila de subtotal (Total Devengado / Total Deducciones) resaltada.</summary>
        private int EscribirFilaTotal(IXLWorksheet ws, int fila, string etiqueta, decimal valor, bool esPositivo)
        {
            ws.Range(fila, 1, fila, 3).Merge().Value = etiqueta;
            ws.Range(fila, 1, fila, 3).Style.Font.SetBold();
            ws.Cell(fila, 4).Value = valor;
            ws.Cell(fila, 4).Style.NumberFormat.Format = "$ #,##0";
            ws.Cell(fila, 4).Style.Font.SetBold();
            ws.Cell(fila, 4).Style.Font.SetFontColor(esPositivo ? XLColor.DarkGreen : XLColor.DarkRed);
            ws.Range(fila, 1, fila, 4).Style.Fill.SetBackgroundColor(ColorSubtotal);
            return fila + 1;
        }
    }
}