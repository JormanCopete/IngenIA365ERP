using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Activation;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Deposits.Accrual;

/// <summary>Lo que la sección de captaciones aportó a un cierre mensual: si aplicó y qué naturalezas cerró. (nuevo)</summary>
public sealed record SeccionDeCaptacionesCerrada(bool Aplica, IReadOnlyList<LineNature> Naturalezas)
{
    public static SeccionDeCaptacionesCerrada NoAplica { get; } = new(false, []);
}

/// <summary>
/// La sección de captaciones del cierre mensual de la Cartera (feature 014, T052; research R10; contabilidad.md §8.3 de la 014): cierre de causación y
/// de posición a fin de mes, <b>sin SARC</b>. Va dentro de la transacción final de <c>CierreMensualDeCartera</c>, antes del informativo
/// <c>CierreDeCarteraRealizado</c> —el mismo para créditos y captaciones: no hay un tipo propio—, así que la guarda de Contabilidad
/// <c>Accounting.Period.LendingNotClosed</c> cubre las captaciones sin cambiar. Sólo aplica con aportes o captaciones activados con fecha ≤ fin de mes.
/// En la fase fundacional no tiene comprobaciones: K8 suma «la causación de cada cuenta llegó al fin de mes» y la posición del último día. Un fallo
/// aquí detiene el cierre como cualquier requisito. (nuevo)
/// </summary>
public sealed class SeccionDeCaptacionesDelCierre(IApplicationDbContext db)
{
    public async Task<Result<SeccionDeCaptacionesCerrada>> CerrarAsync(DateOnly fin, CancellationToken ct)
    {
        var naturalezas = (await ActivacionesDeCaptacion.LeerAsync(db, ct)).DeCaptacionEn(fin);
        return Result.Success(naturalezas.Count == 0 ? SeccionDeCaptacionesCerrada.NoAplica : new SeccionDeCaptacionesCerrada(true, naturalezas));
    }
}
