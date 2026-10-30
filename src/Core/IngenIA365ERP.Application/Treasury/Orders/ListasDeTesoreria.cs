using IngenIA365ERP.Application.Common.Compliance;
using IngenIA365ERP.Application.Common.ExternalChannels;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.People.Services;
using IngenIA365ERP.Domain.Enums.Compliance;
using IngenIA365ERP.Domain.Enums.ExternalChannels;
using Microsoft.EntityFrameworkCore;
using ScreeningContext = IngenIA365ERP.Domain.Enums.Lending.ScreeningContext;

namespace IngenIA365ERP.Application.Treasury.Orders;

/// <summary>Lo que dicen las listas de las personas de un pago: quiénes coinciden y si alguna coincidencia es vinculante. (nuevo)</summary>
public sealed record ResultadoDeListasDeTesoreria(IReadOnlyList<int> Vinculantes, IReadOnlyList<int> NoVinculantes)
{
    public static ResultadoDeListasDeTesoreria Limpio { get; } = new([], []);

    public bool HayVinculante => Vinculantes.Count > 0;
}

/// <summary>
/// La consulta a listas de los beneficiarios y titulares de las órdenes de pago (feature 015, US10 y US26, T0142, T0167; FR-109, FR-203; mensajes.md §5.3),
/// en el contexto <see cref="ScreeningContext.Disbursement"/>: una coincidencia vinculante impide la orden —no entra a un lote aprobado— y no es parametrizable a
/// «sigue» (<c>EfectoDeListasPorContexto.LaVinculanteBloqueaSiempre</c>). Sólo lee: Tesorería no guarda los resultados de Cartera. (nuevo)
/// </summary>
public interface IListasDeTesoreria
{
    Task<ResultadoDeListasDeTesoreria> EvaluarAsync(IReadOnlyCollection<int> personIds, CancellationToken ct);
}

/// <summary>
/// La implementación sobre la consulta común de la plataforma (<see cref="IProveedoresDeListas"/>) por el canal vigente de la cooperativa (sin configuración,
/// <c>CARGA-PROPIA</c>, como Cartera). La plataforma no distingue todavía la lista vinculante de las demás: una coincidencia exacta (<c>Match</c>) cuenta como
/// <b>vinculante</b> y una posible (<c>PossibleMatch</c>) como no vinculante (alerta y sigue). Lo decide el dueño con el oficial (DT §2.7). Un canal caído o el
/// simulado en producción no cuentan como coincidencia. (nuevo)
/// </summary>
public sealed class ListasDeTesoreria(
    IApplicationDbContext db,
    IProveedoresDeListas proveedores,
    ICredencialesDeCanalExterno credenciales,
    ICurrentTenantService tenant,
    IAmbienteDeDespliegue ambiente,
    IDateTimeService reloj) : IListasDeTesoreria
{
    public const ScreeningContext Contexto = ScreeningContext.Disbursement;

    public async Task<ResultadoDeListasDeTesoreria> EvaluarAsync(IReadOnlyCollection<int> personIds, CancellationToken ct)
    {
        if (personIds.Count == 0) return ResultadoDeListasDeTesoreria.Limpio;
        var hoy = reloj.HoyLocal;
        var canal = await CanalVigenteAsync(hoy, ct);
        var contexto = await ContextoAsync(canal, ct);
        if (contexto is null) return ResultadoDeListasDeTesoreria.Limpio;
        var personas = await db.People.AsNoTracking().IgnoreQueryFilters().Where(p => personIds.Contains(p.Id))
            .Select(p => new { p.Id, p.PublicId, p.IdType, p.TaxId, p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName }).ToListAsync(ct);
        var vinculantes = new List<int>();
        var noVinculantes = new List<int>();
        foreach (var p in personas.OrderBy(p => p.Id))
        {
            var r = await proveedores.Resolver(canal).ConsultarAsync(new ConsultaDeListas(p.PublicId, p.IdType ?? string.Empty, p.TaxId ?? string.Empty,
                PersonFactory.NombreVisible(p.FirstName, p.OtherNames, p.LastName, p.SecondLastName, p.BusinessName)), contexto, ct);
            if (r.Outcome != ExternalOutcome.Ok || r.Valor is not { } valor) continue;
            if (ambiente.EsProduccion && (r.Simulado || CodigosDeCanalExterno.EsSimulado(r.ChannelCode))) continue;
            if (valor.Resultado == ScreeningOutcome.Match) vinculantes.Add(p.Id);
            else if (valor.Resultado == ScreeningOutcome.PossibleMatch) noVinculantes.Add(p.Id);
        }
        return new ResultadoDeListasDeTesoreria(vinculantes, noVinculantes);
    }

    private async Task<string> CanalVigenteAsync(DateOnly fecha, CancellationToken ct)
    {
        var configurados = await db.ExternalChannelSettings.AsNoTracking().Where(s => s.Kind == ExternalChannelKind.RestrictiveLists && !s.IsDeleted).ToListAsync(ct);
        return configurados.Where(s => s.VigenteEn(fecha)).OrderByDescending(s => s.ValidFrom).Select(s => s.ChannelCode).FirstOrDefault()
               ?? CodigosDeCanalExterno.CargaPropia;
    }

    private async Task<ContextoDeCanalExterno?> ContextoAsync(string canal, CancellationToken ct)
    {
        var cooperativa = CooperativaDelCanal.Resuelta(tenant) ?? Guid.Empty;
        CredencialDeCanalExterno? credencial = null;
        if (!CodigosDeCanalExterno.NoPideCredencial(canal))
        {
            var resuelta = await credenciales.ResolverAsync(canal, ct);
            if (resuelta.IsFailure) return null;
            credencial = resuelta.Value;
        }
        return new ContextoDeCanalExterno(cooperativa, canal, null, credencial, Guid.NewGuid().ToString("N"), !ambiente.EsProduccion);
    }
}
