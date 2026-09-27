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
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pos;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
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

    private OpenCashSessionCommandHandler Abrir() => new(Db, K.Actor, V.Compras.C.Reloj, K.Alcance, K.Lector(), _cerrojo, new GuardiaDeEmisionFiscal(K.Lector()),
        Sesiones(), V.Confirmacion(), _auditoria);

    private CloseCashSessionCommandHandler Cerrar() => new(Db, K.Actor, V.Compras.C.Reloj, Sesiones(), Arqueos(), V.Toque, V.Confirmacion(), _auditoria);

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

    private CashSession SesionEn(CashRegister caja, decimal @base, int cajero = KardexDePrueba.Usuario)
    {
        var sesion = new CashSession
        {
            CashRegisterId = caja.Id, PointOfSaleId = caja.PointOfSaleId, CashierUserId = cajero, CashierName = cajero == KardexDePrueba.Usuario ? "Cajera Uno" : "Cajera Dos",
            OperatingDate = Catalog.CatalogoDePrueba.Hoy, OpenedAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc),
            LastActivityAt = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc), OpeningBase = @base, ShortageTreatment = "Gasto",
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
        t.K.Parametro(ParametrosDeInventario.CajaTratamientoFaltante, "CargoAlCajero", ParameterScopeKind.PointOfSale, t.V.Punto.Id);

        var r = await t.Abrir().Handle(new OpenCashSessionCommand(t._caja2.PublicId), default);

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
}
