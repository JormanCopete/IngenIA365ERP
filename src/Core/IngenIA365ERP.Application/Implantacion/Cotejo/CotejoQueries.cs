using System.Security.Cryptography;
using System.Text.Json;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Reports;
using MediatR;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Implantacion.Cotejo;

/// <summary>Lo que se pide en un cotejo además del archivo: el corte, si los saldos contables van por naturaleza o débito menos crédito, y la tolerancia en pesos.</summary>
public sealed record ParametrosDeCotejo(DateOnly Corte, bool SignoDebitoMenosCredito, decimal Tolerancia);

/// <summary>Las partidas de los dos lados, ya con llaves únicas, la definición y las notas que la fuente quiere en el informe.</summary>
public sealed record CotejoPreparado(DefinicionDeCotejo Definicion, IReadOnlyList<PartidaDeCotejo> Erp, IReadOnlyList<PartidaDeCotejo> Referencia,
    IReadOnlyList<string> Notas);

/// <summary>
/// Una opción del cotejo (nuevo): qué columnas trae su archivo, qué permiso la abre y cómo arma las partidas del ERP al corte y las de la
/// referencia. Una implementación por opción, registrada en el contenedor; <see cref="CotejarCifrasQuery"/> elige por <see cref="Opcion"/>.
/// </summary>
public interface IFuenteDeCotejo
{
    /// <summary>La clave de la ruta: <c>cuentas</c>, <c>terceros</c>, <c>creditos</c>, <c>cuentas-de-asociados</c>, <c>asociados</c>.</summary>
    string Opcion { get; }

    string Titulo { get; }

    /// <summary>El permiso de la opción; lo exige la ruta.</summary>
    string Permiso { get; }

    IReadOnlyList<ColumnaDeReferencia> Columnas { get; }

    IReadOnlyList<string> Instrucciones { get; }

    Task<Result<CotejoPreparado>> PrepararAsync(ArchivoDeReferencia archivo, ParametrosDeCotejo parametros, CancellationToken ct);
}

/// <summary>Las opciones del cotejo, sus permisos y sus errores. (nuevo)</summary>
public static class OpcionesDeCotejo
{
    public const string Cuentas = "cuentas";
    public const string Terceros = "terceros";
    public const string Creditos = "creditos";
    public const string CuentasDeAsociados = "cuentas-de-asociados";
    public const string Asociados = "asociados";

    public const string PermisoContabilidad = "Implantacion.Cotejo.Contabilidad";
    public const string PermisoCartera = "Implantacion.Cotejo.Cartera";
    public const string PermisoCaptaciones = "Implantacion.Cotejo.Captaciones";
    public const string PermisoAsociados = "Implantacion.Cotejo.Asociados";

    /// <summary>Cada opción con su permiso: la API registra una ruta por opción con el suyo.</summary>
    public static readonly IReadOnlyDictionary<string, string> Permisos = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [Cuentas] = PermisoContabilidad,
        [Terceros] = PermisoContabilidad,
        [Creditos] = PermisoCartera,
        [CuentasDeAsociados] = PermisoCaptaciones,
        [Asociados] = PermisoAsociados,
    };

    public const string Resumen = "resumen";
    public const string Detalle = "detalle";

    public static readonly Error OpcionDesconocida = new("Implantacion.Cotejo.NotFound", "La opción de cotejo no existe.");
    public static readonly Error NivelInvalido = new("Validation.Cotejo.Nivel", "El nivel va «resumen» o «detalle».");
    public static readonly Error ToleranciaInvalida = new("Validation.Cotejo.Tolerancia", "La tolerancia va en pesos, de 0 a 1.000.");
    public static readonly Error CorteInvalido = new("Validation.Cotejo.Corte", "El corte no puede ser posterior a hoy.");
}

/// <summary>La plantilla de una opción (encabezados en la fila 1 e instrucciones en otra hoja; la API la vuelve <c>.xlsx</c>).</summary>
public sealed record GetCotejoTemplateQuery(string Opcion) : IRequest<Result<TablaExportable>>;

public sealed class GetCotejoTemplateQueryHandler(IEnumerable<IFuenteDeCotejo> fuentes) : IRequestHandler<GetCotejoTemplateQuery, Result<TablaExportable>>
{
    public Task<Result<TablaExportable>> Handle(GetCotejoTemplateQuery request, CancellationToken ct)
    {
        var fuente = fuentes.FirstOrDefault(f => f.Opcion == request.Opcion);
        return Task.FromResult(fuente is null
            ? Result.Failure<TablaExportable>(OpcionesDeCotejo.OpcionDesconocida)
            : Result.Success(LecturaDeReferencia.Plantilla(fuente.Titulo, fuente.Columnas, fuente.Instrucciones)));
    }
}

/// <summary>
/// Coteja el archivo de cifras de referencia de una opción contra el ERP al corte (2026-11-28, implantación; nuevo). <b>Sin estado</b>: no guarda
/// el archivo ni el resultado; volver a cotejar es volver a subirlo. Devuelve el resumen por grupo o el detalle por partida (de un grupo o de
/// todos, opcionalmente sólo lo que no cuadra) como <see cref="TablaExportable"/>, que la ruta entrega en json o en archivo. Cada cotejo deja el
/// evento <c>Implantacion.Cotejo.Realizado</c> con el nombre del archivo, su huella SHA-256, el corte, las filas y cuántas partidas quedaron en
/// cada estado.
/// </summary>
public sealed record CotejarCifrasQuery(
    string Opcion,
    string NombreArchivo,
    byte[] Contenido,
    DateOnly? Corte,
    string? Nivel,
    bool SoloDiferencias,
    string? Grupo,
    bool SignoDebitoMenosCredito,
    decimal Tolerancia,
    string? Formato) : IRequest<Result<TablaExportable>>;

public sealed class CotejarCifrasQueryHandler(
    IEnumerable<IFuenteDeCotejo> fuentes,
    ITabularFileReader lector,
    IDateTimeService clock,
    CotejoAuditEmitter audit) : IRequestHandler<CotejarCifrasQuery, Result<TablaExportable>>
{
    public async Task<Result<TablaExportable>> Handle(CotejarCifrasQuery request, CancellationToken ct)
    {
        var fuente = fuentes.FirstOrDefault(f => f.Opcion == request.Opcion);
        if (fuente is null) return Result.Failure<TablaExportable>(OpcionesDeCotejo.OpcionDesconocida);
        var nivel = string.IsNullOrWhiteSpace(request.Nivel) ? OpcionesDeCotejo.Resumen : request.Nivel.Trim().ToLowerInvariant();
        if (nivel is not (OpcionesDeCotejo.Resumen or OpcionesDeCotejo.Detalle)) return Result.Failure<TablaExportable>(OpcionesDeCotejo.NivelInvalido);
        if (request.Tolerancia is < 0m or > 1000m) return Result.Failure<TablaExportable>(OpcionesDeCotejo.ToleranciaInvalida);
        var hoy = clock.HoyLocal;
        var corte = request.Corte ?? hoy;
        if (corte > hoy) return Result.Failure<TablaExportable>(OpcionesDeCotejo.CorteInvalido);

        var tabla = await lector.LeerAsync(request.Contenido, request.NombreArchivo, 1, ct);
        if (tabla.IsFailure) return Result.Failure<TablaExportable>(tabla.Error);
        var archivo = LecturaDeReferencia.Leer(tabla.Value, fuente.Columnas);
        if (archivo.IsFailure) return Result.Failure<TablaExportable>(archivo.Error);

        var preparado = await fuente.PrepararAsync(archivo.Value, new ParametrosDeCotejo(corte, request.SignoDebitoMenosCredito, request.Tolerancia), ct);
        if (preparado.IsFailure) return Result.Failure<TablaExportable>(preparado.Error);
        var p = preparado.Value;
        var resultado = MotorDeCotejo.Comparar(p.Definicion, p.Erp, p.Referencia);

        var huella = Convert.ToHexString(SHA256.HashData(request.Contenido)).ToLowerInvariant();
        var notas = new List<string>
        {
            $"Archivo: {request.NombreArchivo} ({archivo.Value.Filas.Count:N0} filas; huella SHA-256 {huella[..16]}…).",
            $"Cifras del ERP al {corte:dd/MM/yyyy}.",
            $"Partidas: {resultado.Partidas.Count:N0}. Cuadran {resultado.Contar(EstadoDeCotejo.Cuadra):N0}; con diferencia {resultado.Contar(EstadoDeCotejo.Diferencia):N0}; "
            + $"sólo en el ERP {resultado.Contar(EstadoDeCotejo.SoloEnElErp):N0}; sólo en la referencia {resultado.Contar(EstadoDeCotejo.SoloEnLaReferencia):N0}.",
        };
        if (request.Tolerancia > 0m) notas.Add($"Tolerancia: una diferencia de hasta {request.Tolerancia:N2} pesos por importe se toma como cuadrada.");
        notas.AddRange(p.Notas);
        notas.Add("Diferencia = ERP − referencia. Una partida en cero de un lado y ausente del otro cuadra.");

        var subtitulo = $"Corte al {corte:dd/MM/yyyy} · {request.NombreArchivo}";
        var informe = nivel == OpcionesDeCotejo.Resumen
            ? MotorDeCotejo.Resumen(resultado, subtitulo, notas)
            : MotorDeCotejo.Detalle(resultado, subtitulo, notas, request.SoloDiferencias, request.Grupo);

        await audit.EmitirAsync(new
        {
            option = fuente.Opcion,
            file = request.NombreArchivo,
            sha256 = huella,
            cutoff = corte,
            rows = archivo.Value.Filas.Count,
            level = nivel,
            group = request.Grupo,
            onlyDifferences = request.SoloDiferencias,
            tolerance = request.Tolerancia,
            format = FormatosDeInforme.Normalizar(request.Formato),
            items = resultado.Partidas.Count,
            matched = resultado.Contar(EstadoDeCotejo.Cuadra),
            different = resultado.Contar(EstadoDeCotejo.Diferencia),
            onlyInErp = resultado.Contar(EstadoDeCotejo.SoloEnElErp),
            onlyInReference = resultado.Contar(EstadoDeCotejo.SoloEnLaReferencia),
        }, ct);
        return Result.Success(informe);
    }
}

/// <summary>
/// El evento <c>Implantacion.Cotejo.Realizado</c> (molde de <c>AccountingAuditEmitter</c>; nuevo). El cotejo es una consulta y el
/// <c>AuditBehavior</c> sólo ve comandos: se emite aquí. Va a la base de auditoría de la cooperativa por su PublicId; si la escritura falla, no
/// tumba el cotejo y queda en el log.
/// </summary>
public sealed class CotejoAuditEmitter(
    IAuditAppendOnlyWriter writer,
    ICurrentUserService currentUser,
    ICurrentTenantService tenant,
    IDateTimeService clock,
    ILogger<CotejoAuditEmitter> logger)
{
    public const string Modulo = Common.Audit.ModuloDeAuditoria.Implantacion;
    public const string Accion = "Implantacion.Cotejo.Realizado";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task EmitirAsync(object detalle, CancellationToken ct)
    {
        var cooperativa = tenant.TenantId;
        if (string.IsNullOrWhiteSpace(cooperativa))
            throw new InvalidOperationException("El cotejo de cifras de referencia no tiene cooperativa resuelta: su auditoría nunca va a la base global.");
        try
        {
            await writer.AppendAsync(new AuditEventDocument(
                TenantId: cooperativa,
                UserId: currentUser.UserId?.ToString() ?? string.Empty,
                UserName: currentUser.UserName,
                Action: Accion,
                EntityType: "CifrasDeReferencia",
                EntityPublicId: null,
                Module: Modulo,
                OldValuesJson: null,
                NewValuesJson: JsonSerializer.Serialize(detalle, Json),
                ChangedFields: null,
                IpAddress: null,
                UserAgent: null,
                Endpoint: null,
                HttpMethod: null,
                HttpStatusCode: null,
                DurationMs: null,
                OccurredAt: clock.UtcNow), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "La auditoría del cotejo de cifras de referencia falló");
        }
    }
}
