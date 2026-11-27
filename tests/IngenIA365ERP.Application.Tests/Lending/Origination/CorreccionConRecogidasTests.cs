using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Attributions;
using IngenIA365ERP.Application.Lending.Origination;
using IngenIA365ERP.Application.Lending.Payoffs;
using IngenIA365ERP.Domain.Entities.Lending.Loans;
using IngenIA365ERP.Domain.Entities.Parameters;
using IngenIA365ERP.Domain.Enums.Lending;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Lending.Attributions;
using IngenIA365ERP.Domain.Lending.Origination;
using IngenIA365ERP.Domain.Lending.Parameters;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Origination;

/// <summary>
/// Feature 028 (T026, T028, T031; HU2-3, FR-010, FR-011): la foto de la solicitud lee lo que recoge de sus filas en un texto estable —producto, modo y
/// valor pedido—, así que una solicitud devuelta con recogidas se corrige como cualquier otra: se cambian si el grupo «Recogidas» se puede corregir y, si
/// no, se rechaza el cambio pero se admite mandarlas igual; las fotos guardadas antes de la feature, con el JSON de <c>PayoffRequest</c>, comparan sin
/// falsos cambios; y «Qué cambió desde la devolución» lo dice en palabras. (nuevo)
/// </summary>
public sealed class CorreccionConRecogidasTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private OriginacionDePrueba _o = null!;
    private Loan _viejo = null!;
    private Loan _otro = null!;

    public async Task InitializeAsync()
    {
        _o = await OriginacionDePrueba.CrearAsync();
        _viejo = _o.Credito(_o.Ana, 1_000_000m, "CR-VIEJO");
        _otro = _o.Credito(_o.Ana, 500_000m, "CR-OTRO");
    }

    public Task DisposeAsync()
    {
        _o.Dispose();
        return Task.CompletedTask;
    }

    private static PayoffInput Total(Loan credito) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Total);

    private static PayoffInput Parcial(Loan credito, decimal valor) => new(PayoffProductKind.Loan, credito.PublicId, PayoffMode.Partial, valor);

    private CreditApplicationInput Recogiendo(params PayoffInput[] recogidas) => _o.Solicitud() with { Payoffs = recogidas };

    private async Task<CreditApplicationDto> DevueltaAsync(CreditApplicationInput entrada)
    {
        var s = await _o.RadicadaAsync(entrada);
        _o.G.ActuarComo(_o.Analista);
        _o.G.Conceder("Lending.Applications.Read", "Lending.Decisions.Analyst");
        var ruta = await _o.Enrutado().Handle(new GetCreditApplicationRoutingQuery(s.PublicId), default);
        var r = await _o.Decidir().Handle(new RecordCreditDecisionCommand(LendingDecisionSubjects.CreditApplication, s.PublicId,
            CreditDecisionOutcome.Return, "Revise lo que recoge", null, ruta.Value.ContentSha256) { OperationKey = Guid.NewGuid() }, default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();
        _o.G.ActuarComo(_o.Asesora);
        return await LeerAsync(s.PublicId);
    }

    private async Task<CreditApplicationDto> LeerAsync(Guid solicitud)
    {
        var r = await _o.Leer().Handle(new GetCreditApplicationQuery(solicitud), default);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        _o.Db.ChangeTracker.Clear();
        return r.Value;
    }

    private async Task<Result<CreditApplicationDto>> CorregirAsync(CreditApplicationDto s, CreditApplicationInput entrada)
    {
        var r = await _o.Actualizar().Handle(new UpdateCreditApplicationCommand(s.PublicId, entrada) { OperationKey = Guid.NewGuid() }, default);
        _o.Db.ChangeTracker.Clear();
        return r;
    }

    private async Task GruposAsync(string valor)
    {
        _o.Db.ParameterVersions.Add(new ParameterVersion
        {
            Module = ParametrosDeCartera.Modulo, Key = ParametrosDeCartera.OriginacionGruposEditablesAlDevolver, ScopeKind = ParameterScopeKind.None,
            Value = valor, ValidFrom = new DateOnly(2026, 1, 1), Reason = "prueba", CreatedBy = "test",
        });
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();
    }

    // -------------------------------------------------------------------------------------------------------------- foto --

    [Fact]
    public void El_texto_estable_dice_producto_modo_y_valor_en_su_orden()
    {
        FotoDeSolicitud.TextoDeRecogidas([]).Should().BeNull("sin recogidas, la foto se ve como antes de la feature");
        FotoDeSolicitud.TextoDeRecogidas(
            [
                new RecogidaEnLaFoto("CR-1", PayoffMode.Total, 999m),
                new RecogidaEnLaFoto("CR-2", PayoffMode.Partial, 400_000.50m),
                new RecogidaEnLaFoto("CR-3", PayoffMode.Total, null, InstallmentTreatment.Exclude),
            ])
            .Should().Be("CR-1 total; CR-2 parcial 400000.5; CR-3 total sin la cuota causada", "el valor sólo va en un parcial o un abono");
    }

    [Fact]
    public async Task La_foto_lee_lo_que_recoge_de_las_filas()
    {
        var s = await _o.BorradorAsync(Recogiendo(Total(_viejo), Parcial(_otro, 200_000m)));
        var solicitud = await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);

        var foto = await FotoDeSolicitud.TomarAsync(_o.Db, solicitud, default);

        foto.Recogidas.Should().Be("CR-VIEJO total; CR-OTRO parcial 200000");
    }

    [Fact]
    public async Task Una_foto_vieja_con_el_JSON_de_PayoffRequest_compara_sin_falsos_cambios()
    {
        var s = await _o.BorradorAsync(Recogiendo(Total(_viejo), Parcial(_otro, 200_000m)));
        var solicitud = await _o.Db.CreditApplications.AsNoTracking().SingleAsync(a => a.PublicId == s.PublicId);
        var ahora = await FotoDeSolicitud.TomarAsync(_o.Db, solicitud, default);
        // Como lo guardaba la solicitud: un crédito repetido y uno que no existe (la migración los dejó por fuera).
        var vieja = ahora with
        {
            Recogidas = JsonSerializer.Serialize(new[]
            {
                new OwnLoanPayoffInput(_viejo.PublicId), new OwnLoanPayoffInput(_otro.PublicId, "Partial", 200_000m),
                new OwnLoanPayoffInput(_viejo.PublicId), new OwnLoanPayoffInput(Guid.NewGuid()),
            }, Json),
        };

        (await FotoDeSolicitud.NormalizarAsync(_o.Db, vieja, default)).Recogidas.Should().Be(ahora.Recogidas);
        (await ConsultasDeSolicitudes.CambiosAsync(_o.Db, vieja, ahora, default)).Should().BeEmpty();
        (await FotoDeSolicitud.NormalizarAsync(_o.Db, ahora, default)).Should().Be(ahora, "una foto con el texto estable queda como está");
    }

    [Fact]
    public async Task Una_devuelta_antes_de_la_feature_no_muestra_las_recogidas_como_cambio()
    {
        var s = await DevueltaAsync(Recogiendo(Total(_viejo)));
        var solicitud = await _o.Db.CreditApplications.SingleAsync(a => a.PublicId == s.PublicId);
        var foto = FotoDeSolicitud.Leer(solicitud.ReturnSnapshot)!;
        solicitud.ReturnSnapshot = JsonSerializer.Serialize(foto with
        {
            Recogidas = JsonSerializer.Serialize(new[] { new OwnLoanPayoffInput(_viejo.PublicId) }, Json),
        }, Json);
        await _o.Db.SaveChangesAsync();
        _o.Db.ChangeTracker.Clear();

        (await LeerAsync(s.PublicId)).ChangesSinceReturn.Should().BeEmpty();
    }

    // -------------------------------------------------------------------------------------------------------- corregir --

    [Fact]
    public async Task Devuelta_con_el_grupo_Recogidas_editable_se_cambian_y_se_ve_que_cambio()
    {
        var s = await DevueltaAsync(Recogiendo(Total(_viejo)));
        s.Payoffs.Should().ContainSingle();
        s.UsesAdvancedTerms.Should().BeFalse();
        s.EditableGroups.Should().Contain(nameof(GrupoEditableAlDevolver.Recogidas), "de fábrica todo se corrige");

        var r = await CorregirAsync(s, Recogiendo(Parcial(_viejo, 400_000m), Total(_otro)) with { Purpose = "Compra de vehículo" });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Payoffs.Select(p => (p.Number, p.Mode, p.RequestedAmount)).Should().Equal(("CR-VIEJO", PayoffMode.Partial, 400_000m), ("CR-OTRO", PayoffMode.Total, null));
        var leida = await LeerAsync(s.PublicId);
        leida.ChangesSinceReturn.Should().ContainSingle(c => c.Field == "Créditos que recoge").Which.Should()
            .Match<CreditApplicationChangeDto>(c => c.Before == "CR-VIEJO total" && c.After == "CR-VIEJO parcial 400000; CR-OTRO total"
                && c.Group == GruposEditablesAlDevolver.Texto(GrupoEditableAlDevolver.Recogidas));
        leida.ChangesSinceReturn.Select(c => c.Field).Should().Contain("Destino", "lo demás no se pierde");
    }

    [Fact]
    public async Task Devuelta_sin_el_grupo_Recogidas_rechaza_cambiarlas_y_admite_mandarlas_igual()
    {
        var s = await DevueltaAsync(Recogiendo(Total(_viejo)));
        await GruposAsync("Destino,Documentos");

        var cambio = await CorregirAsync(s, Recogiendo(Total(_otro)));
        cambio.Error.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible);
        OriginacionDePrueba.Datos(cambio.Error).GetProperty("groups")[0].GetString().Should().Be(nameof(GrupoEditableAlDevolver.Recogidas));

        var quitar = await CorregirAsync(s, _o.Solicitud());
        quitar.Error.Code.Should().Be(ErroresDeOriginacion.CodigoNoCorregible, "quitarlas también es cambiarlas");

        var igual = await CorregirAsync(s, Recogiendo(Total(_viejo)) with
        {
            Purpose = "Educación", PayoffProjection = new PayoffProjectionInput(OriginacionDePrueba.Hoy.AddDays(4)),
        });
        igual.IsSuccess.Should().BeTrue(igual.IsFailure ? $"{igual.Error.Code}: {igual.Error.Message}" : string.Empty);
        igual.Value.Payoffs.Single().ProjectedDate.Should().Be(OriginacionDePrueba.Hoy.AddDays(4), "otra proyección no es otra recogida");
        (await LeerAsync(s.PublicId)).ChangesSinceReturn.Select(c => c.Field).Should().NotContain("Créditos que recoge");
    }

    [Fact]
    public async Task La_lista_vieja_con_lo_mismo_no_es_un_cambio()
    {
        var s = await DevueltaAsync(Recogiendo(Parcial(_viejo, 300_000m)));
        await GruposAsync("Destino");

        var r = await CorregirAsync(s, _o.Solicitud() with { OwnLoanPayoffs = [new OwnLoanPayoffInput(_viejo.PublicId, "Partial", 300_000m)] });

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
    }
}
