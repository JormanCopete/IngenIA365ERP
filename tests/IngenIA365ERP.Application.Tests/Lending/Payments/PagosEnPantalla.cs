using FluentAssertions;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Payments.Commands;
using IngenIA365ERP.Application.Lending.Payments.Queries;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Payments;

/// <summary>El escenario común de las pruebas de pagos por pantalla (feature 013, US5, T0520). (nuevo)</summary>
internal static class PagosEnPantalla
{
    /// <summary>
    /// Dos pagos sobre la primera cuota vencida: el primero (100.000) a la fecha del vencimiento más uno y el segundo (200.000) dos días después, con
    /// la causación al día. Reversar el primero redistribuye el segundo.
    /// </summary>
    public static async Task<(Guid Primero, Guid Segundo)> DosPagosAsync(PagosDePrueba p)
    {
        var vencimiento = (await p.Db.RepaymentInstallments.AsNoTracking().OrderBy(i => i.Id).FirstAsync()).DueDate;
        await p.CausarHastaAsync(vencimiento.AddDays(3));
        var primero = await p.AplicadoAsync(p.Pago(ModalidadDePago.FreeAmount, 100_000m, vencimiento.AddDays(1)));
        var segundo = await p.AplicadoAsync(p.Pago(ModalidadDePago.FreeAmount, 200_000m, vencimiento.AddDays(3)));
        return (primero.OperationPublicId, segundo.OperationPublicId);
    }
}
