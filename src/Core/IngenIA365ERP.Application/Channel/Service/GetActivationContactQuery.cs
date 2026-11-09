using System.Net.Mail;
using IngenIA365ERP.Application.Channel.Common;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Compliance.HabeasData;
using IngenIA365ERP.Domain.Enums.Compliance;
using MediatR;

namespace IngenIA365ERP.Application.Channel.Service;

// Feature 021 (T059; FR-016, FR-019; research R9; contracts/erp-api-de-servicio.md): los datos de contacto de la persona en su entidad
// validadora, para que el canal le envíe el código de activación o de recuperación. La máscara es lo único que el canal muestra a la PWA;
// los valores completos van en `delivery` sólo a esta ruta de servicio. Toda lectura queda auditada como lectura sensible.

public sealed record ActivationContactDto(
    string FirstNames,
    string LastNames,
    ContactoEnmascaradoDto Email,
    ContactoEnmascaradoDto Mobile,
    ConsentimientoDeContactoDto ContactConsent,
    EntregaDeContactoDto Delivery);

public sealed record ContactoEnmascaradoDto(string? Masked, bool Available);

/// <summary>La última decisión del titular sobre el canal de contacto (Habeas Data); nulo = nunca decidió.</summary>
public sealed record ConsentimientoDeContactoDto(bool? Email, bool? Sms);

public sealed record EntregaDeContactoDto(string? Email, string? Mobile);

public sealed record GetActivationContactQuery(Guid PersonPublicId) : IRequest<Result<ActivationContactDto>>;

public sealed class GetActivationContactQueryHandler(IApplicationDbContext db, IAutorizacionDeDatos autorizaciones, AuditoriaDelCanal auditoria)
    : IRequestHandler<GetActivationContactQuery, Result<ActivationContactDto>>
{
    public async Task<Result<ActivationContactDto>> Handle(GetActivationContactQuery request, CancellationToken ct)
    {
        var p = await PersonaDelCanal.HabilitadaAsync(db, request.PersonPublicId, ct);
        if (p is null) return Result.Failure<ActivationContactDto>(PersonaDelCanal.ErrorNoHabilitada);

        var correo = ContactoDeActivacion.CorreoValido(p.Email);
        var celular = ContactoDeActivacion.CelularValido(p.Mobile);
        var porCorreo = await autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.ContactChannel, nameof(DataAuthorizationChannel.Email), ct);
        var porSms = await autorizaciones.VigenteAsync(p.PublicId, DataAuthorizationPurpose.ContactChannel, nameof(DataAuthorizationChannel.Sms), ct);

        await auditoria.LecturaAsync(AuditoriaDelCanal.EventoContactoDeActivacionLeido, "Person", p.PublicId,
            new { email = correo is not null, mobile = celular is not null }, ct);

        var nombres = string.Join(' ', new[] { p.FirstName, p.OtherNames }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        var apellidos = string.Join(' ', new[] { p.LastName, p.SecondLastName }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
        return Result.Success(new ActivationContactDto(
            nombres,
            apellidos,
            new ContactoEnmascaradoDto(ContactoDeActivacion.EnmascararCorreo(correo), correo is not null),
            new ContactoEnmascaradoDto(ContactoDeActivacion.EnmascararCelular(celular), celular is not null),
            new ConsentimientoDeContactoDto(porCorreo?.Autoriza, porSms?.Autoriza),
            new EntregaDeContactoDto(correo, celular)));
    }
}

/// <summary>Normalización y máscara de los datos de contacto que viajan al canal.</summary>
public static class ContactoDeActivacion
{
    /// <summary>El correo recortado si tiene forma de correo; nulo si no.</summary>
    public static string? CorreoValido(string? correo)
    {
        if (string.IsNullOrWhiteSpace(correo)) return null;
        var limpio = correo.Trim();
        if (limpio.Length > 254 || limpio.Contains(' ')) return null;
        return MailAddress.TryCreate(limpio, out var dir) && dir.Address == limpio && limpio.IndexOf('@') > 0 && dir.Host.Contains('.') ? limpio : null;
    }

    /// <summary>
    /// El celular colombiano en diez dígitos (empieza por 3), sin espacios, guiones ni el indicativo 57; nulo si no lo es. Un fijo no
    /// recibe SMS.
    /// </summary>
    public static string? CelularValido(string? celular)
    {
        if (string.IsNullOrWhiteSpace(celular)) return null;
        var digitos = new string(celular.Where(char.IsAsciiDigit).ToArray());
        if (digitos.Length == 12 && digitos.StartsWith("57", StringComparison.Ordinal)) digitos = digitos[2..];
        return digitos.Length == 10 && digitos[0] == '3' ? digitos : null;
    }

    /// <summary>«j•••@d•••.com»: la primera letra del usuario y del dominio, y la terminación del dominio.</summary>
    public static string? EnmascararCorreo(string? correo)
    {
        if (correo is null) return null;
        var arroba = correo.IndexOf('@');
        var usuario = correo[..arroba];
        var dominio = correo[(arroba + 1)..];
        var punto = dominio.LastIndexOf('.');
        var terminacion = punto > 0 ? dominio[punto..] : string.Empty;
        return $"{usuario[0]}•••@{dominio[0]}•••{terminacion}";
    }

    /// <summary>«•••••• 4321»: sólo los cuatro últimos dígitos.</summary>
    public static string? EnmascararCelular(string? celular) =>
        celular is null ? null : "•••••• " + celular[^4..];
}
