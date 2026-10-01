using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Common.Persistence;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Application.Core.People.Contracts;
using IngenIA365ERP.Domain.Entities.Core;
using Microsoft.EntityFrameworkCore;

namespace IngenIA365ERP.Application.Core.People.Services;

/// <summary>
/// El alta de una persona —sola o con su rol— y, si vino, la autorización de datos del titular (feature 012, T46, T175).
/// Lo usan <c>CreatePersonCommand</c> y los compuestos <c>with-person</c>: un solo sitio que guarda un alta. (nuevo)
///
/// <list type="bullet">
/// <item><b>Sin autorización</b>, exactamente lo de la feature 008: prepara, un <c>SaveChangesAsync</c>, y un choque del
/// índice del documento se traduce a <c>Person.TaxIdDuplicate</c>/<c>TaxIdDeleted</c>.</item>
/// <item><b>Con autorización sobre una versión</b>, persona, rol y consentimiento van en <b>una transacción</b>
/// (<see cref="TransaccionExplicita"/>): el consentimiento necesita el Id de la persona, que sólo existe tras guardarla,
/// y <c>CMP_HabeasDataConsents</c> no tiene navegación a la persona. Si el consentimiento no se puede escribir, no queda
/// la persona. El choque del documento se traduce <b>después</b> de revertir (dentro de una transacción abortada,
/// PostgreSQL no deja consultar).</item>
/// <item><b>Sin política publicada</b>, el alta procede como sin autorización y deja la constancia «sin política
/// vigente» (<see cref="AutorizacionDeDatos.DejarConstanciaSinPoliticaAsync"/>).</item>
/// </list>
/// </summary>
public sealed class AltaConAutorizacion(
    IApplicationDbContext context,
    PersonFactory personas,
    AutorizacionDeDatos autorizaciones)
{
    /// <summary>
    /// Da de alta la persona de <paramref name="entrada"/>, deja que <paramref name="prepararRol"/> agregue su fila hija
    /// (sin guardar) y guarda todo. Devuelve la persona y lo que devolvió el rol.
    /// </summary>
    public async Task<Result<AltaRealizada<T>>> GuardarAsync<T>(
        PersonInput entrada,
        AutorizacionAlCrear? autorizacion,
        Func<Person, Task<Result<T>>> prepararRol,
        CancellationToken ct)
    {
        var resuelta = await autorizaciones.ResolverAsync(autorizacion, ct);
        if (resuelta.IsFailure) return Result.Failure<AltaRealizada<T>>(resuelta.Error);
        var decision = resuelta.Value;

        if (decision is null || decision.SinPolitica)
        {
            var alta = await PrepararAsync(entrada, prepararRol, ct);
            if (alta.IsFailure) return alta;
            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
            {
                // Dos usuarios crearon la misma persona a la vez: ambos pasaron la comprobación y el índice único paró
                // al segundo. Se le responde lo mismo que habría visto un segundo después, con el nombre de quien ganó.
                var colision = await personas.TraducirColisionAsync(ex, entrada.TaxId, ct);
                return Result.Failure<AltaRealizada<T>>(colision!);
            }
            if (decision is not null) await autorizaciones.DejarConstanciaSinPoliticaAsync(alta.Value.Persona, decision, ct);
            return alta;
        }

        DbUpdateException? choque = null;
        var resultado = await TransaccionExplicita.EjecutarAsync(context, async () =>
        {
            choque = null;
            var alta = await PrepararAsync(entrada, prepararRol, ct);
            if (alta.IsFailure) return alta;
            try
            {
                await context.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (PersonFactory.EsColisionDeDocumento(ex))
            {
                choque = ex;
                return Result.Failure<AltaRealizada<T>>(PersonFactory.DocumentoDuplicado("otra persona"));
            }
            autorizaciones.AgregarConsentimiento(alta.Value.Persona, decision);
            await context.SaveChangesAsync(ct);
            return alta;
        }, ct);

        if (choque is null) return resultado;
        context.DescartarCambios();
        var traducida = await personas.TraducirColisionAsync(choque, entrada.TaxId, ct);
        return Result.Failure<AltaRealizada<T>>(traducida ?? PersonFactory.DocumentoDuplicado("otra persona"));
    }

    private async Task<Result<AltaRealizada<T>>> PrepararAsync<T>(PersonInput entrada, Func<Person, Task<Result<T>>> prepararRol, CancellationToken ct)
    {
        var persona = await personas.PrepareAsync(entrada, ct);
        if (persona.IsFailure) return Result.Failure<AltaRealizada<T>>(persona.Error);
        var rol = await prepararRol(persona.Value);
        return rol.IsFailure
            ? Result.Failure<AltaRealizada<T>>(rol.Error)
            : Result.Success(new AltaRealizada<T>(persona.Value, rol.Value));
    }

    /// <summary>Para el alta de la persona sola: no hay rol que preparar.</summary>
    public static Task<Result<bool>> SinRol(Person _) => Task.FromResult(Result.Success(true));
}

/// <summary>La persona dada de alta y lo que agregó su rol. (nuevo)</summary>
public sealed record AltaRealizada<T>(Person Persona, T Rol);
