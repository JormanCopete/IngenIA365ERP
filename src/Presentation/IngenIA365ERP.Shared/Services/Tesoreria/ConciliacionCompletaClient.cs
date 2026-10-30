using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Tesoreria;

// Feature 015, T2 (fase 41, T0449): lo que leen y mandan las pantallas de la conciliación completa. Clase de nota: 1 GMF, 2 comisión, 3 intereses, 4 impuesto, 5 otra; estado
// 1 por aprobar, 2 aprobada, 3 rechazada; sentido 1 abono, 2 cargo; criterio de regla 1 referencia y valor, 2 valor y fecha, 3 valor el mismo día. (nuevo)

public sealed record NotaBancariaDto(Guid PublicId, string Number, Guid BankAccountPublicId, string BankAccountCode, int Kind, DateOnly NoteDate, int Direction, decimal Amount,
    decimal BaseAmount, decimal VatAmount, decimal WithholdingAmount, Guid? StatementLinePublicId, Guid? ThirdPartyPersonPublicId, string? Description, int Status,
    DateTime RegisteredAt, DateTime? DecidedAt, string? RejectionReason);

public sealed record NotaBancariaRequest(Guid BankAccountPublicId, int Kind, decimal Amount, DateOnly? NoteDate, decimal? BaseAmount, decimal? VatAmount, decimal? WithholdingAmount,
    Guid? StatementLinePublicId, Guid? ThirdPartyPersonPublicId, string? Description);

public sealed record SugerenciaDeNotaDto(Guid StatementLinePublicId, DateOnly MovementDate, int Direction, decimal Amount, string? Description, int SuggestedKind);

public sealed record ReglaDeConciliacionDto(Guid PublicId, string Code, string Name, int Criterion, int ToleranceDays, int Priority, Guid? BankAccountPublicId, bool IsActive);

public sealed record ReglaDeConciliacionRequest(string Code, string Name, int Criterion, int ToleranceDays, int Priority, Guid? BankAccountPublicId, bool IsActive);

/// <summary>Cliente tipado de las notas bancarias y las reglas de conciliación (feature 015, T2, fase 41; api.md §13). (nuevo)</summary>
public sealed class ConciliacionCompletaClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/treasury";

    public Task<ResultadoDeInventario<IReadOnlyList<NotaBancariaDto>>> NotasAsync(Guid? cuenta, int? estado, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<NotaBancariaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/bank-notes",
            EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)), ("state", EnvioDeVentas.Texto(estado)))), null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<SugerenciaDeNotaDto>>> SugerenciasAsync(Guid cuenta, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<SugerenciaDeNotaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/bank-notes/suggestions",
            EnvioDeVentas.Query(("account", cuenta.ToString()))), null, null, ct);

    public Task<ResultadoDeInventario<NotaBancariaDto>> RegistrarAsync(NotaBancariaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NotaBancariaDto>(HttpMethod.Post, $"{Base}/bank-notes", r, clave, ct);

    public Task<ResultadoDeInventario<NotaBancariaDto>> AprobarAsync(Guid nota, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NotaBancariaDto>(HttpMethod.Post, $"{Base}/bank-notes/{nota}/approve", null, clave, ct);

    public Task<ResultadoDeInventario<NotaBancariaDto>> RechazarAsync(Guid nota, string motivo, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<NotaBancariaDto>(HttpMethod.Post, $"{Base}/bank-notes/{nota}/reject", new { reason = motivo }, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<ReglaDeConciliacionDto>>> ReglasAsync(CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ReglaDeConciliacionDto>>(HttpMethod.Get, $"{Base}/reconciliation-rules", null, null, ct);

    public Task<ResultadoDeInventario<ReglaDeConciliacionDto>> GuardarReglaAsync(Guid? regla, ReglaDeConciliacionRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<ReglaDeConciliacionDto>(regla is null ? HttpMethod.Post : HttpMethod.Put, regla is null ? $"{Base}/reconciliation-rules" : $"{Base}/reconciliation-rules/{regla}",
            r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}
