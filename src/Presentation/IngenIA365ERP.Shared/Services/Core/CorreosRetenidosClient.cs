using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using static IngenIA365ERP.Shared.Services.Ventas.EnvioDeVentas;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>El modo de prueba del correo vigente (<c>GET /api/core/email-mode</c>). <c>Mode</c>: Enviar, Suspender o Redirigir. (nuevo)</summary>
public sealed record ModoDeCorreoDto(string Mode, string? TestInbox, bool KeepPending, int PendingCount, bool IsTestMode);

/// <summary>Un correo retenido o desviado. <c>Mode</c>: Suspended o Redirected; <c>Status</c>: Pending, Discarded, Redirected o Resent. (nuevo)</summary>
public sealed record CorreoRetenidoDto(
    Guid PublicId, string Mode, string Status, string OriginalTo, string? RedirectedTo, string Subject, int AttachmentCount,
    DateTime CreatedAt, string? CreatedBy, DateTime? ResentAt, string? ResentBy);

public sealed record SeleccionDeCorreosRequest(IReadOnlyList<Guid> PublicIds);

public sealed record ResultadoDeRetenidosDto(int Processed, int Failed, IReadOnlyList<string> Errors);

/// <summary>
/// Cliente tipado del modo de prueba del correo (2026-11-17): el modo vigente para la franja del encabezado y los correos retenidos para
/// reenviarlos o descartarlos. Sin <c>Authorization</c> a mano; reenviar y descartar llevan <c>Idempotency-Key</c>. (nuevo)
/// </summary>
public sealed class CorreosRetenidosClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/core/held-emails";

    public Task<ResultadoDeInventario<ModoDeCorreoDto>> ModoAsync(CancellationToken ct = default) =>
        EnviarAsync<ModoDeCorreoDto>(http, auth, HttpMethod.Get, "/api/core/email-mode", null, null, ct);

    public Task<ResultadoDeInventario<List<CorreoRetenidoDto>>> ListarAsync(string? estado, CancellationToken ct = default) =>
        EnviarAsync<List<CorreoRetenidoDto>>(http, auth, HttpMethod.Get, ConQuery(Ruta, Query(("status", estado))), null, null, ct);

    public Task<ResultadoDeInventario<ResultadoDeRetenidosDto>> ReenviarAsync(IReadOnlyList<Guid> ids, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeRetenidosDto>(http, auth, HttpMethod.Post, $"{Ruta}/resend", new SeleccionDeCorreosRequest(ids), clave, ct);

    public Task<ResultadoDeInventario<ResultadoDeRetenidosDto>> DescartarAsync(IReadOnlyList<Guid> ids, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnviarAsync<ResultadoDeRetenidosDto>(http, auth, HttpMethod.Post, $"{Ruta}/discard", new SeleccionDeCorreosRequest(ids), clave, ct);
}
