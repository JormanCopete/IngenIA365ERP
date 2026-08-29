using FluentValidation;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.SystemSettings;

// ---------------------------------------------------------------- listar ----

/// <summary>
/// Parámetros de configuración de la cooperativa activa (COR_SystemSettings).
///
/// <para>
/// Son por tenant, no globales: el país, la moneda funcional y los decimales
/// contables los decide cada cooperativa. Viven en el schema del tenant y el
/// filtro lo aplica el DbContext, no esta consulta.
/// </para>
/// </summary>
public sealed record ListarParametrosQuery(string? Modulo = null)
    : IRequest<Result<IReadOnlyList<ParametroDto>>>;

public sealed record ParametroDto(
    Guid PublicId,
    string Clave,
    string? Valor,
    string TipoValor,
    string? Descripcion,
    string? Modulo,
    DateTime? ActualizadoEn,
    string? ActualizadoPor);

public sealed class ListarParametrosQueryHandler(IApplicationDbContext db)
    : IRequestHandler<ListarParametrosQuery, Result<IReadOnlyList<ParametroDto>>>
{
    public async Task<Result<IReadOnlyList<ParametroDto>>> Handle(
        ListarParametrosQuery request, CancellationToken ct)
    {
        var consulta = db.SystemSettings.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Modulo))
            consulta = consulta.Where(s => s.ModulePrefix == request.Modulo);

        var parametros = await consulta
            .OrderBy(s => s.ModulePrefix).ThenBy(s => s.SettingKey)
            .Select(s => new ParametroDto(
                s.PublicId, s.SettingKey, s.SettingValue, s.ValueType,
                s.Description, s.ModulePrefix, s.UpdatedAt, s.UpdatedBy))
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<ParametroDto>>(parametros);
    }
}

// -------------------------------------------------------------- actualizar --

/// <summary>
/// Cambia el VALOR de un parámetro existente.
///
/// <para>
/// Deliberadamente no permite crear ni borrar parámetros desde la interfaz. Las
/// claves las declara el código que las consume —<c>Accounting.DecimalPlaces</c>
/// no significa nada si nadie la lee— y el sembrador paramétrico las crea. Dejar
/// que alguien invente una clave por pantalla produce filas que nadie consulta y,
/// peor, la ilusión de haber configurado algo.
/// </para>
/// </summary>
public sealed record ActualizarParametroCommand(Guid PublicId, string? Valor)
    : IRequest<Result>;

public sealed class ActualizarParametroCommandValidator
    : AbstractValidator<ActualizarParametroCommand>
{
    public ActualizarParametroCommandValidator()
    {
        RuleFor(x => x.PublicId).NotEmpty();
        RuleFor(x => x.Valor)
            .MaximumLength(2000)
            .WithMessage("El valor no puede superar 2000 caracteres.");
    }
}

public sealed class ActualizarParametroCommandHandler(
    IApplicationDbContext db,
    ICurrentUserService currentUser)
    : IRequestHandler<ActualizarParametroCommand, Result>
{
    public async Task<Result> Handle(ActualizarParametroCommand request, CancellationToken ct)
    {
        var parametro = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.PublicId == request.PublicId, ct);

        if (parametro is null)
            return Result.Failure("Parametros.NotFound", "No existe el parámetro indicado.");

        // El tipo declarado no es decorativo: quien lee Accounting.DecimalPlaces
        // hace int.Parse. Guardar "dos" en vez de "2" revienta al usarlo, lejos
        // de acá y sin pista de por qué.
        var error = ValidarSegunTipo(parametro.ValueType, request.Valor);
        if (error is not null)
            return Result.Failure("Validation.TipoDeValor", error);

        parametro.SettingValue = request.Valor;
        parametro.UpdatedBy = currentUser.UserName ?? "SYSTEM";
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static string? ValidarSegunTipo(string tipo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;

        return tipo.ToLowerInvariant() switch
        {
            "int" => int.TryParse(valor, out _)
                ? null
                : $"'{valor}' no es un número entero.",
            "decimal" => decimal.TryParse(valor,
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out _)
                ? null
                : $"'{valor}' no es un número decimal. Use punto como separador.",
            "bool" => bool.TryParse(valor, out _)
                ? null
                : $"'{valor}' no es verdadero/falso. Use true o false.",
            "date" => DateTime.TryParse(valor, out _)
                ? null
                : $"'{valor}' no es una fecha válida.",
            _ => null,
        };
    }
}
