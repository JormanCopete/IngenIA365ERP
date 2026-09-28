using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Execution;
using IngenIA365ERP.Application.Common.Integration;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.Documents.Efectos;
using IngenIA365ERP.Application.Inventory.Documents.Numeracion;
using IngenIA365ERP.Application.Inventory.Integration;
using IngenIA365ERP.Application.Inventory.Kardex;
using IngenIA365ERP.Application.Inventory.Sales;
using IngenIA365ERP.Domain.Common.Parametros;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Core.Payments;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Approvals;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Integration;
using IngenIA365ERP.Domain.Enums.Inventory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Sales;

/// <summary>
/// La cooperativa de prueba del crédito provisional (feature 012, I3, T646–T648; quickstart §5.11): la de ventas
/// (<see cref="VentasDePrueba"/>) con los medios <c>CREDASOC</c> (crédito a asociados: 30 días propuestos y 90 máximos, hasta 6 cuotas
/// mensuales, línea sugerida <c>CONSUMO</c>) y <c>CREDCLI</c> (crédito comercial), el asociado X, el asociado retirado Z, la persona
/// inactiva Y y el motor de aprobaciones <b>real</b> con sus dos fuentes (el documento y el crédito). Usuarios: la cajera (7, la que
/// actúa, sin límite propio que importe), <c>cajero.2</c> (8, límite de <c>Inventory.Sales.SellOnCredit</c> 100.000) y
/// <c>supervisor</c> (9, límite 1.000.000). Cartera no está habilitada (<see cref="ConsultasDeCarteraNoHabilitada"/>). (nuevo)
/// </summary>
public sealed class CreditoDePrueba
{
    public const int Cajera = Kardex.KardexDePrueba.Usuario;
    public const int Cajero2 = 8;
    public const int Supervisor = 9;

    public VentasDePrueba V { get; }
    public IngenIA365ERP.Application.Tests.Common.TestApplicationDbContext Db => V.Db;
    public PaymentMeans CredAsoc { get; private set; } = null!;
    public PaymentMeans CredCli { get; private set; } = null!;
    public Person AsociadoX { get; private set; } = null!;
    public Person AsociadoRetiradoZ { get; private set; } = null!;
    public Person InactivaY { get; private set; } = null!;
    public IMotorDeAprobaciones Motor { get; private set; } = null!;
    public CreditosAprobadosEnCurso EnCurso { get; private set; } = new();
    public IConsultasDeCartera Cartera { get; } = new ConsultasDeCarteraNoHabilitada();

    private CreditoDePrueba(VentasDePrueba v) => V = v;

    public static async Task<CreditoDePrueba> CrearAsync()
    {
        var c = new CreditoDePrueba(await VentasDePrueba.CrearAsync());
        var db = c.Db;
        c.CredAsoc = Medio("CREDASOC", PaymentMeansClass.AssociateCredit);
        c.CredCli = Medio("CREDCLI", PaymentMeansClass.CustomerCredit);
        db.PaymentMeans.AddRange(c.CredAsoc, c.CredCli);

        c.AsociadoX = new Person { PersonType = "01", TaxId = "16555111", FirstName = "Xiomara", LastName = "Asociada", IsAssociate = true, Status = "A" };
        c.AsociadoRetiradoZ = new Person { PersonType = "01", TaxId = "16555222", FirstName = "Zoila", LastName = "Retirada", IsAssociate = true, Status = "A" };
        c.InactivaY = new Person { PersonType = "01", TaxId = "16555333", FirstName = "Yolanda", LastName = "Inactiva", IsAssociate = true, IsCustomer = true, Status = "I" };
        db.People.AddRange(c.AsociadoX, c.AsociadoRetiradoZ, c.InactivaY);
        await db.SaveChangesAsync();
        db.Associates.AddRange(
            new Associate { PersonId = c.AsociadoX.Id, Status = "A", JoinDate = new DateOnly(2020, 1, 1) },
            new Associate { PersonId = c.AsociadoRetiradoZ.Id, Status = "R", JoinDate = new DateOnly(2020, 1, 1), WithdrawalDate = new DateOnly(2026, 6, 30) },
            new Associate { PersonId = c.InactivaY.Id, Status = "A", JoinDate = new DateOnly(2020, 1, 1) });

        // Usuarios, roles y límites de Inventory.Sales.SellOnCredit (SEC_PermissionAmountLimits).
        var permiso = new Permission { Resource = "Inventory.Sales", Action = "SellOnCredit" };
        db.Permissions.Add(permiso);
        db.Users.AddRange(
            new User { Id = Cajero2, Username = "cajero.2", IsActive = true },
            new User { Id = Supervisor, Username = "supervisor", IsActive = true });
        if (!db.Users.Any(u => u.Id == Cajera)) db.Users.Add(new User { Id = Cajera, Username = "cajero.1", IsActive = true });
        var cajeros = new Role { Code = "CAJEROS", Name = "Cajeros", IsActive = true };
        var supervisores = new Role { Code = "SUPERV", Name = "Supervisores", IsActive = true };
        db.Roles.AddRange(cajeros, supervisores);
        await db.SaveChangesAsync();
        db.RolePermissions.AddRange(new RolePermission { RoleId = cajeros.Id, PermissionId = permiso.Id }, new RolePermission { RoleId = supervisores.Id, PermissionId = permiso.Id });
        db.UserRoles.AddRange(
            new UserRole { UserId = Cajera, RoleId = cajeros.Id }, new UserRole { UserId = Cajero2, RoleId = cajeros.Id },
            new UserRole { UserId = Supervisor, RoleId = supervisores.Id });
        db.PermissionAmountLimits.AddRange(
            new PermissionAmountLimit { RoleId = cajeros.Id, PermissionCode = AprobacionDeCredito.Permiso, MaxAmount = 100_000m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "cajeros" },
            new PermissionAmountLimit { RoleId = supervisores.Id, PermissionCode = AprobacionDeCredito.Permiso, MaxAmount = 1_000_000m, ValidFrom = new DateOnly(2026, 1, 1), Reason = "supervisores" });
        await db.SaveChangesAsync();

        c.UsarMotorReal();
        return c;
    }

    private static PaymentMeans Medio(string codigo, PaymentMeansClass clase) => new()
    {
        Code = codigo, Name = codigo, Class = clase, DianPaymentMeansCode = "1", OfferedAtAllPointsOfSale = true, OfferedInAllChannels = true,
        OfferedForAllDocumentTypes = true, ValidFrom = new DateOnly(2026, 1, 1), CountMethod = CashCountMethod.None, AllowsPartial = true,
        DefaultTermDays = 30, MaxTermDays = 90, DefaultInstallments = 1, MaxInstallments = 6, InstallmentPeriodDays = 30, SuggestedCreditLineCode = "CONSUMO",
    };

    // ------------------------------------------------------------------------------------------- servicios --

    private void UsarMotorReal()
    {
        var limites = Substitute.For<ILimitesPorPermiso>();
        limites.MontoMaximoAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((decimal?)null);
        var servicios = new ServiceCollection().AddTransient(_ => Confirmacion()).BuildServiceProvider();
        var fuentes = new IFuenteDeAprobacion[]
        {
            new FuenteDeAprobacionDeDocumento(Db, V.K.Maestros(), servicios),
            new FuenteDeAprobacionDeCredito(Db, EnCurso, V.Compras.C.Reloj, servicios),
        };
        Motor = new MotorDeAprobaciones(Db, V.K.Actor, V.K.Permisos, V.K.Alcance, limites, Substitute.For<IAutoridadDeOtroAprobador>(),
            Substitute.For<IAvisosDeAprobacion>(), new VistaDeSolicitudes(Db, fuentes), V.Compras.C.Reloj);
    }

    public CreditoEnLaVenta Credito() => new(Db, V.K.Lector(), Cartera);

    public ReglasDeConfirmacionDeVenta Reglas() => new(Db, V.K.Actor, V.Compras.C.Reloj, V.K.Lector(), V.K.Permisos,
        new IngenIA365ERP.Application.ElectronicInvoicing.GuardiaDeEmisionFiscal(V.K.Lector(), V.Db), V.Calculo(), V.Aprobaciones(), V.Toque, Credito(),
        new AprobacionDeCredito(Db, Motor, V.K.Actor, EnCurso));

    public EfectosDeClase Efectos()
    {
        var registro = V.K.Registro();
        var reversion = new ReversionDeKardex(Db, registro);
        var emision = new EmisionDeInventario(Db, EnCurso);
        var maestros = V.K.Maestros();
        var reglas = Reglas();
        var anulacion = new AnulacionDeVenta(registro, reversion, emision, Db, V.Compras.C.Reloj);
        return new EfectosDeClase(
        [
            new EfectoComprobanteDeVenta(registro, emision, maestros, reglas, anulacion, Db),
            new EfectoNotaDeVentaNoElectronica(registro, emision, maestros, reglas, anulacion, Db),
        ], EntregaDelComercio.I3);
    }

    public ConfirmacionDeDocumento Confirmacion() => new(
        Db, V.K.Maestros(), V.K.Actor, V.Compras.C.Reloj, Efectos(), Motor, V.K.Cerrojo, new Numerador(Db, V.K.Cerrojo),
        new EmisorDeMensajes(Db, V.K.Actor, V.Compras.C.Reloj), V.K.Lector(), V.K.Vista(), [], []);

    public SaveInventoryDraftCommandHandler Guardar() =>
        new(Db, V.K.Maestros(), V.K.Alcance, V.K.Actor, V.Compras.C.Reloj, Efectos(), V.K.Vista(), [V.Borrador()]);

    public async Task<Result<ConfirmationResultDto>> ConfirmarAsync(Guid documento) =>
        await new ConfirmInventoryDocumentCommandHandler(Db, Confirmacion()).Handle(new ConfirmInventoryDocumentCommand(documento, DocumentClassGroup.Sales), default);

    public EvaluateSaleCreditQueryHandler Evaluar() => new(Db, Cartera, Motor, V.K.Actor, V.Compras.C.Reloj);

    // ------------------------------------------------------------------------------------------ escenarios --

    /// <summary>Cambia la persona que actúa.</summary>
    public void ComoUsuario(int userId) =>
        V.K.Actor.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new Actor(ActorKind.Person, userId, Guid.NewGuid(), Guid.NewGuid(),
            $"usuario{userId}@coop.co", $"usuario{userId}@coop.co", ExecutionChannel.Web, "POST /api/inventory/sales/invoices", "10.0.0.1", null));

    public DocumentPaymentInput Credito(PaymentMeans medio, decimal valor, PaymentCreditInput? condiciones = null) =>
        new(medio.PublicId, valor, Credit: condiciones);

    /// <summary>Una venta RV guardada con sus pagos (el total se calcula con un primer guardado).</summary>
    public async Task<Guid> VentaAsync(Person? cliente, Func<decimal, IReadOnlyList<DocumentPaymentInput>> pagos, params SalesLineInput[] lineas)
    {
        var borrador = await Guardar().Handle(new SaveInventoryDraftCommand(null, DocumentClassGroup.Sales, V.Venta(cliente: cliente, lineas: lineas).ComoBorrador()), default);
        if (borrador.IsFailure) throw new InvalidOperationException($"{borrador.Error.Code}: {borrador.Error.Message}");
        var total = V.Documento(borrador.Value.PublicId).AmountDue;
        var conPagos = await Guardar().Handle(new SaveInventoryDraftCommand(borrador.Value.PublicId, DocumentClassGroup.Sales,
            V.Venta(cliente: cliente, pagos: pagos(total), lineas: lineas).ComoBorrador()), default);
        if (conPagos.IsFailure) throw new InvalidOperationException($"{conPagos.Error.Code}: {conPagos.Error.Message}");
        return borrador.Value.PublicId;
    }

    /// <summary>La solicitud pendiente de crédito de la venta.</summary>
    public Domain.Entities.Approvals.ApprovalRequest SolicitudDeCredito(Guid venta)
    {
        var id = V.Documento(venta).Id;
        var pagos = Db.DocumentPayments.AsNoTracking().Where(p => p.DocumentId == id && !p.IsDeleted).Select(p => p.PublicId).ToList();
        return Db.ApprovalRequests.AsNoTracking().Include(r => r.Decisions)
            .Where(r => r.SourceType == Domain.Approvals.ApprovalSourceTypes.DocumentPayment && pagos.Contains(r.SourcePublicId))
            .OrderByDescending(r => r.Id).First();
    }

    /// <summary>Aprueba el nivel actual de la solicitud como <paramref name="usuario"/>.</summary>
    public async Task<Result<DecisionResultDto>> AprobarAsync(Domain.Entities.Approvals.ApprovalRequest solicitud, int usuario)
    {
        ComoUsuario(usuario);
        Db.ChangeTracker.Clear();
        return await Motor.DecidirAsync(new DecisionDeAprobacion(solicitud.PublicId, ApprovalDecisionKind.Approve, null, solicitud.ContentSha256), default);
    }
}
