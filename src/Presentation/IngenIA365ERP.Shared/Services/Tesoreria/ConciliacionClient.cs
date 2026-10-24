using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

/// <summary>Un cruce de la conciliación (las filas de un mismo grupo comparten <c>GroupKey</c>). (nuevo)</summary>
public sealed record CruceDeConciliacionDto(Guid PublicId, Guid GroupKey, Guid StatementLinePublicId, string RecordKind, Guid RecordPublicId, string? RecordNumber,
    decimal Amount, string Mode, string Rule, DateTime MatchedAt, string MatchedBy);

/// <summary>Una partida conciliatoria con su efecto (con signo) y su antigüedad. (nuevo)</summary>
public sealed record PartidaConciliatoriaDto(Guid PublicId, string Kind, decimal Amount, decimal SignedAmount, DateOnly Since, int AgeDays, Guid? SourcePublicId,
    Guid? StatementLinePublicId, Guid? UnidentifiedItemPublicId, Guid? AlertPublicId, string? Note);

/// <summary>Un registro de los libros que falta por cruzar. (nuevo)</summary>
public sealed record RegistroPorCruzarDto(string Kind, Guid PublicId, string Number, DateOnly Date, string Direction, decimal Amount, string? Reference, string Source);

/// <summary>Una línea del extracto que falta por cruzar. (nuevo)</summary>
public sealed record LineaPorCruzarDto(Guid PublicId, int LineNumber, DateOnly MovementDate, string Direction, decimal Amount, string? Reference, string? BankSequence,
    string? DepositorTaxId, string? DepositorName, string? Description, decimal? Balance, bool HasBankIdentifier, bool PossibleDuplicate, string? DuplicateDecision,
    string RecaudoStatus, decimal RecaudoIdentifiedAmount, string ReconciliationStatus);

/// <summary>Una conciliación; en el detalle, cruces, partidas, líneas y registros por cruzar. (nuevo)</summary>
public sealed record ConciliacionDto(Guid PublicId, Guid BankAccountPublicId, string BankAccountCode, int Year, int Month, int Version, string Status,
    decimal StatementBalance, decimal LedgerBalance, decimal ReconcilingItemsTotal, decimal Difference, string Conciliator, DateTime? ClosedAt,
    Guid? ReportAttachmentPublicId, string? ReopenReason, IReadOnlyList<CruceDeConciliacionDto>? Matches = null, IReadOnlyList<PartidaConciliatoriaDto>? Items = null,
    IReadOnlyList<LineaPorCruzarDto>? OpenLines = null, IReadOnlyList<RegistroPorCruzarDto>? AvailableRecords = null);

public sealed record AbrirConciliacionRequest(Guid BankAccountPublicId, int Year, int Month, decimal? StatementBalance);

/// <summary>Un registro elegido para el cruce manual: su clase por nombre (<c>ReceiptsBankReceipt</c>, <c>Remittance</c>, …) y su <c>PublicId</c>. (nuevo)</summary>
public sealed record RegistroElegidoRequest(string Kind, Guid PublicId);

public sealed record CruceManualRequest(Guid StatementLinePublicId, IReadOnlyList<RegistroElegidoRequest> Records, string? Note);

/// <summary>Una partida conciliatoria; la clase va por nombre (<c>IssuedCheckNotCashed</c>, <c>UnidentifiedCredit</c>, …). (nuevo)</summary>
public sealed record PartidaConciliatoriaRequest(string Kind, decimal Amount, DateOnly? Since, Guid? StatementLinePublicId, Guid? SourcePublicId, string? Note);

/// <summary>
/// Cliente tipado de la conciliación bancaria mínima de Tesorería (feature 015, US25, T0280; api.md §12). Las escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class ConciliacionClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury/reconciliations";

    public Task<ResultadoDeInventario<IReadOnlyList<ConciliacionDto>>> ListarAsync(Guid? cuenta = null, int? anio = null, int? mes = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ConciliacionDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery(Base, EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)),
            ("year", EnvioDeVentas.Texto(anio)), ("month", EnvioDeVentas.Texto(mes)))), null, null, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> DetalleAsync(Guid conciliacion, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Get, $"{Base}/{conciliacion}", null, null, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> AbrirAsync(AbrirConciliacionRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, Base, r, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> CruzarSoloAsync(Guid conciliacion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, $"{Base}/{conciliacion}/auto-match", null, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> CruzarAManoAsync(Guid conciliacion, CruceManualRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, $"{Base}/{conciliacion}/matches", r, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> DeshacerCruceAsync(Guid conciliacion, Guid cruce, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Delete, $"{Base}/{conciliacion}/matches/{cruce}", null, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> PartidaAsync(Guid conciliacion, PartidaConciliatoriaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, $"{Base}/{conciliacion}/items", r, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> CerrarAsync(Guid conciliacion, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, $"{Base}/{conciliacion}/close", null, clave, ct);

    public Task<ResultadoDeInventario<ConciliacionDto>> ReabrirAsync(Guid conciliacion, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ConciliacionDto>(HttpMethod.Post, $"{Base}/{conciliacion}/reopen", new MotivoDeTesoreriaRequest(motivo), clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
