using FluentAssertions;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Reports;
using IngenIA365ERP.Application.Payroll.Reports;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Application.Tests.Payroll.Settlements.ServiceBonus;
using IngenIA365ERP.Domain.Enums.Payroll;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll.Reports;

/// <summary>
/// T045 (feature 010; contracts/api.md §11): las dos vistas de liquidaciones especiales del centro de
/// reportes sobre una prima real. El resumen trae un renglón por empleado con base, días, cada rubro,
/// retención y neto; el detalle una fila por línea con su explicación; el encabezado nombra a la
/// cooperativa y a quien lo pide; el permiso es el <c>.View</c> del tipo de la corrida (sin él, 404
/// indistinguible) y sólo la exportación a archivo queda en la auditoría.
/// </summary>
public class ReportesDeLiquidacionesTests
{
    private sealed record Escenario(NominaTestData D, Guid RunId, IPermissionChecker Permisos, ICurrentTenantService Cooperativa);

    private static async Task<Escenario> PrimaCalculadaAsync()
    {
        var s = PrimaDePrueba.SegundoSemestre2026();
        var r = await PrimaDePrueba.Calcular(s.D).Handle(new CalculateServiceBonusCommand(2026, 2, [s.A.PublicId, s.B.PublicId]), CancellationToken.None);
        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var permisos = Substitute.For<IPermissionChecker>();
        permisos.HasPermissionAsync("Payroll.ServiceBonus.View", Arg.Any<CancellationToken>()).Returns(true);
        var cooperativa = Substitute.For<ICurrentTenantService>();
        cooperativa.TenantName.Returns("Cooperativa Demo");
        return new Escenario(s.D, r.Value.RunPublicId, permisos, cooperativa);
    }

    private static LiquidacionEspecialResumenReportQueryHandler Resumen(Escenario e) =>
        new(e.D.Db, e.Permisos, e.Cooperativa, e.D.User, e.D.Clock, e.D.AuditEmitter);

    private static LiquidacionEspecialDetalleReportQueryHandler Detalle(Escenario e) =>
        new(e.D.Db, e.Permisos, e.Cooperativa, e.D.User, e.D.Clock, e.D.AuditEmitter);

    private static int Columna(TablaExportable t, string clave) => t.Columnas.ToList().FindIndex(c => c.Clave == clave || c.Nombre == clave);

    [Fact]
    public async Task El_resumen_trae_un_renglon_por_empleado_con_base_dias_rubro_retencion_y_neto()
    {
        var e = await PrimaCalculadaAsync();

        var r = await Resumen(e).Handle(new LiquidacionEspecialResumenReportQuery(e.RunId), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Titulo.Should().Be("Prima de servicios · resumen por empleado");
        t.Subtitulo.Should().Contain("Cooperativa Demo").And.Contain("Prima de servicios 2026-II").And.Contain("v1").And.Contain("ana@demo");
        t.Columnas.Select(c => c.Nombre).Should().StartWith(["Empleado", "Documento", "Base", "Días"]);
        Columna(t, "PRIMA").Should().BePositive("el rubro principal es una columna");
        Columna(t, "RETENCION").Should().BePositive();
        Columna(t, "NETO").Should().Be(t.Columnas.Count - 1);
        t.Filas.Should().HaveCount(2);

        var alba = t.Filas.Single(f => (string)f.Valores[0]! == "Alba Prueba");
        alba.Valores[Columna(t, "Base")].Should().Be(2_249_095m, "salario + auxilio, promedio ponderado del semestre");
        alba.Valores[Columna(t, "Días")].Should().Be(180);
        alba.Valores[Columna(t, "PRIMA")].Should().Be(1_124_547.50m);
        alba.Valores[Columna(t, "RETENCION")].Should().Be(0m);
        alba.Valores[Columna(t, "NETO")].Should().Be(1_124_547.50m);

        var bruno = t.Filas.Single(f => (string)f.Valores[0]! == "Bruno Prueba");
        bruno.Valores[Columna(t, "Días")].Should().Be(106);
        bruno.Valores[Columna(t, "PRIMA")].Should().Be(630_404.72m);

        t.Totales.Should().NotBeNull();
        t.Totales!.Resaltada.Should().BeTrue();
        t.Totales.Valores[Columna(t, "NETO")].Should().Be(1_124_547.50m + 630_404.72m);
        t.Totales.Valores[Columna(t, "Días")].Should().Be(286);
        t.Notas.Should().Contain(n => n.Contains("Corte 31/12/2026"));

        await e.D.Audit.DidNotReceive().AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollRunExported), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task El_detalle_trae_empleado_por_concepto_con_la_explicacion_y_exportar_queda_en_auditoria()
    {
        var e = await PrimaCalculadaAsync();

        var r = await Detalle(e).Handle(new LiquidacionEspecialDetalleReportQuery(e.RunId, Exportacion: true), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error.Message);
        var t = r.Value;
        t.Titulo.Should().Be("Prima de servicios · detalle por empleado y concepto");
        t.Columnas.Select(c => c.Nombre).Should().Equal("Empleado", "Documento", "Concepto", "Código", "Tipo", "Cantidad", "Base", "Valor", "Explicación");
        var prima = t.Filas.Single(f => (string)f.Valores[0]! == "Alba Prueba" && (string)f.Valores[3]! == "PRIMA");
        prima.Valores[4].Should().Be("Devengos");
        prima.Valores[7].Should().Be(1_124_547.50m);
        ((string)prima.Valores[8]!).Should().Contain("180 días").And.Contain("/360", "el resumen de la explicación acompaña a la línea");
        prima.Seccion.Should().Be("Alba Prueba", "las filas se agrupan por empleado");
        t.Filas.Should().Contain(f => (string)f.Valores[3]! == "RETEFTE_PRIMA" && (string)f.Valores[4]! == "Deducciones");
        t.Totales!.Valores[7].Should().Be(1_124_547.50m + 630_404.72m);

        await e.D.Audit.Received(1).AppendAsync(Arg.Is<AuditEventDocument>(a =>
            a.Action == AuditEventTypes.PayrollRunExported && a.NewValuesJson!.Contains("liquidacion-especial-detalle")), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sin_el_View_del_tipo_de_la_corrida_responde_NotFound_indistinguible()
    {
        var e = await PrimaCalculadaAsync();
        var sinPermiso = Substitute.For<IPermissionChecker>();
        sinPermiso.HasPermissionAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = new LiquidacionEspecialResumenReportQueryHandler(e.D.Db, sinPermiso, e.Cooperativa, e.D.User, e.D.Clock, e.D.AuditEmitter);

        var r = await handler.Handle(new LiquidacionEspecialResumenReportQuery(e.RunId, Exportacion: true), CancellationToken.None);

        r.Error.Code.Should().Be("Generic.NotFound");
        await sinPermiso.Received(1).HasPermissionAsync("Payroll.ServiceBonus.View", Arg.Any<CancellationToken>());
        await e.D.Audit.DidNotReceive().AppendAsync(Arg.Is<AuditEventDocument>(a => a.Action == AuditEventTypes.PayrollRunExported), Arg.Any<CancellationToken>());

        var inexistente = await Resumen(e).Handle(new LiquidacionEspecialResumenReportQuery(Guid.NewGuid()), CancellationToken.None);
        inexistente.Error.Code.Should().Be("Payroll.Run.NotFound");
    }

    [Fact]
    public void El_permiso_y_el_rubro_principal_van_por_tipo_de_corrida()
    {
        ReportesDeLiquidaciones.PermisoDeVista(PayrollRunKind.ServiceBonus).Should().Be("Payroll.ServiceBonus.View");
        ReportesDeLiquidaciones.PermisoDeVista(PayrollRunKind.Severance).Should().Be("Payroll.Severance.View");
        ReportesDeLiquidaciones.PermisoDeVista(PayrollRunKind.Vacation).Should().Be("Payroll.Vacations.View");
        ReportesDeLiquidaciones.PermisoDeVista(PayrollRunKind.Settlement).Should().Be("Payroll.Settlements.View");
        ReportesDeLiquidaciones.PermisoDeVista(PayrollRunKind.Ordinary).Should().Be("Payroll.Runs.View");
        ReportesDeLiquidaciones.RubroPrincipal(PayrollRunKind.Severance).Should().Be("CESANTIAS");
        ReportesDeLiquidaciones.ResumenDe("""{"form":"x","summary":"180 días × 2.249.095"}""").Should().Be("180 días × 2.249.095");
        ReportesDeLiquidaciones.ResumenDe("no es json").Should().BeEmpty();
        new LiquidacionEspecialResumenReportQueryValidator().Validate(new LiquidacionEspecialResumenReportQuery(Guid.Empty)).IsValid.Should().BeFalse();
        new LiquidacionEspecialDetalleReportQueryValidator().Validate(new LiquidacionEspecialDetalleReportQuery(Guid.Empty)).IsValid.Should().BeFalse();
    }
}
