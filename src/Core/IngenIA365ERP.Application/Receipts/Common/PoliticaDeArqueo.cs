using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// 2026-11-24 (pedido del dueño): si el arqueo es estricto (<c>Caja.ArqueoEstricto</c>, apagado por defecto). Lo leen la caja de Recaudo y la del punto de
/// venta: apagado, el arqueo es sencillo —sin arqueo ciego, motivo sólo fuera de la tolerancia, la diferencia resuelta al cerrar con el faltante al gasto,
/// sin testigo, sin segundo custodio y con los adjuntos opcionales—. Se lee al momento de cada operación (no se sella al abrir), así una sesión abierta
/// antes del cambio arquea con la regla vigente. (nuevo)
/// </summary>
public static class ArqueoEstricto
{
    public static Task<bool> EsAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct) =>
        parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaArqueoEstricto, fecha, ct);
}

/// <summary>2026-11-25 (pedido del dueño): si sólo los cajeros asignados abren la caja, en Recaudo, en el punto de venta y en la caja única. (nuevo)</summary>
public static class SoloCajerosAsignados
{
    public static Task<bool> EsAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct) =>
        parametros.SiNoAsync(ParametrosDeRecaudoYTesoreria.CajaSoloCajerosAsignados, fecha, ct);
}

/// <summary>La forma de contar el efectivo y si el arqueo es estricto, para que las pantallas de abrir y arquear pidan sólo lo que corresponde. (nuevo)</summary>
public sealed record CashCountPolicyDto(string CashCountMode, bool Strict);

/// <summary><c>GET /api/receipts/sessions/count-policy</c>: la política vigente del arqueo (2026-11-24). (nuevo)</summary>
public sealed record GetCashCountPolicyQuery : IRequest<Result<CashCountPolicyDto>>;

public sealed class GetCashCountPolicyQueryHandler(ParametrosDeCaja parametros, IDateTimeService reloj) : IRequestHandler<GetCashCountPolicyQuery, Result<CashCountPolicyDto>>
{
    public async Task<Result<CashCountPolicyDto>> Handle(GetCashCountPolicyQuery request, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        return Result.Success(new CashCountPolicyDto(await FormaDeConteoDelEfectivo.LeerAsync(parametros, hoy, ct), await ArqueoEstricto.EsAsync(parametros, hoy, ct)));
    }
}
