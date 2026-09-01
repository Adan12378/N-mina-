using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nomina.Backend.Models
{
    public enum TipoContrato
    {
        TerminoIndefinido,
        TerminoFijo,
        ObraOLabor,
        PrestacionServicios,
        Aprendizaje
    }

    public enum EstadoEmpleado
    {
        Activo,
        Inactivo,
        Vacaciones,
        LicenciaNoRemunerada,
        IncapacidadMedica,
        Retirado
    }

    public class Empleado
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        public string Documento { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Apellido { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Cargo { get; set; } = string.Empty;

        public decimal SalarioBasico { get; set; }

        public DateTime FechaIngreso { get; set; }

        public TipoContrato TipoContrato { get; set; }

        public bool AuxilioTransporte { get; set; }

        public EstadoEmpleado Estado { get; set; } = EstadoEmpleado.Activo;

        [NotMapped]
        public string NombreCompleto => $"{Nombre} {Apellido}".Trim();
    }
}