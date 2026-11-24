using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Receipts.Parameters;
using MediatR;

namespace IngenIA365ERP.Application.Receipts.Common;

/// <summary>
/// 2026-11-23 (pedido del dueño): cómo se cuenta el efectivo al abrir la caja y al arquear, según <c>Caja.FormaDeConteoDelEfectivo</c>
/// (<c>AElegir</c> por defecto, <c>Denominaciones</c> o <c>ValorGlobal</c>). Sólo <c>Denominaciones</c> restringe: rechaza el efectivo registrado con un
/// solo valor. Con <c>ValorGlobal</c> también se admiten los billetes (quien los quiera contar no pierde nada). (nuevo)
/// </summary>
public static class FormaDeConteoDelEfectivo
{
    public static async Task<string> LeerAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct)
    {
        var valor = await parametros.TextoAsync(ParametrosDeRecaudoYTesoreria.CajaFormaDeConteoDelEfectivo, fecha, ct);
        return valor is ParametrosDeRecaudoYTesoreria.ConteoPorDenominaciones or ParametrosDeRecaudoYTesoreria.ConteoPorValorGlobal
            ? valor
            : ParametrosDeRecaudoYTesoreria.ConteoAElegir;
    }

    public static async Task<bool> ExigeDenominacionesAsync(ParametrosDeCaja parametros, DateOnly fecha, CancellationToken ct) =>
        await LeerAsync(parametros, fecha, ct) == ParametrosDeRecaudoYTesoreria.ConteoPorDenominaciones;
}

/// <summary><c>GET /api/receipts/sessions/cash-count-mode</c>: la forma vigente de contar el efectivo (<c>AElegir</c>, <c>Denominaciones</c> o
/// <c>ValorGlobal</c>), para que la pantalla de abrir y arquear muestre sólo lo que corresponde. (nuevo)</summary>
public sealed record GetCashCountModeQuery : IRequest<Result<string>>;

public sealed class GetCashCountModeQueryHandler(ParametrosDeCaja parametros, IDateTimeService reloj) : IRequestHandler<GetCashCountModeQuery, Result<string>>
{
    public async Task<Result<string>> Handle(GetCashCountModeQuery request, CancellationToken ct) =>
        Result.Success(await FormaDeConteoDelEfectivo.LeerAsync(parametros, reloj.HoyLocal, ct));
}
