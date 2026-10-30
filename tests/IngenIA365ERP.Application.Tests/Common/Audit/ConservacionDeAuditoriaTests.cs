using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Parameters;
using IngenIA365ERP.Domain.Enums.Parameters;
using IngenIA365ERP.Domain.Receipts.Parameters;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common.Audit;

/// <summary>
/// Feature 015, R1 (decisión de R1 sobre <c>Conservacion.Anios</c>; FR-014, FR-178; Principio X): la auditoría de Recaudo, Tesorería y go/no-go se conserva lo que diga
/// <c>Conservacion.Anios</c> de la cooperativa —<c>RCD</c> o <c>TES</c>— vigente a la fecha del evento, nunca menos de cinco años; los demás módulos siguen con su
/// retención fija. Los años viajan sellados en la carga (<see cref="AuditEventDocument.RetentionYears"/>). Prueba escrita, sin correr.
/// </summary>
public sealed class ConservacionDeAuditoriaTests
{
    private static readonly DateOnly Hoy = new(2027, 1, 15);
    private readonly ILectorDeParametros _lector = Substitute.For<ILectorDeParametros>();

    private void Vigente(string modulo, int anios)
    {
        var definicion = ParametrosDeRecaudoYTesoreria.Definiciones.First(d => d.Modulo == modulo && d.Clave == ParametrosDeRecaudoYTesoreria.ConservacionAnios);
        _lector.LeerAsync(modulo, ParametrosDeRecaudoYTesoreria.ConservacionAnios, Arg.Any<DateOnly>(), Arg.Any<ParameterScopeKind>(), Arg.Any<int>(),
                Arg.Any<CancellationToken>())
            .Returns(Result.Success(new ValorDeParametro(definicion, anios.ToString(System.Globalization.CultureInfo.InvariantCulture), anios, null)));
    }

    [Fact]
    public async Task Recaudo_y_go_no_go_leen_RCD_y_Tesoreria_lee_TES()
    {
        Vigente(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, 8);
        Vigente(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, 12);
        var c = new ConservacionDeAuditoria(_lector);

        (await c.AniosAsync(ModuloDeAuditoria.Receipts, Hoy, default)).Should().Be(8);
        (await c.AniosAsync(ModuloDeAuditoria.GoLive, Hoy, default)).Should().Be(8);
        (await c.AniosAsync(ModuloDeAuditoria.Treasury, Hoy, default)).Should().Be(12);
        (await c.AniosAsync(ModuloDeAuditoria.Lending, Hoy, default)).Should().BeNull("los demás módulos siguen con su retención fija");
    }

    [Fact]
    public async Task Nunca_menos_de_cinco_anios_y_sin_vigencia_rige_el_defecto_de_diez()
    {
        Vigente(ParametrosDeRecaudoYTesoreria.ModuloRecaudo, 3);
        var c = new ConservacionDeAuditoria(_lector);
        (await c.AniosAsync(ModuloDeAuditoria.Receipts, Hoy, default)).Should().Be(ParametrosDeRecaudoYTesoreria.PisoDeConservacion);

        _lector.LeerAsync(ParametrosDeRecaudoYTesoreria.ModuloTesoreria, ParametrosDeRecaudoYTesoreria.ConservacionAnios, Arg.Any<DateOnly>(),
                Arg.Any<ParameterScopeKind>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<ValorDeParametro>(new Error("Parameters.KeyNotFound", "sin clave")));
        (await c.AniosAsync(ModuloDeAuditoria.Treasury, Hoy, default)).Should().Be(10);
        ConservacionDeAuditoria.Defecto().Should().Be(10);
    }

    [Fact]
    public void El_vencimiento_sale_de_los_anios_sellados_con_el_piso()
    {
        var ocurrido = new DateTime(2027, 1, 15, 14, 0, 0, DateTimeKind.Utc);
        ConservacionDeAuditoria.VenceEl(ocurrido, 7).Should().Be(ocurrido.AddYears(7));
        ConservacionDeAuditoria.VenceEl(ocurrido, 2).Should().Be(ocurrido.AddYears(5));
        var evento = new AuditEventDocument("t", "u", null, "Receipts.Receipt.Issued", "Receipt", null, ModuloDeAuditoria.Receipts, null, null, null, null, null, null,
            null, null, null, ocurrido) { RetentionYears = 7 };
        AuditoriaEncadenada.LeerCarga(AuditoriaEncadenada.EscribirCarga(evento)).RetentionYears.Should().Be(7, "los años viajan sellados en la carga");
    }
}
