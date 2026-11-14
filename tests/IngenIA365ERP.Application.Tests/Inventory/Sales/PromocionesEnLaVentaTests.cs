using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Inventory.Pricing.Promotions;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// Feature 012, I6, T867 (FR-055, US14-4, F9; contracts/api.md §19.4; data-model §14 «Promociones»): la promoción 3×2 vigente se aplica
/// al guardar el borrador de venta y al agregar la línea en el POS como descuento no condicionado de la línea (<c>Source = Promotion</c>
/// con su <c>PromotionId</c>); el descuento baja la base de <c>INV_DocumentTaxLines</c>; la consulta de la venta trae
/// <c>source = Promotion</c> y <c>promotionPublicId</c>; un descuento manual en una línea con promoción se rechaza con
/// <c>Inventory.Discount.PromotionApplied</c>; y una promoción ya usada en un documento confirmado sólo cambia nombre, fin de vigencia y
/// activo (<c>Inventory.Promotion.InUse</c>).
/// </summary>
public class PromocionesEnLaVentaTests
{
    private static readonly DateOnly Desde = new(2026, 9, 1);
    private static readonly DateOnly Hasta = new(2026, 12, 31);

    private static async Task<(VentasDePrueba V, PromotionDto Promocion)> ConTresPorDosAsync()
    {
        var v = await VentasDePrueba.CrearAsync();
        var r = await new CreatePromotionCommandHandler(v.Db).Handle(TresPorDos(v), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return (v, r.Value);
    }

    private static CreatePromotionCommand TresPorDos(VentasDePrueba v, string codigo = "3X2ACEITE") => new(
        codigo, "Aceite lleve 3 pague 2", PromotionKind.BuyNPayM, Desde, Hasta, "Temporada",
        BuyQuantity: 3m, PayQuantity: 2m, Scopes: [new PromotionScopeInput(ProductPublicId: v.P3)]);

    // ------------------------------------------------------------------------------------------------- oficina --

    [Fact]
    public async Task El_borrador_de_venta_aplica_el_3x2_como_descuento_de_la_linea_y_baja_la_base_del_IVA()
    {
        var (v, promocion) = await ConTresPorDosAsync();

        var borrador = await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P3, 3m)]));

        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : string.Empty);
        var documento = v.Documento(borrador.Value.PublicId);
        var linea = documento.Lines.Single();
        (linea.GrossAmount, linea.DiscountAmount, linea.NetAmount).Should().Be((30000m, 10000m, 20000m), "se regala una de las tres a 10.000");
        var descuento = v.Db.DocumentLineDiscounts.Single(d => d.DocumentLineId == linea.Id && !d.IsDeleted);
        descuento.Source.Should().Be(DiscountSource.Promotion);
        descuento.PromotionId.Should().Be(v.Db.Promotions.Single(p => p.PublicId == promocion.PromotionPublicId).Id);
        descuento.RequiresApproval.Should().BeFalse("una promoción no es un descuento del cajero: no se mide contra su tope");
        documento.TaxTotal.Should().Be(3800m, "el IVA es sobre 20.000, no sobre 30.000");
    }

    [Fact]
    public async Task Confirmada_la_base_de_los_impuestos_es_el_neto_y_la_consulta_trae_la_promocion()
    {
        var (v, promocion) = await ConTresPorDosAsync();

        var venta = await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P3, 3m)]);
        var detalle = await new GetSalesDocumentQueryHandler(v.Db, v.K.Vista(), v.K.Alcance).Handle(new GetSalesDocumentQuery(venta.PublicId), default);

        v.Db.DocumentTaxLines.Where(t => t.DocumentId == venta.Id && t.Treatment == TaxTreatment.Generated).Select(t => t.Base).Should().Equal(20000m);
        var descuento = detalle.Value.Lines.Single().Discounts.Single();
        descuento.Source.Should().Be(DiscountSource.Promotion);
        descuento.PromotionPublicId.Should().Be(promocion.PromotionPublicId);
        descuento.Amount.Should().Be(10000m);
    }

    [Fact]
    public async Task Un_descuento_manual_en_una_linea_con_promocion_se_rechaza()
    {
        var (v, _) = await ConTresPorDosAsync();

        var conManual = await v.GuardarAsync(v.Venta(lineas:
            [v.Linea(v.P1, 1m), v.Linea(v.P3, 3m) with { Discount = new SalesDiscountInput(Percent: 0.05m) }]));
        var sinManual = await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P1, 1m), v.Linea(v.P3, 3m)]));

        conManual.Error.Code.Should().Be(ErroresDePrecios.PromotionAppliedCode);
        conManual.Error.Message.Should().Contain("líneas 2 ").And.Contain("3X2ACEITE");
        sinManual.IsSuccess.Should().BeTrue("sin el manual, la promoción entra sola");
    }

    // ---------------------------------------------------------------------------------------------------- POS --

    [Fact]
    public async Task En_el_POS_la_promocion_aparece_al_agregar_la_linea_y_no_admite_descuento_manual()
    {
        var (v, promocion) = await ConTresPorDosAsync();
        v.Caja.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = v.Caja, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = v.K.Tipo("RV").Id });
        await v.Db.SaveChangesAsync();
        var pos = new BorradorDelPos(v.Db, v.K.Actor, v.Compras.C.Reloj, v.K.Alcance, v.K.Maestros(), v.Precificacion(), v.Aprobaciones(), v.K.Motor, v.K.Permisos);
        var codigo = v.Compras.C.Producto(v.P3).Code;

        var abierta = await new CreatePosDraftCommandHandler(v.Db, pos).Handle(new CreatePosDraftCommand(v.Sesion.PublicId), default);
        abierta.IsSuccess.Should().BeTrue(abierta.IsFailure ? $"{abierta.Error.Code}: {abierta.Error.Message}" : string.Empty);
        var dos = await new AddPosLineCommandHandler(v.Db, pos).Handle(new AddPosLineCommand(abierta.Value.DraftPublicId, codigo, Quantity: 2m), default);
        var tres = await new AddPosLineCommandHandler(v.Db, pos).Handle(new AddPosLineCommand(abierta.Value.DraftPublicId, codigo), default);

        dos.Value.Lines.Single().Discounts.Should().BeEmpty("con dos no hay juego completo");
        var linea = tres.Value.Lines.Single();
        linea.Quantity.Should().Be(3m);
        linea.Discounts.Should().ContainSingle(d => d.Source == DiscountSource.Promotion && d.Amount == 10000m && d.PromotionPublicId == promocion.PromotionPublicId);
        tres.Value.Totals.AmountDue.Should().Be(23800m);

        var manual = await new UpdatePosLineCommandHandler(v.Db, pos, Substitute.For<IAuditoriaDelPuntoDeVenta>()).Handle(
            new UpdatePosLineCommand(abierta.Value.DraftPublicId, linea.LinePublicId, Discount: new PosDiscountInput(Percent: 0.05m)), default);
        manual.Error.Code.Should().Be(ErroresDePrecios.PromotionAppliedCode);
    }

    // ------------------------------------------------------------------- 2026-11-13: descuento al total y origen --

    /// <summary>Un tope del 20 % para quien vende: el descuento al total de estas pruebas no pide aprobación.</summary>
    private static async Task ConTopeAsync(VentasDePrueba v)
    {
        var rol = new Domain.Entities.Security.Role { Code = "VENDEDOR", Name = "Vendedores", IsActive = true };
        v.Db.Roles.Add(rol);
        await v.Db.SaveChangesAsync();
        v.Db.UserRoles.Add(new Domain.Entities.Security.UserRole
        {
            UserId = Kardex.KardexDePrueba.Usuario, RoleId = rol.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba",
        });
        v.Db.DiscountCaps.Add(new Domain.Entities.Inventory.Pricing.DiscountCap
        {
            RoleId = rol.Id, MaxLineRate = 0.2m, MaxDocumentRate = 0.2m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Comité",
        });
        await v.Db.SaveChangesAsync();
    }

    [Fact]
    public async Task El_porcentaje_al_total_va_sobre_las_lineas_sin_promocion_en_la_oficina()
    {
        var (v, _) = await ConTresPorDosAsync();
        await ConTopeAsync(v);

        var borrador = await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P1, 1m), v.Linea(v.P3, 3m)]) with
        {
            DocumentDiscount = new SalesDiscountInput(Percent: 0.1m),
        });

        borrador.IsSuccess.Should().BeTrue(borrador.IsFailure ? $"{borrador.Error.Code}: {borrador.Error.Message}" : string.Empty);
        var documento = v.Documento(borrador.Value.PublicId);
        var delTotal = v.Db.DocumentLineDiscounts.Where(d => d.DocumentId == documento.Id && !d.IsDeleted && d.FromDocumentDiscount).ToList();
        delTotal.Sum(d => d.Amount).Should().Be(200m, "el 10 % de los 2.000 del arroz: la línea con el 3×2 no recibe parte");
    }

    [Fact]
    public async Task El_porcentaje_al_total_del_POS_usa_la_misma_base_que_la_oficina()
    {
        var (v, _) = await ConTresPorDosAsync();
        await ConTopeAsync(v);
        v.Caja.DocumentTypes.Add(new CashRegisterDocumentType { CashRegister = v.Caja, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = v.K.Tipo("RV").Id });
        await v.Db.SaveChangesAsync();
        var pos = new BorradorDelPos(v.Db, v.K.Actor, v.Compras.C.Reloj, v.K.Alcance, v.K.Maestros(), v.Precificacion(), v.Aprobaciones(), v.K.Motor, v.K.Permisos);
        var abierta = await new CreatePosDraftCommandHandler(v.Db, pos).Handle(new CreatePosDraftCommand(v.Sesion.PublicId), default);
        await new AddPosLineCommandHandler(v.Db, pos).Handle(new AddPosLineCommand(abierta.Value.DraftPublicId, v.Compras.C.Producto(v.P1).Code), default);
        await new AddPosLineCommandHandler(v.Db, pos).Handle(new AddPosLineCommand(abierta.Value.DraftPublicId, v.Compras.C.Producto(v.P3).Code, Quantity: 3m), default);
        var cabecera = new UpdatePosDraftCommandHandler(v.Db, pos, Substitute.For<IAuditoriaDelPuntoDeVenta>());

        var conPorcentaje = await cabecera.Handle(new UpdatePosDraftCommand(abierta.Value.DraftPublicId, DocumentDiscount: new PosDiscountInput(Percent: 0.1m)), default);
        var conLosDos = await cabecera.Handle(new UpdatePosDraftCommand(abierta.Value.DraftPublicId, DocumentDiscount: new PosDiscountInput(Percent: 0.1m, Amount: 100m)), default);

        conPorcentaje.IsSuccess.Should().BeTrue(conPorcentaje.IsFailure ? $"{conPorcentaje.Error.Code}: {conPorcentaje.Error.Message}" : string.Empty);
        conPorcentaje.Value.Lines.SelectMany(l => l.Discounts).Where(d => d.FromDocumentDiscount).Sum(d => d.Amount)
            .Should().Be(200m, "antes se calculaba sobre 22.000 (con la línea de la promoción) y no cabía en los 2.000 que lo reciben");
        conLosDos.Error.Code.Should().Be(ErroresDePrecios.PercentAndAmountCode);
    }

    [Fact]
    public async Task El_descuento_que_la_linea_trae_de_su_origen_cede_a_la_promocion_y_lo_digitado_no()
    {
        var (v, _) = await ConTresPorDosAsync();
        var p3 = v.K.ProductoId(v.P3);
        var und = v.Compras.C.Unidad("UND").Id;
        PedidoDePrecificacion Pedido(bool cede) => new(Catalog.CatalogoDePrueba.Hoy, null, null, null, v.K.Principal.Id, Kardex.KardexDePrueba.Usuario,
            [new LineaAPrecificar(1, p3, und, 3m, 3m, DescuentoPorcentaje: 0.05m, CedeAPromocion: cede)]);

        var heredado = await v.Precificacion().PrecificarAsync(Pedido(cede: true), default);
        var digitado = await v.Precificacion().PrecificarAsync(Pedido(cede: false), default);

        heredado.IsSuccess.Should().BeTrue(heredado.IsFailure ? $"{heredado.Error.Code}: {heredado.Error.Message}" : string.Empty);
        heredado.Value.Lineas.Single().Descuentos.Should().ContainSingle().Which.Source.Should().Be(DiscountSource.Promotion);
        heredado.Value.Avisos.Should().Contain(a => a.Contains("3X2ACEITE") && a.Contains("origen"));
        digitado.Error.Code.Should().Be(ErroresDePrecios.PromotionAppliedCode);
    }

    // --------------------------------------------------------------------------------------- mantenimiento --

    [Fact]
    public async Task Usada_en_un_documento_confirmado_solo_cambia_nombre_fin_de_vigencia_y_activo()
    {
        var (v, promocion) = await ConTresPorDosAsync();
        await v.VentaConfirmadaAsync(lineas: [v.Linea(v.P3, 3m)]);
        var editar = new UpdatePromotionCommandHandler(v.Db);

        var otroValor = await editar.Handle(new UpdatePromotionCommand(promocion.PromotionPublicId, "Aceite 3×2", Hasta, true, "Cambio", PayQuantity: 1m), default);
        var permitido = await editar.Handle(new UpdatePromotionCommand(promocion.PromotionPublicId, "Aceite 3×2 (fin)", new DateOnly(2026, 10, 31), false, "Cierre"), default);

        otroValor.Error.Code.Should().Be(ErroresDePrecios.PromotionInUseCode);
        permitido.IsSuccess.Should().BeTrue(permitido.IsFailure ? permitido.Error.Message : string.Empty);
        (permitido.Value.Name, permitido.Value.ValidTo, permitido.Value.IsActive, permitido.Value.InUse).Should()
            .Be(("Aceite 3×2 (fin)", new DateOnly(2026, 10, 31), false, true));
        permitido.Value.PayQuantity.Should().Be(2m);
    }

    [Fact]
    public async Task Sin_uso_confirmado_se_cambia_entera_y_la_lista_filtra_por_fecha_y_activo()
    {
        var (v, promocion) = await ConTresPorDosAsync();
        await v.GuardarAsync(v.Venta(lineas: [v.Linea(v.P3, 3m)])); // un borrador no la vuelve «usada»

        var cambiada = await new UpdatePromotionCommandHandler(v.Db).Handle(new UpdatePromotionCommand(promocion.PromotionPublicId, "Aceite al 10 %", Hasta, true,
            "Cambio de mecánica", Kind: PromotionKind.Percent, Percent: 0.10m), default);
        var vigentes = await new ListPromotionsQueryHandler(v.Db).Handle(new ListPromotionsQuery(new DateOnly(2026, 10, 1), true), default);
        var antes = await new ListPromotionsQueryHandler(v.Db).Handle(new ListPromotionsQuery(new DateOnly(2026, 8, 1)), default);
        var una = await new GetPromotionQueryHandler(v.Db).Handle(new GetPromotionQuery(promocion.PromotionPublicId), default);

        cambiada.IsSuccess.Should().BeTrue(cambiada.IsFailure ? cambiada.Error.Message : string.Empty);
        (cambiada.Value.Kind, cambiada.Value.Percent, cambiada.Value.BuyQuantity, cambiada.Value.InUse).Should().Be((PromotionKind.Percent, 0.10m, null, false));
        vigentes.Value.Should().ContainSingle(p => p.Code == "3X2ACEITE");
        antes.Value.Should().BeEmpty();
        una.Value.Scopes.Should().ContainSingle(s => s.Kind == PromotionScopeKind.Product && s.ProductPublicId == v.P3);
    }

    [Fact]
    public async Task El_codigo_es_unico_y_la_clase_debe_ser_coherente_con_sus_campos()
    {
        var (v, _) = await ConTresPorDosAsync();
        var validador = new CreatePromotionCommandValidator();

        var repetida = await new CreatePromotionCommandHandler(v.Db).Handle(TresPorDos(v), default);
        var pagaMasDeLoQueLleva = validador.Validate(TresPorDos(v, "MAL") with { PayQuantity = 3m });
        var porcentajeSinTasa = validador.Validate(new CreatePromotionCommand("PCT", "Sin tasa", PromotionKind.Percent, Desde, Hasta, "Prueba"));
        var tramosSinTramos = validador.Validate(new CreatePromotionCommand("VOL", "Volumen", PromotionKind.QuantityPrice, Desde, Hasta, "Prueba"));
        var paqueteSinCantidades = validador.Validate(new CreatePromotionCommand("PAQ", "Paquete", PromotionKind.BundlePrice, Desde, Hasta, "Prueba",
            BundlePrice: 5000m, Scopes: [new PromotionScopeInput(ProductPublicId: v.P3)]));
        var alReves = validador.Validate(TresPorDos(v, "FECHA") with { ValidTo = new DateOnly(2026, 8, 1) });
        var bien = validador.Validate(new CreatePromotionCommand("VOL", "Volumen", PromotionKind.QuantityPrice, Desde, Hasta, "Prueba",
            Tiers: [new PromotionTierInput(6m, 9000m), new PromotionTierInput(12m, 8000m)]));

        repetida.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        pagaMasDeLoQueLleva.IsValid.Should().BeFalse();
        porcentajeSinTasa.IsValid.Should().BeFalse();
        tramosSinTramos.IsValid.Should().BeFalse();
        paqueteSinCantidades.IsValid.Should().BeFalse();
        alReves.IsValid.Should().BeFalse();
        bien.IsValid.Should().BeTrue(string.Join("; ", bien.Errors.Select(e => e.ErrorMessage)));
    }
}
