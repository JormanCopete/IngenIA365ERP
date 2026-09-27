using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;

namespace IngenIA365ERP.Application.Accounting.Inventory.Consultas;

/// <summary>
/// «¿Es contabilizable?» (feature 012, T515; contracts/contabilidad.md §4.2; FR-074; SC-021), detrás de
/// <see cref="IContabilidadParaInventario.EvaluarAsync"/>. Agrupa los mensajes tal como se emitirían en unidades, arma cada
/// comprobante con el <b>mismo</b> <see cref="ConstructorDeLineasDeInventario"/> del consumidor y los somete a
/// <see cref="AccountingPoster.ValidarVariosAsync"/>, más las comprobaciones de la matriz (regla faltante, tarifa distinta,
/// tipo de comprobante) y los invariantes del contenido. No numera, no agrega y no deja nada rastreado: corre dentro de la
/// confirmación de Inventario, antes del cerrojo.
///
/// <para>
/// Sólo evalúa lo que pasaría a Contabilidad: mensajes de negocio con destino Contabilidad. Los informativos y los de Cartera se
/// ignoran; qué documentos pasan (modo sellado) lo decide Inventario antes de preguntar. Los avisos
/// (<c>Accounting.Line.TaxAmountDiffers</c>) no impiden. Cada hallazgo dice quién lo corrige (<see cref="QuienCorrige"/>). (nuevo)
/// </para>
/// </summary>
public sealed record EvaluateInventoryPostingQuery(IReadOnlyList<MensajeContableDto> Mensajes) : IRequest<Result<ResultadoDeContabilizacionDto>>;

public sealed class EvaluateInventoryPostingQueryValidator : AbstractValidator<EvaluateInventoryPostingQuery>
{
    public EvaluateInventoryPostingQueryValidator()
    {
        RuleFor(x => x.Mensajes).NotNull();
        RuleForEach(x => x.Mensajes).Must(m => m?.Envelope is not null).WithMessage("Cada mensaje lleva su sobre.");
    }
}

public sealed class EvaluateInventoryPostingQueryHandler(
    IApplicationDbContext db, AccountingPoster poster, ResolutorDeReglas resolutor, TiposDeComprobanteDeInventario tipos, IDateTimeService reloj)
    : IRequestHandler<EvaluateInventoryPostingQuery, Result<ResultadoDeContabilizacionDto>>
{
    public async Task<Result<ResultadoDeContabilizacionDto>> Handle(EvaluateInventoryPostingQuery request, CancellationToken ct)
    {
        var unidades = request.Mensajes
            .Where(m => m.Envelope.Kind == IntegrationMessageKind.Business && ConstructorDeLineasDeInventario.VaAContabilidad(m.Envelope.Type))
            .Select(m => new MensajeDeUnidad(m.Envelope, m.Payload))
            .GroupBy(m => (m.Sobre.Origin.PublicId, m.Sobre.OriginEventKey))
            .Select(g => (IReadOnlyList<MensajeDeUnidad>)g.ToList())
            .ToList();
        var errores = new List<HallazgoContableDto>();
        var avisos = new List<HallazgoContableDto>();
        if (unidades.Count == 0) return Result.Success(new ResultadoDeContabilizacionDto(true, errores, avisos));

        var catalogos = await CatalogosDelConstructor.CargarAsync(db, resolutor, tipos, unidades, reloj.TodayUtc, ct);
        var construidas = new List<ConstruccionDeUnidad>();
        foreach (var unidad in unidades)
        {
            var clase = unidad[0].Sobre.Origin.DocumentClass;
            foreach (var m in unidad.Where(m => m.Sobre.Currency != "COP" || m.Sobre.ExchangeRate != 1m))
            {
                var error = AccountingErrors.InventoryMessageCurrencyNotSupported(m.Sobre.Currency, m.Sobre.ExchangeRate);
                errores.Add(new HallazgoContableDto(m.Tipo, [], null, error.Code, error.Message, QuienCorrige.De(error.Code, clase)));
            }
            var c = ConstructorDeLineasDeInventario.Construir(unidad, catalogos);
            if (!c.EsValida)
            {
                errores.AddRange(c.Fallos.Select(f => new HallazgoContableDto(f.MessageType, f.DocumentLines, f.AccountCode, f.Error.Code, f.Error.Message,
                    QuienCorrige.De(f.Error.Code, clase, f.DeLaCuenta))));
                continue;
            }
            if (!c.ValorCero) construidas.Add(c);
        }

        var validaciones = await poster.ValidarVariosAsync(construidas.Select(c => c.Request!).ToList(), ct);
        for (var i = 0; i < construidas.Count; i++)
        {
            var c = construidas[i];
            var clase = c.Principal.Sobre.Origin.DocumentClass;
            errores.AddRange(validaciones[i].Errores.Select(e => Hallazgo(e, c, clase)));
            avisos.AddRange(validaciones[i].Avisos.Select(e => Hallazgo(e, c, clase)));
        }
        return Result.Success(new ResultadoDeContabilizacionDto(errores.Count == 0, errores, avisos));
    }

    /// <summary>Un error de línea del contrato, dicho con el mensaje, las líneas del documento y la cuenta de su línea contable.</summary>
    public static HallazgoContableDto Hallazgo(ErrorDeLinea e, ConstruccionDeUnidad c, Domain.Enums.Inventory.DocumentClass? clase)
    {
        var linea = e.LineNumber >= 1 && e.LineNumber <= c.Mapa.Count ? c.Mapa[e.LineNumber - 1] : null;
        return new HallazgoContableDto(linea?.MessageType ?? c.Principal.Tipo, linea?.DocumentLines ?? [], e.AccountCode ?? linea?.AccountCode,
            e.Code, e.Message, QuienCorrige.De(e.Code, clase));
    }
}
