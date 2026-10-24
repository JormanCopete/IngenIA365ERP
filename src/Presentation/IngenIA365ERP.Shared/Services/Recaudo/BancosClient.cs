using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Un abono del extracto como lo ve Recaudo. (nuevo)</summary>
public sealed record AbonoDto(Guid PublicId, Guid OwnBankAccountPublicId, DateOnly MovementDate, DateOnly? ValueDate, decimal Amount, decimal Identified, string? Reference,
    string? DepositorTaxId, string? DepositorName, string? Description, bool PossibleDuplicate, string OwnBankAccountCode, Guid? BranchPublicId, Guid? BankPublicId,
    string? BankTransferCode, string RecaudoStatus, string? DuplicateDecision, string StatementNumber, Guid? PossibleDuplicateOfPublicId)
{
    public decimal Pending => Amount - Identified;
}

public sealed record PaginaDeAbonosDto(IReadOnlyList<AbonoDto> Items, int Total, int Page, int PageSize);

/// <summary>Una parte de la identificación: crédito (1), cuenta (2) o concepto vario (3) de un titular. (nuevo)</summary>
public sealed record ParteDeAbonoRequest(int Target, Guid BeneficiaryPersonPublicId, decimal Amount, Guid? ProductPublicId = null, Guid? MiscConceptPublicId = null,
    string? CrossDocument = null);

public sealed record IdentificarAbonoRequest(IReadOnlyList<ParteDeAbonoRequest> Parts, Guid? PayerPersonPublicId, bool RemainderAsUnidentified, string? Note, Guid? RunPublicId,
    int? MeansClass = null);

public sealed record LineaDeCorridaDto(Guid PublicId, string LineRef, string IdempotencyKey, int Outcome, decimal Amount, string? Code, string? Message, Guid? ReceiptPublicId,
    string? ReceiptNumber, int Attempts);

public sealed record CorridaDto(Guid PublicId, int Kind, Guid SourcePublicId, string SourceNumber, int Lines, int Applied, int Rejected, int Pending, decimal Total, int Batches,
    DateTime StartedAt, DateTime? FinishedAt, IReadOnlyList<LineaDeCorridaDto> Items);

public sealed record IdentificacionDto(Guid PublicId, Guid BankStatementLinePublicId, int Sequence, int Outcome, decimal Amount, string? ReceiptNumber,
    string? UnidentifiedItemNumber, string? MiscConceptCode, Guid? PaymentOrderPublicId, DateTime IdentifiedAt);

public sealed record IdentificacionDeAbonoDto(CorridaDto Run, IReadOnlyList<IdentificacionDto> Identifications, decimal Identified, decimal Pending, bool LateValueDate,
    string? CreditBalanceNumber);

public sealed record NotaRequest(string? Note);

public sealed record ClasificarAbonoRequest(Guid MiscConceptPublicId, string Reason, decimal? Amount, Guid? PaymentOrderPublicId);

public sealed record DuplicadoRequest(bool IsDuplicate, string? Note);

public sealed record ResolucionDePartidaDto(Guid PublicId, int Kind, decimal Amount, string? DocumentNumber, string? MiscConceptCode, DateTime At, string? Note);

/// <summary>Una partida por identificar (los enums llegan como número). (nuevo)</summary>
public sealed record PartidaDto(Guid PublicId, string Number, Guid? BankStatementLinePublicId, Guid OwnBankAccountPublicId, string? BankName, DateOnly MovementDate,
    decimal Amount, decimal Balance, string? Reference, string? DepositorName, string? DepositorTaxId, int Status, int OriginKind, int AgeDays, short AgingStep,
    IReadOnlyList<ResolucionDePartidaDto> Resolutions);

public sealed record IdentificarPartidaRequest(IReadOnlyList<ParteDeAbonoRequest> Parts, Guid? PayerPersonPublicId, string? Note, int? MeansClass = null);

public sealed record ClasificarPartidaRequest(Guid MiscConceptPublicId, string Reason, decimal? Amount);

public sealed record DevolverPartidaRequest(Guid BeneficiaryPersonPublicId, decimal? Amount, Guid? PersonBankAccountPublicId, string Form, string? Note);

public sealed record PartidaAIngresoRequest(Guid MiscConceptPublicId, string Reason);

/// <summary>
/// Cliente tipado de los abonos, las partidas por identificar y las corridas de Recaudo (feature 015, US6, T0199; contracts/api.md §5). Las escrituras llevan
/// <see cref="ClaveDeOperacion"/>; las que piden aprobador presente se repiten con <c>X-Approval-Request</c>. (nuevo)
/// </summary>
public sealed class BancosClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    public Task<ResultadoDeInventario<PaginaDeAbonosDto>> AbonosAsync(Guid? cuenta = null, DateOnly? desde = null, DateOnly? hasta = null, string? referencia = null,
        bool soloPendientes = true, int pagina = 1, CancellationToken ct = default) =>
        Enviar<PaginaDeAbonosDto>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/bank-items", EnvioDeVentas.Query(("account", EnvioDeVentas.Texto(cuenta)),
            ("from", EnvioDeVentas.Texto(desde)), ("to", EnvioDeVentas.Texto(hasta)), ("reference", referencia), ("onlyPending", soloPendientes ? "true" : "false"),
            ("page", EnvioDeVentas.Texto(pagina)))), null, null, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<IdentificacionDto>>> IdentificacionesAsync(Guid linea, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<IdentificacionDto>>(HttpMethod.Get, $"{Base}/bank-items/{linea}/identifications", null, null, null, ct);

    public Task<ResultadoDeInventario<IdentificacionDeAbonoDto>> IdentificarAsync(Guid linea, IdentificarAbonoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<IdentificacionDeAbonoDto>(HttpMethod.Post, $"{Base}/bank-items/{linea}/identify", r, clave, null, ct);

    public Task<ResultadoDeInventario<PartidaDto>> DejarComoPartidaAsync(Guid linea, string? nota, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PartidaDto>(HttpMethod.Post, $"{Base}/bank-items/{linea}/unidentified", new NotaRequest(nota), clave, null, ct);

    public Task<ResultadoDeInventario<IdentificacionDto>> ClasificarAsync(Guid linea, ClasificarAbonoRequest r, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct = default) =>
        Enviar<IdentificacionDto>(HttpMethod.Post, $"{Base}/bank-items/{linea}/classify", r, clave, aprobacion, ct);

    public Task<ResultadoDeInventario<AbonoDto>> DecidirDuplicadoAsync(Guid linea, DuplicadoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<AbonoDto>(HttpMethod.Post, $"{Base}/bank-items/{linea}/duplicate-decision", r, clave, null, ct);

    public Task<ResultadoDeInventario<CorridaDto>> CorridaAsync(Guid corrida, CancellationToken ct = default) =>
        Enviar<CorridaDto>(HttpMethod.Get, $"{Base}/processing-runs/{corrida}", null, null, null, ct);

    public Task<ResultadoDeInventario<CorridaDto>> ReprocesarAsync(Guid corrida, Guid linea, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CorridaDto>(HttpMethod.Post, $"{Base}/processing-runs/{corrida}/lines/{linea}/retry", null, clave, null, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<PartidaDto>>> PartidasAsync(int? antiguedad = null, int? estado = null, string? consignante = null, bool soloAbiertas = true,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<PartidaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/unidentified-items", EnvioDeVentas.Query(("aging", EnvioDeVentas.Texto(antiguedad)),
            ("state", EnvioDeVentas.Texto(estado)), ("depositor", consignante), ("onlyOpen", soloAbiertas ? "true" : "false"))), null, null, null, ct);

    public Task<ResultadoDeInventario<PartidaDto>> IdentificarPartidaAsync(Guid partida, IdentificarPartidaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PartidaDto>(HttpMethod.Post, $"{Base}/unidentified-items/{partida}/identify", r, clave, null, ct);

    public Task<ResultadoDeInventario<PartidaDto>> ClasificarPartidaAsync(Guid partida, ClasificarPartidaRequest r, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct = default) =>
        Enviar<PartidaDto>(HttpMethod.Post, $"{Base}/unidentified-items/{partida}/classify", r, clave, aprobacion, ct);

    public Task<ResultadoDeInventario<PartidaDto>> DevolverPartidaAsync(Guid partida, DevolverPartidaRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<PartidaDto>(HttpMethod.Post, $"{Base}/unidentified-items/{partida}/refund-by-order", r, clave, null, ct);

    public Task<ResultadoDeInventario<PartidaDto>> LlevarAIngresoAsync(Guid partida, PartidaAIngresoRequest r, ClaveDeOperacion clave, Guid? aprobacion,
        CancellationToken ct = default) =>
        Enviar<PartidaDto>(HttpMethod.Post, $"{Base}/unidentified-items/{partida}/move-to-income", r, clave, aprobacion, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, Guid? aprobacion, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, aprobacion, ct);
}

/// <summary>
/// El envío común de los clientes de Recaudo de las fases 11 a 16 (nuevo): el de siempre (<c>EnvioDeVentas</c>) y, cuando hay aprobación presente, la repetición con
/// <c>X-Approval-Request</c>; el multipart de los archivos que el servidor lee con su formato (extracto, respuesta de pagaduría). Sin <c>Authorization</c> a mano.
/// </summary>
public static class EnvioDeRecaudo
{
    public const string CabeceraDeAprobacion = SesionesClient.CabeceraDeAprobacion;

    public static async Task<ResultadoDeInventario<T>> EnviarAsync<T>(HttpClient http, CentralAuthClient auth, HttpMethod metodo, string url, object? cuerpo,
        ClaveDeOperacion? clave, Guid? aprobacion, CancellationToken ct)
    {
        if (aprobacion is null) return await EnvioDeVentas.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, ct);
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(metodo, url);
            if (cuerpo is not null) req.Content = JsonContent.Create(cuerpo, cuerpo.GetType());
            clave?.Aplicar(req, new { metodo = metodo.Method, url, cuerpo });
            req.Headers.Add(CabeceraDeAprobacion, aprobacion.Value.ToString());
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave?.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
    }

    /// <summary>Un archivo por multipart (campo <c>archivo</c>) con los demás campos del formulario.</summary>
    public static async Task<ResultadoDeInventario<T>> MultipartAsync<T>(HttpClient http, CentralAuthClient auth, string url, string nombre, byte[] contenido,
        IReadOnlyDictionary<string, string?> campos, ClaveDeOperacion clave, CancellationToken ct)
    {
        if (auth.CurrentAccessToken is null) return ResultadoDeInventario<T>.SinToken();
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            var multipart = new MultipartFormDataContent();
            var bytes = new ByteArrayContent(contenido);
            bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            multipart.Add(bytes, "archivo", nombre);
            foreach (var (k, v) in campos.Where(c => c.Value is not null)) multipart.Add(new StringContent(v!), k);
            req.Content = multipart;
            clave.Aplicar(req, new { url, nombre, campos, sha256 = Convert.ToHexString(SHA256.HashData(contenido)) });
            using var resp = await http.SendAsync(req, ct);
            var resultado = await ResultadoDeInventario<T>.DesdeAsync(resp, ct);
            if (resultado.IsSuccess) clave.Exito();
            return resultado;
        }
        catch (HttpRequestException ex)
        {
            return ResultadoDeInventario<T>.ErrorDeRed(ex.Message);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return ResultadoDeInventario<T>.FormatoInesperado(ex.Message);
        }
    }
}
