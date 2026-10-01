using FluentAssertions;
using IngenIA365ERP.Domain.Common;
using IngenIA365ERP.Domain.Entities.Approvals;
using IngenIA365ERP.Domain.Entities.Approvals.Transactions;
using IngenIA365ERP.Domain.Entities.Security;
using IngenIA365ERP.Persistence.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace IngenIA365ERP.Application.Tests.Common.Approvals;

/// <summary>
/// T081 (feature 012; data-model §21): el modelo real de <c>COR_Approval*</c> y <c>SEC_PermissionAmountLimits</c> en
/// los dos motores. InMemory no hace cumplir índices únicos: que la base rechace una segunda solicitud pendiente de la
/// misma fuente o una segunda aprobación del mismo nivel lo fijan aquí sus índices y, contra la base, la e2e tras la
/// migración <c>PlataformaParaInventario</c> (T186).
/// </summary>
public class ModeloDeAprobacionesTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Las_tablas_y_sus_unicos_son_los_de_data_model(bool postgres)
    {
        Unico<ApprovalPolicy>(postgres, "COR_ApprovalPolicies", "UK_COR_ApprovalPolicies_PolicyKey_ValidFrom", "IsDeleted",
            nameof(ApprovalPolicy.PolicyKey), nameof(ApprovalPolicy.ValidFrom));
        Unico<ApprovalPolicyLevel>(postgres, "COR_ApprovalPolicyLevels", "UK_COR_ApprovalPolicyLevels_PolicyId_Order", null,
            nameof(ApprovalPolicyLevel.PolicyId), nameof(ApprovalPolicyLevel.Order));
        Unico<ApprovalRequest>(postgres, "COR_ApprovalRequests", "UK_COR_ApprovalRequests_Source_Subject_Pending", "Status",
            nameof(ApprovalRequest.SourceType), nameof(ApprovalRequest.SourcePublicId), nameof(ApprovalRequest.Subject));
        Unico<ApprovalDecision>(postgres, "COR_ApprovalDecisions", "UK_COR_ApprovalDecisions_RequestId_Level_Approved", "Decision",
            nameof(ApprovalDecision.RequestId), nameof(ApprovalDecision.Level));
        Unico<PermissionAmountLimit>(postgres, "SEC_PermissionAmountLimits", "UK_SEC_PermissionAmountLimits_Role_Permission_ValidFrom", "IsDeleted",
            nameof(PermissionAmountLimit.RoleId), nameof(PermissionAmountLimit.PermissionCode), nameof(PermissionAmountLimit.ValidFrom));

        TipoDelModelo<ApprovalRequest>(postgres).GetIndexes()
            .Single(i => i.GetDatabaseName() == "IX_COR_ApprovalRequests_Status_Module_CurrentLevel")
            .Properties.Select(p => p.Name).Should().Equal(nameof(ApprovalRequest.Status), nameof(ApprovalRequest.Module), nameof(ApprovalRequest.CurrentLevel));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Las_llaves_foraneas_no_borran_en_cascada_y_la_decision_es_un_hecho(bool postgres)
    {
        foreach (var tipo in new[] { typeof(ApprovalPolicyLevel), typeof(ApprovalRequest), typeof(ApprovalDecision), typeof(PermissionAmountLimit) })
            Modelo(postgres).FindEntityType(tipo)!.GetForeignKeys().Should().OnlyContain(fk => fk.DeleteBehavior == DeleteBehavior.Restrict, tipo.Name);

        TipoDelModelo<ApprovalRequest>(postgres).GetForeignKeys().Select(fk => fk.PrincipalEntityType.ClrType)
            .Should().Contain(typeof(User)).And.Contain(typeof(ApprovalPolicy));
        typeof(IHechoInmutable).IsAssignableFrom(typeof(ApprovalDecision)).Should().BeTrue();
        typeof(ApprovalDecision).Namespace.Should().EndWith("Approvals.Transactions");

        var solicitud = TipoDelModelo<ApprovalRequest>(postgres);
        solicitud.FindProperty(nameof(ApprovalRequest.ContentSha256))!.GetMaxLength().Should().Be(64);
        solicitud.FindProperty(nameof(ApprovalRequest.ContentSha256))!.IsFixedLength().Should().BeTrue();
        solicitud.FindProperty(nameof(ApprovalRequest.RequiredLevelsJson))!.GetMaxLength().Should().Be(2000);
        solicitud.FindProperty(nameof(ApprovalRequest.ExcludedUserIdsJson))!.GetMaxLength().Should().Be(400);
        solicitud.FindProperty(nameof(ApprovalRequest.Amount))!.GetScale().Should().Be(2);
        TipoDelModelo<PermissionAmountLimit>(postgres).FindProperty(nameof(PermissionAmountLimit.MaxAmount))!.IsNullable.Should().BeTrue("nulo = sin límite");
    }

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
