using System.Globalization;
using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Treasury.Common;

// Feature 015, US10, US11 y US26 (T0138 a T0167; data-model §21): los errores de las cuentas bancarias propias, el fondo de liquidez, la bandeja de órdenes, los
// lotes, los archivos de pago y los resultados del giro. Los que data-model §21 no nombraba son (nuevo) y quedan en DT §2.7. Cada mensaje dice qué hacer.
public static partial class ErroresDeTesoreria
{
    public const string CodigoNoEncontrado = "Generic.NotFound";

    public static Error NoEncontrado(string recurso = "El registro") => new(CodigoNoEncontrado, $"{recurso} no existe.");

    private static string Pesos(decimal valor) => valor.ToString("N2", CultureInfo.GetCultureInfo("es-CO"));

    // ---- cuentas bancarias propias (US10) ----

    public static Error FondoDeLiquidezExclusivo() => new("Treasury.BankAccount.LiquidityFundExclusive",
        "Una cuenta del fondo de liquidez no admite otro propósito (FR-207): quite los demás propósitos o registre otra cuenta.");

    public static Error CuentaSinVinculo() => new("Treasury.BankAccount.NotLinked",
        "La cuenta bancaria no está vinculada a una cuenta contable bancaria del plan: vincúlela antes de operar con ella.");

    public static Error BancoSinCodigo(string banco) => new ErrorConDatos("Treasury.BankAccount.BankWithoutTransferCode",
        $"El banco «{banco}» no tiene código de transferencia (ACH): regístrelo en el maestro de bancos antes de usarlo.", new { bank = banco });

    public static Error CuentaRepetida(string numero) => new("Treasury.BankAccount.Duplicate",
        $"La cuenta {numero} de ese banco ya está registrada como cuenta propia.");

    public static Error CuentaInactiva(string codigo) => new("Treasury.BankAccount.Inactive",
        $"La cuenta propia {codigo} está inactiva o fuera de su vigencia.");

    public static Error SinPropositos() => new("Treasury.BankAccount.PurposeRequired",
        "Indique al menos un propósito de la cuenta (recaudo, pagos, nómina, convenio o fondo de liquidez).");

    public static Error SinPropositoDePagos(string codigo) => new("Treasury.Batch.SourceNotPayments",
        $"La cuenta {codigo} no tiene el propósito «pagos»: los lotes salen de una cuenta de pagos.");

    // ---- fondo de liquidez (US10, FR-207) ----

    public static Error UsoDelFondoRequerido(decimal monto) => new ErrorConDatos("Treasury.LiquidityFund.UseRequired",
        $"La cuenta es del fondo de liquidez: pagar {Pesos(monto)} desde ella exige un uso del fondo aprobado por ese monto (causal de la norma, soporte y aprobación de otra persona).",
        new { amount = monto });

    public static Error NoEsDelFondo(string codigo) => new("Treasury.LiquidityFund.NotLiquidityFundAccount",
        $"La cuenta {codigo} no es del fondo de liquidez: su uso no se registra.");

    public static Error UsoNoPendiente() => new("Treasury.LiquidityFund.NotPending",
        "El uso del fondo ya fue decidido: no admite otra decisión.");

    // ---- bandeja de órdenes (US10, FR-107 a FR-109, FR-115) ----

    public const string CodigoValorInvalido = "Treasury.Order.InvalidAmount";
    public const string CodigoFormaInvalida = "Treasury.Order.InvalidForm";
    public const string CodigoBeneficiarioInexistente = "Treasury.Order.BeneficiaryNotFound";
    public const string CodigoDestinoNoRegistrado = "Treasury.Order.DestinationNotRegistered";
    public const string CodigoDestinoEnEspera = "Treasury.Order.DestinationWaiting";
    public const string CodigoSinCuentaDestino = "Treasury.Order.DestinationMissing";
    public const string CodigoBancoSinCodigo = "Treasury.BankAccount.BankWithoutTransferCode";
    public const string CodigoListaVinculante = "Treasury.Order.BindingListMatch";

    public static Error ValorInvalido(decimal valor) => new ErrorConDatos(CodigoValorInvalido,
        $"La orden llegó por {Pesos(valor)}: una orden de pago tiene que ser por un valor positivo. El origen debe corregirla.", new { amount = valor });

    public static Error FormaInvalida(string forma) => new ErrorConDatos(CodigoFormaInvalida,
        $"La forma «{forma}» no existe en Tesorería: una orden sale por transferencia, cheque o pago a tercero; el efectivo sale por la caja (FR-114).",
        new { form = forma });

    public static Error PagadaPorArchivoDeCartera() => new("Treasury.Order.PaidByLendingFile",
        "La orden la paga un archivo de desembolsos de Cartera: no entra a un lote de Tesorería; sólo se registra su resultado (FR-108).");

    public static Error OrdenEnOtroLote(string numero) => new ErrorConDatos("Treasury.Order.AlreadyInBatch",
        $"La orden {numero} ya está en otro lote vigente: una orden va en a lo sumo un lote (FR-116).", new { order = numero });

    public static Error OrdenNoLista(string numero, string estado) => new ErrorConDatos("Treasury.Order.NotReady",
        $"La orden {numero} está «{estado}»: sólo una orden lista entra a un lote. Revise sus motivos de pendiente.", new { order = numero, state = estado });

    public static Error OrdenNoRevalidable(string estado) => new("Treasury.Order.InvalidState",
        $"Una orden «{estado}» no se vuelve a validar: sólo las recibidas, pendientes o listas.");

    // ---- lotes (US10, FR-110, FR-111, FR-213, FR-214) ----

    public static Error FormatoDistinto(string formato, string banco) => new ErrorConDatos("Treasury.Batch.FormatMismatch",
        $"El formato {formato} es de otro banco que el de la cuenta de origen ({banco}): elija un formato de ese banco o el genérico.",
        new { format = formato, bank = banco });

    public static Error OrigenEsFondoDeLiquidez() => new("Treasury.Batch.SourceIsLiquidityFund",
        "La cuenta de origen es del fondo de liquidez: un lote desde ella exige un uso del fondo aprobado por el monto (FR-207).");

    public static Error RevisionRequerida(IReadOnlyList<string> ordenes) => new ErrorConDatos("Treasury.Batch.ReviewRequired",
        "Hay órdenes a cuentas destino cambiadas hace pocos días: otra persona distinta de quien preparó tiene que revisarlas expresamente antes de aprobar (FR-210).",
        new { orders = ordenes });

    public static Error RevisionPorQuienPreparo() => new("Treasury.Batch.SelfReview",
        "Usted preparó este lote: la revisión expresa de una cuenta cambiada la hace otra persona.");

    public static Error LoteNoAprobado(string estado) => new ErrorConDatos("Treasury.Batch.NotApproved",
        $"El lote está «{estado}»: tiene que estar aprobado para generar su archivo o marcarlo enviado.", new { state = estado });

    public static Error LoteNoEnBorrador(string estado) => new ErrorConDatos("Treasury.Batch.NotDraft",
        $"El lote está «{estado}»: sólo un lote en borrador cambia sus órdenes o se envía a aprobación.", new { state = estado });

    public static Error LoteVacio() => new("Treasury.Batch.Empty", "El lote no tiene órdenes: agregue al menos una orden lista.");

    public static Error SinFormato(string banco) => new ErrorConDatos("Treasury.Batch.FormatNotFound",
        $"No hay un formato de pagos vigente para {banco} ni el genérico: cárguelo en Formatos bancarios o use la forma «portal del banco» (FR-213).",
        new { bank = banco });

    public static Error FormaDelLoteSinArchivo() => new("Treasury.Batch.NoFileForm",
        "Un lote por el portal del banco o en cheques no genera archivo: márquelo enviado por el portal o emita los cheques.");

    public static Error SoporteDeTerceroRequerido(string numero) => new ErrorConDatos("Treasury.Batch.ThirdPartySupportRequired",
        $"La orden {numero} es un pago a tercero: adjunte su soporte (constancia de consignación) antes de aprobar (FR-214).", new { order = numero });

    public static Error ListaVinculanteEnLote(IReadOnlyList<string> ordenes) => new ErrorConDatos("Treasury.Compliance.BindingListMatch",
        "Un beneficiario del lote coincide con una lista vinculante: sus órdenes no se aprueban ni se pagan. El oficial de cumplimiento revisa y, si es un falso positivo, libera a la persona (FR-203).",
        new { orders = ordenes });

    // ---- archivos de pago (US10, FR-112) ----

    public static Error ArchivoYaEnviado() => new("Treasury.File.AlreadySent",
        "El archivo ya se marcó enviado: no se anula ni se vuelve a enviar. Registre su resultado o su rechazo técnico.");

    public static Error ArchivoNoEnviado() => new("Treasury.File.NotSent",
        "El archivo todavía no se marcó enviado: primero márquelo enviado con quién lo cargó y quién lo autorizó en el banco.");

    public static Error ArchivoAnulado() => new("Treasury.File.Voided", "El archivo está anulado o rechazado por el banco: genere otro.");

    public static Error ArchivoAlterado() => new("Treasury.File.Tampered",
        "El archivo guardado no coincide con la huella registrada al generarlo: no se entrega. Genere uno nuevo y avise al oficial de seguridad.");

    public static Error ArchivoFaltante() => new("Treasury.File.Missing", "El archivo no quedó guardado: genere uno nuevo.");

    public static Error LineaDemasiadoLarga(int linea, string campo, string beneficiario) => new ErrorConDatos("Treasury.File.LineTooLong",
        $"La línea {linea} ({beneficiario}) no cabe en el campo «{campo}» del formato: revise el formato o el dato.", new { line = linea, field = campo });

    public static Error QuienEnElBancoRequerido() => new("Treasury.File.BankActorsRequired",
        "Indique quién cargó el archivo en el banco y quién lo autorizó allí (FR-112).");
}

// Feature 015, US10 (T0139): la segregación del uso del fondo de liquidez. (nuevo)
public static partial class ErroresDeTesoreria
{
    public static Error UsoDelFondoAutoaprobado() => new("Treasury.LiquidityFund.SelfApproval",
        "Usted pidió este uso del fondo de liquidez: lo aprueba otra persona con el permiso de aprobar usos del fondo.");
}

// Feature 015, US10 (T0143): el lote que no espera aprobación. (nuevo)
public static partial class ErroresDeTesoreria
{
    public static Error LoteNoEnAprobacion(string estado) => new ErrorConDatos("Treasury.Batch.NotPendingApproval",
        $"El lote está «{estado}»: sólo un lote enviado a aprobación se aprueba o se rechaza.", new { state = estado });
}
