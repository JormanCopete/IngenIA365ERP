using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Inventory.Common;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Inventory.DocumentTypes;
using IngenIA365ERP.Application.Inventory.Imports;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Inventory.Documents;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Domain.Enums.Inventory;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Inventory.Parameters;
using IngenIA365ERP.Domain.Inventory.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Inventory.DocumentTypes.PlantillaDeTiposDeDocumento;

namespace IngenIA365ERP.Application.Tests.Inventory.DocumentTypes;

/// <summary>
/// Feature 012, T153 (contracts/plantillas.md §8, §0.5 y §0.7): la plantilla 8 —hojas <c>TiposDeDocumento</c> y
/// <c>NivelesDeAprobacion</c>— con la mecánica común (revisar no guarda, aplicar es todo o nada) y las reglas del alta
/// unitaria: clase operable e inmutable, marcas sólo en su clase, consecutivo nuevo que cierra el anterior y no baja de
/// lo emitido, inactivar sin dejar sin tipo una clase del sistema; la política de un tipo se reemplaza con una versión
/// nueva (pide motivo) y queda igual si los niveles no cambian; permisos por hoja y columna; y lo que todavía no se
/// puede citar (bodegas por código, canal, modo de paso) responde <c>Import.Cell.NotYetAvailable</c>.
/// </summary>
public class ImportDocumentTypesCommandTests : IDisposable
{
    private static readonly DateOnly Hoy = new(2026, 9, 25);

    private static readonly string[] EncabezadosTipos =
    [
        P.Codigo, P.Nombre, P.Clase, P.Prefijo, P.SiguienteNumero, P.VigenciaDelPrefijo, P.ExigeTercero, P.ExigeCentroDeCosto,
        P.ExigeMotivo, P.ExigeReferenciaExterna, P.Bodegas, P.Canal, P.RetiroGravado, P.IvaNoDescontable, P.PermiteFechaFutura,
        P.ModoDePaso, P.Activo, P.VigenciaDelModo,
    ];

    private static readonly string[] EncabezadosNiveles = [P.TipoDeDocumento, P.Nivel, P.Umbral, P.Permiso, P.VigenteDesde];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    public ImportDocumentTypesCommandTests()
    {
        _reloj.HoyLocal.Returns(Hoy);
        _reloj.UtcNow.Returns(new DateTime(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc));
        _permisos.EsMaestroGlobal.Returns(true);
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? string.Empty, out var t)
                ? Result.Success(t)
                : Result.Failure<TablaLeida>(ArchivosTabulares.HojaFaltante(ci.ArgAt<string?>(2) ?? "?"))));

        _db.Permissions.AddRange(
            new Permission { Resource = "Inventory.Approvals", Action = "Supervisor" },
            new Permission { Resource = "Inventory.Approvals", Action = "Management" });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    private void Tipos(params string?[][] filas) =>
        _hojas[P.HojaTipos] = new TablaLeida(EncabezadosTipos, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private void Niveles(params string?[][] filas) =>
        _hojas[P.HojaNiveles] = new TablaLeida(EncabezadosNiveles, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private static string?[] Tipo(string codigo, string clase, string? prefijo = null, string? siguiente = null, string? desde = null,
        string nombre = "Tipo", string? motivo = "sí", string? bodegas = null, string? canal = null, string? retiro = null,
        string? modo = null, string? activo = null, string? vigenciaDelModo = null) =>
        [codigo, nombre, clase, prefijo, siguiente, desde, "no", "no", motivo, "no", bodegas, canal, retiro, null, null, modo, activo, vigenciaDelModo];

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo, string motivo = "", string? confirmarFiscales = null)
    {
        var servicios = new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos, servicios);
        var reglas = new ReglasDePlataformaDeInventario(_db, Substitute.For<IAlcanceDeInventario>());
        var r = await new ImportDocumentTypesCommandHandler(_db, ejecutor, _reloj, new LectorDeParametros(_db), reglas)
            .Handle(new ImportDocumentTypesCommand(modo, new ArchivoDeImportacion("tipos.xlsx", [1, 2, 3]), motivo)
            {
                ConfirmFiscalWithoutPosting = confirmarFiscales,
            }, default);
        _db.ChangeTracker.Clear();
        return r;
    }

    private InventoryDocumentType TipoExistente(DocumentClass clase, string codigo, string prefijo = "", long siguiente = 1)
    {
        var tipo = new InventoryDocumentType { Code = codigo, Name = codigo, Class = clase, IsActive = true, RequiresReason = true };
        tipo.Sequences.Add(new DocumentSequence { DocumentType = tipo, Prefix = prefijo, NextValue = siguiente, ValidFrom = new DateOnly(2026, 9, 1) });
        _db.InventoryDocumentTypes.Add(tipo);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        return tipo;
    }

    private static ErrorDeFila UnicoError(Result<ImportResultDto> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Errors.Should().ContainSingle(string.Join(" | ", r.Value.Errors.Select(e => $"{e.Sheet} {e.Row} {e.Column} {e.Code}: {e.Message}")));
        return r.Value.Errors[0];
    }

    // --------------------------------------------------------------------------------------------- la plantilla --

    [Fact]
    public void La_plantilla_8_declara_sus_dos_hojas_con_los_permisos_de_la_7_y_es_la_de_CatalogoDePlantillas()
    {
        P.Definicion.Hojas.Select(h => h.Nombre).Should().Equal(P.HojaTipos, P.HojaNiveles);
        P.Definicion.Hojas.Should().OnlyContain(h => h.Columnas.Count > 0);
        CatalogoDePlantillas.Por("inventory.document-types").Definicion.Should().BeSameAs(P.Definicion);
        P.Definicion.Hoja(P.HojaNiveles)!.Permiso.Should().Be("Inventory.ApprovalPolicies.Manage");
        P.Definicion.Hoja(P.HojaNiveles)!.Obligatoria.Should().BeFalse("un libro sólo con tipos conserva las políticas");
        foreach (var columna in P.ColumnasDelModoDePaso)
            P.Definicion.Hoja(P.HojaTipos)!.Columna(columna)!.Permiso.Should().Be("Inventory.Parameters.Manage");
        P.EtiquetasDeClase.Values.Distinct().Should().HaveCount(Enum.GetValues<DocumentClass>().Length, "cada clase tiene su etiqueta de §8");
    }

    // ------------------------------------------------------------------------------------ revisión y aplicación --

    [Fact]
    public async Task Revisar_no_guarda_y_aplicar_crea_tipos_con_su_consecutivo_y_su_politica()
    {
        Tipos(Tipo("ajx", "Ajuste positivo", prefijo: "ax", siguiente: "100", desde: "2026-09-01"),
              Tipo("CIX", "InternalConsumption", retiro: "sí"));
        Niveles(["AJX", "1", "0", "Inventory.Approvals.Supervisor", "2026-10-01"],
                ["AJX", "2", "5000000", "Inventory.Approvals.Management", "2026-10-01"]);

        var revision = await ImportarAsync(ModoDeImportacion.Review);

        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(e => e.Message)));
        revision.Value.Sheets.Should().BeEquivalentTo(new[]
        {
            new ResumenDeHojaDto(P.HojaTipos, 2, 2, 0, 0),
            new ResumenDeHojaDto(P.HojaNiveles, 2, 2, 0, 0),
        });
        revision.Value.RequiresReason.Should().BeTrue("el archivo crea una versión de política");
        (await _db.InventoryDocumentTypes.CountAsync()).Should().Be(0, "la revisión no guarda nada (FR-030)");
        (await _db.ApprovalPolicies.CountAsync()).Should().Be(0);

        var sinMotivo = await ImportarAsync(ModoDeImportacion.Apply);
        sinMotivo.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _db.InventoryDocumentTypes.CountAsync()).Should().Be(0, "todo o nada");

        var aplicada = await ImportarAsync(ModoDeImportacion.Apply, "Parametrización inicial");

        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Message : string.Empty);
        var ajx = await _db.InventoryDocumentTypes.Include(t => t.Sequences).SingleAsync(t => t.Code == "AJX");
        ajx.Class.Should().Be(DocumentClass.PositiveAdjustment, "la etiqueta en español vale");
        ajx.RequiresReason.Should().BeTrue();
        ajx.AllWarehouses.Should().BeTrue();
        ajx.Sequences.Should().ContainSingle(s => s.Prefix == "AX" && s.NextValue == 100 && s.ValidFrom == new DateOnly(2026, 9, 1));
        (await _db.InventoryDocumentTypes.SingleAsync(t => t.Code == "CIX")).IsTaxableWithdrawal.Should().BeTrue();

        var politica = await _db.ApprovalPolicies.Include(p => p.Levels).SingleAsync();
        politica.PolicyKey.Should().Be(ApprovalPolicy.ClaveDe(ApprovalPolicy.ModuloInventario, ApprovalSubjects.DocumentConfirmation, ajx.PublicId));
        politica.ValidFrom.Should().Be(new DateOnly(2026, 10, 1));
        politica.Reason.Should().Be("Parametrización inicial");
        politica.Levels.OrderBy(l => l.Order).Select(l => (l.Order, l.Threshold, l.PermissionCode)).Should().Equal(
            ((byte)1, 0m, "Inventory.Approvals.Supervisor"), ((byte)2, 5000000m, "Inventory.Approvals.Management"));

        // El mismo libro otra vez: todo «sin cambio» y nada nuevo.
        var otraVez = await ImportarAsync(ModoDeImportacion.Review);
        otraVez.Value.Sheets.Should().BeEquivalentTo(new[]
        {
            new ResumenDeHojaDto(P.HojaTipos, 2, 0, 0, 2),
            new ResumenDeHojaDto(P.HojaNiveles, 2, 0, 0, 2),
        });
        otraVez.Value.RequiresReason.Should().BeFalse();
    }

    [Fact]
    public async Task Cambiar_los_niveles_crea_otra_version_y_cierra_la_anterior_la_vispera()
    {
        var tipo = TipoExistente(DocumentClass.NegativeAdjustment, "AJN");
        Tipos(Tipo("AJN", "NegativeAdjustment", nombre: "AJN"));
        Niveles(["AJN", "1", "0", "Inventory.Approvals.Supervisor", "2026-09-01"]);
        (await ImportarAsync(ModoDeImportacion.Apply, "Primera")).IsSuccess.Should().BeTrue();

        Niveles(["AJN", "1", "100000", "Inventory.Approvals.Supervisor", "2026-11-01"]);
        var r = await ImportarAsync(ModoDeImportacion.Apply, "Umbral nuevo");

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Changes.Should().ContainSingle(c => c.Sheet == P.HojaNiveles && c.Action == AccionDeImportacion.Update);
        var versiones = await _db.ApprovalPolicies.Include(p => p.Levels).Where(p => p.DocumentTypePublicId == tipo.PublicId).OrderBy(p => p.Version).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateOnly(2026, 10, 31));
        versiones[1].Levels.Single().Threshold.Should().Be(100000m);

        // Una versión que empezaría antes de la última se cruza (la misma regla del alta unitaria).
        Niveles(["AJN", "1", "200000", "Inventory.Approvals.Supervisor", "2026-10-15"]);
        var cruzada = UnicoError(await ImportarAsync(ModoDeImportacion.Review));
        (cruzada.Sheet, cruzada.Column, cruzada.Code).Should().Be((P.HojaNiveles, P.VigenteDesde, "Approvals.Policy.Overlaps"));
    }

    // -------------------------------------------------------------------------------- reglas del alta unitaria --

    [Fact]
    public async Task La_clase_de_un_tipo_existente_no_cambia()
    {
        TipoExistente(DocumentClass.PositiveAdjustment, "AJP");
        Tipos(Tipo("AJP", "NegativeAdjustment"));

        var error = UnicoError(await ImportarAsync(ModoDeImportacion.Review));

        (error.Sheet, error.Row, error.Column).Should().Be((P.HojaTipos, 2, P.Clase));
        error.Message.Should().Contain("no cambia");
    }

    [Fact]
    public async Task Una_marca_fuera_de_su_clase_da_el_codigo_del_alta_en_su_columna()
    {
        Tipos(Tipo("AJX", "PositiveAdjustment", retiro: "sí"));

        var error = UnicoError(await ImportarAsync(ModoDeImportacion.Review));

        (error.Column, error.Code).Should().Be((P.RetiroGravado, "Inventory.DocumentType.FlagNotApplicable"));
    }

    [Fact]
    public async Task Una_clase_de_una_entrega_futura_todavia_no_se_registra()
    {
        var futura = Enum.GetValues<DocumentClass>().First(c => !ClasesDeDocumento.De(c).Operable());
        Tipos(Tipo("FUT", futura.ToString()));

        var error = UnicoError(await ImportarAsync(ModoDeImportacion.Review));

        (error.Column, error.Code).Should().Be((P.Clase, ImportErrors.CellNotYetAvailable));
    }

    [Fact]
    public async Task Otro_prefijo_abre_un_consecutivo_nuevo_con_motivo_y_cierra_el_anterior_la_vispera()
    {
        TipoExistente(DocumentClass.PositiveAdjustment, "AJP", prefijo: "", siguiente: 7);
        Tipos(Tipo("AJP", "PositiveAdjustment", nombre: "AJP", prefijo: "AP", siguiente: "1", desde: "2026-10-01"));

        var revision = await ImportarAsync(ModoDeImportacion.Review);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(e => e.Message)));
        revision.Value.RequiresReason.Should().BeTrue();
        revision.Value.Changes.Single().Fields.Select(f => f.Column).Should().Contain(P.Prefijo);

        (await ImportarAsync(ModoDeImportacion.Apply, "Talonario nuevo")).IsSuccess.Should().BeTrue();

        var secuencias = await _db.DocumentSequences.OrderBy(s => s.ValidFrom).ToListAsync();
        secuencias.Should().HaveCount(2);
        (secuencias[0].Prefix, secuencias[0].ValidTo).Should().Be(("", new DateOnly(2026, 9, 30)));
        (secuencias[1].Prefix, secuencias[1].NextValue, secuencias[1].ValidTo).Should().Be(("AP", 1L, (DateOnly?)null));
    }

    [Fact]
    public async Task El_siguiente_numero_no_baja_de_lo_emitido()
    {
        var tipo = TipoExistente(DocumentClass.PositiveAdjustment, "AJP", prefijo: "AP", siguiente: 11);
        var documento = new InventoryDocument { Class = tipo.Class, DocumentTypeId = tipo.Id, Prefix = "AP", Number = 10, OperationDate = Hoy, BranchId = 1 };
        documento.Confirmar(1, DateTime.UtcNow);
        _db.InventoryDocuments.Add(documento);
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
        Tipos(Tipo("AJP", "PositiveAdjustment", nombre: "AJP", prefijo: "AP", siguiente: "5"));

        var error = UnicoError(await ImportarAsync(ModoDeImportacion.Review));

        (error.Column, error.Code).Should().Be((P.SiguienteNumero, "Inventory.Sequence.NumberAlreadyIssued"));
    }

    [Fact]
    public async Task No_se_inactiva_el_ultimo_tipo_de_una_clase_que_genera_el_sistema()
    {
        TipoExistente(DocumentClass.Voiding, "ANU");
        Tipos(Tipo("ANU", "Voiding", nombre: "ANU", activo: "no"));

        var error = UnicoError(await ImportarAsync(ModoDeImportacion.Review));

        (error.Column, error.Code).Should().Be((P.Activo, "Inventory.DocumentType.RequiredBySystem"));

        // Con otro tipo activo de la clase en el mismo archivo, sí.
        Tipos(Tipo("ANU", "Voiding", nombre: "ANU", activo: "no"), Tipo("ANX", "Voiding"));
        var r = await ImportarAsync(ModoDeImportacion.Review);
        r.Value.Valid.Should().BeTrue(string.Join(" | ", r.Value.Errors.Select(e => e.Message)));
        r.Value.RequiresReason.Should().BeTrue("inactivar pide motivo");
    }

    [Fact]
    public async Task Niveles_mal_formados_permiso_desconocido_y_tipo_inexistente()
    {
        Tipos(Tipo("AJX", "PositiveAdjustment"));
        Niveles(["AJX", "1", "0", "Inventory.Approvals.Supervisor", "2026-10-01"],
                ["AJX", "3", "10", "Inventory.Approvals.Management", "2026-10-01"],
                ["OTRO", "1", "0", "Inventory.Approvals.Supervisor", "2026-10-01"],
                ["AJX", "4", "0", "No.Existe", "2026-10-01"]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Select(e => (e.Row, e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (2, P.Nivel, "Approvals.Policy.LevelsInvalid"),
            (4, P.TipoDeDocumento, ImportErrors.CellNotFound),
            (5, P.Permiso, "Approvals.Policy.PermissionUnknown"),
        });
    }

    // ----------------------------------------------------------------------- lo que todavía no se puede citar --

    [Fact]
    public async Task Bodegas_por_codigo_y_canal_responden_que_todavia_no_estan_disponibles()
    {
        Tipos(Tipo("AJX", "PositiveAdjustment", bodegas: "PRIN", canal: "MOSTRADOR", modo: "EnLinea"));

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Select(e => (e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (P.Bodegas, ImportErrors.CellNotYetAvailable),
            (P.Canal, ImportErrors.CellNotYetAvailable),
        }, "el modo de paso ya se carga por plantilla (US3, T286)");

        Tipos(Tipo("AJX", "PositiveAdjustment", bodegas: "*"));
        (await ImportarAsync(ModoDeImportacion.Review)).Value.Valid.Should().BeTrue("* = todas las operativas");
    }

    // --------------------------------------------------------------------------- modo de paso (US3, T286) --

    [Fact]
    public async Task El_modo_de_paso_escribe_una_vigencia_por_tipo_y_volver_a_subirlo_no_cambia_nada()
    {
        TipoExistente(DocumentClass.NegativeAdjustment, "AJN");
        Tipos(Tipo("AJX", "PositiveAdjustment", modo: "PorLotes", vigenciaDelModo: "2026-10-01"),
              Tipo("AJN", "NegativeAdjustment", modo: "EnLinea", vigenciaDelModo: "2026-10-01"));

        var revision = await ImportarAsync(ModoDeImportacion.Review);
        revision.Value.Valid.Should().BeTrue(string.Join(" | ", revision.Value.Errors.Select(e => e.Message)));
        revision.Value.RequiresReason.Should().BeTrue("cambiar el modo de paso pide motivo");
        (await _db.ParameterVersions.CountAsync()).Should().Be(0, "revisar no guarda");

        var aplicada = await ImportarAsync(ModoDeImportacion.Apply, "Contabilidad pasa por lotes");
        aplicada.IsSuccess.Should().BeTrue(aplicada.IsFailure ? aplicada.Error.Message : string.Empty);
        var versiones = await _db.ParameterVersions.ToListAsync();
        var tipos = await _db.InventoryDocumentTypes.ToDictionaryAsync(t => t.Id, t => t.Code);
        versiones.Select(v => (tipos[v.ScopeId], v.Key, v.Value, v.ValidFrom, v.ScopeKind)).Should().BeEquivalentTo(new[]
        {
            ("AJX", ParametrosDeInventario.ContabilidadModoDePaso, "PorLotes", new DateOnly(2026, 10, 1), ParameterScopeKind.DocumentType),
            ("AJN", ParametrosDeInventario.ContabilidadModoDePaso, "EnLinea", new DateOnly(2026, 10, 1), ParameterScopeKind.DocumentType),
        });
        versiones.Should().OnlyContain(v => v.Reason == "Contabilidad pasa por lotes");

        var otraVez = await ImportarAsync(ModoDeImportacion.Review);
        otraVez.Value.Valid.Should().BeTrue();
        otraVez.Value.RequiresReason.Should().BeFalse("igual a la vigencia propia: sin cambio");
    }

    [Fact]
    public async Task Una_cadena_con_modos_distintos_o_incompleta_es_ChainMismatch_y_completa_se_acepta()
    {
        TipoExistente(DocumentClass.PurchaseReceipt, "REC");
        TipoExistente(DocumentClass.SupplierInvoice, "FCP");
        TipoExistente(DocumentClass.SupplierReturn, "DVP");

        Tipos(Tipo("REC", "PurchaseReceipt", modo: "PorLotes", vigenciaDelModo: "2026-10-01"));
        var incompleta = await ImportarAsync(ModoDeImportacion.Review);
        var error = UnicoError(incompleta);
        (error.Column, error.Code).Should().Be((P.ModoDePaso, "Inventory.PostingMode.ChainMismatch"));
        error.Message.Should().Contain("FCP").And.Contain("DVP");

        Tipos(Tipo("REC", "PurchaseReceipt", modo: "PorLotes", vigenciaDelModo: "2026-10-01"),
              Tipo("FCP", "SupplierInvoice", modo: "EnLinea", vigenciaDelModo: "2026-10-01"),
              Tipo("DVP", "SupplierReturn", modo: "PorLotes", vigenciaDelModo: "2026-10-01"));
        var distinta = await ImportarAsync(ModoDeImportacion.Review);
        distinta.Value.Errors.Should().HaveCount(3).And.OnlyContain(e => e.Code == "Inventory.PostingMode.ChainMismatch");

        Tipos(Tipo("REC", "PurchaseReceipt", modo: "PorLotes", vigenciaDelModo: "2026-10-01"),
              Tipo("FCP", "SupplierInvoice", modo: "PorLotes", vigenciaDelModo: "2026-10-01"),
              Tipo("DVP", "SupplierReturn", modo: "PorLotes", vigenciaDelModo: "2026-10-01"));
        (await ImportarAsync(ModoDeImportacion.Review)).Value.Valid.Should().BeTrue();
    }

    [Fact]
    public async Task Dejar_sin_paso_un_tipo_fiscal_exige_repetir_su_codigo_al_aplicar()
    {
        TipoExistente(DocumentClass.PurchaseReceipt, "REC");
        TipoExistente(DocumentClass.SupplierInvoice, "FCP");
        TipoExistente(DocumentClass.SupplierReturn, "DVP");
        Tipos(Tipo("REC", "PurchaseReceipt", modo: "NoPasa", vigenciaDelModo: "2026-10-01"),
              Tipo("FCP", "SupplierInvoice", modo: "NoPasa", vigenciaDelModo: "2026-10-01"),
              Tipo("DVP", "SupplierReturn", modo: "NoPasa", vigenciaDelModo: "2026-10-01"));

        var revision = await ImportarAsync(ModoDeImportacion.Review);
        revision.Value.Valid.Should().BeTrue();
        ((IEnumerable<string>)revision.Value.Extra[ImportDocumentTypesCommandHandler.ExtraFiscalesSinPaso]!).Should().Equal("FCP");

        var sinConfirmar = await ImportarAsync(ModoDeImportacion.Apply, "Cooperativa no contabiliza compras");
        sinConfirmar.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _db.ParameterVersions.CountAsync()).Should().Be(0);

        var confirmada = await ImportarAsync(ModoDeImportacion.Apply, "Cooperativa no contabiliza compras", confirmarFiscales: "fcp");
        confirmada.IsSuccess.Should().BeTrue(confirmada.IsFailure ? confirmada.Error.Message : string.Empty);
        (await _db.ParameterVersions.CountAsync()).Should().Be(3);
    }

    [Fact]
    public async Task El_modo_del_saldo_inicial_se_ignora_con_aviso_y_una_vigencia_en_periodo_cerrado_se_rechaza()
    {
        _db.InventorySetups.Add(new IngenIA365ERP.Domain.Entities.Inventory.Periods.InventorySetup
        {
            StartDate = new DateOnly(2026, 7, 1), LastClosedDate = new DateOnly(2026, 8, 31),
        });
        _db.SaveChanges();
        Tipos(Tipo("SIX", "OpeningBalance", modo: "PorLotes"),
              Tipo("AJX", "PositiveAdjustment", modo: "PorLotes", vigenciaDelModo: "2026-08-15"));
        Niveles(["AJX", "1", "0", "Inventory.Approvals.Supervisor", "2026-08-20"]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Warnings.Should().Contain(a => a.Code == ImportDocumentTypesCommandHandler.AvisoIgnorada && a.Column == P.ModoDePaso);
        r.Value.Errors.Select(e => (e.Sheet, e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (P.HojaTipos, P.VigenciaDelModo, "Parameters.ValidFromInClosedPeriod"),
            (P.HojaNiveles, P.VigenteDesde, "Approvals.Policy.ValidFromInClosedPeriod"),
        });
    }

    // ------------------------------------------------------------------------------------------------ permisos --

    [Fact]
    public async Task La_hoja_de_niveles_y_el_modo_de_paso_exigen_sus_permisos()
    {
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyCollection<string>>(["Inventory.DocumentTypes.Manage"]));
        Tipos(Tipo("AJX", "PositiveAdjustment", modo: "EnLinea"));
        Niveles(["AJX", "1", "0", "Inventory.Approvals.Supervisor", "2026-10-01"]);

        var r = await ImportarAsync(ModoDeImportacion.Review);

        r.Value.Errors.Should().Contain(e => e.Code == ImportErrors.CellPermissionRequired && e.Sheet == P.HojaNiveles && e.Row == 0
            && e.Message.Contains("Inventory.ApprovalPolicies.Manage"));
        r.Value.Errors.Should().Contain(e => e.Code == ImportErrors.CellPermissionRequired && e.Column == P.ModoDePaso
            && e.Message.Contains("Inventory.Parameters.Manage"));
    }
}
