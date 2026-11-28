using System.Text.Json.Nodes;
using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Lending.Consultas;
using IngenIA365ERP.Application.Accounting.Lending.Contabilizacion;
using IngenIA365ERP.Application.Accounting.Lending.Lotes;
using IngenIA365ERP.Application.Accounting.Lending.Reglas;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using R = IngenIA365ERP.Application.Accounting.Lending.Reglas.RolesDeCuentaDeCartera;

namespace IngenIA365ERP.Application.Tests.Accounting.Lending;

/// <summary>
/// Feature 028 (T046–T048; FR-027, FR-027b, D11, H5): el desembolso con recogidas sobre el caso dorado 68 —10.000.000 por giro a un tercero que recoge
/// 000077 (sucursal principal) y 000078 (sucursal norte), con el puente de la recogida propia en una cuenta que exige tercero y documento cruce— y los
/// recibos de sus dos recogidas (<c>recibos</c> del caso).
/// <list type="bullet">
/// <item>el puente queda en cero por cuenta, tercero, cruce y sucursal entre el DS y los RC, con el parámetro o sin él;</item>
/// <item>con <see cref="DesembolsoConSusRecogidas.Clave"/> apagado (de fábrica), cada unidad va sola; encendido, el desembolso y los recibos de sus
/// recogidas salen en <b>un</b> comprobante DS (<c>esperadoEnUnComprobante</c>), en línea y sin lote;</item>
/// <item>el planeador sólo junta los recibos que el desembolso nombra, en su misma fecha contable; un desembolso sin recogidas va como siempre;</item>
/// <item>la completitud no avisa por la naturaleza ni por el cruce del puente de la recogida propia (lo saldan dos hechos de Cartera).</item>
/// </list>
/// (nuevo)
/// </summary>
public class DesembolsoConSusRecogidasTests
{
    private const string Caso = "68-desembolso-con-dos-recogidas-giro-a-tercero";

    private readonly EscenarioContableDeCartera E = new();
    private readonly JsonObject _caso = EscenarioContableDeCartera.Caso(Caso);

    public DesembolsoConSusRecogidasTests() => E.AgregarReglas(_caso["matrizExtra"]!.AsArray());

    private IReadOnlyList<MensajeEntrante> Desembolso() => EscenarioContableDeCartera.Unidad(_caso);

    private IReadOnlyList<IReadOnlyList<MensajeEntrante>> Recibos() =>
        _caso["recibos"]!.AsArray().Select(r => EscenarioContableDeCartera.Unidad(r!.AsObject())).ToList();

    private static UnidadDeConsumo Unidad(IReadOnlyList<MensajeEntrante> mensajes) =>
        new(IntegrationDestinations.Accounting, mensajes[0].Envelope.Origin.PublicId, mensajes[0].Envelope.OriginEventKey,
            mensajes.Select(m => m.MessageId).ToList(), null, 0) { OriginModule = ModulosDeOrigen.Cartera };

    private void Encender(string clave)
    {
        E.D.Db.SystemSettings.Add(new SystemSetting { SettingKey = clave, SettingValue = "true", ValueType = "Bool", ModulePrefix = "ACC", CreatedBy = "test" });
        E.D.Db.SaveChanges();
    }

    private static string Clave(PostingLine l) =>
        string.Join('|', l.AccountCode, l.Debit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            l.Credit.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), l.PersonId, l.BranchId, l.CostCenterId, l.CrossDocumentType,
            l.CrossDocumentNumber);

    /// <summary>El puente (la cuenta de la regla de la recogida propia) por tercero, cruce y sucursal: débito − crédito.</summary>
    private static IEnumerable<decimal> SaldosDelPuente(IEnumerable<PostingLine> lineas) =>
        lineas.Where(l => l.AccountCode == "24959503")
            .GroupBy(l => (l.PersonId, l.CrossDocumentType, l.CrossDocumentNumber, l.BranchId))
            .Select(g => g.Sum(l => l.Debit - l.Credit));

    [Fact]
    public void Los_recibos_del_caso_son_de_su_record_y_son_las_recogidas_que_nombra_el_desembolso()
    {
        foreach (var m in _caso["recibos"]!.AsArray().SelectMany(r => r!["mensajes"]!.AsArray()))
            EscenarioContableDeCartera.PropiedadesDesconocidas(m!["payload"], EscenarioContableDeCartera.TipoDe((string)m["type"]!).Record)
                .Should().BeEmpty();
        var desembolso = (CreditoDesembolsadoV1)Desembolso()[0].Payload!;
        Recibos().Select(r => r[0].Envelope.Origin.PublicId).Should().Equal(desembolso.PayoffDeductions.Select(x => x.OperationPublicId));
        Recibos().Select(r => ((PagoAplicadoV1)r[0].Payload!).Collector.Receipt.Number).Should().OnlyContain(n => n == Desembolso()[0].Envelope.Origin.Number,
            "el recibo de cada recogida es el número del desembolso");
    }

    [Fact]
    public async Task Sin_el_parametro_el_puente_igual_queda_en_cero_entre_el_DS_y_los_RC()
    {
        var unidades = new[] { Desembolso() }.Concat(Recibos()).ToList();
        var catalogos = await E.CatalogosAsync([.. unidades]);
        var construidas = unidades.Select(u => ConstructorDeLineasDeCartera.Construir(u.Select(HechoDeCartera.De).ToList(), catalogos)).ToList();

        construidas.SelectMany(c => c.Fallos).Should().BeEmpty();
        construidas.Select(c => c.Tipo!.VoucherTypeCode).Should().Equal("DS", "RC", "RC");
        SaldosDelPuente(construidas.SelectMany(c => c.Request!.Lines)).Should().HaveCount(2).And.OnlyContain(s => s == 0m,
            "el desembolso acredita y cada recibo debita el mismo puente con la deudora, el crédito recogido y su sucursal");
        construidas[0].Request!.Lines.Where(l => l.AccountCode == "24959503").Should().OnlyContain(l => l.CrossDocumentType == "PG",
            "el puente del desembolso cruza como lo debita el recibo de la recogida");
    }

    [Fact]
    public async Task El_puente_del_desembolso_cruza_como_el_recibo_aunque_el_desembolso_tenga_otro_cruce()
    {
        var otro = await E.D.Db.CrossDocumentTypes.FirstAsync(c => c.Code != "PG");
        var mapeo = await E.D.Db.LendingVoucherMappings.SingleAsync(m => m.Operation == CreditoDesembolsadoV1.Type && m.CollectorClass == null);
        mapeo.CrossDocumentTypeId = otro.Id;
        await E.D.Db.SaveChangesAsync();

        var desembolso = Desembolso();
        var c = ConstructorDeLineasDeCartera.Construir(desembolso.Select(HechoDeCartera.De).ToList(), await E.CatalogosAsync(desembolso));

        c.Fallos.Should().BeEmpty();
        c.Request!.Lines.Single(l => l.AccountCode == "14410501").CrossDocumentType.Should().Be(otro.Code, "el capital cruza con el mapeo del desembolso");
        c.Request.Lines.Where(l => l.AccountCode == "24959503").Should().OnlyContain(l => l.CrossDocumentType == "PG");
    }

    [Fact]
    public void El_planeador_junta_el_desembolso_con_los_recibos_que_nombra_y_nada_mas()
    {
        var desembolso = Desembolso();
        var recibos = Recibos();
        var ajeno = EscenarioContableDeCartera.UnidadDe("02-recogida-de-otro-credito-por-el-contrato");
        var otraFecha = EscenarioContableDeCartera.Copia(recibos[1], origen: recibos[1][0].Envelope.Origin.PublicId, fecha: new DateOnly(2026, 11, 6));
        var sinRecogidas = EscenarioContableDeCartera.UnidadDe("40-desembolso-a-un-tercero");
        var unidades = new[] { recibos[0], ajeno, desembolso, otraFecha, sinRecogidas }.Select(u => (Unidad(u), u)).ToList();

        var trabajos = DesembolsoConSusRecogidas.Planear(unidades);

        var t = trabajos.Should().ContainSingle().Subject;
        t.ClaveDeGrupo.Should().Be($"{DesembolsoConSusRecogidas.PrefijoDeGrupo}{desembolso[0].Envelope.Origin.PublicId:N}");
        ComprobantePorRecibo.EsClaveDeRecibo(t.ClaveDeGrupo).Should().BeTrue("va por el handler del recibo, en línea y sin lote");
        t.Unidades.Should().Equal(unidades[0].Item1, unidades[2].Item1);
        DesembolsoConSusRecogidas.Desembolso(sinRecogidas).Should().BeNull("un desembolso sin recogidas va como siempre");
    }

    [Fact]
    public async Task Apagado_el_planeador_deja_cada_unidad_sola_y_encendido_junta_el_desembolso_con_sus_recibos()
    {
        var unidades = Recibos().Append(Desembolso()).ToList();
        foreach (var u in unidades) E.Emitir(u);
        var planeador = new PlaneadorEnLineaDeCartera(E.D.Db, new MensajesEntrantes(E.D.Db));

        (await planeador.PlanearAsync(unidades.Select(Unidad).ToList(), IntegrationDestinations.Accounting, default))
            .Should().HaveCount(3).And.OnlyContain(t => t.ClaveDeGrupo == null, "de fábrica nada cambia");

        Encender(DesembolsoConSusRecogidas.Clave);
        var juntos = await planeador.PlanearAsync(unidades.Select(Unidad).ToList(), IntegrationDestinations.Accounting, default);
        juntos.Should().ContainSingle().Which.Unidades.Should().HaveCount(3);
    }

    [Fact]
    public async Task Con_el_recibo_por_recibo_encendido_y_este_apagado_las_recogidas_se_juntan_entre_ellas_como_antes()
    {
        var unidades = Recibos().Append(Desembolso()).ToList();
        foreach (var u in unidades) E.Emitir(u);
        Encender(ComprobantePorRecibo.Clave);

        var trabajos = await new PlaneadorEnLineaDeCartera(E.D.Db, new MensajesEntrantes(E.D.Db))
            .PlanearAsync(unidades.Select(Unidad).ToList(), IntegrationDestinations.Accounting, default);

        trabajos.Should().HaveCount(2);
        trabajos.Single(t => t.ClaveDeGrupo is not null).ClaveDeGrupo.Should().NotStartWith(DesembolsoConSusRecogidas.PrefijoDeGrupo);
    }

    [Fact]
    public async Task Encendido_el_desembolso_y_sus_recogidas_salen_en_un_DS_con_el_puente_en_cero()
    {
        var desembolso = Desembolso();
        var recibos = Recibos();
        var unidades = recibos.Append(desembolso).ToList();
        foreach (var u in unidades) E.Emitir(u);
        Encender(DesembolsoConSusRecogidas.Clave);
        var trabajo = (await new PlaneadorEnLineaDeCartera(E.D.Db, new MensajesEntrantes(E.D.Db))
            .PlanearAsync(unidades.Select(Unidad).ToList(), IntegrationDestinations.Accounting, default)).Single();

        var r = await E.Resumido().Handle(new PostLendingSummaryGroupCommand(Guid.Empty, trabajo.ClaveDeGrupo!,
            trabajo.Unidades.SelectMany(u => u.MessagePublicIds).ToList()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Should().HaveCount(3).And.OnlyContain(x => x.Resultado is ResultadoDeConsumo.Processed);
        E.D.Db.ChangeTracker.Clear();
        var documento = await E.D.Db.AccountingDocuments.Include(d => d.Lines).Include(d => d.VoucherType).SingleAsync();
        var esperado = _caso["esperadoEnUnComprobante"]!.AsObject();
        documento.VoucherType!.Code.Should().Be((string)esperado["tipo"]!);
        documento.SourceType.Should().Be((string)esperado["origen"]!);
        documento.SourcePublicId.Should().Be(desembolso[0].Envelope.Origin.PublicId, "el comprobante es el del desembolso");
        documento.Description.Should().EndWith("· recoge 2 créditos");
        var lineas = documento.Lines.Select(l => new PostingLine
        {
            AccountCode = E.D.Db.ChartOfAccounts.Single(a => a.Id == l.AccountId).Code, Debit = l.Debit, Credit = l.Credit, PersonId = l.PersonId,
            BranchId = l.BranchId, CostCenterId = l.CostCenterId,
            CrossDocumentType = l.CrossDocumentTypeId is { } c ? E.D.Db.CrossDocumentTypes.Single(x => x.Id == c).Code : null,
            CrossDocumentNumber = l.CrossDocumentNumber,
        }).ToList();
        lineas.Select(Clave).Should().BeEquivalentTo(esperado["lineas"]!.AsArray().Select(l => Clave(E.Linea(l!.AsObject()))));
        SaldosDelPuente(lineas).Should().HaveCount(2).And.OnlyContain(s => s == 0m, "el puente entra y sale en el mismo comprobante");
        (await E.D.Db.LendingPostings.ToListAsync()).Should().HaveCount(unidades.Sum(u => u.Count))
            .And.OnlyContain(p => p.AccountingDocumentId == documento.Id && p.BatchPublicId == null);
    }

    [Fact]
    public async Task La_completitud_no_avisa_por_el_puente_de_la_recogida_propia_y_si_por_los_demas()
    {
        var cuenta = E.Cuenta("24959501");
        cuenta.Nature = Domain.Enums.Accounting.AccountNature.Credit;
        cuenta.RequiresCrossDocument = true;
        await E.D.Db.SaveChangesAsync();
        var propia = await E.D.Db.LendingPostingRules.SingleAsync(x => x.Role == R.RecaudoPorAplicar && x.CollectorClass == "OwnLoanPayoff" && x.BranchId == null);

        var c = (await new GetLendingRulesCompletenessQueryHandler(E.D.Db, E.Dimensiones, Registro(), new TiposDeComprobanteDeCartera(E.D.Db))
            .Handle(new GetLendingRulesCompletenessQuery(new DateOnly(2026, 11, 5)), default)).Value;

        c.Warnings.Should().NotContain(w => w.RulePublicId == propia.PublicId,
            "el desembolso y el recibo de la recogida saldan el puente con el mismo cruce: ni su naturaleza ni su cruce estorban");
        c.Warnings.Should().Contain(w => w.Code == GetLendingRulesCompletenessQueryHandler.AvisoPuenteConCruce && w.AccountCode == "24959501",
            "«desembolso por girar» lo salda Tesorería con su propio documento");
        ReglasDeLaMatrizDeCartera.NaturalezaIndiferente(R.RecaudoPorAplicar, "OwnLoanPayoff").Should().BeTrue();
        ReglasDeLaMatrizDeCartera.NaturalezaIndiferente(R.RecaudoPorAplicar, "Payroll").Should().BeFalse();
    }

    private static IRegistroDeIntegracion Registro()
    {
        var registro = Substitute.For<IRegistroDeIntegracion>();
        registro.QueCuentanAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ParticipanteVigente>>([]));
        return registro;
    }
}
