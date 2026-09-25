using FluentAssertions;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Application.Core.People.Queries;

namespace IngenIA365ERP.Application.Tests.Core.People;

/// <summary>
/// El formulario de persona carga con <c>GET /api/core/people/{id}</c> (<see cref="PersonEditDto"/>)
/// y guarda con un <c>PUT</c> que manda todo <see cref="PersonInput"/>. Un campo que se escribe y
/// no se lee llega vacío al formulario y el guardado siguiente lo borra: pasó el 2026-09-25 con
/// «Segundo apellido» y «Otros nombres» (feature 010), en producción.
/// </summary>
public class LaEdicionDePersonaDevuelveTodoLoQueSeEscribe
{
    [Fact]
    public void Cada_propiedad_de_PersonInput_vuelve_en_PersonEditDto()
    {
        var escritas = typeof(PersonInput).GetProperties().Select(p => p.Name)
            .Where(n => n != "EqualityContract");
        var leidas = typeof(PersonEditDto).GetProperties().Select(p => p.Name).ToHashSet();

        escritas.Where(n => !leidas.Contains(n)).Should().BeEmpty(
            "lo que el PUT escribe y el GET no devuelve se pierde en la edición siguiente");
    }
}
