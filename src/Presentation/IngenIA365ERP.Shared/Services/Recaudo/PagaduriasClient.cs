using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Security;
using IngenIA365ERP.Shared.Services.Ventas;

namespace IngenIA365ERP.Shared.Services.Recaudo;

/// <summary>Una línea del archivo de descuentos (los enums llegan como número). (nuevo)</summary>
public sealed record LineaDeArchivoDeDescuentosDto(Guid PublicId, Guid DeductionFileLinePublicId, int Kind, Guid ProductPublicId, string ProductNumber, int? InstallmentNumber,
    Guid PersonPublicId, string PersonName, string PayerEmployeeId, string? PaymasterConceptCode, DateOnly DueDate, decimal Amount, decimal PastDueAmount,
    int? DeductionOutcome, decimal? DeductedAmount, DateOnly? DeductionDate, decimal RemittedAmount, decimal PendingRemittance);

/// <summary>El archivo de descuentos de una pagaduría. Estado: 1 recibido, 2 enviado, 3 respondido, 4 reemplazado. (nuevo)</summary>
public sealed record ArchivoDeDescuentosDto(Guid PublicId, Guid FilePublicId, string FileNumber, int FileVersion, short MessageVersion, Guid PaymasterPersonPublicId,
    string PaymasterName, string AgreementCode, int AgreementVersion, string? FileGroupCode, int PeriodYear, int PeriodMonth, int? SubPeriod, string FileName, string FileSha256,
    DateOnly SendBy, int LineCount, decimal Amount, decimal PastDueAmount, int Status, DateTime? SentAt, string? SentReference,
    IReadOnlyList<LineaDeArchivoDeDescuentosDto> Lines);

public sealed record LineaDeRespuestaDto(int LineNumber, Guid? DeductionFileLinePublicId, string? PayerEmployeeId, string? PaymasterConceptCode, string? ProductNumber,
    decimal Expected, decimal Deducted, DateOnly? DeductionDate, int Outcome, string? PaymasterResultCode, string? Reason, bool Unmatched);

/// <summary>Una versión de la respuesta. Estado: 1 en revisión, 2 devuelta, 3 rechazada por Cartera, 4 aceptada por Cartera. (nuevo)</summary>
public sealed record RespuestaDePagaduriaDto(Guid PublicId, Guid FilePublicId, int Version, int Source, string? FileName, int Status, short? EmittedMessageVersion,
    string? RejectionCode, string? RejectionDataJson, int LineCount, decimal Expected, decimal Deducted, int Unmatched, DateTime LoadedAt, IReadOnlyList<LineaDeRespuestaDto> Lines);

public sealed record FilaDeVistaPreviaDto(string Key, int Lines, decimal Expected, decimal Deducted, int Unmatched);

public sealed record VistaPreviaDeRespuestaDto(Guid ResponsePublicId, string GroupBy, IReadOnlyList<FilaDeVistaPreviaDto> Rows, decimal Expected, decimal Deducted, int Unmatched);

public sealed record LineaDeGiroDto(Guid PublicId, int Order, Guid DeductionFileLinePublicId, string ProductNumber, string PersonName, decimal Amount, string IdempotencyKey,
    int Outcome, string? ReceiptNumber, string? RejectionCode, string? RejectionMessage, string? CreditBalanceNumber);

/// <summary>El giro de una pagaduría. Estado: 1 registrado, 2 aplicando, 3 aplicado, 4 aplicado en parte. (nuevo)</summary>
public sealed record GiroDePagaduriaDto(Guid PublicId, string Number, Guid PaymasterPersonPublicId, string PaymasterName, int Source, Guid? BankStatementLinePublicId,
    decimal Amount, DateOnly RemittedOn, int Status, int AllocationOrder, decimal Allocated, decimal Surplus, int Lines, int Applied, int Rejected, int Pending,
    Guid? LastRunPublicId, IReadOnlyList<LineaDeGiroDto> Items);

public sealed record ConfirmarEnvioRequest(int Channel, DateTime? SentAt, string? Reference);

public sealed record FilaCapturadaRequest(decimal Deducted, string? ProductNumber = null, string? PayerEmployeeId = null, string? PaymasterConceptCode = null,
    DateOnly? DeductionDate = null, string? ResultCode = null);

public sealed record CapturaDeRespuestaRequest(string? FileName, IReadOnlyList<FilaCapturadaRequest> Rows);

public sealed record RegistrarGiroRequest(Guid PaymasterPersonPublicId, Guid BankStatementLinePublicId, IReadOnlyList<Guid> FilePublicIds, decimal? Amount,
    int? AllocationOrder, IReadOnlyList<Guid>? LineOrder, bool SurplusAsCreditBalance, string? Note);

public sealed record AplicarGiroRequest(int? MaxLines, Guid? RunPublicId);

/// <summary>
/// Cliente tipado de las pagadurías de Recaudo (feature 015, US7, T0210; contracts/api.md §6). Las escrituras llevan <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
/// <summary>Feature 015, R1 (T0384): una pagaduría en un período del calendario legal de giro. (nuevo)</summary>
public sealed record CalendarioDePagaduriaDto(Guid PaymasterPersonPublicId, string PaymasterName, int Year, int Month, int SubPeriod, decimal Sent, decimal Deducted,
    decimal Remitted, decimal Pending, DateOnly? PayrollPayDate, DateOnly? LegalRemittanceDate, bool IsLate);

public sealed record FechaDePagoRequest(Guid PaymasterPersonPublicId, int Year, int Month, DateOnly PayrollPayDate, int SubPeriod, string? Note);

public sealed class PagaduriasClient(HttpClient http, CentralAuthClient auth)
{
    private const string Base = "/api/receipts";

    public Task<ResultadoDeInventario<IReadOnlyList<ArchivoDeDescuentosDto>>> ArchivosAsync(Guid? pagaduria = null, int? anio = null, int? mes = null, int? estado = null,
        CancellationToken ct = default) =>
        Enviar<IReadOnlyList<ArchivoDeDescuentosDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/payroll-files", EnvioDeVentas.Query(
            ("paymaster", EnvioDeVentas.Texto(pagaduria)), ("year", EnvioDeVentas.Texto(anio)), ("month", EnvioDeVentas.Texto(mes)), ("state", EnvioDeVentas.Texto(estado)))),
            null, null, ct);

    public Task<ResultadoDeInventario<ArchivoDeDescuentosDto>> ArchivoAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<ArchivoDeDescuentosDto>(HttpMethod.Get, $"{Base}/payroll-files/{archivo}", null, null, ct);

    public Task<ResultadoDeInventario<EnlaceDeDescargaDeRecaudoDto>> DescargaAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<EnlaceDeDescargaDeRecaudoDto>(HttpMethod.Get, $"{Base}/payroll-files/{archivo}/file", null, null, ct);

    public Task<ResultadoDeInventario<ArchivoDeDescuentosDto>> ConfirmarEnvioAsync(Guid archivo, ConfirmarEnvioRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<ArchivoDeDescuentosDto>(HttpMethod.Post, $"{Base}/payroll-files/{archivo}/sent", r, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<RespuestaDePagaduriaDto>>> RespuestasAsync(Guid archivo, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<RespuestaDePagaduriaDto>>(HttpMethod.Get, $"{Base}/payroll-files/{archivo}/responses", null, null, ct);

    public Task<ResultadoDeInventario<RespuestaDePagaduriaDto>> CargarRespuestaAsync(Guid archivo, string nombre, byte[] contenido, Guid? formato, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeRecaudo.MultipartAsync<RespuestaDePagaduriaDto>(http, auth, $"{Base}/payroll-files/{archivo}/responses", nombre, contenido,
            new Dictionary<string, string?> { ["format"] = formato?.ToString() }, clave, ct);

    public Task<ResultadoDeInventario<RespuestaDePagaduriaDto>> CapturarRespuestaAsync(Guid archivo, CapturaDeRespuestaRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        Enviar<RespuestaDePagaduriaDto>(HttpMethod.Post, $"{Base}/payroll-files/{archivo}/responses/captured", r, clave, ct);

    public Task<ResultadoDeInventario<VistaPreviaDeRespuestaDto>> VistaPreviaAsync(Guid archivo, Guid respuesta, CancellationToken ct = default) =>
        Enviar<VistaPreviaDeRespuestaDto>(HttpMethod.Get, $"{Base}/payroll-files/{archivo}/responses/{respuesta}/preview", null, null, ct);

    public Task<ResultadoDeInventario<RespuestaDePagaduriaDto>> DevolverAsync(Guid archivo, Guid respuesta, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RespuestaDePagaduriaDto>(HttpMethod.Post, $"{Base}/payroll-files/{archivo}/responses/{respuesta}/emit", null, clave, ct);

    public Task<ResultadoDeInventario<RespuestaDePagaduriaDto>> ActualizarEvaluacionAsync(Guid archivo, Guid respuesta, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<RespuestaDePagaduriaDto>(HttpMethod.Post, $"{Base}/payroll-files/{archivo}/responses/{respuesta}/refresh", null, clave, ct);

    public Task<ResultadoDeInventario<IReadOnlyList<GiroDePagaduriaDto>>> GirosAsync(Guid? pagaduria = null, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<GiroDePagaduriaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/paymaster-remittances",
            EnvioDeVentas.Query(("paymaster", EnvioDeVentas.Texto(pagaduria)))), null, null, ct);

    public Task<ResultadoDeInventario<GiroDePagaduriaDto>> RegistrarGiroAsync(RegistrarGiroRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GiroDePagaduriaDto>(HttpMethod.Post, $"{Base}/paymaster-remittances", r, clave, ct);

    public Task<ResultadoDeInventario<GiroDePagaduriaDto>> AplicarGiroAsync(Guid giro, AplicarGiroRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<GiroDePagaduriaDto>(HttpMethod.Post, $"{Base}/paymaster-remittances/{giro}/apply", r, clave, ct);

    // ---- Feature 015, R1 (T0384): el calendario legal de giro y el informe de morosas ----
    public Task<ResultadoDeInventario<IReadOnlyList<CalendarioDePagaduriaDto>>> CalendarioAsync(int? anio = null, bool soloMorosas = false, CancellationToken ct = default) =>
        Enviar<IReadOnlyList<CalendarioDePagaduriaDto>>(HttpMethod.Get, EnvioDeVentas.ConQuery($"{Base}/paymaster-calendar",
            EnvioDeVentas.Query(("year", EnvioDeVentas.Texto(anio)), ("onlyLate", soloMorosas ? "true" : "false"))), null, null, ct);

    public Task<ResultadoDeInventario<CalendarioDePagaduriaDto>> RegistrarFechaDePagoAsync(FechaDePagoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        Enviar<CalendarioDePagaduriaDto>(HttpMethod.Post, $"{Base}/paymaster-calendar", r, clave, ct);

    private Task<ResultadoDeInventario<T>> Enviar<T>(HttpMethod metodo, string url, object? cuerpo, ClaveDeOperacion? clave, CancellationToken ct) =>
        EnvioDeRecaudo.EnviarAsync<T>(http, auth, metodo, url, cuerpo, clave, null, ct);
}

/// <summary>El enlace firmado de una descarga (o, en el formato anterior, «por la API»). (nuevo)</summary>
public sealed record EnlaceDeDescargaDeRecaudoDto(bool Direct, string? Url, DateTimeOffset? ExpiresAt);
