using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Inventory.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Receipts.Contabilizacion;

/// <summary>
/// 2026-11-26 (pedido del dueño; <see cref="ReciboEnUnSoloComprobante"/>): el <b>recibo de caja en un solo comprobante</b>. Junta la unidad de Recaudo del
/// recibo (el RR: caja, medios y conceptos varios contra el puente) con las de Cartera que ese recibo abona (créditos, aportes, ahorro: el puente contra
/// cada obligación) y las contabiliza juntas, por <see cref="AccountingPoster"/>, en <b>un</b> comprobante del tipo del recibo de caja, con origen el recibo
/// y fecha la contable del recibo. Las líneas no se suman y el puente queda a la vista: entra y sale en el mismo comprobante. Cada línea se valida con el
/// módulo que la manda (<see cref="PostingLine.Module"/>). Los recibos de <c>ACC_ReceiptsPostings</c> y de <c>ACC_LendingPostings</c> nombran el mismo
/// comprobante; un <c>SaveChanges</c>. <b>Sin ruta ni permisos</b>: lo envía el despachador por <c>DestinoContabilidad</c>.
///
/// <para>
/// <b>Todo o nada.</b> Si algo no permite juntarlas —una unidad ya procesada, que espera a su original, que no se arma, que no lleva líneas, de otra fecha,
/// o un comprobante que el contrato rechaza— no se guarda nada y responde <see cref="CodigoNoSeJunta"/>: el destino las consume como siempre, cada
/// módulo con su comprobante (el RR de Recaudo y el de Cartera), y lo que falle se rechaza con su motivo de siempre. Nunca queda medio recibo en un
/// comprobante. (nuevo)
/// </para>
/// </summary>
public sealed record PostCashReceiptVoucherCommand(string GroupKey, IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<IReadOnlyList<ResultadoDeUnidad>>>, IReintentableAnteConcurrencia
{
    /// <summary>Lo que dice que no se juntaron: el destino las consume por separado. <c>data.lendingGroupKey</c>: la clave de Cartera por recibo, si aplica.</summary>
    public const string CodigoNoSeJunta = "Accounting.CashReceipt.NotJoined";
}

/// <summary>Los datos de <see cref="PostCashReceiptVoucherCommand.CodigoNoSeJunta"/>: la clave de Cartera por recibo para sus abonos, si los junta.</summary>
public sealed record ReciboNoSeJunta(string? LendingGroupKey);

public sealed class PostCashReceiptVoucherCommandValidator : AbstractValidator<PostCashReceiptVoucherCommand>
{
    public PostCashReceiptVoucherCommandValidator()
    {
        RuleFor(x => x.GroupKey).NotEmpty().MaximumLength(200).Must(ReciboEnUnSoloComprobante.EsClaveDeGrupo).WithMessage("No es la clave de un recibo de caja.");
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("El recibo no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PostCashReceiptVoucherCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeModulo consumoDeRecaudo,
    ConsumoDeCartera consumoDeCartera,
    AccountingPoster poster,
    ResolutorDeLaMatriz resolutorDeRecaudo,
    ResolutorDeReglasDeCartera resolutorDeCartera,
    TiposDeComprobanteDeCartera tiposDeCartera,
    IDateTimeService reloj) : IRequestHandler<PostCashReceiptVoucherCommand, Result<IReadOnlyList<ResultadoDeUnidad>>>
{
    private static readonly ModuloDeLaMatriz Recaudo = MatricesDeRecaudoYTesoreria.Recaudo;

    public async Task<Result<IReadOnlyList<ResultadoDeUnidad>>> Handle(PostCashReceiptVoucherCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<ResultadoDeUnidad>>(leidos.Error);
        var unidades = AgrupadorDeResumidos.Unidades(leidos.Value, IntegrationDestinations.Accounting);
        var deRecaudo = unidades.Where(u => u.Unidad.OriginModule == ModulosDeOrigen.Recaudo).ToList();
        var deCartera = unidades.Where(u => u.Unidad.OriginModule == ModulosDeOrigen.Cartera).ToList();
        // Si no se juntan, los abonos de Cartera van juntos por recibo sólo si la empresa también lo pidió (ComprobantePorRecibo).
        var claveDeCartera = deCartera.Count > 1 && ComprobantePorRecibo.Encendido(await db.SystemSettings.AsNoTracking()
                .Where(s => s.SettingKey == ComprobantePorRecibo.Clave).Select(s => s.SettingValue).FirstOrDefaultAsync(ct))
            ? ComprobantePorRecibo.ClaveDe(deCartera[0].Mensajes)
            : null;
        Result<IReadOnlyList<ResultadoDeUnidad>> NoSeJunta(string motivo) => Result.Failure<IReadOnlyList<ResultadoDeUnidad>>(new ErrorConDatos(
            PostCashReceiptVoucherCommand.CodigoNoSeJunta, $"El recibo no se contabiliza en un solo comprobante: {motivo}", new ReciboNoSeJunta(claveDeCartera)));

        if (deRecaudo.Count != 1 || deCartera.Count == 0 || deRecaudo.Count + deCartera.Count != unidades.Count)
            return NoSeJunta("no es una unidad de Recaudo con las de Cartera de su recibo.");
        var (unidadDeCaja, mensajesDeCaja) = deRecaudo[0];

        // Nada procesado ni esperando: si no, cada módulo dice lo suyo.
        if (await consumoDeRecaudo.RevisarAsync(Recaudo, mensajesDeCaja, ct) is not null) return NoSeJunta("la parte de Recaudo no está lista.");
        foreach (var (_, mensajes) in deCartera)
        {
            if (await consumoDeCartera.RevisarAsync(mensajes, ct) is not null) return NoSeJunta("una parte de Cartera no está lista.");
            if (ConstructorDeLineasDeCartera.EsInformativa(mensajes.Select(HechoDeCartera.De).ToList())) return NoSeJunta("una parte de Cartera es informativa.");
        }

        // Cada parte, como si fuera sola.
        var hechosDeCaja = mensajesDeCaja.Select(HechoContableDeModulo.De).ToList();
        var caja = ConstructorDeComprobanteDeModulo.Construir(Recaudo, hechosDeCaja, await consumoDeRecaudo.CatalogosAsync(Recaudo, resolutorDeRecaudo, hechosDeCaja, ct));
        if (!caja.EsValida || caja.SinLineas) return NoSeJunta("la parte de Recaudo no se arma.");
        var comoHechos = deCartera.Select(x => (IReadOnlyList<HechoDeCartera>)x.Mensajes.Select(HechoDeCartera.De).ToList()).ToList();
        var catalogos = await CatalogosDeCartera.CargarAsync(db, resolutorDeCartera, tiposDeCartera, comoHechos, reloj.HoyLocal, ct);
        var abonos = comoHechos.Select(h => ConstructorDeLineasDeCartera.Construir(h, catalogos)).ToList();
        if (abonos.Any(a => !a.EsValida || a.SinLineas)) return NoSeJunta("una parte de Cartera no se arma.");
        if (abonos.Any(a => a.Fecha != caja.Fecha)) return NoSeJunta("las partes tienen fechas contables distintas.");

        var comprobante = await poster.PrepareAsync(Unido(caja, abonos), ct);
        if (comprobante.IsFailure)
        {
            db.DescartarCambios();
            return NoSeJunta(comprobante.Error.Message);
        }
        await consumoDeRecaudo.AgregarRecibosAsync(Recaudo, mensajesDeCaja, comprobante.Value, null, null, ct);
        foreach (var (_, mensajes) in deCartera) await consumoDeCartera.AgregarRecibosAsync(mensajes, comprobante.Value, null, null, ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ConsumoDeModulo.EsColision(ex) || ConsumoDeCartera.EsColisionDelRecibo(ex))
        {
            // Otra réplica procesó alguna parte: nada de este recibo quedó guardado; la siguiente pasada ve lo ya procesado.
            db.DescartarCambios();
            return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades
                .Select(u => new ResultadoDeUnidad(u.Unidad, new ResultadoDeConsumo.Retry("Otra réplica procesó parte del recibo."))).ToList());
        }

        await consumoDeRecaudo.AuditarContabilizacionAsync(Recaudo, comprobante.Value, mensajesDeCaja, null, ct);
        await consumoDeCartera.AuditarContabilizacionAsync(comprobante.Value, deCartera.SelectMany(x => x.Mensajes).ToList(), null, ct);
        var procesado = new ResultadoDeConsumo.Processed(comprobante.Value.PublicId, comprobante.Value.VoucherType?.Code,
            comprobante.Value.Number?.ToString(CultureInfo.InvariantCulture));
        return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades.Select(u => new ResultadoDeUnidad(u.Unidad, procesado)).ToList());
    }

    /// <summary>
    /// El comprobante del recibo: el tipo, la fecha, el origen y quien lo registró, los del recibo de caja; las líneas de Recaudo y después las de cada
    /// obligación, tal cual y cada una con su módulo; la descripción dice el recibo y cuántos créditos y cuentas abona.
    /// </summary>
    public static PostingRequest Unido(ConstruccionDeModulo caja, IReadOnlyList<ConstruccionDeCartera> abonos)
    {
        ArgumentNullException.ThrowIfNull(caja);
        ArgumentNullException.ThrowIfNull(abonos);
        var recibo = caja.Request!;
        var numero = abonos.Select(a => ComprobantePorRecibo.Recaudo(a.Principal.Contenido)?.Receipt.Number).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));
        var creditos = abonos.SelectMany(a => a.Mapa).SelectMany(m => m.Creditos).Distinct(StringComparer.Ordinal).Count();
        var cuentas = abonos.SelectMany(a => a.Mapa).SelectMany(m => m.CuentasDeCaptacion).Distinct(StringComparer.Ordinal).Count();
        var partes = new List<string> { $"Recibo {numero}".TrimEnd(), caja.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
        if (creditos > 0) partes.Add($"{creditos} {(creditos == 1 ? "crédito" : "créditos")}");
        if (cuentas > 0) partes.Add($"{cuentas} {(cuentas == 1 ? "cuenta" : "cuentas")}");
        var lineas = recibo.Lines.Select(l => l with { Module = ModuloContable.Recaudo })
            .Concat(abonos.SelectMany(a => a.Request!.Lines).Select(l => l with { Module = ModuloContable.Cartera }))
            .ToList();
        return recibo with { Description = string.Join(" · ", partes), Lines = lineas };
    }
}
