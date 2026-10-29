using FluentAssertions;
using IngenIA365ERP.Application.Receipts.Security;
using Xunit;

namespace IngenIA365ERP.Application.Tests.Receipts.Security;

/// <summary>
/// Feature 015 (decisión del dueño del 2026-10-23, DT §2.7): las pantallas de configuración de Recaudo eligen usuarios por una consulta propia, con el
/// permiso de configuración de Recaudo: sólo los activos, con su id público y su nombre, filtrables por texto.
/// </summary>
public class UsuariosDeRecaudoTests
{
    private readonly EscenarioDeRecaudo _e = new();

    [Fact]
    public async Task Lista_los_usuarios_activos_con_su_nombre()
    {
        _e.SinPersona.IsActive = false;
        await _e.Db.SaveChangesAsync();

        var todos = await new ListReceiptsUsersQueryHandler(_e.Db).Handle(new ListReceiptsUsersQuery(), CancellationToken.None);
        var filtrados = await new ListReceiptsUsersQueryHandler(_e.Db).Handle(new ListReceiptsUsersQuery("carla"), CancellationToken.None);

        todos.Value.Items.Select(u => u.Username).Should().Contain(["carla", "sergio", "oscar"]).And.NotContain("sinpersona");
        filtrados.Value.Items.Select(u => u.Username).Should().BeEquivalentTo(["carla", "carla.bis"]);
    }
}
