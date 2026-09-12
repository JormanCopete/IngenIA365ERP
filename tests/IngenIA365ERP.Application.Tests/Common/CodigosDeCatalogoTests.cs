using FluentAssertions;
using IngenIA365ERP.Application.Common.Catalogos;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Core.Branches.Commands.CreateBranch;
using IngenIA365ERP.Application.Core.Branches.Commands.UpdateBranch;
using IngenIA365ERP.Application.Payroll.HealthInsuranceProviders.Commands.CreateHealthInsuranceProvider;
using IngenIA365ERP.Domain.Entities.Core;
using NSubstitute;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// El código de un catálogo es la nomenclatura de la cooperativa: alfanumérico, en
/// mayúsculas, único en su tabla. Hasta el 2026-09-12 nómina lo exigía numérico y Core ni
/// lo mostraba; el duplicado lo rechazaba el índice de la base con un error crudo.
/// </summary>
public class CodigosDeCatalogoTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private readonly ICurrentUserService _usuario = Substitute.For<ICurrentUserService>();

    public CodigosDeCatalogoTests()
    {
        _reloj.UtcNow.Returns(new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc));
        _usuario.UserName.Returns("prueba");
    }

    [Theory]
    [InlineData(" eps-01 ", "EPS-01")]
    [InlineData("sura", "SURA")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void Se_normaliza_a_mayusculas_sin_espacios_y_vacio_es_nulo(string? entrada, string? esperado) =>
        CodigoDeCatalogo.Normalizar(entrada).Should().Be(esperado);

    [Fact]
    public async Task Nomina_el_codigo_es_alfanumerico_y_se_guarda_en_mayusculas()
    {
        var handler = new CreateHealthInsuranceProviderCommandHandler(_db, _reloj, _usuario);

        var r = await handler.Handle(new CreateHealthInsuranceProviderCommand { Code = "sura", Name = "EPS Sura", TaxId = "800088702" }, CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        _db.HealthInsuranceProviders.Single().Code.Should().Be("SURA");
    }

    [Fact]
    public async Task Nomina_el_codigo_repetido_se_rechaza_con_mensaje_y_nombre_del_existente()
    {
        var handler = new CreateHealthInsuranceProviderCommandHandler(_db, _reloj, _usuario);
        await handler.Handle(new CreateHealthInsuranceProviderCommand { Code = "SURA", Name = "EPS Sura", TaxId = "800088702" }, CancellationToken.None);

        var r = await handler.Handle(new CreateHealthInsuranceProviderCommand { Code = "sura", Name = "Otra", TaxId = "1" }, CancellationToken.None);

        r.IsSuccess.Should().BeFalse();
        r.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        r.Error.Message.Should().Contain("SURA").And.Contain("EPS Sura");
    }

    [Fact]
    public void Nomina_el_validador_exige_codigo_con_el_patron_y_el_largo()
    {
        var v = new CreateHealthInsuranceProviderCommandValidator();

        v.Validate(new CreateHealthInsuranceProviderCommand { Code = "", Name = "x" }).IsValid.Should().BeFalse("obligatorio");
        v.Validate(new CreateHealthInsuranceProviderCommand { Code = "CON ESPACIO", Name = "x" }).IsValid.Should().BeFalse("sin espacios");
        v.Validate(new CreateHealthInsuranceProviderCommand { Code = new string('A', 11), Name = "x" }).IsValid.Should().BeFalse("hasta 10");
        v.Validate(new CreateHealthInsuranceProviderCommand { Code = "EPS_01.A-B", Name = "x" }).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Core_el_codigo_es_opcional_pero_si_viene_es_unico_y_va_en_mayusculas()
    {
        var crear = new CreateBranchCommandHandler(_db, _reloj, _usuario);

        (await crear.Handle(new CreateBranchCommand { Name = "Sin código" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await crear.Handle(new CreateBranchCommand { Name = "Tampoco" }, CancellationToken.None)).IsSuccess.Should().BeTrue("dos sin código no chocan");
        (await crear.Handle(new CreateBranchCommand { Code = "cali", Name = "Cali" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        _db.Branches.Single(b => b.Name == "Cali").LegacyCode.Should().Be("CALI");

        var repetido = await crear.Handle(new CreateBranchCommand { Code = "CALI", Name = "Cali norte" }, CancellationToken.None);
        repetido.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
        repetido.Error.Message.Should().Contain("Cali");
    }

    [Fact]
    public async Task Core_al_editar_el_propio_codigo_no_cuenta_como_duplicado()
    {
        var cali = new Branch { Name = "Cali", LegacyCode = "CALI", CreatedBy = "t" };
        var bogota = new Branch { Name = "Bogotá", LegacyCode = "BOG", CreatedBy = "t" };
        _db.Branches.AddRange(cali, bogota);
        await _db.SaveChangesAsync();
        var editar = new UpdateBranchCommandHandler(_db, _reloj, _usuario);

        (await editar.Handle(new UpdateBranchCommand { PublicId = cali.PublicId, Code = "CALI", Name = "Cali centro" }, CancellationToken.None)).IsSuccess.Should().BeTrue();
        var choque = await editar.Handle(new UpdateBranchCommand { PublicId = cali.PublicId, Code = "bog", Name = "Cali centro" }, CancellationToken.None);

        choque.Error.Code.Should().Be("Catalogo.CodigoDuplicado");
    }

    [Fact]
    public async Task La_busqueda_por_codigo_responde_existe_con_nombre_y_publicId_o_no_existe()
    {
        _db.Branches.Add(new Branch { Name = "Cali", LegacyCode = "CALI", CreatedBy = "t" });
        await _db.SaveChangesAsync();
        var buscar = new BuscarCodigoDeCatalogoQueryHandler(_db);

        var si = await buscar.Handle(new BuscarCodigoDeCatalogoQuery("agencias", "cali"), CancellationToken.None);
        var no = await buscar.Handle(new BuscarCodigoDeCatalogoQuery("agencias", "MED"), CancellationToken.None);
        var desconocido = await buscar.Handle(new BuscarCodigoDeCatalogoQuery("planetas", "X"), CancellationToken.None);

        si.Value.Should().Be(new CodigoDeCatalogoEncontrado(true, _db.Branches.Single().PublicId, "Cali"));
        no.Value.Existe.Should().BeFalse();
        desconocido.IsSuccess.Should().BeFalse();
    }
}
