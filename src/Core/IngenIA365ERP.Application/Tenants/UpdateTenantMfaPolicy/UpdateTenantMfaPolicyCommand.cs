using IngenIA365ERP.Application.Common.Models;
using MediatR;

namespace IngenIA365ERP.Application.Tenants.UpdateTenantMfaPolicy;

/// <summary>
/// T099 — Activa o desactiva la política "MFA obligatorio" para una empresa, y
/// desde la etapa de métodos, también qué métodos acepta.
/// Al cambiar cualquiera de las dos cosas publica
/// <c>IMembershipChangedNotifier.PublishForTenantMembersAsync</c> para invalidar
/// la caché de cada miembro.
/// </summary>
/// <param name="MetodosAceptados">
/// Literales (<c>"Totp"</c>, <c>"WebAuthn"</c>). <b><c>null</c> significa «no
/// tocar»</b>, no «volver al valor por defecto».
///
/// <para>
/// Es un PUT, y en un PUT de reemplazo el campo ausente significaría reset — que
/// aquí ampliaría o restringiría los métodos aceptados sin que nadie lo pidiera,
/// con el efecto de dejar gente fuera por una llamada que sólo quería encender la
/// exigencia. Se prefiere la semántica parcial y decirlo, a la ortodoxia REST y
/// un incidente.
/// </para>
/// </param>
/// <param name="PermitirRecuperacionPorCorreo">
/// <c>null</c> significa «no tocar», igual que los métodos. <b>Apagada por
/// defecto</b>: encenderla es aceptar que quien controle un buzón pueda, con la
/// contraseña y una espera, retirarle el segundo factor a una persona.
/// </param>
/// <param name="HorasDeDemora">
/// Cuánto espera una solicitud antes de poder ejecutarse. Mínimo 1. Es lo que
/// convierte el ataque de silencioso e instantáneo en ruidoso y con tiempo para
/// reaccionar.
/// </param>
public sealed record UpdateTenantMfaPolicyCommand(
    Guid TenantPublicId,
    bool IsRequired,
    IReadOnlyList<string>? MetodosAceptados = null,
    bool? PermitirRecuperacionPorCorreo = null,
    int? HorasDeDemora = null
) : IRequest<Result<UpdateTenantMfaPolicyResult>>;

/// <param name="MiembrosSinMetodoAceptado">
/// Cuántas personas de esta cooperativa tienen segundo factor pero <b>ninguno</b>
/// de los métodos que la política acepta ahora. No son un error —el sistema las
/// manda a inscribir, no las deja fuera— pero es el número que convierte a una
/// administradora que decide informada en una que no encierra a su cooperativa
/// por accidente.
///
/// <para>
/// No incluye a quien no tiene ningún segundo factor: a esas personas ya las
/// afectaba <c>IsRequired</c> y no las cambia la máscara.
/// </para>
/// </param>
public sealed record UpdateTenantMfaPolicyResult(int MiembrosSinMetodoAceptado);
