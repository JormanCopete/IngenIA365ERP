using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Deposits.Accounts;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Lending.Deposits.Configuration;
using IngenIA365ERP.Domain.Entities.Lending.Governance;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Deposits;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Lending.Deposits.TermDeposits.NegotiatedRates;

/// <summary>
/// Una tasa negociada evaluada (feature 014, US6, T171): lo que dijo el motor (<see cref="TasaNegociada"/>), la versión de la regla aplicada, si cabe en la
/// atribución de quien la pide y, si no, el nivel de la matriz que la decide (nulo sin matriz: la decide quien tenga <c>Lending.NegotiatedRates.Approve</c>). (nuevo)
/// </summary>
public sealed record NegociacionEvaluada(EvaluacionDeTasaNegociada Motor, NegotiatedRateRule? Regla, bool CabeEnAtribucion, AttributionLevel? Nivel,
    IReadOnlyList<AttributionLevel> Niveles);

/// <summary>
/// La negociación de tasa de un CDAT (feature 014, US6, T171; research R13, E130; FR-127, D34): las reglas vigentes de la línea —la última versión de cada
/// código con <c>ValidFrom</c> ≤ la fecha, si sigue activa—, el pedido con el perfil del titular (antigüedad como asociado, saldo total en captaciones y
/// aportes, oficina, canal), la evaluación del motor y la <b>atribución</b>: la matriz vigente enruta el sujeto <c>NegotiatedRate</c> con el monto del CDAT y el
/// margen pedido (criterio «puntos adicionales», <c>AttributionRule.MaxNegotiatedSpread</c>); cabe si quien pide puede decidir el nivel efectivo
/// (<see cref="AtribucionDelDecisor.PuedeDecidirAsync"/>). Sin matriz vigente cabe si quien pide tiene <c>Lending.NegotiatedRates.Approve</c>. No escribe. (nuevo)
/// </summary>
public sealed class NegociacionDeTasa(IApplicationDbContext db, MatrizVigenteDeAtribuciones matrices, AtribucionDelDecisor atribucion, IDateTimeService reloj)
{
    /// <summary>El permiso que decide una tasa negociada cuando no hay matriz vigente.</summary>
    public const string PermisoDeAprobacion = "Lending.NegotiatedRates.Approve";

    /// <summary>Las reglas vigentes de la línea a <paramref name="fecha"/> con lo registrado hasta <paramref name="hasta"/> (nulo = todo).</summary>
    public async Task<IReadOnlyList<NegotiatedRateRule>> ReglasVigentesAsync(int lineaId, DateOnly fecha, DateTime? hasta, CancellationToken ct)
    {
        var versiones = await db.NegotiatedRateRules.AsNoTracking().Where(r => r.CreditLineId == lineaId && !r.IsDeleted).ToListAsync(ct);
        return versiones.GroupBy(r => r.Code, StringComparer.Ordinal)
            .Select(g => VigenciaBitemporal.VigenteDeCartera(g, fecha, hasta ?? DateTime.MaxValue))
            .Where(r => r is { IsActive: true })
            .Select(r => r!)
            .ToList();
    }

    /// <summary>El pedido para el motor: la antigüedad en meses desde la afiliación (cero si no es asociado) y el saldo del titular en sus cuentas vivas.</summary>
    public async Task<PedidoDeTasaNegociada> PedidoAsync(Person titular, int oficina, decimal monto, int? plazo, decimal tasaDeTabla, decimal margen, string? canal,
        DateOnly fecha, CancellationToken ct)
    {
        var afiliacion = await PersonaDeOriginacion.AfiliacionAsync(db, titular, fecha, ct);
        var meses = afiliacion.Desde is { } desde && desde <= fecha ? (fecha.Year - desde.Year) * 12 + fecha.Month - desde.Month - (fecha.Day < desde.Day ? 1 : 0) : 0;
        var cuentas = await db.MemberAccounts.AsNoTracking()
            .Where(a => a.HolderPersonId == titular.Id && a.Status != MemberAccountStatus.Closed && !a.IsDeleted)
            .Select(a => new { a.Id, a.Nature }).ToListAsync(ct);
        var saldos = await LectorDeCuentas.SaldosAsync(db, cuentas.Select(c => c.Id).ToList(), ct);
        var saldo = cuentas.Sum(c => LectorDeCuentas.SaldoDelTitular(c.Nature, saldos.GetValueOrDefault(c.Id) ?? []));
        return new PedidoDeTasaNegociada(Math.Max(0, meses), monto, plazo, tasaDeTabla, saldo, oficina, canal, margen);
    }

    /// <summary>Evalúa el pedido y la atribución de quien lo pide (ver la clase).</summary>
    public async Task<Result<NegociacionEvaluada>> EvaluarAsync(int lineaId, string codigoDeLinea, PedidoDeTasaNegociada pedido, DateOnly fecha, CancellationToken ct)
    {
        var reglas = await ReglasVigentesAsync(lineaId, fecha, null, ct);
        var motor = TasaNegociada.Evaluar(reglas.Select(r => r.ParaElMotor()), pedido, null);
        var regla = motor.Regla is { } aplicada ? reglas.First(r => r.Id == aplicada.Id) : null;
        if (!motor.CabeEnLaRegla) return Result.Success(new NegociacionEvaluada(motor, regla, false, null, []));

        var nivel = await NivelAsync(codigoDeLinea, pedido.Monto, pedido.MargenPedido, fecha, ct);
        if (nivel.IsFailure) return Result.Failure<NegociacionEvaluada>(nivel.Error);
        var (efectivo, niveles) = nivel.Value;
        var cabe = efectivo is null
            ? (await atribucion.ConcedidosAsync(ct)).Contains(PermisoDeAprobacion)
            : await atribucion.PuedeDecidirAsync(efectivo, niveles, ct);
        var conAtribucion = TasaNegociada.Evaluar(reglas.Select(r => r.ParaElMotor()), pedido, cabe ? pedido.MargenPedido : null);
        return Result.Success(new NegociacionEvaluada(conAtribucion, regla, cabe, efectivo, niveles));
    }

    /// <summary>
    /// El nivel efectivo de la matriz vigente para el sujeto <c>NegotiatedRate</c> con el monto del CDAT y el margen pedido; nulo sin matriz vigente (la
    /// decide <see cref="PermisoDeAprobacion"/>).
    /// </summary>
    public async Task<Result<(AttributionLevel? Nivel, IReadOnlyList<AttributionLevel> Niveles)>> NivelAsync(string codigoDeLinea, decimal monto, decimal margen,
        DateOnly fecha, CancellationToken ct)
    {
        var matriz = await matrices.LeerAsync(fecha, reloj.UtcNow, ct);
        if (matriz is null) return Result.Success<(AttributionLevel?, IReadOnlyList<AttributionLevel>)>((null, []));
        try
        {
            var enrutado = EnrutadorDeAtribuciones.Enrutar(new SujetoAEnrutar(LendingDecisionSubjects.NegotiatedRate, monto)
            {
                Linea = codigoDeLinea,
                MargenNegociado = margen,
            }, matriz.ParaElMotor);
            var nivel = matriz.Niveles.First(n => n.Code == enrutado.NivelEfectivo.Codigo);
            return Result.Success<(AttributionLevel?, IReadOnlyList<AttributionLevel>)>((nivel, matriz.Niveles));
        }
        catch (InvalidOperationException e)
        {
            return Result.Failure<(AttributionLevel?, IReadOnlyList<AttributionLevel>)>(new Error(AttributionMatrix.CodigoSinNivelFinal, e.Message));
        }
    }

    /// <summary>El error del motor como respuesta (422 con el máximo de la regla).</summary>
    public static Error Rechazo(EvaluacionDeTasaNegociada e) => new ErrorConDatos(e.CodigoDeRechazo ?? TasaNegociada.CodigoSinRegla, e.Explicacion,
        new { maxMarginEa = e.MargenMaximo, rule = e.Regla?.Codigo });
}
