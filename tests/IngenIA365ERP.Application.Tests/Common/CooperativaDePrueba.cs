using IngenIA365ERP.Application.Common.Interfaces;
using NSubstitute;

namespace IngenIA365ERP.Application.Tests.Common;

/// <summary>
/// La cooperativa activa que ven los emisores de auditoría en las pruebas de handlers. Su
/// <c>TenantId</c> es el PublicId en formato N, como lo da <c>TenantContextAccessor</c> en la API:
/// los eventos explícitos tienen que ir a la misma base que la consola lee.
/// </summary>
public static class CooperativaDePrueba
{
    public const string PublicIdN = "0f8fad5bd9cb469fa16570867728950e";

    public static ICurrentTenantService Actual { get; } = Crear();

    private static ICurrentTenantService Crear()
    {
        var t = Substitute.For<ICurrentTenantService>();
        t.TenantId.Returns(PublicIdN);
        t.TenantName.Returns("Cooperativa de prueba");
        return t;
    }
}
