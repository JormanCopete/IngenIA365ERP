using FluentAssertions;
using IngenIA365ERP.Application.Accounting.Accounts;
using IngenIA365ERP.Application.Accounting.Assets;
using IngenIA365ERP.Application.Accounting.Reports;
using IngenIA365ERP.Application.Common.Interfaces.Audit;
using IngenIA365ERP.Application.Tests.Accounting.Common;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Enums.Accounting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Accounting.Assets;

/// <summary>
/// T155 — US12 con el contrato: registrar no contabiliza; la corrida deja un DP cuadrado (gasto contra acumulada) y es única por
/// período; un activo que empezó en un mes cerrado acumula en su primera cuota; reversar deja la reversión y el mes vuelve a correrse;
/// sólo se reversa la última; con cuotas, las cuentas y el costo no cambian pero la vida útil sí; la baja cancela costo y acumulada.
/// El escenario es el de <see cref="ContabilidadTestData"/>: enero y febrero cerrados, marzo y abril abiertos.
/// </summary>
public class ActivosTests
{
    private sealed class Escenario
    {
        public ContabilidadTestData D { get; } = new();
        public AccountingAuditEmitter Emisor { get; }
        public AccountEligibility Elegibilidad { get; }
        public ChartOfAccount Equipo { get; }
        public ChartOfAccount Acumulada { get; }
        public ChartOfAccount Gasto { get; }
        public ChartOfAccount Perdida { get; }

        public Escenario()
        {
            Emisor = new AccountingAuditEmitter(Substitute.For<IAuditAppendOnlyWriter>(), D.User, D.Clock, NullLogger<AccountingAuditEmitter>.Instance, CooperativaDePrueba.Actual);
            Elegibilidad = new AccountEligibility(D.Db);
            D.Db.VoucherTypes.Add(new VoucherType { Code = "DP", Name = "Depreciación", Usage = VoucherUsage.Assets, ModuleCode = "ACT", IsSeeded = true, CreatedBy = "test" });
            D.Db.SaveChanges();
            Equipo = D.Cuenta("1524051", modulos: AccountingModules.Assets);
            Acumulada = D.Cuenta("1592051", AccountNature.Credit, AccountingModules.Assets);
            Gasto = D.Cuenta("5160051", modulos: AccountingModules.Assets);
            Perdida = D.Cuenta("5310051", modulos: AccountingModules.Assets);
        }

        public FixedAssetInput Entrada(string codigo = "EQ001", decimal costo = 6_000_000m, int vida = 60, DateOnly? inicio = null) =>
            new(codigo, "Computador", "Asset", Equipo.PublicId, Acumulada.PublicId, Gasto.PublicId, D.Principal.PublicId, null, null,
                new DateOnly(2026, 1, 10), "FC-1", costo, 0m, vida, inicio ?? new DateOnly(2026, 3, 1));

        public async Task<Guid> RegistrarAsync(FixedAssetInput entrada)
        {
            var r = await new RegisterFixedAssetCommandHandler(D.Db, Elegibilidad, D.Clock, D.User, Emisor).Handle(new RegisterFixedAssetCommand(entrada), default);
            r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
            return r.Value;
        }

        public Task<IngenIA365ERP.Application.Common.Models.Result<AssetRunDto>> CorrerAsync(int mes) =>
            new RunAssetDepreciationCommandHandler(D.Db, D.Poster, D.Clock, D.User, Emisor).Handle(new RunAssetDepreciationCommand(2026, mes), default);

        public Task<IngenIA365ERP.Application.Common.Models.Result<AssetRunDto>> ReversarAsync(Guid corrida) =>
            new ReverseAssetRunCommandHandler(D.Db, D.Poster, D.Clock, D.User, Emisor).Handle(new ReverseAssetRunCommand(corrida, "Valor equivocado"), default);
    }

    [Fact]
    public async Task La_corrida_deja_un_DP_cuadrado_y_no_se_repite()
    {
        var e = new Escenario();
        await e.RegistrarAsync(e.Entrada());

        var r = await e.CorrerAsync(3);
        r.IsSuccess.Should().BeTrue(r.IsFailure ? r.Error.Message : null);
        r.Value.Total.Should().Be(100_000m);
        var doc = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == r.Value.DocumentPublicId);
        doc.OriginModule.Should().Be("ACT");
        doc.Lines.Single(l => l.AccountId == e.Gasto.Id).Debit.Should().Be(100_000m);
        doc.Lines.Single(l => l.AccountId == e.Acumulada.Id).Credit.Should().Be(100_000m);

        (await e.CorrerAsync(3)).Error.Code.Should().Be("Accounting.Assets.AlreadyRun");
    }

    [Fact]
    public async Task Un_activo_que_empezo_en_un_mes_cerrado_acumula_en_su_primera_cuota()
    {
        var e = new Escenario();
        await e.RegistrarAsync(e.Entrada(inicio: new DateOnly(2026, 1, 1)));
        var r = await e.CorrerAsync(3);
        r.Value.Total.Should().Be(300_000m);
    }

    [Fact]
    public async Task Reversar_deja_la_reversion_y_el_mes_se_vuelve_a_correr()
    {
        var e = new Escenario();
        var activo = await e.RegistrarAsync(e.Entrada());
        var corrida = (await e.CorrerAsync(3)).Value;

        var reversa = await e.ReversarAsync(corrida.PublicId);
        reversa.IsSuccess.Should().BeTrue(reversa.IsFailure ? reversa.Error.Message : null);
        reversa.Value.ReversalDocumentPublicId.Should().NotBeNull();

        var ficha = await new GetFixedAssetQueryHandler(e.D.Db, e.D.Alcance).Handle(new GetFixedAssetQuery(activo), default);
        ficha.Value.Asset.Accumulated.Should().Be(0m);
        ficha.Value.Installments.Should().ContainSingle(i => i.Status == "Reversed");

        (await e.CorrerAsync(3)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Solo_se_reversa_la_ultima_corrida_y_no_se_corre_un_mes_anterior()
    {
        var e = new Escenario();
        await e.RegistrarAsync(e.Entrada());
        var marzo = (await e.CorrerAsync(3)).Value;
        (await e.CorrerAsync(4)).IsSuccess.Should().BeTrue();

        (await e.ReversarAsync(marzo.PublicId)).Error.Code.Should().Be("Accounting.Assets.NotLastRun");
    }

    [Fact]
    public async Task En_un_periodo_cerrado_no_se_corre()
    {
        var e = new Escenario();
        await e.RegistrarAsync(e.Entrada(inicio: new DateOnly(2026, 1, 1)));
        (await e.CorrerAsync(2)).Error.Code.Should().Be("Accounting.Assets.PeriodClosed");
    }

    [Fact]
    public async Task Con_cuotas_el_costo_no_cambia_pero_la_vida_util_si()
    {
        var e = new Escenario();
        var id = await e.RegistrarAsync(e.Entrada());
        await e.CorrerAsync(3);
        var corregir = new UpdateFixedAssetCommandHandler(e.D.Db, e.Elegibilidad, e.D.Clock, e.D.User, e.Emisor);

        (await corregir.Handle(new UpdateFixedAssetCommand(id, e.Entrada(costo: 7_000_000m), null), default)).Error.Code.Should().Be("Accounting.Assets.Locked");
        (await corregir.Handle(new UpdateFixedAssetCommand(id, e.Entrada(vida: 30), "Revisión de vida útil"), default)).IsSuccess.Should().BeTrue();

        var abril = await e.CorrerAsync(4);
        abril.Value.Total.Should().Be(Math.Round(5_900_000m / 29m, 2, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public async Task La_baja_cancela_costo_y_acumulada_contra_la_contrapartida()
    {
        var e = new Escenario();
        var id = await e.RegistrarAsync(e.Entrada());
        await e.CorrerAsync(3);

        var baja = await new RetireFixedAssetCommandHandler(e.D.Db, e.D.Poster, e.Elegibilidad, e.D.Clock, e.D.User, e.Emisor)
            .Handle(new RetireFixedAssetCommand(id, new DateOnly(2026, 4, 5), e.Perdida.PublicId, "Hurto"), default);
        baja.IsSuccess.Should().BeTrue(baja.IsFailure ? baja.Error.Message : null);

        var doc = await e.D.Db.AccountingDocuments.Include(d => d.Lines).SingleAsync(d => d.PublicId == baja.Value);
        doc.Lines.Single(l => l.AccountId == e.Acumulada.Id).Debit.Should().Be(100_000m);
        doc.Lines.Single(l => l.AccountId == e.Perdida.Id).Debit.Should().Be(5_900_000m);
        doc.Lines.Single(l => l.AccountId == e.Equipo.Id).Credit.Should().Be(6_000_000m);
        (await e.D.Db.FixedAssets.SingleAsync(a => a.PublicId == id)).Status.Should().Be(FixedAssetStatus.Retired);
    }

    [Fact]
    public async Task Un_activo_fijo_exige_la_cuenta_de_acumulada()
    {
        var e = new Escenario();
        var r = await new RegisterFixedAssetCommandHandler(e.D.Db, e.Elegibilidad, e.D.Clock, e.D.User, e.Emisor)
            .Handle(new RegisterFixedAssetCommand(e.Entrada() with { AccumulatedAccountPublicId = null }), default);
        r.Error.Code.Should().Be("Accounting.Assets.AccumulatedRequired");
    }
}
