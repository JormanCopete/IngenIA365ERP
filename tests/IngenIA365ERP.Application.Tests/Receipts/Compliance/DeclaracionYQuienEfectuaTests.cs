using FluentAssertions;
using IngenIA365ERP.Application.Receipts.CashDesk;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Tests.Receipts.CashDesk;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Receipts.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Receipts.Compliance;

/// <summary>
/// Feature 015, US14, T0233 (FR-094, FR-191; C9, E-02 c): la declaración de origen de fondos se exige desde <c>Sarlaft.UmbralDeclaracionOrigenFondos</c> sobre el efectivo
/// del recibo y se captura una vez por recibo con su soporte; con ella, el recibo sale y la cita; quien efectúa se exige sobre el umbral de la transacción individual. Prueba
/// escrita, sin correr.
/// </summary>
public class DeclaracionYQuienEfectuaTests
{
    private readonly CajaConContratos _k = new();

    private async Task<CashOperationDto> OperacionAsync(decimal efectivo)
    {
        await _k.AbrirAsync();
        var op = (await _k.Crear().Handle(new CreateCashOperationCommand(_k.Sesion.PublicId, _k.Asociado.PublicId), default)).Value;
        await _k.Items().Handle(new UpdateCashOperationItemsCommand(op.PublicId, [_k.Cuota(_k.CreditoA, efectivo)]), default);
        return (await _k.Medios().Handle(new UpdateCashOperationMeansCommand(op.PublicId, [_k.Efectivo(efectivo)]), default)).Value;
    }

    [Fact]
    public async Task Sobre_el_umbral_se_exige_la_declaracion_y_con_ella_el_recibo_la_cita()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, "500000");
        var op = await OperacionAsync(600_000m);

        var sin = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);
        sin.Error.Code.Should().Be(ErroresDeRecaudo.CodigoDeclaracionRequerida);

        (await _k.Declarar().Handle(new DeclareSourceOfFundsCommand(op.PublicId, "Venta de un vehículo", 600_000m, Guid.NewGuid()) { OperationKey = Guid.NewGuid() }, default))
            .IsSuccess.Should().BeTrue();
        var quien = new Person { FirstName = "Ana", LastName = "Asociada", TaxId = "2009" };
        _k.C.E.Db.People.Add(quien);
        await _k.C.E.Db.SaveChangesAsync();
        var conDeclaracion = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        conDeclaracion.IsSuccess.Should().BeTrue(conDeclaracion.IsFailure ? conDeclaracion.Error.Code : null);
        (await _k.C.E.Db.IssuedReceipts.SingleAsync()).SourceOfFundsDeclarationId.Should().NotBeNull();
        (await _k.C.E.Db.SourceOfFundsDeclarations.CountAsync()).Should().Be(1, "la declaración se captura una vez por recibo");
    }

    [Fact]
    public async Task Sobre_el_umbral_individual_quien_efectua_es_obligatorio()
    {
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralDeclaracionOrigenFondos, "900000000");
        _k.C.E.Parametro(ParametrosDeRecaudoYTesoreria.SarlaftUmbralTransaccionIndividual, "500000");
        var op = await OperacionAsync(600_000m);

        var r = await _k.Confirmacion().ConfirmarAsync(op.PublicId, Guid.NewGuid(), null, default);

        r.Error.Code.Should().Be(ErroresDeRecaudo.QuienEfectuaRequerido("CashThreshold", 500_000m).Code);
    }
}
