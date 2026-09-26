using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Inventory.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using O = IngenIA365ERP.Application.Accounting.Inventory.Reglas.OperacionesDeInventario;
using R = IngenIA365ERP.Application.Accounting.Inventory.Reglas.RolesDeCuenta;

namespace IngenIA365ERP.Application.Tests.Accounting.Inventory;

/// <summary>
/// Feature 012, T451 (T27, T28; contracts/contabilidad.md §2.4–§2.6; api.md §26.1–§26.2): crear, versión, desactivar y
/// mapeo. Dimensiones (<c>DimensionRequired</c>, <c>DimensionNotAllowed</c>, <c>DimensionCodeUnknown</c> por
/// <c>IDimensionesDeInventario</c>), cuenta (<c>AccountEligibility</c> para INV, clase de impuesto, tarifa del catálogo),
/// vigencia (la versión cierra la anterior la víspera, <c>Overlaps</c>, <c>RetroactiveOverPosted</c> con la excepción de
/// la clave nueva), desactivar con motivo y sin borrar, y el mapeo sólo a tipos <c>Module</c>/<c>INV</c> activos.
/// </summary>
public class InventoryPostingRuleCommandsTests
{
    private static readonly DateOnly Marzo1 = new(2026, 3, 1);

    private sealed class Escenario
    {
        public MatrizDePrueba M { get; } = new();
        public ReglasDeLaMatriz Reglas { get; }

        public Escenario() => Reglas = new ReglasDeLaMatriz(M.D.Db, M.Dimensiones, new ResolutorDeReglas(M.D.Db));

        public Task<Result<ReglaGuardadaDto>> CrearAsync(string operacion, string rol, DimensionesDeLaReglaDto dimensiones, ChartOfAccount cuenta,
            DateOnly? desde = null) =>
            new CreateInventoryPostingRuleCommandHandler(M.D.Db, Reglas)
                .Handle(new CreateInventoryPostingRuleCommand(operacion, rol, dimensiones, cuenta.PublicId, desde ?? MatrizDePrueba.Enero1, null, "Matriz aprobada"), default);

        public Task<Result<ReglaGuardadaDto>> VersionAsync(Guid regla, ChartOfAccount cuenta, DateOnly desde) =>
            new AddInventoryPostingRuleVersionCommandHandler(M.D.Db, Reglas)
                .Handle(new AddInventoryPostingRuleVersionCommand(regla, cuenta.PublicId, desde, "Cambio de cuenta", "La contadora lo pidió"), default);

        public Task<Result> DesactivarAsync(Guid regla, DateOnly hasta) =>
            new DeactivateInventoryPostingRuleCommandHandler(M.D.Db, Reglas)
                .Handle(new DeactivateInventoryPostingRuleCommand(regla, hasta, "Ya no se usa"), default);
    }

    private static readonly DimensionesDeLaReglaDto Abarrotes = new(AccountingGroupCode: "ABARROTES");

    // ------------------------------------------------------------------------------------------------ dimensiones --

    [Fact]
    public async Task Crea_la_regla_con_sus_dimensiones_clave_y_peso()
    {
        var e = new Escenario();

        var r = await e.CrearAsync(O.Compra, R.Inventario, new DimensionesDeLaReglaDto("abarrotes", "b01", BranchPublicId: e.M.D.Principal.PublicId), e.M.Inventario);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var regla = await e.M.D.Db.InventoryPostingRules.SingleAsync(x => x.PublicId == r.Value.RulePublicId);
        regla.AccountingGroupCode.Should().Be("ABARROTES");
        regla.WarehouseCode.Should().Be("B01");
        regla.BranchId.Should().Be(e.M.D.Principal.Id);
        regla.SpecificityWeight.Should().Be(22, "bodega 16 + sucursal 4 + grupo 2");
        regla.DimensionKey.Should().StartWith("Compra|Inventario|G:ABARROTES|W:B01|");
        regla.Notes.Should().Be("Matriz aprobada", "sin notas, queda el motivo: quién decidió y por qué");
    }

    [Fact]
    public async Task Una_dimension_exigida_ausente_o_una_no_admitida_se_rechazan()
    {
        var e = new Escenario();

        (await e.CrearAsync(O.Compra, R.Inventario, new DimensionesDeLaReglaDto(), e.M.Inventario))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionRequired");
        (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes with { PaymentMeansCode = "EFE" }, e.M.Inventario))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionNotAllowed");
        (await e.CrearAsync(O.Baja, R.Contrapartida, new DimensionesDeLaReglaDto(), e.M.SinInventario))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionRequired", "la contrapartida de una baja exige la causa");
        (await e.CrearAsync(O.Compra, R.Ingreso, Abarrotes, e.M.Inventario))
            .Error.Code.Should().Be("Accounting.InventoryRule.RoleNotInOperation");

        var validador = new CreateInventoryPostingRuleCommandValidator();
        var forma = await validador.ValidateAsync(new CreateInventoryPostingRuleCommand(O.Venta, R.Impuesto, Abarrotes, Guid.NewGuid(), Marzo1, null, "motivo"));
        forma.IsValid.Should().BeFalse("el validador responde 400 por la forma");
        forma.Errors.Select(x => x.ErrorCode).Should().Contain(["Accounting.InventoryRule.DimensionRequired", "Accounting.InventoryRule.DimensionNotAllowed"]);
    }

    [Fact]
    public async Task Un_codigo_que_Inventario_no_conoce_se_rechaza()
    {
        var e = new Escenario();

        HabilitarInv(e, e.M.SinInventario);
        var r = await e.CrearAsync(O.Compra, R.Inventario, new DimensionesDeLaReglaDto("LACTEOS"), e.M.Inventario);
        r.Error.Code.Should().Be("Accounting.InventoryRule.DimensionCodeUnknown");
        (await e.CrearAsync(O.Baja, R.Contrapartida, new DimensionesDeLaReglaDto(ReasonCode: "OTRA"), e.M.SinInventario))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionCodeUnknown", "la causa la publica Inventario");
        (await e.CrearAsync(O.Baja, R.Contrapartida, new DimensionesDeLaReglaDto(ReasonCode: "VENC"), e.M.SinInventario)).IsSuccess.Should().BeTrue();
        (await e.CrearAsync(O.DiferenciaDeArqueo, R.Sobrante, new DimensionesDeLaReglaDto(ReasonCode: "ShortageToCashier"), e.M.Caja))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionCodeUnknown", "el sobrante sólo admite Surplus");
        await e.M.Dimensiones.Received().CatalogoAsync(Arg.Any<CancellationToken>());
    }

    private static void HabilitarInv(Escenario e, ChartOfAccount cuenta)
    {
        cuenta.EnabledModules |= AccountingModules.Inventory;
        e.M.D.Db.SaveChanges();
    }

    // ----------------------------------------------------------------------------------------------------- cuenta --

    [Fact]
    public async Task La_cuenta_tiene_que_ser_elegible_para_Inventario()
    {
        var e = new Escenario();

        var r = await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.SinInventario);
        r.Error.Code.Should().Be("Accounting.Account.NotEligible");
        r.Error.Message.Should().Contain("no está habilitada para Inventario");

        var agrupadora = e.M.D.Cuenta("1435", movimiento: false, modulos: AccountingModules.Inventory);
        (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, agrupadora)).Error.Code.Should().Be("Accounting.Account.NotEligible");
    }

    [Fact]
    public async Task En_los_roles_de_impuesto_la_clase_de_la_cuenta_y_la_tarifa_del_catalogo_se_comprueban()
    {
        var e = new Escenario();
        var iva = new DimensionesDeLaReglaDto(TaxRateCode: "IVA19", TaxRate: 0.19m);

        (await e.CrearAsync(O.Venta, R.Impuesto, iva, e.M.Retencion)).Error.Code
            .Should().Be("Accounting.Account.NotEligible", "una cuenta de retención en la fuente no recibe IVA");
        (await e.CrearAsync(O.Venta, R.Impuesto, iva with { TaxRate = 0.16m }, e.M.IvaGenerado)).Error.Code
            .Should().Be("Accounting.InventoryRule.TaxRateMismatch", "la tarifa debe ser la del catálogo en ValidFrom");

        var ok = await e.CrearAsync(O.Venta, R.Impuesto, iva, e.M.IvaGenerado);
        ok.IsSuccess.Should().BeTrue(ok.Error?.Message);
        ok.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Si_la_cuenta_tiene_otra_tarifa_en_algun_tramo_se_guarda_con_aviso()
    {
        var e = new Escenario();
        e.M.IvaGenerado.TaxRates.Add(new AccountTaxRate { ValidFrom = new DateOnly(2026, 7, 1), Rate = 0.16m, CreatedBy = "test" });
        e.M.D.Db.SaveChanges();

        var r = await e.CrearAsync(O.Venta, R.Impuesto, new DimensionesDeLaReglaDto(TaxRateCode: "IVA19", TaxRate: 0.19m), e.M.IvaGenerado);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Warnings.Should().ContainSingle(a => a.Code == "Accounting.InventoryRule.TaxRateMismatch");
    }

    // --------------------------------------------------------------------------------------------------- vigencia --

    [Fact]
    public async Task Una_version_nueva_cierra_la_vigente_la_vispera()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario)).Value.RulePublicId;

        var r = await e.VersionAsync(original, e.M.InventarioBodega, Marzo1);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var reglas = await e.M.D.Db.InventoryPostingRules.OrderBy(x => x.ValidFrom).ToListAsync();
        reglas.Should().HaveCount(2);
        reglas[0].ValidTo.Should().Be(new DateOnly(2026, 2, 28));
        reglas[1].ValidFrom.Should().Be(Marzo1);
        reglas[1].AccountId.Should().Be(e.M.InventarioBodega.Id);
        reglas[1].DimensionKey.Should().Be(reglas[0].DimensionKey);
        reglas[1].Notes.Should().Be("Cambio de cuenta");
    }

    [Fact]
    public async Task Dos_vigencias_de_la_misma_clave_no_se_cruzan()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario)).Value.RulePublicId;

        (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.InventarioBodega, Marzo1))
            .Error.Code.Should().Be("Accounting.InventoryRule.Overlaps", "crear no reemplaza: para eso está la versión");
        (await e.VersionAsync(original, e.M.InventarioBodega, MatrizDePrueba.Enero1))
            .Error.Code.Should().Be("Accounting.InventoryRule.Overlaps", "una versión en la misma fecha pisa a la vigente");
    }

    [Fact]
    public async Task Una_version_no_empieza_en_ni_antes_de_lo_ya_contabilizado()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario)).Value.RulePublicId;
        e.M.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra(), "CO-7");

        var r = await e.VersionAsync(original, e.M.InventarioBodega, new DateOnly(2026, 3, 9));

        r.Error.Code.Should().Be("Accounting.InventoryRule.RetroactiveOverPosted");
        var datos = ((ErrorConDatos)r.Error).Data;
        datos.GetType().GetProperty("lastPostedDate")!.GetValue(datos).Should().Be(new DateOnly(2026, 3, 9));
        datos.GetType().GetProperty("document")!.GetValue(datos).Should().Be("CO CO-7");
        (await e.VersionAsync(original, e.M.InventarioBodega, new DateOnly(2026, 3, 10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_clave_nueva_puede_empezar_antes_si_nada_contabilizado_coincide()
    {
        var e = new Escenario();
        e.M.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra("ABARROTES", "B01"), "CO-7");

        (await e.CrearAsync(O.Compra, R.Inventario, new DimensionesDeLaReglaDto("ASEO"), e.M.Inventario, Marzo1))
            .IsSuccess.Should().BeTrue("ninguna compra contabilizada movió ASEO: rescata mensajes rechazados sin cambiar nada (FR-078)");
        (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario, Marzo1))
            .Error.Code.Should().Be("Accounting.InventoryRule.RetroactiveOverPosted", "la compra CO-7 movió ABARROTES");
    }

    // ------------------------------------------------------------------------------------------------- desactivar --

    [Fact]
    public async Task Desactivar_exige_motivo_fija_ValidTo_y_nunca_borra()
    {
        var e = new Escenario();
        var id = (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario)).Value.RulePublicId;

        var sinMotivo = await new DeactivateInventoryPostingRuleCommandValidator().ValidateAsync(new DeactivateInventoryPostingRuleCommand(id, Marzo1, " "));
        sinMotivo.IsValid.Should().BeFalse();

        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 31))).IsSuccess.Should().BeTrue();
        var regla = await e.M.D.Db.InventoryPostingRules.SingleAsync();
        regla.ValidTo.Should().Be(new DateOnly(2026, 3, 31));
        regla.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Desactivar_antes_de_lo_contabilizado_se_rechaza()
    {
        var e = new Escenario();
        var id = (await e.CrearAsync(O.Compra, R.Inventario, Abarrotes, e.M.Inventario)).Value.RulePublicId;
        e.M.Contabilizado(CompraRecibidaV1.Type, new DateOnly(2026, 3, 9), MatrizDePrueba.Compra(), "CO-7");

        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 8))).Error.Code.Should().Be("Accounting.InventoryRule.RetroactiveOverPosted",
            "el espejo de una anulación de CO-7 necesita la regla vigente el 9 de marzo");
        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 9))).IsSuccess.Should().BeTrue();
        (await e.DesactivarAsync(Guid.NewGuid(), Marzo1)).Error.Code.Should().Be("Accounting.InventoryRule.NotFound");
    }

    // ------------------------------------------------------------------------------------------------------- mapeo --

    [Fact]
    public async Task El_mapeo_solo_admite_tipos_de_modulo_INV_activos_y_reemplaza_en_su_sitio()
    {
        var e = new Escenario();
        var ei = new VoucherType { Code = "EI", Name = "Entrada de inventario", Usage = VoucherUsage.Module, ModuleCode = "INV", CreatedBy = "test" };
        var otro = new VoucherType { Code = "EX", Name = "Otra entrada", Usage = VoucherUsage.Module, ModuleCode = "INV", CreatedBy = "test" };
        var inactivo = new VoucherType { Code = "EV", Name = "Vieja", Usage = VoucherUsage.Module, ModuleCode = "INV", IsActive = false, CreatedBy = "test" };
        e.M.D.Db.VoucherTypes.AddRange(ei, otro, inactivo);
        e.M.D.Db.SaveChanges();
        var semilla = new InventoryVoucherMapping(O.Compra, null, ei.Id) { CreatedBy = TiposDeComprobanteDeInventario.CreadoPorLaSemilla };
        e.M.D.Db.InventoryVoucherMappings.Add(semilla);
        e.M.D.Db.SaveChanges();
        var cg = e.M.D.Db.VoucherTypes.Single(v => v.Code == "CG");
        var nm = e.M.D.Db.VoucherTypes.Single(v => v.Code == "NM");
        var handler = new SetInventoryVoucherMappingCommandHandler(e.M.D.Db, e.M.Dimensiones);

        (await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, null, cg.PublicId, null, "prueba"), default))
            .Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule", "CG es manual");
        (await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, null, nm.PublicId, null, "prueba"), default))
            .Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule", "NM es de Nómina");
        (await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, null, inactivo.PublicId, null, "prueba"), default))
            .Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule", "un tipo inactivo tampoco");
        (await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, "XX", otro.PublicId, null, "prueba"), default))
            .Error.Code.Should().Be("Accounting.InventoryRule.DimensionCodeUnknown", "el tipo de documento lo publica Inventario");

        var reemplazo = await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, null, otro.PublicId, null, "La contadora prefiere EX"), default);
        reemplazo.IsSuccess.Should().BeTrue(reemplazo.Error?.Message);
        reemplazo.Value.Should().Be(semilla.PublicId, "agrega o reemplaza en su sitio por (operación, tipo)");
        var porTipo = await handler.Handle(new SetInventoryVoucherMappingCommand(O.Compra, "co", ei.PublicId, null, "Excepción por tipo"), default);
        porTipo.IsSuccess.Should().BeTrue(porTipo.Error?.Message);

        var lista = (await new ListInventoryVoucherMappingsQueryHandler(e.M.D.Db).Handle(new ListInventoryVoucherMappingsQuery(), default)).Value;
        lista.Should().HaveCount(2);
        lista[0].VoucherType.Code.Should().Be("EX");
        lista[0].IsSeeded.Should().BeTrue();
        lista[1].InventoryDocumentTypeCode.Should().Be("CO");
        lista[1].IsSeeded.Should().BeFalse();

        var mapeos = await new TiposDeComprobanteDeInventario(e.M.D.Db).CargarAsync(default);
        mapeos.Resolver(O.Compra, "CO").Value.VoucherTypeCode.Should().Be("EI", "primero (operación, tipo del documento)");
        mapeos.Resolver(O.Compra, "OTRO").Value.VoucherTypeCode.Should().Be("EX", "después (operación, nulo)");
        mapeos.Resolver(O.Venta, null).Error.Code.Should().Be("Accounting.VoucherType.NotFound");
    }

    [Fact]
    public void El_tipo_de_la_unidad_lo_da_el_mensaje_principal_y_la_anulacion_usa_el_del_original()
    {
        IntegrationEnvelopeV1 Sobre(string tipo, string? tipoDeDocumento = "PV") =>
            new() { Type = tipo, Origin = new MessageOriginV1 { DocumentTypeCode = tipoDeDocumento } };

        TiposDeComprobanteDeInventario.OperacionDe(
        [
            (Sobre(CostoDeVentaReconocidoV1.Type), new CostoDeVentaReconocidoV1()),
            (Sobre(VentaFacturadaV1.Type), new VentaFacturadaV1()),
        ]).Should().Be(new OperacionDeLaUnidad(O.Venta, "PV"), "el comercial antes que el de costo");

        TiposDeComprobanteDeInventario.OperacionDe(
        [
            (Sobre(NotaCreditoEmitidaV1.Type, "NCV"), new NotaCreditoEmitidaV1()),
            (Sobre(DevolucionRegistradaV1.Type, "NCV"), new DevolucionRegistradaV1 { Operation = O.DevolucionDeCliente }),
        ])!.Operation.Should().Be(O.NotaCredito);

        var contenido = System.Text.Json.JsonDocument.Parse("""{"operation":"AjusteNegativo","causeCode":"VENC"}""").RootElement.Clone();
        TiposDeComprobanteDeInventario.OperacionDe(
        [
            (Sobre(DocumentoAnuladoV1.Type, "ANU"), new DocumentoAnuladoV1
            {
                VoidedDocumentTypeCode = "AJ",
                VoidedContents = [new VoidedContentV1 { Type = AjusteInventarioAprobadoV1.Type, Content = contenido }],
            }),
        ]).Should().Be(new OperacionDeLaUnidad(O.AjusteNegativo, "AJ"));
    }
}
