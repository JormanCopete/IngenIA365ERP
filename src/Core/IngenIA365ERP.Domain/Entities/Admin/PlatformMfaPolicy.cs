using IngenIA365ERP.Domain.Common;

namespace IngenIA365ERP.Domain.Entities.Admin;

/// <summary>
/// Qué métodos de segundo factor acepta la plataforma para el administrador
/// maestro. Fila única en <c>ADM_PlatformMfaPolicy</c>.
///
/// <para>
/// <b>Decide QUÉ, nunca SI.</b> No hay <c>IsRequired</c> y no lo va a haber: que
/// el maestro tenga segundo factor es una constante del sistema —su login
/// devuelve un desafío, nunca una sesión directa— y hacerlo editable pondría el
/// interruptor de apagar la seguridad de la cuenta más poderosa al alcance de
/// quien logre usarla una vez.
/// </para>
///
/// <para>
/// <b>La fila puede no existir, y eso significa «todos los métodos».</b> Un
/// arranque limpio no puede depender de que alguien se acordara de insertarla: si
/// la ausencia significara «ninguno», una base recién creada dejaría al maestro
/// sin poder entrar antes de que nadie hubiera configurado nada.
/// </para>
///
/// <para>
/// El maestro sólo puede rescatarse a sí mismo —<c>ForceMfaReset</c> exige ser
/// maestro, y la doble aprobación corre sobre la base de una cooperativa donde él
/// no existe—, así que una máscara mal puesta aquí no tiene salida por la
/// aplicación. Por eso existe el interruptor de configuración que la ignora; ver
/// <c>PoliticaDePlataforma</c>.
/// </para>
/// </summary>
public class PlatformMfaPolicy : AuditableEntity
{
    /// <summary>
    /// Discriminador de fila única. Siempre <see cref="FilaUnica"/>.
    ///
    /// <para>
    /// Es una columna con índice único y no «confía en que sólo haya una fila»:
    /// no hay precedente de tabla de fila única en este repositorio, y la forma
    /// que sí tiene precedente —índice único filtrado sobre una columna de
    /// ámbito— necesita una columna de ámbito. Ésta lo es, con un solo valor
    /// legal.
    /// </para>
    /// </summary>
    public const string FilaUnica = "global";

    public string Scope { get; private set; } = FilaUnica;

    public MetodosMfa AllowedMethodsMask { get; private set; } = ConversionDeMetodosMfa.Todos;

    public DateTime? ChangedAt { get; private set; }
    public Guid? ChangedByUserId { get; private set; }

    // EF Core
    private PlatformMfaPolicy() { }

    public static PlatformMfaPolicy Inicial() =>
        new() { Scope = FilaUnica, AllowedMethodsMask = ConversionDeMetodosMfa.Todos };

    /// <exception cref="InvalidOperationException">
    /// Si se intenta dejarla sin métodos. Aquí no hay matiz posible: el maestro
    /// siempre necesita segundo factor, así que una máscara vacía lo deja fuera de
    /// su propio sistema sin ninguna pantalla que lo saque.
    /// </exception>
    public void PermitirMetodos(MetodosMfa metodos, Guid byUserId, DateTime now)
    {
        if (metodos == MetodosMfa.Ninguno)
        {
            throw new InvalidOperationException(
                "La política de la plataforma no puede quedarse sin métodos: el segundo " +
                "factor del maestro es obligatorio y se quedaría sin ninguno que usar.");
        }

        AllowedMethodsMask = metodos;
        ChangedAt = now;
        ChangedByUserId = byUserId;
    }
}
