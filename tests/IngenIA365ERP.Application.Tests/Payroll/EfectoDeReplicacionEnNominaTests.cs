using FluentAssertions;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.NationalValues;
using IngenIA365ERP.Application.Common.Taxation;
using IngenIA365ERP.Application.Payroll.Holidays;
using IngenIA365ERP.Application.Payroll.LegalParameters;
using IngenIA365ERP.Application.Payroll.NationalValues;
using IngenIA365ERP.Application.Payroll.Services;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Application.Tests.Common.NationalValues;
using IngenIA365ERP.Application.Tests.Payroll.Common;
using IngenIA365ERP.Domain.Common.NationalValues;
using IngenIA365ERP.Domain.Entities.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Payroll;
using IngenIA365ERP.Domain.Payroll.Calculation;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Payroll;

/// <summary>
/// T0026 (feature 013, T93, enmienda A7; data-model parte 3 §7.5; quickstart §1.5): la proyección de Nómina de los valores
/// nacionales, <see cref="EfectoDeReplicacionEnNomina"/>, corrida por el núcleo de la réplica sobre la base de la cooperativa en
/// memoria. Las cuatro reglas: (1) adopta lo igual y conserva y alerta lo distinto; (2) inserta lo nuevo; (3) cierra la vigencia
/// anterior la víspera; (4) sólo la réplica escribe: <c>AddLegalParameterVersionCommand</c> rechaza SMMLV y UVT y los comandos de
/// festivos no tocan un <c>National</c>. Los festivos <c>Manual</c> no se tocan y <c>LectorDeUvt</c> lee la proyección sin cambio.
/// </summary>
public class EfectoDeReplicacionEnNominaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Enero2026 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();
    private readonly CentralDePrueba _central = new();

    private Task<ResultadoDeReplicacion> Replicar() =>
        new ReplicacionDeValoresNacionales(_db, _central, [new EfectoDeReplicacionEnNomina()], CopiasDePrueba.Reloj(Ahora))
            .AplicarAsync(NationalValueReplicationOrigin.TenantSeeder, "Semilla del alta", CancellationToken.None);

    private void ParametroDeNomina(string codigo, decimal valor, DateTime desde, string fuente = "Decreto 1469 de 2025")
    {
        _db.PayrollLegalParameters.Add(new PayrollLegalParameter
        {
            Code = codigo, Name = codigo == LegalParameterCodes.Smmlv ? "Salario mínimo mensual legal vigente" : "Unidad de Valor Tributario",
            Kind = LegalParameterKind.Amount, Value = valor, ValidFrom = desde, Source = fuente, CreatedBy = "system:seed",
        });
        _db.SaveChanges();
    }

    // ------------------------------------------------------------------------------------- regla 1: adoptar --

    [Fact]
    public async Task Lo_igual_se_adopta_como_proyeccion_sin_insertar()
    {
        ParametroDeNomina(LegalParameterCodes.Smmlv, 1_750_905m, Enero2026);
        var smmlv = _central.Valor(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 1, 1), 1_750_905m, "Pesos");

        var r = await Replicar();

        var fila = await _db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Smmlv);
        fila.Source.Should().Be($"Valor nacional {smmlv.PublicId:D}");
        fila.Value.Should().Be(1_750_905m);
        r.Efectos[EfectoDeReplicacionEnNomina.NombreDelEfecto].Conteos["parametrosAdoptados"].Should().Be(1);
        r.Efectos[EfectoDeReplicacionEnNomina.NombreDelEfecto].Alertas.Should().BeEmpty();
    }

    [Fact]
    public async Task Lo_distinto_conserva_el_de_Nomina_y_alerta_Nomina_ValorNacionalDistinto()
    {
        ParametroDeNomina(LegalParameterCodes.Uvt, 52_000m, Enero2026, "Resolución digitada por la contadora");
        _central.Valor(CodigosDeValorNacional.Uvt, "", new DateOnly(2026, 1, 1), 52_374m, "Pesos");

        var r = await Replicar();

        var fila = await _db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt);
        (fila.Value, fila.Source).Should().Be((52_000m, "Resolución digitada por la contadora"));
        var efecto = r.Efectos[EfectoDeReplicacionEnNomina.NombreDelEfecto];
        efecto.Conteos["parametrosDistintos"].Should().Be(1);
        var alerta = efecto.Alertas.Should().ContainSingle().Which;
        alerta.TypeCode.Should().Be(AlertasDeValoresNacionales.ValorNacionalDistinto);
        alerta.DedupKey.Should().Be("Nomina.ValorNacionalDistinto:UVT:2026-01-01");
        (await _db.NationalValueReplications.SingleAsync()).EffectsSummaryJson.Should().Contain("parametrosDistintos");
    }

    [Fact]
    public async Task Un_festivo_Ley51_se_adopta_y_uno_Manual_o_Decretado_no_se_toca()
    {
        _db.Holidays.AddRange(
            new Holiday { Date = new DateOnly(2026, 12, 8), Name = "Inmaculada Concepción", Origin = HolidayOrigin.Ley51Fixed, Year = 2026, CreatedBy = "system:seed" },
            new Holiday { Date = new DateOnly(2026, 11, 2), Name = "Puente decretado", Origin = HolidayOrigin.Decreed, Year = 2026, CreatedBy = "contadora" },
            new Holiday { Date = new DateOnly(2026, 12, 24), Name = "Día de la cooperativa", Origin = HolidayOrigin.Manual, Year = 2026, CreatedBy = "contadora" });
        await _db.SaveChangesAsync();
        var inmaculada = _central.Festivo(new DateOnly(2026, 12, 8), "Inmaculada Concepción");
        _central.Festivo(new DateOnly(2026, 11, 2), "Todos los Santos");

        var r = await Replicar();

        var adoptado = await _db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 12, 8));
        (adoptado.Origin, adoptado.NationalValuePublicId).Should().Be((HolidayOrigin.National, (Guid?)inmaculada.PublicId));
        var decretado = await _db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 11, 2));
        (decretado.Origin, decretado.Name, decretado.NationalValuePublicId).Should().Be((HolidayOrigin.Decreed, "Puente decretado", (Guid?)null));
        (await _db.Holidays.SingleAsync(h => h.Date == new DateOnly(2026, 12, 24))).Origin.Should().Be(HolidayOrigin.Manual);
        (await _db.Holidays.CountAsync()).Should().Be(3, "nada se duplica por fecha (UK_PAY_Holidays_Date)");
        var conteos = r.Efectos[EfectoDeReplicacionEnNomina.NombreDelEfecto].Conteos;
        (conteos["festivosAdoptados"], conteos["festivosLocalesConservados"]).Should().Be((1, 1));
    }

    // ---------------------------------------------------------------------------------- regla 2 y 3: insertar --

    [Fact]
    public async Task Lo_nuevo_se_inserta_y_cierra_la_vigencia_anterior_la_vispera()
    {
        ParametroDeNomina(LegalParameterCodes.Smmlv, 1_750_905m, Enero2026);
        var smmlv2027 = _central.Valor(CodigosDeValorNacional.Smmlv, "", new DateOnly(2027, 1, 1), 1_900_000m, "Pesos", fuente: "Decreto de 2026");
        var festivo = _central.Festivo(new DateOnly(2027, 1, 1), "Año nuevo");

        await Replicar();

        var versiones = await _db.PayrollLegalParameters.Where(p => p.Code == LegalParameterCodes.Smmlv).OrderBy(p => p.ValidFrom).ToListAsync();
        versiones.Should().HaveCount(2);
        versiones[0].ValidTo.Should().Be(new DateTime(2026, 12, 31), "como AddLegalParameterVersionCommand: la víspera de la nueva");
        (versiones[1].Value, versiones[1].ValidFrom, versiones[1].ValidTo, versiones[1].Source, versiones[1].Name, versiones[1].Kind)
            .Should().Be((1_900_000m, new DateTime(2027, 1, 1), (DateTime?)null, $"Valor nacional {smmlv2027.PublicId:D}", "Salario mínimo mensual legal vigente", LegalParameterKind.Amount));
        var nuevo = await _db.Holidays.SingleAsync();
        (nuevo.Date, nuevo.Name, nuevo.Origin, nuevo.Year, nuevo.NationalValuePublicId)
            .Should().Be((new DateOnly(2027, 1, 1), "Año nuevo", HolidayOrigin.National, (short)2027, (Guid?)festivo.PublicId));
    }

    [Fact]
    public async Task Una_cooperativa_nueva_recibe_el_SMMLV_y_la_UVT_que_ya_no_siembra_Nomina()
    {
        _central.Valor(CodigosDeValorNacional.Smmlv, "", new DateOnly(2026, 1, 1), 1_750_905m, "Pesos");
        _central.Valor(CodigosDeValorNacional.Uvt, "", new DateOnly(2026, 1, 1), 52_374m, "Pesos");

        await Replicar();

        (await _db.PayrollLegalParameters.Select(p => p.Code).ToListAsync()).Should().BeEquivalentTo([LegalParameterCodes.Smmlv, LegalParameterCodes.Uvt]);
        var uvt = await new LectorDeUvt(_db).LeerAsync(new DateOnly(2026, 6, 30));
        uvt.Value.Valor.Should().Be(52_374m, "LectorDeUvt sigue siendo el único lector y lee la proyección sin cambio");
        uvt.Value.Fuente.Should().StartWith(EfectoDeReplicacionEnNomina.PrefijoDeFuente);
    }

    [Fact]
    public async Task Una_correccion_central_cambia_el_valor_de_la_proyeccion()
    {
        var original = _central.Valor(CodigosDeValorNacional.Uvt, "", new DateOnly(2026, 1, 1), 52_000m, "Pesos");
        await Replicar();
        var correccion = _central.Valor(CodigosDeValorNacional.Uvt, "", new DateOnly(2026, 1, 1), 52_374m, "Pesos", corrige: original.PublicId);

        await Replicar();

        var fila = await _db.PayrollLegalParameters.SingleAsync(p => p.Code == LegalParameterCodes.Uvt);
        (fila.Value, fila.Source).Should().Be((52_374m, $"Valor nacional {correccion.PublicId:D}"));
    }

    // ------------------------------------------------------------------------------ regla 4: sólo la réplica --

    [Fact]
    public async Task AddLegalParameterVersionCommand_rechaza_SMMLV_y_UVT()
    {
        var handler = new AddLegalParameterVersionCommandHandler(_db, CopiasDePrueba.Reloj(Ahora), Substitute.For<ICurrentUserService>(),
            Substitute.For<IPayrollRunStaleMarker>());

        foreach (var codigo in new[] { "SMMLV", "uvt" })
        {
            var r = await handler.Handle(new AddLegalParameterVersionCommand(codigo, new DateTime(2027, 1, 1), 1_900_000m, null, "Decreto"), CancellationToken.None);
            r.Error.Code.Should().Be("Payroll.LegalParameter.NationalValue");
            r.Error.Message.Should().Be($"El {codigo.ToUpperInvariant()} es un valor nacional: lo registra la plataforma, no Nómina.");
        }
        (await _db.PayrollLegalParameters.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Los_comandos_de_festivos_no_retiran_un_festivo_nacional()
    {
        var d = new NominaTestData();
        var nacional = new Holiday { Date = new DateOnly(2026, 12, 8), Name = "Inmaculada Concepción", Origin = HolidayOrigin.National, Year = 2026,
            NationalValuePublicId = Guid.NewGuid(), CreatedBy = "Réplica" };
        d.Db.Holidays.Add(nacional);
        d.Db.SaveChanges();

        var retirar = await new DeleteHolidayCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter)
            .Handle(new DeleteHolidayCommand(nacional.PublicId), CancellationToken.None);
        var crear = await new CreateHolidayCommandHandler(d.Db, d.Clock, d.User, d.AuditEmitter)
            .Handle(new CreateHolidayCommand(new DateOnly(2026, 12, 9), "Otro", HolidayOrigin.National), CancellationToken.None);

        retirar.Error.Code.Should().Be(HolidayErrors.National.Code);
        crear.Error.Code.Should().Be(HolidayErrors.OriginInvalid.Code, "un National sólo lo escribe la réplica");
        (await d.Db.Holidays.SingleAsync(h => h.PublicId == nacional.PublicId)).IsDeleted.Should().BeFalse();
    }
}
