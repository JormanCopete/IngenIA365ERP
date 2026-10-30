using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Persistence.Inventory;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Receipts;

namespace IngenIA365ERP.Persistence.Treasury;

/// <summary>
/// El SQL del cerrojo de la Tesorería nueva (feature 015, T0038; data-model §0.5): las tablas de cada nivel en su orden. Las sentencias son las
/// de <see cref="SqlDelCerrojoDeRecaudo"/> (mismos candados y misma forma) sobre las tablas <c>TES_</c>; la numeración comparte
/// <c>COR_Sequences</c>. (nuevo)
/// </summary>
public static class SqlDelCerrojoDeTesoreria
{
    public const string TablaSetup = "TES_Setup";
    public const string TablaCuentas = "TES_BankAccounts";
    public const string TablaChequeras = "TES_Checkbooks";
    public const string TablaLotes = "TES_PaymentBatches";
    public const string TablaOrdenes = "TES_PaymentOrders";
    public const string TablaConciliaciones = "TES_Reconciliations";

    /// <summary>La tabla de cada nivel de <see cref="ICerrojoDeTesoreria.BloquearAsync"/>.</summary>
    public static readonly IReadOnlyDictionary<NivelDeTesoreria, string> TablaDelNivel = new Dictionary<NivelDeTesoreria, string>
    {
        [NivelDeTesoreria.CuentaDeOrigen] = TablaCuentas,
        [NivelDeTesoreria.Chequera] = TablaChequeras,
        [NivelDeTesoreria.Lote] = TablaLotes,
        [NivelDeTesoreria.Ordenes] = TablaOrdenes,
        [NivelDeTesoreria.Conciliacion] = TablaConciliaciones,
    };

    public static SentenciaDelCerrojo Setup(DatabaseProvider motor, bool exclusivo) => SqlDelCerrojoDeRecaudo.Setup(motor, TablaSetup, exclusivo);

    public static SentenciaDelCerrojo? Filas(DatabaseProvider motor, NivelDeTesoreria nivel, IEnumerable<long> ids, bool exclusivo) =>
        TablaDelNivel.TryGetValue(nivel, out var tabla)
            ? SqlDelCerrojoDeRecaudo.Filas(motor, tabla, ids, exclusivo)
            : throw new ArgumentOutOfRangeException(nameof(nivel), nivel, "La configuración se toma con Setup.");
}
