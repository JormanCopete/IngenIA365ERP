using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Lending.Origination;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Origination;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Pedido del dueño del 2026-11-23: una solicitud devuelta para corregir sólo cambia lo que el parámetro
/// <c>Originacion.GruposEditablesAlDevolver</c> de su línea deja (de fábrica, todo menos la línea y la oficina); al devolverla queda la foto de lo
/// que decía y el detalle muestra qué cambió. Un borrador se edita entero.
/// </summary>
public sealed class CorregirSolicitudDevueltaTests : IAsyncLifetime
{
    private OriginacionDePrueba _o = null!;

    public async Task InitializeAsync() => _o = await OriginacionDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private async Task<CreditApplicationDto> DevueltaAsync()
    {
        var s = await _o.RadicadaAsync();
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        var ruta = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);
        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId,
            CreditDecisionOutcome.Return, "Corrija el monto y el destino", null, ruta.Value.ContentSha256) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return (await _o.Leer().Handle(new GetCreditApplicationQuery(s.PublicId), default)).Value;
    }

    private async Task<Result<CreditApplicationDto>> CorregirAsync(CreditApplicationDto s, CreditApplicationInput entrada)
    {
        var r = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, entrada) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task ParametroAsync(string valor)
    {
        _o.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.OriginacionGruposEditablesAlDevolver, ScopeKind = ParameterScopeKind.None,
            Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
        });
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Devuelta_guarda_la_foto_y_muestra_lo_que_cambio()
    {
        var s = await DevueltaAsync();
        s.Status.Should().Be(CreditApplicationStatus.Returned);
        s.EditableGroups.Should().NotContain(["Linea", "Oficina"]).And.Contain(["Condiciones", "Destino", "Documentos"]);
        (await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId)).ReturnSnapshot.Should().NotBeNullOrWhiteSpace();
        _o.Db.ChangeTracker.Clear();

        var r = await CorregirAsync(s, _o.Solicitud(2_500_000m) with { Purpose = "Compra de vehículo" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var leida = (await _o.Leer().Handle(new GetCreditApplicationQuery(s.PublicId), default)).Value;
        leida.ChangesSinceReturn.Select(c => (c.Field, c.Before, c.After)).Should().Contain(
            [("Monto", "3000000", "2500000"), ("Destino", "Libre inversión", "Compra de vehículo")]);
    }

    [Fact]
    public async Task Lo_que_el_parametro_deja_fijo_no_cambia_y_lo_demas_si()
    {
        var s = await DevueltaAsync();
        await ParametroAsync("Destino,Documentos");

        var monto = await CorregirAsync(s, _o.Solicitud(2_500_000m));
        monto.Error.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible);
        OriginacionDePrueba.Datos(monto.Error).GetProperty("groups")[0].GetString().Should().Be("Condiciones");

        var destino = await CorregirAsync(s, _o.Solicitud() with { Purpose = "Educación" });
        destino.IsSuccess.Should().BeTrue(destino.IsFailure ? $"{destino.Error.Code}: {destino.Error.Message}" : string.Empty);
        destino.Value.Purpose.Should().Be("Educación");
    }

    [Fact]
    public async Task Un_codeudor_nuevo_es_un_cambio_de_codeudores()
    {
        var s = await DevueltaAsync();
        await ParametroAsync("Condiciones");

        var r = await CorregirAsync(s, _o.Solicitud(3_000_000m, 24, null, _o.Beto));

        r.Error.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible);
        OriginacionDePrueba.Datos(r.Error).GetProperty("groups")[0].GetString().Should().Be("Codeudores");
    }

    [Fact]
    public async Task Documentos_y_garantias_se_corrigen_solo_si_el_parametro_los_deja()
    {
        var s = await DevueltaAsync();
        await ParametroAsync("Condiciones");
        var fila = await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);

        var documentos = await _o.Editables().ExigirAsync(fila, GrupoEditableAlDevolver.Documentos, OriginacionDePrueba.Hoy, default);
        documentos!.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible);
        (await _o.Editables().ExigirAsync(fila, GrupoEditableAlDevolver.Condiciones, OriginacionDePrueba.Hoy, default)).Should().BeNull();

        fila.Status = CreditApplicationStatus.Draft;
        (await _o.Editables().ExigirAsync(fila, GrupoEditableAlDevolver.Documentos, OriginacionDePrueba.Hoy, default)).Should().BeNull("el borrador se edita entero");
    }

    [Fact]
    public async Task El_borrador_se_edita_entero()
    {
        var s = await _o.BorradorAsync();
        var leida = (await _o.Leer().Handle(new GetCreditApplicationQuery(s.PublicId), default)).Value;
        leida.EditableGroups.Should().HaveCount(GruposEditablesAlDevolver.Nombres.Count);
        leida.ChangesSinceReturn.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Condiciones,Destino", GrupoEditableAlDevolver.Condiciones | GrupoEditableAlDevolver.Destino)]
    [InlineData("garantias", GrupoEditableAlDevolver.Garantias)]
    [InlineData("", null)]
    [InlineData("Condiciones,Inventado", null)]
    public void El_texto_del_parametro_se_lee(string texto, GrupoEditableAlDevolver? esperado) =>
        GruposEditablesAlDevolver.Leer(texto).Should().Be(esperado);

    [Fact]
    public void El_defecto_deja_fijas_la_linea_y_la_oficina()
    {
        var defecto = GruposEditablesAlDevolver.Leer(GruposEditablesAlDevolver.Defecto)!.Value;
        defecto.HasFlag(GrupoEditableAlDevolver.Linea).Should().BeFalse();
        defecto.HasFlag(GrupoEditableAlDevolver.Oficina).Should().BeFalse();
        defecto.HasFlag(GrupoEditableAlDevolver.Condiciones).Should().BeTrue();
        System.Text.RegularExpressions.Regex.IsMatch(GruposEditablesAlDevolver.Defecto, GruposEditablesAlDevolver.Patron).Should().BeTrue();
    }
}
