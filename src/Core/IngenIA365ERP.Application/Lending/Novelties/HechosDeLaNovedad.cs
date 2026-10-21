using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Accrual;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Operations;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Replay;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Novelties;

/// <summary>
/// Los hechos contables de una novedad, armados desde su cálculo (feature 013, K3, T0881; mensajes.md §10.20; contabilidad.md §13): el sobre y las
/// dimensiones de cada crédito (por <see cref="PartesDeHechosDeCartera"/>), su categoría contable, lo capitalizado, lo que pasa al enlazado, los planes y
/// el conteo; y la validación previa de pantalla (A8: con <c>Contabilidad.FaltaDeRegla = Bloquear</c> y sin regla, <c>Lending.Prevalidation.NotPostable</c>).
/// La simulación y la aplicación preguntan lo mismo. No escribe. (nuevo)
/// </summary>
public sealed class HechosDeLaNovedad(
    IApplicationDbContext db,
    PartesDeHechosDeCartera partes,
    FechaContable fechaContable,
    EmisionDeCartera emision,
    ValidacionPreviaContableDeCartera validacionPrevia,
    IDateTimeService reloj)
{
    /// <summary>
    /// Arma la novedad para emitir: con <paramref name="operaciones"/> (la <c>TermsChange</c> de cada crédito, por su Id) al aplicar; sin ellas, para
    /// la validación previa. <paramref name="planNuevo"/> es el resumen del plan que deja (en el enlazado, el suyo).
    /// </summary>
    public async Task<Result<NovedadAEmitir>> ArmarAsync(NovedadCalculada c, IReadOnlyDictionary<int, LoanOperation>? operaciones, LoanRefV1? enlazado,
        Guid? puntoDeActa, LoanPlanSummaryV1? planNuevo, DateTime registrado, CancellationToken ct)
    {
        var contable = await fechaContable.CalcularAsync(c.Fecha, ct);
        if (contable.IsFailure) return Result.Failure<NovedadAEmitir>(contable.Error);
        var datos = await partes.DatosAsync(c.Creditos.Select(x => x.Cargado), ct);
        var categorias = await new CategoriaContableDeCartera(db).VigentesAsync(c.Creditos.Select(x => x.Credito.Id).ToList(), contable.Value, ct);
        var versiones = await VersionesDelPlanAsync(c.Creditos.Select(x => x.Credito.Id).ToList(), ct);

        var creditos = new List<CreditoDeNovedadAEmitir>();
        foreach (var x in c.Creditos)
        {
            var sucursal = await emision.SucursalContableAsync(x.Credito.BranchId, ct);
            if (sucursal.IsFailure) return Result.Failure<NovedadAEmitir>(sucursal.Error);
            var esPrincipal = x.Credito.Id == c.Principal.Credito.Id;
            var capitalizado = esPrincipal ? c.Capitalizaciones : [];
            creditos.Add(new CreditoDeNovedadAEmitir(
                datos[x.Credito.Id].Hecho(contable.Value),
                categorias[x.Credito.Id],
                capitalizado,
                c.CreaEnlazado ? Trasladado(x.Estado, capitalizado) : [],
                x.Estado.Saldos,
                ComprobantesDePago.PlanDelHecho(x.Estado.Plan, versiones.GetValueOrDefault(x.Credito.Id), x.Cargado.Vigentes.Periodicity.ToString()),
                operaciones?.GetValueOrDefault(x.Credito.Id)));
        }
        return Result.Success(new NovedadAEmitir(c.Novedad.PublicId, c.Novedad.Mechanism, c.Novedad.Cause, creditos, planNuevo,
            c.Principal.Cargado.Vigentes.RestructuringCount, c.ReestructuracionesDespues, enlazado, puntoDeActa, c.Fecha, registrado)
        {
            SucursalNueva = c.Terminos.BranchPublicId,
        });
    }

    /// <summary>
    /// La validación previa de pantalla de los hechos de la novedad (contabilidad.md §13.2). Con <paramref name="alertar"/> en falso (la simulación) no
    /// levanta la alerta.
    /// </summary>
    public Task<DecisionDeValidacionPrevia> ValidarAsync(IReadOnlyList<HechoDeNovedad> hechos, string rotulo, bool alertar, CancellationToken ct) =>
        validacionPrevia.DecidirAsync(hechos.Select(h => new MensajeContableDto(new IntegrationEnvelopeV1
        {
            MessageId = Guid.Empty,
            Type = h.Tipo,
            Kind = IntegrationMessageKind.Business,
            OriginModule = ModulosDeOrigen.Cartera,
            OriginEventKey = h.Aparte ? ClavesDeEvento.ConfirmacionPor(h.Credito.Hecho.Credito.PublicId) : ClavesDeEvento.Confirmacion,
            BranchPublicId = h.Credito.Hecho.SucursalPublicId,
        }, h.Contenido)).ToList(), OrigenDeValidacionPrevia.Pantalla, rotulo, alertar, ct);

    /// <summary>Lo que el crédito anterior de una novación traslada al enlazado: todo su saldo a la fecha, sin lo que se capitaliza.</summary>
    public static IReadOnlyList<PartidaDeNovedad> Trasladado(EstadoDelCredito estado, IReadOnlyList<PartidaDeNovedad> capitalizado)
    {
        var salida = new List<PartidaDeNovedad>();
        foreach (var s in estado.Saldos.Where(s => s.Valor != 0m))
        {
            var menos = capitalizado.Where(p => p.Concepto == s.Concepto && p.Cuota == s.Cuota).Sum(p => p.Monto);
            if (s.Valor - menos != 0m) salida.Add(new PartidaDeNovedad(s.Concepto, s.Cuota, s.Valor - menos));
        }
        return salida;
    }

    /// <summary>La última versión del plan de cada crédito.</summary>
    public async Task<IReadOnlyDictionary<int, int>> VersionesDelPlanAsync(IReadOnlyCollection<int> loanIds, CancellationToken ct) =>
        await db.RepaymentScheduleVersions.AsNoTracking().Where(v => loanIds.Contains(v.LoanId)).GroupBy(v => v.LoanId)
            .Select(g => new { g.Key, Max = g.Max(v => v.VersionNumber) }).ToDictionaryAsync(x => x.Key, x => x.Max, ct);

    public DateTime Ahora => reloj.UtcNow;
}
