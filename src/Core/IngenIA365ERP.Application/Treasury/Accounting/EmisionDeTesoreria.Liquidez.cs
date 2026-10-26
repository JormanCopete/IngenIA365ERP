using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Treasury;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Treasury.Liquidity;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Treasury.Accounting;

// Feature 015, T2 (fase 43, T0461; E-02 e; FR-139): el saldo del fondo de liquidez que Tesorería informa a Captaciones con la foto de la posición. (nuevo)

public sealed partial class EmisionDeTesoreria
{
    public const string ClaseFotoDeLiquidez = "LiquiditySnapshot";
    public const string TipoDeFotoDeLiquidez = "LQ";

    /// <summary>
    /// <c>SaldoDelFondoDeLiquidez</c> v1 (<c>TES → Lending</c>): origen la foto, clave <c>Confirmation:{foto:N}</c>, modo <c>Always</c>, con la oficina de la primera cuenta
    /// del fondo. Sin guardar.
    /// </summary>
    public async Task<IntegrationMessage> SaldoDelFondoDeLiquidezAsync(LiquiditySnapshot foto, IReadOnlyList<CuentaDelFondoV1> cuentas, int oficinaId, CancellationToken ct)
    {
        var oficina = await db.Branches.AsNoTracking().IgnoreQueryFilters().Where(b => b.Id == oficinaId).Select(b => b.PublicId).FirstAsync(ct);
        var contenido = new SaldoDelFondoDeLiquidezV1
        {
            SnapshotPublicId = foto.PublicId, Date = foto.Date, Accounts = cuentas, Total = cuentas.Sum(c => c.Balance), RegisteredBy = await UsuarioAsync(ct),
        };
        var mensajes = await emisor.EmitirAsync(new SolicitudDeEmision(
            new OrigenDeEmision(MessageOriginKind.Document, foto.PublicId, ClaseFotoDeLiquidez, TipoDeFotoDeLiquidez, foto.Number, foto.Date, oficina),
            ClavesDeEvento.ConfirmacionPor(foto.PublicId), [contenido], new ModoDeEntrega.Sellado(DeliveryMode.Always)), ct);
        return mensajes[0];
    }
}
