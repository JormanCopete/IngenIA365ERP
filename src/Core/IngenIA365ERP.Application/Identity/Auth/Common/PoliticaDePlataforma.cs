using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Domain.Entities.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IngenIA365ERP.Application.Identity.Auth.Common;

/// <summary>
/// Qué métodos acepta la plataforma para el administrador maestro.
/// </summary>
public interface IPoliticaDePlataforma
{
    Task<MetodosMfa> MetodosAceptadosAsync(CancellationToken ct);
}

public sealed class PoliticaDePlataforma(
    IAdminDbContext adminDb,
    IConfiguration configuracion,
    ILogger<PoliticaDePlataforma> logger)
    : IPoliticaDePlataforma
{
    /// <summary>
    /// <b>El rescate del maestro.</b> Puesto a <c>true</c>, la política guardada se
    /// ignora y se aceptan todos los métodos.
    ///
    /// <para>
    /// Existe porque el maestro es la única cuenta sin salida: sólo él puede
    /// ejecutar <c>ForceMfaReset</c> —o sea, sólo puede rescatarse a sí mismo— y la
    /// doble aprobación corre sobre la base de una cooperativa donde no existe. Una
    /// máscara mal puesta aquí lo deja fuera de su propio sistema, y con él la
    /// capacidad de desbloquear a todos los demás. La alternativa era un
    /// procedimiento manual con SQL contra la base administrativa; esto lo puede
    /// hacer quien tenga acceso al despliegue, sin tocar datos.
    /// </para>
    ///
    /// <para>
    /// Se escribió y se probó ANTES de que la política existiera, a propósito: un
    /// rescate que llega después del riesgo no es un rescate.
    /// </para>
    /// </summary>
    public const string ClaveDelRescate = "Mfa:PlataformaSinRestriccion";

    public async Task<MetodosMfa> MetodosAceptadosAsync(CancellationToken ct)
    {
        if (configuracion.GetValue<bool>(ClaveDelRescate))
        {
            logger.LogWarning(
                "{Clave} está activo: la política de métodos de la plataforma se ignora y " +
                "se aceptan todos. Es un rescate, no un estado normal — apagalo cuando termines.",
                ClaveDelRescate);
            return ConversionDeMetodosMfa.Todos;
        }

        var mascara = await adminDb.PlatformMfaPolicies
            .AsNoTracking()
            .Where(p => p.Scope == PlatformMfaPolicy.FilaUnica)
            .Select(p => (MetodosMfa?)p.AllowedMethodsMask)
            .FirstOrDefaultAsync(ct);

        // Fila ausente = todos. Un arranque limpio no puede depender de que alguien
        // insertara una fila; si la ausencia significara «ninguno», una base recién
        // creada dejaría al maestro sin entrar antes de configurar nada.
        if (mascara is null) return ConversionDeMetodosMfa.Todos;

        // Fila presente pero vacía: no debería poder ocurrir —la entidad lo
        // prohíbe— así que si ocurre es que alguien escribió por SQL directo. Se
        // registra fuerte y se cae del lado que no encierra a nadie.
        if (mascara.Value == MetodosMfa.Ninguno)
        {
            logger.LogError(
                "ADM_PlatformMfaPolicy tiene la máscara vacía, que la entidad no permite escribir. " +
                "Se ignora y se aceptan todos los métodos para no dejar al maestro sin acceso.");
            return ConversionDeMetodosMfa.Todos;
        }

        return mascara.Value;
    }
}
