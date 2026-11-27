using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Application.Common.Parameters.AddParameterVersion;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.Parameters;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Lending.Common;

/// <summary>
/// La excepción por línea de crédito de un parámetro de Cartera (2026-11-26): la pantalla de Parámetros la ofrecía y el servidor respondía
/// «Recurso no encontrado», porque <see cref="ResolutorDeAmbitoDeCartera"/> sólo resolvía el tipo de hecho. Salió al registrar
/// <c>Pagos.DescuentoPorPagoAnticipado</c> para PRESTOR18 en COOFLOPAL.
/// </summary>
public sealed class ResolutorDeAmbitoDeCarteraTests
{
    private static readonly DateOnly Desde = new(2026, 11, 26);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly IPermissionChecker _permisos = Substitute.For<IPermissionChecker>();
    private readonly CreditLine _linea;

    public ResolutorDeAmbitoDeCarteraTests()
    {
        _permisos.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        _linea = new CreditLine { Code = "PRESTOR18", Name = "Préstamo ordinario 1,8" };
        _db.CreditLines.Add(_linea);
        _db.SaveChanges();
    }

    private AddParameterVersionCommandHandler Alta() => new(_db, _permisos, new ResolutorDeAmbitoCompuesto(
            [new ResolutorDeAmbitoDeModulo(ParametrosDeCartera.Modulo, ResolutorDeAmbitoDeCartera.Ambitos, new ResolutorDeAmbitoDeCartera(_db))]),
        new ReglasDeParametrosPorModulo(new ReglasDeParametrosVacias(),
            [new ReglasDeParametrosDeCartera(new LimitesDePrueba().Con(CatalogoDeLimitesLegales.LibranzaVentanaGiroDias, 3), new LectorDeParametros(_db))]));

    private static AddParameterVersionCommand PorLinea(Guid linea) =>
        new(ParametrosDeCartera.Modulo, ParametrosDeCartera.PagosDescuentoPorPagoAnticipado, ParameterScopeKind.CreditLine, linea, null,
            "0.2222222222", Desde, "Descuento de la línea PRESTOR18", null) { OperationKey = Guid.NewGuid() };

    [Fact]
    public async Task Una_vigencia_por_linea_se_registra_con_el_Id_de_la_linea_y_la_lee_el_lector()
    {
        var r = await Alta().Handle(PorLinea(_linea.PublicId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        var fila = await _db.ParameterVersions.SingleAsync(v => v.Key == ParametrosDeCartera.PagosDescuentoPorPagoAnticipado);
        (fila.ScopeKind, fila.ScopeId, fila.Value).Should().Be((ParameterScopeKind.CreditLine, _linea.Id, "0.2222222222"));

        var leido = await new LectorDeParametros(_db).LeerAsync(ParametrosDeCartera.Modulo, ParametrosDeCartera.PagosDescuentoPorPagoAnticipado, Desde,
            ParameterScopeKind.CreditLine, _linea.Id, CancellationToken.None);
        leido.IsSuccess.Should().BeTrue();
        leido.Value.Valor.Should().Be(0.2222222222m);
    }

    [Fact]
    public async Task Una_linea_que_no_existe_sigue_respondiendo_no_encontrado()
    {
        var r = await Alta().Handle(PorLinea(Guid.NewGuid()), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task El_listado_nombra_la_linea_de_la_excepcion()
    {
        var descritas = await new ResolutorDeAmbitoDeCartera(_db).DescribirAsync(ParameterScopeKind.CreditLine, [_linea.Id], CancellationToken.None);

        descritas.Should().ContainSingle().Which.Should().Be(
            new AmbitoDeParametro(ParameterScopeKind.CreditLine, _linea.Id, _linea.PublicId, "PRESTOR18", "Préstamo ordinario 1,8"));
    }
}
