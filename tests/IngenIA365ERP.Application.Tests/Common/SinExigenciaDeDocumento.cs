using IngenIA365ERP.Application.Identity.Auth.Documento;
using IngenIA365ERP.Domain.Entities.Admin;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// Feature 017: la exigencia de documento de una cooperativa sin fecha, que es el defecto. Las pruebas de
/// las puertas que no tratan del documento la usan para que nada cambie respecto de antes.
/// </summary>
public sealed class SinExigenciaDeDocumento : IExigenciaDeDocumento
{
    public static readonly SinExigenciaDeDocumento Instancia = new();

    public Task<GuardiaDeDocumento.Veredicto> EvaluarAsync(CentralUser persona, IEnumerable<Guid> cooperativas, CancellationToken ct) =>
        Task.FromResult(GuardiaDeDocumento.Veredicto.Admite);
}
