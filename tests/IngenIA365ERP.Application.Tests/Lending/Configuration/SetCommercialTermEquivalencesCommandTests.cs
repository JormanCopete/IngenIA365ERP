using FluentAssertions;
using IngenIA365ERP.Application.Lending.Configuration;
using IngenIA365ERP.Domain.Enums.Lending;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Tests.Lending.Configuration;

/// <summary>
/// Feature 013, T0337 (US1; api.md §3.2; data-model §1.9; FR-022, T17): las equivalencias de Comercial por
/// <see cref="SetCommercialTermEquivalencesCommandHandler"/>. Son versiones de sólo inserción leídas con <c>VigenciaBitemporal</c> a la
/// fecha de la venta (<see cref="EquivalenciasDeComercio"/>): registrar otra vez el mismo texto crea la versión siguiente y la anterior
/// sigue rigiendo antes de su fecha; un texto repetido en el lote es <c>Lending.CommercialTermEquivalence.Duplicate</c> y una periodicidad
/// desconocida <c>.PeriodicityUnknown</c>, sin guardar nada.
/// </summary>
public sealed class SetCommercialTermEquivalencesCommandTests : IDisposable
{
    private readonly LineasDePrueba _l = new();

    public void Dispose() => _l.Dispose();

    private async Task<IngenIA365ERP.Application.Common.Models.Result<IReadOnlyList<CommercialTermEquivalenceDto>>> Registrar(DateOnly desde, params CommercialTermEquivalenceInput[] items)
    {
        var r = await new SetCommercialTermEquivalencesCommandHandler(_l.Db, _l.G.Reloj)
            .Handle(new SetCommercialTermEquivalencesCommand(items, desde, "Equivalencias acordadas con Comercial") { OperationKey = Guid.NewGuid() }, default);
        _l.Db.ChangeTracker.Clear();
        return r;
    }

    [Fact]
    public async Task Se_versionan_por_texto_y_se_leen_a_la_fecha_de_la_venta()
    {
        var primera = await Registrar(new DateOnly(2026, 11, 1),
            new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, " monthly ", RepaymentPeriodicity.Monthly),
            new CommercialTermEquivalenceInput(CommercialTermKind.TermUnit, "Months", DaysPerUnit: 30));
        var segunda = await Registrar(new DateOnly(2027, 1, 1),
            new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, "MONTHLY", RepaymentPeriodicity.Biweekly));

        primera.IsSuccess.Should().BeTrue(primera.IsFailure ? primera.Error.Message : string.Empty);
        primera.Value.Should().HaveCount(2);
        primera.Value.Should().Contain(e => e.CommercialCode == "MONTHLY" && e.VersionNumber == 1);
        segunda.IsSuccess.Should().BeTrue();
        (await _l.Db.CommercialTermEquivalences.CountAsync()).Should().Be(3, "una versión nueva no reescribe la anterior");
        (await _l.Db.CommercialTermEquivalences.SingleAsync(e => e.VersionNumber == 2)).CommercialCode.Should().Be("MONTHLY");

        var lector = new EquivalenciasDeComercio(_l.Db);
        (await lector.PeriodicidadAsync("monthly", new DateOnly(2026, 12, 15))).Should().Be(RepaymentPeriodicity.Monthly, "la venta es anterior a la versión 2");
        (await lector.PeriodicidadAsync("Monthly", new DateOnly(2027, 1, 2))).Should().Be(RepaymentPeriodicity.Biweekly);
        (await lector.PeriodicidadAsync("Weekly", new DateOnly(2027, 1, 2))).Should().BeNull("sin equivalencia el mensaje queda no convertible");
        (await lector.DiasPorUnidadAsync("MONTHS", new DateOnly(2026, 12, 1))).Should().Be(30);
        (await lector.PeriodicidadAsync("MONTHLY", new DateOnly(2026, 10, 1))).Should().BeNull("antes de la primera versión no hay equivalencia");

        var lista = await new ListCommercialTermEquivalencesQueryHandler(_l.Db, _l.G.Reloj).Handle(new ListCommercialTermEquivalencesQuery(new DateOnly(2027, 2, 1)), default);
        lista.Value.Should().Contain(e => e.CommercialCode == "MONTHLY" && e.Periodicity == RepaymentPeriodicity.Biweekly);
    }

    [Fact]
    public async Task Un_texto_repetido_en_el_lote_es_Duplicate()
    {
        var r = await Registrar(LineasDePrueba.Hoy,
            new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, "Weekly", RepaymentPeriodicity.Weekly),
            new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, "WEEKLY", RepaymentPeriodicity.Biweekly));

        r.Error.Code.Should().Be("Lending.CommercialTermEquivalence.Duplicate");
        (await _l.Db.CommercialTermEquivalences.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Una_periodicidad_desconocida_es_PeriodicityUnknown()
    {
        var sinPeriodicidad = await Registrar(LineasDePrueba.Hoy, new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, "Lunar"));
        var fueraDelEnum = await Registrar(LineasDePrueba.Hoy, new CommercialTermEquivalenceInput(CommercialTermKind.Periodicity, "Lunar", (RepaymentPeriodicity)29));

        sinPeriodicidad.Error.Code.Should().Be("Lending.CommercialTermEquivalence.PeriodicityUnknown");
        fueraDelEnum.Error.Code.Should().Be("Lending.CommercialTermEquivalence.PeriodicityUnknown");
        (await _l.Db.CommercialTermEquivalences.CountAsync()).Should().Be(0);
    }
}
