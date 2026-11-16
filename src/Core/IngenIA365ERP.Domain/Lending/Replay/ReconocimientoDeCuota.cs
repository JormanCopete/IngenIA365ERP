using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Domain.Lending.Replay;

// 2026-11-16 (pedido del dueño; feature 024, fase 4): el reconocimiento al inicio del ciclo. El motor sigue calculando día a día (el cálculo no cambia);
// lo que cambia es cuándo y contra qué se reconoce el ingreso. Esto es lo que la reconstrucción necesita para reproducirlo en el libro. (nuevo)

/// <summary>
/// Cómo se reconoce la cuota del período (<c>Causacion.InicioDelCiclo.Reconocimiento</c>): contra el <b>ingreso diferido</b>, que la causación de cada día
/// pasa a ingreso, o <b>directo</b> (todo al ingreso —en el seguro, al seguro por pagar— el día del reconocimiento; la causación del día ya no reconoce
/// nada). Se sella en el concepto del libro (<see cref="ConceptosReconocidos"/>). (nuevo)
/// </summary>
public enum ModoDeReconocimiento
{
    IngresoDiferido = 1,
    IngresoDirecto = 2,
}

/// <summary>
/// Un reconocimiento de la cuota del período tal como quedó en el libro (filas <see cref="LoanTransactionKind.PeriodRecognition"/>): el día desde el que
/// cubre la causación de la cuota (incluido) hasta su vencimiento, la cuota, el concepto reconocido (que lleva el rubro y el modo), lo que se reconoció y
/// cuándo se registró (R). Es la entrada determinística de la reconstrucción: los montos se calcularon una sola vez al registrarlo
/// (<see cref="ReconstruccionDeCredito.CuotaPorReconocer"/>) y nunca se recalculan. (nuevo)
/// </summary>
public sealed record ReconocimientoDeCuota(DateOnly Fecha, int Cuota, LoanConcept Concepto, decimal Monto, DateTime RegistradoEn);

/// <summary>
/// Lo reconocido de una cuota y un concepto a la fecha de un estado: lo que se reconoció, lo que ya consumió la causación (y el interés cobrado completo
/// al cancelar) y lo que queda por causar. <see cref="Concepto"/> es el concepto reconocido; <see cref="Rubro"/>, el concepto de su causación. (nuevo)
/// </summary>
public sealed record SaldoReconocido(int Cuota, LoanConcept Concepto, DateOnly Desde, decimal Reconocido, decimal Pendiente)
{
    public LoanConcept Rubro => ConceptosReconocidos.RubroDe(Concepto);
}

/// <summary>
/// Los conceptos de lo reconocido y todavía no causado (feature 024, fase 4): uno por rubro que se reconoce —interés corriente, seguro y administración; el
/// capital no tiene asiento y la mora nunca se reconoce— y por modo (<see cref="ModoDeReconocimiento"/>). Que el modo vaya en el concepto es lo que deja
/// cambiar el parámetro sin tocar lo ya reconocido: cada fila sabe contra qué se reconoció. Puro. (nuevo)
/// </summary>
public static class ConceptosReconocidos
{
    /// <summary>Los rubros que se reconocen al inicio del ciclo, en su orden.</summary>
    public static IReadOnlyList<LoanConcept> Rubros { get; } = [LoanConcept.CurrentInterest, LoanConcept.Insurance, LoanConcept.Administration];

    /// <summary>Los seis conceptos reconocidos, en su orden.</summary>
    public static IReadOnlyList<LoanConcept> Todos { get; } =
    [
        LoanConcept.RecognizedInterest, LoanConcept.RecognizedInsurance, LoanConcept.RecognizedAdministration,
        LoanConcept.RecognizedInterestDirect, LoanConcept.RecognizedInsuranceDirect, LoanConcept.RecognizedAdministrationDirect,
    ];

    /// <summary>¿Es un concepto de lo reconocido y no causado?</summary>
    public static bool Es(LoanConcept concepto) => concepto is >= LoanConcept.RecognizedInterest and <= LoanConcept.RecognizedAdministrationDirect;

    /// <summary>¿Se reconoció contra el ingreso diferido?</summary>
    public static bool EsDiferido(LoanConcept concepto) => concepto is >= LoanConcept.RecognizedInterest and <= LoanConcept.RecognizedAdministration;

    /// <summary>El modo sellado en el concepto.</summary>
    public static ModoDeReconocimiento ModoDe(LoanConcept concepto) =>
        EsDiferido(concepto) ? ModoDeReconocimiento.IngresoDiferido : ModoDeReconocimiento.IngresoDirecto;

    /// <summary>El rubro (el concepto de su causación) de un concepto reconocido; el mismo concepto si no lo es.</summary>
    public static LoanConcept RubroDe(LoanConcept concepto) => concepto switch
    {
        LoanConcept.RecognizedInterest or LoanConcept.RecognizedInterestDirect => LoanConcept.CurrentInterest,
        LoanConcept.RecognizedInsurance or LoanConcept.RecognizedInsuranceDirect => LoanConcept.Insurance,
        LoanConcept.RecognizedAdministration or LoanConcept.RecognizedAdministrationDirect => LoanConcept.Administration,
        _ => concepto,
    };

    /// <summary>El concepto reconocido de un rubro con un modo. Sólo el interés corriente, el seguro y la administración se reconocen.</summary>
    public static LoanConcept De(LoanConcept rubro, ModoDeReconocimiento modo) => (rubro, modo) switch
    {
        (LoanConcept.CurrentInterest, ModoDeReconocimiento.IngresoDiferido) => LoanConcept.RecognizedInterest,
        (LoanConcept.Insurance, ModoDeReconocimiento.IngresoDiferido) => LoanConcept.RecognizedInsurance,
        (LoanConcept.Administration, ModoDeReconocimiento.IngresoDiferido) => LoanConcept.RecognizedAdministration,
        (LoanConcept.CurrentInterest, ModoDeReconocimiento.IngresoDirecto) => LoanConcept.RecognizedInterestDirect,
        (LoanConcept.Insurance, ModoDeReconocimiento.IngresoDirecto) => LoanConcept.RecognizedInsuranceDirect,
        (LoanConcept.Administration, ModoDeReconocimiento.IngresoDirecto) => LoanConcept.RecognizedAdministrationDirect,
        _ => throw new ArgumentOutOfRangeException(nameof(rubro), rubro, "Sólo el interés corriente, el seguro y la administración se reconocen al inicio del ciclo."),
    };
}
