using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Pricing;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Inventory.Catalog;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Pricing;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Pricing;

/// <summary>
/// Feature 012, I3, T602 (contracts/api.md §19.3; FR-054, T33, T51; data-model §14): la aprobación de los descuentos sobre el tope. La
/// solicitud es <c>DiscountOverCap</c> sobre <c>DocumentLineDiscount</c> con la huella de la línea; sin política rige la regla fija de
/// un nivel con <c>Inventory.Discounts.Authorize</c>; si la línea cambia, la solicitud pendiente queda sin efecto y se pide otra; quien
/// aprueba necesita un tope suficiente (<c>Inventory.Discount.CapInsufficient</c>) y no puede ser quien pidió; al aprobar, la fila
/// copia aprobador y método; cobrar con un descuento sin aprobación vigente es <c>Inventory.Discount.ApprovalPending</c>.
/// </summary>
public class AprobacionDeDescuentosTests
{
    private const int Cajero = 7;
    private const int Supervisor = 20;
    private const int Gerente = 21;

    private CatalogoDePrueba _c = null!;
    private readonly IActorActual _actor = Substitute.For<IActorActual>();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly IAlcanceDeInventario _alcance = Substitute.For<IAlcanceDeInventario>();
    private readonly ILimitesPorPermiso _limites = Substitute.For<ILimitesPorPermiso>();
    private InventoryDocument _documento = null!;
    private InventoryDocumentLine _linea = null!;
    private DocumentLineDiscount _descuento = null!;

    public AprobacionDeDescuentosTests()
    {
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(AlcanceDeInventario.Total);
        _limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        Como(Cajero);
    }

    private static async Task<AprobacionDeDescuentosTests> CrearAsync()
    {
        var t = new AprobacionDeDescuentosTests { _c = await CatalogoDePrueba.CrearAsync() };
        var db = t._c.Db;
        var producto = await t._c.ProductoAsync(t._c.Alta("P1"));
        var tipo = new InventoryDocumentType { Code = "POS", Name = "Venta POS", Class = DocumentClass.PosEquivalentDocument, IsActive = true };
        db.InventoryDocumentTypes.Add(tipo);
        await db.SaveChangesAsync();
        t._documento = new InventoryDocument
        {
            Class = DocumentClass.PosEquivalentDocument, DocumentTypeId = tipo.Id, Prefix = string.Empty, OperationDate = CatalogoDePrueba.Hoy,
            BranchId = 1, CreatedByUserId = Cajero,
        };
        t._linea = new InventoryDocumentLine
        {
            Document = t._documento, LineNumber = 1, ProductId = t._c.Producto(producto.PublicId).Id, UnitId = t._c.Unidad("UND").Id,
            Quantity = 2m, QuantityBase = 2m, ListPrice = 10000m, UnitPrice = 10000m, GrossAmount = 20000m, DiscountAmount = 3000m, NetAmount = 17000m,
        };
        t._documento.Lines.Add(t._linea);
        db.InventoryDocuments.Add(t._documento);
        await db.SaveChangesAsync();
        t._descuento = new DocumentLineDiscount
        {
            DocumentLineId = t._linea.Id, DocumentId = t._documento.Id, Sequence = 1, Rate = 0.15m, Amount = 3000m, CapRateApplied = 0.05m,
            RequiresApproval = true, Reason = "Cliente frecuente",
        };
        db.DocumentLineDiscounts.Add(t._descuento);

        var supervisores = new Role { Code = "SUPERVISOR", Name = "Supervisores", IsActive = true };
        var gerentes = new Role { Code = "GERENTE", Name = "Gerentes", IsActive = true };
        db.Roles.AddRange(supervisores, gerentes);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = Supervisor, RoleId = supervisores.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
        db.UserRoles.Add(new UserRole { UserId = Gerente, RoleId = gerentes.Id, AssignedAt = DateTime.UtcNow, AssignedBy = "prueba" });
        db.DiscountCaps.Add(new DiscountCap { RoleId = supervisores.Id, MaxLineRate = 0.10m, MaxDocumentRate = 0.05m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Comité" });
        db.DiscountCaps.Add(new DiscountCap { RoleId = gerentes.Id, MaxLineRate = 0.30m, MaxDocumentRate = 0.20m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "Comité" });
        await db.SaveChangesAsync();
        return t;
    }

    private void Como(int userId) =>
        _actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(), $"u{userId}", null,
            ExecutionChannel.Web, "POST", null, null));

    private MotorDeAprobaciones Motor() => new(_c.Db, _actor, _permisos, _alcance, _limites, Substitute.For<IAutoridadDeOtroAprobador>(),
        Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(_c.Db, [new FuenteDeAprobacionDeDescuento(_c.Db)]), _c.Reloj);

    private AprobacionDeDescuentos Servicio() => new(_c.Db, Motor(), _actor);

    private async Task<Result<IReadOnlyList<Domain.Entities.Approvals.ApprovalRequest>>> SolicitarAsync(Guid? punto = null)
    {
        Como(Cajero);
        var r = await Servicio().SolicitarAsync(_documento, [(_linea, _descuento)], punto, default);
        await _c.Db.SaveChangesAsync();
        return r;
    }

    private Task<Result<DecisionResultDto>> DecidirAsync(int como, string? huella = null)
    {
        Como(como);
        var solicitud = _c.Db.ApprovalRequests.Single(r => r.Status == ApprovalRequestStatus.Pending);
        return Motor().DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, ApprovalDecisionKind.Approve, null, huella ?? solicitud.ContentSha256), default);
    }

    [Fact]
    public async Task Pide_una_solicitud_DiscountOverCap_con_la_huella_de_la_linea_y_la_regla_fija()
    {
        var t = await CrearAsync();
        var punto = Guid.NewGuid();

        var r = await t.SolicitarAsync(punto);
        var otraVez = await t.SolicitarAsync(punto);

        r.Value.Should().ContainSingle();
        var solicitud = t._c.Db.ApprovalRequests.Single();
        solicitud.Subject.Should().Be(ApprovalSubjects.DiscountOverCap);
        solicitud.SourceType.Should().Be(ApprovalSourceTypes.DocumentLineDiscount);
        solicitud.SourcePublicId.Should().Be(t._descuento.PublicId);
        solicitud.ContentSha256.Should().Be(AprobacionDeDescuentos.Huella(t._linea, t._descuento));
        solicitud.ScopePointOfSalePublicId.Should().Be(punto);
        solicitud.PolicyId.Should().BeNull("sin política rige la regla fija");
        solicitud.NivelesRequeridos().Single().PermissionCode.Should().Be("Inventory.Discounts.Authorize");
        otraVez.Value.Should().BeEmpty("la misma huella no pide otra");
    }

    [Fact]
    public async Task Si_la_linea_cambia_la_solicitud_pendiente_queda_sin_efecto_y_se_pide_otra()
    {
        var t = await CrearAsync();
        await t.SolicitarAsync();

        t._linea.Quantity = 3m;
        await t.SolicitarAsync();

        t._c.Db.ApprovalRequests.Select(r => r.Status).Should().BeEquivalentTo([ApprovalRequestStatus.Cancelled, ApprovalRequestStatus.Pending]);
    }

    [Fact]
    public async Task Aprueba_quien_tiene_tope_suficiente_y_la_fila_copia_aprobador_y_metodo()
    {
        var t = await CrearAsync();
        await t.SolicitarAsync();
        (await t.Servicio().LineasSinAprobarAsync(t._documento, default)).Should().Equal(1);

        var corto = await t.DecidirAsync(Supervisor);
        var propio = await t.DecidirAsync(Cajero);
        var gerente = await t.DecidirAsync(Gerente);

        corto.Error.Code.Should().Be(ErroresDePrecios.CapInsufficientCode, "el supervisor tiene 10 % y se pide 15 %");
        propio.Error.Code.Should().Be("Approvals.SelfApprovalForbidden");
        gerente.IsSuccess.Should().BeTrue(gerente.IsFailure ? gerente.Error.Message : string.Empty);
        var descuento = t._c.Db.DocumentLineDiscounts.Single();
        (descuento.ApprovedByUserId, descuento.ApprovalMethod).Should().Be((Gerente, ApprovalMethod.OwnSession));
        descuento.ApprovalRequestId.Should().Be(t._c.Db.ApprovalRequests.Single().Id);
        (await t.Servicio().ExigirAprobadosAsync(t._documento, default)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Cobrar_con_un_descuento_sin_aprobacion_vigente_es_ApprovalPending()
    {
        var t = await CrearAsync();
        await t.SolicitarAsync();
        await t.DecidirAsync(Gerente);

        t._linea.Quantity = 5m;
        await t._c.Db.SaveChangesAsync();
        var r = await t.Servicio().ExigirAprobadosAsync(t._documento, default);

        r.Error.Code.Should().Be(ErroresDePrecios.ApprovalPendingCode, "la aprobación caducó al cambiar la línea");
        ((ErrorConDatos)r.Error).Data.GetType().GetProperty("lines")!.GetValue(((ErrorConDatos)r.Error).Data).Should().BeEquivalentTo(new[] { 1 });
    }

    [Fact]
    public async Task Un_descuento_que_ya_no_pide_aprobacion_retira_su_solicitud()
    {
        var t = await CrearAsync();
        await t.SolicitarAsync();

        t._descuento.RequiresApproval = false;
        await t.SolicitarAsync();

        t._c.Db.ApprovalRequests.Single().Status.Should().Be(ApprovalRequestStatus.Cancelled);
        (await t.Servicio().LineasSinAprobarAsync(t._documento, default)).Should().BeEmpty();
    }
}
