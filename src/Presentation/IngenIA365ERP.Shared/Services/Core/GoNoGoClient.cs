using IngenIA365ERP.Shared.Services.Http;
using IngenIA365ERP.Shared.Services.Inventario;
using IngenIA365ERP.Shared.Services.Recaudo;
using IngenIA365ERP.Shared.Services.Security;

namespace IngenIA365ERP.Shared.Services.Core;

/// <summary>Una condición de la lista de go/no-go. <c>Kind</c>: 1 automática, 2 manual; <c>Status</c>: 1 verde, 2 rojo, 3 no aplica (la API los envía por número). (nuevo)</summary>
public sealed record CondicionDeGoNoGoDto(
    Guid PublicId, string Module, string Code, string Description, int Kind, int Status, string? Evidence, Guid? EvidenceAttachmentPublicId, DateTime? CheckedAt,
    string? CheckedByName, string? OverrideApprovedByName, string? OverrideReason)
{
    public bool EsManual => Kind == GoNoGoClient.Manual;
    public bool EnRojo => Status == GoNoGoClient.Rojo;
    public bool EnRojoSinExcepcion => EnRojo && OverrideApprovedByName is null;
}

/// <summary>Una lista de go/no-go. <c>Status</c>: 1 abierta, 2 Go, 3 NoGo, 4 salida parcial. (nuevo)</summary>
public sealed record ListaDeGoNoGoDto(
    Guid PublicId, string Name, string Environment, DateOnly TargetDate, int Status, string? DecidedByName, DateTime? DecisionAt, string? DecisionReason,
    Guid? ScenarioAttachmentPublicId, IReadOnlyList<CondicionDeGoNoGoDto> Items, int RedWithoutOverride);

public sealed record CondicionManualRequest(string Module, string Code, string Description);

public sealed record CrearListaDeGoNoGoRequest(string Name, string Environment, DateOnly TargetDate, IReadOnlyList<CondicionManualRequest> ManualItems);

public sealed record VerificarCondicionRequest(int? Status, string? Evidence, Guid? EvidenceAttachmentPublicId);

public sealed record MotivoDeGoNoGoRequest(string Reason);

public sealed record DecidirGoNoGoRequest(int Decision, string Reason, Guid? ScenarioAttachmentPublicId);

/// <summary>
/// Cliente tipado de la lista de go/no-go (feature 015, US24, T0322; api.md §14; FR-227) sobre <c>/api/core/go-live-checklists</c>: listar, crear con las condiciones
/// manuales, verificar una condición (la manual con su evidencia; una automática se vuelve a calcular), aprobar seguir con un rojo (<c>Platform.GoLive.Override</c>) y
/// decidir. Las escrituras llevan su <see cref="ClaveDeOperacion"/>. (nuevo)
/// </summary>
public sealed class GoNoGoClient(HttpClient http, CentralAuthClient auth)
{
    public const string Ruta = "/api/core/go-live-checklists";

    /// <summary>El dueño de los adjuntos de una lista (evidencias y escenario de no-go parcial).</summary>
    public const string DuenoDeEvidencias = "GoLiveEvidence";

    public const int Automatica = 1;
    public const int Manual = 2;
    public const int Verde = 1;
    public const int Rojo = 2;
    public const int NoAplica = 3;
    public const int Abierta = 1;
    public const int Go = 2;
    public const int NoGo = 3;
    public const int SalidaParcial = 4;

    /// <summary>Las condiciones manuales que se proponen al crear la lista de la salida de R0/T0 (runbook de go/no-go).</summary>
    public static readonly IReadOnlyList<CondicionManualRequest> ManualesSugeridas =
    [
        new("Plataforma", "CRONOGRAMA", "Cronograma del cambio aprobado (ensayo, respaldo, despliegue, carga definitiva, activación, cuadre de apertura)."),
        new("Plataforma", "NOGO-PARCIAL", "Escenarios de no-go parcial (qué hace la ventanilla si Cartera o Aportes no se activan) con su plan de reversa, aprobados y adjuntos."),
        new("Plataforma", "PARALELO", "Informe del paralelo en QA con cada diferencia explicada (vista parallel-comparison)."),
        new("Plataforma", "PRODUCCION-LIMPIA", "Producción no tiene ningún dato del paralelo."),
        new("Plataforma", "RESPALDOS", "Respaldo por base (pg_dump de cada cooperativa y de la administrativa) antes del despliegue."),
        new("Plataforma", "CAPACITACION", "Cajeros, supervisores, analistas y tesoreros capacitados con los manuales de R0/T0."),
    ];

    public static string Estado(int estado) => estado switch
    {
        Abierta => "Abierta",
        Go => "Salir (Go)",
        NoGo => "No salir (No-go)",
        SalidaParcial => "Salida parcial",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public static string EstadoDeCondicion(int estado) => estado switch
    {
        Verde => "Verde",
        Rojo => "Rojo",
        NoAplica => "No aplica",
        _ => estado.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public Task<ResultadoDeInventario<List<ListaDeGoNoGoDto>>> ListarAsync(CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<List<ListaDeGoNoGoDto>>(http, auth, HttpMethod.Get, Ruta, null, null, null, ct);

    public Task<ResultadoDeInventario<ListaDeGoNoGoDto>> ObtenerAsync(Guid lista, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<ListaDeGoNoGoDto>(http, auth, HttpMethod.Get, $"{Ruta}/{lista}", null, null, null, ct);

    public Task<ResultadoDeInventario<ListaDeGoNoGoDto>> CrearAsync(CrearListaDeGoNoGoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<ListaDeGoNoGoDto>(http, auth, HttpMethod.Post, Ruta, r, clave, null, ct);

    public Task<ResultadoDeInventario<ListaDeGoNoGoDto>> VerificarAsync(Guid lista, Guid condicion, VerificarCondicionRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<ListaDeGoNoGoDto>(http, auth, HttpMethod.Post, $"{Ruta}/{lista}/items/{condicion}/check", r, clave, null, ct);

    public Task<ResultadoDeInventario<ListaDeGoNoGoDto>> AprobarRojoAsync(Guid lista, Guid condicion, MotivoDeGoNoGoRequest r, ClaveDeOperacion clave,
        CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<ListaDeGoNoGoDto>(http, auth, HttpMethod.Post, $"{Ruta}/{lista}/items/{condicion}/override", r, clave, null, ct);

    public Task<ResultadoDeInventario<ListaDeGoNoGoDto>> DecidirAsync(Guid lista, DecidirGoNoGoRequest r, ClaveDeOperacion clave, CancellationToken ct = default) =>
        EnvioDeRecaudo.EnviarAsync<ListaDeGoNoGoDto>(http, auth, HttpMethod.Post, $"{Ruta}/{lista}/decision", r, clave, null, ct);
}
