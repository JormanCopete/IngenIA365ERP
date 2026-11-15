using FluentAssertions;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Sales.Payments;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// 2026-11-12, la factura de venta de oficina en producción: el borrador sin pagos avisaba «Los pagos no suman exactamente el valor a
/// pagar» mientras la persona todavía armaba la factura, y quitar la última línea dejaba el borrador con sus líneas en el servidor. El
/// borrador sin pagos no lo avisa (con pagos que no cuadran, sí), confirmar sin pagos se rechaza diciéndolo, y un borrador guardado se
/// guarda también sin líneas (confirmarlo las sigue exigiendo).
/// </summary>
public class BorradorDeFacturaTests
{
    private static async Task<IReadOnlyList<SalesIssueDto>> AvisosAsync(VentasDePrueba v, Guid documento)
    {
        var detalle = await new GetSalesDocumentQueryHandler(v.Db, v.K.Vista(), v.K.Alcance).Handle(new GetSalesDocumentQuery(documento), default);
        detalle.IsSuccess.Should().BeTrue(detalle.IsFailure ? detalle.Error.Message : string.Empty);
        return detalle.Value.Issues;
    }

    [Fact]
    public async Task Sin_pagos_el_borrador_no_avisa_que_no_suman_y_confirmar_dice_que_faltan_los_pagos()
    {
        var v = await VentasDePrueba.CrearAsync();
        var borrador = await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P1, 1m)]));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? borrador.Error.Message : string.Empty);

        var avisos = await AvisosAsync(v, borrador.Value.PublicId);
        var confirmar = await v.ConfirmarAsync(borrador.Value.PublicId);

        avisos.Should().NotContain(i => i.Code == ValidadorDePagos.TotalMismatch);
        confirmar.IsFailure.Should().BeTrue("sin pagos no se confirma");
        confirmar.Error.Code.Should().Be(ValidadorDePagos.TotalMismatch);
        confirmar.Error.Message.Should().Contain("No hay pagos registrados");
    }

    [Fact]
    public async Task Con_pagos_que_no_cuadran_el_borrador_lo_avisa()
    {
        var v = await VentasDePrueba.CrearAsync();
        var borrador = await v.GuardarAsync(v.Venta(pagos: [v.Pago(v.Efectivo, 1m)], lineas: [v.Linea(v.P1, 1m)]));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? borrador.Error.Message : string.Empty);

        var avisos = await AvisosAsync(v, borrador.Value.PublicId);

        avisos.Should().Contain(i => i.Code == ValidadorDePagos.TotalMismatch);
    }

    [Fact]
    public async Task Un_borrador_guardado_se_guarda_sin_lineas_y_confirmarlo_las_exige()
    {
        var v = await VentasDePrueba.CrearAsync();
        var borrador = await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P1, 1m)]));
        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? borrador.Error.Message : string.Empty);

        var vacio = await v.GuardarAsync(v.Venta(), borrador.Value.PublicId);

        vacio.IsSuccess.Should().BeTrue(vacio.IsFailure ? vacio.Error.Message : string.Empty);
        var documento = v.Documento(borrador.Value.PublicId);
        documento.Lines.Where(l => !l.IsDeleted).Should().BeEmpty("quitar la última línea también se guarda");
        documento.AmountDue.Should().Be(0m);
        (await AvisosAsync(v, borrador.Value.PublicId)).Should().Contain(i => i.Code == InventoryErrors.Empty().Code);
        (await v.ConfirmarAsync(borrador.Value.PublicId)).IsFailure.Should().BeTrue("una factura sin líneas no se confirma");
    }
}
