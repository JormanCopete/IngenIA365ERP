using System.Text.Json;
using FluentAssertions;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Integration.Contracts.Lending;
using IngenIA365ERP.Application.Lending.Integration.DoblesDePrueba;
using IngenIA365ERP.Application.Tests.Lending.Libranza;
using IngenIA365ERP.Domain.Enums.Integration;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.TestDoubles;

/// <summary>
/// Feature 013, K2, T0817 (mensajes.md §18, api.md §20): el recaudador de prueba con el archivo de descuentos.
/// <list type="bullet">
/// <item>consume <c>DescuentosPorPagaduria</c>; <c>ConfirmTestPayrollFileSentCommand</c> emite <c>EnvioDeDescuentosConfirmado</c> como <c>REC</c> con la
/// persona que prueba de usuario de origen;</item>
/// <item><c>EmitTestPayrollDeductionResultCommand</c> lee el archivo con el formato de vuelta y emite <c>ResultadoDeDescuentos</c>; un archivo inválido
/// responde <c>Import.Invalid</c> con sus líneas; sin el doble registrado (producción) las dos responden el 404 genérico.</item>
/// </list>
/// </summary>
public sealed class RecaudadorDePruebaDeLibranzaTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private LibranzaDePrueba _l = null!;
    private Guid _archivo;

    public async Task InitializeAsync()
    {
        _l = await LibranzaDePrueba.CrearAsync();
        _archivo = (await _l.ArchivoAsync()).PublicId;
        _l.G.ActuarComo(_l.D.Cajera);
    }

    public Task DisposeAsync()
    {
        _l.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public void Acepta_el_archivo_de_descuentos()
    {
        var doble = new RecaudadorDePrueba();
        doble.Destino.Should().Be(IntegrationDestinations.Receipts);
        doble.Acepta(DescuentosPorPagaduriaV1.Type, 1).Should().BeTrue();
        doble.Acepta(DescuentosPorPagaduriaV1.Type, 2).Should().BeFalse();
    }

    [Fact]
    public async Task Confirmar_el_envio_emite_EnvioDeDescuentosConfirmado_como_REC_con_la_persona_que_prueba()
    {
        var r = await _l.ConfirmarEnvio().Handle(new ConfirmTestPayrollFileSentCommand(_archivo, "Email", "RAD-77") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        var m = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.MessagePublicId);
        (m.Type, m.OriginModule, m.OriginActorKind, m.RelatedPublicId, m.PersonPublicId)
            .Should().Be((EnvioDeDescuentosConfirmadoV1.Type, "REC", ActorKind.Person, (Guid?)_archivo, (Guid?)_l.Pagaduria.PublicId));
        m.OriginUserName.Should().NotBeNullOrWhiteSpace();
        var envio = JsonSerializer.Deserialize<EnvioDeDescuentosConfirmadoV1>(m.PayloadJson, Json)!;
        (envio.FilePublicId, envio.Channel, envio.Reference).Should().Be((_archivo, "Email", "RAD-77"));
        envio.FileSha256.Should().Be((await _l.Db.PayrollDeductionFiles.AsNoTracking().SingleAsync(f => f.PublicId == _archivo)).FileSha256);
        (await _l.Db.IntegrationMessageDeliveries.AsNoTracking().SingleAsync(d => d.MessageId == m.Id)).Destination.Should().Be(IntegrationDestinations.Lending);
    }

    [Fact]
    public async Task Cargar_la_respuesta_la_lee_con_el_formato_de_vuelta_y_emite_ResultadoDeDescuentos()
    {
        var esperado = (await _l.LineaAsync(_archivo, _l.Credito)).TotalAmount;

        var r = await _l.EmitirResultado().Handle(new EmitTestPayrollDeductionResultCommand(_archivo, "respuesta.csv",
            LibranzaDePrueba.Respuesta((_l.Credito.Number, "07", 0m, null))) { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();

        r.IsSuccess.Should().BeTrue(r.IsFailure ? $"{r.Error.Code}: {r.Error.Message}" : string.Empty);
        r.Value.Lines.Should().Be(1);
        var m = await _l.Db.IntegrationMessages.AsNoTracking().SingleAsync(x => x.PublicId == r.Value.MessagePublicId);
        (m.Type, m.OriginModule).Should().Be((ResultadoDeDescuentosV1.Type, "REC"));
        var resultado = JsonSerializer.Deserialize<ResultadoDeDescuentosV1>(m.PayloadJson, Json)!;
        var linea = resultado.Lines.Should().ContainSingle().Subject;
        (linea.Outcome, linea.ExpectedAmount, linea.DeductedAmount, linea.PaymasterResultCode, linea.Reason, linea.Detail)
            .Should().Be(("NotApplied", esperado, 0m, "07", "Leave", "Licencia no remunerada"));
        (resultado.Totals.LineCount, resultado.Totals.NotApplied).Should().Be((1, esperado));
    }

    [Fact]
    public async Task Un_archivo_invalido_responde_Import_Invalid_con_sus_lineas()
    {
        var ajeno = await _l.EmitirResultado().Handle(new EmitTestPayrollDeductionResultCommand(_archivo, "respuesta.csv",
            LibranzaDePrueba.Respuesta(("CR-999999", "00", 1m, new DateOnly(2027, 1, 30)))) { OperationKey = Guid.NewGuid() }, default);
        ajeno.Error.Code.Should().Be(ImportErrors.InvalidCode);
        Disbursements.DesembolsoDePrueba.Datos(ajeno.Error).GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("LoanNumber");

        var mal = await _l.EmitirResultado().Handle(new EmitTestPayrollDeductionResultCommand(_archivo, "respuesta.csv",
            "solo-una-columna\n"u8.ToArray()) { OperationKey = Guid.NewGuid() }, default);
        mal.Error.Code.Should().Be(ImportErrors.InvalidCode);
        (await _l.Db.IntegrationMessages.AnyAsync(x => x.Type == ResultadoDeDescuentosV1.Type)).Should().BeFalse();
    }

    [Fact]
    public async Task Sin_el_doble_registrado_las_dos_responden_el_404_generico()
    {
        (await _l.ConfirmarEnvio(dobleRegistrado: false).Handle(new ConfirmTestPayrollFileSentCommand(_archivo) { OperationKey = Guid.NewGuid() }, default))
            .Error.Code.Should().Be("Generic.NotFound");
        (await _l.EmitirResultado(dobleRegistrado: false).Handle(new EmitTestPayrollDeductionResultCommand(_archivo, "r.csv", [1]) { OperationKey = Guid.NewGuid() },
            default)).Error.Code.Should().Be("Generic.NotFound");
    }
}
