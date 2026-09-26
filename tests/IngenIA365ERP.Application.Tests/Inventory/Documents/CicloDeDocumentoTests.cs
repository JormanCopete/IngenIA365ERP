using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T108 (T17; FR-005, FR-006; contracts/api.md §9.3–§9.6): el ciclo común del documento con una estrategia de
/// clase falsa y los puertos de plataforma sustituidos. Guardar un borrador (grupo de la ruta, tope de líneas, avisos que
/// no bloquean, unidades y decimales), descartar, confirmar en el orden del flujo canónico (reglas → aprobación → cerrojo
/// → efecto → número → mensajes → un guardado; con niveles, <c>PendingApproval</c> sin número ni cerrojo) y anular. Lo
/// transaccional (cerrojo real, números sin huecos entre confirmaciones simultáneas) es de las e2e (T14).
/// </summary>
public class CicloDeDocumentoTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);
    private static readonly DateTime Ahora = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);
    private const int Usuario = 7;

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMaestrosDelDocumento _maestros = Substitute.For<IMaestrosDelDocumento>();
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly IMotorDeAprobaciones _motor = Substitute.For<IMotorDeAprobaciones>();
    private readonly ICerrojoDeInventario _cerrojo = Substitute.For<ICerrojoDeInventario>();
    private readonly List<string> _diario = [];
    private readonly EfectoFalso _efecto;
    private int _guardados;

    private readonly Branch _sucursal;
    private readonly BodegaDelDocumento _bodega;
    private readonly ProductoDelDocumento _producto = new(10, Guid.NewGuid(), "P001", "Arroz", true, ProductStatus.Active);
    private readonly UnidadDelDocumento _unidad = new(100, Guid.NewGuid(), "UN", 1m, 0, 0);
    private readonly UnidadDelDocumento _caja = new(101, Guid.NewGuid(), "CAJA12", 12m, 0, 0);

    public CicloDeDocumentoTests()
    {
        _sucursal = new Branch { Name = "Principal" };
        _db.Branches.Add(_sucursal);
        _db.SaveChanges();
        _bodega = new BodegaDelDocumento(1, Guid.NewGuid(), "PRIN", "Principal", _sucursal.Id, false, true, false);
        _db.SavedChanges += (_, _) => _guardados++;

        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(Ahora);
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, Usuario, Guid.NewGuid(), Guid.NewGuid(),
            "bodeguero@coop.co", "bodeguero@coop.co", ExecutionChannel.Web, "POST /api/inventory/adjustments", "10.0.0.1", null));
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);

        _maestros.BodegasAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<Guid>>().Contains(_bodega.PublicId) ? [_bodega] : Array.Empty<BodegaDelDocumento>());
        _maestros.BodegasPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => c.Arg<IReadOnlyCollection<int>>().Contains(_bodega.Id) ? [_bodega] : Array.Empty<BodegaDelDocumento>());
        _maestros.ProductosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([_producto]);
        _maestros.ProductosPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([_producto]);
        _maestros.UnidadAsync(_producto.Id, _unidad.PublicId, Arg.Any<CancellationToken>()).Returns(_unidad);
        _maestros.UnidadAsync(_producto.Id, _caja.PublicId, Arg.Any<CancellationToken>()).Returns(_caja);
        _maestros.UnidadesPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([_unidad, _caja]);
        _maestros.UbicacionesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        _maestros.UbicacionesPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([]);
        _maestros.CausasDeAjustePorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([]);
        _maestros.CorteAsync(Arg.Any<CancellationToken>()).Returns(CorteDeInventario.SinCorte);

        SinNiveles();
        _cerrojo.When(c => c.BloquearAsync(Arg.Any<PedidoDeCerrojo>(), Arg.Any<CancellationToken>())).Do(_ => _diario.Add("Cerrojo"));
        _cerrojo.When(c => c.BloquearNumeracionAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())).Do(_ => _diario.Add("Numeracion"));

        _efecto = new EfectoFalso(DocumentClass.PositiveAdjustment, _diario);
    }

    // ---------------------------------------------------------------------------------------------- piezas --

    private void SinNiveles() =>
        _motor.EvaluarAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(_ =>
        {
            _diario.Add("Aprobacion");
            return Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.SinAprobacion, [], null, false, false), null));
        });

    private void ConUnNivel()
    {
        var niveles = new List<NivelDeAprobacion> { new(1, 0m, "Inventory.Adjustments.Approve") };
        _motor.EvaluarAsync(default!, default, default, default, default, default).ReturnsForAnyArgs(_ =>
        {
            _diario.Add("Aprobacion");
            return Result.Success(new EvaluacionConPolitica(new EvaluacionDeAprobacion(ResultadoDeEvaluacion.ConNiveles, niveles, null, false, false), 1));
        });
        _motor.SolicitarAsync(default!, default).ReturnsForAnyArgs(c =>
        {
            var solicitud = new ApprovalRequest { SourcePublicId = c.Arg<SolicitudDeAprobacion>().SourcePublicId, CurrentLevel = 1 };
            solicitud.SellarNiveles(niveles);
            return Result.Success<ApprovalRequest?>(solicitud);
        });
        _diario.Clear(); // reconfigurar el sustituto invoca una vez la configuración anterior
    }

    private VistaDeDocumentos Vista() => new(_db, _maestros, _permisos, _alcance);

    private EfectosDeClase Efectos() => new([_efecto]);

    private ConfirmacionDeDocumento Confirmacion() => new(
        _db, _maestros, _actor, _reloj, Efectos(), _motor, _cerrojo, new Numerador(_db, _cerrojo),
        new EmisorDeMensajes(_db, _actor, _reloj), new LectorDeParametros(_db), Vista(), [], []);

    private SaveInventoryDraftCommandHandler Guardar() => new(_db, _maestros, _alcance, _actor, _reloj, Efectos(), Vista());

    private InventoryDocumentType Tipo(DocumentClass clase = DocumentClass.PositiveAdjustment, string codigo = "AJP", bool motivo = false)
    {
        var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, RequiresReason = motivo, IsActive = true };
        tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = codigo[..2], NextValue = 1, ValidFrom = new DateOnly(2026, 1, 1) });
        _db.InventoryDocumentTypes.Add(tipo);
        _db.SaveChanges();
        return tipo;
    }

    private SaveInventoryDraftRequest Borrador(InventoryDocumentType tipo, params SaveInventoryDraftLine[] lineas) => new(
        tipo.PublicId, null, _bodega.PublicId, null, null, null, null, null, null, null, null, null, null,
        lineas.Length == 0 ? [new SaveInventoryDraftLine(null, _producto.PublicId, _unidad.PublicId, 5m, UnitCost: 1000m)] : lineas);

    private async Task<InventoryDocumentDto> BorradorGuardadoAsync(InventoryDocumentType tipo)
    {
        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo)), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        return r.Value;
    }

    private InventoryDocument Confirmado(InventoryDocumentType tipo, DocumentClass? clase = null)
    {
        var documento = new InventoryDocument
        {
            Class = clase ?? tipo.Class, DocumentTypeId = tipo.Id, OperationDate = new DateOnly(2026, 9, 1), BranchId = _sucursal.Id,
            WarehouseId = _bodega.Id, CreatedByUserId = 3, Prefix = "AJ", Number = 40, CostTotal = 5000m,
        };
        documento.Lines.Add(new InventoryDocumentLine { Document = documento, LineNumber = 1, ProductId = _producto.Id, UnitId = _unidad.Id, Quantity = 5, QuantityBase = 5, UnitCost = 1000m, TotalCost = 5000m });
        documento.Confirmar(3, Ahora.AddDays(-20));
        _db.InventoryDocuments.Add(documento);
        _db.SaveChanges();
        return documento;
    }

    private static string Codigo<T>(Result<T> r) => r.IsFailure ? r.Error.Code : "(éxito)";

    // --------------------------------------------------------------------------------------- guardar --

    [Fact]
    public async Task Guardar_crea_el_borrador_sin_numero_convierte_la_unidad_y_calcula_totales()
    {
        var tipo = Tipo();
        _guardados = 0;
        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            Borrador(tipo, new SaveInventoryDraftLine(null, _producto.PublicId, _caja.PublicId, 3m, UnitCost: 100m))), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(DocumentStatus.Draft);
        r.Value.Number.Should().BeNull("el borrador no consume número");
        r.Value.OperationDate.Should().Be(Hoy, "sin fecha, la de hoy en Colombia");
        var linea = r.Value.Lines.Single();
        linea.Factor.Should().Be(12m);
        linea.QuantityBase.Should().Be(36m, "3 cajas de 12 son 36 unidades base");
        linea.RoundingQuantity.Should().Be(0m);
        r.Value.Totals.CostTotal.Should().Be(3600m);
        _guardados.Should().Be(1);
    }

    [Fact]
    public async Task Un_tipo_de_otro_grupo_no_se_guarda_por_esta_ruta()
    {
        var recepcion = Tipo(DocumentClass.PurchaseReceipt, "REC");

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(recepcion)), default);

        Codigo(r).Should().Be("Inventory.Document.TypeNotForRoute");
    }

    [Fact]
    public async Task Mas_de_cuatro_mil_lineas_se_rechazan()
    {
        var tipo = Tipo();
        var lineas = Enumerable.Range(0, InventoryDocument.MaxLineas + 1)
            .Select(_ => new SaveInventoryDraftLine(null, _producto.PublicId, _unidad.PublicId, 1m)).ToArray();

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo, lineas)), default);

        Codigo(r).Should().Be("Inventory.Document.TooManyLines");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { max = 4000 });
    }

    [Fact]
    public async Task Lo_que_impediria_confirmar_vuelve_como_aviso_sin_bloquear_el_guardado()
    {
        var tipo = Tipo(motivo: true);
        _maestros.BodegasAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([_bodega with { Activa = false }]);

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo)), default);

        r.IsSuccess.Should().BeTrue("los avisos no detienen el guardado");
        r.Value.Warnings.Select(w => w.Code).Should().Contain(["Inventory.Document.FieldRequired", "Inventory.Warehouse.NotActive"]);
        r.Value.Warnings.Should().Contain(w => w.Code == "Inventory.Document.FieldRequired")
            .Which.Data.Should().BeEquivalentTo(new { field = "reason" });
        r.Value.Status.Should().Be(DocumentStatus.Draft);
    }

    [Fact]
    public async Task La_estrategia_de_la_clase_suma_sus_avisos()
    {
        var tipo = Tipo();
        _efecto.Avisos.Add(InventoryErrors.StockInsufficient([new InventoryErrors.LineaSinExistencia(1, _producto.PublicId, "P001", _bodega.PublicId, null, 5, 2)]));

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo)), default);

        r.Value.Warnings.Select(w => w.Code).Should().Contain("Inventory.Stock.Insufficient");
    }

    [Fact]
    public async Task Una_unidad_que_no_es_del_producto_se_rechaza()
    {
        var tipo = Tipo();

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            Borrador(tipo, new SaveInventoryDraftLine(null, _producto.PublicId, Guid.NewGuid(), 1m))), default);

        Codigo(r).Should().Be("Inventory.Unit.NotForProduct");
    }

    [Fact]
    public async Task Una_cantidad_con_mas_decimales_de_los_que_admite_la_unidad_se_rechaza()
    {
        var tipo = Tipo();

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments,
            Borrador(tipo, new SaveInventoryDraftLine(null, _producto.PublicId, _unidad.PublicId, 1.5m))), default);

        Codigo(r).Should().Be("Inventory.Unit.DecimalsNotAllowed");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { lineNumber = 1, unitCode = "UN", allowedDecimals = 0 },
            o => o.ExcludingMissingMembers());
    }

    [Fact]
    public async Task Una_bodega_fuera_del_alcance_es_el_mismo_404_que_una_inexistente()
    {
        var tipo = Tipo();
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Vacio);

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo)), default);

        Codigo(r).Should().Be("Inventory.Warehouse.NotFound");
    }

    [Fact]
    public async Task Reemplazar_conserva_la_linea_con_su_PublicId_y_da_de_baja_las_que_faltan()
    {
        var tipo = Tipo();
        var creado = await BorradorGuardadoAsync(tipo);
        var conservada = creado.Lines.Single().LinePublicId;

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(creado.PublicId, DocumentClassGroup.Adjustments, Borrador(tipo,
            new SaveInventoryDraftLine(null, _producto.PublicId, _unidad.PublicId, 2m),
            new SaveInventoryDraftLine(conservada, _producto.PublicId, _unidad.PublicId, 9m))), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Lines.Should().HaveCount(2);
        r.Value.Lines.Single(l => l.LinePublicId == conservada).Quantity.Should().Be(9m);
        r.Value.Lines.Single(l => l.LinePublicId == conservada).LineNumber.Should().Be(2);
    }

    [Fact]
    public async Task Un_PUT_sobre_un_documento_que_no_es_borrador_se_rechaza()
    {
        var tipo = Tipo();
        var confirmado = Confirmado(tipo);

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(confirmado.PublicId, DocumentClassGroup.Adjustments, Borrador(tipo)), default);

        Codigo(r).Should().Be("Inventory.Document.NotDraft");
    }

    // -------------------------------------------------------------------------------------- descartar --

    [Fact]
    public void Descartar_exige_motivo()
    {
        new DiscardInventoryDraftCommandValidator().Validate(new DiscardInventoryDraftCommand(Guid.NewGuid(), DocumentClassGroup.Adjustments, " "))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Descartar_deja_el_borrador_descartado_con_quien_y_por_que_sin_consumir_numero()
    {
        var tipo = Tipo();
        var borrador = await BorradorGuardadoAsync(tipo);

        var r = await new DiscardInventoryDraftCommandHandler(_db, _actor, _reloj, Vista())
            .Handle(new DiscardInventoryDraftCommand(borrador.PublicId, DocumentClassGroup.Adjustments, "Digitado dos veces"), default);

        r.IsSuccess.Should().BeTrue();
        var guardado = await _db.InventoryDocuments.SingleAsync(d => d.PublicId == borrador.PublicId);
        guardado.Status.Should().Be(DocumentStatus.Discarded);
        guardado.DiscardReason.Should().Be("Digitado dos veces");
        guardado.DiscardedByUserId.Should().Be(Usuario);
        guardado.Number.Should().BeNull();
        (await _db.DocumentSequences.SingleAsync()).NextValue.Should().Be(1, "descartar no consume número");
    }

    [Fact]
    public async Task Descartar_un_confirmado_se_rechaza()
    {
        var confirmado = Confirmado(Tipo());

        var r = await new DiscardInventoryDraftCommandHandler(_db, _actor, _reloj, Vista())
            .Handle(new DiscardInventoryDraftCommand(confirmado.PublicId, DocumentClassGroup.Adjustments, "ya no"), default);

        r.Error.Code.Should().Be("Inventory.Document.NotDraft");
    }

    // -------------------------------------------------------------------------------------- confirmar --

    [Fact]
    public async Task Confirmar_sigue_el_orden_canonico_numera_y_guarda_una_sola_vez()
    {
        var tipo = Tipo();
        var borrador = await BorradorGuardadoAsync(tipo);
        _guardados = 0;

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Adjustments), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.Number.Should().Be(1);
        r.Value.DisplayNumber.Should().Be("AJ1");
        r.Value.PostingMode.Should().Be(PostingMode.Online, "sin parámetro, el defecto seguro EnLinea");
        r.Value.Prevalidation!.Outcome.Should().Be(PrevalidationOutcome.NotApplicable, "antes de I2 no hay validación previa");
        _diario.Should().Equal("Validar", "Aprobacion", "Cerrojo", "Aplicar", "Numeracion", "Mensajes");
        _guardados.Should().Be(1, "un solo SaveChanges con todo");

        var documento = await _db.InventoryDocuments.SingleAsync(d => d.PublicId == borrador.PublicId);
        documento.ConfirmedByUserId.Should().Be(Usuario);
    }

    [Fact]
    public async Task Con_niveles_queda_en_aprobacion_sin_numero_y_sin_tocar_cerrojo_ni_numerador()
    {
        var tipo = Tipo();
        var borrador = await BorradorGuardadoAsync(tipo);
        ConUnNivel();

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Adjustments), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Status.Should().Be(DocumentStatus.PendingApproval);
        r.Value.Number.Should().BeNull();
        r.Value.Approval!.Levels.Should().ContainSingle().Which.PermissionCode.Should().Be("Inventory.Adjustments.Approve");
        _diario.Should().Equal("Validar", "Aprobacion");
        await _cerrojo.DidNotReceiveWithAnyArgs().BloquearAsync(default!, default);
        await _cerrojo.DidNotReceiveWithAnyArgs().BloquearNumeracionAsync(default, default);
        await _motor.Received(1).SolicitarAsync(Arg.Is<SolicitudDeAprobacion>(s =>
            s.SourceType == ApprovalSourceTypes.InventoryDocument && s.SourcePublicId == borrador.PublicId && s.Amount == 5000m
            && s.PermisoLimitado == "Inventory.Adjustments.Confirm"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task La_ultima_aprobacion_reentra_y_confirma_con_numero_sin_volver_a_pedir_aprobacion()
    {
        var tipo = Tipo();
        var borrador = await BorradorGuardadoAsync(tipo);
        ConUnNivel();
        await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Adjustments), default);
        _diario.Clear();

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, null, PorAprobacion: true), default);

        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.Number.Should().Be(1);
        _diario.Should().NotContain("Aprobacion");
    }

    [Fact]
    public async Task Si_las_reglas_de_la_clase_fallan_no_se_pide_aprobacion_ni_se_bloquea()
    {
        var tipo = Tipo();
        var borrador = await BorradorGuardadoAsync(tipo);
        _efecto.FallaAlValidar = InventoryErrors.ProductBlocked(1, "P001");

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Adjustments), default);

        Codigo(r).Should().Be("Inventory.Product.Blocked");
        _diario.Should().Equal("Validar");
    }

    [Fact]
    public async Task Las_reglas_comunes_detienen_la_confirmacion_con_el_codigo_del_aviso()
    {
        var tipo = Tipo(motivo: true);
        var borrador = await BorradorGuardadoAsync(tipo);

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Adjustments), default);

        Codigo(r).Should().Be("Inventory.Document.FieldRequired");
        _diario.Should().BeEmpty();
    }

    [Fact]
    public async Task Una_clase_sin_estrategia_registrada_no_esta_disponible()
    {
        var tipo = Tipo(DocumentClass.WriteOff, "BAJ");

        var r = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Adjustments, Borrador(tipo)), default);

        Codigo(r).Should().Be("Inventory.DocumentClass.NotAvailable");
    }

    [Fact]
    public async Task Confirmar_desde_la_ruta_de_otro_grupo_es_el_404_del_documento()
    {
        var borrador = await BorradorGuardadoAsync(Tipo());

        var r = await Confirmacion().ConfirmarAsync(new PedidoDeConfirmacion(borrador.PublicId, DocumentClassGroup.Purchases), default);

        Codigo(r).Should().Be("Inventory.Document.NotFound");
    }

    // ----------------------------------------------------------------------------------------- anular --

    private VoidInventoryDocumentCommandHandler Anular() => new(_db, _actor, _reloj, Vista(), Confirmacion());

    [Fact]
    public void Anular_exige_motivo()
    {
        new VoidInventoryDocumentCommandValidator().Validate(new VoidInventoryDocumentCommand(Guid.NewGuid(), DocumentClassGroup.Adjustments, ""))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Anular_crea_el_contrario_con_su_propia_fecha_y_las_referencias_en_los_dos_sentidos()
    {
        var tipo = Tipo();
        Tipo(DocumentClass.Voiding, "ANU");
        var original = Confirmado(tipo);

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(original.PublicId, DocumentClassGroup.Adjustments, "Error de digitación"), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Status.Should().Be(DocumentStatus.Confirmed);
        r.Value.OperationDate.Should().Be(Hoy, "nunca hereda la fecha del original");
        var anulacion = await _db.InventoryDocuments.SingleAsync(d => d.PublicId == r.Value.VoidingDocumentPublicId);
        anulacion.Class.Should().Be(DocumentClass.Voiding);
        anulacion.VoidsDocumentId.Should().Be(original.Id);
        anulacion.Reason.Should().Be("Error de digitación");
        original.Status.Should().Be(DocumentStatus.Voided);
        original.VoidedByDocumentId.Should().Be(anulacion.Id);
        (await _db.DocumentLinks.SingleAsync()).Kind.Should().Be(DocumentLinkKind.Voids);
        _diario.Should().Contain("Revertir").And.NotContain("Aplicar");
    }

    [Fact]
    public async Task Anular_dos_veces_responde_ya_anulado()
    {
        var tipo = Tipo();
        Tipo(DocumentClass.Voiding, "ANU");
        var original = Confirmado(tipo);
        (await Anular().Handle(new VoidInventoryDocumentCommand(original.PublicId, DocumentClassGroup.Adjustments, "uno"), default)).IsSuccess.Should().BeTrue();

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(original.PublicId, DocumentClassGroup.Adjustments, "dos"), default);

        Codigo(r).Should().Be("Inventory.Document.AlreadyVoided");
    }

    [Fact]
    public async Task Solo_se_anula_un_confirmado()
    {
        var borrador = await BorradorGuardadoAsync(Tipo());

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(borrador.PublicId, DocumentClassGroup.Adjustments, "no"), default);

        Codigo(r).Should().Be("Inventory.Document.NotConfirmed");
    }

    [Fact]
    public async Task Una_anulacion_no_se_anula()
    {
        var tipo = Tipo();
        var anulacionTipo = Tipo(DocumentClass.Voiding, "ANU");
        var original = Confirmado(tipo);
        var anulacion = Confirmado(anulacionTipo, DocumentClass.Voiding);
        anulacion.VoidsDocumentId = original.Id;
        await _db.SaveChangesAsync();

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(anulacion.PublicId, DocumentClassGroup.Adjustments, "no"), default);

        Codigo(r).Should().Be("Inventory.Document.VoidingNotVoidable");
    }

    [Fact]
    public async Task Con_dependientes_vigentes_se_rechaza_nombrandolos()
    {
        var tipo = Tipo();
        var original = Confirmado(tipo);
        var dependiente = Confirmado(tipo);
        _db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = original.Id, TargetDocumentId = dependiente.Id, Kind = DocumentLinkKind.CountAdjustmentOf });
        await _db.SaveChangesAsync();

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(original.PublicId, DocumentClassGroup.Adjustments, "no"), default);

        Codigo(r).Should().Be("Inventory.Document.HasDependents");
        var datos = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data;
        datos.GetType().GetProperty("dependents")!.GetValue(datos).Should().BeAssignableTo<IEnumerable<InventoryErrors.Dependiente>>()
            .Which.Should().ContainSingle(d => d.PublicId == dependiente.PublicId);
    }

    [Fact]
    public async Task Un_fiscal_emitido_se_corrige_con_su_nota_no_se_anula()
    {
        var factura = Tipo(DocumentClass.SalesInvoice, "FV");
        var original = Confirmado(factura);

        var r = await Anular().Handle(new VoidInventoryDocumentCommand(original.PublicId, DocumentClassGroup.Sales, "no"), default);

        Codigo(r).Should().Be("Inventory.Document.FiscalUseCorrection");
    }

    // ----------------------------------------------------------------------------------- la estrategia --

    private sealed class EfectoFalso(DocumentClass clase, List<string> diario) : EfectoDeClaseBase
    {
        public override DocumentClass Clase => clase;

        public List<Error> Avisos { get; } = [];

        public Error? FallaAlValidar { get; set; }

        public override Task<IReadOnlyList<Error>> AvisosDelBorradorAsync(ContextoDeEfecto contexto, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Error>>(Avisos);

        public override Task<Result> ValidarAsync(ContextoDeEfecto contexto, CancellationToken ct)
        {
            diario.Add("Validar");
            return Task.FromResult(FallaAlValidar is { } e ? Result.Failure(e) : Result.Success());
        }

        public override Task<Result> AplicarAsync(ContextoDeEfecto contexto, CancellationToken ct)
        {
            diario.Add("Aplicar");
            return Task.FromResult(Result.Success());
        }

        public override Task<IReadOnlyList<object>> MensajesAsync(ContextoDeEfecto contexto, CancellationToken ct)
        {
            diario.Add("Mensajes");
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        public override Task<Result> RevertirAsync(ContextoDeEfecto contexto, CancellationToken ct)
        {
            diario.Add("Revertir");
            return Task.FromResult(Result.Success());
        }
    }
}
