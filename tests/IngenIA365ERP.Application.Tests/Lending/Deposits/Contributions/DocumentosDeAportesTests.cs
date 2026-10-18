using FluentAssertions;
using IngenIA365ERP.Application.Common.Calendar;
using IngenIA365ERP.Application.Common.Integration.Lending;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Deposits.Contributions;
using IngenIA365ERP.Application.Lending.Deposits.Documents;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Application.Lending.Payments;
using IngenIA365ERP.Domain.Entities.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Deposits.Contributions;

/// <summary>
/// Feature 014, US3 (T087, T092; contracts/documentos.md §2.6, §2.7, §4; FR-101, FR-106): la constancia de aportes sale del mismo saldo que la consulta de
/// aportes, por clase y con el mínimo; el paz y salvo de aportes se niega con cuotas pendientes y, al día, queda sujeto a firmeza; el reverso de la
/// consignación anula los documentos que la vieron. (nuevo)
/// </summary>
public sealed class DocumentosDeAportesTests : IDisposable
{
    private static readonly DateOnly Enero10 = new(2027, 1, 10);
    private readonly AportesDePrueba _a = new();

    public void Dispose() => _a.Dispose();

    private DocumentosDeAportes Documentos() => new(_a.Db, _a.Reglas(), new FirmezaDelPago(_a.C.Parametros(), new CalendarioDeDiasHabiles(_a.Db)));

    private static DocumentoArmado Ok(Result<DocumentoArmado> r)
    {
        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        return r.Value;
    }

    private PedidoDeDocumentoDeCartera Pedido(IssuedDocumentKind clase, DateOnly? corte = null) =>
        new(clase, _a.C.Ana, null, corte, DateTime.SpecifyKind(DateTime.UtcNow.AddYears(5), DateTimeKind.Utc), _a.G.HoyLocal);

    [Fact]
    public async Task La_constancia_dice_el_saldo_por_clase_el_minimo_y_las_cuotas_pendientes_como_la_consulta()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 80_000m));

        var armado = Ok(await Documentos().ArmarAsync(Pedido(IssuedDocumentKind.ContributionCertificate, Enero10) with { }, default));

        var cuerpo = armado.Cuerpo.Should().BeOfType<CuerpoDeConstanciaDeAportes>().Subject;
        cuerpo.Total.Should().Be(80_000m);
        cuerpo.PorClase.Single(c => c.Clase == "Mínimos").Valor.Should().Be(80_000m);
        cuerpo.Minimo.Should().Be(500_000m);
        cuerpo.CuotasPendientes.Select(q => (q.Periodo, q.Pendiente)).Should().Equal(("2027-01", 20_000m));
        armado.Leyendas.Should().Contain(CuerpoDeConstanciaDeAportes.LeyendaDeGarantia);
        armado.Cuentas.Should().ContainSingle().Which.BalanceAtCutoff.Should().Be(80_000m);
        var consulta = await _a.Consulta().SaldosAsync(new SolicitudDeSaldoDeAportes([_a.C.Ana.PublicId], Enero10), default);
        consulta.Balances.Single().Contributions.Should().Be(cuerpo.Total, "el mismo saldo que la consulta de aportes (SC-010)");
    }

    [Fact]
    public async Task El_paz_y_salvo_se_niega_con_cuotas_pendientes_y_al_dia_queda_sujeto_a_firmeza()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        await _a.GenerarAsync(cuenta, Enero10);
        _a.G.HoyLocal = Enero10;

        var negado = await Documentos().ArmarAsync(Pedido(IssuedDocumentKind.ContributionClearance), default);
        negado.Error.Code.Should().Be(DocumentErrors.CodigoPazYSalvoNegado);
        GobiernoDePruebaDatos.Datos(negado.Error).GetProperty("installments").GetArrayLength().Should().Be(2);

        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 100_000m, medio: ClaseDeMedioDeRecaudo.Check));
        var armado = Ok(await Documentos().ArmarAsync(Pedido(IssuedDocumentKind.ContributionClearance), default));
        var cuerpo = armado.Cuerpo.Should().BeOfType<CuerpoDePazYSalvoDeAportes>().Subject;
        cuerpo.SaldoDeAportes.Should().Be(100_000m);
        armado.FinalityUntil.Should().NotBeNull().And.BeAfter(Enero10, "el cheque tiene días de firmeza");
    }

    [Fact]
    public async Task El_reverso_de_la_consignacion_anula_la_constancia_que_la_vio()
    {
        var cuenta = await _a.CuentaDeAportesAsync(_a.C.Ana);
        var clave = AportesDePrueba.Clave();
        await _a.ConsignadaAsync(_a.Consignacion(cuenta, 30_000m, ModalidadDeConsignacion.Free, clave: clave));
        var id = await _a.Db.MemberAccounts.Where(a => a.PublicId == cuenta.PublicId).Select(a => a.Id).SingleAsync();
        var documento = new IssuedLendingDocument
        {
            Kind = IssuedDocumentKind.ContributionCertificate, Number = "CNA-000001", PersonId = _a.C.Ana.Id, AsOfDate = _a.G.HoyLocal,
            InformationCutoff = DateTime.UtcNow.AddYears(1), VerificationCode = "ABCDEFGHJKLMNPQR", IssuedByUserId = _a.C.Asesora.Id, IssuedAt = DateTime.UtcNow,
            RulesVersion = "LND-2026.1", CreatedBy = "test",
        };
        _a.Db.IssuedLendingDocuments.Add(documento);
        await _a.Db.SaveChangesAsync();
        _a.Db.IssuedDocumentAccounts.Add(new IssuedDocumentAccount { IssuedDocumentId = documento.Id, MemberAccountId = id, CreatedBy = "test" });
        await _a.Db.SaveChangesAsync();
        _a.Db.ChangeTracker.Clear();

        var reversion = new ReversionDeCuentas(_a.Db, _a.RegistroDeCuentas(), _a.Numerador(), _a.Emision(), _a.G.Actor, _a.G.Reloj,
            new AnulacionDeDocumentosDeCuentas(_a.Db));
        var original = (await reversion.OperacionAsync(AportesDePrueba.Prueba.Code, clave, default))!;
        var r = await reversion.ReversarAsync(original, "Cheque devuelto", "Collector", default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.Comprobante.VoidedDocumentPublicIds.Should().Equal(documento.PublicId);
        var evento = await _a.Db.IssuedDocumentEvents.AsNoTracking().SingleAsync(e => e.Kind == IssuedDocumentEventKind.Voided);
        evento.CausingAccountOperationId.Should().NotBeNull();
        evento.CausingOperationId.Should().BeNull();
    }
}

/// <summary>Los datos de un error con datos, como JSON (como <c>GobiernoDePrueba.Datos</c>).</summary>
internal static class GobiernoDePruebaDatos
{
    public static System.Text.Json.JsonElement Datos(Error error) => Sessions.GobiernoDePrueba.Datos(error);
}
