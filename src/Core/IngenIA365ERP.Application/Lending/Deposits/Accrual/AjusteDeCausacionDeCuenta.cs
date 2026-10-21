using IngenIA365ERP.Application.Common.Integration.Contracts.Lending.Deposits;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Ledger;
using IngenIA365ERP.Application.Lending.Integration;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Accounts;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Projections;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Transactions;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>
/// El ajuste de causación por fecha valor <b>enlazado</b> a la operación que lo provoca (feature 014, US5, T133; research R04; FR-062): una consignación, un
/// retiro, un traslado o un reverso con fecha valor en días ya causados de la ventana abierta cambia los saldos de esos días; el motor vuelve a calcular la
/// ventana hasta el último día causado con lo que ya se sabe y la diferencia contra lo que está en el libro entra en la <b>misma</b> operación como
/// <c>AccrualAdjustment</c> fechado el último día causado, con su hecho <c>AjusteDeCausacionDeCaptacion</c> (causa <c>ValueDatedOperation</c>, los días que
/// cambiaron y el hecho principal de la operación). Sin causación previa o en una cuenta que no causa, no hace nada. Corre en la transacción y bajo el
/// cerrojo de quien llama, después de que la operación guardó sus filas; guarda.
///
/// <para>
/// <b>Fecha valor en un período ya liquidado</b> (respuesta 64 B del dueño, E256; antes E97 lo dejaba sin ajustar): el movimiento <b>entra</b> y los intereses de
/// los días liquidados que cambian —lo que el motor da <i>con</i> la operación menos lo que daría <i>sin</i> ella, período por período— se ajustan en el
/// <b>período abierto</b>: una fila <c>SettledPeriodAdjustment</c> fechada el primer día después de la última liquidación (la próxima liquidación la abona;
/// no forma parte de la ventana que la causación compara con el motor) y su valor viaja en el mismo hecho, con los días liquidados que cambiaron. No se
/// reabre ni se reescribe lo liquidado (ni su retención ya practicada: la del ajuste la calcula la liquidación en la que entra).
/// </para>
/// (nuevo)
/// </summary>
public sealed class AjusteDeCausacionDeCuenta(IApplicationDbContext db, LectorDeInteres lector, EmisionDeCartera emision, RegistroDeCuentas registro)
{
    public async Task<Result<decimal>> AjustarAsync(MemberAccount cuenta, MemberAccountOperation operacion, string claseDeLaOperacion, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cuenta);
        ArgumentNullException.ThrowIfNull(operacion);
        if (!LiquidacionDeIntereses.GanaInteres(cuenta)) return Result.Success(0m);
        var control = db.MemberAccountControls.Local.FirstOrDefault(c => c.MemberAccountId == cuenta.Id)
                      ?? await db.MemberAccountControls.FirstOrDefaultAsync(c => c.MemberAccountId == cuenta.Id, ct);
        if (control is null) return Result.Success(0m);
        var ultimo = control.LastAccruedDate;
        if (operacion.ValueDate > ultimo) return Result.Success(0m);

        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var version = LectorDeInteres.VersionDelDia(versiones, cuenta, ultimo, DateTime.MaxValue);
        if (version is null) return Result.Success(0m);
        var fallecido = await Deceased.TitularesFallecidos.EsAsync(db, cuenta.HolderPersonId, ct);

        // 1. La ventana abierta: lo que el motor da hoy contra lo que está en el libro.
        var ajusteAbierto = 0m;
        var dias = new List<AccrualDayV1>();
        var inicio = LectorDeInteres.InicioDeLaVentana(cuenta, control, version.Version, ultimo);
        if (ultimo > inicio)
        {
            var ventana = await lector.VentanaAsync(cuenta, inicio, ultimo, DateTime.MaxValue, fallecido, ct);
            if (ventana.IsFailure) return Result.Failure<decimal>(ventana.Error);
            var enElLibro = await lector.CausadoEnElLibroAsync(cuenta.Id, inicio, ultimo, ct);
            ajusteAbierto = ventana.Value.Interes.Causado - enElLibro;

            // Los días que cambiaron: lo que causó cada uno según el libro y lo que causa ahora.
            var clases = LectorDeInteres.ClasesDeLoCausado.ToArray();
            var previos = (await db.MemberAccountTransactions.AsNoTracking()
                    .Where(t => t.MemberAccountId == cuenta.Id && t.Concept == MemberAccountConcept.InterestPayable && clases.Contains(t.Kind)
                                && t.ValueDate > inicio && t.ValueDate <= ultimo && !t.IsDeleted)
                    .Select(t => new { t.ValueDate, t.Amount }).ToListAsync(ct))
                .GroupBy(t => t.ValueDate).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
            dias.AddRange(ventana.Value.Interes.Dias
                .Select(d => new AccrualDayV1 { Date = d.Dia, Previous = previos.GetValueOrDefault(d.Dia), Recalculated = d.Causado })
                .Where(d => d.Previous != d.Recalculated));
        }

        // 2. Los períodos ya liquidados que la fecha valor alcanza (respuesta 64 B): su diferencia entra en el período abierto.
        var liquidada = await DeLosPeriodosLiquidadosAsync(cuenta, control, operacion, fallecido, ct);
        if (liquidada.IsFailure) return Result.Failure<decimal>(liquidada.Error);
        var ajusteLiquidado = liquidada.Value.Diferencia;
        dias.AddRange(liquidada.Value.Dias);
        if (ajusteAbierto == 0m && ajusteLiquidado == 0m) return Result.Success(0m);

        var total = ajusteAbierto + ajusteLiquidado;
        Guid? mensaje = null;
        var hecho = await emision.HechoDeCuentaAsync(cuenta, operacion.AccountingDate, ct);
        if (hecho.IsFailure) return Result.Failure<decimal>(hecho.Error);
        if (total != 0m)
        {
            var exenta = await db.GmfExemptAccounts.AsNoTracking()
                .AnyAsync(g => g.MemberAccountId == cuenta.Id && !g.IsDeleted && g.ValidFrom <= ultimo && (g.ValidTo == null || g.ValidTo >= ultimo), ct);
            var unidad = await emision.AjusteDeCausacionAsync(new AjusteAEmitir(new HechoDeCuentaMarcada(hecho.Value, exenta), operacion, total,
                dias.OrderBy(d => d.Date).ToList(), EmisionDeCartera.CausaOperacionFechadaAtras, claseDeLaOperacion), ct);
            if (unidad.IsFailure) return Result.Failure<decimal>(unidad.Error);
            mensaje = unidad.Value.De(AjusteDeCausacionDeCaptacionV1.Type);
        }

        var filas = new List<MemberAccountTransaction>();
        if (ajusteAbierto != 0m)
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.AccrualAdjustment, ajusteAbierto, ultimo, hecho.Value.FechaContable, mensaje));
        if (ajusteLiquidado != 0m)
            filas.Add(Fila(cuenta, operacion, MemberAccountTransactionKind.SettledPeriodAdjustment, ajusteLiquidado, control.LastSettledThrough!.Value.AddDays(1),
                hecho.Value.FechaContable, mensaje));
        registro.RegistrarTransacciones(filas);
        await db.SaveChangesAsync(ct);
        return Result.Success(total);
    }

    private static MemberAccountTransaction Fila(MemberAccount cuenta, MemberAccountOperation operacion, MemberAccountTransactionKind clase, decimal monto, DateOnly fecha,
        DateOnly contable, Guid? mensaje) => new()
    {
        MemberAccountId = cuenta.Id,
        Operation = operacion,
        OperationId = operacion.Id == 0 ? null : operacion.Id,
        Kind = clase,
        Concept = MemberAccountConcept.InterestPayable,
        Amount = monto,
        ValueDate = fecha,
        AccountingDate = contable,
        RegisteredAt = operacion.RegisteredAt,
        MessagePublicId = mensaje,
    };

    /// <summary>
    /// Respuesta 64 B (E256): la diferencia de intereses de los días <b>ya liquidados</b> que la fecha valor de la operación alcanza, período de liquidación por
    /// período: lo que el motor da con la operación menos lo que daría sin ella (las dos veces con las tasas y los saldos de hoy, así sólo cuenta el efecto de la
    /// operación y no una tasa registrada tarde). Cero si la fecha valor cae en el período abierto o la cuenta es migrada y la fecha es anterior al corte. Una
    /// fecha valor <b>igual</b> al día de la última liquidación no se ajusta: es la de la cancelación, la entrega por fallecimiento, el pago de saldos no
    /// reclamados y el retiro del asociado, que liquidan a la fecha y entregan el mismo día (la cuenta se cierra: no habría liquidación que abonara el ajuste).
    /// </summary>
    private async Task<Result<(decimal Diferencia, IReadOnlyList<AccrualDayV1> Dias)>> DeLosPeriodosLiquidadosAsync(MemberAccount cuenta, MemberAccountControl control,
        MemberAccountOperation operacion, bool fallecido, CancellationToken ct)
    {
        if (control.LastSettledThrough is not { } liquidada || operacion.ValueDate >= liquidada) return Result.Success<(decimal, IReadOnlyList<AccrualDayV1>)>((0m, []));
        var versiones = await lector.VersionesAsync(cuenta.CreditLineId, ct);
        var corte = LectorDeInteres.CorteDeMigracion(cuenta);
        var diferencia = 0m;
        var dias = new List<AccrualDayV1>();
        var dia = operacion.ValueDate;
        for (var vuelta = 0; dia <= liquidada && vuelta < 400; vuelta++)
        {
            var version = LectorDeInteres.VersionDelDia(versiones, cuenta, dia, DateTime.MaxValue);
            if (version is null) break;
            var periodo = InteresDeCaptacion.PeriodoDeLiquidacion(version.Version.SettlementPeriodicity, version.Version.SettlementDay, dia);
            var inicio = periodo is { } p ? p.Desde.AddDays(-1) : cuenta.OpenedOn;
            if (inicio < cuenta.OpenedOn) inicio = cuenta.OpenedOn;
            if (corte is { } c && c > inicio) inicio = c;
            var fin = periodo is { } q && q.Hasta < liquidada ? q.Hasta : liquidada;
            if (fin > inicio)
            {
                var con = await lector.VentanaAsync(cuenta, inicio, fin, DateTime.MaxValue, fallecido, ct);
                if (con.IsFailure) return Result.Failure<(decimal, IReadOnlyList<AccrualDayV1>)>(con.Error);
                var sin = await lector.VentanaAsync(cuenta, inicio, fin, DateTime.MaxValue, fallecido, ct, sinLaOperacion: operacion.Id);
                if (sin.IsFailure) return Result.Failure<(decimal, IReadOnlyList<AccrualDayV1>)>(sin.Error);
                diferencia += con.Value.Interes.Causado - sin.Value.Interes.Causado;
                var antes = sin.Value.Interes.Dias.ToDictionary(d => d.Dia, d => d.Causado);
                dias.AddRange(con.Value.Interes.Dias
                    .Select(d => new AccrualDayV1 { Date = d.Dia, Previous = antes.GetValueOrDefault(d.Dia), Recalculated = d.Causado })
                    .Where(d => d.Previous != d.Recalculated));
            }
            dia = fin.AddDays(1);
        }
        return Result.Success<(decimal, IReadOnlyList<AccrualDayV1>)>((diferencia, dias));
    }
}
