using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Integration;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;

/// <summary>
/// Contabiliza un <b>grupo resumido</b> de hechos de Cartera de un lote (feature 013, T0648; contracts/contabilidad.md §10.4; FR-128, US7-3;
/// molde <c>PostInventorySummaryGroupCommand</c>): la causación diaria en un comprobante por día y tipo de comprobante, con la sucursal en cada
/// línea. <b>Sin ruta ni permisos</b>; lo envía el despachador por <c>DestinoContabilidad</c>.
///
/// <list type="number">
///   <item>arma el <see cref="PostingRequest"/> de cada unidad (cada parte) como si fuera por hecho, con la matriz, los tipos y las personas
///         cargados una vez, y los valida todos con <see cref="AccountingPoster.ValidarVariosAsync"/>;</item>
///   <item>excluye las que fallan (<c>Rejected</c>, con hecho, concepto y crédito) y a sus dependientes del mismo grupo, que esperan
///         (<c>Retry</c>): una parte mala no tumba la causación del día;</item>
///   <item>suma las líneas válidas por cuenta, lado, sucursal y centro, débitos y créditos por separado, sin netear; las cuentas que exigen
///         tercero, cruce o base conservan una línea <b>por crédito</b> con su deudor y <c>PG</c> + número;</item>
///   <item>un <see cref="AccountingPoster.PrepareAsync"/> por tipo de comprobante, fechado en la fecha contable del grupo, con origen
///         <c>LendingPostingBatch</c> y <c>RegistradoPor</c> nulo; un recibo por hecho con el mismo comprobante y el lote; un <c>SaveChanges</c>.</item>
/// </list>
/// Devuelve un <see cref="ResultadoDeUnidad"/> por unidad, en el orden recibido. Repetir el grupo no crea nada (recibo único). (nuevo)
/// </summary>
public sealed record PostLendingSummaryGroupCommand(Guid BatchPublicId, string GroupKey, IReadOnlyList<Guid> MessagePublicIds)
    : IRequest<Result<IReadOnlyList<ResultadoDeUnidad>>>, IReintentableAnteConcurrencia;

public sealed class PostLendingSummaryGroupCommandValidator : AbstractValidator<PostLendingSummaryGroupCommand>
{
    public PostLendingSummaryGroupCommandValidator()
    {
        RuleFor(x => x.BatchPublicId).NotEqual(Guid.Empty);
        RuleFor(x => x.GroupKey).NotEmpty().MaximumLength(200);
        RuleFor(x => x.MessagePublicIds).NotEmpty().WithMessage("El grupo no trae hechos.");
        RuleForEach(x => x.MessagePublicIds).NotEqual(Guid.Empty);
    }
}

public sealed class PostLendingSummaryGroupCommandHandler(
    IApplicationDbContext db,
    IMensajesEntrantes entrantes,
    ConsumoDeCartera consumo,
    AccountingPoster poster,
    ResolutorDeReglasDeCartera resolutor,
    TiposDeComprobanteDeCartera tipos,
    IDateTimeService reloj) : IRequestHandler<PostLendingSummaryGroupCommand, Result<IReadOnlyList<ResultadoDeUnidad>>>
{
    public async Task<Result<IReadOnlyList<ResultadoDeUnidad>>> Handle(PostLendingSummaryGroupCommand request, CancellationToken ct)
    {
        var leidos = await entrantes.LeerAsync(request.MessagePublicIds, IntegrationDestinations.Accounting, ct);
        if (leidos.IsFailure) return Result.Failure<IReadOnlyList<ResultadoDeUnidad>>(leidos.Error);
        var unidades = AgrupadorDeResumidosDeCartera.Unidades(leidos.Value, IntegrationDestinations.Accounting);
        var resultados = new Dictionary<UnidadDeConsumo, ResultadoDeConsumo>();

        // Pasos 2 a 6 por unidad.
        var vivas = new List<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)>();
        foreach (var (unidad, mensajes) in unidades)
        {
            var alto = await consumo.RevisarAsync(mensajes, ct);
            if (alto is not null) resultados[unidad] = alto;
            else if (ConstructorDeLineasDeCartera.EsInformativa(mensajes.Select(HechoDeCartera.De).ToList()))
            {
                await consumo.AgregarRecibosAsync(mensajes, null, LendingPosting.SinComprobanteInformativo, request.BatchPublicId, ct);
                resultados[unidad] = new ResultadoDeConsumo.Processed(null, null, null, MotivoSinComprobante.Informational);
            }
            else vivas.Add((unidad, mensajes));
        }

        // 1. cada unidad como si fuera por hecho, con lo del lote cargado una vez (§10.5), validadas todas juntas.
        var comoHechos = vivas.Select(v => (IReadOnlyList<HechoDeCartera>)v.Mensajes.Select(HechoDeCartera.De).ToList()).ToList();
        var catalogos = await CatalogosDeCartera.CargarAsync(db, resolutor, tipos, comoHechos, reloj.TodayUtc, ct);
        var construidas = vivas.Select((v, i) => (v.Unidad, v.Mensajes, Construccion: ConstructorDeLineasDeCartera.Construir(comoHechos[i], catalogos))).ToList();

        var excluidos = new HashSet<Guid>();
        foreach (var (unidad, _, c) in construidas.Where(x => !x.Construccion.EsValida))
        {
            resultados[unidad] = ConsumoDeCartera.RechazoDe(c);
            excluidos.Add(unidad.OriginPublicId);
        }
        var conLineas = construidas.Where(x => x.Construccion.EsValida && !x.Construccion.SinLineas).ToList();
        var validaciones = await poster.ValidarVariosAsync(conLineas.Select(x => x.Construccion.Request!).ToList(), ct);
        for (var i = 0; i < conLineas.Count; i++)
        {
            if (validaciones[i].EsValido) continue;
            var periodo = validaciones[i].Errores.FirstOrDefault(e => e.Code == ConsumoDeCartera.CodigoPeriodoInexistente);
            resultados[conLineas[i].Unidad] = periodo is not null
                ? (await consumo.PeriodoInexistenteAsync(periodo.ComoError(), conLineas[i].Construccion.Fecha, ct))!
                : ConsumoDeCartera.RechazoDe(validaciones[i], conLineas[i].Construccion.Mapa);
            excluidos.Add(conLineas[i].Unidad.OriginPublicId);
        }

        // 2. los dependientes de un excluido, transitivamente, esperan: no se contabiliza un reverso sin su original.
        bool cambio;
        do
        {
            cambio = false;
            foreach (var (unidad, mensajes, _) in construidas)
            {
                if (resultados.ContainsKey(unidad) || !ConsumoDeCartera.Originales(mensajes).Any(excluidos.Contains)) continue;
                var error = AccountingErrors.LendingFactWaitingForOriginal(ConsumoDeCartera.Originales(mensajes).First(excluidos.Contains));
                resultados[unidad] = new ResultadoDeConsumo.Retry(error.Message, error.Code);
                excluidos.Add(unidad.OriginPublicId);
                cambio = true;
            }
        } while (cambio);

        foreach (var (unidad, mensajes, _) in construidas.Where(x => resultados.GetValueOrDefault(x.Unidad) is ResultadoDeConsumo.Rejected))
            await consumo.AuditarRechazoAsync(mensajes, (ResultadoDeConsumo.Rejected)resultados[unidad], ct);

        // 8. sin líneas: recibo sin comprobante.
        foreach (var (unidad, mensajes, c) in construidas.Where(x => !resultados.ContainsKey(x.Unidad) && x.Construccion.SinLineas).ToList())
        {
            await consumo.AgregarRecibosAsync(mensajes, null, c.SinComprobante, request.BatchPublicId, ct);
            resultados[unidad] = new ResultadoDeConsumo.Processed(null, null, null, ConsumoDeCartera.MotivoDe(c.SinComprobante));
        }

        // 3 a 7. el resumen de las válidas, un comprobante por tipo de comprobante (normalmente uno).
        var incluidas = conLineas.Where(x => !resultados.ContainsKey(x.Unidad)).ToList();
        var comprobantes = new List<(AccountingDocument Documento, IReadOnlyList<(UnidadDeConsumo Unidad, IReadOnlyList<MensajeEntrante> Mensajes)> Unidades)>();
        if (incluidas.Count > 0)
        {
            var numeroDeLote = await db.IntegrationBatches.AsNoTracking().Where(b => b.PublicId == request.BatchPublicId)
                .Select(b => (long?)b.Number).FirstOrDefaultAsync(ct);
            foreach (var porTipo in incluidas.GroupBy(x => x.Construccion.Tipo!.VoucherTypeCode, StringComparer.Ordinal))
            {
                var deEsteTipo = porTipo.ToList();
                var resumen = Resumen(deEsteTipo.Select(x => x.Construccion).ToList(), request.BatchPublicId, numeroDeLote);
                var preparado = await poster.PrepareAsync(resumen, ct);
                if (preparado.IsFailure)
                {
                    db.DescartarCambios();
                    ResultadoDeConsumo fallo = await consumo.PeriodoInexistenteAsync(preparado.Error, resumen.Date, ct) is { } reintento
                        ? reintento
                        : ConsumoDeCartera.Rechazo(preparado.Error);
                    foreach (var x in incluidas)
                    {
                        resultados[x.Unidad] = fallo;
                        if (fallo is ResultadoDeConsumo.Rejected r) await consumo.AuditarRechazoAsync(x.Mensajes, r, ct);
                    }
                    comprobantes.Clear();
                    break;
                }
                foreach (var x in deEsteTipo) await consumo.AgregarRecibosAsync(x.Mensajes, preparado.Value, null, request.BatchPublicId, ct);
                comprobantes.Add((preparado.Value, deEsteTipo.Select(x => (x.Unidad, x.Mensajes)).ToList()));
            }
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ConsumoDeCartera.EsColisionDelRecibo(ex))
        {
            // Otra réplica procesó alguno: nada de este grupo quedó guardado; la siguiente pasada ve lo ya procesado.
            db.DescartarCambios();
            return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades
                .Select(u => new ResultadoDeUnidad(u.Unidad, new ResultadoDeConsumo.Retry("Otra réplica procesó parte del grupo.")))
                .ToList());
        }

        foreach (var (documento, deEste) in comprobantes)
        {
            await consumo.AuditarContabilizacionAsync(documento, deEste.SelectMany(x => x.Mensajes).ToList(), request.BatchPublicId, ct);
            var procesado = new ResultadoDeConsumo.Processed(documento.PublicId, documento.VoucherType?.Code, documento.Number?.ToString(CultureInfo.InvariantCulture));
            foreach (var x in deEste) resultados[x.Unidad] = procesado;
        }

        return Result.Success<IReadOnlyList<ResultadoDeUnidad>>(unidades.Select(u => new ResultadoDeUnidad(u.Unidad, resultados[u.Unidad])).ToList());
    }

    /// <summary>
    /// El comprobante resumido (§10.4): suma por cuenta, lado, sucursal y centro (débitos y créditos por separado, sin netear); las líneas cuya
    /// cuenta exige tercero, cruce o base se suman sólo con las de su mismo crédito (una línea por crédito, con su deudor y su cruce).
    /// </summary>
    public static PostingRequest Resumen(IReadOnlyList<ConstruccionDeCartera> construcciones, Guid lote, long? numeroDeLote)
    {
        var lineas = new List<PostingLine>();
        var indice = new Dictionary<string, int>(StringComparer.Ordinal);
        var creditos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in construcciones)
        {
            for (var i = 0; i < c.Request!.Lines.Count; i++)
            {
                var linea = c.Request.Lines[i];
                var mapa = c.Mapa[i];
                creditos.UnionWith(mapa.Creditos);
                var lado = linea.Debit > 0m ? "D" : "C";
                // Una línea por crédito en las cuentas que exigen tercero, cruce o base (§10.4): el crédito va en la clave aunque la cuenta no
                // pida cruce. Hasta el 2026-10-13 dos créditos del mismo deudor se juntaban en una línea si la cuenta sólo exigía tercero (lo
                // destapó la medición de T0633).
                var clave = mapa.ConservaDetalle
                    ? string.Join('|', "detalle", linea.AccountCode, lado, linea.BranchId, linea.CostCenterId, linea.PersonId, linea.CrossDocumentType,
                        linea.CrossDocumentNumber, string.Join(',', mapa.Creditos.Order(StringComparer.Ordinal)))
                    : string.Join('|', linea.AccountCode, lado, linea.BranchId, linea.CostCenterId);
                if (indice.TryGetValue(clave, out var j))
                {
                    lineas[j] = lineas[j] with { Debit = lineas[j].Debit + linea.Debit, Credit = lineas[j].Credit + linea.Credit };
                    continue;
                }
                indice[clave] = lineas.Count;
                lineas.Add(mapa.ConservaDetalle
                    ? linea
                    : linea with { PersonId = null, CrossDocumentType = null, CrossDocumentNumber = null, Detail = $"Lote {numeroDeLote} · {mapa.AccountName}" });
            }
        }
        var primero = construcciones[0];
        var tipo = primero.Tipo!.VoucherTypeCode;
        var fecha = primero.Fecha;
        var descripcion = $"Lote {numeroDeLote} · {tipo} · {fecha:yyyy-MM-dd} · {creditos.Count} {(creditos.Count == 1 ? "crédito" : "créditos")}";
        return new PostingRequest(tipo, fecha, descripcion, new AccountingOrigin(ModuloContable.Cartera, OrigenesDeCartera.LoteResumido, lote), lineas,
            Domain.Enums.Accounting.DocumentKind.Regular);
    }
}
