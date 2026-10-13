using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Lending;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 013, T0614 (T52, T54; contracts/contabilidad.md §3.1, §3.5, §3.6, §4.2; api.md §23.1–§23.2): crear, versión, desactivar y mapeo de
/// comprobantes. Dimensiones (<c>DimensionRequired</c>, <c>DimensionNotAllowed</c>, <c>DimensionCodeUnknown</c> por
/// <c>IDimensionesDeCartera</c>, también el sub-segmento que no es de la modalidad); cuenta (<c>AccountEligibility</c> para CAR, naturaleza
/// distinta con aviso); vigencia (la versión cierra la anterior la víspera, <c>Overlaps</c>, <c>RetroactiveOverPosted</c> con
/// <c>{ lastPostedDate, document }</c> y la excepción de la clave nueva); <c>DimensionKey</c> de §3.1 y peso al guardar; desactivar con motivo y
/// sin borrar; el mapeo sólo a tipos <c>Module</c>/<c>CAR</c> activos y la resolución <c>(hecho, clase)</c> antes que <c>(hecho, *)</c>.
/// </summary>
public class LendingPostingRuleCommandsTests
{
    private static readonly DateOnly Marzo1 = new(2026, 3, 1);

    private sealed class Escenario
    {
        public MatrizDePruebaDeCartera M { get; } = new();
        public ReglasDeLaMatrizDeCartera Reglas { get; }

        public Escenario() => Reglas = M.Reglas();

        public Task<Result<ReglaDeCarteraGuardadaDto>> CrearAsync(string rol, DimensionesDeReglaDeCarteraDto dimensiones, ChartOfAccount cuenta,
            DateOnly? desde = null, string? operacion = null) =>
            new CreateLendingPostingRuleCommandHandler(M.D.Db, Reglas)
                .Handle(new CreateLendingPostingRuleCommand(operacion, rol, dimensiones, cuenta.PublicId, desde ?? MatrizDePruebaDeCartera.Enero1, null,
                    "Matriz aprobada por la contadora"), default);

        public Task<Result<ReglaDeCarteraGuardadaDto>> VersionAsync(Guid regla, ChartOfAccount cuenta, DateOnly desde) =>
            new AddLendingPostingRuleVersionCommandHandler(M.D.Db, Reglas)
                .Handle(new AddLendingPostingRuleVersionCommand(regla, cuenta.PublicId, desde, "Cambio de cuenta", "La contadora lo pidió"), default);

        public Task<Result> DesactivarAsync(Guid regla, DateOnly hasta) =>
            new DeactivateLendingPostingRuleCommandHandler(M.D.Db, Reglas)
                .Handle(new DeactivateLendingPostingRuleCommand(regla, hasta, "Ya no se usa"), default);
    }

    private static readonly DimensionesDeReglaDeCarteraDto ConsumoA = new(ModalityCode: "CONSUMO", RiskCategory: "A");

    // ------------------------------------------------------------------------------------------------ dimensiones --

    [Fact]
    public async Task Crea_la_regla_con_su_clave_de_3_1_y_su_peso()
    {
        var e = new Escenario();

        var r = await e.CrearAsync(R.Capital, new DimensionesDeReglaDeCarteraDto(ModalityCode: "consumo", SubsegmentCode: "ConLibranza", RiskCategory: "a",
            IsEmployee: false, BranchPublicId: e.M.D.Principal.PublicId), e.M.CapitalA);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var regla = await e.M.D.Db.LendingPostingRules.SingleAsync(x => x.PublicId == r.Value.RulePublicId);
        regla.Operation.Should().Be("*", "sin hecho, la regla vale para todos");
        regla.ModalityCode.Should().Be("CONSUMO");
        regla.SubsegmentCode.Should().Be("ConLibranza", "el sub-segmento conserva su forma");
        regla.RiskCategory.Should().Be("A");
        regla.BranchId.Should().Be(e.M.D.Principal.Id);
        regla.DimensionKey.Should().Be($"*|Capital|*|CONSUMO|ConLibranza|A|*|0|*|*|*|*|*|{e.M.D.Principal.Id}");
        regla.SpecificityWeight.Should().Be(256 + 64 + 4, "empleado + sub-segmento + sucursal");
        regla.Notes.Should().Be("Matriz aprobada por la contadora", "sin notas, queda el motivo: quién decidió y por qué");
    }

    [Fact]
    public void La_clave_de_los_ejemplos_de_3_1()
    {
        LendingPostingRule.ClaveDe("*", "Capital", null, "CONSUMO", "ConLibranza", "A", null, false, null, null, null, null, null, null)
            .Should().Be("*|Capital|*|CONSUMO|ConLibranza|A|*|0|*|*|*|*|*|*");
        LendingPostingRule.ClaveDe(null, "Capital", null, "CONSUMO", null, "A", null, true, null, null, null, null, null, null)
            .Should().Be("*|Capital|*|CONSUMO|*|A|*|1|*|*|*|*|*|*");
        LendingPostingRule.ClaveDe("*", "RecaudoPorAplicar", null, null, null, null, null, null, null, null, null, "Payroll", null, null)
            .Should().Be("*|RecaudoPorAplicar|*|*|*|*|*|*|*|*|*|Payroll|*|*");
    }

    [Fact]
    public async Task Una_dimension_exigida_ausente_o_una_no_admitida_se_rechazan()
    {
        var e = new Escenario();

        (await e.CrearAsync(R.Capital, new DimensionesDeReglaDeCarteraDto(ModalityCode: "CONSUMO"), e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionRequired", "el capital exige la categoría");
        (await e.CrearAsync(R.Capital, ConsumoA with { CollectorClass = "Receipts" }, e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionNotAllowed", "el capital no admite la clase de recaudador");
        (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA, operacion: PagoAplicadoV1.Type))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionNotAllowed", "en un rol de saldo el hecho va en * (C77)");
        (await e.CrearAsync(R.Ingreso, new DimensionesDeReglaDeCarteraDto(ModalityCode: "CONSUMO", ConceptCode: "CurrentInterest"), e.M.Ingreso,
                operacion: SeguroCausadoV1.Type))
            .Error.Code.Should().Be("Accounting.LendingRule.RoleNotInOperation", "el seguro causado no mueve el ingreso");
        (await e.CrearAsync(R.GastoDeterioro, new DimensionesDeReglaDeCarteraDto(ReasonCode: "Individual"), e.M.Ingreso))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionRequired", "con Individual el gasto exige modalidad y concepto");
        (await e.CrearAsync("Cartera", ConsumoA, e.M.CapitalA)).Error.Code.Should().Be("Accounting.LendingRule.RoleNotInOperation");

        var validador = new CreateLendingPostingRuleCommandValidator();
        var forma = await validador.ValidateAsync(new CreateLendingPostingRuleCommand(null, R.InteresPorCobrar,
            new DimensionesDeReglaDeCarteraDto(ModalityCode: "CONSUMO", CollectorClass: "Payroll"), Guid.NewGuid(), Marzo1, null, "motivo"));
        forma.IsValid.Should().BeFalse("el validador responde 400 por la forma");
        forma.Errors.Select(x => x.ErrorCode).Should().Contain(["Accounting.LendingRule.DimensionRequired", "Accounting.LendingRule.DimensionNotAllowed"]);
    }

    [Fact]
    public async Task Un_codigo_que_Cartera_no_conoce_se_rechaza()
    {
        var e = new Escenario();

        (await e.CrearAsync(R.Capital, ConsumoA with { CreditLineCode = "LIB99" }, e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown", "la línea la publica Cartera");
        (await e.CrearAsync(R.Capital, new DimensionesDeReglaDeCarteraDto(ModalityCode: "VIVIENDA", RiskCategory: "A"), e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown", "la modalidad es del catálogo de modalidades vigente");
        (await e.CrearAsync(R.Capital, ConsumoA with { SubsegmentCode = "PersonaNatural" }, e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown", "PersonaNatural es de la modalidad comercial, no de consumo");
        (await e.CrearAsync(R.Capital, new DimensionesDeReglaDeCarteraDto(ModalityCode: "CONSUMO", RiskCategory: "F"), e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown");
        (await e.CrearAsync(R.InteresPorCobrar, ConsumoA with { ConceptCode = "Principal" }, e.M.CapitalA))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown", "el interés por cobrar sólo admite los conceptos de interés");
        (await e.CrearAsync(R.RecaudoPorAplicar, new DimensionesDeReglaDeCarteraDto(CollectorClass: "Banco"), e.M.Puente))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown");
        (await e.CrearAsync(R.DesembolsoPorGirar, new DimensionesDeReglaDeCarteraDto(ReasonCode: "InKind"), e.M.Puente))
            .Error.Code.Should().Be("Accounting.LendingRule.DimensionCodeUnknown", "el motivo es del rol");

        (await e.CrearAsync(R.Capital, ConsumoA with { CreditLineCode = "VIE01" }, e.M.CapitalA))
            .IsSuccess.Should().BeTrue("una línea cerrada sigue siendo un código válido (§3.2)");
        await e.M.Dimensiones.Received().CatalogoAsync(Arg.Any<CancellationToken>());
    }

    // ----------------------------------------------------------------------------------------------------- cuenta --

    [Fact]
    public async Task La_cuenta_tiene_que_ser_elegible_para_Cartera()
    {
        var e = new Escenario();

        var r = await e.CrearAsync(R.Capital, ConsumoA, e.M.SinCartera);
        r.Error.Code.Should().Be("Accounting.Account.NotEligible");
        r.Error.Message.Should().Contain("no está habilitada para Cartera");

        var agrupadora = e.M.D.Cuenta("1441", movimiento: false, modulos: AccountingModules.Lending);
        (await e.CrearAsync(R.Capital, ConsumoA, agrupadora)).Error.Code.Should().Be("Accounting.Account.NotEligible");
    }

    [Fact]
    public async Task La_naturaleza_distinta_de_la_del_rol_se_guarda_con_aviso()
    {
        var e = new Escenario();

        var r = await e.CrearAsync(R.DeterioroIndividual, ConsumoA with { ConceptCode = "Principal" }, e.M.CapitalA);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Warnings.Should().ContainSingle(a => a.Code == "Accounting.LendingRule.NatureMismatch");
        (await e.CrearAsync(R.DeterioroIndividual, ConsumoA with { ConceptCode = "CurrentInterest" }, e.M.Deterioro)).Value.Warnings.Should().BeEmpty();
    }

    // --------------------------------------------------------------------------------------------------- vigencia --

    [Fact]
    public async Task Una_version_nueva_cierra_la_vigente_la_vispera()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;

        var r = await e.VersionAsync(original, e.M.CapitalConsumoA, Marzo1);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        var reglas = await e.M.D.Db.LendingPostingRules.OrderBy(x => x.ValidFrom).ToListAsync();
        reglas.Should().HaveCount(2);
        reglas[0].ValidTo.Should().Be(new DateOnly(2026, 2, 28));
        reglas[1].ValidFrom.Should().Be(Marzo1);
        reglas[1].AccountId.Should().Be(e.M.CapitalConsumoA.Id);
        reglas[1].DimensionKey.Should().Be(reglas[0].DimensionKey);
        reglas[1].Notes.Should().Be("Cambio de cuenta");
    }

    [Fact]
    public async Task Dos_vigencias_de_la_misma_clave_no_se_cruzan()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;

        (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalConsumoA, Marzo1))
            .Error.Code.Should().Be("Accounting.LendingRule.Overlaps", "crear no reemplaza: para eso está la versión");
        (await e.VersionAsync(original, e.M.CapitalConsumoA, MatrizDePruebaDeCartera.Enero1))
            .Error.Code.Should().Be("Accounting.LendingRule.Overlaps", "una versión en la misma fecha pisa a la vigente");
    }

    [Fact]
    public async Task Una_version_no_empieza_en_ni_antes_de_lo_ya_contabilizado()
    {
        var e = new Escenario();
        var original = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;
        e.M.Contabilizado(PagoAplicadoV1.Type, new DateOnly(2026, 3, 9), MatrizDePruebaDeCartera.Pago(), "000123");

        var r = await e.VersionAsync(original, e.M.CapitalConsumoA, new DateOnly(2026, 3, 9));

        r.Error.Code.Should().Be("Accounting.LendingRule.RetroactiveOverPosted");
        var datos = ((ErrorConDatos)r.Error).Data;
        datos.GetType().GetProperty("lastPostedDate")!.GetValue(datos).Should().Be(new DateOnly(2026, 3, 9));
        datos.GetType().GetProperty("document")!.GetValue(datos).Should().Be("PaymentApplication OP-000123 · crédito 000123");
        (await e.VersionAsync(original, e.M.CapitalConsumoA, new DateOnly(2026, 3, 10))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_clave_nueva_puede_empezar_antes_si_nada_contabilizado_casa_con_ella()
    {
        var e = new Escenario();
        e.M.Contabilizado(PagoAplicadoV1.Type, new DateOnly(2026, 3, 9), MatrizDePruebaDeCartera.Pago(categoria: "A"), "000123");

        (await e.CrearAsync(R.Capital, new DimensionesDeReglaDeCarteraDto(ModalityCode: "CONSUMO", RiskCategory: "C"), e.M.CapitalA, Marzo1))
            .IsSuccess.Should().BeTrue("ningún pago contabilizado movió capital en C: rescata hechos rechazados sin cambiar nada (§3.5)");
        (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA, Marzo1))
            .Error.Code.Should().Be("Accounting.LendingRule.RetroactiveOverPosted", "el pago del 9 de marzo movió capital de consumo en A");
        (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA, new DateOnly(2026, 3, 10)))
            .IsSuccess.Should().BeTrue("después de lo contabilizado no hay retroactividad");
    }

    // ------------------------------------------------------------------------------------------------- desactivar --

    [Fact]
    public async Task Desactivar_exige_motivo_fija_ValidTo_y_nunca_borra()
    {
        var e = new Escenario();
        var id = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;

        var sinMotivo = await new DeactivateLendingPostingRuleCommandValidator().ValidateAsync(new DeactivateLendingPostingRuleCommand(id, Marzo1, " "));
        sinMotivo.IsValid.Should().BeFalse();

        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 31))).IsSuccess.Should().BeTrue();
        var regla = await e.M.D.Db.LendingPostingRules.SingleAsync();
        regla.ValidTo.Should().Be(new DateOnly(2026, 3, 31));
        regla.IsDeleted.Should().BeFalse();
        (await e.DesactivarAsync(Guid.NewGuid(), Marzo1)).Error.Code.Should().Be("Accounting.LendingRule.NotFound");
    }

    [Fact]
    public async Task Desactivar_antes_de_lo_contabilizado_que_la_uso_se_rechaza()
    {
        var e = new Escenario();
        var id = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;
        e.M.Contabilizado(PagoAplicadoV1.Type, new DateOnly(2026, 3, 9), MatrizDePruebaDeCartera.Pago(), "000123");

        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 8))).Error.Code.Should().Be("Accounting.LendingRule.RetroactiveOverPosted");
        (await e.DesactivarAsync(id, new DateOnly(2026, 3, 9))).IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------------------------- mapeo --

    [Fact]
    public async Task El_mapeo_solo_admite_tipos_de_modulo_CAR_activos_y_se_resuelve_por_clase_antes_que_por_hecho()
    {
        var e = new Escenario();
        var rc = new VoucherType { Code = "RC", Name = "Recaudo de cartera", Usage = VoucherUsage.Module, ModuleCode = "CAR", CreatedBy = "test" };
        var dn = new VoucherType { Code = "DN", Name = "Descuento de nómina de cartera", Usage = VoucherUsage.Module, ModuleCode = "CAR", CreatedBy = "test" };
        var inactivo = new VoucherType { Code = "RV", Name = "Viejo", Usage = VoucherUsage.Module, ModuleCode = "CAR", IsActive = false, CreatedBy = "test" };
        var deInventario = new VoucherType { Code = "FV", Name = "Factura", Usage = VoucherUsage.Module, ModuleCode = "INV", CreatedBy = "test" };
        e.M.D.Db.VoucherTypes.AddRange(rc, dn, inactivo, deInventario);
        e.M.D.Db.SaveChanges();
        var semilla = new LendingVoucherMapping(PagoAplicadoV1.Type, null, rc.Id, isSeeded: true) { CreatedBy = "system:seed" };
        e.M.D.Db.LendingVoucherMappings.Add(semilla);
        e.M.D.Db.SaveChanges();
        var cg = e.M.D.Db.VoucherTypes.Single(v => v.Code == "CG");
        var usuario = Substitute.For<ICurrentUserService>();
        usuario.UserName.Returns("contadora@demo");
        var handler = new SetLendingVoucherMappingCommandHandler(e.M.D.Db, usuario, e.M.D.Clock);

        foreach (var (tipo, porque) in new[] { (cg, "CG es manual"), (deInventario, "FV es de Inventario"), (inactivo, "un tipo inactivo tampoco") })
            (await handler.Handle(new SetLendingVoucherMappingCommand(PagoAplicadoV1.Type, null, tipo.PublicId, null, "prueba"), default))
                .Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule", porque);
        new SetLendingVoucherMappingCommandValidator().Validate(new SetLendingVoucherMappingCommand(CarteraActivadaV1.Type, null, rc.PublicId, null, "prueba"))
            .IsValid.Should().BeFalse("un informativo no tiene comprobante");

        var porClase = await handler.Handle(new SetLendingVoucherMappingCommand(PagoAplicadoV1.Type, "Payroll", dn.PublicId, null, "Lo de Nómina va en DN"), default);
        porClase.IsSuccess.Should().BeTrue(porClase.Error?.Message);
        var reemplazo = await handler.Handle(new SetLendingVoucherMappingCommand(PagoAplicadoV1.Type, "*", rc.PublicId, null, "Confirmado"), default);
        reemplazo.Value.Should().Be(semilla.PublicId, "agrega o reemplaza en su sitio por (hecho, clase)");

        var lista = (await new ListLendingVoucherMappingsQueryHandler(e.M.D.Db).Handle(new ListLendingVoucherMappingsQuery(), default)).Value;
        lista.Should().HaveCount(2);
        lista[0].CollectorClass.Should().Be("*");
        lista[0].IsSeeded.Should().BeTrue();
        lista[0].ReviewPending.Should().BeFalse("guardarlo es la conformidad de la contadora");
        lista[1].CollectorClass.Should().Be("Payroll");

        var mapeos = await new TiposDeComprobanteDeCartera(e.M.D.Db).CargarAsync(default);
        mapeos.Resolver(PagoAplicadoV1.Type, "Payroll").Value.VoucherTypeCode.Should().Be("DN", "primero (hecho, clase)");
        mapeos.Resolver(PagoAplicadoV1.Type, "Receipts").Value.VoucherTypeCode.Should().Be("RC", "después (hecho, *)");
        mapeos.Resolver(PagoAplicadoV1.Type, null).Value.VoucherTypeCode.Should().Be("RC");
        mapeos.Resolver(InteresCausadoV1.Type, null).Error.Code.Should().Be("Accounting.VoucherType.NotFound");

        rc.IsActive = false;
        e.M.D.Db.SaveChanges();
        (await new TiposDeComprobanteDeCartera(e.M.D.Db).CargarAsync(default)).Resolver(PagoAplicadoV1.Type, null)
            .Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule", "un tipo que se inactivó después ya no sirve");
    }

    [Fact]
    public async Task Las_consultas_de_la_matriz_dicen_la_cuenta_el_peso_y_las_versiones()
    {
        var e = new Escenario();
        var id = (await e.CrearAsync(R.Capital, ConsumoA, e.M.CapitalA)).Value.RulePublicId;
        await e.VersionAsync(id, e.M.CapitalConsumoA, Marzo1);
        e.M.D.Clock.HoyLocal.Returns(ContabilidadTestData.Hoy);

        var versiones = (await new ListLendingPostingRuleVersionsQueryHandler(e.M.D.Db, e.M.D.Clock)
            .Handle(new ListLendingPostingRuleVersionsQuery(id), default)).Value;
        versiones.Should().HaveCount(2);
        versiones[0].Account.Code.Should().Be("14420501");
        versiones[0].IsCurrent.Should().BeTrue();
        versiones[0].Account.IsEligible.Should().BeTrue();

        var resuelta = (await new ResolveLendingRuleQueryHandler(e.M.D.Db, new ResolutorDeReglasDeCartera(e.M.D.Db), e.M.D.Clock)
            .Handle(new ResolveLendingRuleQuery(PagoAplicadoV1.Type, R.Capital, ConsumoA with { SubsegmentCode = "ConLibranza" }, new DateOnly(2026, 2, 10)), default)).Value;
        resuelta.Account!.Code.Should().Be("14410501", "el 10 de febrero rige la primera versión");
        resuelta.Candidates.Should().ContainSingle(c => c.Matched);

        var sinRegla = (await new ResolveLendingRuleQueryHandler(e.M.D.Db, new ResolutorDeReglasDeCartera(e.M.D.Db), e.M.D.Clock)
            .Handle(new ResolveLendingRuleQuery(PagoAplicadoV1.Type, R.Capital, ConsumoA with { RiskCategory = "B" }, Marzo1), default)).Value;
        sinRegla.Rule.Should().BeNull();
        sinRegla.MissingCode.Should().Be("Accounting.LendingRule.Missing");

        var catalogo = (await new GetLendingRulesCatalogQueryHandler(e.M.Dimensiones).Handle(new GetLendingRulesCatalogQuery(), default)).Value;
        catalogo.Operations.Should().HaveCount(35);
        catalogo.Roles.Should().HaveCount(26);
        catalogo.Dimensions.CreditLines.Select(l => l.Code).Should().Contain("VIE01");
    }
}
