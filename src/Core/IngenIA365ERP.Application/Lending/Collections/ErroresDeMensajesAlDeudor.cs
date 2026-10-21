using System.Globalization;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Domain.Entities.Lending.Collections;
using IngenIA365ERP.Domain.Enums.Lending;

namespace IngenIA365ERP.Application.Lending.Collections;

/// <summary>
/// Los errores de los mensajes al deudor (feature 013, T0306–T0308; T74, T75; contracts/api.md §14.1–§14.2; data-model §10.1, C111). Los
/// <c>*.NotFound</c> son 404 —también fuera del alcance, con el mismo mensaje—; los demás, 422 con <c>data</c> para corregir. Los mensajes van
/// en español claro y dicen qué hacer (FR-013). (nuevo)
/// </summary>
public static class ErroresDeMensajesAlDeudor
{
    public const string CodigoDestinatarioNoObligado = DebtorMessage.CodigoDestinatarioNoObligado;
    public const string CodigoCanalNoAutorizado = "Lending.DebtorMessage.ChannelNotAuthorized";
    public const string CodigoFueraDeVentana = "Lending.DebtorMessage.OutsideLegalWindow";
    public const string CodigoCanalSemanalUsado = "Lending.DebtorMessage.WeeklyChannelUsed";
    public const string CodigoMensajeInexistente = "Lending.DebtorMessage.NotFound";
    public const string CodigoYaSalio = "Lending.DebtorMessage.AlreadySent";
    public const string CodigoAutorizacionSinEvidencia = "Lending.Authorization.WithoutEvidence";
    public const string CodigoPlantillaInexistente = "Lending.MessageTemplate.NotFound";
    public const string CodigoCreditoInexistente = "Lending.Loan.NotFound";

    /// <summary>Cartera sin activar: la cola no programa nada antes del registro de la activación (FR-142).</summary>
    public const string CodigoCarteraSinActivar = IngenIA365ERP.Application.Common.Integration.Lending.ContratoDeObligacionesSinActivar.CodigoNoActivada;

    /// <summary>El formato del instante permitido en los mensajes al usuario: día y hora de Colombia.</summary>
    public static string Instante(DateTimeOffset instante) =>
        instante.ToString("dd/MM/yyyy 'a las' h:mm tt", CultureInfo.GetCultureInfo("es-CO"));

    public static Error DestinatarioNoObligado() => new(CodigoDestinatarioNoObligado,
        "Sólo se puede escribir al deudor y a los codeudores obligados del crédito.");

    public static Error CanalNoAutorizado(string persona, MessageChannel canal) => new ErrorConDatos(CodigoCanalNoAutorizado,
        $"{persona} no autorizó el canal {NombreDelCanal(canal)}.", new { channel = canal.ToString() });

    public static Error AutorizacionSinEvidencia(string persona, string finalidad) => new ErrorConDatos(CodigoAutorizacionSinEvidencia,
        $"La autorización de {persona} para {finalidad} no tiene evidencia. Adjúntela para continuar.", new { purpose = finalidad });

    public static Error FueraDeVentana(DateTimeOffset siguiente, IReadOnlyList<ContactWindowReason> motivos) => new ErrorConDatos(CodigoFueraDeVentana,
        $"Está fuera del horario que permite la ley. Puede enviarlo desde el {Instante(siguiente)}.",
        new { nextAllowedAt = siguiente, reasons = motivos.Select(m => m.ToString()).ToArray() });

    public static Error CanalSemanalUsado(MessageChannel? canalUsado, DateOnly inicioDeSemana, DateTimeOffset siguiente) =>
        new ErrorConDatos(CodigoCanalSemanalUsado,
            canalUsado is { } c
                ? $"Esta semana ya hubo un contacto directo por {NombreDelCanal(c)}: sólo se puede usar ese canal. Por otro canal, desde el {Instante(siguiente)}."
                : $"Esta semana ya hubo un contacto directo por otro canal: sólo se puede usar ese canal. Por éste, desde el {Instante(siguiente)}.",
            new
            {
                channelUsed = canalUsado?.ToString(),
                weekStart = inicioDeSemana.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                nextAllowedAt = siguiente,
            });

    public static Error MensajeInexistente() => new(CodigoMensajeInexistente, "El mensaje al deudor no existe.");

    public static Error YaSalio(DebtorMessageEventKind estado) => new ErrorConDatos(CodigoYaSalio,
        $"El mensaje ya no está en cola (estado: {NombreDelEstado(estado)}): sólo se cancela antes de salir.", new { status = estado.ToString() });

    public static Error PlantillaInexistente(string code, MessageChannel? canal) => new ErrorConDatos(CodigoPlantillaInexistente,
        canal is { } c
            ? $"No hay una plantilla {code} de {NombreDelCanal(c)} vigente. Créela en Cartera › Plantillas de mensaje."
            : $"La plantilla {code} no existe.",
        new { code, channel = canal?.ToString() });

    public static Error CreditoInexistente() => new(CodigoCreditoInexistente, "El crédito no existe.");

    public static Error CarteraSinActivar() => new(CodigoCarteraSinActivar,
        "Cartera todavía no está activada en esta cooperativa. Esta operación estará disponible cuando se registre la activación.");

    public static string NombreDelCanal(MessageChannel canal) => canal switch
    {
        MessageChannel.Email => "correo",
        MessageChannel.Sms => "SMS",
        MessageChannel.WhatsApp => "WhatsApp",
        MessageChannel.Letter => "carta",
        _ => canal.ToString(),
    };

    public static string NombreDelEstado(DebtorMessageEventKind estado) => estado switch
    {
        DebtorMessageEventKind.Queued => "en cola",
        DebtorMessageEventKind.Deferred => "diferido",
        DebtorMessageEventKind.Excluded => "excluido",
        DebtorMessageEventKind.Sent => "enviado",
        DebtorMessageEventKind.Delivered => "entregado",
        DebtorMessageEventKind.Failed => "fallido",
        DebtorMessageEventKind.Simulated => "simulado, no enviado",
        DebtorMessageEventKind.Cancelled => "cancelado",
        _ => estado.ToString(),
    };
}
