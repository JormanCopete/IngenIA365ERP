using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Imports;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using C = IngenIA365ERP.Application.Lending.Imports.PayrollAgreementsTemplate.Columnas;
using H = IngenIA365ERP.Application.Lending.Imports.PayrollAgreementsTemplate.Hojas;

namespace IngenIA365ERP.Application.Tests.Lending.Libranza;

/// <summary>
/// Feature 013, K2, T0808 (FR-108; api.md §13.1; plantillas.md §8; I13, C84): los convenios de libranza. Crear exige una pagaduría jurídica del maestro,
/// el registro de operador de libranza en el perfil regulatorio vigente y un código no repetido; la nómina propia es una sola y su pagaduría es la
/// cooperativa. Una versión nueva no se cruza con la anterior (que cierra la víspera), sus formatos son de los ámbitos de libranza, su tope y su ventana
/// no son menos estrictos que la ley, y la versión que respaldó un archivo enviado no se recorta. Los contactos son personas del maestro y la consulta
/// muestra nombre, papel y contacto institucional, sin documento. La plantilla es todo o nada, con errores por fila y columna, y no crea personas.
/// </summary>
public sealed class PayrollAgreementCommandsTests : IAsyncLifetime
{
    private LibranzaDePrueba _l = null!;

    public async Task InitializeAsync() => _l = await LibranzaDePrueba.CrearAsync(conConvenio: false);

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    private async Task<Result<PayrollAgreementDto>> CrearAsync(string codigo, Guid pagaduria, bool nominaPropia = false, PayrollAgreementVersionInput? version = null)
    {
        var r = await _l.Crear().Handle(new CreatePayrollAgreementCommand(codigo, "Convenio " + codigo, pagaduria, nominaPropia, version ?? _l.Version())
        {
            OperationKey = Guid.NewGuid(),
        }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task<Result<PayrollAgreementDto>> VersionarAsync(Guid convenio, PayrollAgreementVersionInput version)
    {
        var r = await _l.Versionar().Handle(new AddPayrollAgreementVersionCommand(convenio, version) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    // ------------------------------------------------------------------------------------------------------------- crear --

    [Fact]
    public async Task Crea_el_convenio_con_su_version_y_sus_codigos_y_lo_muestra_en_la_lista()
    {
        var r = await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var c = r.Value;
        (c.Code, c.Name, c.IsOwnPayroll, c.Status, c.IsActive).Should().Be(("PAGMUN01", "Convenio PAGMUN01", false, "Active", true));
        c.Payer.LegalName.Should().Be("Secretaría de Educación");
        c.Payer.TaxId.Should().Be("800111222", "el NIT de la pagaduría no es un dato personal");
        var v = c.Versions.Should().ContainSingle().Subject;
        (v.CutoffDay, v.SendDay, v.PayDay, v.SendLeadDays, v.DeductionCap, v.NotAppliedAction, v.AlternateRepaymentChannel)
            .Should().Be((20, (int?)25, 30, 5, (decimal?)0.4m, "MovedToCashPayment", "Cashier"));
        (v.OutboundFormatCode, v.ResultFormatCode).Should().Be(("PAGMUN-IDA", "PAGMUN-VUELTA"));
        v.ResultCodes.Select(x => (x.Code, x.Outcome, x.Reason)).Should().Equal(("00", "Deducted", (string?)null), ("07", "NotDeducted", "Leave"));

        var lista = (await _l.Listar().Handle(new ListPayrollAgreementsQuery(), default)).Value;
        var fila = lista.Should().ContainSingle().Subject;
        (fila.CurrentVersion!.CutoffDay, fila.CurrentVersion.SendDay, fila.CurrentVersion.PayDay).Should().Be((20, (int?)25, 30));
        fila.ActiveLoans.Should().Be(0);
    }

    [Fact]
    public async Task La_pagaduria_es_una_persona_juridica_del_maestro()
    {
        var r = await CrearAsync("ANA", _l.Ana.PublicId);

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoPagadorNoJuridico);
        (await CrearAsync("NADIE", Guid.NewGuid())).Error.Code.Should().Be("Core.Person.NotFound");
        (await _l.Db.PayrollAgreements.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_registro_de_operador_de_libranza_no_se_crea()
    {
        _l.ConRegistroDeOperador(false);

        (await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId)).Error.Code.Should().Be(ErroresDeLibranza.CodigoSinRegistroDeOperador);
    }

    [Fact]
    public async Task Un_codigo_vivo_repetido_es_CodigoDuplicado()
    {
        (await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId)).IsSuccess.Should().BeTrue();

        var r = await CrearAsync("pagmun01", _l.OtraPagaduria.PublicId);

        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("Convenio PAGMUN01");
    }

    [Fact]
    public async Task La_nomina_propia_es_una_sola_y_su_pagaduria_es_la_cooperativa()
    {
        (await CrearAsync("NOMINA", _l.Pagaduria.PublicId, nominaPropia: true)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNominaPropiaNoEsLaCooperativa);
        (await CrearAsync("NOMINA", _l.Cooperativa.PublicId, nominaPropia: true)).IsSuccess.Should().BeTrue();

        (await CrearAsync("NOMINA2", _l.Cooperativa.PublicId, nominaPropia: true)).Error.Code.Should().Be(ErroresDeLibranza.CodigoNominaPropiaRepetida);
    }

    // ---------------------------------------------------------------------------------------------------------- versiones --

    [Fact]
    public async Task Una_version_nueva_cierra_la_anterior_la_vispera_y_no_se_cruza()
    {
        var c = (await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId)).Value;

        (await VersionarAsync(c.PublicId, _l.Version(new DateOnly(2026, 11, 1)))).Error.Code.Should().Be(ErroresDeLibranza.CodigoVersionCruzada);
        var r = await VersionarAsync(c.PublicId, _l.Version(new DateOnly(2027, 3, 1)) with { DeductionCap = 0.3m, Reason = "Otrosí 1" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Versions.Select(v => (v.ValidFrom, v.ValidTo)).Should().Equal(
            (new DateOnly(2026, 11, 1), (DateOnly?)new DateOnly(2027, 2, 28)), (new DateOnly(2027, 3, 1), (DateOnly?)null));
        (await VersionarAsync(c.PublicId, _l.Version(new DateOnly(2027, 2, 1)))).Error.Code.Should().Be(ErroresDeLibranza.CodigoVersionCruzada);
    }

    [Fact]
    public async Task Los_formatos_son_de_los_ambitos_de_libranza()
    {
        var c = (await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId)).Value;
        var cambiados = _l.Version(new DateOnly(2027, 3, 1)) with { OutboundFormatPublicId = _l.Vuelta.PublicId, ResultFormatPublicId = _l.Ida.PublicId };

        var r = await VersionarAsync(c.PublicId, cambiados);

        r.Error.Code.Should().Be("Core.BankFileFormat.ScopeMismatch");
        (await CrearAsync("OTRO", _l.OtraPagaduria.PublicId, version: cambiados)).Error.Code.Should().Be("Core.BankFileFormat.ScopeMismatch");
    }

    [Theory]
    [InlineData(0.6, null)]
    [InlineData(null, 15)]
    public async Task El_tope_y_la_ventana_no_son_menos_estrictos_que_la_ley(double? tope, int? ventana)
    {
        var version = _l.Version() with { DeductionCap = tope is null ? null : (decimal)tope, TransferWindowDays = ventana };

        var r = await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId, version: version);

        r.Error.Code.Should().Be("Lending.Parameter.BelowLegalFloor");
    }

    [Fact]
    public async Task La_version_que_respaldo_un_archivo_enviado_no_se_recorta()
    {
        _l.Convenio = await _l.ConvenioAsync("PAGMUN01", _l.Pagaduria);
        _l.Vincular(_l.Credito, _l.Convenio, "EMP-0001");
        var archivo = await _l.ArchivoAsync(2027, 1);
        await _l.ConfirmarYConsumirAsync(archivo.PublicId);

        var r = await VersionarAsync(_l.Convenio.PublicId, _l.Version(new DateOnly(2027, 1, 15)));

        r.Error.Code.Should().Be(ErroresDeLibranza.CodigoVersionEnUso);
        (await VersionarAsync(_l.Convenio.PublicId, _l.Version(new DateOnly(2027, 2, 1)))).IsSuccess.Should().BeTrue("la versión nueva empieza después del período enviado");
    }

    // ---------------------------------------------------------------------------------------------------------- contactos --

    [Fact]
    public async Task Los_contactos_son_personas_del_maestro_y_se_muestran_sin_documento()
    {
        var c = (await CrearAsync("PAGMUN01", _l.Pagaduria.PublicId)).Value;
        _l.D.O.Beto.Email = "beto@pagaduria.test";
        _l.D.O.Beto.Phone1 = "6025551234";
        _l.Db.People.Update(_l.D.O.Beto);
        await _l.Db.SaveChangesAsync();
        _l.Db.ChangeTracker.Clear();

        var r = await _l.Contactos().Handle(new SetPayrollAgreementContactsCommand(c.PublicId,
            [new PayrollAgreementContactInput(_l.D.O.Beto.PublicId, "Payroll", IsPrimary: true), new PayrollAgreementContactInput(_l.D.O.Dario.PublicId, "Treasury")])
        {
            OperationKey = Guid.NewGuid(),
        }, default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var beto = r.Value.Should().HaveCount(2).And.ContainSingle(x => x.IsPrimary).Subject;
        (beto.Name, beto.Role, beto.Email, beto.Phone).Should().Be(("Beto Codeudor", "Payroll", "beto@pagaduria.test", "6025551234"));
        typeof(PayrollAgreementContactDto).GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("Tax") || n.Contains("Document"),
            "bajo PayrollAgreements.View el contacto no lleva documento (C84)");

        _l.Db.ChangeTracker.Clear();
        var sinDario = await _l.Contactos().Handle(new SetPayrollAgreementContactsCommand(c.PublicId,
            [new PayrollAgreementContactInput(_l.D.O.Beto.PublicId, "Payroll", IsPrimary: true)]) { OperationKey = Guid.NewGuid() }, default);
        sinDario.Value.Should().ContainSingle(x => x.PersonPublicId == _l.D.O.Beto.PublicId);
        (await _l.Contactos().Handle(new SetPayrollAgreementContactsCommand(c.PublicId, [new PayrollAgreementContactInput(Guid.NewGuid())])
            { OperationKey = Guid.NewGuid() }, default)).Error.Code.Should().Be("Core.Person.NotFound");
    }

    // ------------------------------------------------------------------------------------------------------------ plantilla --

    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly Dictionary<string, TablaLeida> _hojas = new(StringComparer.OrdinalIgnoreCase);

    private void Hoja(string nombre, string[] encabezados, params string?[][] filas) =>
        _hojas[nombre] = new TablaLeida(encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    private async Task<Result<ImportResultDto>> ImportarAsync(ModoDeImportacion modo = ModoDeImportacion.Apply)
    {
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(_hojas.Keys.ToList())));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(Result.Success(_hojas.TryGetValue(ci.ArgAt<string?>(2) ?? string.Empty, out var t) ? t : new TablaLeida([], [], "xlsx"))));
        _l.G.Permisos.EsMaestroGlobal.Returns(true);
        var servicios = new ServiceCollection().AddSingleton(_l.G.Mongo).BuildServiceProvider();
        var ejecutor = new EjecutorDeImportacion(_l.Db, _lector, _l.G.Permisos, servicios);
        var r = await new ImportPayrollAgreementsCommandHandler(_l.Db, ejecutor, _l.Convenios(), _l.Alta(), _l.G.Reloj)
            .Handle(new ImportPayrollAgreementsCommand(modo, new ArchivoDeImportacion("convenios.xlsx", [1, 2, 3]), "Convenios 2027"), default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    private static readonly string[] Convenios =
    [
        C.Codigo, C.VigenteDesde, C.VigenteHasta, C.Pagaduria, C.EsNominaPropia, C.Nombre, C.DiaDeCorte, C.DiaDeEnvio, C.DiaDePago, C.AntelacionDias,
        C.FormatoDeIda, C.FormatoDeVuelta, C.TopeDeDescuento, C.IncluyeVencido, C.VentanaDeGiroDias, C.AccionNoAplicado, C.FormaDePagoAlterna, C.Notas,
    ];

    private string?[] Convenio(string codigo, string desde, string pagaduria, string? tope = "40") =>
        [codigo, desde, null, pagaduria, "no", "Convenio " + codigo, "20", "25", "30", "5", "PAGMUN-IDA", "PAGMUN-VUELTA", tope, "no", null, null, null, null];

    [Fact]
    public void La_plantilla_tiene_las_hojas_y_columnas_de_plantillas_8()
    {
        var d = PayrollAgreementsTemplate.Definicion;
        d.Clave.Should().Be("lending.payroll-agreements");
        d.Hojas.Select(h => h.Nombre).Should().Equal(H.Convenios, H.Contactos, H.CodigosDeRespuesta);
        d.Hojas[0].Columnas.Select(c => c.Nombre).Should().Equal(Convenios);
        CatalogoDePlantillasDeCartera.Buscar("lending.payroll-agreements")!.Definicion.Should().BeSameAs(d);
    }

    [Fact]
    public async Task La_plantilla_crea_convenios_versiones_contactos_y_codigos()
    {
        Hoja(H.Convenios, Convenios, Convenio("PAGMUN01", "2026-11-01", "800111222"), Convenio("PAGMUN01", "2027-03-01", "800111222", "30"),
            Convenio("GOBVAL", "2026-11-01", "800333444"));
        Hoja(H.Contactos, [C.Convenio, C.Persona, C.Cargo, C.RecibeArchivo], ["PAGMUN01", _l.D.O.Beto.TaxId, null, "sí"]);
        Hoja(H.CodigosDeRespuesta, [C.Convenio, C.VigenteDesde, C.Codigo, C.Descripcion, C.Descontado, C.Motivo],
            ["PAGMUN01", "2026-11-01", "07", "Licencia", "no", "Leave"], ["PAGMUN01", "2026-11-01", "00", "Descontado", "sí", null]);

        (await ImportarAsync(ModoDeImportacion.Review)).IsSuccess.Should().BeTrue();
        (await _l.Db.PayrollAgreements.AnyAsync()).Should().BeFalse("revisar no guarda");
        var r = await ImportarAsync();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        var pagmun = await _l.Db.PayrollAgreements.SingleAsync(a => a.Code == "PAGMUN01");
        var versiones = await _l.Db.PayrollAgreementVersions.Where(v => v.PayrollAgreementId == pagmun.Id).OrderBy(v => v.ValidFrom).ToListAsync();
        versiones.Select(v => (v.ValidFrom, v.ValidTo, v.DeductionCapFraction)).Should().Equal(
            (new DateOnly(2026, 11, 1), (DateOnly?)new DateOnly(2027, 2, 28), (decimal?)0.4m), (new DateOnly(2027, 3, 1), (DateOnly?)null, (decimal?)0.3m));
        (await _l.Db.PayrollAgreementResultCodes.Where(c => c.PayrollAgreementVersionId == versiones[0].Id).Select(c => c.PaymasterCode).ToListAsync())
            .Should().BeEquivalentTo(["00", "07"]);
        (await _l.Db.PayrollAgreementContacts.SingleAsync()).Should().Match<Domain.Entities.Lending.Payroll.PayrollAgreementContact>(
            c => c.PersonId == _l.D.O.Beto.Id && c.Role == "Payroll" && c.IsPrimary);
        (await _l.Db.PayrollAgreements.CountAsync()).Should().Be(2);
        (await ImportarAsync()).Value.Sheets[0].Unchanged.Should().Be(3, "la misma plantilla otra vez no cambia nada");
    }

    [Fact]
    public async Task La_plantilla_es_todo_o_nada_con_errores_por_fila_y_columna_y_no_crea_personas()
    {
        var personas = await _l.Db.People.CountAsync();
        Hoja(H.Convenios, Convenios, Convenio("PAGMUN01", "2026-11-01", "800111222"), Convenio("NOEXISTE", "2026-11-01", "999999999"),
            Convenio("ANA", "2026-11-01", _l.Ana.TaxId!), Convenio("TOPE", "2026-11-01", "800333444", "60"));

        var r = await ImportarAsync();

        r.Error.Code.Should().Be(ImportErrors.InvalidCode);
        var errores = r.Error.Should().BeOfType<ErrorConDatos>().Subject.Data.Should().BeOfType<ImportResultDto>().Subject.Errors;
        errores.Should().Contain(e => e.Row == 3 && e.Column == C.Pagaduria && e.Code == ImportErrors.CellNotFound);
        errores.Should().Contain(e => e.Row == 4 && e.Column == C.Pagaduria && e.Code == ErroresDeLibranza.CodigoPagadorNoJuridico);
        errores.Should().Contain(e => e.Row == 5 && e.Column == C.TopeDeDescuento && e.Code == "Lending.Parameter.BelowLegalFloor");
        (await _l.Db.PayrollAgreements.AnyAsync()).Should().BeFalse("todo o nada");
        (await _l.Db.People.CountAsync()).Should().Be(personas, "ninguna plantilla crea personas");
    }
}
