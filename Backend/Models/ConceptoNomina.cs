namespace Nomina.Backend.Models
{
    public enum TipoRecargo
    {
        ExtraDiurna,
        ExtraNocturna,
        RecargoNocturno,
        RecargoDominicalFestivo,
        ExtraDiurnaDominicalFestivo,
        ExtraNocturnaDominicalFestivo
    }

    public class ConceptoNomina
    {
        public int Id { get; set; }

        public int NominaId { get; set; }

        public TipoRecargo Tipo { get; set; }

        public decimal HorasTrabajadas { get; set; }

        public decimal PorcentajeRecargo { get; set; }

        public decimal ValorCalculado { get; set; }

        public static decimal ObtenerPorcentaje(TipoRecargo tipo)
        {
            return tipo switch
            {
                TipoRecargo.ExtraDiurna => 0.25m,
                TipoRecargo.ExtraNocturna => 0.75m,
                TipoRecargo.RecargoNocturno => 0.35m,
                TipoRecargo.RecargoDominicalFestivo => 0.90m,
                TipoRecargo.ExtraDiurnaDominicalFestivo => 1.15m,
                TipoRecargo.ExtraNocturnaDominicalFestivo => 1.65m,
                _ => 0m
            };
        }
    }
}