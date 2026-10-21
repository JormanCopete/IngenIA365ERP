using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Lending.Activation;
using IngenIA365ERP.Application.Lending.Migration;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Lending.Segregation;
using Microsoft.EntityFrameworkCore;
using P = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Partes;
using C = IngenIA365ERP.Application.Lending.Imports.DefinicionesDeMigracion.Columnas;

namespace IngenIA365ERP.Application.Tests.Lending.Migration;

/// <summary>
/// Feature 013, US8, T0719 (plantillas.md §5.1, §5.3, C64; api.md §19.1): importar un archivo de una parte. <c>review</c> corre las mismas reglas y no
/// guarda; <c>apply</c> es todo o nada para el archivo y escribe una tanda (<c>BatchNumber</c> consecutivo, nombre, SHA-256, filas, quién y cuándo) con
/// sus filas; el mismo archivo otra vez es «sin cambio»; una llave repetida en otra tanda es <c>Import.Row.Duplicate</c> nombrando la tanda;
/// <c>replace=true</c> reemplaza las tandas de la parte; el maestro, las sucursales vinculadas, las líneas vigentes al corte y el alcance se exigen
/// con <c>Import.Cell.NotFound</c>; la usura del corte y las referencias a créditos que la carga no trae son avisos; sólo un borrador recibe partes;
/// la auditoría lleva el SHA-256 en el módulo <c>Lending</c>. (nuevo)
/// </summary>
public sealed class ImportMigrationPartCommandHandlerTests : IAsyncLifetime
{
    private MigracionDePrueba _m = null!;

    public async Task InitializeAsync() => _m = await MigracionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _m.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Revisar_no_guarda_y_aplicar_escribe_la_tanda_con_su_huella_y_sus_filas()
    {
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("cr-1"), MigracionDePrueba.Credito("CR-2", "79000002"));

        var revisada = await _m.ImportarAsync(P.Loans, ModoDeImportacion.Review, contenido: "creditos-1");
        revisada.IsSuccess.Should().BeTrue();
        revisada.Value.Valid.Should().BeTrue(string.Join("; ", revisada.Value.Errors.Select(e => $"{e.Row}/{e.Column}: {e.Message}")));
        (await _m.Db.MigrationLoadParts.AnyAsync()).Should().BeFalse("revisar no guarda");
        (await _m.Db.MigrationLoanRows.AnyAsync()).Should().BeFalse();

        var r = await _m.ImportarAsync(P.Loans, archivo: "creditos.xlsx", contenido: "creditos-1");

        r.IsSuccess.Should().BeTrue();
        var tanda = await _m.Db.MigrationLoadParts.AsNoTracking().SingleAsync();
        tanda.Should().BeEquivalentTo(new
        {
            MigrationLoadId = _m.Carga.Id, PartKey = P.Loans, BatchNumber = 1, Status = MigrationPartStatus.Imported, FileName = "creditos.xlsx",
            FileSha256 = r.Value.FileSha256, RowCount = 2, ImportedByUserId = _m.Jefa.Id,
        }, o => o.ExcludingMissingMembers());
        var filas = await _m.Db.MigrationLoanRows.AsNoTracking().OrderBy(f => f.RowNumber).ToListAsync();
        filas.Select(f => (f.MigrationLoadPartId, f.RowNumber, f.SourceLoanNumber, f.PersonId)).Should()
            .Equal((tanda.Id, 2, "CR-1", _m.Ana.Id), (tanda.Id, 3, "CR-2", _m.Beto.Id));
        filas[0].OfficePublicId.Should().Be(_m.Norte.PublicId);
        filas[0].CreditLineId.Should().Be(_m.LineaId);
        filas[0].TermsJson.Should().Contain("\"rateKind\":\"Fixed\"").And.Contain("\"term\":12");
        (await _m.Db.CaseParticipations.AsNoTracking().SingleAsync()).Action.Should().Be(AccionesDeSegregacion.CargarMigracion);
    }

    [Fact]
    public async Task El_mismo_archivo_otra_vez_es_sin_cambio_y_una_llave_de_otra_tanda_es_duplicada()
    {
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-1"));
        (await _m.ImportarAsync(P.Loans, contenido: "uno")).IsSuccess.Should().BeTrue();

        var otraVez = await _m.ImportarAsync(P.Loans, contenido: "uno");
        otraVez.Value.Sheets[0].Unchanged.Should().Be(1);
        (await _m.Db.MigrationLoadParts.CountAsync()).Should().Be(1, "el mismo archivo no abre otra tanda");

        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-2"), MigracionDePrueba.Credito("CR-1"));
        var repetida = await _m.ImportarAsync(P.Loans, contenido: "dos");

        MigracionDePrueba.Errores(repetida).Should().ContainSingle(e => e.Row == 3 && e.Column == C.Numero && e.Code == ImportErrors.RowDuplicate
            && e.Message.Contains("tanda 1"));
        (await _m.Db.MigrationLoadParts.CountAsync()).Should().Be(1, "todo o nada para el archivo");

        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-3"));
        (await _m.ImportarAsync(P.Loans, contenido: "tres")).IsSuccess.Should().BeTrue();
        (await _m.Db.MigrationLoadParts.AsNoTracking().OrderBy(p => p.BatchNumber).Select(p => p.BatchNumber).ToListAsync()).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Reemplazar_la_parte_deja_las_tandas_anteriores_reemplazadas_y_sus_filas_dejan_de_contar()
    {
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-1"));
        (await _m.ImportarAsync(P.Loans, contenido: "uno")).IsSuccess.Should().BeTrue();

        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-1", monto: "2000000"));
        var r = await _m.ImportarAsync(P.Loans, reemplazar: true, contenido: "uno corregido");

        r.IsSuccess.Should().BeTrue(MigracionDePrueba.Errores(r).FirstOrDefault()?.Message);
        var tandas = await _m.Db.MigrationLoadParts.AsNoTracking().OrderBy(p => p.BatchNumber).ToListAsync();
        tandas.Select(t => (t.BatchNumber, t.Status)).Should().Equal((1, MigrationPartStatus.Superseded), (2, MigrationPartStatus.Imported));
        (await _m.Db.MigrationLoanRows.CountAsync()).Should().Be(2, "las filas de la tanda reemplazada se conservan");
    }

    [Fact]
    public async Task El_maestro_las_sucursales_las_lineas_y_el_alcance_se_exigen_fila_por_fila()
    {
        var dada = _m.G.Persona("Dora", "Retirada", "30000099");
        dada.IsDeleted = true;
        _m.Db.People.Update(dada);
        _m.Db.SaveChanges();
        _m.Db.ChangeTracker.Clear();
        _m.Archivo(P.Loans,
            MigracionDePrueba.Credito("CR-1", deudor: "99999999"),
            MigracionDePrueba.Credito("CR-2", deudor: "30000099"),
            MigracionDePrueba.Credito("CR-3", oficina: "SUR"),
            MigracionDePrueba.Credito("CR-4", linea: "NOEXISTE"));

        var r = await _m.ImportarAsync(P.Loans);

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        MigracionDePrueba.Errores(r).Select(e => (e.Row, e.Column, e.Code)).Should().BeEquivalentTo(new[]
        {
            (2, C.Deudor, ImportErrors.CellNotFound),
            (3, C.Deudor, ImportErrors.CellNotFound),
            (4, C.Oficina, ImportErrors.CellNotFound),
            (5, C.Linea, ImportErrors.CellNotFound),
        });

        // Fuera del alcance de quien importa: el mismo NotFound (T62).
        _m.G.AlcanceActual = new AlcanceDeCartera(false, new HashSet<int>(), null, true, new HashSet<int>());
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-5"));
        MigracionDePrueba.Errores(await _m.ImportarAsync(P.Loans)).Should().ContainSingle(e => e.Column == C.Oficina && e.Code == ImportErrors.CellNotFound);
        _m.G.AlcanceActual = new AlcanceDeCartera(true, new HashSet<int>(), null, false, new HashSet<int>());
        MigracionDePrueba.Errores(await _m.ImportarAsync(P.Loans)).Should().ContainSingle(e => e.Column == C.Linea && e.Code == ImportErrors.CellNotFound);
    }

    [Fact]
    public async Task La_tasa_sobre_la_usura_del_corte_y_las_referencias_pendientes_son_avisos()
    {
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-1", tasa: "30"));
        var r = await _m.ImportarAsync(P.Loans);

        r.IsSuccess.Should().BeTrue("un crédito vivo no se rechaza por usura: el motor topa la tasa del día");
        r.Value.Warnings.Should().ContainSingle(w => w.Row == 2 && w.Column == C.TasaCorrienteEA && w.Code == "Lending.Usury.Exceeded");

        _m.Archivo(P.Installments, MigracionDePrueba.Cuota("CR-9", 1, "2026-12-15", "1000"));
        var cuotas = await _m.ImportarAsync(P.Installments);
        cuotas.IsSuccess.Should().BeTrue();
        cuotas.Value.Warnings.Should().ContainSingle(w => w.Column == C.NumeroDeCredito && w.Code == ImportMigrationPartCommandHandler.CodigoReferenciaPendiente);
    }

    [Fact]
    public async Task Una_carga_que_no_es_borrador_no_recibe_partes_y_una_parte_desconocida_no_existe()
    {
        var carga = await _m.Db.MigrationLoads.SingleAsync();
        carga.Status = MigrationLoadStatus.Applied;
        await _m.Db.SaveChangesAsync();
        _m.Db.ChangeTracker.Clear();
        _m.Archivo(P.Loans, MigracionDePrueba.Credito("CR-1"));

        (await _m.ImportarAsync(P.Loans)).Error.Code.Should().Be(ErroresDeActivacion.CodigoCargaNoBorrador);
        (await _m.ImportarAsync("prestamos")).Error.Code.Should().Be("Lending.Template.NotFound");
    }

    [Fact]
    public async Task La_auditoria_lleva_la_plantilla_el_modo_y_el_SHA_en_el_modulo_Lending()
    {
        _m.Archivo(P.Balances, MigracionDePrueba.Saldo("CR-1", "Principal", "1000000"));

        await _m.ImportarAsync(P.Balances, ModoDeImportacion.Review, contenido: "saldos");
        var r = await _m.ImportarAsync(P.Balances, contenido: "saldos");

        var eventos = _m.Eventos.Concat((await _m.G.EventosAsync()).Select(a => new IngenIA365ERP.Application.Common.Interfaces.AuditLogCommand { Action = a, Module = ModuloDeAuditoria.Lending }))
            .ToList();
        eventos.Should().Contain(e => e.Action == EjecutorDeImportacion.EventoRevisada && e.Module == ModuloDeAuditoria.Lending);
        var aplicada = eventos.First(e => e.Action == EjecutorDeImportacion.EventoAplicada);
        aplicada.Module.Should().Be(ModuloDeAuditoria.Lending);
        if (aplicada.Metadata is { } metadata)
        {
            metadata["FileSha256"].Should().Be(r.Value.FileSha256);
            metadata["Template"].Should().Be("lending.migration.balances");
        }
    }

    [Fact]
    public async Task Un_bloqueo_automatico_no_se_migra_y_el_fallecimiento_pide_la_aseguradora()
    {
        _m.Archivo(P.Blocks,
            MigracionDePrueba.Fila((C.Persona, "52000001"), (C.Motivo, "Arrears"), (C.Desde, "2026-01-01"), (C.Soporte, "x")),
            MigracionDePrueba.Fila((C.Persona, "52000001"), (C.Motivo, "7"), (C.Desde, "2026-01-01"), (C.Soporte, "Acta de defunción")));

        var errores = MigracionDePrueba.Errores(await _m.ImportarAsync(P.Blocks));

        errores.Should().Contain(e => e.Row == 2 && e.Column == C.Motivo);
        errores.Should().Contain(e => e.Row == 3 && e.Column == C.Aseguradora && e.Code == ImportErrors.CellRequired, "el motivo admite el número del enum (Deceased 7)");
    }
}
