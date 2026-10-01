using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Approvals;
using IngenIA365ERP.Application.Common.Approvals.SaveApprovalPolicy;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Tests.Inventory.Kardex;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Inventory.Periods;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.Common;

/// <summary>
/// Feature 012, T274 (contracts/api.md §7, §15.1; T21, T33): lo que Inventario le aporta a la plataforma de parámetros y de
/// aprobaciones (<see cref="ReglasDePlataformaDeInventario"/>). Resuelve bodega y tipo de documento por <c>PublicId</c> (una
/// bodega fuera del alcance es el 404 <c>Inventory.Warehouse.NotFound</c>); <c>Contabilidad.ModoDePaso</c> con <c>chain</c>
/// escribe una vigencia por tipo activo de la cadena y un tipo encadenado sin ella es <c>ChainMismatch</c>; dejar sin paso un tipo
/// fiscal exige el permiso y la confirmación; <c>validFrom</c> en un período cerrado se rechaza en parámetros y en políticas; y
/// una política vacía para el saldo inicial o el ajuste de conteo es <c>RequiredForClass</c>. Las reglas de <c>Costeo.*</c> las
/// cubre <c>RetroactivoMinimoTests</c>.
/// </summary>
public class ReglasDePlataformaDeInventarioTests
{
    private static readonly DateOnly Octubre = new(2026, 10, 1);

    private static JsonElement Datos(Error error) =>
        JsonSerializer.SerializeToElement(error.Should().BeOfType<ErrorConDatos>().Subject.Data, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static ReglasDePlataformaDeInventario Reglas(KardexDePrueba k) => new(k.C.Db, k.Alcance, k.Lector(), k.Permisos);

    private static async Task<KardexDePrueba> ConComprasAsync()
    {
        var k = await KardexDePrueba.CrearAsync();
        foreach (var (codigo, clase, activo) in new[]
                 {
                     ("REC", DocumentClass.PurchaseReceipt, true), ("FCP", DocumentClass.SupplierInvoice, true),
                     ("DVP", DocumentClass.SupplierReturn, true), ("NTP", DocumentClass.SupplierNote, false),
                     ("SIN", DocumentClass.OpeningBalance, true),
                 })
            k.C.Db.InventoryDocumentTypes.Add(new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = activo });
        await k.C.Db.SaveChangesAsync();
        return k;
    }

    private static Task<Result<AddParameterVersionResponse>> AltaAsync(KardexDePrueba k, string clave, string valor, DateOnly desde,
        ParameterScopeKind ambito = ParameterScopeKind.None, Guid? entidad = null, string? chain = null, bool confirmar = false)
    {
        var reglas = Reglas(k);
        return new AddParameterVersionCommandHandler(k.C.Db, k.Permisos, reglas, reglas).Handle(
            new AddParameterVersionCommand(ParametrosDeInventario.Modulo, clave, ambito, entidad, chain, valor, desde, "Cambio aprobado", null, confirmar),
            default);
    }

    // ------------------------------------------------------------------------------------------ ámbitos --

    [Fact]
    public async Task Resuelve_bodega_y_tipo_de_documento_por_PublicId_y_una_bodega_fuera_del_alcance_es_404()
    {
        var k = await KardexDePrueba.CrearAsync();
        var reglas = Reglas(k);

        var bodega = await reglas.ResolverAsync(ParameterScopeKind.Warehouse, k.Principal.PublicId, default);
        var tipo = await reglas.ResolverAsync(ParameterScopeKind.DocumentType, k.Tipo("AJP").PublicId, default);
        var tipoInexistente = await reglas.ResolverAsync(ParameterScopeKind.DocumentType, Guid.NewGuid(), default);

        bodega.Value.Id.Should().Be(k.Principal.Id);
        tipo.Value.Kind.Should().Be(ParameterScopeKind.DocumentType);
        tipo.Value.Code.Should().Be("AJP");
        tipoInexistente.Error.Code.Should().Be("Inventory.DocumentType.NotFound");
        (await reglas.DescribirAsync(ParameterScopeKind.DocumentType, [k.Tipo("AJN").Id], default)).Should().ContainSingle().Which.Code.Should().Be("AJN");

        k.Alcance.ObtenerAsync(Arg.Any<CancellationToken>()).Returns(new AlcanceDeInventario(false, new HashSet<int> { k.Segunda.Id }, null, false, new HashSet<int>(), null));
        var fuera = await Reglas(k).ResolverAsync(ParameterScopeKind.Warehouse, k.Principal.PublicId, default);
        fuera.Error.Code.Should().Be("Inventory.Warehouse.NotFound");
    }

    // ---------------------------------------------------------------------------------- modo de paso --

    [Fact]
    public async Task El_modo_de_paso_por_cadena_escribe_una_vigencia_por_tipo_activo_de_la_cadena()
    {
        var k = await ConComprasAsync();

        var r = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", Octubre, ParameterScopeKind.DocumentType, chain: "Purchases");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.AffectedDocumentTypes!.Select(t => t.Code).Should().BeEquivalentTo(["REC", "FCP", "DVP"], "NTP está inactivo");
        var escritas = await k.C.Db.ParameterVersions.Where(v => v.Key == ParametrosDeInventario.ContabilidadModoDePaso).ToListAsync();
        escritas.Should().HaveCount(3).And.OnlyContain(v => v.ScopeKind == ParameterScopeKind.DocumentType && v.Value == "PorLotes");
    }

    [Fact]
    public async Task Un_tipo_encadenado_sin_la_cadena_es_ChainMismatch_con_los_tipos_de_la_cadena()
    {
        var k = await ConComprasAsync();

        var r = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", Octubre, ParameterScopeKind.DocumentType, k.Tipo("FCP").PublicId);
        var sinCadena = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", Octubre, ParameterScopeKind.DocumentType, k.Tipo("AJN").PublicId);

        r.Error.Code.Should().Be("Inventory.PostingMode.ChainMismatch");
        var datos = Datos(r.Error);
        datos.GetProperty("chain").GetString().Should().Be("Purchases");
        datos.GetProperty("documentTypes").EnumerateArray().Select(t => t.GetProperty("code").GetString()).Should().BeEquivalentTo(["REC", "FCP", "DVP"]);
        sinCadena.IsSuccess.Should().BeTrue("los tipos sin cadena van uno a uno");
    }

    [Fact]
    public async Task Dejar_sin_paso_un_tipo_fiscal_exige_el_permiso_y_la_confirmacion()
    {
        var k = await ConComprasAsync();
        k.Permisos.HasPermissionAsync(ReglasDePlataformaDeInventario.PermisoDeFiscalSinPaso, Arg.Any<CancellationToken>()).Returns(false);

        var sinPermiso = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", Octubre, ParameterScopeKind.DocumentType, chain: "Purchases");
        k.Permisos.HasPermissionAsync(ReglasDePlataformaDeInventario.PermisoDeFiscalSinPaso, Arg.Any<CancellationToken>()).Returns(true);
        var sinConfirmar = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", Octubre, ParameterScopeKind.DocumentType, chain: "Purchases");
        var general = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", Octubre);
        var confirmado = await AltaAsync(k, ParametrosDeInventario.ContabilidadModoDePaso, "NoPasa", Octubre, ParameterScopeKind.DocumentType, chain: "Purchases", confirmar: true);

        sinPermiso.Error.Code.Should().Be("Parameters.PermissionRequired");
        Datos(sinPermiso.Error).GetProperty("permissionCode").GetString().Should().Be("Inventory.DocumentTypes.DisableFiscalPosting");
        sinConfirmar.Error.Code.Should().Be("Inventory.PostingMode.FiscalRequiresConfirmation");
        Datos(sinConfirmar.Error).GetProperty("fiscalDocumentTypes").EnumerateArray()
            .Select(t => (t.GetProperty("code").GetString(), t.GetProperty("class").GetString()))
            .Should().BeEquivalentTo([("FCP", "SupplierInvoice")]);
        general.Error.Code.Should().Be("Inventory.PostingMode.FiscalRequiresConfirmation", "el valor general lo heredan los tipos fiscales sin vigencia propia");
        confirmado.IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------ período cerrado --

    [Fact]
    public async Task Una_vigencia_dentro_de_un_periodo_cerrado_es_ValidFromInClosedPeriod()
    {
        var k = await KardexDePrueba.CrearAsync();
        k.C.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 8, 31) });
        await k.C.Db.SaveChangesAsync();

        var r = await AltaAsync(k, ParametrosDeInventario.ExistenciasStockNegativoPermitido, "true", new DateOnly(2026, 8, 20));
        var abierto = await AltaAsync(k, ParametrosDeInventario.ExistenciasStockNegativoPermitido, "true", new DateOnly(2026, 9, 1));

        r.Error.Code.Should().Be("Parameters.ValidFromInClosedPeriod");
        Datos(r.Error).GetProperty("lastClosedDate").GetString().Should().Be("2026-08-31");
        abierto.IsSuccess.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------ políticas de aprobación --

    [Fact]
    public async Task Las_politicas_de_aprobacion_respetan_el_periodo_cerrado_y_los_tipos_que_siempre_se_aprueban()
    {
        var k = await ConComprasAsync();
        k.C.Db.InventorySetups.Add(new InventorySetup { StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 8, 31) });
        var conteo = new InventoryDocumentType { Code = "AJCON", Name = "Ajuste de conteo", Class = DocumentClass.PositiveAdjustment, IsActive = true };
        k.C.Db.InventoryDocumentTypes.Add(conteo);
        await k.C.Db.SaveChangesAsync();
        var politicaDelConteo = new ApprovalPolicy
        {
            Subject = ApprovalSubjects.DocumentConfirmation, DocumentTypePublicId = conteo.PublicId, Version = 1, ValidFrom = new DateOnly(2026, 7, 1),
            PolicyKey = ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, conteo.PublicId), Reason = "semilla",
        };
        politicaDelConteo.Levels.Add(new ApprovalPolicyLevel { Policy = politicaDelConteo, Order = 1, Threshold = 0m, PermissionCode = ReglasDePlataformaDeInventario.PermisoDeConteo });
        k.C.Db.ApprovalPolicies.Add(politicaDelConteo);
        await k.C.Db.SaveChangesAsync();
        var reglas = Reglas(k);
        var handler = new SaveApprovalPolicyCommandHandler(k.C.Db, reglas);

        Task<Result<ApprovalPolicyDto>> Politica(Guid? tipo, DateOnly desde) =>
            handler.Handle(new SaveApprovalPolicyCommand(ApprovalSubjects.DocumentConfirmation, tipo, desde, "Cambio", []), default);

        var enCerrado = await Politica(k.Tipo("AJN").PublicId, new DateOnly(2026, 8, 15));
        var saldoInicial = await Politica(k.Tipo("SIN").PublicId, Octubre);
        var delConteo = await Politica(conteo.PublicId, Octubre);
        var inexistente = await Politica(Guid.NewGuid(), Octubre);
        var normal = await Politica(k.Tipo("AJN").PublicId, Octubre);

        enCerrado.Error.Code.Should().Be("Approvals.Policy.ValidFromInClosedPeriod");
        saldoInicial.Error.Code.Should().Be("Approvals.Policy.RequiredForClass");
        Datos(saldoInicial.Error).GetProperty("class").GetString().Should().Be("OpeningBalance");
        delConteo.Error.Code.Should().Be("Approvals.Policy.RequiredForClass");
        inexistente.Error.Code.Should().Be("Inventory.DocumentType.NotFound");
        normal.IsSuccess.Should().BeTrue();
        normal.Value.DocumentType!.Code.Should().Be("AJN");
        normal.Value.DocumentType.Class.Should().Be("NegativeAdjustment");
    }
}
