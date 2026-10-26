using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Receipts;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Common;

/// <summary>
/// Feature 015, T0017 (R-12; FR-007, FR-170; data-model §0.5; CH16): el SQL de <see cref="SqlDelCerrojoDeRecaudo"/> —una sentencia por nivel
/// ordenada por <c>Id</c>, <c>FOR UPDATE</c> en PostgreSQL y <c>WITH (UPDLOCK, ROWLOCK, HOLDLOCK)</c> en SQL Server, la fila de pagador que se crea si
/// falta— y el orden global que lleva <see cref="OrdenDeCerrojos"/>: los niveles en su orden, un nivel ya tomado no estorba, la numeración al
/// final y <b>ninguna fila de Recaudo después de una de Tesorería</b>, también cuando quien llama es Tesorería (por eso
/// <c>TomarCerrojosAsync</c> del puerto va antes de los cerrojos de Tesorería). Molde: <c>CerrojoDeCarteraSqlTests</c>.
/// </summary>
public class CerrojoDeRecaudoTests
{
    private static readonly DateTime Ahora = new(2026, 12, 1, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void La_configuracion_se_toma_compartida_o_exclusiva_en_cada_motor()
    {
        SqlDelCerrojoDeRecaudo.Setup(DatabaseProvider.PostgreSql, "RCD_Setup", exclusivo: false).Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"RCD_Setup\" ORDER BY \"Id\" FOR SHARE;");
        SqlDelCerrojoDeRecaudo.Setup(DatabaseProvider.SqlServer, "RCD_Setup", exclusivo: false).Sql
            .Should().Be("SELECT [Id] FROM [dbo].[RCD_Setup] WITH (ROWLOCK, HOLDLOCK) ORDER BY [Id];");
        SqlDelCerrojoDeRecaudo.Setup(DatabaseProvider.SqlServer, "RCD_Setup", exclusivo: true).Sql.Should().Contain("XLOCK, ROWLOCK, HOLDLOCK");
    }

    [Fact]
    public void Las_filas_de_un_nivel_van_en_una_sentencia_ordenada_por_Id_sin_repetidos()
    {
        var pg = SqlDelCerrojoDeRecaudo.Filas(DatabaseProvider.PostgreSql, "RCD_CashSessions", [9, 3, 9, 5], exclusivo: true)!;
        pg.Sql.Should().Be("SELECT \"Id\" FROM \"dbo\".\"RCD_CashSessions\" WHERE \"Id\" IN (3, 5, 9) ORDER BY \"Id\" FOR UPDATE;");

        var ss = SqlDelCerrojoDeRecaudo.Filas(DatabaseProvider.SqlServer, "RCD_CashSessions", [9, 3], exclusivo: true)!;
        ss.Sql.Should().Be("SELECT [Id] FROM [dbo].[RCD_CashSessions] WITH (UPDLOCK, ROWLOCK, HOLDLOCK) WHERE [Id] IN (3, 9) ORDER BY [Id];");

        SqlDelCerrojoDeRecaudo.Filas(DatabaseProvider.SqlServer, "RCD_CollectionPointDays", [1], exclusivo: false)!.Sql.Should().Contain("WITH (ROWLOCK, HOLDLOCK)");
        SqlDelCerrojoDeRecaudo.Filas(DatabaseProvider.PostgreSql, "RCD_CashSessions", [], exclusivo: true).Should().BeNull();
    }

    [Fact]
    public void Los_pagadores_que_faltan_se_crean_y_despues_se_bloquean()
    {
        var pg = SqlDelCerrojoDeRecaudo.Pagadores(DatabaseProvider.PostgreSql, [7, 2], "Ana", Ahora);
        pg.Should().HaveCount(2);
        pg[0].Sql.Should().Contain("INSERT INTO \"dbo\".\"RCD_PayerControls\"").And.Contain("ON CONFLICT DO NOTHING").And.Contain("(VALUES (2), (7))");
        pg[0].Parametros.Should().Equal(Ahora, "Ana");
        pg[1].Sql.Should().Be("SELECT \"Id\" FROM \"dbo\".\"RCD_PayerControls\" WHERE \"PersonId\" IN (2, 7) ORDER BY \"Id\" FOR UPDATE;");

        var ss = SqlDelCerrojoDeRecaudo.Pagadores(DatabaseProvider.SqlServer, [2], "Ana", Ahora);
        ss[0].Sql.Should().Contain("WHERE NOT EXISTS").And.Contain("WITH (UPDLOCK, HOLDLOCK)");
        ss[1].Sql.Should().Contain("WITH (UPDLOCK, ROWLOCK, HOLDLOCK)");
        SqlDelCerrojoDeRecaudo.Pagadores(DatabaseProvider.SqlServer, [], "Ana", Ahora).Should().BeEmpty();
    }

    [Fact]
    public void La_numeracion_nueva_va_sobre_COR_Sequences_con_su_clave_y_modulo()
    {
        var pg = SqlDelCerrojoDeRecaudo.NumeracionNueva(DatabaseProvider.PostgreSql, "RCD:RC:RC-", "RC-", "RCD", "Ana", Ahora);
        pg.Sql.Should().Contain("INSERT INTO \"dbo\".\"COR_Sequences\"").And.Contain("ON CONFLICT DO NOTHING");
        pg.Parametros.Should().Equal(Ahora, "Ana", "RCD:RC:RC-", "RC-", "RCD");
        SqlDelCerrojoDeRecaudo.NumeracionNueva(DatabaseProvider.SqlServer, "TES:LT", "LT-", "TES", "Ana", Ahora).Sql.Should().Contain("WHERE NOT EXISTS");
        var otro = () => SqlDelCerrojoDeRecaudo.NumeracionNueva(DatabaseProvider.SqlServer, "X", "", "LND", "Ana", Ahora);
        otro.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Las_tablas_de_cada_nivel_son_las_de_data_model()
    {
        // Fase 12 (DT §2.7): el nivel 6 admite también las líneas del archivo de descuentos de Cartera (TablasDeRecaudo.LineasDeDescuento).
        SqlDelCerrojoDeRecaudo.TablasDelNivel[NivelDeRecaudo.Origenes].Should().Equal(
            "RCD_PayerCreditBalances", "RCD_UnidentifiedItems", "RCD_PayrollDeductionFileLines", "RCD_PaymasterRemittanceLines", "RCD_ReceivedChecks", "RCD_Receipts");
        IngenIA365ERP.Persistence.Treasury.SqlDelCerrojoDeTesoreria.TablaDelNivel.Values.Should().Equal(
            "TES_BankAccounts", "TES_Checkbooks", "TES_PaymentBatches", "TES_PaymentOrders", "TES_Reconciliations");
    }

    [Fact]
    public void Los_niveles_se_toman_en_orden_y_un_nivel_ya_tomado_no_estorba()
    {
        var orden = new OrdenDeCerrojos();
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Configuracion), "RCD_Setup");
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Sesion), "RCD_CashSessions");
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Pagadores), "RCD_PayerControls");
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_PayerCreditBalances");
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_Receipts");
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.CuentaDeOrigen), "TES_BankAccounts");
        orden.Tomar(OrdenDeCerrojos.Numeracion, "COR_Sequences");

        var tarde = () => orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Sesion), "RCD_CashSessions");
        tarde.Should().Throw<InvalidOperationException>().WithMessage("*después de la numeración*");
    }

    [Fact]
    public void Una_persona_despues_de_un_origen_va_contra_el_orden()
    {
        var orden = new OrdenDeCerrojos();
        orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_UnidentifiedItems");
        var persona = () => orden.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Pagadores), "RCD_PayerControls");
        persona.Should().Throw<InvalidOperationException>().WithMessage("*orden fijo*");
    }

    /// <summary>
    /// CH16 con dos transacciones cruzadas: el resultado de una orden de Recaudo (Tesorería → Recaudo) y la anulación de la misma devolución en
    /// caja (Recaudo → Tesorería). Si las dos toman Recaudo antes que Tesorería no hay espera circular; si Tesorería tomara su lote antes del saldo
    /// a favor de Recaudo, el orden lo rechaza.
    /// </summary>
    [Fact]
    public void Tesoreria_toma_los_niveles_de_Recaudo_antes_que_los_suyos()
    {
        // Resultado de una orden de origen Receipts: TomarCerrojosAsync del puerto (RCD_Setup y el saldo a favor) y después Tesorería.
        var resultado = new OrdenDeCerrojos();
        resultado.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Configuracion), "RCD_Setup");
        resultado.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_PayerCreditBalances");
        resultado.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Configuracion), "TES_Setup");
        resultado.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Lote), "TES_PaymentBatches");
        resultado.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Ordenes), "TES_PaymentOrders");

        // Anulación de la devolución en caja: Recaudo primero, y la orden de Tesorería después por su puerto.
        var anulacion = new OrdenDeCerrojos();
        anulacion.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Configuracion), "RCD_Setup");
        anulacion.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_PayerCreditBalances");
        anulacion.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Ordenes), "TES_PaymentOrders");

        // Sin TomarCerrojosAsync, Tesorería llegaría a Recaudo con su lote ya tomado: el orden lo rechaza.
        var sinPuerto = new OrdenDeCerrojos();
        sinPuerto.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Lote), "TES_PaymentBatches");
        var tarde = () => sinPuerto.Tomar(OrdenDeCerrojos.DeRecaudo(NivelDeRecaudo.Origenes), "RCD_PayerCreditBalances");
        tarde.Should().Throw<InvalidOperationException>().WithMessage("*TomarCerrojosAsync*");
    }
}
