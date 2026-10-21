using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Lending.Documents;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Documents;

/// <summary>
/// Feature 013, US13, K5, T0967 (research R149: «comparar el SHA-256 de un PDF subido»; api.md §15): la verificación con <c>sha256</c> responde
/// <c>pdfMatches = true</c> sólo si la huella es la del PDF guardado (sin importar mayúsculas), <c>false</c> si es otra, y nada si no se pidió; una
/// huella mal formada es <c>Validation.Invalid</c> antes de mirar el código; la comparación queda en la misma lectura auditada
/// <c>Lending.Document.Verified</c> con su resultado. El PDF nunca llega: el navegador calcula la huella. La verificación sin huella la cubre
/// <see cref="DocumentosEmitidosDeCarteraTests"/>.
/// </summary>
public sealed class VerifyLendingDocumentQueryTests : IAsyncLifetime
{
    private DocumentosDePrueba _d = null!;

    public async Task InitializeAsync() => _d = await DocumentosDePrueba.CrearAsync();

    public Task DisposeAsync()
    {
        _d.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(IssuedLendingDocumentDto Emitido, string Huella)> PlanAsync()
    {
        var emitido = await _d.EmitidoAsync(DocumentosDePrueba.Pedido(IssuedDocumentKind.PaymentPlan, _d.Ana.PublicId, _d.Credito.PublicId));
        return (emitido, (await _d.DocumentoAsync(emitido.DocumentPublicId)).ContentSha256);
    }

    private async Task<IReadOnlyList<string>> VerificacionesAuditadasAsync() =>
        (await _d.Db.AuditOutbox.AsNoTracking().ToListAsync())
            .Where(f => AuditoriaEncadenada.LeerCarga(f.PayloadJson!).Action == AuditEventTypes.LendingDocumentVerified)
            .Select(f => f.PayloadJson!).ToList();

    [Fact]
    public async Task La_huella_del_pdf_guardado_coincide_y_queda_en_la_lectura_auditada()
    {
        var (emitido, huella) = await PlanAsync();

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(emitido.VerificationCode, huella.ToUpperInvariant()), default);

        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : string.Empty);
        r.Value.PdfMatches.Should().BeTrue();
        r.Value.Authentic.Should().BeTrue();
        (await VerificacionesAuditadasAsync()).Should().ContainSingle().Which.Should().Contain("pdfMatches").And.Contain("true");
    }

    [Fact]
    public async Task Otra_huella_no_coincide_y_tambien_se_audita()
    {
        var (emitido, huella) = await PlanAsync();
        var otra = new string(huella.Reverse().ToArray()) == huella ? new string('0', 64) : new string(huella.Reverse().ToArray());

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(emitido.VerificationCode, otra), default);

        r.Value.PdfMatches.Should().BeFalse("un PDF alterado o de otro documento no es el guardado");
        r.Value.Authentic.Should().BeTrue("el código sí es auténtico: lo que no coincide es el archivo");
        (await VerificacionesAuditadasAsync()).Should().ContainSingle().Which.Should().Contain("pdfMatches").And.Contain("false");
    }

    [Fact]
    public async Task Sin_huella_no_se_compara()
    {
        var (emitido, _) = await PlanAsync();

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(emitido.VerificationCode), default);

        r.Value.PdfMatches.Should().BeNull();
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("zz00000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("00000000000000000000000000000000000000000000000000000000000000000")]
    public async Task Una_huella_mal_formada_es_Validation_Invalid(string huella)
    {
        var (emitido, _) = await PlanAsync();

        var r = await _d.Verificar().Handle(new VerifyLendingDocumentQuery(emitido.VerificationCode, huella), default);

        r.Error.Code.Should().Be("Validation.Invalid");
        (await VerificacionesAuditadasAsync()).Should().BeEmpty("no se verificó nada");
    }
}
