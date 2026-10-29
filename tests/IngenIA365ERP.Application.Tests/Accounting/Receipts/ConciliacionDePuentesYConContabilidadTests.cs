using FluentAssertions;
using IngenIA365ERP.Application.Accounting.RecaudoYTesoreria;
using IngenIA365ERP.Application.Common.Integration.Accounting;
using IngenIA365ERP.Application.Common.Integration.Contracts.Receipts;
using IngenIA365ERP.Application.Tests.Common;
using IngenIA365ERP.Domain.Entities.Accounting.Integration;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using static IngenIA365ERP.Application.Tests.Accounting.Receipts.MatricesDePrueba;

namespace IngenIA365ERP.Application.Tests.Accounting.Receipts;

/// <summary>
/// Feature 015, US8, T0252 (FR-144, FR-146; SC-014): el saldo contable que piden los cuadres de Recaudo y Tesorería sale del libro contabilizado sobre las cuentas a
/// las que la matriz lleva el rol —caja por oficina, partidas, saldos a favor— o sobre la cuenta vinculada a la cuenta propia (bancos); sin regla para el rol no hay
/// dato (nunca un cero inventado); los hechos <c>REC</c> y <c>TES</c> que tocan los puentes de Cartera llevan el puente del origen por código. Prueba escrita, sin
/// correr.
/// </summary>
public class ConciliacionDePuentesYConContabilidadTests
{
    private readonly TestApplicationDbContext _db = TestDbContextFactory.Create();

    private void Asiento(int cuenta, int sucursal, decimal debito, decimal credito, bool contabilizado = true) =>
        _db.JournalEntries.Add(new JournalEntry
        {
            DocumentId = 1, LineNumber = 1, AccountId = cuenta, BranchId = sucursal, Debit = debito, Credit = credito, Date = Hoy.AddDays(-1), IsPosted = contabilizado,
        });

    [Fact]
    public async Task La_caja_de_la_oficina_suma_el_libro_contabilizado_de_las_cuentas_del_rol()
    {
        var oficina = new Branch { Name = "Principal", CreatedBy = "test" };
        var otra = new Branch { Name = "Norte", CreatedBy = "test" };
        _db.Branches.AddRange(oficina, otra);
        var caja = Cuenta("110505");
        _db.ChartOfAccounts.Add(caja);
        _db.ReceiptsPostingRules.Add(Regla<ReceiptsPostingRule>(RolesDeRecaudo.Caja, caja));
        await _db.SaveChangesAsync();
        Asiento(caja.Id, oficina.Id, 100m, 0m);
        Asiento(caja.Id, oficina.Id, 0m, 30m);
        Asiento(caja.Id, otra.Id, 500m, 0m);
        Asiento(caja.Id, oficina.Id, 999m, 0m, contabilizado: false);
        await _db.SaveChangesAsync();

        var saldos = await SaldosPorRol.SaldoAsync(_db, new ResolutorDeLaMatriz(_db), MatricesDeRecaudoYTesoreria.Recaudo,
            new SolicitudDeSaldo(RolesDeRecaudo.Caja, Hoy, oficina.PublicId, null), default);

        saldos.Should().ContainSingle().Which.Balance.Should().Be(70m);
    }

    [Fact]
    public async Task Sin_regla_para_el_rol_no_hay_dato_y_el_banco_sale_del_vinculo()
    {
        (await SaldosPorRol.SaldoAsync(_db, new ResolutorDeLaMatriz(_db), MatricesDeRecaudoYTesoreria.Recaudo,
            new SolicitudDeSaldo(RolesDeRecaudo.PartidasPorIdentificar, Hoy, null, null), default)).Should().BeEmpty();

        var bancaria = Cuenta("111005");
        _db.ChartOfAccounts.Add(bancaria);
        var propia = Guid.NewGuid();
        _db.OwnBankAccountLinks.Add(new OwnBankAccountLink { OwnBankAccountPublicId = propia, ChartOfAccountId = bancaria.Id, ValidFrom = Hoy.AddYears(-1) });
        await _db.SaveChangesAsync();
        Asiento(bancaria.Id, 1, 250m, 0m);
        await _db.SaveChangesAsync();

        var saldos = await SaldosPorRol.SaldoAsync(_db, new ResolutorDeLaMatriz(_db), MatricesDeRecaudoYTesoreria.Recaudo,
            new SolicitudDeSaldo(RolesDeRecaudo.BancoRecaudo, Hoy, null, propia), default);
        saldos.Single().Balance.Should().Be(250m);
    }

    [Fact]
    public void Los_hechos_que_tocan_los_puentes_de_Cartera_llevan_el_modulo_y_la_clase_del_recaudador()
    {
        var recibo = new ReciboEmitidoV1
        {
            Lines =
            [
                new FactLineV1 { Role = RolesDeRecaudo.PuenteDelModulo, Side = RolesDeRecaudo.Credito, Amount = 10m,
                    Dimensions = new ReceiptsFactDimensionsV1 { TargetModule = "CAR", CollectorClass = "Receipts" } },
            ],
        };
        var lineas = HechoContableDeModulo.Leer(Mensaje("REC", recibo, ReciboEmitidoV1.Type).PayloadJson);

        lineas.Single().Dimensiones.Should().Contain(new KeyValuePair<string, string>("TargetModule", "CAR"))
            .And.Contain(new KeyValuePair<string, string>("CollectorClass", "Receipts"));
        lineas.Single().Debito.Should().BeFalse();
    }
}
