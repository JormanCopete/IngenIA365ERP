using FluentAssertions;
using IngenIA365ERP.Application.Attachments.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Documents;

/// <summary>
/// Feature 012, T255 (FR-037, T41; contracts/api.md §10 «Soportes»): el dueño <c>InventoryAdjustmentSupport</c> en
/// <see cref="AdjuntosDeModulo"/>. Sube quien tiene <c>Inventory.Adjustments.Create</c> mientras el ajuste está en borrador o en
/// aprobación; se lee con <c>Inventory.Adjustments.View</c>; confirmado, no se sube ni se borra (<c>Attachments.OwnerLocked</c>);
/// un ajuste inexistente o sin permiso es el mismo 404.
/// </summary>
public class SoportesDeAjusteTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();

    public SoportesDeAjusteTests() => _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

    private InventoryDocument Ajuste(DocumentStatus estado, DocumentClass clase = DocumentClass.WriteOff)
    {
        var documento = new InventoryDocument { Class = clase, DocumentTypeId = 1, OperationDate = new DateOnly(2026, 9, 25), BranchId = 1 };
        if (estado == DocumentStatus.PendingApproval) documento.EnviarAAprobacion();
        if (estado is DocumentStatus.Confirmed or DocumentStatus.Voided) documento.Confirmar(1, DateTime.UtcNow);
        if (estado == DocumentStatus.Voided) documento.MarcarAnulado(99);
        _db.InventoryDocuments.Add(documento);
        _db.SaveChanges();
        return documento;
    }

    [Theory]
    [InlineData(DocumentStatus.Draft)]
    [InlineData(DocumentStatus.PendingApproval)]
    public async Task Se_sube_mientras_el_ajuste_esta_en_borrador_o_en_aprobacion(DocumentStatus estado)
    {
        var ajuste = Ajuste(estado);

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteDeAjuste, ajuste.PublicId, default)).Should().BeNull();
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, AdjuntosDeModulo.SoporteDeAjuste, ajuste.PublicId, default, _permisos)).Should().BeNull();
    }

    [Theory]
    [InlineData(DocumentStatus.Confirmed)]
    [InlineData(DocumentStatus.Voided)]
    public async Task Confirmado_el_soporte_se_conserva_con_el_ajuste(DocumentStatus estado)
    {
        var ajuste = Ajuste(estado);

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteDeAjuste, ajuste.PublicId, default))!.Code
            .Should().Be("Attachments.OwnerLocked");
        (await AdjuntosDeModulo.PuedeBorrarAsync(_db, AdjuntosDeModulo.SoporteDeAjuste, ajuste.PublicId, default, _permisos))!.Code
            .Should().Be("Attachments.OwnerLocked");
    }

    [Fact]
    public async Task Sin_permiso_de_crear_ajustes_o_con_un_ajuste_inexistente_es_el_mismo_404()
    {
        var ajuste = Ajuste(DocumentStatus.Draft);
        var compra = Ajuste(DocumentStatus.Draft, DocumentClass.PurchaseReceipt);

        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteDeAjuste, Guid.NewGuid(), default))!.Code.Should().Be("Generic.NotFound");
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteDeAjuste, compra.PublicId, default))!.Code
            .Should().Be("Generic.NotFound", "sólo los documentos del grupo de ajustes");
        _permisos.HasPermissionAsync("Inventory.Adjustments.Create", Arg.Any<CancellationToken>()).Returns(false);
        (await AdjuntosDeModulo.PuedeSubirAsync(_db, _permisos, AdjuntosDeModulo.SoporteDeAjuste, ajuste.PublicId, default))!.Code.Should().Be("Generic.NotFound");
    }

    [Fact]
    public async Task Se_lee_con_el_permiso_de_ver_ajustes()
    {
        _permisos.HasPermissionAsync("Inventory.Adjustments.View", Arg.Any<CancellationToken>()).Returns(false);

        (await AdjuntosDeModulo.PuedeLeerAsync(_permisos, AdjuntosDeModulo.SoporteDeAjuste, default)).Should().BeFalse();
    }
}
