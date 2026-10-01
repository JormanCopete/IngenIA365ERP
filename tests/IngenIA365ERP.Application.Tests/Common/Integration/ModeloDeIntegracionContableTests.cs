using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Accounting;
using IngenIA365ERP.Domain.Entities.Accounting.Inventory;
using IngenIA365ERP.Domain.Entities.Accounting.Transactions;
using IngenIA365ERP.Domain.Entities.Core;
using IngenIA365ERP.Domain.Entities.Integration;
using IngenIA365ERP.Domain.Entities.Integration.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Common.Integration;

/// <summary>
/// Feature 012, T476–T483 (data-model §19 y §20; decisiones-transversales §2.2): el modelo real de las seis tablas de I2
/// en los dos motores. InMemory no hace cumplir únicos: que dos réplicas no creen la misma franja programada, que un
/// mensaje deje un solo recibo o que un intento no se repita lo fijan aquí sus índices y, contra la base, las e2e tras
/// la migración <c>IntegracionContableDeInventario</c> (T486).
/// </summary>
public class ModeloDeIntegracionContableTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Las_tablas_y_sus_unicos_son_los_de_data_model(bool postgres)
    {
        Unico<IntegrationDeliveryAttempt>(postgres, "COR_IntegrationDeliveryAttempts", "UK_COR_IntegrationDeliveryAttempts_Delivery_Attempt", null,
            nameof(IntegrationDeliveryAttempt.DeliveryId), nameof(IntegrationDeliveryAttempt.AttemptNumber));
        Unico<IntegrationBatch>(postgres, "COR_IntegrationBatches", "UK_COR_IntegrationBatches_Number", null, nameof(IntegrationBatch.Number));
        Unico<IntegrationBatch>(postgres, "COR_IntegrationBatches", "UK_COR_IntegrationBatches_Schedule", "Trigger",
            nameof(IntegrationBatch.ScheduleKey), nameof(IntegrationBatch.ScheduledFor));
        Unico<IntegrationBatch>(postgres, "COR_IntegrationBatches", "UK_COR_IntegrationBatches_CashSession", "Trigger",
            nameof(IntegrationBatch.CashSessionPublicId));
        Unico<InventoryPostingRule>(postgres, "ACC_InventoryPostingRules", "UK_ACC_InventoryPostingRules_DimensionKey_ValidFrom", "IsDeleted",
            nameof(InventoryPostingRule.DimensionKey), nameof(InventoryPostingRule.ValidFrom));
        Unico<InventoryVoucherMapping>(postgres, "ACC_InventoryVoucherMappings", "UK_ACC_InventoryVoucherMappings_MappingKey", "IsDeleted",
            nameof(InventoryVoucherMapping.MappingKey));
        Unico<InventoryPosting>(postgres, "ACC_InventoryPostings", "UK_ACC_InventoryPostings_MessagePublicId", null,
            nameof(InventoryPosting.MessagePublicId));
        TipoDelModelo<IntegrationBatchCounter>(postgres).GetTableName().Should().Be("COR_IntegrationBatchCounters");

        TipoDelModelo<InventoryPostingRule>(postgres).GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_ACC_InventoryPostingRules_Operation_Role_ValidFrom")
            .Properties.Select(p => p.Name).Should().Equal(nameof(InventoryPostingRule.Operation), nameof(InventoryPostingRule.Role), nameof(InventoryPostingRule.ValidFrom));

        var recibos = TipoDelModelo<InventoryPosting>(postgres).GetIndexes().Where(i => !i.IsUnique)
            .Select(i => i.Properties.Single().Name).ToList();
        recibos.Should().Contain([nameof(InventoryPosting.SourcePublicId), nameof(InventoryPosting.RelatedDocumentPublicId),
            nameof(InventoryPosting.AccountingDocumentId), nameof(InventoryPosting.BatchPublicId), nameof(InventoryPosting.OperationDate)]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Los_filtros_de_los_lotes_estan_escritos_para_el_motor(bool postgres)
    {
        var lote = TipoDelModelo<IntegrationBatch>(postgres);
        var franja = lote.GetIndexes().Single(i => i.GetDatabaseName() == "UK_COR_IntegrationBatches_Schedule").GetFilter();
        var sesion = lote.GetIndexes().Single(i => i.GetDatabaseName() == "UK_COR_IntegrationBatches_CashSession").GetFilter();
        if (postgres)
        {
            franja.Should().Be("\"Trigger\" = 1 AND \"IsDeleted\" = FALSE");
            sesion.Should().Be("\"Trigger\" = 2 AND \"IsDeleted\" = FALSE");
        }
        else
        {
            franja.Should().Be("[Trigger] = 1 AND [IsDeleted] = 0");
            sesion.Should().Be("[Trigger] = 2 AND [IsDeleted] = 0");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Las_llaves_foraneas_no_borran_en_cascada_ni_tocan_tablas_de_inventario(bool postgres)
    {
        var modelo = Modelo(postgres);
        foreach (var tipo in new[] { typeof(IntegrationDeliveryAttempt), typeof(IntegrationBatch), typeof(InventoryPostingRule), typeof(InventoryVoucherMapping), typeof(InventoryPosting) })
        {
            var fks = modelo.FindEntityType(tipo)!.GetForeignKeys().ToList();
            fks.Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict, tipo.Name);
            fks.Select(fk => fk.PrincipalEntityType.GetTableName()).Should().NotContain(t => t!.StartsWith("INV_"), tipo.Name);
        }

        Principales<InventoryPostingRule>(postgres).Should().BeEquivalentTo([typeof(ChartOfAccount), typeof(Branch), typeof(CostCenter)]);
        Principales<InventoryVoucherMapping>(postgres).Should().BeEquivalentTo([typeof(VoucherType), typeof(CrossDocumentType)]);
        Principales<InventoryPosting>(postgres).Should().BeEquivalentTo([typeof(AccountingDocument), typeof(User)]);
        Principales<IntegrationDeliveryAttempt>(postgres).Should().BeEquivalentTo([typeof(IntegrationMessageDelivery), typeof(User), typeof(IntegrationBatch)]);
        Principales<IntegrationBatch>(postgres).Should().BeEquivalentTo([typeof(User)]);

        // La columna BatchId de la entrega nació en I1 sin FK; la FK entra con la tabla de lotes (T480).
        var entrega = TipoDelModelo<IntegrationMessageDelivery>(postgres).GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(IntegrationBatch));
        entrega.Properties.Single().Name.Should().Be(nameof(IntegrationMessageDelivery.BatchId));
        entrega.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Tarifas_con_seis_decimales_y_columnas_con_su_largo(bool postgres)
    {
        // C8: ACC_AccountTaxRates.Rate pasa de (9,4) a (9,6) y la regla guarda la tarifa con la misma precisión (T482).
        Precision<AccountTaxRate>(postgres, nameof(AccountTaxRate.Rate)).Should().Be((9, 6));
        Precision<InventoryPostingRule>(postgres, nameof(InventoryPostingRule.TaxRate)).Should().Be((9, 6));

        var regla = TipoDelModelo<InventoryPostingRule>(postgres);
        regla.FindProperty(nameof(InventoryPostingRule.DimensionKey))!.GetMaxLength().Should().Be(200);
        regla.FindProperty(nameof(InventoryPostingRule.ReasonCode))!.GetMaxLength().Should().Be(40);
        regla.FindProperty(nameof(InventoryPostingRule.Notes))!.GetMaxLength().Should().Be(300);
        regla.FindProperty(nameof(InventoryPostingRule.SpecificityWeight))!.ClrType.Should().Be(typeof(short));

        var lote = TipoDelModelo<IntegrationBatch>(postgres);
        lote.FindProperty(nameof(IntegrationBatch.Number))!.ClrType.Should().Be(typeof(long));
        lote.FindProperty(nameof(IntegrationBatch.CutoffMessageId))!.ClrType.Should().Be(typeof(long?));
        Precision<IntegrationBatch>(postgres, nameof(IntegrationBatch.TotalDebit)).Should().Be((18, 2));
        lote.FindProperty(nameof(IntegrationBatch.RequestedByIp))!.GetMaxLength().Should().Be(45);
        lote.FindProperty(nameof(IntegrationBatch.Reason))!.GetMaxLength().Should().Be(500);

        var recibo = TipoDelModelo<InventoryPosting>(postgres);
        recibo.FindProperty(nameof(InventoryPosting.Id))!.ClrType.Should().Be(typeof(long));
        recibo.FindProperty(nameof(InventoryPosting.NoVoucherReason))!.GetMaxLength().Should().Be(20);
        recibo.FindProperty(nameof(InventoryPosting.SourceModule))!.GetMaxLength().Should().Be(3);

        TipoDelModelo<IntegrationDeliveryAttempt>(postgres).FindProperty(nameof(IntegrationDeliveryAttempt.Instance))!.GetMaxLength().Should().Be(100);
        // Concurrencia optimista: ROWVERSION en SQL Server, xmin en PostgreSQL (feature 004).
        TipoDelModelo<IntegrationBatchCounter>(postgres).GetProperties().Should().Contain(p => p.IsConcurrencyToken);
    }

    [Fact]
    public void El_intento_y_el_recibo_son_hechos_en_sus_carpetas_de_transacciones()
    {
        typeof(IHechoInmutable).IsAssignableFrom(typeof(IntegrationDeliveryAttempt)).Should().BeTrue();
        typeof(IHechoInmutable).IsAssignableFrom(typeof(InventoryPosting)).Should().BeTrue();
        typeof(IntegrationDeliveryAttempt).Namespace.Should().Be("IngenIA365ERP.Domain.Entities.Integration.Transactions");
        typeof(InventoryPosting).Namespace.Should().Be("IngenIA365ERP.Domain.Entities.Accounting.Transactions");
        typeof(IntegrationDeliveryAttempt).IsDefined(typeof(SinDiffDeAuditoriaAttribute), false).Should().BeTrue();
        typeof(AuditableEntityLong).IsAssignableFrom(typeof(IntegrationDeliveryAttempt)).Should().BeTrue();
        typeof(AuditableEntityLong).IsAssignableFrom(typeof(InventoryPosting)).Should().BeTrue();
        foreach (var hecho in new[] { typeof(IntegrationDeliveryAttempt), typeof(InventoryPosting) })
            hecho.GetProperties().Where(p => p.DeclaringType == hecho && p.SetMethod is { IsPublic: true })
                .Where(p => !p.SetMethod!.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)))
                .Select(p => p.Name).Should().BeEmpty(hecho.Name + " sólo tiene init");
    }

    private static (int?, int?) Precision<T>(bool postgres, string propiedad)
    {
        var p = TipoDelModelo<T>(postgres).FindProperty(propiedad)!;
        return (p.GetPrecision(), p.GetScale());
    }

    private static IEnumerable<Type> Principales<T>(bool postgres) =>
        TipoDelModelo<T>(postgres).GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType).Distinct();

    private static void Unico<T>(bool postgres, string tabla, string indice, string? filtroMenciona, params string[] columnas)
    {
        var tipo = TipoDelModelo<T>(postgres);
        tipo.GetTableName().Should().Be(tabla);
        var unico = tipo.GetIndexes().Single(i => i.GetDatabaseName() == indice);
        unico.IsUnique.Should().BeTrue(indice);
        unico.Properties.Select(p => p.Name).Should().Equal(columnas, indice);
        if (filtroMenciona is null) unico.GetFilter().Should().BeNull(indice);
        else unico.GetFilter().Should().Contain(filtroMenciona, indice);
    }

    private static IModel Modelo(bool postgres)
    {
        var builder = new DbContextOptionsBuilder<ApplicationDbContext>()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        builder = postgres
            ? builder.UseNpgsql("Host=localhost;Database=x;Username=y;Password=z")
            : builder.UseSqlServer("Server=localhost;Database=x;User Id=y;Password=z;TrustServerCertificate=True");
        using var db = new ApplicationDbContext(builder.Options);
        return db.Model;
    }

    private static IEntityType TipoDelModelo<T>(bool postgres) => Modelo(postgres).FindEntityType(typeof(T))!;
}
