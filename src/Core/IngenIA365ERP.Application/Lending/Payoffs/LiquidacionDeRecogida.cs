using System.Globalization;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Disbursements;
using IngenIA365ERP.Application.Lending.Engine;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending;
using IngenIA365ERP.Domain.Lending.AccrualProcesses;
using IngenIA365ERP.Domain.Lending.DayCount;
using IngenIA365ERP.Domain.Lending.Parameters;
using IngenIA365ERP.Domain.Lending.Payments;
using IngenIA365ERP.Domain.Lending.Payoffs;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Payoffs;

/// <summary>De dónde salió la liquidación de un crédito recogido (feature 028, R3). (nuevo)</summary>
public enum FuenteDeLaLiquidacion
{
    /// <summary>La reconstrucción a (D, R): la de siempre, con desglose exacto, modo de intereses y cuota causada.</summary>
    Reconstruccion = 1,

    /// <summary>Un crédito sin condiciones selladas: su última posición diaria hasta D.</summary>
    PosicionDiaria = 2,

    /// <summary>Un crédito sin condiciones selladas ni posición: lo que dice su libro hasta D, sin anticipos, saldos a favor ni mora.</summary>
    Libro = 3,
}

/// <summary>Por qué no se puede recoger un crédito: el código del error (<c>Lending.Payoff.*</c>), el motivo corto y el mensaje para la persona. (nuevo)</summary>
public sealed record BloqueoDeRecogida(string Codigo, string Motivo, string Mensaje)
{
    public Error ComoError() => new ErrorConDatos(Codigo, Mensaje, new { reason = Motivo });
}

/// <summary>
/// Los motivos y los errores que impiden recoger un crédito (feature 028, FR-002; api.md §1 <c>blockReason</c>, §2): en mora es una reestructuración
/// (<c>Lending.Payoff.RequiresRestructuring</c>, el mismo código del desembolso, que la tramita en Novedades); castigado, en insolvencia que negocia, sin
/// saldo o no activo, <c>Lending.Payoff.NotPayable</c> con el motivo. (nuevo)
/// </summary>
public static class ErroresDeRecogida
{
    public const string CodigoRequiereReestructuracion = ErroresDeDesembolso.CodigoRequiereReestructuracion;
    public const string CodigoNoPagable = "Lending.Payoff.NotPayable";

    public const string MotivoEnMora = "RequiresRestructuring";
    public const string MotivoCastigado = "WrittenOff";
    public const string MotivoInsolvencia = "Insolvency";
    public const string MotivoSinSaldo = "NoBalance";
    public const string MotivoNoActivo = "NotActive";

    /// <summary>2026-11-27 (feature 028, T062): por qué un crédito de vivienda se recoge completo aunque se pida dejar la cuota causada. (nuevo)</summary>
    public const string MotivoViviendaCompleta =
        "Un crédito de vivienda (en UVR o en pesos) se recoge completo: dejar la cuota causada con el interés del período sobre todo el saldo cobraría "
        + "interés del capital que se prepaga, y la Ley 546 de 1999 no admite penalidad por prepago.";

    /// <summary>2026-11-27 (feature 028, E2, T066): por qué se recoge completo un crédito sin cuota causada que dejar a la fecha. (nuevo)</summary>
    public const string MotivoSinCuotaQueDejar =
        "A la fecha del desembolso el crédito no tiene una cuota causada que dejar (la fecha es un vencimiento, está en gracia o el período de la próxima no "
        + "ha empezado): se recoge completo.";

    /// <summary>2026-11-27 (feature 028, T062): por qué un crédito sin condiciones selladas se recoge completo. (nuevo)</summary>
    public const string MotivoSinReconstruccionCompleta =
        "El crédito no tiene condiciones selladas: se recoge completo, con la cuota causada.";

    public static BloqueoDeRecogida EnMora(string numero, int dias) => new(CodigoRequiereReestructuracion, MotivoEnMora,
        $"El crédito {numero} tiene {dias} {(dias == 1 ? "día" : "días")} de mora: recogerlo es una reestructuración. Tramítela en Novedades como una "
        + "refinanciación que lo recoge.");

    public static BloqueoDeRecogida Castigado(string numero) => new(CodigoNoPagable, MotivoCastigado,
        $"El crédito {numero} está castigado: no se recoge con un crédito nuevo.");

    public static BloqueoDeRecogida EnInsolvencia(string numero) => new(CodigoNoPagable, MotivoInsolvencia,
        $"El deudor del crédito {numero} está en un proceso de insolvencia que negocia: sus créditos no se recogen mientras dure.");

    public static BloqueoDeRecogida SinSaldo(string numero) => new(CodigoNoPagable, MotivoSinSaldo,
        $"El crédito {numero} no tiene saldo por recoger.");

    public static BloqueoDeRecogida NoActivo(string numero, LoanStatus estado) => new(CodigoNoPagable, MotivoNoActivo, estado switch
    {
        LoanStatus.PaidOff => $"El crédito {numero} ya está cancelado.",
        LoanStatus.PendingDisbursement => $"El crédito {numero} todavía no se ha girado: sólo se recoge un crédito vigente.",
        LoanStatus.Voided => $"El crédito {numero} está anulado.",
        LoanStatus.Novated => $"El crédito {numero} se novó: su saldo pasó a otro crédito.",
        _ => $"El crédito {numero} no está vigente: sólo se recoge un crédito activo.",
    });

    // 2026-11-27 (feature 028, T014): los de la consulta de productos para recoger (api.md §1 y §2). (nuevo)

    public const string CodigoNoEsDelSolicitante = "Lending.Payoff.NotOfApplicant";
    public const string CodigoProductoNoAdmitido = "Lending.Payoff.ProductNotAdmitted";
    public const string CodigoFechaPasada = "Lending.Payoff.DateInPast";

    /// <summary>El motivo de un producto que la línea del crédito nuevo no admite recoger (<c>Recogida.ProductosAdmitidos</c>).</summary>
    public const string MotivoNoAdmitido = "NotAdmitted";

    /// <summary>El motivo de un crédito que no se pudo liquidar (una regla del motor o la usura del día sin publicar).</summary>
    public const string MotivoSinLiquidar = "NotSettled";

    public static Error NoEsDelSolicitante(string persona) => new(CodigoNoEsDelSolicitante,
        $"Uno de los productos elegidos no es de {persona} como deudora: sólo se recogen sus propios productos.");

    public static Error FechaPasada() => new(CodigoFechaPasada, "La fecha proyectada de desembolso es hoy o una fecha futura.");

    public static BloqueoDeRecogida NoAdmitido(string numero) => new(CodigoProductoNoAdmitido, MotivoNoAdmitido,
        $"La línea del crédito nuevo no admite recoger créditos (parámetro Recogida.ProductosAdmitidos): el crédito {numero} no se puede recoger con ella.");

    public static BloqueoDeRecogida SinLiquidar(string numero, Error error) => new(error.Code, MotivoSinLiquidar,
        $"El crédito {numero} no se pudo liquidar a la fecha: {error.Message}");

    // 2026-11-27 (feature 028, T024): los de guardar y radicar una solicitud que recoge (api.md §2). (nuevo)

    public const string CodigoModoNoAdmitido = "Lending.Payoff.ModeNotAdmitted";
    public const string CodigoNetoNoPositivo = "Lending.Payoff.NetNotPositive";

    /// <summary>
    /// En la entrega E1 sólo se recogían créditos; desde E3 (2026-11-27, T077) también las cuentas de aportes y ahorros y desde E4 (T087) las facturas a crédito de
    /// Comercial. Queda para el camino que no tiene con qué recogerlas (sin el servicio de cuentas o el puerto de Recaudo).
    /// </summary>
    public static Error ClaseNoAdmitida(PayoffProductKind clase) => new ErrorConDatos(CodigoProductoNoAdmitido,
        clase == PayoffProductKind.Account
            ? "La recogida de cuentas de aportes y ahorros no está disponible aquí."
            : "La recogida de facturas a crédito no está disponible aquí.",
        new { kind = clase.ToString() });

    /// <summary>La línea del crédito nuevo admite recoger créditos, pero no de ese modo (<c>Recogida.ProductosAdmitidos</c>).</summary>
    public static Error ModoNoAdmitido(string numero, PayoffMode modo, IReadOnlyList<PayoffMode> admitidos) => new ErrorConDatos(CodigoModoNoAdmitido,
        $"La línea del crédito nuevo no admite recoger el crédito {numero} {TextoDelModo(modo)}. Admite: {string.Join(", ", admitidos.Select(TextoDelModo))}.",
        new { number = numero, mode = modo.ToString(), admittedModes = admitidos.Select(m => m.ToString()).ToList() });

    /// <summary>Lo que se recoge deja el neto a girar en cero o negativo: así no se radica (FR-007).</summary>
    public static Error NetoNoPositivo(decimal monto, decimal recogido) => new ErrorConDatos(CodigoNetoNoPositivo,
        $"Lo que se recoge ({Pesos(recogido)}) deja el neto a girar del crédito nuevo ({Pesos(monto)}) en {Pesos(monto - recogido)}: suba el monto o "
        + "recoja menos.",
        new { amount = monto, payoffTotal = recogido, net = monto - recogido });

    // 2026-11-27 (feature 028, T032–T035; api.md §3): los del desembolso que recoge lo que autorizó la solicitud. (nuevo)

    public const string CodigoDebeCoincidirConLaSolicitud = "Lending.Payoff.MustMatchApplication";
    public const string CodigoDiferenciasSinReconocer = "Lending.Payoff.DifferencesNotAcknowledged";
    public const string CodigoDiferenciaSobreLaTolerancia = "Lending.Payoff.DifferenceAboveTolerance";
    public const string CodigoAplicadoDistinto = "Lending.Payoff.AppliedDiffers";

    /// <summary>
    /// El cuerpo del desembolso trae una lista de recogidas distinta de la que autorizó la solicitud (D9: todas, del mismo modo; un tramo siguiente no
    /// recoge nada). La lista es opcional: sin ella se recoge lo autorizado.
    /// </summary>
    public static Error DebeCoincidirConLaSolicitud(IReadOnlyList<string> autorizados) => new ErrorConDatos(CodigoDebeCoincidirConLaSolicitud,
        autorizados.Count == 0
            ? "Este desembolso no recoge nada: la solicitud no autoriza recoger créditos o es un tramo siguiente (las recogidas van con el primero). "
              + "Quite los créditos que envió."
            : $"El desembolso recoge lo que autorizó la solicitud, todo y del mismo modo ({string.Join(", ", autorizados)}): no se elige de nuevo. Quite la "
              + "lista o envíe la de la solicitud; para cambiar lo que recoge, devuélvala para corregirla.",
        new { authorized = autorizados });

    /// <summary>Lo que se recoge cambió desde la proyección de la solicitud (o un producto ya se canceló) y quien desembolsa no dijo que lo revisó.</summary>
    public static Error DiferenciasSinReconocer(int productos, decimal total) => new ErrorConDatos(CodigoDiferenciasSinReconocer,
        $"Lo que se recoge cambió desde la proyección de la solicitud en {productos} {(productos == 1 ? "producto" : "productos")} ({Pesos(total)} en total). "
        + "Revise las diferencias en la vista previa y confirme que las revisó.",
        new { count = productos, total });

    /// <summary>Una diferencia pasa de <c>Recogida.ToleranciaDeDiferencia</c>: hay que devolver la solicitud y volver a mostrarle al asociado lo que recibe.</summary>
    /// <remarks>2026-11-27 (feature 028, E3): los números de <paramref name="cuentas"/> se nombran como cuentas.</remarks>
    public static Error DiferenciaSobreLaTolerancia(decimal tolerancia, IReadOnlyList<(string Numero, decimal Diferencia)> productos,
        IReadOnlyCollection<string>? cuentas = null, IReadOnlyCollection<string>? facturas = null) => new ErrorConDatos(
        CodigoDiferenciaSobreLaTolerancia,
        $"Lo que se recoge cambió más de {Pesos(tolerancia)} desde la proyección de la solicitud en "
        + string.Join(", ", productos.Select(p =>
            $"{(cuentas?.Contains(p.Numero) == true ? "la cuenta" : facturas?.Contains(p.Numero) == true ? "la factura" : "el crédito")} {p.Numero} ({Pesos(p.Diferencia)})"))
        + ". Devuelva la solicitud para corregirla y mostrarle al asociado lo que recibe.",
        new { tolerance = tolerancia, products = productos.Select(p => new { number = p.Numero, difference = p.Diferencia }).ToList() });

    /// <summary>Defensa del desembolso (H4): Cartera aplicó a un crédito recogido algo distinto de lo que se liquidó bajo el cerrojo; nada queda hecho.</summary>
    public static Error AplicadoDistinto(Guid credito, string numero, decimal liquidado, decimal aplicado) => new ErrorConDatos(CodigoAplicadoDistinto,
        $"Al crédito {numero} se le aplicaron {Pesos(aplicado)} y se habían liquidado {Pesos(liquidado)}: el desembolso no se hizo. Pida otra vez la vista "
        + "previa y confirme.",
        new { loanPublicId = credito, number = numero, expected = liquidado, applied = aplicado });

    private static string TextoDelModo(PayoffMode modo) => modo switch
    {
        PayoffMode.Total => "total",
        PayoffMode.Partial => "parcial",
        PayoffMode.Installments => "por cuotas",
        PayoffMode.Deposit => "como abono",
        _ => modo.ToString(),
    };

    public static string Pesos(decimal valor) => (valor < 0m ? "-$" : "$") + Math.Abs(valor).ToString("#,##0.##", CultureInfo.GetCultureInfo("es-CO"));

    // 2026-11-27 (feature 028, E2, T066; HU4-3): dejar la cuota causada se pide sólo con «Preguntar». (nuevo)

    public const string CodigoTratamientoNoAdmitido = "Lending.Payoff.InstallmentTreatmentNotAdmitted";

    /// <summary>Se pidió dejar la cuota causada de un crédito cuya línea dice «Incluir» (<c>Recogida.CuotaCausada</c>): no lo elige el asesor.</summary>
    public static Error TratamientoNoAdmitido(string numero) => new ErrorConDatos(CodigoTratamientoNoAdmitido,
        $"La línea del crédito {numero} recoge la cuota causada completa (parámetro Recogida.CuotaCausada = Incluir): no se puede dejar a su canal.",
        new { number = numero });

    // 2026-11-27 (feature 028, E3, T077–T078; HU6): los de recoger aportes y ahorros. (nuevo)

    /// <summary>El motivo de una cuenta de aportes cuya línea no admite aportes extraordinarios (no admite un abono).</summary>
    public const string MotivoSinAbono = "DepositNotAllowed";

    /// <summary>La cooperativa no recoge ese producto con la línea del crédito nuevo (HU6-1: de fábrica, «la cooperativa no los recoge»).</summary>
    public static BloqueoDeRecogida CuentaNoAdmitida(string numero, ProductoDeRecogida producto) => new(CodigoProductoNoAdmitido, MotivoNoAdmitido,
        $"La cooperativa no recoge {TextoDelProducto(producto)} con el crédito nuevo (parámetro Recogida.ProductosAdmitidos): la cuenta {numero} no se puede "
        + "recoger.");

    /// <summary>La cuenta no recibe consignaciones: sólo se recoge una cuenta activa o inactiva.</summary>
    public static BloqueoDeRecogida CuentaNoOperable(string numero, MemberAccountStatus estado) => new(CodigoNoPagable, MotivoNoActivo, estado switch
    {
        MemberAccountStatus.Closed => $"La cuenta {numero} ya está cancelada.",
        MemberAccountStatus.Opening => $"La cuenta {numero} sigue en apertura: sólo se recoge una cuenta activa.",
        _ => $"La cuenta {numero} está «{estado}»: sólo se recoge una cuenta activa o inactiva.",
    });

    public static BloqueoDeRecogida TitularFallecido(string numero) => new(CodigoNoPagable, MotivoNoActivo,
        $"El titular de la cuenta {numero} figura como fallecido: la cuenta no recibe operaciones.");

    /// <summary>HU6-2: una línea de aportes que no admite extraordinarios no admite un abono libre.</summary>
    public static BloqueoDeRecogida SinAbonoEnLaLinea(string numero, string linea) => new(CodigoModoNoAdmitido, MotivoSinAbono,
        $"La línea de aportes {linea} no admite aportes extraordinarios: la cuenta {numero} sólo se recoge por sus cuotas pendientes.");

    /// <summary>Por cuotas, sin cuotas pendientes a la fecha: no hay nada que recoger.</summary>
    public static BloqueoDeRecogida SinCuotasPendientes(string numero, DateOnly fecha) => new(CodigoNoPagable, MotivoSinSaldo,
        $"La cuenta {numero} no tiene cuotas pendientes al {fecha:dd/MM/yyyy}: no hay nada que recoger por cuotas.");

    /// <summary>HU6-4: un CDAT no es una deuda y no se recoge.</summary>
    public static Error CdatNoSeRecoge(string numero) => new ErrorConDatos(CodigoProductoNoAdmitido,
        $"La cuenta {numero} es un CDAT: no es una deuda y no se recoge con un crédito nuevo.", new { number = numero, reason = MotivoNoAdmitido });

    /// <summary>HU6-3: el modo no lo tiene el producto (el ahorro contractual, sólo cuotas) o no lo admite la línea del crédito nuevo.</summary>
    public static Error ModoDeCuentaNoAdmitido(string numero, ProductoDeRecogida producto, PayoffMode modo, IReadOnlyList<PayoffMode> admitidos) =>
        new ErrorConDatos(CodigoModoNoAdmitido,
            (ProductosAdmitidos.ModosPosibles[producto].Contains(modo)
                ? $"La línea del crédito nuevo no admite recoger la cuenta {numero} {TextoDelModo(modo)}."
                : $"La cuenta {numero} es de {TextoDelProducto(producto)}: no se recoge {TextoDelModo(modo)}.")
            + (admitidos.Count > 0 ? $" Admite: {string.Join(", ", admitidos.Select(TextoDelModo))}." : string.Empty),
            new { number = numero, mode = modo.ToString(), admittedModes = admitidos.Select(m => m.ToString()).ToList() });

    /// <summary>La cuenta de una recogida autorizada ya no existe o no es del solicitante como titular.</summary>
    public static Error CuentaNoAutorizada(Guid cuenta, string numero) => new ErrorConDatos(ErroresDeDesembolso.CodigoRecogidaNoAutorizada,
        $"La cuenta {numero} ya no es del solicitante como titular: no se recoge. Devuelva la solicitud para corregir lo que recoge.",
        new { accountPublicId = cuenta, number = numero });

    /// <summary>Defensa del desembolso (H4): la cuenta recibió algo distinto de lo evaluado; nada queda hecho.</summary>
    public static Error ConsignadoDistinto(Guid cuenta, string numero, decimal evaluado, decimal consignado) => new ErrorConDatos(CodigoAplicadoDistinto,
        $"A la cuenta {numero} se le consignaron {Pesos(consignado)} y se habían evaluado {Pesos(evaluado)}: el desembolso no se hizo. Pida otra vez la "
        + "vista previa y confirme.",
        new { accountPublicId = cuenta, number = numero, expected = evaluado, applied = consignado });

    /// <summary>El error de la consignación (o de su reverso) con la cuenta: el mismo código, el mensaje precedido por la cuenta.</summary>
    public static Error ConLaCuenta(Error error, Guid cuenta, string numero)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error is ErrorConDatos { Data: not null } conDatos
            ? new ErrorConDatos(error.Code, $"Cuenta {numero}: {error.Message}", conDatos.Data)
            : new ErrorConDatos(error.Code, $"Cuenta {numero}: {error.Message}", new { accountPublicId = cuenta, number = numero });
    }

    // 2026-11-27 (feature 028, E4, T087; HU7): los de recoger facturas a crédito de Comercial. (nuevo)

    /// <summary>El motivo de una factura que Recaudo no puede recoger todavía (falta su tipo de documento o el medio del cruce interno).</summary>
    public const string MotivoRecaudoSinConfigurar = "ReceiptsNotReady";

    /// <summary>HU7: la cooperativa no recoge facturas a crédito con la línea del crédito nuevo (de fábrica, «la cooperativa no las recoge»).</summary>
    public static BloqueoDeRecogida FacturaNoAdmitida(string numero) => new(CodigoProductoNoAdmitido, MotivoNoAdmitido,
        $"La cooperativa no recoge facturas a crédito con el crédito nuevo (parámetro Recogida.ProductosAdmitidos): la factura {numero} no se puede recoger.");

    /// <summary>Recaudo no puede emitir el recibo sin caja de la recogida (FR-026): sin él la factura no se recoge y se dice por qué.</summary>
    public static BloqueoDeRecogida RecaudoSinConfigurar(string numero, string motivo) => new(CodigoNoPagable, MotivoRecaudoSinConfigurar,
        $"La factura {numero} no se puede recoger: {motivo}");

    /// <summary>La línea del crédito nuevo admite facturas, pero no de ese modo.</summary>
    public static Error ModoDeFacturaNoAdmitido(string numero, PayoffMode modo, IReadOnlyList<PayoffMode> admitidos) => new ErrorConDatos(CodigoModoNoAdmitido,
        $"La línea del crédito nuevo no admite recoger la factura {numero} {TextoDelModo(modo)}."
        + (admitidos.Count > 0 ? $" Admite: {string.Join(", ", admitidos.Select(TextoDelModo))}." : string.Empty),
        new { number = numero, mode = modo.ToString(), admittedModes = admitidos.Select(m => m.ToString()).ToList() });

    /// <summary>La factura que se pide no tiene saldo pendiente a nombre del solicitante (no es suya, no existe o ya se pagó).</summary>
    public static Error FacturaSinSaldo(string numero, string persona) => new ErrorConDatos(CodigoNoEsDelSolicitante,
        $"La factura {numero} no tiene saldo pendiente a nombre de {persona}: sólo se recogen sus propias facturas a crédito.", new { invoice = numero });

    /// <summary>Defensa del desembolso (H4): Recaudo aplicó a una factura algo distinto de lo evaluado; nada queda hecho.</summary>
    public static Error FacturaAplicadaDistinta(string numero, decimal evaluado, decimal aplicado) => new ErrorConDatos(CodigoAplicadoDistinto,
        $"A la factura {numero} se le aplicaron {Pesos(aplicado)} y se habían evaluado {Pesos(evaluado)}: el desembolso no se hizo. Pida otra vez la vista "
        + "previa y confirme.",
        new { invoice = numero, expected = evaluado, applied = aplicado });

    /// <summary>El producto en palabras («aportes», «ahorro a la vista»…).</summary>
    public static string TextoDelProducto(ProductoDeRecogida producto) => producto switch
    {
        ProductoDeRecogida.Creditos => "créditos",
        ProductoDeRecogida.Aportes => "aportes",
        ProductoDeRecogida.AhorroALaVista => "ahorro a la vista",
        ProductoDeRecogida.AhorroPermanente => "ahorro permanente",
        ProductoDeRecogida.AhorroContractual => "ahorro contractual",
        ProductoDeRecogida.FacturasComerciales => "facturas a crédito",
        _ => producto.ToString(),
    };
}

/// <summary>
/// La liquidación de un crédito recogido a la fecha proyectada D con la información hasta R (feature 028, R3; FR-003, FR-024): lo que cobraría el
/// <c>FullPayoff</c> de la recogida por el contrato (<see cref="ValorARecoger"/>, al peso), su desglose por concepto, el modo de intereses con su
/// explicación, los días de mora, lo que impide recogerlo, la cuota causada, la huella y los avisos. (nuevo)
/// </summary>
public sealed record LiquidacionDeCreditoRecogido
{
    public required int LoanId { get; init; }
    public required Guid LoanPublicId { get; init; }
    public required string Number { get; init; }

    /// <summary>D.</summary>
    public required DateOnly Fecha { get; init; }

    /// <summary>R: lo conocido hasta ese instante (ahora, si no se pidió otro).</summary>
    public required DateTime InformacionHasta { get; init; }

    /// <summary>Lo que cobra la recogida total: exactamente lo que aplica el contrato con <c>FullPayoff</c> y el documento del desembolso.</summary>
    public required decimal ValorARecoger { get; init; }

    /// <summary>Por concepto; su <see cref="DesgloseDeRecogida.Total"/> es <see cref="ValorARecoger"/> (salvo un crédito sin saldo, que no se recoge).</summary>
    public required DesgloseDeRecogida Desglose { get; init; }

    /// <summary>Cómo cobra el interés de la cuota en curso: <c>Corrido</c> o <c>CuotaCompleta</c>, con su motivo legal si difiere de lo pedido.</summary>
    public required ModoDeInteresesAlCancelar Intereses { get; init; }

    /// <summary>Lo que dice <c>Recogida.InteresesProporcionales</c> para la línea del crédito a D: <c>ComoAlCancelar</c>, <c>Si</c> o <c>No</c>.</summary>
    public required string Proporcionales { get; init; }

    /// <summary>El modo de intereses en palabras (por qué se cobra el corrido o la cuota completa); nula si no hay nada que explicar.</summary>
    public string? ExplicacionDeIntereses { get; init; }

    /// <summary>La línea causa por período calendario a D (lo causado de un tramo sin empezar no se cobra: <see cref="DesgloseDeRecogida.InteresDevuelto"/>).</summary>
    public bool PeriodoCalendario { get; init; }

    public int DiasDeMora { get; init; }

    /// <summary>Lo que impide recogerlo, en orden; vacío si se puede.</summary>
    public IReadOnlyList<BloqueoDeRecogida> Bloqueos { get; init; } = [];

    /// <summary>La cuota del período en curso a D (en E1 siempre se incluye en la recogida); nula sin cuota por vencer o sin reconstrucción.</summary>
    public CuotaCausadaDeRecogida? CuotaCausada { get; init; }

    /// <summary>El saldo a favor del deudor en el crédito: la cancelación no lo usa (D7), así que no se descuenta; queda a favor y se resuelve aparte.</summary>
    public decimal SaldoAFavorSinDescontar { get; init; }

    /// <summary><c>sha256:</c> + la huella de la reconstrucción con el modo de intereses, el período calendario y el valor.</summary>
    public required string Huella { get; init; }

    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, T051; HU5): el interés de la recogida explicado renglón a renglón —lo corrido hasta D, la cuota completa, el proporcional por
    /// la ley, lo devuelto por período calendario y lo reconocido que se libera—, con los días de la reconstrucción. Vacío sin reconstrucción. (nuevo)
    /// </summary>
    public IReadOnlyList<RenglonDeIntereses> ExplicacionDelInteres { get; init; } = [];

    /// <summary>
    /// 2026-11-27 (feature 028, T051): lo reconocido al inicio del ciclo y no causado a D que la recogida libera. Es informativo: no es deuda del asociado
    /// y no cambia <see cref="ValorARecoger"/>. (nuevo)
    /// </summary>
    public decimal ReconocidoQueSeLibera { get; init; }

    public FuenteDeLaLiquidacion Fuente { get; init; } = FuenteDeLaLiquidacion.Reconstruccion;

    /// <summary>
    /// 2026-11-27 (feature 028, T062): ¿se le puede dejar la cuota causada («todo menos la cuota causada», <c>Recogida.CuotaCausada = Excluir</c>)? No a un
    /// crédito de vivienda —en UVR o en pesos—, que se recoge completo (<see cref="RecogidoCompletoPorque"/>), ni a uno sin reconstrucción. De fábrica
    /// (<c>Incluir</c>) no se mira. (nuevo)
    /// </summary>
    public bool AdmiteDejarLaCuota { get; init; }

    /// <summary>2026-11-27 (feature 028, T062): por qué se recoge completo aunque se pida dejar la cuota causada; nulo si se puede dejar. (nuevo)</summary>
    public string? RecogidoCompletoPorque { get; init; }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; HU4, D1): lo que dice <c>Recogida.CuotaCausada</c> con la línea de este crédito a D: <c>Incluir</c> (de fábrica),
    /// <c>Excluir</c> o <c>Preguntar</c>. (nuevo)
    /// </summary>
    public string CuotaCausadaConfigurada { get; init; } = ParametrosDeCartera.CuotaIncluir;

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; FR-020): lo que cuesta recoger todo menos la cuota causada (<see cref="AplicadorDePagos.ValorParaCancelarSalvoCuota"/>):
    /// la cuota que se deja, lo que cuesta lo demás y su desglose, y lo que la cuota deja debiendo para su canal. Nulo si no se admite dejarla. (nuevo)
    /// </summary>
    public RecogidaSalvoCuota? SalvoCuota { get; init; }

    /// <summary>2026-11-27 (feature 028, E2, T064, T066; FR-021): en qué va la cuota del período frente a su pagaduría o su nómina propia; nula si no va. (nuevo)</summary>
    public CuotaEnviada? CuotaEnviada { get; init; }

    public bool Recogible => Bloqueos.Count == 0;

    public BloqueoDeRecogida? Bloqueo => Bloqueos.Count == 0 ? null : Bloqueos[0];

    /// <summary>2026-11-27 (feature 028, E2, T066; D2): con «Preguntar» y un crédito que admite dejar la cuota, el asesor elige. (nuevo)</summary>
    public bool CuotaElegible => AdmiteDejarLaCuota && CuotaCausadaConfigurada == ParametrosDeCartera.CuotaPreguntar;

    /// <summary>2026-11-27 (feature 028, E2, T066): el tratamiento que rige sin elección: «Excluir» lo excluye (si se admite); lo demás, incluirla. (nuevo)</summary>
    public InstallmentTreatment TratamientoPorDefecto =>
        TratamientoEfectivo(CuotaCausadaConfigurada == ParametrosDeCartera.CuotaExcluir ? InstallmentTreatment.Exclude : InstallmentTreatment.Include);

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; HU4-3): el tratamiento que rige con lo que se pidió: «Excluir» siempre lo excluye; «Preguntar», lo pedido (sin pedir,
    /// incluirla); «Incluir» no admite que se pida excluirla (<c>Lending.Payoff.InstallmentTreatmentNotAdmitted</c>). Después, <see cref="TratamientoEfectivo"/>:
    /// un crédito que no admite dejar la cuota se recoge completo. (nuevo)
    /// </summary>
    public Result<InstallmentTreatment> ResolverTratamiento(InstallmentTreatment? pedido) => CuotaCausadaConfigurada switch
    {
        ParametrosDeCartera.CuotaExcluir => Result.Success(TratamientoEfectivo(InstallmentTreatment.Exclude)),
        ParametrosDeCartera.CuotaPreguntar => Result.Success(TratamientoEfectivo(pedido ?? InstallmentTreatment.Include)),
        _ => pedido == InstallmentTreatment.Exclude
            ? Result.Failure<InstallmentTreatment>(ErroresDeRecogida.TratamientoNoAdmitido(Number))
            : Result.Success(InstallmentTreatment.Include),
    };

    /// <summary>
    /// Lo que guarda la solicitud (<c>LND_CreditApplicationPayoffs</c>) con el tratamiento de la cuota causada ya resuelto. 2026-11-27 (feature 028, T062):
    /// pedir que se excluya a un crédito que no lo admite (<see cref="AdmiteDejarLaCuota"/>) es incluirla: se recoge completo. 2026-11-27 (feature 028, E2,
    /// T066): excluyéndola, lo que cuesta todo menos la cuota y la cuota que se deja con lo que deberá; y siempre, la cuota enviada.
    /// </summary>
    public ProyeccionDeRecogida Proyeccion(InstallmentTreatment tratamiento = InstallmentTreatment.Include)
    {
        var segun = Segun(tratamiento);
        return new(Fecha, InformacionHasta, segun.Desglose, Huella, Intereses.Efectivo, segun.Tratamiento, segun.Cuota,
            CuotaEnviada?.Estado ?? SentInstallmentState.None, CuotaEnviada?.ArchivoId);
    }

    /// <summary>2026-11-27 (feature 028, T062): el tratamiento que rige: excluir sólo si se admite dejar la cuota; si no, incluirla. (nuevo)</summary>
    public InstallmentTreatment TratamientoEfectivo(InstallmentTreatment pedido) =>
        pedido == InstallmentTreatment.Exclude && !AdmiteDejarLaCuota ? InstallmentTreatment.Include : pedido;

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066): lo que se recoge con el tratamiento <paramref name="pedido"/> (ya efectivo): el valor, el desglose, la cuota causada
    /// (la que se deja, con lo que deberá; o la que se incluye), la cuota dejada, el interés explicado (sin los renglones de la cuota dejada) y la explicación
    /// de la cuota en la recogida. (nuevo)
    /// </summary>
    public RecogidaSegunCuota Segun(InstallmentTreatment pedido)
    {
        var tratamiento = TratamientoEfectivo(pedido);
        // 2026-11-27 (feature 028, D13 ajustada): el texto de la cuota dice la regla con que se deja —con el proporcional, cuánto, por qué y lo que sobraría
        // si su canal la paga completa—; los renglones del interés de la cuota dejada no van (los reemplaza ese texto, como en D13-A).
        if (tratamiento == InstallmentTreatment.Exclude && SalvoCuota is { } s)
            return new RecogidaSegunCuota(tratamiento, s.ValorARecoger, s.Desglose, new CuotaCausadaDeRecogida(s.Cuota, s.QuedaEnLaCuota, s.Vencimiento),
                s.Cuota, s.QuedaEnLaCuota, ExplicacionDelInteres.Where(r => r.Cuota != s.Cuota).ToList(),
                TextosDeCuotaEnviada.EnLaRecogida(s.Cuota, s.QuedaEnLaCuota, tratamiento, CuotaEnviada, s.Regla, s.QuedaTalComoSeEnvio));
        return new RecogidaSegunCuota(InstallmentTreatment.Include, ValorARecoger, Desglose, CuotaCausada, null, null, ExplicacionDelInteres,
            CuotaCausada is { } c ? TextosDeCuotaEnviada.EnLaRecogida(c.Numero, c.Valor, InstallmentTreatment.Include, CuotaEnviada) : null);
    }
}

/// <summary>
/// 2026-11-27 (feature 028, E2, T066; FR-019, FR-020): lo que cuesta recoger todo menos la cuota causada: la cuota que se deja y su vencimiento, lo que se
/// recoge (lo mismo que aplica el contrato con <c>PayoffExceptCurrentInstallment</c>) y su desglose, y lo que la cuota deberá al vencer —lo que su canal paga
/// para cerrarla al peso—. (nuevo)
/// </summary>
public sealed record RecogidaSalvoCuota(int Cuota, DateOnly Vencimiento, decimal ValorARecoger, DesgloseDeRecogida Desglose, decimal QuedaEnLaCuota)
{
    /// <summary>
    /// 2026-11-27 (feature 028, D13 ajustada por el dueño): la regla con que se deja la cuota —tal como se envió (fuera del tope legal del prepago) o con el
    /// interés proporcional (dentro del tope o sin los valores nacionales para saberlo)—, la misma con que la aplica el contrato. (nuevo)
    /// </summary>
    public ReglaDeCuotaDejada Regla { get; init; } = ReglaDeCuotaDejada.TalComoSeEnvio;

    /// <summary>2026-11-27 (feature 028, D13 ajustada): lo que la cuota debería tal como se envió; igual a <see cref="QuedaEnLaCuota"/> sin el proporcional. (nuevo)</summary>
    public decimal QuedaTalComoSeEnvio { get; init; } = QuedaEnLaCuota;

    /// <summary>2026-11-27 (feature 028, D13 ajustada): lo que sobra si el canal paga la cuota tal como se envió (cero sin el proporcional). (nuevo)</summary>
    public decimal DeMasSiSePagaComoSeEnvio => Math.Max(0m, QuedaTalComoSeEnvio - QuedaEnLaCuota);
}

/// <summary>
/// 2026-11-27 (feature 028, E2, T066): lo que se recoge con un tratamiento de la cuota causada: el tratamiento efectivo, el valor y su desglose, la cuota
/// causada como se muestra, la cuota que se deja y su valor (nulos si se incluye), el interés explicado y el texto de la cuota en la recogida. (nuevo)
/// </summary>
public sealed record RecogidaSegunCuota(
    InstallmentTreatment Tratamiento,
    decimal Valor,
    DesgloseDeRecogida Desglose,
    CuotaCausadaDeRecogida? Cuota,
    int? CuotaDejada,
    decimal? ValorDejado,
    IReadOnlyList<RenglonDeIntereses> ExplicacionDelInteres,
    string? ExplicacionDeLaCuota);

/// <summary>
/// <b>Un solo sitio liquida una recogida</b> (feature 028, R3, T010; FR-003, FR-024): la consulta de productos (la proyección a D con R), la vista previa
/// y el comando del desembolso dicen lo mismo al peso, y lo mismo que aplica el contrato. Sale de la reconstrucción (<see cref="CargadorDeReconstruccion"/>
/// + <see cref="MotorDeCartera.Reconstruir"/>) a D con lo conocido hasta R —una D futura se proyecta sin pagos con la última usura conocida, como la
/// liquidación del crédito—, aplica el modo de intereses de la recogida (<see cref="InteresesAlCancelar.ModoAlRecogerAsync"/>, <c>Recogida.InteresesProporcionales</c>
/// con las excepciones legales) y el período calendario de la línea, y cobra lo mismo que <see cref="AplicadorDePagos.ValorParaCancelar"/>, que es lo que
/// reparte el <c>FullPayoff</c> de la recogida (T050). Un crédito sin condiciones selladas cae a su última posición diaria o, sin ella, a su libro, como
/// <c>MotorDeCreditoEnDesembolso</c>. No audita ni asegura el alcance: eso lo hace quien llama. No escribe. La vigila
/// <c>LaRecogidaSeLiquidaEnUnSoloSitio</c>. (nuevo)
/// </summary>
public sealed class LiquidacionDeRecogida(
    IApplicationDbContext db,
    CargadorDeReconstruccion cargador,
    InteresesAlCancelar intereses,
    IDateTimeService reloj,
    ReglasDeParametrosDeCartera parametros,
    CuotaEnviadaDelCredito cuotaEnviada)
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// La liquidación para recoger <paramref name="credito"/> a <paramref name="fecha"/> (D) con lo conocido hasta <paramref name="informacionHasta"/> (R; nulo,
    /// ahora). Falla sólo si no se puede liquidar: una regla del motor (<c>ReglaDeCarteraException</c>) o, con D ≤ hoy, la usura del día sin publicar
    /// (<c>NationalValues.Missing</c>, como la liquidación del crédito). Lo que impide recogerlo (mora, castigo, insolvencia, sin saldo, no activo) no es una
    /// falla: va en <see cref="LiquidacionDeCreditoRecogido.Bloqueos"/>.
    /// </summary>
    public async Task<Result<LiquidacionDeCreditoRecogido>> LiquidarCreditoAsync(Loan credito, DateOnly fecha, DateTime? informacionHasta, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credito);
        var r = informacionHasta is { } instante
            ? DateTime.SpecifyKind(instante.Kind == DateTimeKind.Local ? instante.ToUniversalTime() : instante, DateTimeKind.Utc)
            : (DateTime?)null;
        var hasta = r ?? reloj.UtcNow;
        var insolvencia = await db.InsolvencyProceedings.AsNoTracking()
            .AnyAsync(i => i.PersonId == credito.DebtorPersonId && i.Status == InsolvencyProceedingStatus.Negotiating && !i.IsDeleted, ct);

        var cargado = await cargador.CargarAsync(credito.Id, r, fecha, ct);
        if (cargado is { Listo: true })
        {
            EstadoDelCredito estado;
            try
            {
                estado = MotorDeCartera.Reconstruir(cargado.Entrada!, fecha);
            }
            catch (ReglaDeCarteraException ex)
            {
                return Result.Failure<LiquidacionDeCreditoRecogido>(ErroresDeGobierno.De(ex.Problema));
            }
            if (estado.Origen is not null)
            {
                if (fecha <= reloj.HoyLocal && cargado.Entrada!.Condiciones.UsuraRequerida && estado.PrimerDiaSinUsura is { } sinUsura && sinUsura <= fecha)
                    return Result.Failure<LiquidacionDeCreditoRecogido>(ErroresDeValoresNacionales.Faltante(CodigosDeValorNacional.Usura,
                        cargado.Series?.ClaveDeUsura ?? cargado.Pactadas.ModalityCode, sinUsura));
                return Result.Success(await DeLaReconstruccionAsync(credito, estado, hasta, insolvencia, ct));
            }
        }
        return Result.Success(await SinReconstruccionAsync(credito, fecha, r, hasta, insolvencia, ct));
    }

    /// <summary>La liquidación de la reconstrucción: el modo de la recogida, el período calendario y el desglose de lo que cobra el <c>FullPayoff</c>.</summary>
    private async Task<LiquidacionDeCreditoRecogido> DeLaReconstruccionAsync(Loan credito, EstadoDelCredito estado, DateTime hasta, bool insolvencia,
        CancellationToken ct)
    {
        var d = estado.Fecha;
        var proporcionales = await intereses.RecogidaConfiguradaAsync(d, credito.CreditLineId, ct);
        // Como la recogida por el contrato (AplicacionDePagos.ModoAsync): un crédito en UVR (vivienda) va con el corrido y sin período calendario.
        var modo = estado.EnUvr ? ModoDeInteresesAlCancelar.Corrido : await intereses.ModoAlRecogerAsync(credito, estado.RulesVersion, d, ct);
        var calendario = !estado.EnUvr && await intereses.PeriodoCalendarioAsync(d, credito.CreditLineId, ct);

        var valor = AplicadorDePagos.ValorParaCancelar(estado, modo.CuotaCompleta, calendario);
        var cobrado = modo.CuotaCompleta ? AplicadorDePagos.InteresDeLaCuotaCompleta(estado, calendario) : [];
        var devuelto = calendario ? AplicadorDePagos.InteresSinTramo(estado, d) : [];
        // 2026-11-27 (feature 028, E2, T066): Recogida.CuotaCausada con la línea de este crédito, todo menos la cuota causada y la cuota enviada.
        var cuotaCausada = await CuotaCausadaConfiguradaAsync(d, credito.CreditLineId, ct);
        // 2026-11-27 (feature 028, D13 ajustada por el dueño): la regla legal con que se deja la cuota (proporcional dentro del tope del prepago).
        var regla = SeRecogeCompleto(estado) ? ReglaDeCuotaDejada.TalComoSeEnvio : await intereses.CuotaDejadaAsync(credito, estado.RulesVersion, ct);
        var salvo = SeRecogeCompleto(estado) ? null : SalvoCuota(estado, modo, calendario, devuelto, regla);
        var enviada = await cuotaEnviada.DeAsync(CuotaEnviadaDelCredito.CuotaDelEstado(credito, estado), d, ct);
        decimal S(params LoanConcept[] conceptos) => conceptos.Sum(c => estado.Saldo(c));
        var desglose = new DesgloseDeRecogida(
            Capital: S(LoanConcept.Principal),
            InteresCorriente: S(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense),
            InteresDevuelto: devuelto.Sum(l => l.Monto),
            InteresCobradoPorProporcionales: cobrado.Sum(l => l.Monto),
            Mora: S(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
            Seguro: S(LoanConcept.Insurance),
            Administracion: S(LoanConcept.Administration),
            OtrosCargos: S(LoanConcept.CollectionFee, LoanConcept.OtherCharge),
            Anticipo: estado.Anticipo,
            // D7: la cancelación usa el anticipo del mismo crédito, nunca el saldo a favor; no se descuenta (va en los avisos).
            SaldoAFavor: 0m);

        var avisos = new List<string>();
        var explicacion = Explicacion(proporcionales, modo);
        if (explicacion is not null) avisos.Add(explicacion);
        // 2026-11-27 (feature 028, T051): lo cobrado por la cuota completa y lo devuelto por período calendario ya no van en los avisos: van, con sus días,
        // en la explicación del interés.
        var (renglones, liberado) = ExplicacionDeInteresesDeRecogida.Explicar(estado, modo, calendario, cobrado, devuelto);
        if (estado.SaldoAFavor > 0m)
            avisos.Add($"El crédito tiene un saldo a favor de {Pesos(estado.SaldoAFavor)} que la recogida no descuenta: queda a favor del deudor y se "
                + "resuelve aparte.");
        if (estado.EnUvr)
            avisos.Add($"Crédito en UVR: se recoge en pesos a la UVR del {d:dd/MM/yyyy}, con el interés corrido (Ley 546 de 1999).");
        if (estado.ValidaHasta is { } validaHasta)
            avisos.Add($"La liquidación usa la última usura conocida desde el {validaHasta.AddDays(1):dd/MM/yyyy}: vale hasta el {validaHasta:dd/MM/yyyy}.");
        if (estado.Suspendida) avisos.Add("La causación está suspendida: los intereses van a suspenso.");

        var huella = "sha256:" + HuellaDeCartera.DeTexto(estado.Huella + "|" + new HuellaDeCartera()
            .Texto("fuente", nameof(FuenteDeLaLiquidacion.Reconstruccion))
            .Texto("intereses", modo.Efectivo)
            .Texto("motivo", modo.Motivo)
            .Texto("proporcionales", proporcionales)
            .Logico("periodoCalendario", calendario)
            .Decimal("valor", valor)
            .Texto("cuotaCausada", cuotaCausada)
            .Si(salvo is not null, h => h.Entero("cuotaDejada", salvo!.Cuota).Decimal("valorSalvoCuota", salvo.ValorARecoger)
                .Decimal("quedaEnLaCuota", salvo.QuedaEnLaCuota)
                // 2026-11-27 (feature 028, D13 ajustada): la regla proporcional, sólo si la hay (las huellas de antes no cambian).
                .Si(salvo.Regla.Proporcional, x => x.Texto("reglaDeLaCuotaDejada", salvo.Regla.Regla).Texto("motivoDeLaCuotaDejada", salvo.Regla.Motivo)))
            .Si(enviada is not null, h => h.Enumeracion("envio", (SentInstallmentState?)enviada!.Estado).Entero("archivo", enviada.ArchivoId))
            .Canonico());

        return new LiquidacionDeCreditoRecogido
        {
            LoanId = credito.Id, LoanPublicId = credito.PublicId, Number = credito.Number, Fecha = d, InformacionHasta = hasta,
            ValorARecoger = valor, Desglose = desglose, Intereses = modo, Proporcionales = proporcionales, ExplicacionDeIntereses = explicacion,
            PeriodoCalendario = calendario, DiasDeMora = estado.AlturaDeMora.Dias,
            Bloqueos = Bloqueos(credito, insolvencia, estado.AlturaDeMora.Dias, valor),
            CuotaCausada = CuotaCausada(estado, calendario), SaldoAFavorSinDescontar = Math.Max(0m, estado.SaldoAFavor),
            Huella = huella, Avisos = avisos, ExplicacionDelInteres = renglones, ReconocidoQueSeLibera = liberado,
            Fuente = FuenteDeLaLiquidacion.Reconstruccion,
            // 2026-11-27 (feature 028, T062): un crédito de vivienda se recoge completo (el motor aplica así la cancelación salvo cuota, con su aviso).
            // 2026-11-27 (feature 028, E2, T066): tampoco se deja si a D no hay cuota causada (D es un vencimiento, gracia, el tramo no empezó).
            AdmiteDejarLaCuota = salvo is not null,
            RecogidoCompletoPorque = SeRecogeCompleto(estado) ? ErroresDeRecogida.MotivoViviendaCompleta
                : salvo is null ? ErroresDeRecogida.MotivoSinCuotaQueDejar : null,
            CuotaCausadaConfigurada = cuotaCausada,
            SalvoCuota = salvo,
            CuotaEnviada = enviada,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; HU4, D1): <c>Recogida.CuotaCausada</c> con la línea del crédito que se recoge (cae a la general); ilegible o vacío,
    /// «Incluir» (lo de siempre). (nuevo)
    /// </summary>
    public async Task<string> CuotaCausadaConfiguradaAsync(DateOnly fecha, int lineaId, CancellationToken ct)
    {
        var leido = await parametros.LeerAsync(ParametrosDeCartera.RecogidaCuotaCausada, fecha, null, ParameterScopeKind.CreditLine, lineaId, ct);
        return (leido.IsSuccess ? leido.Value.Texto?.Trim() : null) switch
        {
            ParametrosDeCartera.CuotaExcluir => ParametrosDeCartera.CuotaExcluir,
            ParametrosDeCartera.CuotaPreguntar => ParametrosDeCartera.CuotaPreguntar,
            _ => ParametrosDeCartera.CuotaIncluir,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, E2, T066; FR-019): lo que cuesta todo menos la cuota causada, sin aplicarlo, con el modo de intereses de la recogida
    /// (<see cref="AplicadorDePagos.ValorParaCancelarSalvoCuota"/>: no se toca el motor). Nulo si no hay cuota causada que dejar. El desglose es el de cada
    /// concepto sin lo de la cuota dejada (el capital de las siguientes, por cuota, como la cancelación), lo devuelto por período calendario de las demás y
    /// —lo que cierra al peso con lo que cobra el contrato— el anticipo del crédito que se usa. (nuevo)
    /// </summary>
    /// <remarks>2026-11-27 (feature 028, D13 ajustada): con la regla legal de la cuota dejada (<see cref="InteresesAlCancelar.CuotaDejadaAsync"/>): lo que se
    /// recoge no cambia; lo que queda en la cuota sí, y se guarda también lo que debería tal como se envió.</remarks>
    private static RecogidaSalvoCuota? SalvoCuota(EstadoDelCredito estado, ModoDeInteresesAlCancelar modo, bool calendario,
        IReadOnlyList<LineaDeDistribucion> devuelto, ReglaDeCuotaDejada regla)
    {
        var v = AplicadorDePagos.ValorParaCancelarSalvoCuota(estado, null, modo.CuotaCompleta, calendario, regla.Proporcional);
        if (v.Cuota is not { } n || estado.Plan is not { } plan) return null;
        decimal S(params LoanConcept[] conceptos) => conceptos.Sum(c => estado.Saldo(c) - estado.Saldo(c, n));
        var desglose = new DesgloseDeRecogida(
            Capital: S(LoanConcept.Principal),
            InteresCorriente: S(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense),
            InteresDevuelto: devuelto.Where(l => l.Cuota != n).Sum(l => l.Monto),
            Mora: S(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
            Seguro: S(LoanConcept.Insurance),
            Administracion: S(LoanConcept.Administration),
            OtrosCargos: S(LoanConcept.CollectionFee, LoanConcept.OtherCharge));
        // Lo que el contrato no cobra de los conceptos es el anticipo del mismo crédito que usa: así el total es, al peso, lo que aplica.
        var anticipo = desglose.Total - v.ParaCancelar;
        desglose = anticipo >= 0m ? desglose with { Anticipo = anticipo } : desglose with { OtrosCargos = desglose.OtrosCargos - anticipo };
        var vence = plan.Cuotas.First(c => c.Numero == n).Vencimiento;
        return new RecogidaSalvoCuota(n, vence, v.ParaCancelar, desglose, v.QuedaEnLaCuota)
        {
            Regla = regla, QuedaTalComoSeEnvio = v.QuedaTalComoSeEnvio,
        };
    }

    /// <summary>
    /// Un crédito sin condiciones selladas (o sin poder armar su reconstrucción): su última posición diaria hasta D o, sin ella, lo que dice su libro, sin
    /// anticipos ni saldos a favor y sin mora —lo mismo que <c>MotorDeCreditoEnDesembolso</c>—. Sin modo de intereses ni cuota causada. Con R, sólo lo
    /// registrado hasta R.
    /// </summary>
    private async Task<LiquidacionDeCreditoRecogido> SinReconstruccionAsync(Loan credito, DateOnly fecha, DateTime? r, DateTime hasta, bool insolvencia,
        CancellationToken ct)
    {
        var proporcionales = await intereses.RecogidaConfiguradaAsync(fecha, credito.CreditLineId, ct);
        // 2026-11-27 (feature 028, E2, T066): el parámetro y la cuota enviada se informan aunque la cuota no se pueda dejar.
        var cuotaCausada = await CuotaCausadaConfiguradaAsync(fecha, credito.CreditLineId, ct);
        var enviada = await cuotaEnviada.DeAsync(new CuotaPorConsultar(credito.Id, credito.PublicId, null, fecha), fecha, ct);
        var posiciones = db.LoanDailyPositions.AsNoTracking().Where(p => p.LoanId == credito.Id && p.PositionDate <= fecha);
        if (r is { } corte) posiciones = posiciones.Where(p => p.RegisteredAt <= corte);
        var posicion = await posiciones.OrderByDescending(p => p.PositionDate).ThenByDescending(p => p.Version).FirstOrDefaultAsync(ct);

        DesgloseDeRecogida desglose;
        decimal valor;
        int dias;
        FuenteDeLaLiquidacion fuente;
        string huella;
        var avisos = new List<string>();
        if (posicion is not null)
        {
            desglose = new DesgloseDeRecogida(
                Capital: posicion.Principal,
                InteresCorriente: posicion.CurrentInterest + posicion.CurrentInterestInSuspense,
                Mora: posicion.DefaultInterest + posicion.DefaultInterestInSuspense,
                Seguro: posicion.Insurance,
                Administracion: posicion.Administration,
                OtrosCargos: posicion.CollectionFee + posicion.OtherCharge,
                Anticipo: posicion.Advance);
            valor = posicion.PayoffAmount;
            // Lo que la posición no desglosa (una posición de antes de que llevara todos los conceptos) va en otros cargos: el total es el de la posición.
            var sinDesglosar = valor - desglose.Total;
            if (sinDesglosar != 0m) desglose = desglose with { OtrosCargos = desglose.OtrosCargos + sinDesglosar };
            dias = posicion.DaysPastDue;
            fuente = FuenteDeLaLiquidacion.PosicionDiaria;
            avisos.Add($"El crédito no tiene condiciones selladas: la liquidación sale de su posición del {posicion.PositionDate:dd/MM/yyyy}, sin intereses "
                + "proporcionales.");
            if (posicion.CreditBalance > 0m)
                avisos.Add($"El crédito tiene un saldo a favor de {Pesos(posicion.CreditBalance)} que la recogida no descuenta: queda a favor del deudor y se "
                    + "resuelve aparte.");
            huella = new HuellaDeCartera()
                .Texto("fuente", nameof(FuenteDeLaLiquidacion.PosicionDiaria))
                .Fecha("fecha", posicion.PositionDate)
                .Entero("version", posicion.Version)
                .Texto("insumos", posicion.InputsSha256)
                .Decimal("valor", valor)
                .Canonico();
        }
        else
        {
            var filas = db.LoanTransactions.AsNoTracking()
                .Where(t => t.LoanId == credito.Id && t.ValueDate <= fecha && !t.InControl
                            && t.Concept != LoanConcept.Advance && t.Concept != LoanConcept.CreditBalance
                            // 2026-11-16 (feature 024, fase 4): lo reconocido al inicio del ciclo no es deuda.
                            && t.Concept < LoanConcept.RecognizedInterest);
            if (r is { } corteDelLibro) filas = filas.Where(t => t.RegisteredAt <= corteDelLibro);
            var porConcepto = (await filas.GroupBy(t => t.Concept).Select(g => new { Concepto = g.Key, Total = g.Sum(t => t.Amount) }).ToListAsync(ct))
                .ToDictionary(x => x.Concepto, x => x.Total);
            decimal S(params LoanConcept[] conceptos) => conceptos.Sum(c => porConcepto.GetValueOrDefault(c));
            desglose = new DesgloseDeRecogida(
                Capital: S(LoanConcept.Principal),
                InteresCorriente: S(LoanConcept.CurrentInterest, LoanConcept.CurrentInterestInSuspense),
                Mora: S(LoanConcept.DefaultInterest, LoanConcept.DefaultInterestInSuspense),
                Seguro: S(LoanConcept.Insurance),
                Administracion: S(LoanConcept.Administration),
                OtrosCargos: S(LoanConcept.CollectionFee, LoanConcept.OtherCharge));
            valor = Math.Max(0m, porConcepto.Values.Sum());
            dias = 0;
            fuente = FuenteDeLaLiquidacion.Libro;
            avisos.Add("El crédito no tiene condiciones selladas ni posición diaria: la liquidación sale de su libro, sin intereses proporcionales ni mora.");
            huella = new HuellaDeCartera()
                .Texto("fuente", nameof(FuenteDeLaLiquidacion.Libro))
                .Lista("conceptos", porConcepto.OrderBy(x => x.Key), (h, x) => h.Enumeracion("concepto", (LoanConcept?)x.Key).Decimal("total", x.Value))
                .Decimal("valor", valor)
                .Canonico();
        }

        return new LiquidacionDeCreditoRecogido
        {
            LoanId = credito.Id, LoanPublicId = credito.PublicId, Number = credito.Number, Fecha = fecha, InformacionHasta = hasta,
            ValorARecoger = valor, Desglose = desglose, Intereses = ModoDeInteresesAlCancelar.Corrido, Proporcionales = proporcionales,
            DiasDeMora = dias, Bloqueos = Bloqueos(credito, insolvencia, dias, valor), SaldoAFavorSinDescontar = Math.Max(0m, posicion?.CreditBalance ?? 0m),
            Huella = "sha256:" + HuellaDeCartera.DeTexto(huella), Avisos = avisos, Fuente = fuente,
            // 2026-11-27 (feature 028, T062): sin reconstrucción el contrato no tiene con qué dejar la cuota: se recoge completo.
            AdmiteDejarLaCuota = false,
            RecogidoCompletoPorque = ErroresDeRecogida.MotivoSinReconstruccionCompleta,
            CuotaCausadaConfigurada = cuotaCausada,
            CuotaEnviada = enviada,
        };
    }

    /// <summary>
    /// 2026-11-27 (feature 028, T062): ¿se recoge completo aunque se pida dejar la cuota causada? Un crédito de vivienda —en UVR o con las reglas de vivienda en
    /// pesos—: dejar la cuota con el interés del período sobre todo el saldo cobraría interés del capital que se prepaga (Ley 546 de 1999, sin penalidad).
    /// La misma regla que el motor (<see cref="AplicadorDePagos.AvisoViviendaSeCancelaCompleto"/>). (nuevo)
    /// </summary>
    public static bool SeRecogeCompleto(EstadoDelCredito estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        return estado.EnUvr || VersionesDeReglas.EsDeVivienda(estado.RulesVersion);
    }

    /// <summary>Lo que impide recogerlo, en este orden: castigado o no activo, insolvencia que negocia, mora (reestructuración) y sin saldo.</summary>
    private static List<BloqueoDeRecogida> Bloqueos(Loan credito, bool insolvencia, int diasDeMora, decimal valor)
    {
        var bloqueos = new List<BloqueoDeRecogida>();
        if (credito.Status == LoanStatus.WrittenOff) bloqueos.Add(ErroresDeRecogida.Castigado(credito.Number));
        else if (credito.Status != LoanStatus.Active) bloqueos.Add(ErroresDeRecogida.NoActivo(credito.Number, credito.Status));
        if (insolvencia) bloqueos.Add(ErroresDeRecogida.EnInsolvencia(credito.Number));
        if (diasDeMora > 0) bloqueos.Add(ErroresDeRecogida.EnMora(credito.Number, diasDeMora));
        if (valor <= 0m && credito.Status == LoanStatus.Active) bloqueos.Add(ErroresDeRecogida.SinSaldo(credito.Number));
        return bloqueos;
    }

    /// <summary>
    /// La cuota del período en curso a la fecha del estado —la próxima que vence o, por período calendario, la que vence en el tramo en curso (la misma que
    /// la caja ofrece como cuota del período)— con lo que le falta por pagar al vencer (su valor menos lo anticipado).
    /// </summary>
    private static CuotaCausadaDeRecogida? CuotaCausada(EstadoDelCredito estado, bool calendario)
    {
        if (estado.Plan is not { } plan) return null;
        var delTramo = calendario ? AplicadorDePagos.CuotasDelTramoEnCurso(plan, estado.Fecha) : null;
        var cuota = delTramo is null
            ? plan.Cuotas.FirstOrDefault(c => c.Vencimiento > estado.Fecha)
            : plan.Cuotas.FirstOrDefault(c => delTramo.Contains(c.Numero));
        return cuota is null ? null : new CuotaCausadaDeRecogida(cuota.Numero, AplicadorDePagos.ValorParaAnticipar(estado, cuota.Numero), cuota.Vencimiento);
    }

    /// <summary>El modo de intereses en palabras: el motivo legal si lo hay; si no, lo que pidió la recogida.</summary>
    private static string? Explicacion(string proporcionales, ModoDeInteresesAlCancelar modo) =>
        modo.Explicacion ?? proporcionales switch
        {
            ParametrosDeCartera.ProporcionalesSi =>
                "Se cobra el interés corrido hasta la fecha del desembolso: la cooperativa cobra o devuelve los intereses proporcionales al recoger.",
            ParametrosDeCartera.ProporcionalesNo when modo.CuotaCompleta =>
                "Se cobra completo el interés de la cuota en curso: la cooperativa no hace proporcionales al recoger.",
            _ when modo.CuotaCompleta => "Se cobra completo el interés de la cuota en curso, como en una cancelación en caja.",
            _ => null,
        };

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", Colombia);
}

/// <summary>Las clases de un renglón de la explicación del interés de una recogida (feature 028, T051; api.md §1 <c>interestExplanation[].kind</c>). (nuevo)</summary>
public static class ClasesDeRenglonDeInteres
{
    /// <summary>Se cobra el interés causado de la cuota en curso hasta D.</summary>
    public const string Corrido = "Accrued";

    /// <summary>Se cobra además el interés de los días que faltan de la cuota en curso: la cuota completa.</summary>
    public const string CuotaCompleta = "FullInstallment";

    /// <summary>Se pidió la cuota completa y la ley obliga a cobrar sólo el proporcional (vivienda, tope del prepago).</summary>
    public const string Proporcional = "Proportional";

    /// <summary>Por período calendario se devuelve lo ya causado de una cuota cuyo período no ha empezado.</summary>
    public const string Devuelto = "Returned";

    /// <summary>Lo reconocido al inicio del ciclo y no causado a D: se libera, no es deuda (informativo).</summary>
    public const string ReconocidoQueSeLibera = "RecognizedReleased";
}

/// <summary>
/// Un renglón de la explicación del interés de una recogida (feature 028, T051; HU5): qué se cobra o se devuelve, de qué cuota, cuántos días —los de la
/// convención del plan, entre las fechas de la reconstrucción— y cuánto, con su texto para la persona. (nuevo)
/// </summary>
public sealed record RenglonDeIntereses(string Clase, int? Cuota, int? Dias, decimal Valor, string Texto);

/// <summary>
/// La explicación del interés de una recogida (feature 028, T051; HU5, FR-024), renglón a renglón, a partir del estado reconstruido a D y de lo que la
/// liquidación ya decidió —el modo de intereses, el período calendario, lo que cobra la cuota completa y lo que se devuelve—: no recalcula ningún valor, sólo
/// los explica con los días del plan (<see cref="ContadorDeDias"/> con la convención sellada; el período de una cuota va de su <c>Desde</c> excluido a su
/// vencimiento incluido). Lo reconocido al inicio del ciclo y no causado se informa aparte y no cambia lo que se recoge. Un crédito en UVR no se explica aquí:
/// va con el corrido y su aviso. Puro. (nuevo)
/// </summary>
public static class ExplicacionDeInteresesDeRecogida
{
    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>Los renglones y lo reconocido que se libera.</summary>
    /// <param name="estado">El estado reconstruido a D.</param>
    /// <param name="modo">El modo de intereses de la recogida, con su motivo legal si lo hay.</param>
    /// <param name="periodoCalendario">La línea causa por período calendario a D.</param>
    /// <param name="cobrado">Lo que agrega la cuota completa (<see cref="AplicadorDePagos.InteresDeLaCuotaCompleta"/>); vacío con el corrido.</param>
    /// <param name="devuelto">Lo causado de las cuotas cuyo período no ha empezado (<see cref="AplicadorDePagos.InteresSinTramo"/>).</param>
    public static (IReadOnlyList<RenglonDeIntereses> Renglones, decimal ReconocidoQueSeLibera) Explicar(EstadoDelCredito estado,
        ModoDeInteresesAlCancelar modo, bool periodoCalendario, IReadOnlyList<LineaDeDistribucion> cobrado, IReadOnlyList<LineaDeDistribucion> devuelto)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(modo);
        var renglones = new List<RenglonDeIntereses>();
        if (estado.Plan is not { } plan || estado.EnUvr) return (renglones, 0m);
        var d = estado.Fecha;
        var cuotas = plan.Cuotas.GroupBy(c => c.Numero).ToDictionary(g => g.Key, g => g.First());
        int Dias(DateOnly desde, DateOnly hasta) => hasta <= desde ? 0 : ContadorDeDias.Dias(plan.Entrada.Convencion, desde, hasta);

        var devueltas = devuelto.Select(l => l.Cuota).OfType<int>().ToHashSet();
        var enCurso = plan.Cuotas.Where(c => c.Vencimiento > d).OrderBy(c => c.Vencimiento).FirstOrDefault();

        // La cuota en curso: lo corrido hasta D y, si se cobra completa, los días que le faltan; o el proporcional por la ley.
        if (enCurso is not null && !devueltas.Contains(enCurso.Numero))
        {
            var n = enCurso.Numero;
            var corrido = estado.Saldo(LoanConcept.CurrentInterest, n) + estado.Saldo(LoanConcept.CurrentInterestInSuspense, n);
            var diasCorridos = Dias(enCurso.Desde, d);
            if (corrido > 0m && diasCorridos > 0)
                renglones.Add(new(ClasesDeRenglonDeInteres.Corrido, n, diasCorridos, corrido,
                    $"Se cobra el interés de {DiasEnTexto(diasCorridos)} de la cuota {n} hasta la fecha del desembolso (del "
                    + $"{enCurso.Desde.AddDays(1):dd/MM/yyyy} al {d:dd/MM/yyyy}): {Pesos(corrido)}."));
        }
        foreach (var g in cobrado.Where(l => l.Cuota is not null).GroupBy(l => l.Cuota!.Value).OrderBy(g => g.Key))
        {
            var valor = g.Sum(l => l.Monto);
            if (valor <= 0m || !cuotas.TryGetValue(g.Key, out var q)) continue;
            var faltan = Dias(d, q.Vencimiento);
            renglones.Add(new(ClasesDeRenglonDeInteres.CuotaCompleta, g.Key, faltan, valor,
                $"Se cobra el interés completo de la cuota {g.Key}: también los {DiasEnTexto(faltan)} que faltan hasta su vencimiento, el "
                + $"{q.Vencimiento:dd/MM/yyyy}, que todavía no se han causado: {Pesos(valor)}."));
        }
        if (modo.Motivo is { } motivo && RazonLegal(motivo) is { } razon)
        {
            // Lo que la cuota completa habría cobrado y la ley no deja cobrar.
            var noCobrado = AplicadorDePagos.InteresDeLaCuotaCompleta(estado, periodoCalendario);
            var valor = noCobrado.Sum(l => l.Monto);
            var cuota = noCobrado.Select(l => l.Cuota).OfType<int>().FirstOrDefault();
            var faltan = cuotas.TryGetValue(cuota, out var q) ? Dias(d, q.Vencimiento) : (int?)null;
            renglones.Add(new(ClasesDeRenglonDeInteres.Proporcional, valor > 0m ? cuota : null, valor > 0m ? faltan : null, valor,
                valor > 0m && faltan is { } dias
                    ? $"Se cobra el proporcional: {razon}. No se cobran los {DiasEnTexto(dias)} que faltan de la cuota {cuota} ({Pesos(valor)})."
                    : $"Se cobra el proporcional: {razon}."));
        }

        // Lo devuelto por período calendario: lo causado de una cuota cuyo período no ha empezado.
        foreach (var g in devuelto.Where(l => l.Cuota is not null).GroupBy(l => l.Cuota!.Value).OrderBy(g => g.Key))
        {
            var valor = g.Sum(l => l.Monto);
            if (valor <= 0m || !cuotas.TryGetValue(g.Key, out var q)) continue;
            var dias = Dias(q.Desde, d);
            var empieza = PeriodoDeCausacion.TramoQueContiene(q.Vencimiento, plan.Entrada.Periodicidad).Desde;
            renglones.Add(new(ClasesDeRenglonDeInteres.Devuelto, g.Key, dias, valor,
                $"Se devuelve el interés de {DiasEnTexto(dias)} no corridos de la cuota {g.Key} ({Pesos(valor)}): ya estaba causado, pero la cuota vence el "
                + $"{q.Vencimiento:dd/MM/yyyy} y su período no empieza hasta el {empieza:dd/MM/yyyy} (período calendario)."));
        }

        // Lo reconocido al inicio del ciclo y no causado a D: se libera (el interés de una cuota cobrada completa no, porque la cuota completa lo consume).
        var completas = cobrado.Select(l => l.Cuota).OfType<int>().ToHashSet();
        var liberado = 0m;
        foreach (var g in estado.Reconocidos.Where(r => r.Pendiente > 0m && !(r.Rubro == LoanConcept.CurrentInterest && completas.Contains(r.Cuota)))
                     .GroupBy(r => r.Cuota).OrderBy(g => g.Key))
        {
            var valor = g.Sum(r => r.Pendiente);
            liberado += valor;
            var dias = cuotas.TryGetValue(g.Key, out var q) ? Dias(d, q.Vencimiento) : (int?)null;
            renglones.Add(new(ClasesDeRenglonDeInteres.ReconocidoQueSeLibera, g.Key, dias, valor,
                $"Reconocido que se libera: {Pesos(valor)} de la cuota {g.Key} que se reconoció al inicio del ciclo y no se ha causado"
                + (dias is { } n && q is not null ? $" ({DiasEnTexto(n)}, hasta el {q.Vencimiento:dd/MM/yyyy})" : string.Empty)
                + ". No es deuda del asociado: no cambia lo que se recoge."));
        }
        return (renglones, liberado);
    }

    /// <summary>Por qué la ley obliga al proporcional aunque se pidió la cuota completa; nula si el motivo no es legal.</summary>
    public static string? RazonLegal(string motivo) => motivo switch
    {
        InteresesAlCancelar.MotivoVivienda => "el crédito es de vivienda y se prepaga sin penalidad (Ley 546 de 1999)",
        InteresesAlCancelar.MotivoDentroDelTope => "el crédito está dentro del tope legal del prepago sin penalidad (Ley 1555 de 2012)",
        InteresesAlCancelar.MotivoTopeDesconocido =>
            "faltan el salario mínimo o el tope del prepago a la fecha del desembolso para saber si el crédito está fuera del tope legal",
        _ => null,
    };

    private static string DiasEnTexto(int dias) => dias == 1 ? "1 día" : $"{dias} días";

    private static string Pesos(decimal valor) => "$" + valor.ToString("#,##0.##", Colombia);
}
