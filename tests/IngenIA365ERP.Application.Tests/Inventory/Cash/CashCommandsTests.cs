using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Integration.Contracts.Inventory;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.ElectronicInvoicing;
using IngenIA365ERP.Application.Inventory.Cash;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Pos;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Application.Tests.Inventory.Sales;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.ElectronicInvoicing;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Cash;

/// <summary>
/// Feature 012, I3, T555 (contracts/api.md §21; T50; FR-099, FR-100): la caja. Abrir sella el modo de base —el fondo fijo hereda el de la
/// caja y avisa <c>BaseDiffers</c>; la base del día es obligatoria y entra por un movimiento <c>BaseIncome</c>—, no abre sobre un día
/// cerrado, exige persona al cajero con el faltante a su cargo y abre con aviso si la guardia fiscal bloquea. Cerrar se niega con ventas o
/// movimientos en curso, exige motivo en toda diferencia y el dato que corresponde al método de cada medio; dentro de la tolerancia la
/// diferencia se confirma, por encima pasa a la política sin el cajero, el rechazo deja recontar y el retiro de cierre saca lo contado menos
/// el fondo. Los movimientos: caja destino sin sesión, retiro sobre lo esperado y anulación con la sesión cerrada. El cierre del día exige
/// las sesiones cerradas, no se repite y al reabrirlo el siguiente es la versión 2.
/// </summary>
public class CashCommandsTests
{
    private const int OtraCajera = 30;

    private VentasDePrueba V = null!;
    private KardexDePrueba K => V.K;
    private IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    private CashRegister _caja2 = null!;
    private readonly ICerrojoPorClave _cerrojo = Substitute.For<ICerrojoPorClave>();
    private readonly IAuditoriaDelPuntoDeVenta _auditoria = Substitute.For<IAuditoriaDelPuntoDeVenta>();

    private static async Task<CashCommandsTests> CrearAsync()
    {
        var t = new CashCommandsTests { V = await VentasDePrueba.CrearAsync() };
        var db = t.Db;
        foreach (var (codigo, clase) in new[] { ("CJ", DocumentClass.CashMovement), ("AQ", DocumentClass.CashCountDifference) })
        {
            var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, AllWarehouses = true };
            tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo, NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
            db.InventoryDocumentTypes.Add(tipo);
        }
        t.V.Efectivo.ToleranceAmount = 1000m;
        t._caja2 = new CashRegister { PointOfSaleId = t.V.Punto.Id, Code = "CJ2", Name = "Caja 2", WarehouseId = t.K.Principal.Id, ReceiptWidthMm = 80 };
        db.CashRegisters.Add(t._caja2);
        await db.SaveChangesAsync();
        db.CashRegisterDocumentTypes.Add(new CashRegisterDocumentType { CashRegisterId = t._caja2.Id, Role = CashRegisterDocumentRole.PosSale, DocumentTypeId = t.K.Tipo("RV").Id });
        await db.SaveChangesAsync();

        var emision = new EmisionDeInventario(db);
        t.V.EfectosAdicionales.Add(new EfectoMovimientoDeCaja(db, emision, t.Reglas(), t.V.Toque, t.V.Compras.C.Reloj));
        t.V.EfectosAdicionales.Add(new EfectoDiferenciaDeArqueo(db, emision));
        return t;
    }

    // ------------------------------------------------------------------------------------------------ servicios --

    private SesionesDeCaja Sesiones() => new(Db, K.Actor, K.Alcance, K.Permisos);

    private ReglasDeMovimientoDeCaja Reglas() => new(Db, Sesiones());

    private ArqueoDeLaSesion Arqueos() => new(Db, V.Compras.C.Reloj, Sesiones(), V.Confirmacion());

    private OpenCashSessionCommandHandler Abrir() => new(Db, K.Actor, V.Compras.C.Reloj, K.Alcance, K.Lector(), _cerrojo, new GuardiaDeEmisionFiscal(K.Lector(), Db),
        Sesiones(), V.Confirmacion(), _auditoria);

    /// <summary>2026-11-25: la apertura con el lector de parámetros real, donde Caja.ArqueoEstricto viene apagado (el arqueo sencillo).</summary>
    private OpenCashSessionCommandHandler AbrirSencillo() => new(Db, K.Actor, V.Compras.C.Reloj, K.Alcance, K.Lector(), _cerrojo,
        new GuardiaDeEmisionFiscal(K.Lector(), Db),
        new SesionesDeCaja(Db, K.Actor, K.Alcance, K.Permisos, new IngenIA365ERP.Application.Receipts.Common.ParametrosDeCaja(K.Lector()), V.Compras.C.Reloj),
        V.Confirmacion(), _auditoria);

    private CloseCashSessionCommandHandler Cerrar() => new(Db, K.Actor, V.Compras.C.Reloj, Sesiones(), Arqueos(), V.Toque, V.Confirmacion(), _auditoria);

    /// <summary>2026-11-24: el cierre con el lector de parámetros real, donde Caja.ArqueoEstricto viene apagado (el arqueo sencillo).</summary>
    private CloseCashSessionCommandHandler CerrarSencillo()
    {
        var sesiones = new SesionesDeCaja(Db, K.Actor, K.Alcance, K.Permisos, new IngenIA365ERP.Application.Receipts.Common.ParametrosDeCaja(K.Lector()), V.Compras.C.Reloj);
        return new(Db, K.Actor, V.Compras.C.Reloj, sesiones, new ArqueoDeLaSesion(Db, V.Compras.C.Reloj, sesiones, V.Confirmacion()), V.Toque, V.Confirmacion(),
            _auditoria);
    }

    private RecountCashSessionCommandHandler Recontar() => new(Db, K.Actor, V.Compras.C.Reloj, Sesiones(), Arqueos(), V.Confirmacion(), _auditoria);

    private SaveInventoryDraftCommandHandler Guardar() => new(Db, K.Maestros(), K.Alcance, K.Actor, V.Compras.C.Reloj, V.Efectos(), K.Vista(),
        [new BorradorDeMovimientoDeCaja(Db, Sesiones(), Reglas())]);

    private ExecuteDayCloseCommandHandler CerrarDia() => new(Db, K.Actor, V.Compras.C.Reloj, K.Alcance, _cerrojo, _auditoria);

    private Guid TipoMovimiento => K.Tipo("CJ").PublicId;

    private async Task<Guid> MovimientoAsync(CashSession sesion, CashMovementKind clase, decimal valor, Guid? cajaDestino = null)
    {
        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Cash,
            new CashMovementInput(TipoMovimiento, sesion.PublicId, clase, V.Efectivo.PublicId, valor, "Retiro de prueba",
                DestinationCashRegisterPublicId: cajaDestino).ComoBorrador(TipoMovimiento)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        return r.Value.PublicId;
    }

    private Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        new ConfirmInventoryDocumentCommandHandler(Db, V.Confirmacion()).Handle(new ConfirmInventoryDocumentCommand(documento, DocumentClassGroup.Cash), default);

    private CashSession SesionEn(CashRegister caja, decimal @base, int cajero = KardexDePrueba.Usuario, Guid? vinculada = null)
    {
        var sesion = new CashSession
        {
            CashRegisterId = caja.Id, PointOfSaleId = caja.PointOfSaleId, CashierUserId = cajero, CashierName = cajero == KardexDePrueba.Usuario ? "Cajera Uno" : "Cajera Dos",
            OperatingDate = Catalog.CatalogoDePrueba.Hoy, OpenedAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc),
            LastActivityAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), OpeningBase = @base, ShortageTreatment = "Gasto", LinkedSessionPublicId = vinculada,
        };
        Db.CashSessions.Add(sesion);
        Db.SaveChanges();
        return sesion;
    }

    private async Task<decimal> VentaEnEfectivoAsync(CashSession? sesion = null)
    {
        var s = sesion ?? V.Sesion;
        var venta = await V.VentaConfirmadaAsync(total => [new DocumentPaymentInput(V.Efectivo.PublicId, total, CashSessionPublicId: s.PublicId)],
            lineas: V.Linea(V.P1, 1m));
        return venta.AmountDue;
    }

    private static CashCountInput Efectivo(VentasDePrueba v, decimal contado, string? motivo = null) => new(v.Efectivo.PublicId, CountedTotal: contado, Reason: motivo);

    // ----------------------------------------------------------------------------------------------------- abrir --

    [Fact]
    public async Task Con_fondo_fijo_la_sesion_hereda_el_fondo_de_la_caja_y_avisa_si_la_base_indicada_difiere()
    {
        var t = await CrearAsync();
        var anterior = t.SesionEn(t._caja2, 200_000m, OtraCajera);
        anterior.Cerrar(OtraCajera, new DateTime(2026, 9, 24, 23, 0, 0, DateTimeKind.Utc));
        await t.Db.SaveChangesAsync();

        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 150_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.OpeningBase.Should().Be(200_000m);
        r.Value.Session.BaseMode.Should().Be(CashSession.BaseFondoFijo);
        r.Value.BaseIncomeDocument.Should().BeNull("el fondo fijo no emite movimiento");
        r.Value.Warnings.Select(w => w.Code).Should().Contain(ErroresDeCaja.BaseDiffersCode);
        r.Value.Session.OperatingDate.Should().Be(Catalog.CatalogoDePrueba.Hoy);
        await t._cerrojo.Received().BloquearAsync(ClavesDeCerrojo.PuntoDeVenta(t.V.Punto.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Con_el_arqueo_sencillo_el_fondo_fijo_es_lo_que_el_cajero_conto_al_abrir()
    {
        // 2026-11-25 (pedido del dueño): se abría con 30.000 y el arqueo esperaba los 100.000 de la sesión anterior de la caja.
        var t = await CrearAsync();
        var anterior = t.SesionEn(t._caja2, 100_000m, OtraCajera);
        anterior.Cerrar(OtraCajera, new DateTime(2026, 9, 24, 23, 0, 0, DateTimeKind.Utc));
        await t.Db.SaveChangesAsync();

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 30_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.OpeningBase.Should().Be(30_000m);
        r.Value.Session.BaseMode.Should().Be(CashSession.BaseFondoFijo);
        r.Value.Warnings.Select(w => w.Code).Should().NotContain(ErroresDeCaja.BaseDiffersCode);
        var esperado = await new GetCashSessionExpectedQueryHandler(t.Sesiones()).Handle(new GetCashSessionExpectedQuery(r.Value.Session.CashSessionPublicId), default);
        esperado.Value.Total.Should().Be(30_000m, "el arqueo espera la base con que se abrió");
    }

    [Fact]
    public async Task La_base_contada_billete_por_billete_es_la_suma_de_los_billetes()
    {
        var t = await CrearAsync();
        var cincuenta = new CashDenomination { Value = 50_000m, Kind = CashDenominationKind.Bill, ValidFrom = new DateOnly(2026, 1, 1) };
        var diez = new CashDenomination { Value = 10_000m, Kind = CashDenominationKind.Bill, ValidFrom = new DateOnly(2026, 1, 1) };
        t.Db.CashDenominations.AddRange(cincuenta, diez);
        await t.Db.SaveChangesAsync();

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, null,
            [new DenominationCountInput(cincuenta.PublicId, 1), new DenominationCountInput(diez.PublicId, 3)]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.OpeningBase.Should().Be(80_000m);
    }

    [Fact]
    public async Task Con_el_estricto_el_fondo_fijo_no_se_hereda_de_una_sesion_de_la_caja_unica()
    {
        var t = await CrearAsync();
        var propia = t.SesionEn(t._caja2, 100_000m, OtraCajera);
        propia.Cerrar(OtraCajera, new DateTime(2026, 9, 23, 23, 0, 0, DateTimeKind.Utc));
        var vinculada = t.SesionEn(t._caja2, 0m, OtraCajera, vinculada: Guid.NewGuid());
        vinculada.Cerrar(OtraCajera, new DateTime(2026, 9, 24, 23, 0, 0, DateTimeKind.Utc));
        await t.Db.SaveChangesAsync();

        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 100_000m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.OpeningBase.Should().Be(100_000m, "la sesión de la caja única abre en cero y no es el fondo de la caja");
        r.Value.Warnings.Select(w => w.Code).Should().NotContain(ErroresDeCaja.BaseDiffersCode);
    }

    [Fact]
    public async Task Con_base_del_dia_la_base_es_obligatoria_y_entra_por_un_movimiento_BaseIncome()
    {
        var t = await CrearAsync();
        t.K.Parametro(ParametrosDeInventario.CajaBaseModo, CashSession.BaseDelDia);

        var sinBase = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId), default);
        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 50_000m), default);

        sinBase.Error.Code.Should().Be(ErroresDeCaja.BaseRequiredCode);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.OpeningBase.Should().Be(0m, "con base del día la base llega por su movimiento");
        r.Value.BaseIncomeDocument!.Status.Should().Be(DocumentStatus.Confirmed);
        var detalle = t.Db.CashMovementDetails.Single();
        (detalle.Kind, detalle.Amount, detalle.Destination).Should().Be((CashMovementKind.BaseIncome, 50_000m, CashMovementDestination.Safe));
        t.V.MensajesDe(r.Value.BaseIncomeDocument.DocumentPublicId).Should().Contain(MovimientoDeCajaRegistradoV1.Type);

        var esperado = await new GetCashSessionExpectedQueryHandler(t.Sesiones()).Handle(new GetCashSessionExpectedQuery(r.Value.Session.CashSessionPublicId), default);
        esperado.Value.Total.Should().Be(50_000m, "el ingreso de base confirmado cuenta en el esperado");
    }

    [Fact]
    public async Task No_abre_sobre_un_dia_cerrado()
    {
        var t = await CrearAsync();
        t.Db.DayCloses.Add(new DayClose
        {
            PointOfSaleId = t.V.Punto.Id, OperatingDate = Catalog.CatalogoDePrueba.Hoy, ClosedAt = DateTime.UtcNow, ClosedByUserId = KardexDePrueba.Usuario,
        });
        await t.Db.SaveChangesAsync();

        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId), default);

        r.Error.Code.Should().Be(ErroresDeCaja.DayClosedCode);
    }

    [Fact]
    public async Task Con_el_faltante_a_cargo_del_cajero_un_usuario_sin_persona_no_abre()
    {
        var t = await CrearAsync();
        // 2026-11-26: el tratamiento del faltante es el de Recaudo, común a las dos cajas, y sólo rige con el arqueo estricto.
        t.V.Compras.Parametro(Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.ModuloRecaudo,
            Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.CajaArqueoEstricto, "true");
        t.V.Compras.Parametro(Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.ModuloRecaudo,
            Domain.Receipts.Parameters.ParametrosDeRecaudoYTesoreria.CajaTratamientoFaltante, "CashierReceivable");

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId), default);

        r.Error.Code.Should().Be(ErroresDeCaja.CashierWithoutPersonCode);
    }

    [Fact]
    public async Task Con_la_guardia_fiscal_bloqueada_la_sesion_abre_con_el_aviso_NotReady()
    {
        var t = await CrearAsync();
        t.Db.ParameterVersions.Single(p => p.Key == ParametrosDeFacturacionElectronica.ObligadaAFacturar).Value = "true";
        await t.Db.SaveChangesAsync();

        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Session.Status.Should().Be(CashSessionStatus.Open);
        r.Value.Warnings.Select(w => w.Code).Should().Contain(ErroresDeVentas.NotReadyCode);
    }

    [Fact]
    public async Task Una_sesion_ajena_sin_ViewAll_es_el_mismo_404_que_la_inexistente()
    {
        var t = await CrearAsync();
        var ajena = t.SesionEn(t._caja2, 0m, OtraCajera);
        t.K.Permisos.HasPermissionAsync(SesionesDeCaja.PermisoVerTodas, Arg.Any<CancellationToken>()).Returns(false);

        var r = await new GetCashSessionQueryHandler(t.Db, t.Sesiones()).Handle(new GetCashSessionQuery(ajena.PublicId), default);
        var lista = await new ListCashSessionsQueryHandler(t.Db, t.Sesiones()).Handle(new ListCashSessionsQuery(), default);

        r.Error.Code.Should().Be(ErroresDeCaja.SessionNotFoundCode);
        lista.Value.Items.Select(s => s.CashSessionPublicId).Should().Equal(t.V.Sesion.PublicId);
    }

    // ----------------------------------------------------------------------------------------------------- cerrar --

    [Fact]
    public async Task No_cierra_con_ventas_suspendidas_ni_con_movimientos_en_curso()
    {
        var t = await CrearAsync();
        var suspendida = new InventoryDocument
        {
            Class = DocumentClass.NonElectronicSalesReceipt, DocumentTypeId = t.K.Tipo("RV").Id, OperationDate = Catalog.CatalogoDePrueba.Hoy,
            BranchId = t.K.Sucursal.Id, PointOfSaleId = t.V.Punto.Id, CashRegisterId = t.V.Caja.Id, CashSessionId = t.V.Sesion.Id,
            CreatedByUserId = KardexDePrueba.Usuario, IsSuspended = true, SuspendedLabel = "Mesa 3", AmountDue = 4_000m,
        };
        t.Db.InventoryDocuments.Add(suspendida);
        await t.Db.SaveChangesAsync();

        var conBorrador = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, []), default);
        // El comando que falla descarta lo que seguía: se relee.
        t.Db.InventoryDocuments.Single(d => d.PublicId == suspendida.PublicId).Descartar(KardexDePrueba.Usuario, DateTime.UtcNow, "prueba");
        await t.Db.SaveChangesAsync();
        await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToSafe, 1_000m);
        var conMovimiento = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, []), default);

        conBorrador.Error.Code.Should().Be(ErroresDeCaja.HasOpenDraftsCode);
        conBorrador.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new
        {
            drafts = new[] { new BorradorAbiertoDto(suspendida.PublicId, "Mesa 3", 4_000m) },
        });
        conMovimiento.Error.Code.Should().Be(ErroresDeCaja.HasPendingMovementsCode);
    }

    [Fact]
    public async Task Toda_diferencia_exige_motivo_y_un_dato_ajeno_al_metodo_es_CountMethodMismatch()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();

        var sinMotivo = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total - 500m)]), default);
        var conLote = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId,
            [new CashCountInput(t.V.Efectivo.PublicId, TerminalBatches: [new TerminalBatchInput(Guid.NewGuid(), "001", total, 1)])]), default);
        var transferenciaContada = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId,
            [Efectivo(t.V, total), new CashCountInput(t.V.Transferencia.PublicId, CountedTotal: 10m)]), default);

        sinMotivo.Error.Code.Should().Be(ErroresDeCaja.ReasonRequiredCode);
        sinMotivo.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { paymentMeansCodes = new[] { "EFECTIVO" } });
        conLote.Error.Code.Should().Be(ErroresDeCaja.CountMethodMismatchCode);
        transferenciaContada.Error.Code.Should().Be(ErroresDeCaja.CountMethodMismatchCode, "un medio sin arqueo no se cuenta");
        t.Db.CashSessions.Single(s => s.Id == t.V.Sesion.Id).Status.Should().Be(CashSessionStatus.Open);
    }

    [Fact]
    public async Task Una_diferencia_dentro_de_la_tolerancia_se_confirma_con_su_motivo_y_emite_DiferenciaDeArqueoAprobada()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();

        var r = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total - 500m, "Faltó un billete")]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(CashSessionStatus.Closed);
        var linea = r.Value.Lines.Single(l => l.PaymentMeansCode == "EFECTIVO");
        (linea.Expected, linea.Counted, linea.Difference, linea.WithinTolerance, linea.Treatment)
            .Should().Be((total, total - 500m, -500m, true, CashDifferenceTreatment.ShortageToExpense));
        r.Value.DifferenceDocument!.Status.Should().Be(DocumentStatus.Confirmed);
        var documento = t.V.Documento(r.Value.DifferenceDocument.DocumentPublicId);
        documento.Class.Should().Be(DocumentClass.CashCountDifference);
        var diferencia = t.Db.CashDocumentLines.Single(l => l.DocumentId == documento.Id);
        (diferencia.Sign, diferencia.Amount, diferencia.WithinTolerance, diferencia.Reason).Should().Be(((short)-1, 500m, true, "Faltó un billete"));
        t.Db.CashCounts.Single().Status.Should().Be(DocumentStatus.Confirmed);
        t.Db.CashCountLines.Single(l => l.PaymentMeansId == t.V.Efectivo.Id).ToleranceAmount.Should().Be(1000m, "la tolerancia se copia del medio");
        t.V.MensajesDe(documento.PublicId).Should().Contain(DiferenciaDeArqueoAprobadaV1.Type);
        await t.K.Motor.DidNotReceive().EvaluarAsync(Arg.Any<string>(), t.K.Tipo("AQ").PublicId, Arg.Any<DateOnly>(), Arg.Any<decimal>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Por_encima_de_la_tolerancia_pasa_a_la_politica_sin_el_cajero_y_el_rechazo_deja_recontar()
    {
        var t = await CrearAsync();
        var ajena = t.SesionEn(t._caja2, 10_000m, OtraCajera);
        var tipoDiferencia = t.K.Tipo("AQ").PublicId;
        var niveles = new List<NivelDeAprobacion> { new(1, 0m, "Inventory.CashDifferences.Approve") };
        var conNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ConNiveles, niveles, null, false, false), 1));
        var sinNiveles = Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], null, false, false), null));
        t.K.Motor.EvaluarAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(ci => ci.ArgAt<Guid>(1) == tipoDiferencia ? conNiveles : sinNiveles);
        SolicitudDeAprobacion? pedida = null;
        ApprovalRequest? solicitud = null;
        t.K.Motor.SolicitarAsync(default!, default).ReturnsForAnyArgs(ci =>
        {
            pedida = ci.Arg<SolicitudDeAprobacion>();
            solicitud = new ApprovalRequest { SourcePublicId = pedida.SourcePublicId, CurrentLevel = 1 };
            solicitud.SellarNiveles(niveles);
            return Result.Success<ApprovalRequest?>(solicitud);
        });

        // La cierra un supervisor (el actor, con ViewAll): la cajera de la sesión tampoco aprueba.
        var r = await t.Cerrar().Handle(new CloseCashSessionCommand(ajena.PublicId, [Efectivo(t.V, 5_000m, "No cuadra")]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Status.Should().Be(CashSessionStatus.Closed, "cerrar es inmediato aunque la diferencia espere aprobación");
        r.Value.DifferenceDocument!.Status.Should().Be(DocumentStatus.PendingApproval);
        pedida!.Participantes.Should().Contain([OtraCajera, KardexDePrueba.Usuario]);
        pedida.Amount.Should().Be(5_000m);

        var yaSinRechazo = await t.Recontar().Handle(new RecountCashSessionCommand(ajena.PublicId, [Efectivo(t.V, 9_500m, "Apareció")]), default);
        yaSinRechazo.Error.Code.Should().Be(ErroresDeCaja.NothingToRecountCode);

        var fuente = new FuenteDeAprobacionDeDocumento(t.Db, t.K.Maestros(), new ServiceCollection().BuildServiceProvider());
        (await fuente.AlDevolverAsync(solicitud!, "Recuente", default)).IsSuccess.Should().BeTrue();
        await t.Db.SaveChangesAsync();
        t.V.Documento(r.Value.DifferenceDocument.DocumentPublicId).Status.Should().Be(DocumentStatus.Draft);

        var recuento = await t.Recontar().Handle(new RecountCashSessionCommand(ajena.PublicId, [Efectivo(t.V, 9_500m, "Apareció en el cajón")]), default);

        recuento.IsSuccess.Should().BeTrue(recuento.IsFailure ? $"{recuento.Error.Code}: {recuento.Error.Message}" : null);
        recuento.Value.DifferenceDocument!.Status.Should().Be(DocumentStatus.Confirmed, "dentro de la tolerancia se confirma sin volver a la política");
        var documento = t.V.Documento(r.Value.DifferenceDocument.DocumentPublicId);
        t.Db.CashDocumentLines.Where(l => l.DocumentId == documento.Id && !l.IsDeleted).Select(l => l.Amount).Should().Equal(500m);
        t.Db.CashCountLines.Single(l => !l.IsDeleted && l.PaymentMeansId == t.V.Efectivo.Id).CountedAmount.Should().Be(9_500m);
        await t._auditoria.Received().AnotarAsync(IngenIA365ERP.Application.Common.Audit.AuditEventTypes.InventoryCashSessionRecounted, Arg.Any<object>(),
            ajena.PublicId, Arg.Any<object?>(), Arg.Any<CancellationToken>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task El_retiro_de_cierre_saca_lo_contado_en_efectivo_menos_el_fondo()
    {
        var t = await CrearAsync();
        var sesion = t.SesionEn(t._caja2, 50_000m);
        var total = await t.VentaEnEfectivoAsync(sesion);

        var r = await t.Cerrar().Handle(new CloseCashSessionCommand(sesion.PublicId, [Efectivo(t.V, 50_000m + total)],
            new ClosingWithdrawalInput(CashMovementDestination.Safe)), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.DifferenceDocument.Should().BeNull();
        r.Value.ClosingWithdrawalDocument!.Status.Should().Be(DocumentStatus.Confirmed);
        var retiro = t.Db.CashMovementDetails.Single();
        (retiro.Kind, retiro.Amount, retiro.Destination).Should().Be((CashMovementKind.WithdrawalToSafe, total, CashMovementDestination.Safe));
        t.Db.CashCounts.Single().Status.Should().Be(DocumentStatus.Confirmed);
    }

    // ------------------------------------------------------------------------------------------------ movimientos --

    [Fact]
    public async Task Un_retiro_a_una_caja_sin_sesion_abierta_es_DestinationRegisterClosed()
    {
        var t = await CrearAsync();
        await t.VentaEnEfectivoAsync();
        var movimiento = await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToRegister, 1_000m, t._caja2.PublicId);

        var r = await t.ConfirmarAsync(movimiento);

        r.Error.Code.Should().Be(ErroresDeCaja.DestinationRegisterClosedCode);
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { cashRegisterCode = "CJ2" });
    }

    [Fact]
    public async Task Un_retiro_a_otra_caja_resta_en_el_origen_y_suma_en_la_sesion_abierta_de_la_destino()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();
        var destino = t.SesionEn(t._caja2, 0m, OtraCajera);
        var movimiento = await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToRegister, 1_000m, t._caja2.PublicId);

        var r = await t.ConfirmarAsync(movimiento);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        t.Db.CashMovementDetails.Single().DestinationCashSessionId.Should().Be(destino.Id);
        (await t.Sesiones().EsperadoAsync(t.V.Sesion, null, default)).Total.Should().Be(total - 1_000m);
        (await t.Sesiones().EsperadoAsync(destino, null, default)).Total.Should().Be(1_000m);
        t.V.MensajesDe(movimiento).Should().Contain(MovimientoDeCajaRegistradoV1.Type);
    }

    [Fact]
    public async Task Una_reclasificacion_entre_tarjetas_mueve_lo_esperado_del_datafono_del_pago_corregido()
    {
        // La Visa era una Mastercard pasada por el mismo datáfono: lo esperado de ese datáfono pasa de un medio al otro, así el lote
        // del datáfono cuadra en los dos (e2e T566, 2026-09-27: el origen iba sin datáfono y el cierre pedía motivo para dos
        // diferencias que no existían).
        var t = await CrearAsync();
        var datafono = new IngenIA365ERP.Domain.Entities.Core.Payments.CardTerminal { CardAcquirerId = t.V.Tarjeta.CardAcquirerId!.Value, Code = "TER01" };
        var master = new IngenIA365ERP.Domain.Entities.Core.Payments.PaymentMeans
        {
            Code = "MASTER", Name = "MASTER", Class = IngenIA365ERP.Domain.Enums.Core.PaymentMeansClass.CreditCard, DianPaymentMeansCode = "48",
            CardNetworkId = t.V.Tarjeta.CardNetworkId, CardAcquirerId = t.V.Tarjeta.CardAcquirerId, CountMethod = IngenIA365ERP.Domain.Enums.Core.CashCountMethod.VoucherTotal,
            OfferedAtAllPointsOfSale = true, OfferedInAllChannels = true, OfferedForAllDocumentTypes = true, ValidFrom = new DateOnly(2026, 1, 1),
        };
        t.Db.CardTerminals.Add(datafono);
        t.Db.PaymentMeans.Add(master);
        await t.Db.SaveChangesAsync();
        var venta = await t.V.VentaConfirmadaAsync(total => [new DocumentPaymentInput(t.V.Tarjeta.PublicId, total, AuthorizationCode: "123456", Last4: "4242",
            CardTerminalPublicId: datafono.PublicId, CashSessionPublicId: t.V.Sesion.PublicId)], lineas: t.V.Linea(t.V.P1, 1m));
        var pago = t.Db.DocumentPayments.Single(p => p.DocumentId == venta.Id);

        var r = await t.Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Cash,
            new CashMovementInput(t.TipoMovimiento, t.V.Sesion.PublicId, CashMovementKind.ReclassificationBetweenMeans, t.V.Tarjeta.PublicId, venta.AmountDue,
                "Era una Mastercard", TargetPaymentMeansPublicId: master.PublicId, ReclassifiedPaymentPublicId: pago.PublicId).ComoBorrador(t.TipoMovimiento)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        (await t.ConfirmarAsync(r.Value.PublicId)).IsSuccess.Should().BeTrue();

        var esperado = await t.Sesiones().EsperadoAsync(t.V.Sesion, null, default);
        var visa = esperado.Lines.Single(l => l.Medio.PaymentMeansId == t.V.Tarjeta.Id);
        var mc = esperado.Lines.Single(l => l.Medio.PaymentMeansId == master.Id);
        visa.Expected.Should().Be(0m);
        visa.Terminals.Single(d => d.CardTerminalId == datafono.Id).Expected.Should().Be(0m, "sale del datáfono del pago que corrige");
        mc.Expected.Should().Be(venta.AmountDue);
        mc.Terminals.Single(d => d.CardTerminalId == datafono.Id).Expected.Should().Be(venta.AmountDue, "y entra al mismo si no dice otro");
    }

    [Fact]
    public async Task Un_retiro_sobre_lo_esperado_es_ExceedsExpected()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();
        var movimiento = await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToSafe, total + 1m);

        var r = await t.ConfirmarAsync(movimiento);

        r.Error.Code.Should().Be(ErroresDeCaja.ExceedsExpectedCode);
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { paymentMeansCode = "EFECTIVO", expected = total });
    }

    [Fact]
    public async Task Un_movimiento_se_anula_con_la_sesion_abierta_y_con_la_sesion_cerrada_es_SessionClosed()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();
        var primero = await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToSafe, 500m);
        var segundo = await t.MovimientoAsync(t.V.Sesion, CashMovementKind.WithdrawalToSafe, 500m);
        (await t.ConfirmarAsync(primero)).IsSuccess.Should().BeTrue();
        (await t.ConfirmarAsync(segundo)).IsSuccess.Should().BeTrue();
        var anular = new VoidInventoryDocumentCommandHandler(t.Db, t.K.Actor, t.V.Compras.C.Reloj, t.K.Vista(), t.V.Confirmacion());

        var abierta = await anular.Handle(new VoidInventoryDocumentCommand(primero, DocumentClassGroup.Cash, "Se registró dos veces"), default);
        abierta.IsSuccess.Should().BeTrue(abierta.IsFailure ? $"{abierta.Error.Code}: {abierta.Error.Message}" : null);
        (await t.Sesiones().EsperadoAsync(t.V.Sesion, null, default)).Total.Should().Be(total - 500m, "lo anulado ya no cuenta");

        t.V.Sesion.Cerrar(KardexDePrueba.Usuario, DateTime.UtcNow);
        await t.Db.SaveChangesAsync();
        var cerrada = await anular.Handle(new VoidInventoryDocumentCommand(segundo, DocumentClassGroup.Cash, "Tarde"), default);

        cerrada.Error.Code.Should().Be(ErroresDeCaja.MovementSessionClosedCode);
    }

    // ------------------------------------------------------------------------------------------------ cierre del día --

    [Fact]
    public async Task El_dia_no_se_cierra_con_sesiones_abiertas_ni_dos_veces_y_al_reabrirlo_el_siguiente_es_la_version_2()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();
        var hoy = Catalog.CatalogoDePrueba.Hoy;

        var conAbiertas = await t.CerrarDia().Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, hoy), default);
        (await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total)]), default)).IsSuccess.Should().BeTrue();
        var primero = await t.CerrarDia().Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, hoy), default);
        var repetido = await t.CerrarDia().Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, hoy), default);
        var reabierto = await new ReopenDayCloseCommandHandler(t.Db, t.K.Actor, t.V.Compras.C.Reloj, t.K.Alcance, t._auditoria)
            .Handle(new ReopenDayCloseCommand(primero.Value.DayClosePublicId, "Faltó una venta"), default);
        var segundo = await t.CerrarDia().Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, hoy), default);

        conAbiertas.Error.Code.Should().Be(ErroresDeCaja.DayCloseSessionsOpenCode);
        primero.IsSuccess.Should().BeTrue(primero.IsFailure ? $"{primero.Error.Code}: {primero.Error.Message}" : null);
        (primero.Value.Version, primero.Value.Total).Should().Be(((short)1, total));
        repetido.Error.Code.Should().Be(ErroresDeCaja.DayCloseAlreadyClosedCode);
        reabierto.Value.Status.Should().Be(DayCloseStatus.Reopened);
        reabierto.Value.ReopenReason.Should().Be("Faltó una venta");
        segundo.Value.Version.Should().Be(2);

        var detalle = await new GetDayCloseQueryHandler(t.Db, t.K.Alcance).Handle(new GetDayCloseQuery(segundo.Value.DayClosePublicId), default);
        detalle.Value.Lines.Should().ContainSingle().Which.Should().Be(new DayCloseLineDto("EFECTIVO", total, total, 0m, 1));
        detalle.Value.Sessions.Select(s => s.CashSessionPublicId).Should().Equal(t.V.Sesion.PublicId);
        t.Db.DayCloseLines.Where(l => !l.IsDeleted).Select(l => l.DetailKey).Distinct()
            .Should().Equal(DayCloseLine.Clave(t.V.Efectivo.Id, null, null));
        t.V.MensajesDe(segundo.Value.DayClosePublicId).Should().BeEmpty("el cierre del día no emite mensajes");
    }

    // T631: sin documentTypePublicId, la ruta de movimientos toma el tipo por defecto de la clase CashMovement.
    [Fact]
    public async Task El_tipo_de_movimiento_por_defecto_es_el_primero_activo_de_la_clase()
    {
        var t = await CrearAsync();
        var handler = new GetDefaultCashMovementTypeQueryHandler(t.Db);

        (await handler.Handle(new GetDefaultCashMovementTypeQuery(), default)).Value.Should().Be(t.K.Tipo("CJ").PublicId);

        t.K.Tipo("CJ").IsActive = false;
        await t.Db.SaveChangesAsync();
        (await handler.Handle(new GetDefaultCashMovementTypeQuery(), default)).Value.Should().BeNull("sin tipo activo, el guardado responde el error del tipo");
    }

    // ------------------------------------------------------------------------------------------------ arqueo sencillo --

    [Fact]
    public async Task Con_el_arqueo_sencillo_dentro_de_la_tolerancia_no_pide_motivo_y_el_faltante_va_al_gasto()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();

        var r = await t.CerrarSencillo().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total - 500m)]), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
        r.Value.Lines.Single(l => l.PaymentMeansCode == "EFECTIVO").Treatment.Should().Be(CashDifferenceTreatment.ShortageToExpense,
            "con el arqueo sencillo el faltante va al gasto");
    }

    [Fact]
    public async Task Con_el_arqueo_sencillo_fuera_de_la_tolerancia_sigue_pidiendo_motivo()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();

        var r = await t.CerrarSencillo().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total - 5_000m)]), default);

        r.Error.Code.Should().Be(ErroresDeCaja.ReasonRequiredCode);
    }

    // 2026-11-25 (pedido del dueño: «ver el cierre del día bien detallado»): cada sesión del cierre trae lo que dio su arqueo y los medios que difieren
    // con su tratamiento, para saber a quién corresponde cada descuadre sin abrir sesión por sesión.
    [Fact]
    public async Task El_detalle_del_cierre_del_dia_trae_por_sesion_su_arqueo_y_los_medios_que_difieren()
    {
        var t = await CrearAsync();
        var total = await t.VentaEnEfectivoAsync();
        var cerrada = await t.CerrarSencillo().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, total - 500m)]), default);
        cerrada.IsSuccess.Should().BeTrue(cerrada.IsFailure ? $"{cerrada.Error.Code}: {cerrada.Error.Message}" : null);
        var cierre = await t.CerrarDia().Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, Catalog.CatalogoDePrueba.Hoy), default);
        cierre.IsSuccess.Should().BeTrue(cierre.IsFailure ? $"{cierre.Error.Code}: {cierre.Error.Message}" : null);

        var detalle = await new GetDayCloseQueryHandler(t.Db, t.K.Alcance).Handle(new GetDayCloseQuery(cierre.Value.DayClosePublicId), default);

        var sesion = detalle.Value.Sessions.Should().ContainSingle().Subject;
        sesion.OpeningBase.Should().Be(t.V.Sesion.OpeningBase);
        sesion.Difference.Should().Be(-500m);
        (sesion.Counted - sesion.Expected).Should().Be(-500m);
        sesion.LinkedToCollection.Should().BeFalse();
        sesion.Differences.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { PaymentMeansCode = "EFECTIVO", Difference = -500m, Treatment = CashDifferenceTreatment.ShortageToExpense });
    }

    // ------------------------------------------------------------------------------------------- caja única (2026-11-25) --

    private GetLinkedCashSessionQueryHandler Vinculada() => new(Db, Sesiones());

    [Fact]
    public async Task La_caja_unica_cierra_la_sesion_vinculada_de_otro_cajero_aunque_quien_arquea_no_vea_las_ajenas()
    {
        var t = await CrearAsync();
        var vinculada = t.SesionEn(t._caja2, 0m, OtraCajera, vinculada: Guid.NewGuid());
        t.K.Permisos.HasPermissionAsync(SesionesDeCaja.PermisoVerTodas, Arg.Any<CancellationToken>()).Returns(false);

        var porSuCuenta = await t.Cerrar().Handle(new CloseCashSessionCommand(vinculada.PublicId, [Efectivo(t.V, 0m)]), default);
        var dto = (await t.Vinculada().Handle(new GetLinkedCashSessionQuery(vinculada.LinkedSessionPublicId!.Value), default)).Value!;
        var desdeRecaudo = await t.Cerrar().Handle(CajaVinculada.Cierre(dto, Guid.NewGuid()), default);

        porSuCuenta.Error.Code.Should().Be(ErroresDeCaja.SessionNotFoundCode, "sin ViewAll la sesión ajena no se ve por la ruta del punto de venta");
        desdeRecaudo.IsSuccess.Should().BeTrue(desdeRecaudo.IsFailure ? $"{desdeRecaudo.Error.Code}: {desdeRecaudo.Error.Message}" : null);
    }

    [Fact]
    public async Task Cerrada_la_sesion_vinculada_informa_lo_que_conto_su_cierre_y_no_su_esperado_en_vivo()
    {
        var t = await CrearAsync();
        var recaudo = Guid.NewGuid();
        var vinculada = t.SesionEn(t._caja2, 0m, vinculada: recaudo);
        vinculada.Cerrar(KardexDePrueba.Usuario, new DateTime(2026, 9, 25, 23, 0, 0, DateTimeKind.Utc));
        t.Db.CashCounts.Add(new CashCount
        {
            CashSessionId = vinculada.Id, CountedAt = DateTime.UtcNow, CountedByUserId = KardexDePrueba.Usuario, TotalExpected = 50_000m, TotalCounted = 50_000m,
            Lines = [new CashCountLine { PaymentMeansId = t.V.Efectivo.Id, CountMethod = Domain.Enums.Core.CashCountMethod.PhysicalCount, ExpectedAmount = 50_000m,
                CountedAmount = 50_000m }],
        });
        await t.Db.SaveChangesAsync();

        var dto = (await t.Vinculada().Handle(new GetLinkedCashSessionQuery(recaudo), default)).Value!;

        dto.Open.Should().BeFalse();
        dto.Means.Should().ContainSingle(m => m.PaymentMeansId == t.V.Efectivo.Id).Which.Expected.Should().Be(50_000m,
            "lo que entra a Recaudo es lo contado al cerrar: después del retiro a la caja fuerte el esperado del efectivo es cero");
    }

    [Fact]
    public async Task El_cierre_del_dia_no_remesa_el_efectivo_que_la_caja_unica_ya_paso_a_Recaudo()
    {
        var t = await CrearAsync();
        var hoy = Catalog.CatalogoDePrueba.Hoy;
        (await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, [Efectivo(t.V, 0m)]), default)).IsSuccess.Should().BeTrue();
        var vinculada = t.SesionEn(t._caja2, 50_000m, vinculada: Guid.NewGuid());
        var dto = (await t.Vinculada().Handle(new GetLinkedCashSessionQuery(vinculada.LinkedSessionPublicId!.Value), default)).Value!;
        (await t.Cerrar().Handle(CajaVinculada.Cierre(dto, Guid.NewGuid()), default)).IsSuccess.Should().BeTrue();

        var cierre = await new ExecuteDayCloseCommandHandler(t.Db, t.K.Actor, t.V.Compras.C.Reloj, t.K.Alcance, t._cerrojo, t._auditoria,
                Substitute.For<IngenIA365ERP.Application.Common.Integration.Treasury.IRemesasDeTesoreria>())
            .Handle(new ExecuteDayCloseCommand(t.V.Punto.PublicId, hoy) { Remittance = new RemesaDelCierreDelDia(Guid.NewGuid(), 50_000m) }, default);

        cierre.Error.Code.Should().Be(ErroresDeCaja.DayCloseRemittanceOverCashCode, "el efectivo de la sesión vinculada ya está en Recaudo");
    }

    // ------------------------------------------------------------------------------------ cajeros asignados --
    // 2026-11-25 (pedido del dueño): con Caja.SoloCajerosAsignados (encendido por defecto) una caja con cajeros sólo la abren ellos y los de la caja de
    // Recaudo a la que esté unida; sin ninguno, cualquiera del punto.

    private void Usuarios()
    {
        Db.Users.AddRange(
            new IngenIA365ERP.Domain.Entities.Security.User { Id = KardexDePrueba.Usuario, Username = "cajera.uno" },
            new IngenIA365ERP.Domain.Entities.Security.User { Id = OtraCajera, Username = "cajera.dos" });
        Db.SaveChanges();
    }

    private void CajeroDe(CashRegister caja, int usuario, DateOnly? hasta = null)
    {
        Db.CashRegisterCashiers.Add(new CashRegisterCashier { CashRegisterId = caja.Id, UserId = usuario, ValidFrom = Catalog.CatalogoDePrueba.Hoy.AddDays(-30), ValidTo = hasta });
        Db.SaveChanges();
    }

    /// <summary>Una caja de Recaudo unida a la caja del punto de venta, con su cajero.</summary>
    private void UnidaARecaudo(CashRegister caja, int cajero)
    {
        var deRecaudo = new IngenIA365ERP.Domain.Entities.Receipts.Configuration.CashBox
        {
            CollectionPointId = 1, Code = "CAJA1", Name = "Caja de Recaudo", ValidFrom = Catalog.CatalogoDePrueba.Hoy.AddYears(-1), PosCashRegisterPublicId = caja.PublicId,
        };
        Db.CashBoxes.Add(deRecaudo);
        Db.SaveChanges();
        Db.CashBoxCashiers.Add(new IngenIA365ERP.Domain.Entities.Receipts.Configuration.CashBoxCashier
        {
            CashBoxId = deRecaudo.Id, UserId = cajero, ValidFrom = Catalog.CatalogoDePrueba.Hoy.AddDays(-30),
        });
        Db.SaveChanges();
    }

    [Fact]
    public async Task Una_caja_sin_cajeros_asignados_la_abre_cualquiera_del_punto()
    {
        var t = await CrearAsync();
        t.Usuarios();

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
    }

    [Fact]
    public async Task Una_caja_con_cajeros_asignados_no_la_abre_otra_persona_y_dice_quienes_la_abren()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.CajeroDe(t._caja2, OtraCajera);

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.Error.Code.Should().Be(ErroresDeCaja.CashierNotAssignedCode);
        r.Error.Message.Should().Contain("cajera.dos").And.Contain("CJ2");
        t.Db.CashSessions.Should().NotContain(s => s.CashRegisterId == t._caja2.Id, "no se abre nada");
    }

    [Fact]
    public async Task El_cajero_asignado_abre_su_caja()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.CajeroDe(t._caja2, OtraCajera);
        t.CajeroDe(t._caja2, KardexDePrueba.Usuario);

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
    }

    [Fact]
    public async Task Un_cajero_retirado_ya_no_cuenta()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.CajeroDe(t._caja2, OtraCajera, hasta: Catalog.CatalogoDePrueba.Hoy.AddDays(-1));

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.IsSuccess.Should().BeTrue("sin cajeros vigentes la caja vuelve a estar libre");
    }

    [Fact]
    public async Task La_caja_unida_a_Recaudo_la_abren_tambien_los_cajeros_de_esa_caja()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.CajeroDe(t._caja2, OtraCajera);
        t.UnidaARecaudo(t._caja2, KardexDePrueba.Usuario);

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : null);
    }

    [Fact]
    public async Task La_caja_unida_a_Recaudo_no_la_abre_quien_no_es_cajero_de_ninguna_de_las_dos()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.UnidaARecaudo(t._caja2, OtraCajera);

        var r = await t.AbrirSencillo().Handle(new OpenCashSessionCommand(t._caja2.PublicId, 0m), default);

        r.Error.Code.Should().Be(ErroresDeCaja.CashierNotAssignedCode);
    }

    [Fact]
    public void Con_el_parametro_apagado_cualquiera_abre_y_sin_cajeros_tambien()
    {
        var cajeros = new[] { new CajeroDeLaCaja(OtraCajera, Guid.NewGuid(), "Cajera Dos", null) };

        CajerosDeLaCaja.PuedeAbrir(soloAsignados: false, cajeros, KardexDePrueba.Usuario).Should().BeTrue();
        CajerosDeLaCaja.PuedeAbrir(soloAsignados: true, [], KardexDePrueba.Usuario).Should().BeTrue();
        CajerosDeLaCaja.PuedeAbrir(soloAsignados: true, cajeros, KardexDePrueba.Usuario).Should().BeFalse();
        CajerosDeLaCaja.PuedeAbrir(soloAsignados: true, cajeros, OtraCajera).Should().BeTrue();
    }

    [Fact]
    public async Task Asignar_y_retirar_un_cajero_y_la_lista_trae_de_solo_lectura_los_de_la_caja_de_Recaudo()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.UnidaARecaudo(t._caja2, KardexDePrueba.Usuario);
        var dos = t.Db.Users.Single(u => u.Id == OtraCajera).PublicId;
        var hoy = Catalog.CatalogoDePrueba.Hoy;

        var asignado = await new AssignCashRegisterCashierCommandHandler(t.Db, t.K.Alcance, t.V.Compras.C.Reloj)
            .Handle(new AssignCashRegisterCashierCommand(t.V.Punto.PublicId, t._caja2.PublicId, dos), default);
        asignado.IsSuccess.Should().BeTrue(asignado.IsFailure ? $"{asignado.Error.Code}: {asignado.Error.Message}" : null);
        asignado.Value.Should().ContainSingle(c => c.UserPublicId == dos && c.Current && c.CollectionBoxCode == null && c.ValidFrom == hoy);
        asignado.Value.Should().ContainSingle(c => c.UserName == "cajera.uno" && c.CollectionBoxCode == "CAJA1", "el de Recaudo se ve, de sólo lectura");

        var repetido = await new AssignCashRegisterCashierCommandHandler(t.Db, t.K.Alcance, t.V.Compras.C.Reloj)
            .Handle(new AssignCashRegisterCashierCommand(t.V.Punto.PublicId, t._caja2.PublicId, dos), default);
        repetido.Error.Code.Should().Be(ErroresDePuntoDeVenta.CashierOverlapsCode);

        var retirado = await new RetireCashRegisterCashierCommandHandler(t.Db, t.K.Alcance, t.V.Compras.C.Reloj)
            .Handle(new RetireCashRegisterCashierCommand(t.V.Punto.PublicId, t._caja2.PublicId, dos), default);
        retirado.IsSuccess.Should().BeTrue(retirado.IsFailure ? $"{retirado.Error.Code}: {retirado.Error.Message}" : null);
        retirado.Value.Should().NotContain(c => c.UserPublicId == dos && c.Current, "asignado hoy y retirado hoy no llega a regir");
        t.Db.CashRegisterCashiers.IgnoreQueryFilters().Should().ContainSingle(c => c.UserId == OtraCajera && c.IsDeleted, "nada se borra: queda de baja");
    }

    [Fact]
    public async Task La_lista_de_cajas_dice_quien_las_abre_y_si_quien_pregunta_puede()
    {
        var t = await CrearAsync();
        t.Usuarios();
        t.CajeroDe(t._caja2, OtraCajera);

        var r = await new ListCashRegistersQueryHandler(t.Db, t.K.Alcance, t.K.Actor,
                new IngenIA365ERP.Application.Receipts.Common.ParametrosDeCaja(t.K.Lector()), t.V.Compras.C.Reloj)
            .Handle(new ListCashRegistersQuery(t.V.Punto.PublicId), default);

        r.IsSuccess.Should().BeTrue();
        var caja2 = r.Value.Single(c => c.CashRegisterPublicId == t._caja2.PublicId);
        caja2.CashierNames.Should().Equal("cajera.dos");
        caja2.OpenableByMe.Should().BeFalse();
        r.Value.Where(c => c.CashRegisterPublicId != t._caja2.PublicId).Should().OnlyContain(c => c.OpenableByMe == true && c.CashierNames!.Count == 0);
    }

    // ------------------------------------------------------------------------------------- 2026-11-26, factura de oficina --

    [Fact]
    public async Task Lo_vendido_de_la_caja_cuenta_la_factura_de_oficina_cobrada_en_ella()
    {
        var t = await CrearAsync();
        var venta = await t.V.VentaConfirmadaAsync(lineas: t.V.Linea(t.V.P1, 1m));
        venta.CashSessionId.Should().BeNull("la factura de oficina lleva la caja sólo en sus pagos");

        var dto = await t.Sesiones().DtoAsync(t.V.Sesion, default);

        dto.SalesCount.Should().Be(1);
        dto.SalesTotal.Should().Be(venta.Total);
        dto.CreditSalesTotal.Should().Be(0m);
    }

    [Fact]
    public async Task No_cierra_con_una_factura_de_oficina_en_borrador_cobrada_en_la_caja()
    {
        var t = await CrearAsync();
        var borrador = await t.V.GuardarAsync(t.V.Venta(lineas: t.V.Linea(t.V.P1, 1m)));
        var total = t.V.Documento(borrador.Value.PublicId).AmountDue;
        var conPago = await t.V.GuardarAsync(t.V.Venta(pagos: [t.V.Pago(t.V.Efectivo, total)], lineas: t.V.Linea(t.V.P1, 1m)), borrador.Value.PublicId);
        conPago.IsSuccess.Should().BeTrue(conPago.IsFailure ? $"{conPago.Error.Code}: {conPago.Error.Message}" : null);

        var r = await t.Cerrar().Handle(new CloseCashSessionCommand(t.V.Sesion.PublicId, []), default);

        r.Error.Code.Should().Be(ErroresDeCaja.HasOpenDraftsCode);
    }
}
