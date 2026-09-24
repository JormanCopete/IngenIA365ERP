using IngenIA365ERP.Application.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IngenIA365ERP.Application.Tests.Attachments;

/// <summary>
/// Una base en memoria que se puede hacer fallar al guardar, como una base que se cae en el peor momento.
/// Para probar el orden «primero el almacén, después la fila» y lo que queda si la fila no se guarda.
/// Cada <see cref="Db"/> es un contexto nuevo sobre la misma base: lo que vería la petición siguiente.
/// </summary>
internal sealed class BaseQueFallaAlGuardar
{
    private readonly string _nombre = Guid.NewGuid().ToString();
    private readonly Interceptor _interceptor = new();

    public bool Falla
    {
        get => _interceptor.Activo;
        set => _interceptor.Activo = value;
    }

    public TestApplicationDbContext Db() => new(new DbContextOptionsBuilder<TestApplicationDbContext>()
        .UseInMemoryDatabase(_nombre)
        .AddInterceptors(_interceptor)
        .Options);

    private sealed class Interceptor : SaveChangesInterceptor
    {
        public bool Activo { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) =>
            Activo ? throw new DbUpdateException("La base no respondió.") : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
