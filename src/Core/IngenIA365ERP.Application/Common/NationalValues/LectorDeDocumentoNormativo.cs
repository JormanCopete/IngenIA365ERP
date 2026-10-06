using System.Collections.Concurrent;
using IngenIA365ERP.Application.Common.Alerts;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Saas.NationalValues;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Core.NationalValues;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IngenIA365ERP.Application.Common.NationalValues;

/// <summary><see cref="INormativaVigente"/> sobre <c>COR_RegulatoryDocuments</c>. (nuevo)</summary>
public sealed class LectorDeDocumentoNormativo(
    IApplicationDbContext db,
    IAlertas? alertas = null,
    ILogger<LectorDeDocumentoNormativo>? logger = null) : INormativaVigente
{
    /// <summary>Lo ya interpretado, por huella y tipo: el contenido de una huella no cambia nunca.</summary>
    private static readonly ConcurrentDictionary<(string Sha256, Type Tipo, short Esquema), object> Memoria = new();

    private readonly ILogger _logger = logger ?? (ILogger)NullLogger.Instance;

    public Task<Result<DocumentoNormativoVigente<CatalogoDeModalidades>>> CatalogoDeModalidadesAsync(DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default) =>
        LeerAsync(RegulatoryDocumentKind.CatalogoDeModalidades, fecha, CatalogoDeModalidades.Leer, informacionHasta, ct);

    public Task<Result<DocumentoNormativoVigente<TiposDeOrganizacion>>> TiposDeOrganizacionAsync(DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default) =>
        LeerAsync(RegulatoryDocumentKind.TiposDeOrganizacion, fecha, TiposDeOrganizacion.Leer, informacionHasta, ct);

    public async Task<Result<DocumentoNormativoVigente<T>>> LeerAsync<T>(
        RegulatoryDocumentKind clase, DateOnly fecha, Func<string, short, ResultadoDeDocumento<T>> leer,
        DateTime? informacionHasta = null, CancellationToken ct = default) where T : class
    {
        var nombre = clase.ToString();
        var versiones = await db.RegulatoryDocuments.AsNoTracking()
            .Where(d => d.Kind == nombre && d.ValidFrom <= fecha && !d.IsDeleted)
            .ToListAsync(ct);
        var vigente = VigenciaBitemporal.Vigente(versiones, fecha, informacionHasta ?? DateTime.MaxValue);
        if (vigente is null)
        {
            var error = ErroresDeValoresNacionales.Faltante(nombre, string.Empty, fecha);
            await LevantarFaltanteAsync(nombre, fecha, error.Message, ct);
            return Result.Failure<DocumentoNormativoVigente<T>>(error);
        }

        var clave = (vigente.ContentSha256, typeof(T), vigente.SchemaVersion);
        if (!Memoria.TryGetValue(clave, out var leido))
        {
            var resultado = leer(vigente.Content, vigente.SchemaVersion);
            if (!resultado.EsValido)
                return Result.Failure<DocumentoNormativoVigente<T>>(ErroresDeValoresNacionales.DocumentoInvalido(resultado.Errores));
            leido = Memoria.GetOrAdd(clave, resultado.Documento!);
        }

        var revisiones = await RevisionesDeCopias.UltimasAsync(db, [vigente.PublicId], ct);
        return Result.Success(new DocumentoNormativoVigente<T>(
            vigente.PublicId, vigente.SourcePublicId, clase, vigente.Code, vigente.ValidFrom, vigente.SchemaVersion, vigente.Verified,
            vigente.ContentSha256, RevisionesDeCopias.Estado(revisiones.GetValueOrDefault(vigente.PublicId)), (T)leido));
    }

    private async Task LevantarFaltanteAsync(string clase, DateOnly fecha, string mensaje, CancellationToken ct)
    {
        var alerta = new AlertaALevantar(AlertasDeValoresNacionales.ValorNacionalFaltante, $"Falta el documento {clase}", mensaje,
            EntityType: nameof(RegulatoryDocument), DedupKey: AlertasDeValoresNacionales.ClaveDeFaltante(clase, string.Empty, fecha));
        if (alertas is null)
        {
            _logger.LogWarning("[ValoresNacionales.Alerta] {Tipo}: {Cuerpo}", alerta.TypeCode, alerta.Body);
            return;
        }
        try
        {
            var r = await alertas.LevantarAsync(alerta, ct);
            if (r.IsFailure) _logger.LogWarning("[ValoresNacionales.AlertaNoLevantada] {Tipo}: {Codigo}", alerta.TypeCode, r.Error.Code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "[ValoresNacionales.AlertaNoLevantada] {Tipo}", alerta.TypeCode);
        }
    }
}
