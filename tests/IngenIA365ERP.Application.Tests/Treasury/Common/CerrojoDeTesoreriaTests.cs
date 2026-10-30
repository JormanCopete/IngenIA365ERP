using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Common;
using IngenIA365ERP.Application.Treasury.Common;
using IngenIA365ERP.Persistence.Providers;
using IngenIA365ERP.Persistence.Treasury;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Treasury.Common;

/// <summary>
/// Feature 015, T0017 (R-12; data-model §0.5): el SQL de <see cref="SqlDelCerrojoDeTesoreria"/> —la configuración compartida, los cinco niveles por
/// <c>Id</c> en una sentencia, compartido o exclusivo— y el orden de sus niveles en <see cref="OrdenDeCerrojos"/>.
/// </summary>
public class CerrojoDeTesoreriaTests
{
    [Fact]
    public void La_configuracion_de_Tesoreria_se_toma_compartida()
    {
        SqlDelCerrojoDeTesoreria.Setup(DatabaseProvider.PostgreSql, exclusivo: false).Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"TES_Setup\" ORDER BY \"Id\" FOR SHARE;");
        SqlDelCerrojoDeTesoreria.Setup(DatabaseProvider.SqlServer, exclusivo: false).Sql
            .Should().Be("SELECT [Id] FROM [dbo].[TES_Setup] WITH (ROWLOCK, HOLDLOCK) ORDER BY [Id];");
    }

    [Fact]
    public void Cada_nivel_bloquea_su_tabla_por_Id()
    {
        SqlDelCerrojoDeTesoreria.Filas(DatabaseProvider.PostgreSql, NivelDeTesoreria.Ordenes, [4, 1], exclusivo: true)!.Sql
            .Should().Be("SELECT \"Id\" FROM \"dbo\".\"TES_PaymentOrders\" WHERE \"Id\" IN (1, 4) ORDER BY \"Id\" FOR UPDATE;");
        SqlDelCerrojoDeTesoreria.Filas(DatabaseProvider.SqlServer, NivelDeTesoreria.CuentaDeOrigen, [2], exclusivo: false)!.Sql
            .Should().Be("SELECT [Id] FROM [dbo].[TES_BankAccounts] WITH (ROWLOCK, HOLDLOCK) WHERE [Id] IN (2) ORDER BY [Id];");
        var configuracion = () => SqlDelCerrojoDeTesoreria.Filas(DatabaseProvider.SqlServer, NivelDeTesoreria.Configuracion, [1], true);
        configuracion.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Los_niveles_de_Tesoreria_van_en_su_orden()
    {
        var orden = new OrdenDeCerrojos();
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Configuracion), "TES_Setup");
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.CuentaDeOrigen), "TES_BankAccounts");
        orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.Lote), "TES_PaymentBatches");
        var cuentaDespues = () => orden.Tomar(OrdenDeCerrojos.DeTesoreria(NivelDeTesoreria.CuentaDeOrigen), "TES_BankAccounts");
        cuentaDespues.Should().Throw<InvalidOperationException>();
    }
}
