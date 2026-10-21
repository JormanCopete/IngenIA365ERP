using System.Text.Json;

namespace IngenIA365ERP.Shared.Services.Cartera;

// Feature 013, US7 (T0663; contracts/api.md §16.1, §16.3): los hechos contables de Cartera, sus partes y la conciliación con Contabilidad vistos
// desde Cartera. Espejo de los DTOs de la API: el modo, el estado y la validación previa de un hecho llegan como texto (su nombre); la bandeja de
// emitidos reutiliza los DTOs de la plataforma de Inventario (BandejaDeMensajesDto y vecinos), que son los mismos. (nuevo)

/// <summary>El comprobante de un hecho tal como lo dejó la entrega (Cartera no lee las tablas de Contabilidad).</summary>
public sealed record ComprobanteDelHechoContableDto(string? Reference, string? VoucherTypeCode, string? VoucherNumber, DateTime? ProcessedAt);

/// <summary>La parte de hechos de la que salió un hecho resumido: su tramo y cuántos créditos reúne.</summary>
public sealed record ParteDelHechoContableDto(Guid PartPublicId, int? Tranche, int Loans);

/// <summary>Un hecho de Cartera a Contabilidad con su modo, su estado, su comprobante y, si salió de una parte, la parte.</summary>
public sealed record HechoContableDeCarteraDto(
    Guid MessagePublicId,
    string Type,
    DateOnly AccountingDate,
    Guid Branch,
    string? Mode,
    string? Status,
    string? Number,
    string? PrevalidationOutcome,
    decimal Totals,
    ParteDelHechoContableDto? Part,
    ComprobanteDelHechoContableDto? Voucher);

/// <summary>Un crédito de una parte: número, tercero y valores por concepto (<c>LoanConcept</c> por su nombre).</summary>
public sealed record CreditoDeLaParteDto(Guid LoanPublicId, string LoanNumber, string Debtor, IReadOnlyDictionary<string, decimal> ByConcept, decimal Total);

/// <summary>Una parte de hechos con sus totales por combinación (<c>TotalsByDimension</c>, tal como se emitió) y la lista por crédito.</summary>
public sealed record ParteDeHechosContablesDto(
    Guid PublicId,
    string FactType,
    string SourceKind,
    DateOnly ValueDate,
    DateOnly AccountingDate,
    int? BatchNumber,
    int LoanCount,
    decimal TotalAmount,
    JsonElement TotalsByDimension,
    string? PrevalidationOutcome,
    IReadOnlyList<Guid> Messages,
    PaginaDeCarteraDto<CreditoDeLaParteDto> Loans);

/// <summary>Los filtros de la lista de hechos; <c>Estado</c> es el nombre de <c>DeliveryStatus</c>.</summary>
public sealed record FiltroDeHechosContables(
    string? Tipo = null,
    string? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    Guid? Sucursal = null,
    Guid? Credito = null,
    int Pagina = 1,
    int TamanoDePagina = 50);

// ----------------------------------------------------------------------------------------------- conciliación --

/// <summary>Una combinación de dimensiones de un conjunto con su saldo en Cartera.</summary>
public sealed record CombinacionConciliadaDto(
    string Role,
    string? CreditLine,
    string? Modality,
    string? Subsegment,
    string? Category,
    string? Concept,
    string? CollectorClass,
    string? Reason,
    Guid Branch,
    decimal LendingBalance);

public sealed record CuentaConciliadaDto(string Code, string Name);

/// <summary>La diferencia explicada por el estado de entrega de los hechos que todavía no llegaron al libro.</summary>
public sealed record ExplicacionPorEstadoDto(decimal Pending, decimal InBatch, decimal Rejected, decimal NotPosted);

/// <summary>Un hecho que explica la diferencia: tipo, número, fecha contable, estado y valor.</summary>
public sealed record HechoQueExplicaDto(Guid MessagePublicId, string Type, string? Number, DateOnly AccountingDate, string Status, decimal Amount);

/// <summary>Un conjunto de cuentas (familia de rol) con el saldo de Cartera, el contable, la diferencia y su explicación hecho por hecho.</summary>
public sealed record ConjuntoConciliadoDto(
    string Family,
    IReadOnlyList<CombinacionConciliadaDto> Combinations,
    IReadOnlyList<CuentaConciliadaDto> Accounts,
    decimal LendingBalance,
    decimal AccountingBalance,
    decimal Difference,
    ExplicacionPorEstadoDto ExplainedBy,
    IReadOnlyList<HechoQueExplicaDto> Facts,
    decimal Unexplained,
    bool UnexplainedIsOtherSide);

/// <summary>El saldo de la cuenta puente de una clase de recaudador.</summary>
public sealed record PuenteConciliadoDto(string CollectorClass, decimal Balance);

/// <summary>Una combinación con saldo en Cartera sin regla en la matriz: su saldo no se puede comparar.</summary>
public sealed record CombinacionSinReglaDto(string Role, string? CreditLine, string? Modality, string? Category, string? Concept, Guid Branch,
    decimal LendingBalance);

/// <summary>La conciliación de Cartera con Contabilidad a una fecha (§16.3).</summary>
public sealed record ConciliacionContableDto(
    DateOnly Date,
    IReadOnlyList<ConjuntoConciliadoDto> Sets,
    IReadOnlyList<PuenteConciliadoDto> Bridges,
    IReadOnlyList<CombinacionSinReglaDto> UnresolvedCombinations,
    decimal UnexplainedTotal);

// ------------------------------------------------------------------------------------------------- emitidos --

/// <summary>Los filtros de la bandeja de emitidos (§16.1); <c>Estado</c> y <c>ValidacionPrevia</c> son los números de sus enums.</summary>
public sealed record FiltroDeMensajesEmitidos(
    int? Estado = null,
    string? Destino = null,
    string? Tipo = null,
    Guid? Origen = null,
    string? NumeroDeOrigen = null,
    Guid? Lote = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    bool Clausura = false,
    int? ValidacionPrevia = null,
    int Pagina = 1,
    int TamanoDePagina = 50);

/// <summary>Un tipo de hecho del catálogo (<c>LendingFactTypeDto</c>): su código es el tipo del mensaje.</summary>
public sealed record TipoDeHechoDto(Guid PublicId, string Code, string ShortCode, string Name, bool IsInformational, string? VoucherTypeCode,
    string DefaultMode, string? DefaultBatchTrigger);

/// <summary>El envío posterior de Cartera: rango, tipos de hecho por <c>PublicId</c> (nulo = todos), el corte de la vista previa y el motivo.</summary>
public sealed record EnvioPosteriorDeCarteraRequest(DateOnly From, DateOnly To, IReadOnlyList<Guid>? Types, Guid CutoffMessagePublicId, string Reason);
