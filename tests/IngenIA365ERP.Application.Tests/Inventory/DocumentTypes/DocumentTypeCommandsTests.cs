using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.Documents;
using IngenIA365ERP.Application.Inventory.DocumentTypes;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Persistence.Seeding.Parametric;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Inventory.DocumentTypes;

/// <summary>
/// Feature 012, T107 (FR-036 a FR-038; contracts/api.md §8; data-model §5.8–§5.9): tipos de documento y su numeración.
/// La clase y el código son fijos; una clase de una entrega futura no se elige; cada marca vale sólo en su clase; ninguna
/// bodega de tránsito; los tipos con resolución DIAN no tienen consecutivo propio; inactivar respeta los documentos
/// abiertos y los tipos que el sistema necesita; un consecutivo nuevo cierra el anterior la víspera, nunca por debajo de
/// lo emitido y sin cruzarse; el prefijo tiene hasta 4 caracteres en mayúsculas. Y la semilla del tipo por clase (T152).
/// </summary>
public class DocumentTypeCommandsTests
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IMaestrosDelDocumento _maestros = Substitute.For<IMaestrosDelDocumento>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();

    private readonly BodegaDelDocumento _principal = new(1, Guid.NewGuid(), "PRIN", "Principal", 1, false, true, false);
    private readonly BodegaDelDocumento _transito = new(2, Guid.NewGuid(), "TRAN", "Tránsito", 1, true, true, false);

    public DocumentTypeCommandsTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc));
        _maestros.BodegasAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(c => new[] { _principal, _transito }.Where(b => c.Arg<IReadOnlyCollection<Guid>>().Contains(b.PublicId)).ToList());
        _maestros.BodegasPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>())
            .Returns(c => new[] { _principal, _transito }.Where(b => c.Arg<IReadOnlyCollection<int>>().Contains(b.Id)).ToList());
        _maestros.CanalesDeVentaPorIdAsync(Arg.Any<IReadOnlyCollection<int>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    private VistaDeTiposDeDocumento Vista() => new(_db, _maestros, new LectorDeParametros(_db), _reloj);

    private CreateInventoryDocumentTypeCommand Alta(
        DocumentClass clase = DocumentClass.PositiveAdjustment, string codigo = "ajx", string? prefijo = "AX", long? primero = 100,
        bool retiroGravado = false, bool ivaAlCosto = false, bool fechaFutura = false, IReadOnlyList<Guid>? bodegas = null) =>
        new(codigo, "Ajuste especial", clase, false, false, true, false, bodegas, null, retiroGravado, ivaAlCosto, fechaFutura,
            prefijo, primero, new DateOnly(2026, 9, 1));

    private Task<Result<DocumentTypeDto>> CrearAsync(CreateInventoryDocumentTypeCommand comando) =>
        new CreateInventoryDocumentTypeCommandHandler(_db, _maestros, _reloj, Vista()).Handle(comando, default);

    private Task<Result<DocumentTypeDto>> SecuenciaAsync(Guid tipo, string? prefijo, long siguiente, DateOnly desde) =>
        new AddDocumentSequenceCommandHandler(_db, Vista()).Handle(new AddDocumentSequenceCommand(tipo, prefijo, siguiente, desde, "Cambio de talonario"), default);

    private InventoryDocumentType TipoDirecto(DocumentClass clase, string codigo, bool activo = true)
    {
        var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = activo };
        _db.InventoryDocumentTypes.Add(tipo);
        _db.SaveChanges();
        return tipo;
    }

    private void Documento(InventoryDocumentType tipo, string prefijo, long? numero, DocumentStatus estado = DocumentStatus.Confirmed)
    {
        var documento = new InventoryDocument { Class = tipo.Class, DocumentTypeId = tipo.Id, Prefix = prefijo, Number = numero, OperationDate = Hoy, BranchId = 1 };
        if (estado == DocumentStatus.Confirmed) documento.Confirmar(1, DateTime.UtcNow);
        if (estado == DocumentStatus.PendingApproval) documento.EnviarAAprobacion();
        _db.InventoryDocuments.Add(documento);
        _db.SaveChanges();
    }

    // ------------------------------------------------------------------------------------------ alta --

    [Fact]
    public async Task El_alta_normaliza_codigo_y_prefijo_y_abre_su_primer_consecutivo()
    {
        var r = await CrearAsync(Alta(prefijo: "ax"));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Code.Should().Be("AJX");
        r.Value.Class.Should().Be(DocumentClass.PositiveAdjustment);
        r.Value.Group.Should().Be(DocumentClassGroup.Adjustments);
        r.Value.Prefix.Should().Be("AX", "el prefijo va en mayúsculas");
        r.Value.CurrentSequence!.NextValue.Should().Be(100, "continúa la numeración de SOLIDO");
        r.Value.CurrentSequence.ValidFrom.Should().Be(new DateOnly(2026, 9, 1));
        r.Value.RequiredFields.Reason.Should().BeTrue();
        r.Value.Warehouses.Should().BeEmpty("sin bodegas = todas las operativas");
    }

    [Theory]
    [InlineData("ABCDE")]
    [InlineData("A-1")]
    public void El_prefijo_tiene_hasta_cuatro_letras_o_digitos(string prefijo)
    {
        new CreateInventoryDocumentTypeCommandValidator().Validate(Alta(prefijo: prefijo)).IsValid.Should().BeFalse();
        new AddDocumentSequenceCommandValidator().Validate(new AddDocumentSequenceCommand(Guid.NewGuid(), prefijo, 1, Hoy, "motivo"))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Un_consecutivo_nuevo_exige_motivo()
    {
        new AddDocumentSequenceCommandValidator().Validate(new AddDocumentSequenceCommand(Guid.NewGuid(), "A", 1, Hoy, " "))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task El_codigo_repetido_nombra_el_existente()
    {
        (await CrearAsync(Alta())).IsSuccess.Should().BeTrue();

        var r = await CrearAsync(Alta(codigo: "AJX"));

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Ajuste especial");
    }

    [Fact]
    public async Task Una_clase_de_una_entrega_futura_no_esta_disponible()
    {
        var r = await CrearAsync(Alta(DocumentClass.Assembly));

        r.Error.Code.Should().Be("Inventory.DocumentClass.NotAvailable");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { @class = "Assembly", availableIn = "I6" });
    }

    [Fact]
    public async Task Cada_marca_vale_solo_en_su_clase()
    {
        (await CrearAsync(Alta(retiroGravado: true))).Error.Code.Should().Be("Inventory.DocumentType.FlagNotApplicable");
        (await CrearAsync(Alta(ivaAlCosto: true))).Error.Code.Should().Be("Inventory.DocumentType.FlagNotApplicable");
        (await CrearAsync(Alta(DocumentClass.SupplierInvoice, fechaFutura: true))).Error.Code.Should().Be("Inventory.DocumentType.FlagNotApplicable");

        (await CrearAsync(Alta(DocumentClass.InternalConsumption, "CIX", retiroGravado: true))).IsSuccess.Should().BeTrue();
        (await CrearAsync(Alta(DocumentClass.PurchaseReceipt, "REX", ivaAlCosto: true))).IsSuccess.Should().BeTrue();
        (await CrearAsync(Alta(codigo: "AJF", fechaFutura: true))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Una_bodega_de_transito_no_es_bodega_permitida_de_un_tipo()
    {
        var r = await CrearAsync(Alta(bodegas: [_principal.PublicId, _transito.PublicId]));

        r.Error.Code.Should().Be("Inventory.DocumentType.TransitNotAllowed");
    }

    [Fact]
    public async Task Con_bodegas_permitidas_el_tipo_deja_de_admitir_todas()
    {
        var r = await CrearAsync(Alta(bodegas: [_principal.PublicId]));

        r.Value.Warehouses.Should().ContainSingle().Which.Code.Should().Be("PRIN");
        (await _db.InventoryDocumentTypes.SingleAsync()).AllWarehouses.Should().BeFalse();
    }

    [Fact]
    public async Task La_edicion_no_cambia_ni_la_clase_ni_el_codigo()
    {
        var creado = (await CrearAsync(Alta())).Value;

        var r = await new UpdateInventoryDocumentTypeCommandHandler(_db, _maestros, _reloj, Vista()).Handle(
            new UpdateInventoryDocumentTypeCommand(creado.PublicId, "Otro nombre", true, false, false, false, null, null, false, false, false), default);

        r.Value.Name.Should().Be("Otro nombre");
        r.Value.Code.Should().Be("AJX");
        r.Value.Class.Should().Be(DocumentClass.PositiveAdjustment);
        r.Value.RequiredFields.Counterparty.Should().BeTrue();
    }

    // ------------------------------------------------------------------------------------ numeración --

    [Fact]
    public async Task Un_tipo_con_resolucion_DIAN_no_tiene_consecutivo_propio()
    {
        var factura = TipoDirecto(DocumentClass.SalesInvoice, "FV");

        var r = await SecuenciaAsync(factura.PublicId, "FE", 1, Hoy);

        r.Error.Code.Should().Be("Inventory.DocumentType.NumberedByResolution");
    }

    [Fact]
    public async Task Un_prefijo_nuevo_cierra_el_vigente_la_vispera()
    {
        var creado = (await CrearAsync(Alta())).Value;

        var r = await SecuenciaAsync(creado.PublicId, "BX", 1, new DateOnly(2026, 10, 1));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Sequences.Should().HaveCount(2);
        r.Value.Sequences!.Single(s => s.Prefix == "AX").ValidTo.Should().Be(new DateOnly(2026, 9, 30));
        r.Value.Sequences!.Single(s => s.Prefix == "BX").Should().BeEquivalentTo(new { NextValue = 1L, ValidFrom = new DateOnly(2026, 10, 1), ValidTo = (DateOnly?)null });
    }

    [Fact]
    public async Task Volver_a_un_prefijo_usado_reabre_su_fila_y_continua()
    {
        var creado = (await CrearAsync(Alta())).Value;
        await SecuenciaAsync(creado.PublicId, "BX", 1, new DateOnly(2026, 10, 1));
        Documento(await _db.InventoryDocumentTypes.SingleAsync(), "AX", 150);

        var r = await SecuenciaAsync(creado.PublicId, "AX", 151, new DateOnly(2026, 11, 1));

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Sequences.Should().HaveCount(2, "el número es único por tipo y prefijo: la fila se reabre");
        r.Value.Sequences!.Single(s => s.Prefix == "AX").Should().BeEquivalentTo(new { NextValue = 151L, ValidFrom = new DateOnly(2026, 11, 1), ValidTo = (DateOnly?)null });
        r.Value.Sequences!.Single(s => s.Prefix == "BX").ValidTo.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public async Task El_siguiente_numero_no_puede_quedar_en_o_debajo_de_lo_emitido()
    {
        var creado = (await CrearAsync(Alta())).Value;
        Documento(await _db.InventoryDocumentTypes.SingleAsync(), "AX", 150);

        var r = await SecuenciaAsync(creado.PublicId, "AX", 150, Hoy);

        r.Error.Code.Should().Be("Inventory.Sequence.NumberAlreadyIssued");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { lastIssued = 150L });
    }

    [Fact]
    public async Task El_mismo_prefijo_vigente_solo_mueve_su_siguiente_numero()
    {
        var creado = (await CrearAsync(Alta())).Value;

        var r = await SecuenciaAsync(creado.PublicId, "AX", 500, Hoy);

        r.Value.Sequences.Should().ContainSingle().Which.NextValue.Should().Be(500);
    }

    [Fact]
    public async Task Dos_vigencias_del_mismo_tipo_no_se_cruzan()
    {
        var creado = (await CrearAsync(Alta())).Value;

        var r = await SecuenciaAsync(creado.PublicId, "BX", 1, new DateOnly(2026, 8, 1));

        r.Error.Code.Should().Be("Inventory.Sequence.Overlaps");
    }

    // ---------------------------------------------------------------------------- inactivar y reactivar --

    [Fact]
    public async Task Inactivar_con_borradores_o_en_aprobacion_se_rechaza_con_las_cantidades()
    {
        var tipo = TipoDirecto(DocumentClass.PositiveAdjustment, "AJP");
        Documento(tipo, string.Empty, null, DocumentStatus.Draft);
        Documento(tipo, string.Empty, null, DocumentStatus.Draft);
        Documento(tipo, string.Empty, null, DocumentStatus.PendingApproval);

        var r = await new DeactivateInventoryDocumentTypeCommandHandler(_db, Vista())
            .Handle(new DeactivateInventoryDocumentTypeCommand(tipo.PublicId, "ya no se usa"), default);

        r.Error.Code.Should().Be("Inventory.DocumentType.HasOpenDocuments");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { drafts = 2, pendingApproval = 1 });
    }

    [Fact]
    public async Task El_ultimo_tipo_activo_de_una_clase_del_sistema_no_se_inactiva()
    {
        var anulacion = TipoDirecto(DocumentClass.Voiding, "ANU");

        var r = await new DeactivateInventoryDocumentTypeCommandHandler(_db, Vista())
            .Handle(new DeactivateInventoryDocumentTypeCommand(anulacion.PublicId, "no"), default);

        r.Error.Code.Should().Be("Inventory.DocumentType.RequiredBySystem");
    }

    [Fact]
    public async Task Inactivar_y_reactivar()
    {
        var tipo = TipoDirecto(DocumentClass.PositiveAdjustment, "AJP");

        (await new DeactivateInventoryDocumentTypeCommandHandler(_db, Vista())
            .Handle(new DeactivateInventoryDocumentTypeCommand(tipo.PublicId, "temporada"), default)).Value.IsActive.Should().BeFalse();
        (await new ReactivateInventoryDocumentTypeCommandHandler(_db, Vista())
            .Handle(new ReactivateInventoryDocumentTypeCommand(tipo.PublicId, "vuelve"), default)).Value.IsActive.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------------- clases --

    [Fact]
    public async Task Las_clases_dicen_si_son_operables_en_este_despliegue()
    {
        var r = await new ListDocumentClassesQueryHandler().Handle(new ListDocumentClassesQuery(), default);

        r.Value.Should().HaveCount(34);
        r.Value.Single(c => c.Class == DocumentClass.PositiveAdjustment).Operable.Should().BeTrue();
        r.Value.Single(c => c.Class == DocumentClass.SalesInvoice).Should().BeEquivalentTo(new
        {
            Operable = false, IsFiscal = true, NumberedBy = Domain.Inventory.Documents.NumberedBy.DianResolution,
        }, o => o.ExcludingMissingMembers());
    }

    // ------------------------------------------------------------------------------------------ semilla --

    [Fact]
    public async Task La_semilla_deja_un_tipo_por_clase_operable_con_su_consecutivo_y_la_politica_del_saldo_inicial()
    {
        var insertadas = await InventoryDocumentTypesSeeder.AplicarAsync(_db, Hoy, default);

        var tipos = await _db.InventoryDocumentTypes.Include(t => t.Sequences).ToListAsync();
        var operables = Domain.Inventory.Documents.ClasesDeDocumento.Todas.Where(c => c.Operable()).Select(c => c.Class).ToList();
        tipos.Select(t => t.Class).Should().BeEquivalentTo(operables, "un tipo por clase de I1, incluida la anulación");
        tipos.Should().OnlyContain(t => t.IsSeeded && t.IsActive);
        tipos.Should().OnlyContain(t => t.Sequences.Count == 1 && t.Sequences.Single().Prefix == string.Empty
            && t.Sequences.Single().ValidFrom == new DateOnly(2026, 9, 1) && t.Sequences.Single().NextValue == 1);
        tipos.Should().Contain(t => t.Class == DocumentClass.Voiding);

        var saldo = tipos.Single(t => t.Class == DocumentClass.OpeningBalance);
        var politica = await _db.ApprovalPolicies.Include(p => p.Levels).SingleAsync();
        politica.Should().BeEquivalentTo(new { Subject = ApprovalSubjects.DocumentConfirmation, DocumentTypePublicId = (Guid?)saldo.PublicId, Version = 1 },
            o => o.ExcludingMissingMembers());
        politica.Levels.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Order = (byte)1, Threshold = 0m, PermissionCode = "Inventory.OpeningBalance.Approve" }, o => o.ExcludingMissingMembers());
        insertadas.Should().Be(operables.Count + 1);

        (await InventoryDocumentTypesSeeder.AplicarAsync(_db, Hoy, default)).Should().Be(0, "idempotente por código");
    }

    [Fact]
    public async Task La_semilla_no_pisa_un_tipo_que_la_cooperativa_ya_tiene()
    {
        TipoDirecto(DocumentClass.PositiveAdjustment, "AJP");

        await InventoryDocumentTypesSeeder.AplicarAsync(_db, Hoy, default);

        (await _db.InventoryDocumentTypes.CountAsync(t => t.Code == "AJP")).Should().Be(1);
        (await _db.InventoryDocumentTypes.SingleAsync(t => t.Code == "AJP")).IsSeeded.Should().BeFalse();
    }

    [Fact]
    public async Task El_slug_tipos_de_documento_encuentra_el_codigo_antes_de_escribir_el_resto()
    {
        TipoDirecto(DocumentClass.PositiveAdjustment, "AJP");
        var consulta = new IngenIA365ERP.Application.Common.Catalogos.BuscarCodigoDeCatalogoQueryHandler(_db);

        var r = await consulta.Handle(new IngenIA365ERP.Application.Common.Catalogos.BuscarCodigoDeCatalogoQuery("tipos-de-documento", "ajp"), default);

        r.IsSuccess.Should().BeTrue();
        r.Value.Existe.Should().BeTrue();
    }
}
