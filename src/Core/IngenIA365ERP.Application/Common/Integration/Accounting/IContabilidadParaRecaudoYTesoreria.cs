using IngenIA365ERP.Application.Common.Models;

namespace IngenIA365ERP.Application.Common.Integration.Accounting;

// Feature 015, T0041 (R-22; FR-004; E-04; contracts/interfaces.md §3): lo que Recaudo y Tesorería preguntan a Contabilidad y lo que le ofrecen.
// Sin implementación todavía: la trae la fase 16 (US8) con la integración contable. Recaudo y Tesorería nunca conocen una cuenta: lo que cruza
// a Contabilidad cruza por hechos (RecaudoNoConoceContabilidad, TesoreriaNoConoceContabilidad).

/// <summary>Una combinación de hecho × rol × dimensiones (por código) que la validación previa evalúa. (nuevo)</summary>
public sealed record CombinacionDeHecho(string Module, string FactType, IReadOnlyDictionary<string, string> Dimensions, DateOnly AccountingDate);

/// <summary>El resultado de la validación previa: si cada combinación tiene regla, y qué hace la política si no (D25). (nuevo)</summary>
public sealed record EvaluacionContableDto(bool Postable, string Policy, IReadOnlyList<CombinacionSinReglaDto> Missing);

/// <summary>Una combinación sin regla: hecho, rol y lo que falta, para que la pantalla diga quién debe parametrizarla. (nuevo)</summary>
public sealed record CombinacionSinReglaDto(string FactType, string Role, string Detail);

/// <summary>El saldo contable que piden los cuadres (caja por oficina, partidas, saldos a favor). (nuevo)</summary>
public sealed record SolicitudDeSaldo(string Concept, DateOnly Date, Guid? OfficePublicId, Guid? OwnBankAccountPublicId);

public sealed record SaldoContableDto(string Concept, Guid? OfficePublicId, Guid? OwnBankAccountPublicId, decimal Balance);

/// <summary>Una cuenta por cobrar comercial de la persona (R1). (nuevo)</summary>
public sealed record CuentaPorCobrarComercialDto(Guid DocumentPublicId, string Number, DateOnly Date, decimal Pending);

/// <summary>Una cuenta bancaria del plan, para vincular una cuenta propia de Tesorería (E-04). (nuevo)</summary>
public sealed record CuentaContableBancariaDto(Guid ChartAccountPublicId, string Code, string Name, Guid? BankPublicId, string? BankAccountNumber);

/// <summary>El vínculo vigente de una cuenta propia con su auxiliar (<c>ACC_OwnBankAccountLinks</c>). (nuevo)</summary>
public sealed record VinculoDto(Guid OwnBankAccountPublicId, Guid ChartAccountPublicId, string Code, DateOnly ValidFrom);

/// <summary>Un movimiento contable de la cuenta bancaria, para la conciliación mínima (FR-209). (nuevo)</summary>
public sealed record MovimientoContableDeCuentaDto(
    Guid JournalEntryPublicId, string VoucherNumber, DateOnly Date, decimal Debit, decimal Credit, string? Description, string SourceModule);

/// <summary>Qué cuentas por pagar pide Tesorería (T2). (nuevo)</summary>
public sealed record FiltroDeCuentasPorPagar(Guid? PersonPublicId, DateOnly? DueUntil);

public sealed record CuentaPorPagarDto(Guid DocumentPublicId, Guid PersonPublicId, string Number, DateOnly DueDate, decimal Pending);

/// <summary>Un pendiente operativo que el cierre del mes contable consulta (FR-228; T1: conciliaciones abiertas, sesiones sin cerrar). (nuevo)</summary>
public sealed record PendienteDeCierreDto(string Module, string Code, string Message, bool Blocks);

/// <summary>
/// La completitud de la matriz de un módulo para su activación (feature 015, US9, T0303; FR-150; nuevo, DT §2.7): con Contabilidad iniciada, el porcentaje de las
/// combinaciones de la plantilla que tienen regla y cuántas faltan; con el modo de paso en «no pasa» (<see cref="NotPosted"/>) o sin Contabilidad iniciada, no se exige.
/// </summary>
public sealed record CompletitudContableDeModuloDto(bool AccountingInitialized, bool NotPosted, decimal Percent, int Missing)
{
    /// <summary>¿Cumple el requisito de FR-150?</summary>
    public bool Cumple => !AccountingInitialized || NotPosted || Percent >= 100m;
}

/// <summary>Lo que Recaudo pregunta a Contabilidad (interfaces.md §3). (nuevo)</summary>
public interface IContabilidadParaRecaudo
{
    /// <summary>La validación previa.</summary>
    Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct);

    /// <summary>La fecha contable propuesta.</summary>
    Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);

    /// <summary>Caja por oficina, partidas y saldos a favor para los cuadres.</summary>
    Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct);

    /// <summary>R1.</summary>
    Task<IReadOnlyList<CuentaPorCobrarComercialDto>> CuentasPorCobrarComercialesAsync(Guid personPublicId, CancellationToken ct);

    /// <summary>La completitud de la matriz de Recaudo a la fecha, para la activación (fase 19, T0303).</summary>
    Task<CompletitudContableDeModuloDto> CompletitudDeLaMatrizAsync(DateOnly fecha, CancellationToken ct);
}

/// <summary>Lo que Tesorería pregunta a Contabilidad (interfaces.md §3). (nuevo)</summary>
public interface IContabilidadParaTesoreria
{
    Task<EvaluacionContableDto> EvaluarAsync(IReadOnlyList<CombinacionDeHecho> combinaciones, CancellationToken ct);

    /// <summary>La fecha contable propuesta de un hecho de Tesorería (US10, T0145; nuevo, DT §2.7): el mismo estado del período que lee Recaudo.</summary>
    Task<EstadoDelPeriodoDto> EstadoDelPeriodoAsync(DateOnly fecha, CancellationToken ct);

    Task<IReadOnlyList<CuentaContableBancariaDto>> CuentasBancariasDelPlanAsync(Guid? bankPublicId, CancellationToken ct);

    /// <summary>
    /// Escribe <c>ACC_OwnBankAccountLinks</c> en la unidad de trabajo de quien llama, sin guardar (US10, T0137). Responde un <see cref="Result"/> (interfaces.md §3
    /// decía <c>Task</c>; DT §2.7): la cuenta contable que no es bancaria o ya representa a otra cuenta propia es un rechazo, no una excepción.
    /// </summary>
    Task<Result> VincularCuentaBancariaAsync(Guid ownBankAccountPublicId, Guid chartAccountPublicId, DateOnly desde, CancellationToken ct);

    Task<VinculoDto?> VinculoAsync(Guid ownBankAccountPublicId, CancellationToken ct);

    Task<decimal> SaldoEnLibrosAsync(Guid ownBankAccountPublicId, DateOnly fecha, CancellationToken ct);

    /// <summary>La conciliación mínima (FR-209).</summary>
    Task<IReadOnlyList<MovimientoContableDeCuentaDto>> MovimientosDeCuentaBancariaAsync(Guid ownBankAccountPublicId, DateOnly desde, DateOnly hasta, CancellationToken ct);

    Task<IReadOnlyList<SaldoContableDto>> SaldoContableAsync(SolicitudDeSaldo solicitud, CancellationToken ct);

    /// <summary>T2.</summary>
    Task<IReadOnlyList<CuentaPorPagarDto>> CuentasPorPagarAsync(FiltroDeCuentasPorPagar filtro, CancellationToken ct);

    /// <summary>La completitud de la matriz de Tesorería a la fecha, para la activación (fase 19, T0303).</summary>
    Task<CompletitudContableDeModuloDto> CompletitudDeLaMatrizAsync(DateOnly fecha, CancellationToken ct);
}

// IDimensionesDeRecaudo e IDimensionesDeTesoreria: en IDimensionesDeRecaudoYTesoreria.cs (fase 16, T0260).

/// <summary>
/// Lo que Recaudo y Tesorería ofrecen al cierre del mes contable (interfaces.md §3): el cuadre provisional pendiente del mes de la activación
/// bloquea (FR-228); en T1, conciliaciones abiertas y sesiones sin cerrar avisan (bloquean con <c>Contabilidad.CierreExigeConciliaciones</c>). (nuevo)
/// </summary>
public interface IEstadoOperativoParaElCierre
{
    Task<IReadOnlyList<PendienteDeCierreDto>> PendientesAsync(int anio, int mes, CancellationToken ct);
}
