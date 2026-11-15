using Carter;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Interfaces.Notifications;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.PersonStatement;
using MediatR;

namespace IngenIA365ERP.API.Endpoints.Lending;

/// <summary>
/// El estado de cuenta de una persona en Cartera (2026-11-13, pedido del dueño; pantalla <c>/cartera/estado-de-cuenta</c>):
/// <list type="bullet">
/// <item><c>GET /api/lending/person-statement?personPublicId=&amp;date=&amp;includeClosed=</c> (<c>Lending.Loans.Read</c>): sus créditos y cuentas con
/// los datos básicos de cada uno (<see cref="GetPersonAccountStatementQuery"/>).</item>
/// <item><c>POST /api/lending/person-statement/send</c> (<c>Lending.Documents.Issue</c>): el mismo estado de cuenta en PDF, con las columnas que eligió
/// quien lo envía, al correo registrado de la persona —nunca a otro: el correo lo cambia sólo la persona—. Queda en la auditoría como exportación
/// de la vista <c>person-statement</c> con formato <c>email-pdf</c>.</item>
/// </list>
/// Exportar e imprimir van por la vista <c>person-statement</c> del centro de informes (<c>/api/reports/lending/person-statement</c>). (nuevo)
/// </summary>
public class PersonStatementEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string Enviar = "Lending.Documents.Issue";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var estado = LendingGroup.Grupo(app, "/person-statement", "Cartera Estado de cuenta");

        estado.MapGet("/", async (Guid personPublicId, DateOnly? date, bool? includeClosed, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPersonAccountStatementQuery(personPublicId, date, includeClosed ?? false), ct))
            .WithName("Lending_PersonStatement_Get")
            .RequirePermission(Leer);

        estado.MapPost("/send", async (EnvioRequest body, ISender sender, IEmailSender correo, LendingAuditEmitter auditoria, CancellationToken ct) =>
            {
                var leido = await sender.Send(new GetPersonAccountStatementQuery(body.PersonPublicId, body.Date, body.IncludeClosed ?? false), ct);
                if (leido.IsFailure) return (object)leido;
                var e = leido.Value;
                if (string.IsNullOrWhiteSpace(e.Persona.Correo))
                    return Result.Failure(new Error("Lending.PersonStatement.NoEmail",
                        $"{e.Persona.Nombre} no tiene correo registrado: regístrelo en su ficha antes de enviar el estado de cuenta."));

                var tabla = TablaDelEstadoDeCuenta.Armar(e, body.Columns);
                var archivo = ExportadorDeTablas.Exportar(tabla, "pdf", $"estado-de-cuenta-{e.Persona.Documento}-{e.Fecha:yyyyMMdd}");
                var cuerpo = $"<p>Hola, {System.Net.WebUtility.HtmlEncode(e.Persona.Nombre)}.</p>"
                             + $"<p>Adjuntamos su estado de cuenta al {e.Fecha:dd/MM/yyyy}.</p>"
                             + "<p>Si tiene alguna pregunta, comuníquese con la cooperativa.</p>";
                await correo.SendAsync(new EmailMessage(e.Persona.Correo!, $"Estado de cuenta al {e.Fecha:dd/MM/yyyy}", cuerpo,
                    Attachments: [new EmailAttachment(archivo.NombreArchivo, archivo.TipoContenido, archivo.Contenido)]), ct);
                await auditoria.EmitirExportacionAsync(PersonStatementReportQueryHandler.Vista.Key,
                    new { person = body.PersonPublicId, date = e.Fecha, columns = body.Columns, to = "titular" }, "email-pdf", tabla.Filas.Count, ct);
                return Results.Ok(new { sentTo = Enmascarar(e.Persona.Correo!), products = e.Productos.Count });
            })
            .WithName("Lending_PersonStatement_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Enviar);
    }

    /// <summary>El correo a medias para la respuesta (<c>ju***@dominio.com</c>): quien envía no necesita verlo entero.</summary>
    private static string Enmascarar(string correo)
    {
        var arroba = correo.IndexOf('@');
        if (arroba <= 0) return "***";
        var usuario = correo[..arroba];
        return (usuario.Length <= 2 ? usuario[..1] : usuario[..2]) + "***" + correo[arroba..];
    }

    /// <summary>El envío: la persona, la fecha, las columnas que se ven y si van los productos cerrados.</summary>
    public sealed record EnvioRequest(Guid PersonPublicId, DateOnly? Date, IReadOnlyList<string>? Columns, bool? IncludeClosed);
}
