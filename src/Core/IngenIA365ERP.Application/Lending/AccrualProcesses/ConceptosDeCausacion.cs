using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Parameters;

namespace IngenIA365ERP.Application.Lending.AccrualProcesses;

/// <summary>Un concepto del proceso de causación: su bandera, el tipo de hecho que lo lleva, el parámetro de su modo y su nombre. (nuevo)</summary>
public sealed record ConceptoDeCausacion(AccrualProcessConcepts Concepto, string TipoDeHecho, string ParametroDeModo, string Nombre);

/// <summary>
/// Los cinco conceptos que puede tomar un proceso de causación (2026-11-16, pedido del dueño; feature 024, fase 3), uno por parámetro
/// <c>Causacion.Modo*</c> y por hecho masivo de la causación: la misma correspondencia que sella <c>ModoDePasoDeCartera.ParametroDeModoDeCausacion</c>.
/// El ajuste por fecha valor no está: viaja con su pago o lo emite la corrida de corrección, nunca espera. (nuevo)
/// </summary>
public static class ConceptosDeCausacion
{
    public static IReadOnlyList<ConceptoDeCausacion> Todos { get; } =
    [
        new(AccrualProcessConcepts.CurrentInterest, InteresCausadoV1.Type, ParametrosDeCartera.CausacionModoInteresCorriente, "Interés corriente"),
        new(AccrualProcessConcepts.DefaultInterest, MoraCausadaV1.Type, ParametrosDeCartera.CausacionModoInteresDeMora, "Interés de mora"),
        new(AccrualProcessConcepts.Insurance, SeguroCausadoV1.Type, ParametrosDeCartera.CausacionModoSeguro, "Seguro"),
        new(AccrualProcessConcepts.Administration, AdministracionCausadaV1.Type, ParametrosDeCartera.CausacionModoAdministracion, "Administración"),
        new(AccrualProcessConcepts.OtherItems, CorreccionMonetariaUvrCausadaV1.Type, ParametrosDeCartera.CausacionModoOtrosRubros,
            "Otros rubros (corrección monetaria UVR)"),
    ];

    /// <summary>Todos los conceptos juntos.</summary>
    public static AccrualProcessConcepts Cualquiera { get; } = Todos.Aggregate(AccrualProcessConcepts.None, (a, c) => a | c.Concepto);

    /// <summary>Los conceptos de la bandera, en el orden del catálogo.</summary>
    public static IReadOnlyList<ConceptoDeCausacion> De(AccrualProcessConcepts conceptos) =>
        Todos.Where(c => (conceptos & c.Concepto) == c.Concepto).ToList();

    /// <summary>Los tipos de hecho de la bandera.</summary>
    public static IReadOnlyList<string> TiposDe(AccrualProcessConcepts conceptos) => De(conceptos).Select(c => c.TipoDeHecho).ToList();

    /// <summary>El concepto de un tipo de hecho, o nulo si no es de la causación.</summary>
    public static ConceptoDeCausacion? DelTipo(string? tipoDeHecho) => Todos.FirstOrDefault(c => c.TipoDeHecho == tipoDeHecho);

    /// <summary>2026-11-16 (feature 024, fase 4): el nombre de un tipo de hecho del proceso (la cuota reconocida no es de un solo concepto). (nuevo)</summary>
    public static string NombreDelTipo(string tipoDeHecho) =>
        DelTipo(tipoDeHecho)?.Nombre ?? tipoDeHecho switch
        {
            CuotaReconocidaV1.Type => "Cuota reconocida del período",
            // 2026-11-24 (feature 025, fase 3): los hechos del recálculo de la carga desde el corte.
            InteresDelCorteV1.Type => "Interés del período al corte",
            AjusteDeCausacionPorFechaValorV1.Type => "Ajuste de causación desde el corte",
            PagoRedistribuidoPorValorNacionalV1.Type => "Pagos redistribuidos desde el corte",
            _ => tipoDeHecho,
        };

    /// <summary>La bandera de una lista de conceptos.</summary>
    public static AccrualProcessConcepts Juntar(IEnumerable<AccrualProcessConcepts> conceptos) =>
        conceptos.Aggregate(AccrualProcessConcepts.None, (a, c) => a | c);

    /// <summary>La lista de una bandera, concepto por concepto.</summary>
    public static IReadOnlyList<AccrualProcessConcepts> Separar(AccrualProcessConcepts conceptos) => De(conceptos).Select(c => c.Concepto).ToList();

    /// <summary>
    /// 2026-11-16 (feature 024, fase 4): el rubro del libro que la clase <c>PeriodRecognition</c> reconoce por cada concepto del proceso: el interés corriente,
    /// el seguro y la administración; la mora y los otros rubros no se reconocen al inicio del ciclo (nulo). (nuevo)
    /// </summary>
    public static LoanConcept? RubroReconocible(AccrualProcessConcepts concepto) => concepto switch
    {
        AccrualProcessConcepts.CurrentInterest => LoanConcept.CurrentInterest,
        AccrualProcessConcepts.Insurance => LoanConcept.Insurance,
        AccrualProcessConcepts.Administration => LoanConcept.Administration,
        _ => null,
    };

    /// <summary>2026-11-16 (feature 024, fase 4): el concepto del proceso de un rubro reconocible (o del concepto reconocido de ese rubro). (nuevo)</summary>
    public static AccrualProcessConcepts DelRubro(LoanConcept rubro) => Domain.Lending.Replay.ConceptosReconocidos.RubroDe(rubro) switch
    {
        LoanConcept.CurrentInterest => AccrualProcessConcepts.CurrentInterest,
        LoanConcept.Insurance => AccrualProcessConcepts.Insurance,
        LoanConcept.Administration => AccrualProcessConcepts.Administration,
        _ => AccrualProcessConcepts.None,
    };
}
