using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Receipts.Accounting;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Domain.Entities.Receipts.Sessions;
using IngenIA365ERP.Domain.Enums.Receipts;
using IngenIA365ERP.Domain.Receipts.Arqueo;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Receipts.Sessions;

/// <summary>
/// El arqueo de una sesión con el motor <see cref="ArqueoDeCaja"/> (feature 015, T0093; R-14; FR-031, FR-032, FR-183): resuelve lo contado de la pantalla
/// (denominaciones, lotes y referencias por PublicId), arquea contra lo esperado, exige motivo en toda diferencia, inserta el arqueo completo (hecho) y
/// aplica el efecto de las diferencias que no necesitan aprobación —dentro de la tolerancia, con motivo y sin aprobación (Q7)—: el faltante a cargo del
/// cajero o al gasto, el sobrante a <c>RCD_CashSurpluses</c>, y su hecho <c>DiferenciaDeArqueoAprobada</c>. Las de sobre la tolerancia quedan por aprobar
/// por otra persona (<c>ApproveCountDifferenceCommand</c>). (nuevo)
/// </summary>
public sealed class ArqueosDeSesion(IApplicationDbContext db, EmisionDeRecaudo emision, ParametrosDeCaja parametros, IDateTimeService reloj)
{
    /// <param name="Estricto">Si rigió el arqueo estricto (<c>Caja.ArqueoEstricto</c>); sin él, el cierre resuelve las diferencias sin otra persona.</param>
    public sealed record ArqueoRegistrado(CashCount Arqueo, IReadOnlyList<CashCountLine> Lineas, ResultadoDeArqueo Resultado, bool Estricto = true);

    /// <summary>
    /// 2026-11-25, caja única: arquea sin escribir nada, sumando a lo esperado lo que la caja vinculada del punto de venta recibió y todavía no entró a la
    /// sesión (<paramref name="delPuntoDeVenta"/>, por <c>PaymentMeansId</c>). El coordinador lo pregunta antes de cerrar la caja del punto de venta: un
    /// arqueo que se va a rechazar (un motivo que falta, el conteo por billetes) no la deja cerrada con Recaudo abierta.
    /// </summary>
    public async Task<Result> ComprobarAsync(CashSession sesion, IReadOnlyList<ConteoDeMedioDto> contado, IReadOnlyDictionary<int, decimal>? delPuntoDeVenta,
        CancellationToken ct)
    {
        var evaluado = await EvaluarAsync(sesion, contado, delPuntoDeVenta, ct);
        return evaluado.IsFailure ? Result.Failure(evaluado.Error) : Result.Success();
    }

    /// <summary>
    /// Arquea e inserta; con una diferencia sin motivo, el error y nada se escribe. <paramref name="delPuntoDeVenta"/>: lo de la caja vinculada que todavía
    /// no entró a la sesión (el arqueo sorpresivo de una caja única, 2026-11-25: el cajón es uno solo).
    /// </summary>
    public async Task<Result<ArqueoRegistrado>> RegistrarAsync(CashSession sesion, CashCountKind clase, IReadOnlyList<ConteoDeMedioDto> contado, int contadoPor,
        int? testigo, Guid? acta, DateTime ahora, CancellationToken ct, IReadOnlyDictionary<int, decimal>? delPuntoDeVenta = null)
    {
        var evaluado = await EvaluarAsync(sesion, contado, delPuntoDeVenta, ct);
        if (evaluado.IsFailure) return Result.Failure<ArqueoRegistrado>(evaluado.Error);
        var (resultado, detalle, estricto) = (evaluado.Value.Resultado, evaluado.Value.Detalle, evaluado.Value.Estricto);
        var sellados = ValoresSelladosDeLaSesion.De(sesion);

        var arqueo = new CashCount
        {
            CashSessionId = sesion.Id,
            Kind = clase,
            CountedAt = ahora,
            CountedByUserId = contadoPor,
            WitnessUserId = testigo,
            IsBlind = sellados.BlindCount && estricto,
            MinutesAttachmentPublicId = acta,
            VerificationCode = CloseCollectionPointDayCommandHandler.CodigoDeVerificacion(),
        };
        return await InsertarAsync(arqueo, resultado, detalle, estricto, ct);
    }

    /// <summary>El detalle de lo contado en un medio, resuelto a Id (denominaciones, lotes y referencias).</summary>
    private sealed record DetalleContado(List<(int Id, int Cantidad, decimal Valor)> Denominaciones, List<(int Terminal, string Lote, decimal Valor)> Lotes,
        List<ReferenciaContadaDto> Referencias);

    /// <summary>El arqueo evaluado, todavía sin escribir.</summary>
    private sealed record Evaluado(ResultadoDeArqueo Resultado, Dictionary<int, DetalleContado> Detalle, bool Estricto);

    /// <summary>Lo contado resuelto y arqueado contra lo esperado (más lo del punto de venta), sin escribir.</summary>
    private async Task<Result<Evaluado>> EvaluarAsync(CashSession sesion, IReadOnlyList<ConteoDeMedioDto> contado,
        IReadOnlyDictionary<int, decimal>? delPuntoDeVenta, CancellationToken ct)
    {
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var esperado = await SesionesDeCaja.EsperadoAsync(db, sesion, ct);
        if (delPuntoDeVenta is { Count: > 0 })
            esperado = esperado.Select(e => delPuntoDeVenta.TryGetValue(e.Medio.PaymentMeansId, out var v) && v > 0m ? e with { Entradas = e.Entradas + v } : e)
                .ToList();
        // 2026-11-24: con el arqueo sencillo (Caja.ArqueoEstricto apagado) no hay arqueo ciego, el motivo se pide sólo fuera de la tolerancia y el faltante
        // va al gasto (decisión del dueño).
        var estricto = await ArqueoEstricto.EsAsync(parametros, reloj.HoyLocal, ct);

        // 2026-11-23: con Caja.FormaDeConteoDelEfectivo = Denominaciones el efectivo no se registra con un solo valor.
        if (contado.Any(c => c.Total is > 0m && (c.Denominations is null || c.Denominations.Count == 0)
                && sellados.Means.FirstOrDefault(m => m.PublicId == c.PaymentMeansPublicId)?.Class == Domain.Enums.Core.PaymentMeansClass.Cash)
            && await FormaDeConteoDelEfectivo.ExigeDenominacionesAsync(parametros, reloj.HoyLocal, ct))
            return Falla(ErroresDeRecaudo.ConteoPorDenominaciones());

        var conteos = new List<ConteoDeMedio>();
        var detalle = new Dictionary<int, DetalleContado>();
        foreach (var c in contado)
        {
            var medio = sellados.Means.FirstOrDefault(m => m.PublicId == c.PaymentMeansPublicId);
            if (medio is null) return Falla(ErroresDeRecaudo.NoEncontrado("El medio de la sesión"));
            var denominaciones = await SesionesDeCaja.DenominacionesAsync(db, c.Denominations, ct);
            if (denominaciones.IsFailure) return Falla(denominaciones.Error);
            var lotes = new List<(int, string, decimal)>();
            foreach (var l in c.TerminalBatches ?? [])
            {
                var terminal = await db.CardTerminals.AsNoTracking().Where(t => t.PublicId == l.CardTerminalPublicId).Select(t => (int?)t.Id).FirstOrDefaultAsync(ct);
                if (terminal is null) return Falla(ErroresDeRecaudo.NoEncontrado("El datáfono"));
                lotes.Add((terminal.Value, l.BatchNumber.Trim(), l.Amount));
            }
            var referencias = (c.References ?? []).ToList();
            detalle[medio.PaymentMeansId] = new DetalleContado(denominaciones.Value.Select(d => (d.Denominacion.Id, d.Cantidad, d.Valor)).ToList(), lotes, referencias);
            conteos.Add(new ConteoDeMedio(
                medio.PaymentMeansId,
                c.Total,
                denominaciones.Value.Select(d => new DenominacionContada(d.Denominacion.Value, d.Cantidad)).ToList(),
                referencias.Select(r => new ReferenciaContada(r.Reference, r.Amount, r.Confirmed)).ToList(),
                lotes.Select(l => new LoteContado(l.Item1, l.Item2, l.Item3)).ToList(),
                c.Reason));
        }

        var resultado = ArqueoDeCaja.Arquear(esperado, conteos, estricto ? sellados.ShortageTreatment : CountTreatment.Expense,
            motivoSoloFueraDeTolerancia: !estricto);
        if (resultado.SinMotivo.Count > 0) return Falla(ErroresDeRecaudo.SinMotivo(resultado.SinMotivo));
        return Result.Success(new Evaluado(resultado, detalle, estricto));

        static Result<Evaluado> Falla(Error e) => Result.Failure<Evaluado>(e);
    }

    /// <summary>Inserta el arqueo arqueado con sus líneas y su detalle.</summary>
    private async Task<Result<ArqueoRegistrado>> InsertarAsync(CashCount arqueo, ResultadoDeArqueo resultado, Dictionary<int, DetalleContado> detalle,
        bool estricto, CancellationToken ct)
    {
        db.ReceiptsCashCounts.Add(arqueo);
        await db.SaveChangesAsync(ct);
        var lineas = new List<CashCountLine>();
        foreach (var l in resultado.Lineas)
        {
            var linea = new CashCountLine
            {
                CashCountId = arqueo.Id,
                PaymentMeansId = l.PaymentMeansId,
                Method = l.Method,
                Expected = l.Expected,
                Counted = l.Counted,
                Difference = l.Difference,
                Tolerance = l.Tolerance,
                WithinTolerance = l.WithinTolerance,
                Reason = l.Reason,
                Treatment = l.Treatment,
            };
            db.ReceiptsCashCountLines.Add(linea);
            lineas.Add(linea);
        }
        await db.SaveChangesAsync(ct);
        foreach (var linea in lineas)
        {
            if (!detalle.TryGetValue(linea.PaymentMeansId, out var d)) continue;
            foreach (var (id, cantidad, valor) in d.Denominaciones)
                db.ReceiptsCashCountDenominations.Add(new CashCountDenomination { CashCountLineId = linea.Id, CashDenominationId = id, Quantity = cantidad, Amount = valor });
            foreach (var (terminal, lote, valor) in d.Lotes)
                db.ReceiptsCashCountTerminalBatches.Add(new CashCountTerminalBatch { CashCountLineId = linea.Id, CardTerminalId = terminal, BatchNumber = lote, Amount = valor });
            foreach (var r in d.Referencias)
                db.CashCountReferences.Add(new CashCountReference { CashCountLineId = linea.Id, Reference = r.Reference.Trim(), Amount = r.Amount, Confirmed = r.Confirmed });
        }
        return Result.Success(new ArqueoRegistrado(arqueo, lineas, resultado, estricto));
    }

    /// <summary>
    /// El efecto de una diferencia ya resuelta (dentro de la tolerancia, o aprobada): el faltante a cargo del cajero (<c>RCD_CashierReceivables</c>) o al
    /// gasto, el sobrante a <c>RCD_CashSurpluses</c> con su fecha de paso a ingreso, y el hecho <c>DiferenciaDeArqueoAprobada</c>. Sin guardar.
    /// </summary>
    public async Task AplicarDiferenciaAsync(CashCountLine linea, CashSession sesion, int aprobador, DateOnly hoy, CancellationToken ct)
    {
        if (linea.Difference == 0m) return;
        var sellados = ValoresSelladosDeLaSesion.De(sesion);
        var personaDelCajero = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.Id == sesion.CashierUserId).Select(u => u.PersonId).FirstOrDefaultAsync(ct);
        Guid? personaPublicId = personaDelCajero is { } p
            ? await db.People.AsNoTracking().IgnoreQueryFilters().Where(x => x.Id == p).Select(x => (Guid?)x.PublicId).FirstOrDefaultAsync(ct)
            : null;
        switch (linea.Treatment)
        {
            case CountTreatment.CashierReceivable when personaDelCajero is { } persona:
                var valor = Math.Abs(linea.Difference);
                db.CashierReceivables.Add(new CashierReceivable { CashierPersonId = persona, CashCountLineId = linea.Id, Amount = valor, Balance = valor });
                break;
            case CountTreatment.Surplus:
                db.CashSurpluses.Add(new CashSurplus
                {
                    CashCountLineId = linea.Id,
                    Amount = linea.Difference,
                    ToIncomeFrom = sellados.SurplusDaysBeforeIncome is { } dias ? hoy.AddDays(dias) : null,
                });
                break;
        }
        await emision.DiferenciaDeArqueoAsync(linea, sesion, personaPublicId, aprobador, hoy, ct);
    }
}
