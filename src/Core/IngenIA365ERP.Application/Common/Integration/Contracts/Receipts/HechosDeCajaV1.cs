using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;

namespace IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;

// Feature 015, US2 (T0091; FR-004, FR-140, FR-141; contracts/contabilidad.md §2, §3): la forma común de los hechos REC → Accounting y los cinco de la caja.
// Ningún hecho lleva una cuenta contable: dimensiones por código y roles; la cuenta la decide la matriz (LosHechosDeRecaudoYTesoreriaNoLlevanCuentas).

/// <summary>Las dimensiones de un hecho de Recaudo, por código (contabilidad.md §2). (nuevo)</summary>
public sealed record ReceiptsFactDimensionsV1
{
    public string BranchCode { get; init; } = string.Empty;

    public string? CollectionPointCode { get; init; }

    public string? CashBoxCode { get; init; }

    public Guid? OwnBankAccountPublicId { get; init; }

    public string? OwnBankAccountCode { get; init; }

    public string? MeansCode { get; init; }

    /// <summary><c>ClaseDeMedioDeRecaudo</c> como número.</summary>
    public int? MeansClass { get; init; }

    public string? MiscConceptCode { get; init; }

    public string? TargetModule { get; init; }

    public string? TargetNature { get; init; }

    public string? CollectorClass { get; init; }

    public string? Channel { get; init; }
}

/// <summary>Una línea de un hecho: rol, lado, valor, tercero y cruce donde la cuenta lo exija, y sus dimensiones (contabilidad.md §2). (nuevo)</summary>
public sealed record FactLineV1
{
    /// <summary>Uno de los roles de contabilidad.md §5 (<c>Caja</c>, <c>Boveda</c>, <c>CuentaPorCobrarCajero</c>…).</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary><c>Debit</c> o <c>Credit</c>.</summary>
    public string Side { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public Guid? ThirdPartyPersonPublicId { get; init; }

    public string? CrossDocument { get; init; }

    public ReceiptsFactDimensionsV1 Dimensions { get; init; } = new();
}

/// <summary>Los roles y lados que usan los hechos de la caja (contabilidad.md §5). (nuevo)</summary>
public static class RolesDeRecaudo
{
    public const string Debito = "Debit";
    public const string Credito = "Credit";

    public const string Caja = "Caja";
    public const string Boveda = "Boveda";
    public const string EfectivoEnTransito = "EfectivoEnTransito";
    public const string BancoRecaudo = "BancoRecaudo";
    public const string ChequesEnCanje = "ChequesEnCanje";
    public const string MedioPorCobrar = "MedioPorCobrar";
    public const string CuentaPorCobrarCajero = "CuentaPorCobrarCajero";
    public const string GastoFaltante = "GastoFaltante";
    public const string SobrantesDeCaja = "SobrantesDeCaja";
    public const string IngresoSobrantes = "IngresoSobrantes";
    public const string PuenteDelModulo = "PuenteDelModulo";
    public const string ConceptoVario = "ConceptoVario";

    // US5 (fase 10; contabilidad.md §3): el saldo a favor de los pagadores y el puente de la devolución por orden de pago a Tesorería.
    public const string SaldoAFavorDePagadores = "SaldoAFavorDePagadores";
    public const string PuenteDeDevolucion = "PuenteDeDevolucion";

    // US6, US7, US15, US14 (fases 11 a 15; contabilidad.md §5): las partidas, las pagadurías, el cheque devuelto y la custodia por listas.
    public const string PartidasPorIdentificar = "PartidasPorIdentificar";
    public const string SaldoDePagadurias = "SaldoDePagadurias";
    public const string CuentaPorCobrarPagador = "CuentaPorCobrarPagador";
    public const string CustodiaPorLista = "CustodiaPorLista";

    // R1 (fases 25 a 30; contabilidad.md §5): el recaudo pendiente de aplicar, las cuentas por cobrar comerciales y el GMF de los egresos de caja.
    public const string RecaudoPendienteDeAplicar = "RecaudoPendienteDeAplicar";
    public const string CuentasPorCobrarComerciales = "CuentasPorCobrarComerciales";
    public const string GmfGasto = "GmfGasto";
    public const string GmfPorPagar = "GmfPorPagar";
}

/// <summary>
/// <c>MovimientoDeCajaDeRecaudoRegistrado</c> v1 (<c>MCR</c>; el tipo no es <c>MovimientoDeCajaRegistrado</c>, que es de Comercial, feature 012): un movimiento de caja o de bóveda —abastecer, retirar a bóveda, trasladar entre cajas— con sus
/// líneas <c>Caja</c> ↔ <c>Boveda</c> ↔ <c>Caja</c>. La base de apertura no emite (no mueve dinero entre cuentas). (nuevo)
/// </summary>
public sealed record MovimientoDeCajaRegistradoV1
{
    public const string Type = "MovimientoDeCajaDeRecaudoRegistrado";

    public Guid MovementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    /// <summary>El nombre de <c>CashMovementKindR</c> o de <c>VaultMovementKind</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public Guid SessionPublicId { get; init; }

    public Guid? CounterpartSessionPublicId { get; init; }

    public Guid? VaultPublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>MedioReclasificado</c> v1 (<c>MRC</c>): lo esperado de un medio pasa a otro sin tocar el recibo (US2-7): rol del medio de origen ↔ rol del de destino.
/// (nuevo)
/// </summary>
public sealed record MedioReclasificadoV1
{
    public const string Type = "MedioReclasificado";

    public Guid MovementPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid SessionPublicId { get; init; }

    public Guid? ReceiptPublicId { get; init; }

    public string FromMeansCode { get; init; } = string.Empty;

    public string ToMeansCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid? ApprovalRequestPublicId { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary>
/// <c>DiferenciaDeArqueoDeRecaudoAprobada</c> v1 (<c>DAA</c>; el tipo no es <c>DiferenciaDeArqueoAprobada</c>, que es de Comercial, feature 012): la diferencia de una línea de arqueo, aprobada o dentro de la tolerancia con motivo. Faltante:
/// <c>CuentaPorCobrarCajero</c> (tercero = el cajero) o <c>GastoFaltante</c> → <c>Caja</c>; sobrante: <c>Caja</c> → <c>SobrantesDeCaja</c>. (nuevo)
/// </summary>
public sealed record DiferenciaDeArqueoAprobadaV1
{
    public const string Type = "DiferenciaDeArqueoDeRecaudoAprobada";

    public Guid CountLinePublicId { get; init; }

    public Guid SessionPublicId { get; init; }

    public string MeansCode { get; init; } = string.Empty;

    public decimal Expected { get; init; }

    public decimal Counted { get; init; }

    /// <summary>Con signo: contado − esperado.</summary>
    public decimal Difference { get; init; }

    /// <summary>El nombre de <c>CountTreatment</c>.</summary>
    public string Treatment { get; init; } = string.Empty;

    public Guid? CashierPersonPublicId { get; init; }

    public DateOnly OperationDate { get; init; }

    public string? Reason { get; init; }

    public Guid? ApprovalRequestPublicId { get; init; }

    public UserRefV1 ApprovedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary><c>AjusteDeSesionRegistrado</c> v1 (<c>ASR</c>): el ajuste aprobado de una sesión cerrada, enlazado con su sesión (US2-5). (nuevo)</summary>
public sealed record AjusteDeSesionRegistradoV1
{
    public const string Type = "AjusteDeSesionRegistrado";

    public Guid AdjustmentPublicId { get; init; }

    public string Number { get; init; } = string.Empty;

    public Guid SessionPublicId { get; init; }

    /// <summary>El nombre de <c>SessionAdjustmentKind</c>.</summary>
    public string Kind { get; init; } = string.Empty;

    public string MeansCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public string Reason { get; init; } = string.Empty;

    public Guid ApprovalRequestPublicId { get; init; }

    /// <summary>El hecho de la diferencia que corrige, si lo hubo.</summary>
    public OriginalFactRefV1? Original { get; init; }

    public UserRefV1 RegisteredBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}

/// <summary><c>SobranteLlevadoAIngreso</c> v1 (<c>SLI</c>): un sobrante vencido pasa a ingreso con aprobación: <c>SobrantesDeCaja</c> → <c>IngresoSobrantes</c>. (nuevo)</summary>
public sealed record SobranteLlevadoAIngresoV1
{
    public const string Type = "SobranteLlevadoAIngreso";

    public Guid SurplusPublicId { get; init; }

    public Guid CountLinePublicId { get; init; }

    public decimal Amount { get; init; }

    public DateOnly OperationDate { get; init; }

    public Guid? ApprovalRequestPublicId { get; init; }

    public UserRefV1 ApprovedBy { get; init; } = new();

    public IReadOnlyList<FactLineV1> Lines { get; init; } = [];
}
