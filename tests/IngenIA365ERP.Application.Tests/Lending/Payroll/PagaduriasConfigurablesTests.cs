using FluentAssertions;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Application.Lending.Payroll.Files;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Payroll;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 014, US3 (T090–T092; api.md §18.3; data-model §1.4.1, §1.4.2; FR-125, E-05): los grupos de archivo y las equivalencias de concepto de un
/// convenio de pagaduría. Fijarlos registra una versión nueva que copia la vigente (la anterior rige hasta la víspera); los formatos son de los
/// ámbitos de libranza; un ítem no va en dos códigos; con grupos, cada código va en uno. Con grupos, generar exige decir cuál (T091, decisión del dueño
/// del 2026-10-18, D36; el archivo por grupo lo prueba <see cref="ArchivoPorGrupoTests"/>). (nuevo)
/// </summary>
public sealed class PagaduriasConfigurablesTests
{
    private static readonly DateOnly Diciembre = new(2026, 12, 1);

    private static SetPayrollAgreementFileGroupsCommandHandler Grupos(LibranzaDePrueba l) => new(l.Db, Copia(l));
    private static SetPayrollAgreementConceptEquivalencesCommandHandler Equivalencias(LibranzaDePrueba l) => new(l.Db, Copia(l));
    private static CopiaDeVersionDeConvenio Copia(LibranzaDePrueba l) => new(l.Db, l.Alta());

    private static SetPayrollAgreementFileGroupsCommand DosGrupos(LibranzaDePrueba l, DateOnly? desde = null) => new(l.Convenio.PublicId, desde ?? Diciembre,
    [
        new PayrollFileGroupInput("CRED", "Créditos", l.Ida.PublicId, l.Vuelta.PublicId),
        new PayrollFileGroupInput("APORTES", "Aportes y ahorros", l.Ida.PublicId, l.Vuelta.PublicId, CutoffDay: 15),
    ], "La pagaduría separa los archivos") { OperationKey = Guid.NewGuid() };

    private static PayrollConceptEquivalenceInput Codigo(string codigo, string? grupo, ConceptAllocationRule regla, params PayrollEquivalenceItemInput[] items) =>
        new(codigo, "Código " + codigo, regla, items, grupo);

    [Fact]
    public async Task Fijar_grupos_registra_una_version_nueva_con_grupos_y_cierra_la_anterior_la_vispera()
    {
        using var l = await LibranzaDePrueba.CrearAsync(vincular: false);

        var r = await Grupos(l).Handle(DosGrupos(l), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Select(g => (g.Code, g.Order, g.Calendar.CutoffDay)).Should().Equal(("CRED", 1, (int?)null), ("APORTES", 2, (int?)15));
        var versiones = await l.Db.PayrollAgreementVersions.AsNoTracking().Where(v => v.PayrollAgreementId == l.Convenio.Id).OrderBy(v => v.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(Diciembre.AddDays(-1));
        versiones[1].UsesFileGroups.Should().BeTrue();
        (versiones[1].OutboundFormatId, versiones[1].ResultFormatId).Should().Be(((int?)null, (int?)null), "con grupos, cada grupo lleva sus formatos");
        (await l.Db.PayrollAgreementResultCodes.CountAsync(c => c.PayrollAgreementVersionId == versiones[1].Id)).Should().Be(2, "copia los códigos de respuesta");

        var leidos = await new ListPayrollAgreementFileGroupsQueryHandler(l.Db, l.G.Reloj).Handle(new ListPayrollAgreementFileGroupsQuery(l.Convenio.PublicId, Diciembre), default);
        leidos.Value.Should().HaveCount(2);
        var antes = await new ListPayrollAgreementFileGroupsQueryHandler(l.Db, l.G.Reloj).Handle(new ListPayrollAgreementFileGroupsQuery(l.Convenio.PublicId, Diciembre.AddDays(-1)), default);
        antes.Value.Should().BeEmpty("la versión anterior no tiene grupos");
    }

    [Fact]
    public async Task Un_formato_de_otro_ambito_o_un_grupo_repetido_o_una_version_cruzada_se_rechazan()
    {
        using var l = await LibranzaDePrueba.CrearAsync(vincular: false);
        var pago = l.Formato("PAGO1", BankFileScope.PayrollDisbursement, ("Doc", nameof(BankFieldSource.PayeeDocument)));

        var otroAmbito = await Grupos(l).Handle(DosGrupos(l) with
        {
            Groups = [new PayrollFileGroupInput("CRED", "Créditos", pago.PublicId, l.Vuelta.PublicId)],
        }, default);
        otroAmbito.Error.Code.Should().Be("Core.BankFileFormat.ScopeMismatch");

        var repetido = await Grupos(l).Handle(DosGrupos(l) with
        {
            Groups = [new("CRED", "A", l.Ida.PublicId, l.Vuelta.PublicId), new("cred", "B", l.Ida.PublicId, l.Vuelta.PublicId)],
        }, default);
        repetido.Error.Code.Should().Be(ErroresDeLibranza.CodigoGrupoRepetido);

        var cruzada = await Grupos(l).Handle(DosGrupos(l, new DateOnly(2026, 10, 1)), default);
        cruzada.Error.Code.Should().Be(ErroresDeLibranza.CodigoVersionCruzada);
    }

    [Fact]
    public async Task Las_equivalencias_con_grupos_van_cada_una_en_un_grupo_y_un_item_no_va_en_dos_codigos()
    {
        using var l = await LibranzaDePrueba.CrearAsync(vincular: false);
        (await Grupos(l).Handle(DosGrupos(l), default)).IsSuccess.Should().BeTrue();
        var enero = new DateOnly(2027, 1, 1);

        var sinGrupo = await Equivalencias(l).Handle(new SetPayrollAgreementConceptEquivalencesCommand(l.Convenio.PublicId, enero,
            [Codigo("101", null, ConceptAllocationRule.Priority, new PayrollEquivalenceItemInput(LineNature.Placement, "*", "Installment"))], "Cambio") { OperationKey = Guid.NewGuid() }, default);
        sinGrupo.Error.Code.Should().Be(ErroresDeLibranza.CodigoLineaSinGrupo);

        var dosVeces = await Equivalencias(l).Handle(new SetPayrollAgreementConceptEquivalencesCommand(l.Convenio.PublicId, enero,
        [
            Codigo("101", "CRED", ConceptAllocationRule.Priority, new PayrollEquivalenceItemInput(LineNature.Placement, "*", "Installment")),
            Codigo("102", "CRED", ConceptAllocationRule.Priority, new PayrollEquivalenceItemInput(LineNature.Placement, "CONSUMO", "Principal")),
        ], "Cambio") { OperationKey = Guid.NewGuid() }, default);
        dosVeces.Error.Code.Should().Be(EquivalenciasDePagaduria.CodigoMapeadoDosVeces);

        var bien = await Equivalencias(l).Handle(new SetPayrollAgreementConceptEquivalencesCommand(l.Convenio.PublicId, enero,
        [
            Codigo("101", "CRED", ConceptAllocationRule.Priority, new PayrollEquivalenceItemInput(LineNature.Placement, "*", "Installment")),
            Codigo("201", "APORTES", ConceptAllocationRule.Proportional,
                new PayrollEquivalenceItemInput(LineNature.Contribution, "*", "Installment"), new PayrollEquivalenceItemInput(LineNature.Deposit, "AHPERM", "Installment")),
        ], "Equivalencias de la pagaduría") { OperationKey = Guid.NewGuid() }, default);
        bien.IsSuccess.Should().BeTrue(bien.IsFailure ? $"{bien.Error.Code}: {bien.Error.Message}" : string.Empty);
        bien.Value.Select(e => (e.PaymasterConceptCode, e.FileGroupCode, e.Split, e.Members.Count)).Should()
            .Equal(("101", "CRED", "Priority", 1), ("201", "APORTES", "Proportional", 2));
        (await new ListPayrollAgreementFileGroupsQueryHandler(l.Db, l.G.Reloj).Handle(new ListPayrollAgreementFileGroupsQuery(l.Convenio.PublicId, enero), default))
            .Value.Should().HaveCount(2, "la versión de equivalencias copia los grupos");

        // Volver a fijar los grupos copia las equivalencias con su grupo por código; si un grupo desaparece, la equivalencia se queda sin el suyo.
        var sinAportes = await Grupos(l).Handle(DosGrupos(l, new DateOnly(2027, 2, 1)) with
        {
            Groups = [new PayrollFileGroupInput("CRED", "Créditos", l.Ida.PublicId, l.Vuelta.PublicId)],
        }, default);
        sinAportes.Error.Code.Should().Be(ErroresDeLibranza.CodigoLineaSinGrupo);
    }

    [Fact]
    public async Task Un_convenio_con_grupos_genera_por_grupo_y_hay_que_decir_cual()
    {
        using var l = await LibranzaDePrueba.CrearAsync();
        (await Grupos(l).Handle(DosGrupos(l), default)).IsSuccess.Should().BeTrue();
        l.Db.ChangeTracker.Clear();

        var r = await l.Preparacion().PrepararAsync(l.Convenio.PublicId, 2026, 12, null, PayrollDeductionLines.LoteDeVistaPrevia, default);

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoGrupoRequerido);
    }
}
