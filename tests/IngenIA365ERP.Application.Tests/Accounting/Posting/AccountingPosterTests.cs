using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Posting;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Posting;

/// <summary>
/// T035 — el contrato de contabilización (contracts/contabilizacion.md): agrega sin guardar, no
/// deja rastro cuando falla, numera en secuencia, respeta tipos, períodos y alcance, y reversa
/// con referencia cruzada.
/// </summary>
public class AccountingPosterTests
{
    private static readonly DateOnly Marzo15 = ContabilidadTestData.Marzo15;

    [Fact]
    public async Task Agrega_documento_y_lineas_sin_guardar_y_el_llamador_guarda_todo_junto()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        var r = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var doc = r.Value;
        doc.Number.Should().Be(1);
        doc.Status.Should().Be(DocumentStatus.Posted);
        doc.OriginModule.Should().Be("CNT");
        doc.PostedBy.Should().Be("contadora@demo");
        doc.RegisteredByUserId.Should().Be(9);
        doc.PeriodId.Should().NotBeNull();
        doc.Lines.Should().HaveCount(2);
        doc.Lines.Should().OnlyContain(l => l.IsPosted && l.Date == Marzo15 && l.BranchId == d.Principal.Id, "la sucursal principal se propone sola");
        doc.Lines.Select(l => l.LineNumber).Should().Equal(1, 2);

        (await d.Db.AccountingDocuments.CountAsync()).Should().Be(0, "el contrato no guarda");
        d.Db.ChangeTracker.Entries<AccountingDocument>().Should().ContainSingle();

        await d.Db.SaveChangesAsync();
        (await d.Db.JournalEntries.CountAsync()).Should().Be(2);
        (await d.Db.VoucherTypes.SingleAsync(v => v.Code == "CG")).NextNumber.Should().Be(2);
        (await d.Db.ChartOfAccounts.SingleAsync(a => a.Id == caja.Id)).FirstMovementAt.Should().Be(Marzo15);
    }

    [Fact]
    public async Task Un_descuadre_no_agrega_nada_ni_consume_numeracion()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);
        var request = new PostingRequest("CG", Marzo15, "Descuadrado", ContabilidadTestData.Manual(),
            [PostingLine.Debito(caja.Id, 100m), PostingLine.Credito(ingreso.Id, 90m)]);

        var r = await d.Poster.PrepareAsync(request, CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Document.Unbalanced");
        r.Error.Message.Should().Contain("10");
        d.Db.ChangeTracker.Entries<AccountingDocument>().Should().BeEmpty();
        d.Db.ChangeTracker.Entries<JournalEntry>().Should().BeEmpty();
        (await d.Db.VoucherTypes.SingleAsync(v => v.Code == "CG")).NextNumber.Should().Be(1);
        (await d.Db.ChartOfAccounts.SingleAsync(a => a.Id == caja.Id)).FirstMovementAt.Should().BeNull();
    }

    [Fact]
    public async Task Varios_errores_llegan_juntos_con_datos_para_la_pantalla()
    {
        var d = new ContabilidadTestData();
        var conTercero = d.Cuenta("130505", tercero: true);
        var inactiva = d.Cuenta("413505", AccountNature.Credit, activa: false);

        var r = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(conTercero, inactiva), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Document.Invalid");
        r.Error.Message.Should().Contain("2 error(es)").And.Contain("130505").And.Contain("413505");
        var datos = r.Error.Should().BeOfType<ErrorConDatos>().Which.Data;
        var errores = (System.Collections.IEnumerable)datos.GetType().GetProperty("errors")!.GetValue(datos)!;
        errores.Cast<object>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Un_solo_error_de_linea_llega_con_su_codigo_y_su_linea()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var conTercero = d.Cuenta("130505", tercero: true);

        var r = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, conTercero), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Line.ThirdPartyRequired");
        r.Error.Message.Should().StartWith("Línea 2:");
    }

    [Fact]
    public async Task El_tipo_debe_corresponder_al_origen()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        // NM es de Nómina: no se digita a mano.
        var manualConNm = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, tipo: "NM"), CancellationToken.None);
        manualConNm.Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule");

        // CG es manual: Nómina no lo usa.
        var nominaConCg = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, origen: ContabilidadTestData.Nomina()), CancellationToken.None);
        nominaConCg.Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule");

        // Y NM desde Nómina, con cuentas habilitadas para Nómina, sí.
        var nomina = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, origen: ContabilidadTestData.Nomina(), tipo: "NM"), CancellationToken.None);
        nomina.IsSuccess.Should().BeTrue(nomina.Error.Message);

        (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, tipo: "XX"), CancellationToken.None)).Error.Code.Should().Be("Accounting.VoucherType.NotFound");
    }

    [Fact]
    public async Task La_fecha_cae_en_un_periodo_abierto_y_solo_al_digitar_no_puede_ser_futura()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, fecha: new DateOnly(2026, 2, 10)), CancellationToken.None))
            .Error.Code.Should().Be("Accounting.Period.Closed");
        (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, fecha: new DateOnly(2026, 7, 10)), CancellationToken.None))
            .Error.Code.Should().Be("Accounting.Date.InFuture");
        (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, fecha: new DateOnly(2025, 12, 10)), CancellationToken.None))
            .Error.Code.Should().Be("Accounting.Period.NotFound");

        // La nómina se aprueba el 20 con fecha 31: el período está abierto y la fecha es la de la operación.
        var nomina = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, fecha: new DateOnly(2026, 3, 31), origen: ContabilidadTestData.Nomina(), tipo: "NM"), CancellationToken.None);
        nomina.IsSuccess.Should().BeTrue(nomina.Error.Message);
    }

    [Fact]
    public async Task Sin_contabilidad_iniciada_nada_se_contabiliza()
    {
        var d = new ContabilidadTestData(iniciada: false);
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None)).Error.Code.Should().Be("Accounting.NotInitialized");
    }

    [Fact]
    public async Task El_numero_es_consecutivo_por_tipo()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        var primero = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None);
        await d.Db.SaveChangesAsync();
        var segundo = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None);
        await d.Db.SaveChangesAsync();
        var nomina = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso, origen: ContabilidadTestData.Nomina(), tipo: "NM"), CancellationToken.None);

        primero.Value.Number.Should().Be(1);
        segundo.Value.Number.Should().Be(2);
        nomina.Value.Number.Should().Be(1, "cada tipo lleva su consecutivo");
    }

    [Fact]
    public async Task El_alcance_de_sucursales_propone_la_del_usuario_y_rechaza_las_demas_solo_al_digitar()
    {
        var d = new ContabilidadTestData();
        d.RestringirA(d.Norte);
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);

        var propuesta = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None);
        propuesta.IsSuccess.Should().BeTrue(propuesta.Error.Message);
        propuesta.Value.Lines.Should().OnlyContain(l => l.BranchId == d.Norte.Id, "se propone la sucursal asignada");

        var fuera = await d.Poster.PrepareAsync(new PostingRequest("CG", Marzo15, "Fuera", ContabilidadTestData.Manual(),
            [new PostingLine { AccountId = caja.Id, Debit = 100m, BranchId = d.Principal.Id }, PostingLine.Credito(ingreso.Id, 100m)]), CancellationToken.None);
        fuera.Error.Code.Should().Be("Accounting.Line.BranchOutOfScope");

        // Nómina manda la sucursal de la operación; el alcance de quien aprueba no la limita (FR-035).
        var nomina = await d.Poster.PrepareAsync(new PostingRequest("NM", Marzo15, "Nómina", ContabilidadTestData.Nomina(),
            [new PostingLine { AccountId = caja.Id, Debit = 100m, BranchId = d.Principal.Id }, PostingLine.Credito(ingreso.Id, 100m)]), CancellationToken.None);
        nomina.IsSuccess.Should().BeTrue(nomina.Error.Message);
        nomina.Value.Lines.Should().OnlyContain(l => l.BranchId == d.Principal.Id);
    }

    [Fact]
    public async Task El_centro_de_costo_de_un_modulo_se_descarta_si_la_cuenta_no_lo_maneja_pero_al_digitar_es_error()
    {
        var d = new ContabilidadTestData();
        var gasto = d.Cuenta("510505", centro: true);
        var pasivo = d.Cuenta("250505", AccountNature.Credit);
        PostingRequest Con(AccountingOrigin origen, string tipo) => new(tipo, Marzo15, "Con centro", origen,
        [
            new PostingLine { AccountId = gasto.Id, Debit = 100m, CostCenterId = d.Centro.Id },
            new PostingLine { AccountId = pasivo.Id, Credit = 100m, CostCenterId = d.Centro.Id },
        ]);

        var nomina = await d.Poster.PrepareAsync(Con(ContabilidadTestData.Nomina(), "NM"), CancellationToken.None);
        nomina.IsSuccess.Should().BeTrue(nomina.Error.Message);
        nomina.Value.Lines.Single(l => l.AccountId == gasto.Id).CostCenterId.Should().Be(d.Centro.Id);
        nomina.Value.Lines.Single(l => l.AccountId == pasivo.Id).CostCenterId.Should().BeNull("el pasivo no maneja centro de costo");

        var manual = await d.Poster.PrepareAsync(Con(ContabilidadTestData.Manual(), "CG"), CancellationToken.None);
        manual.Error.Code.Should().Be("Accounting.Line.CostCenterNotAllowed");
    }

    [Fact]
    public async Task Validar_devuelve_errores_y_avisos_por_campo_sin_agregar_nada()
    {
        var d = new ContabilidadTestData();
        var retencion = d.Cuenta("236505", AccountNature.Credit, baseGravable: true, tarifa: 0.04m);
        var gasto = d.Cuenta("510505");
        var request = new PostingRequest("CG", Marzo15, "Retención", ContabilidadTestData.Manual(),
        [
            PostingLine.Debito(gasto.Id, 103m),
            new PostingLine { AccountId = retencion.Id, Credit = 103m, TaxBase = 2_500m },
        ]);

        var v = await d.Poster.ValidarAsync(request, CancellationToken.None);

        v.EsValido.Should().BeTrue();
        v.Avisos.Should().ContainSingle().Which.Code.Should().Be("Accounting.Line.TaxAmountDiffers");
        v.Avisos[0].LineNumber.Should().Be(2);
        v.Avisos[0].Field.Should().Be("TaxBase");
        v.TotalDebit.Should().Be(103m);
        d.Db.ChangeTracker.Entries<AccountingDocument>().Should().BeEmpty();

        // Un fallo de encabezado llega como infracción de la línea 0 con su campo.
        var cerrado = await d.Poster.ValidarAsync(request with { Date = new DateOnly(2026, 1, 15) }, CancellationToken.None);
        cerrado.EsValido.Should().BeFalse();
        cerrado.Errores.Should().ContainSingle().Which.Should().BeEquivalentTo(new { LineNumber = 0, Field = "Date", Code = "Accounting.Period.Closed" });

        // Y el aviso no impide contabilizar.
        var contabilizado = await d.Poster.PrepareAsync(request, CancellationToken.None);
        contabilizado.IsSuccess.Should().BeTrue(contabilizado.Error.Message);
    }

    [Fact]
    public async Task La_apertura_solo_se_fecha_el_dia_anterior_al_primer_periodo_y_no_lleva_periodo()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var capital = d.Cuenta("310505", AccountNature.Credit);

        var malFechada = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, capital, fecha: Marzo15, tipo: "AP", kind: DocumentKind.Opening), CancellationToken.None);
        malFechada.Error.Code.Should().Be("Accounting.Opening.DateInvalid");
        malFechada.Error.Message.Should().Contain("2025-12-31");

        var conCg = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, capital, fecha: new DateOnly(2025, 12, 31), tipo: "CG", kind: DocumentKind.Opening), CancellationToken.None);
        conCg.Error.Code.Should().Be("Accounting.VoucherType.NotAllowedForModule");

        var apertura = await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, capital, fecha: new DateOnly(2025, 12, 31), tipo: "AP", kind: DocumentKind.Opening), CancellationToken.None);
        apertura.IsSuccess.Should().BeTrue(apertura.Error.Message);
        apertura.Value.Kind.Should().Be(DocumentKind.Opening);
        apertura.Value.PeriodId.Should().BeNull();
        apertura.Value.Date.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public async Task La_reversion_invierte_referencia_y_solo_la_hace_el_modulo_dueno()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);
        var original = (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None)).Value;
        await d.Db.SaveChangesAsync();

        var ajena = await d.Poster.PrepareReversalAsync(original, Marzo15, "no es mío", ContabilidadTestData.Nomina(), CancellationToken.None);
        ajena.Error.Code.Should().Be("Accounting.Document.ModuleOwned");

        var sinMotivo = await d.Poster.PrepareReversalAsync(original, Marzo15, " ", ContabilidadTestData.Manual(), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Accounting.ReasonRequired");

        var r = await d.Poster.PrepareReversalAsync(original, Marzo15, "Se duplicó", ContabilidadTestData.Manual(), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var reverso = r.Value;
        reverso.Number.Should().Be(2);
        reverso.Kind.Should().Be(DocumentKind.Reversal);
        reverso.ReversesDocument.Should().BeSameAs(original);
        reverso.ReversalReason.Should().Be("Se duplicó");
        reverso.Description.Should().Contain("CG-1").And.Contain("Se duplicó");
        reverso.Lines.Single(l => l.AccountId == caja.Id).Credit.Should().Be(100m);
        reverso.Lines.Single(l => l.AccountId == ingreso.Id).Debit.Should().Be(100m);
        original.Status.Should().Be(DocumentStatus.Reversed);
        original.ReversedByDocument.Should().BeSameAs(reverso);

        await d.Db.SaveChangesAsync();
        (await d.Db.AccountingDocuments.SingleAsync(x => x.Id == original.Id)).ReversedByDocumentId.Should().Be(reverso.Id);
        (await d.Poster.PrepareReversalAsync(original, Marzo15, "otra vez", ContabilidadTestData.Manual(), CancellationToken.None)).Error.Code.Should().Be("Accounting.Document.AlreadyReversed");
        (await d.Poster.PrepareReversalAsync(reverso, Marzo15, "la reversión", ContabilidadTestData.Manual(), CancellationToken.None)).Error.Code.Should().Be("Accounting.Document.IsReversal");
    }

    [Fact]
    public async Task La_reversion_en_periodo_cerrado_se_fecha_en_el_primer_periodo_abierto_y_lo_dice()
    {
        var d = new ContabilidadTestData();
        var caja = d.Cuenta("110505");
        var ingreso = d.Cuenta("413505", AccountNature.Credit);
        var original = (await d.Poster.PrepareAsync(ContabilidadTestData.Comprobante(caja, ingreso), CancellationToken.None)).Value;
        await d.Db.SaveChangesAsync();
        (await d.Db.AccountingPeriods.SingleAsync(p => p.Month == 3)).Status = PeriodStatus.Closed;
        await d.Db.SaveChangesAsync();
        d.Clock.TodayUtc.Returns(new DateOnly(2026, 4, 10));

        var r = await d.Poster.PrepareReversalAsync(original, Marzo15, "tarde", ContabilidadTestData.Manual(), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        r.Value.Date.Should().Be(new DateOnly(2026, 4, 1));
        r.Value.Description.Should().Contain("2026-04-01").And.Contain("2026-03");
        (await d.Db.AccountingPeriods.SingleAsync(p => p.Id == r.Value.PeriodId)).Month.Should().Be(4);
    }
}
