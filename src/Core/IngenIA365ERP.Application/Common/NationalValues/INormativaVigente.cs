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

/// <summary>
/// El único lector de los documentos normativos de la cooperativa (feature 013, T40, T45; data-model parte 3 §4.2): toma de
/// <c>COR_RegulatoryDocuments</c> la versión que rige a la fecha con la regla bitemporal, la valida con el lector tipado de su
/// clase y la entrega con su <c>PublicId</c> y su SHA-256 para sellarlos (cierre, SICSES, explicación del crédito). El documento
/// leído se memoriza por huella: dos lecturas del mismo contenido no lo vuelven a interpretar. (nuevo)
/// </summary>
public interface INormativaVigente
{
    /// <summary>El documento de <paramref name="clase"/> vigente el <paramref name="fecha"/>, leído con <paramref name="leer"/>.</summary>
    Task<Result<DocumentoNormativoVigente<T>>> LeerAsync<T>(
        RegulatoryDocumentKind clase, DateOnly fecha, Func<string, short, ResultadoDeDocumento<T>> leer,
        DateTime? informacionHasta = null, CancellationToken ct = default) where T : class;

    /// <summary>El catálogo de modalidades vigente (<see cref="CatalogoDeModalidades.Leer"/>).</summary>
    Task<Result<DocumentoNormativoVigente<CatalogoDeModalidades>>> CatalogoDeModalidadesAsync(DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default);

    /// <summary>Los tipos de organización vigentes (<see cref="TiposDeOrganizacion.Leer"/>).</summary>
    Task<Result<DocumentoNormativoVigente<TiposDeOrganizacion>>> TiposDeOrganizacionAsync(DateOnly fecha, DateTime? informacionHasta = null, CancellationToken ct = default);
}

/// <summary>Un documento normativo vigente, tipado, con lo que se sella de él.</summary>
/// <param name="PublicId">El de la copia en esta cooperativa.</param>
/// <param name="SourcePublicId">El de la fila central.</param>
/// <param name="Sha256">La huella del contenido canónico.</param>
public sealed record DocumentoNormativoVigente<T>(
    Guid PublicId,
    Guid SourcePublicId,
    RegulatoryDocumentKind Clase,
    string Codigo,
    DateOnly ValidFrom,
    short SchemaVersion,
    bool Verified,
    string Sha256,
    EstadoDeRevision Estado,
    T Documento) where T : class;
