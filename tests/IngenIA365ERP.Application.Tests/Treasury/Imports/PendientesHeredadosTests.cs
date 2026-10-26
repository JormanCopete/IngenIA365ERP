using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Receipts;
using IngenIA365ERP.Application.Treasury.Imports;
using IngenIA365ERP.Domain.Entities.Treasury.Activation;
using IngenIA365ERP.Domain.Entities.Treasury.Checkbooks;
using IngenIA365ERP.Domain.Enums.Treasury;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using P = IngenIA365ERP.Application.Treasury.Imports.PlantillaDePendientesHeredados;

namespace IngenIA365ERP.Application.Tests.Treasury.Imports;

/// <summary>
/// Feature 015, fase 46, T0470 (archivos-y-plantillas.md §5; data-model §24 paso 3; FR-161): la plantilla <c>tesoreria.pendientes-heredados</c> carga a
/// <c>TES_LegacyPendingLoads</c> los cheques sin cobrar —que nacen como cheques girados en tránsito de una chequera registrada— y las facturas por pagar —que nacen como
/// órdenes de la bandeja contra la cuenta por pagar contable de su documento cruce—, coteja cada total con su cifra de referencia (avisa, no bloquea) y es todo o nada.
/// Prueba escrita, sin correr.
/// </summary>
public class PendientesHeredadosTests
{
    private readonly EscenarioDeTesoreria _t = new();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly Guid _documentoContable = Guid.NewGuid();

    private static readonly string[] Columnas = [P.ClaseDePendiente, P.Referencia, P.Cuenta, P.Numero, P.Beneficiario, P.Valor, P.Fecha, P.Factura, P.Entregado];

    public PendientesHeredadosTests()
    {
        _permisos.EsMaestroGlobal.Returns(true);
        _t.E.Como(_t.E.Cajera);
        _t.E.Db.Checkbooks.Add(new Checkbook
        {
            BankAccountId = _t.Pagos.Id, FromNumber = 100, ToNumber = 200, NextNumber = 150, Status = CheckbookState.Active, CustodianUserId = _t.E.Cajera.Id,
            RegisteredByUserId = _t.E.Cajera.Id, RegisteredAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        _t.E.Db.SaveChanges();
        _t.Contabilidad.CuentasPorPagarAsync(Arg.Any<FiltroDeCuentasPorPagar>(), Arg.Any<CancellationToken>())
            .Returns([new CuentaPorPagarDto(_documentoContable, _t.Beneficiaria.PublicId, "FV-321", EscenarioDeRecaudo.Hoy.AddDays(-10), 800_000m)]);
    }

    private void Archivo(params string?[][] filas)
    {
        var tabla = new TablaLeida(Columnas, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success(tabla)));
    }

    private static string?[] Cheque(string referencia, string numero, string valor = "450000") =>
        ["ChequeSinCobrar", referencia, "PAGOS", numero, "2001", valor, "2026-10-15", null, null];

    private static string?[] Factura(string referencia, string valor = "300000", string factura = "FV-321") =>
        ["FacturaPorPagar", referencia, null, null, "2001", valor, "2026-11-20", factura, null];

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo = ModoDeImportacion.Apply)
    {
        var ejecutor = new EjecutorDeImportacion(_t.E.Db, _lector, _permisos, _t.E.Servicios);
        var r = await new ImportLegacyTreasuryPendingCommandHandler(_t.E.Db, ejecutor, _t.Contabilidad, _t.E.Actor, _t.Numerador, _t.Validador, _t.Parametros, _t.E.Reloj)
            .Handle(new ImportLegacyTreasuryPendingCommand(modo, new ArchivoDeImportacion("pendientes.xlsx", [1, 2, 3])), default);
        _t.E.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task El_cheque_sin_cobrar_nace_como_cheque_girado_en_transito_de_su_chequera()
    {
        Archivo(Cheque("17", "120"));

        var r = await ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var cheque = await _t.E.Db.IssuedChecks.SingleAsync();
        cheque.CheckNumber.Should().Be(120);
        cheque.Status.Should().Be(IssuedCheckState.Issued);
        cheque.IsOpeningLoad.Should().BeTrue("es un cheque girado antes de la Tesorería nueva, como los de la puesta en marcha");
        var carga = await _t.E.Db.TreasuryLegacyPendingLoads.SingleAsync();
        carga.Should().Match<LegacyPendingLoad>(c => c.Kind == LegacyPendingKind.UncashedCheck && c.LegacyReference == "17" && c.Amount == 450_000m
                                                     && c.Status == LegacyPendingLoadState.Migrated && c.MigratedToPublicId == cheque.PublicId);
        (await _t.E.Db.IntegrationMessages.CountAsync()).Should().Be(0, "sin hechos: lo heredado ya está en los saldos de apertura");
    }

    [Fact]
    public async Task La_factura_por_pagar_nace_como_orden_contra_su_cuenta_por_pagar_contable()
    {
        Archivo(Factura("31"));

        var r = await ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        var orden = await _t.E.Db.PaymentOrders.SingleAsync();
        orden.Origin.Should().Be(PaymentOrderOrigin.AccountingPayable);
        orden.OriginOrderNumber.Should().Be("FV-321");
        orden.NetAmount.Should().Be(300_000m);
        orden.PayDate.Should().Be(EscenarioDeRecaudo.Hoy, "la factura venció el 20/11: la orden se paga desde hoy");
        var carga = await _t.E.Db.TreasuryLegacyPendingLoads.SingleAsync();
        carga.Status.Should().Be(LegacyPendingLoadState.Migrated);
        carga.MigratedToPublicId.Should().Be(orden.PublicId);
        carga.InvoiceNumber.Should().Be("FV-321");
    }

    [Fact]
    public async Task Todo_o_nada_una_factura_sin_cuenta_por_pagar_detiene_el_archivo()
    {
        Archivo(Cheque("17", "120"), Factura("32", factura: "FV-999"));

        var r = await ImportarAsync();

        r.IsFailure.Should().BeTrue();
        (await _t.E.Db.IssuedChecks.CountAsync()).Should().Be(0);
        (await _t.E.Db.TreasuryLegacyPendingLoads.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Lo_pendiente_en_contabilidad_no_se_pasa_entre_dos_filas_de_la_misma_factura()
    {
        Archivo(Factura("33", "500000"), Factura("34", "400000"));

        (await ImportarAsync()).IsFailure.Should().BeTrue("800.000 pendientes no alcanzan para 900.000");
    }

    [Fact]
    public async Task Un_pendiente_no_se_carga_dos_veces()
    {
        Archivo(Cheque("17", "120"));
        (await ImportarAsync()).IsSuccess.Should().BeTrue();

        Archivo(Cheque("17", "121"));
        var otra = await ImportarAsync();

        otra.IsFailure.Should().BeTrue();
        (await _t.E.Db.TreasuryLegacyPendingLoads.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task El_cheque_fuera_de_toda_chequera_registrada_no_entra()
    {
        Archivo(Cheque("18", "999"));

        (await ImportarAsync()).IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task El_total_se_coteja_con_la_cifra_de_referencia_y_avisa_si_no_coincide()
    {
        _t.E.Db.TreasuryReferenceFigures.Add(new TreasuryReferenceFigure
        {
            CutoffDate = EscenarioDeRecaudo.Hoy.AddDays(-1), Concept = P.ConceptoDeCheques, Dimension = "PAGOS", Amount = 500_000m, LoadedByUserId = _t.E.Cajera.Id,
            LoadedAt = new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc), ImportBatchPublicId = Guid.NewGuid(), SourceFileName = "cifras.xlsx",
        });
        _t.E.Db.SaveChanges();
        Archivo(Cheque("17", "120"));

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.IsSuccess.Should().BeTrue();
        r.Value.Warnings.Should().Contain(w => w.Code == ImportLegacyTreasuryPendingCommandHandler.CodigoDiferenciaConReferencia);
        (await _t.E.Db.TreasuryLegacyPendingLoads.CountAsync()).Should().Be(0, "la revisión no guarda nada");
        (await _t.E.Db.PaymentOrders.CountAsync()).Should().Be(0);
    }
}
