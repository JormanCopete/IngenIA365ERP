using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Receipts.Common;

// Feature 015, US6 (T0193 a T0195; data-model §21; DT §2.7): los errores de la identificación de abonos, las partidas por identificar y las corridas no interactivas.
// Los que data-model §21 no nombraba son (nuevo).
public static partial class ErroresDeRecaudo
{
    public const string CodigoAbonoSobreIdentificado = "Receipts.BankItem.OverIdentified";
    public const string CodigoAbonoYaIdentificado = "Receipts.BankItem.AlreadyIdentified";
    public const string CodigoDivisionNoCuadra = "Receipts.BankItem.SplitMismatch";
    public const string CodigoClasificacionConAprobacion = "Receipts.UnidentifiedItem.ClassificationNeedsApproval";

    public static Error AbonoSobreIdentificado(decimal pendiente) => new ErrorConDatos(CodigoAbonoSobreIdentificado,
        $"Lo identificado pasa de lo que queda del abono ({pendiente:N2}): revise los valores.", new { pending = pendiente });

    public static Error AbonoYaIdentificado() => new(CodigoAbonoYaIdentificado,
        "El abono ya está identificado por completo: no queda nada por identificar.");

    public static Error DivisionNoCuadra(decimal pendiente, decimal dividido) => new ErrorConDatos(CodigoDivisionNoCuadra,
        $"La división suma {dividido:N2} y el abono tiene {pendiente:N2} por identificar: la suma tiene que ser exacta (o deje el resto como partida).",
        new { pending = pendiente, split = dividido });

    public static Error AbonoNoEsAbono() => new("Receipts.BankItem.NotACredit",
        "El movimiento es un cargo: sólo los abonos se identifican en Recaudo.");

    public static Error AbonoDuplicadoPendiente() => new("Receipts.BankItem.DuplicatePending",
        "El abono está marcado como posible duplicado: decida primero si es un duplicado o un movimiento distinto.");

    public static Error SinMedioDeTransferencia() => new("Receipts.BankItem.NoTransferMeans",
        "No hay un medio de pago activo de la clase transferencia (o consignación) en el catálogo común: créelo en Maestros antes de identificar abonos.");

    public static Error AbonoNoEsDuplicado() => new("Receipts.BankItem.NotADuplicate",
        "El abono no está marcado como posible duplicado.");

    public static Error ClasificacionConAprobacion() => new(CodigoClasificacionConAprobacion,
        "Clasificar un abono o una partida a un concepto necesita la aprobación de otra persona con el permiso de clasificar.");

    public static Error ConceptoDeClasificacionInvalido(string concepto) => new ErrorConDatos("Receipts.BankItem.ConceptInvalid",
        $"El concepto {concepto} no está activo o no es un concepto de ingreso.", new { concept = concepto });

    public static Error GiroDevueltoSinOrden() => new("Receipts.BankItem.ReturnedDisbursementOrderRequired",
        "El giro devuelto se enlaza a la orden de pago que se había girado: indique la orden.");

    public static Error PartidaSinSaldo(decimal saldo) => new ErrorConDatos("Receipts.UnidentifiedItem.InsufficientBalance",
        $"La partida tiene {saldo:N2} por resolver: no alcanza para lo pedido.", new { balance = saldo });

    public static Error PartidaResuelta(string estado) => new ErrorConDatos("Receipts.UnidentifiedItem.Resolved",
        $"La partida está «{estado}»: ya no tiene saldo por resolver.", new { state = estado });

    public static Error PartidaNoVencida(int dias, int plazo) => new ErrorConDatos("Receipts.UnidentifiedItem.NotDue",
        $"La partida tiene {dias} días y el destino de las partidas vencidas aplica desde los {plazo}: todavía no se puede llevar a ingreso.",
        new { ageDays = dias, dueDays = plazo });

    public static Error DestinoVencidoNoAdmiteIngreso(string destino) => new ErrorConDatos("Receipts.UnidentifiedItem.DestinationNotIncome",
        $"La política vigente de las partidas vencidas es «{destino}»: no admite llevarlas a ingreso.", new { destination = destino });

    public static Error CorridaLineaNoReprocesable(string estado) => new ErrorConDatos("Receipts.ProcessingRun.LineNotRetryable",
        $"La línea está «{estado}»: sólo se reprocesa una línea rechazada o pendiente.", new { state = estado });
}
