using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Files;
using IngenIA365ERP.Application.Common.Interfaces.Security;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Transport.Imports;
using IngenIA365ERP.Application.Transport.Vehicles;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using P = IngenIA365ERP.Application.Transport.Imports.PlantillaDeVehiculos;

namespace IngenIA365ERP.Application.Tests.Transport.Imports;

/// <summary>
/// Feature 020 (T067; R3, R8; FR-017): la plantilla de vehículos con la mecánica de la 016. Revisar no guarda; un error lleva su fila y su
/// columna; con un error no se guarda nada; al actualizar sólo cambian las celdas con valor; el propietario no se cambia con esta plantilla.
/// </summary>
public class PlantillaDeVehiculosTests
{
    private static readonly string[] Encabezados = [P.PlacaCol, P.NumeroInterno, P.RadioMovil, P.TipoDocumentoPropietario, P.DocumentoPropietario, P.Color];

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly ITabularFileReader _lector = Substitute.For<ITabularFileReader>();
    private readonly ICurrentUserPermissions _permisos = Substitute.For<ICurrentUserPermissions>();
    private readonly IDateTimeService _reloj = Substitute.For<IDateTimeService>();
    private TablaLeida _tabla = new([], [], "xlsx");

    public PlantillaDeVehiculosTests()
    {
        _reloj.HoyLocal.Returns(new DateOnly(2026, 11, 8));
        _reloj.UtcNow.Returns(new DateTime(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc));
        _permisos.EsMaestroGlobal.Returns(false);
        _permisos.ListAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyCollection<string>>(["Transport.Vehicles.Import"]));
        _lector.ListarHojasAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success<IReadOnlyList<string>>(["Datos"])));
        _lector.LeerHojaAsync(Arg.Any<byte[]>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Result.Success(_tabla)));

        _db.People.Add(new Person { IdType = "CC", TaxId = "1010101010", Username = "1010101010", FirstName = "Ana", LastName = "Propietaria", CreatedBy = "test" });
        _db.SaveChanges();
    }

    private void Filas(params string?[][] filas) =>
        _tabla = new TablaLeida(Encabezados, filas.Select((f, i) => new FilaLeida(i + 2, f)).ToList(), "xlsx");

    /// <summary>El resultado de la importación: el éxito o, si aplicar encontró errores, el que viaja en el fallo (todo o nada).</summary>
    private async Task<ImportResultDto> CorrerAsync(ModoDeImportacion modo, SiYaExiste existentes = SiYaExiste.Actualizar)
    {
        var r = await EjecutarAsync(modo, existentes);
        if (r.IsSuccess) return r.Value;
        r.Error.Should().BeOfType<ErrorConDatos>($"un fallo sin resultado: {r.Error.Code} {r.Error.Message}");
        return ((ErrorConDatos)r.Error).Data.Should().BeOfType<ImportResultDto>().Subject;
    }

    private Task<Result<ImportResultDto>> EjecutarAsync(ModoDeImportacion modo, SiYaExiste existentes)
    {
        var ejecutor = new EjecutorDeImportacion(_db, _lector, _permisos,
            new ServiceCollection().AddSingleton(Substitute.For<IAuditService>()).BuildServiceProvider());
        var handler = new ImportVehiclesCommandHandler(_db, ejecutor, new VehicleRegistrar(_db, _reloj));
        return handler.Handle(new ImportVehiclesCommand(modo, new ArchivoDeImportacion("vehiculos.xlsx", [1, 2, 3]), "Carga de prueba", existentes)
        {
            OperationKey = Guid.NewGuid(),
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Revisar_no_guarda_y_dice_lo_que_crearia()
    {
        Filas(["abc-123", "0001", "101", "CC", "1010101010", "Amarillo"]);

        var r = await CorrerAsync(ModoDeImportacion.Review);

        r.Valid.Should().BeTrue(string.Join("; ", r.Errors.Select(e => e.Message)));
        r.Applied.Should().BeFalse();
        r.Sheets.Single().Created.Should().Be(1);
        _db.Vehicles.Should().BeEmpty();
    }

    [Fact]
    public async Task Aplicar_crea_con_la_placa_normalizada()
    {
        Filas(["abc-123", "0001", "101", "CC", "1010101010", "Amarillo"]);

        var r = await CorrerAsync(ModoDeImportacion.Apply);

        r.Applied.Should().BeTrue(string.Join("; ", r.Errors.Select(e => e.Message)));
        _db.Vehicles.Should().ContainSingle().Which.Plate.Should().Be("ABC123");
    }

    [Fact]
    public async Task Un_propietario_que_no_existe_es_error_en_su_fila_y_columna_y_no_se_guarda_nada()
    {
        Filas(
            ["AAA111", "0001", null, "CC", "1010101010", null],
            ["BBB222", "0002", null, "CC", "9999999", null]);

        var r = await CorrerAsync(ModoDeImportacion.Apply);

        r.Applied.Should().BeFalse();
        var error = r.Errors.Should().ContainSingle().Subject;
        (error.Row, error.Column).Should().Be((3, P.DocumentoPropietario));
        _db.Vehicles.Should().BeEmpty("todo o nada: la fila buena tampoco se guarda");
    }

    [Fact]
    public async Task Una_placa_repetida_en_el_archivo_es_error()
    {
        Filas(["AAA111", "0001", null, "CC", "1010101010", null], ["aaa-111", "0002", null, "CC", "1010101010", null]);

        var r = await CorrerAsync(ModoDeImportacion.Review);

        r.Valid.Should().BeFalse();
        r.Errors.Should().Contain(e => e.Row == 3 && e.Column == P.PlacaCol);
    }

    [Fact]
    public async Task Al_actualizar_solo_cambian_las_celdas_con_valor()
    {
        Filas(["AAA111", "0001", "101", "CC", "1010101010", "Amarillo"]);
        (await CorrerAsync(ModoDeImportacion.Apply)).Applied.Should().BeTrue();

        Filas(["AAA111", null, null, null, null, "Blanco"]);
        var r = await CorrerAsync(ModoDeImportacion.Apply);

        r.Applied.Should().BeTrue(string.Join("; ", r.Errors.Select(e => e.Message)));
        var v = _db.Vehicles.Single();
        v.Color.Should().Be("Blanco");
        v.InternalNumber.Should().Be("0001");
        v.RadioCode.Should().Be("101");
    }

    [Fact]
    public async Task Con_conservar_lo_existente_no_se_toca()
    {
        Filas(["AAA111", "0001", null, "CC", "1010101010", "Amarillo"]);
        (await CorrerAsync(ModoDeImportacion.Apply)).Applied.Should().BeTrue();

        Filas(["AAA111", null, null, null, null, "Blanco"]);
        var r = await CorrerAsync(ModoDeImportacion.Apply, SiYaExiste.Conservar);

        r.Sheets.Single().Unchanged.Should().Be(1);
        _db.Vehicles.Single().Color.Should().Be("Amarillo");
    }
}
