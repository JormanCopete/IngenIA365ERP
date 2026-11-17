using IngenIA365ERP.Application.Core.People.Imports;
using IngenIA365ERP.Domain.Common.Identidad;

namespace IngenIA365ERP.Application.Core.People.Contracts;

/// <summary>
/// Las reglas del documento de una persona que se escribe desde un formulario (2026-11-16, revisión de crear y editar persona): el tipo
/// tiene que estar en el catálogo fijo de la feature 017 (<see cref="TiposDeDocumento"/>, por su código de persona: «C», «CE», «NI»…) y el
/// número, <b>normalizado</b>, pasa por <see cref="UsuarioDeDocumento.Validar"/> —sólo dígitos salvo el pasaporte y dentro de las
/// longitudes del tipo—, igual que la importación (<c>DocumentoDePlantilla</c>). Hasta ese día sólo se exigía el número no vacío y de
/// 20 caracteres: una CC «CE987654» derivaba el usuario <c>CE987654</c> y chocaba con la CE 987654, y un número «-» se guardaba vacío.
///
/// <para>
/// El NIT no lleva el dígito de verificación dentro del número («900123456-7» se rechaza: el DV va en su campo), y el DV, si viene, tiene
/// que ser el que da el algoritmo de la DIAN (<see cref="PlantillaDePersonas.DigitoDeVerificacionDian"/>). La pantalla replica estas reglas
/// en <c>Shared/Models/Personas/DocumentoDePersonaEnElCliente</c>.
/// </para>
/// </summary>
public static class DocumentoDePersona
{
    /// <summary>Código de error del documento que se rechaza al editar (la regla corre en el handler sólo si el documento cambió).</summary>
    public const string CodigoInvalido = "Person.DocumentInvalid";

    /// <summary>Una falla: la propiedad de <see cref="PersonInput"/> a la que se atribuye y el mensaje para la persona.</summary>
    public sealed record Falla(string Propiedad, string Mensaje);

    /// <summary>
    /// Las fallas del tipo, el número y el dígito de verificación. Vacía si el documento es válido. El número y el tipo vacíos no se
    /// reportan aquí: de eso se ocupan las reglas de obligatoriedad de <see cref="PersonInputValidator"/>.
    /// </summary>
    public static IReadOnlyList<Falla> Fallas(string? idType, string? taxId, string? digito)
    {
        var fallas = new List<Falla>();
        var tipo = TiposDeDocumento.PorCodigoDePersona(idType);
        if (tipo is null)
        {
            fallas.Add(new(nameof(PersonInput.IdType), "Tipo de documento no válido."));
            return fallas;
        }

        if (tipo == TiposDeDocumento.Nit && taxId is not null && taxId.Contains('-'))
        {
            fallas.Add(new(nameof(PersonInput.TaxId),
                "Escriba el NIT sin el dígito de verificación: el dígito va en su propio campo."));
            return fallas;
        }

        var validado = UsuarioDeDocumento.Validar(tipo.Codigo, taxId, soloDeAcceso: false, out var error);
        if (validado is null)
        {
            fallas.Add(new(nameof(PersonInput.TaxId), error ?? "Número de documento no válido."));
            return fallas;
        }

        var dv = digito?.Trim();
        if (tipo == TiposDeDocumento.Nit && dv is { Length: 1 } && char.IsAsciiDigit(dv[0])
            && PlantillaDePersonas.DigitoDeVerificacionDian(validado.Numero) is { } esperado && esperado != dv[0] - '0')
        {
            fallas.Add(new(nameof(PersonInput.TaxIdCheckDigit),
                $"El dígito de verificación del NIT {validado.Numero} es {esperado}, no {dv}."));
        }
        return fallas;
    }
}
