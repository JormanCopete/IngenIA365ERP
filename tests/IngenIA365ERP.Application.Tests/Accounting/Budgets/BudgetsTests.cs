using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Budgets;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Budgets;

/// <summary>
/// T137–T139 — US9: el presupuesto sólo admite cuentas de movimiento, no repite filas, nace en
/// borrador, se aprueba y desde ahí todo cambio exige motivo y crea una versión (la anterior queda
/// <c>Superseded</c>); la copia de otro año se ajusta y redondea a pesos; la distribución reparte
/// el total en doce cuotas y deja el resto donde el contrato dice.
/// </summary>
public class BudgetsTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public ChartOfAccount Gasto { get; }
        public ChartOfAccount OtroGasto { get; }
        public ChartOfAccount Ingreso { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance);
            Gasto = D.Cuenta("5105061");
            OtroGasto = D.Cuenta("5105062");
            Ingreso = D.Cuenta("4135051", AccountNature.Credit);
        }

        public CreateBudgetCommandHandler Creador() => new(D.Db, D.Clock, D.User, Emisor);
        public UpdateBudgetCommandHandler Modificador() => new(D.Db, D.Clock, D.User, Emisor);
        public ApproveBudgetCommandHandler Aprobador() => new(D.Db, D.Clock, D.User, Emisor);
        public CopyBudgetCommandHandler Copiador() => new(D.Db, D.Clock, D.User, Emisor);
        public DistributeBudgetCommandHandler Distribuidor() => new(D.Db, D.Clock, D.User, Emisor);
        public GetBudgetQueryHandler Consulta() => new(D.Db);

        public async Task<FiscalYear> AbrirEjercicioAsync(int year)
        {
            var ejercicio = new FiscalYear { Year = year, CreatedBy = "test" };
            D.Db.FiscalYears.Add(ejercicio);
            await D.Db.SaveChangesAsync();
            return ejercicio;
        }

        /// <summary>Doce valores: <paramref name="porMes"/> en cada (mes, valor); cero en el resto.</summary>
        public static decimal[] Meses(params (int Mes, decimal Valor)[] porMes)
        {
            var montos = new decimal[12];
            foreach (var (mes, valor) in porMes) montos[mes - 1] = valor;
            return montos;
        }

        public static BudgetLineInput Linea(ChartOfAccount cuenta, params (int Mes, decimal Valor)[] porMes) =>
            new(cuenta.PublicId, null, null, Meses(porMes));

        public async Task<BudgetDto> CrearAprobadoAsync()
        {
            var creado = await Creador().Handle(new CreateBudgetCommand(2026, [Linea(Gasto, (1, 100m), (2, 100m), (3, 100m)), Linea(Ingreso, (3, 500m))]), CancellationToken.None);
            creado.IsSuccess.Should().BeTrue(creado.Error?.Message);
            var aprobado = await Aprobador().Handle(new ApproveBudgetCommand(2026), CancellationToken.None);
            aprobado.IsSuccess.Should().BeTrue(aprobado.Error?.Message);
            return aprobado.Value;
        }
    }

    [Fact]
    public async Task Una_cuenta_de_agrupacion_se_rechaza_con_su_codigo()
    {
        var e = new Escenario();
        var grupo = e.D.Cuenta("510506", movimiento: false);

        var r = await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(grupo, (1, 100m))]), CancellationToken.None);

        r.IsFailure.Should().BeTrue();
        r.Error.Code.Should().Be("Accounting.Budget.AccountNotMovement");
        r.Error.Should().BeOfType<ErrorConDatos>().Which.Data.Should().BeEquivalentTo(new { accountCode = "510506" });
        (await e.D.Db.Budgets.CountAsync()).Should().Be(0, "no se guarda nada");
    }

    [Fact]
    public async Task Una_cuenta_inactiva_tampoco_se_presupuesta()
    {
        var e = new Escenario();
        var inactiva = e.D.Cuenta("5105069", activa: false);

        var r = await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(inactiva, (1, 100m))]), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Budget.AccountNotMovement");
    }

    [Fact]
    public async Task Dos_filas_con_la_misma_cuenta_sucursal_y_centro_se_rechazan()
    {
        var e = new Escenario();

        var r = await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 100m)), Escenario.Linea(e.Gasto, (2, 50m))]), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Budget.LineDuplicate");
        r.Error.Message.Should().Contain("5105061");
    }

    [Fact]
    public async Task La_misma_cuenta_con_sucursales_distintas_son_dos_filas_validas()
    {
        var e = new Escenario();

        var r = await e.Creador().Handle(new CreateBudgetCommand(2026,
        [
            new BudgetLineInput(e.Gasto.PublicId, e.D.Principal.PublicId, null, Escenario.Meses((1, 100m))),
            new BudgetLineInput(e.Gasto.PublicId, e.D.Norte.PublicId, null, Escenario.Meses((1, 40m))),
        ]), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Lines.Should().HaveCount(2);
        r.Value.Lines.Select(l => l.BranchName).Should().BeEquivalentTo("Principal", "Norte");
    }

    [Fact]
    public async Task Sin_ejercicio_abierto_no_se_presupuesta()
    {
        var e = new Escenario();

        var r = await e.Creador().Handle(new CreateBudgetCommand(2031, [Escenario.Linea(e.Gasto, (1, 100m))]), CancellationToken.None);

        r.Error.Code.Should().Be("Accounting.Budget.FiscalYearNotFound");
    }

    [Fact]
    public async Task Crear_deja_la_version_1_en_borrador_y_no_se_crea_dos_veces()
    {
        var e = new Escenario();

        var r = await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 100m), (12, 200m))]), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Should().Match<BudgetDto>(b => b.Year == 2026 && b.Version == 1 && b.Status == "Draft" && b.ApprovedAt == null);
        r.Value.Versions.Should().ContainSingle().Which.Status.Should().Be("Draft");
        var linea = r.Value.Lines.Should().ContainSingle().Subject;
        linea.AccountCode.Should().Be("5105061");
        linea.Amounts.Should().HaveCount(12);
        linea.Amounts[0].Should().Be(100m);
        linea.Amounts[11].Should().Be(200m);
        linea.Total.Should().Be(300m);

        var otra = await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 1m))]), CancellationToken.None);
        otra.Error.Code.Should().Be("Accounting.Budget.AlreadyExists");
    }

    [Fact]
    public async Task Modificar_un_borrador_lo_reemplaza_en_su_sitio_sin_versionar()
    {
        var e = new Escenario();
        await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 100m))]), CancellationToken.None);

        var r = await e.Modificador().Handle(new UpdateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 150m)), Escenario.Linea(e.Ingreso, (2, 900m))], null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Version.Should().Be(1);
        r.Value.Status.Should().Be("Draft");
        r.Value.Lines.Should().HaveCount(2);
        r.Value.Lines.Single(l => l.AccountCode == "5105061").Amounts[0].Should().Be(150m);
        (await e.D.Db.BudgetLines.CountAsync(l => !l.IsDeleted)).Should().Be(2);
        (await e.D.Db.BudgetLines.CountAsync(l => l.IsDeleted)).Should().Be(1, "la fila anterior queda dada de baja, no borrada");
    }

    [Fact]
    public async Task Aprobar_sella_quien_y_cuando_y_no_se_aprueba_dos_veces()
    {
        var e = new Escenario();
        await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 100m))]), CancellationToken.None);

        var r = await e.Aprobador().Handle(new ApproveBudgetCommand(2026), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Status.Should().Be("Approved");
        r.Value.ApprovedAt.Should().Be(ContabilidadTestData.Ahora);
        r.Value.ApprovedBy.Should().Be("contadora@demo");

        var otra = await e.Aprobador().Handle(new ApproveBudgetCommand(2026), CancellationToken.None);
        otra.Error.Code.Should().Be("Accounting.Budget.NotDraft");
    }

    [Fact]
    public async Task Modificar_un_aprobado_exige_motivo_y_con_motivo_crea_la_version_2_dejando_la_1_reemplazada()
    {
        var e = new Escenario();
        await e.CrearAprobadoAsync();

        var sinMotivo = await e.Modificador().Handle(new UpdateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (3, 200m))], null), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Accounting.Budget.ReasonRequired");
        (await e.D.Db.Budgets.CountAsync()).Should().Be(1, "sin motivo no cambia nada");

        var conMotivo = await e.Modificador().Handle(new UpdateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (3, 200m))], "Ajuste por inflación"), CancellationToken.None);

        conMotivo.IsSuccess.Should().BeTrue(conMotivo.Error?.Message);
        conMotivo.Value.Should().Match<BudgetDto>(b => b.Version == 2 && b.Status == "Approved" && b.ChangeReason == "Ajuste por inflación" && b.ApprovedBy == "contadora@demo");
        conMotivo.Value.Versions.Select(v => (v.Version, v.Status)).Should().Equal((1, "Superseded"), (2, "Approved"));
        conMotivo.Value.Lines.Should().ContainSingle().Which.Amounts[2].Should().Be(200m);

        var anterior = await e.D.Db.Budgets.SingleAsync(b => b.Version == 1);
        anterior.Status.Should().Be(BudgetStatus.Superseded);
        (await e.D.Db.BudgetLines.CountAsync(l => l.BudgetId == anterior.Id && !l.IsDeleted)).Should().Be(4, "la versión 1 conserva sus filas intactas");
    }

    [Fact]
    public async Task La_consulta_trae_las_versiones_y_distingue_la_inicial_de_la_vigente()
    {
        var e = new Escenario();
        await e.CrearAprobadoAsync();
        await e.Modificador().Handle(new UpdateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (3, 200m))], "Ajuste"), CancellationToken.None);

        var vigente = await e.Consulta().Handle(new GetBudgetQuery(2026, null), CancellationToken.None);
        var inicial = await e.Consulta().Handle(new GetBudgetQuery(2026, 1), CancellationToken.None);
        var inexistente = await e.Consulta().Handle(new GetBudgetQuery(2026, 7), CancellationToken.None);

        vigente.Value.Version.Should().Be(2);
        vigente.Value.Status.Should().Be("Approved");
        vigente.Value.Lines.Should().ContainSingle().Which.Total.Should().Be(200m);
        vigente.Value.Versions.Should().HaveCount(2);

        inicial.Value.Version.Should().Be(1);
        inicial.Value.Status.Should().Be("Superseded");
        inicial.Value.Lines.Should().HaveCount(2);
        inicial.Value.Lines.Single(l => l.AccountCode == "5105061").Total.Should().Be(300m);
        inicial.Value.Versions.Should().HaveCount(2, "la lista de versiones es la misma desde cualquiera");

        inexistente.Error.Code.Should().Be("Accounting.Budget.NotFound");
    }

    [Fact]
    public async Task Un_ano_sin_presupuesto_responde_None_con_las_listas_vacias()
    {
        var e = new Escenario();

        var r = await e.Consulta().Handle(new GetBudgetQuery(2026, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue();
        r.Value.Should().BeEquivalentTo(new BudgetDto(2026, 0, "None", null, null, null, [], []));
    }

    [Fact]
    public async Task Copiar_del_ano_anterior_ajusta_el_porcentaje_y_redondea_a_pesos()
    {
        var e = new Escenario();
        await e.AbrirEjercicioAsync(2027);
        // 1.001 × 1,05 = 1.051,05 → 1.051; 333 × 1,05 = 349,65 → 350; 10 × 1,05 = 10,5 → 11 (la mitad, hacia arriba).
        await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 1001m), (2, 333m), (3, 10m))]), CancellationToken.None);
        await e.Aprobador().Handle(new ApproveBudgetCommand(2026), CancellationToken.None);

        var r = await e.Copiador().Handle(new CopyBudgetCommand(2027, 2026, 5m), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Should().Match<BudgetDto>(b => b.Year == 2027 && b.Version == 1 && b.Status == "Draft");
        var linea = r.Value.Lines.Should().ContainSingle().Subject;
        linea.Amounts[0].Should().Be(1051m);
        linea.Amounts[1].Should().Be(350m);
        linea.Amounts[2].Should().Be(11m);
        linea.Total.Should().Be(1412m);

        var origen = await e.Consulta().Handle(new GetBudgetQuery(2026, null), CancellationToken.None);
        origen.Value.Lines.Single().Total.Should().Be(1344m, "el año de origen no se toca");
    }

    [Fact]
    public async Task Copiar_reemplaza_el_borrador_pero_no_pisa_un_aprobado_y_exige_origen()
    {
        var e = new Escenario();
        await e.AbrirEjercicioAsync(2027);

        var sinOrigen = await e.Copiador().Handle(new CopyBudgetCommand(2027, 2026, 0m), CancellationToken.None);
        sinOrigen.Error.Code.Should().Be("Accounting.Budget.SourceNotFound");

        await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 100m))]), CancellationToken.None);
        await e.Creador().Handle(new CreateBudgetCommand(2027, [Escenario.Linea(e.Ingreso, (1, 999m))]), CancellationToken.None);

        var sobreBorrador = await e.Copiador().Handle(new CopyBudgetCommand(2027, 2026, 10m), CancellationToken.None);
        sobreBorrador.IsSuccess.Should().BeTrue(sobreBorrador.Error?.Message);
        sobreBorrador.Value.Version.Should().Be(1);
        sobreBorrador.Value.Lines.Should().ContainSingle().Which.Should().Match<BudgetLineDto>(l => l.AccountCode == "5105061" && l.Amounts[0] == 110m);

        await e.Aprobador().Handle(new ApproveBudgetCommand(2027), CancellationToken.None);
        var sobreAprobado = await e.Copiador().Handle(new CopyBudgetCommand(2027, 2026, 10m), CancellationToken.None);
        sobreAprobado.Error.Code.Should().Be("Accounting.Budget.NotDraft");
    }

    [Fact]
    public void Distribucion_igual_reparte_a_pesos_y_deja_el_resto_en_diciembre()
    {
        var r = DistribucionDeCuotas.Calcular(100m, "equal", null);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Take(11).Should().OnlyContain(c => c == 8m);
        r.Value[11].Should().Be(12m);
        r.Value.Sum().Should().Be(100m);
    }

    [Fact]
    public void Distribucion_porcentual_convierte_a_pesos_y_deja_el_resto_en_el_ultimo_mes_con_porcentaje()
    {
        // 33,33 % × 3 = 99,99: entra por la tolerancia; 1.000 × 33,33 % = 333,3 → 333; el resto (1) va a marzo, el último con porcentaje.
        var r = DistribucionDeCuotas.Calcular(1000m, "percent", [33.33m, 33.33m, 33.33m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m]);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Take(3).Should().Equal(333m, 333m, 334m);
        r.Value.Skip(3).Should().OnlyContain(c => c == 0m);
        r.Value.Sum().Should().Be(1000m);
    }

    [Fact]
    public void Distribucion_porcentual_que_no_suma_100_se_rechaza()
    {
        var r = DistribucionDeCuotas.Calcular(1000m, "percent", [50m, 40m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m]);

        r.Error.Code.Should().Be("Accounting.Budget.InvalidDistribution");
        r.Error.Message.Should().Contain("90");
    }

    [Fact]
    public void Distribucion_manual_que_no_suma_el_total_se_rechaza_y_la_que_suma_se_acepta_tal_cual()
    {
        var mal = DistribucionDeCuotas.Calcular(100m, "manual", [10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 5m]);
        mal.Error.Code.Should().Be("Accounting.Budget.InvalidDistribution");

        var bien = DistribucionDeCuotas.Calcular(100m, "manual", [10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 10m, 5m, 0m, 5m]);
        bien.IsSuccess.Should().BeTrue(bien.Error?.Message);
        bien.Value[9].Should().Be(5m);
        bien.Value[10].Should().Be(0m);
    }

    [Fact]
    public void Distribucion_sin_doce_valores_con_total_cero_o_con_negativos_se_rechaza()
    {
        DistribucionDeCuotas.Calcular(100m, "manual", [100m]).Error.Code.Should().Be("Accounting.Budget.InvalidDistribution");
        DistribucionDeCuotas.Calcular(0m, "equal", null).Error.Code.Should().Be("Accounting.Budget.InvalidDistribution");
        DistribucionDeCuotas.Calcular(100m, "percent", [110m, -10m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m]).Error.Code.Should().Be("Accounting.Budget.NegativeAmount");
    }

    [Fact]
    public async Task Distribuir_guarda_las_doce_cuotas_de_la_cuenta_en_el_borrador_y_deja_las_otras_cuentas_como_estaban()
    {
        var e = new Escenario();
        await e.Creador().Handle(new CreateBudgetCommand(2026, [Escenario.Linea(e.Gasto, (1, 1m)), Escenario.Linea(e.Ingreso, (6, 600m))]), CancellationToken.None);

        var r = await e.Distribuidor().Handle(new DistributeBudgetCommand(2026, e.Gasto.PublicId, null, null, 100m, "equal", null, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Version.Should().Be(1);
        var gasto = r.Value.Lines.Single(l => l.AccountCode == "5105061");
        gasto.Amounts.Take(11).Should().OnlyContain(c => c == 8m);
        gasto.Amounts[11].Should().Be(12m);
        gasto.Total.Should().Be(100m);
        r.Value.Lines.Single(l => l.AccountCode == "4135051").Amounts[5].Should().Be(600m);
    }

    [Fact]
    public async Task Distribuir_sobre_un_aprobado_exige_motivo_y_versiona_conservando_las_otras_cuentas()
    {
        var e = new Escenario();
        await e.CrearAprobadoAsync();

        var sinMotivo = await e.Distribuidor().Handle(new DistributeBudgetCommand(2026, e.Gasto.PublicId, null, null, 1200m, "equal", null, null), CancellationToken.None);
        sinMotivo.Error.Code.Should().Be("Accounting.Budget.ReasonRequired");

        var r = await e.Distribuidor().Handle(new DistributeBudgetCommand(2026, e.Gasto.PublicId, null, null, 1200m, "equal", null, "Reparto anual"), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Should().Match<BudgetDto>(b => b.Version == 2 && b.Status == "Approved" && b.ChangeReason == "Reparto anual");
        r.Value.Lines.Single(l => l.AccountCode == "5105061").Amounts.Should().OnlyContain(c => c == 100m);
        r.Value.Lines.Single(l => l.AccountCode == "4135051").Amounts[2].Should().Be(500m, "la otra cuenta viaja a la versión nueva tal cual");
    }

    [Fact]
    public async Task Distribuir_sin_presupuesto_previo_crea_el_borrador_con_esa_cuenta()
    {
        var e = new Escenario();

        var r = await e.Distribuidor().Handle(new DistributeBudgetCommand(2026, e.Gasto.PublicId, null, null, 1200m, "equal", null, null), CancellationToken.None);

        r.IsSuccess.Should().BeTrue(r.Error?.Message);
        r.Value.Should().Match<BudgetDto>(b => b.Version == 1 && b.Status == "Draft");
        r.Value.Lines.Should().ContainSingle().Which.Total.Should().Be(1200m);
    }
}
