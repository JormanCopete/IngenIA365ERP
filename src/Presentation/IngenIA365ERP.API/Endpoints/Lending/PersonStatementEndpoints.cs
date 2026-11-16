using Carter;
using IngenIA365ERP.API.Endpoints.Reports;
using IngenIA365ERP.API.Filters;
using IngenIA365ERP.API.Reports;
using IngenIA365ERP.API.Reports.Exportadores;
using IngenIA365ERP.Application.Common.Interfaces;
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
/// los datos básicos de cada uno (<see cref="GetPersonAccountStatementQuery"/>); desde el 2026-11-16 con el membrete, el cupo y el resumen de
/// conceptos.</item>
/// <item><c>POST /api/lending/person-statement/send</c> (<c>Lending.Documents.Issue</c>): el mismo estado de cuenta en PDF, con las columnas que eligió
/// quien lo envía, al correo registrado de la persona —nunca a otro: el correo lo cambia sólo la persona—. Queda en la auditoría como exportación
/// de la vista <c>person-statement</c> con formato <c>email-pdf</c>. Desde el 2026-11-16 adjunta el PDF con membrete
/// (<see cref="EstadoDeCuentaDeLaPersonaReport"/>) y acepta <c>decimals</c>.</item>
/// <item>2026-11-16 (pedido del dueño): <c>GET /sections/{section}?personPublicId=&amp;date=</c>, una sección por persona como tabla
/// (<see cref="GetPersonStatementSectionQuery"/>; cada una con el permiso de su módulo, <see cref="SeccionesDelEstadoDeCuenta.Todas"/>);
/// <c>GET /pdf</c> y <c>GET /xlsx</c> con los filtros de la consulta más <c>columns</c> y <c>decimals</c>, con los permisos y la auditoría de exportar
/// la vista <c>person-statement</c>; y la simulación de cancelación a una fecha (<see cref="SimulatePersonPayoffQuery"/>): <c>POST /payoff-simulation</c>
/// y <c>/payoff-simulation/pdf</c> (<c>Lending.Payoffs.Read</c>) y <c>/payoff-simulation/send</c> (<c>Lending.Documents.Issue</c>, con clave).</item>
/// </list>
/// Exportar desde el centro de informes sigue por la vista <c>person-statement</c> (<c>/api/reports/lending/person-statement</c>). (nuevo)
/// </summary>
public class PersonStatementEndpoints : ICarterModule
{
    public const string Leer = "Lending.Loans.Read";
    public const string Enviar = "Lending.Documents.Issue";
    public const string Simular = "Lending.Payoffs.Read";

    /// <summary>La clave con que la simulación queda en la auditoría de exportaciones (no es una vista del centro de informes).</summary>
    public const string ClaveDeLaSimulacion = "person-payoff-simulation";

    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var estado = LendingGroup.Grupo(app, "/person-statement", "Cartera Estado de cuenta");

        estado.MapGet("/", async (Guid personPublicId, DateOnly? date, bool? includeClosed, ISender sender, CancellationToken ct) =>
                await sender.Send(new GetPersonAccountStatementQuery(personPublicId, date, includeClosed ?? false), ct))
            .WithName("Lending_PersonStatement_Get")
            .RequirePermission(Leer);

        // Una ruta por sección, cada una con el permiso de su módulo; la tabla sale como la del centro de informes en json (con el sobre si falla).
        foreach (var seccion in SeccionesDelEstadoDeCuenta.Todas)
        {
            var codigo = seccion.Codigo;
            estado.MapGet($"/sections/{codigo}", async (Guid personPublicId, DateOnly? date, HttpContext http, ISender sender, CancellationToken ct) =>
                    await EntregaDeInformes.EntregarAsync(await sender.Send(new GetPersonStatementSectionQuery(personPublicId, codigo, date), ct),
                        EntregaDeInformes.Json, codigo, http))
                .WithName($"Lending_PersonStatement_Section_{codigo}")
                .RequirePermission(seccion.Permiso);
        }

        // El PDF con membrete y el Excel: lo mismo que exportar la vista person-statement (ver, exportar, leer créditos y datos personales).
        estado.MapGet("/pdf", async (Guid personPublicId, DateOnly? date, string? columns, bool? includeClosed, bool? decimals, ISender sender,
                LendingAuditEmitter auditoria, IDateTimeService reloj, CancellationToken ct) =>
            {
                var leido = await sender.Send(new GetPersonAccountStatementQuery(personPublicId, date, includeClosed ?? false), ct);
                if (leido.IsFailure) return (object)leido;
                var e = leido.Value;
                var elegidas = Columnas(columns);
                var pdf = EstadoDeCuentaDeLaPersonaReport.Generate(e, elegidas, decimals ?? false, reloj.AhoraLocal);
                await auditoria.EmitirExportacionAsync(PersonStatementReportQueryHandler.Vista.Key,
                    new { person = personPublicId, date = e.Fecha, columns = elegidas, includeClosed = includeClosed ?? false, decimals = decimals ?? false },
                    "pdf", e.Productos.Count, ct);
                return Results.File(pdf, "application/pdf", $"{NombreDelArchivo(e)}.pdf");
            })
            .WithName("Lending_PersonStatement_Pdf")
            .RequirePermission(LendingReportsRoutes.PermisoDeVer)
            .RequirePermission(LendingReportsRoutes.PermisoDeExportar)
            .RequirePermission(Leer)
            .RequirePermission(LendingReportsRoutes.PermisoDeDatosPersonales);

        estado.MapGet("/xlsx", async (Guid personPublicId, DateOnly? date, string? columns, bool? includeClosed, ISender sender,
                LendingAuditEmitter auditoria, CancellationToken ct) =>
            {
                var leido = await sender.Send(new GetPersonAccountStatementQuery(personPublicId, date, includeClosed ?? false), ct);
                if (leido.IsFailure) return (object)leido;
                var e = leido.Value;
                var elegidas = Columnas(columns);
                // Las notas de la tabla ya llevan el resumen de conceptos y el cupo (TablaDelEstadoDeCuenta.NotasDelResumen).
                var tabla = TablaDelEstadoDeCuenta.Armar(e, elegidas);
                var archivo = ExportadorDeTablas.Exportar(tabla, "xlsx", NombreDelArchivo(e));
                await auditoria.EmitirExportacionAsync(PersonStatementReportQueryHandler.Vista.Key,
                    new { person = personPublicId, date = e.Fecha, columns = elegidas, includeClosed = includeClosed ?? false }, "xlsx", tabla.Filas.Count, ct);
                return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
            })
            .WithName("Lending_PersonStatement_Xlsx")
            .RequirePermission(LendingReportsRoutes.PermisoDeVer)
            .RequirePermission(LendingReportsRoutes.PermisoDeExportar)
            .RequirePermission(Leer)
            .RequirePermission(LendingReportsRoutes.PermisoDeDatosPersonales);

        estado.MapPost("/send", async (EnvioRequest body, ISender sender, IEmailSender correo, LendingAuditEmitter auditoria, IDateTimeService reloj,
                CancellationToken ct) =>
            {
                var leido = await sender.Send(new GetPersonAccountStatementQuery(body.PersonPublicId, body.Date, body.IncludeClosed ?? false), ct);
                if (leido.IsFailure) return (object)leido;
                var e = leido.Value;
                if (string.IsNullOrWhiteSpace(e.Persona.Correo))
                    return Result.Failure(new Error("Lending.PersonStatement.NoEmail",
                        $"{e.Persona.Nombre} no tiene correo registrado: regístrelo en su ficha antes de enviar el estado de cuenta."));

                // 2026-11-16: el PDF con membrete, tarjetas y resumen de conceptos (antes, el genérico de ExportadorDeTablas).
                var pdf = EstadoDeCuentaDeLaPersonaReport.Generate(e, body.Columns, body.Decimals ?? false, reloj.AhoraLocal);
                var cuerpo = $"<p>Hola, {System.Net.WebUtility.HtmlEncode(e.Persona.Nombre)}.</p>"
                             + $"<p>Adjuntamos su estado de cuenta al {e.Fecha:dd/MM/yyyy}.</p>"
                             + "<p>Si tiene alguna pregunta, comuníquese con la cooperativa.</p>";
                await correo.SendAsync(new EmailMessage(e.Persona.Correo!, $"Estado de cuenta al {e.Fecha:dd/MM/yyyy}", cuerpo,
                    Attachments: [new EmailAttachment($"{NombreDelArchivo(e)}.pdf", "application/pdf", pdf)]), ct);
                await auditoria.EmitirExportacionAsync(PersonStatementReportQueryHandler.Vista.Key,
                    new { person = body.PersonPublicId, date = e.Fecha, columns = body.Columns, decimals = body.Decimals ?? false, to = "titular" }, "email-pdf",
                    e.Productos.Count, ct);
                return Results.Ok(new { sentTo = Enmascarar(e.Persona.Correo!), products = e.Productos.Count });
            })
            .WithName("Lending_PersonStatement_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Enviar);

        // La simulación de cancelación a una fecha (no escribe: sin clave), su PDF y su envío al correo de la persona (con clave).
        estado.MapPost("/payoff-simulation", async (SimulacionRequest body, ISender sender, CancellationToken ct) =>
                await sender.Send(new SimulatePersonPayoffQuery(body.PersonPublicId, body.Loans ?? [], body.Date), ct))
            .WithName("Lending_PersonStatement_PayoffSimulation")
            .RequirePermission(Simular);

        estado.MapPost("/payoff-simulation/pdf", async (SimulacionRequest body, ISender sender, LendingAuditEmitter auditoria, IDateTimeService reloj,
                CancellationToken ct) =>
            {
                var leida = await sender.Send(new SimulatePersonPayoffQuery(body.PersonPublicId, body.Loans ?? [], body.Date), ct);
                if (leida.IsFailure) return (object)leida;
                var s = leida.Value;
                var pdf = EstadoDeCuentaDeLaPersonaReport.GenerateSimulacion(s, body.Decimals ?? false, reloj.AhoraLocal);
                await auditoria.EmitirExportacionAsync(ClaveDeLaSimulacion,
                    new { person = body.PersonPublicId, date = s.Fecha, loans = s.Creditos.Select(c => c.LoanPublicId) }, "pdf", s.Creditos.Count, ct);
                return Results.File(pdf, "application/pdf", $"{NombreDeLaSimulacion(s)}.pdf");
            })
            .WithName("Lending_PersonStatement_PayoffSimulation_Pdf")
            .RequirePermission(Simular);

        estado.MapPost("/payoff-simulation/send", async (SimulacionRequest body, ISender sender, IEmailSender correo, LendingAuditEmitter auditoria,
                IDateTimeService reloj, CancellationToken ct) =>
            {
                var leida = await sender.Send(new SimulatePersonPayoffQuery(body.PersonPublicId, body.Loans ?? [], body.Date), ct);
                if (leida.IsFailure) return (object)leida;
                var s = leida.Value;
                if (string.IsNullOrWhiteSpace(s.Persona.Correo))
                    return Result.Failure(new Error("Lending.PersonStatement.NoEmail",
                        $"{s.Persona.Nombre} no tiene correo registrado: regístrelo en su ficha antes de enviar la simulación."));

                var pdf = EstadoDeCuentaDeLaPersonaReport.GenerateSimulacion(s, body.Decimals ?? false, reloj.AhoraLocal);
                var cuerpo = $"<p>Hola, {System.Net.WebUtility.HtmlEncode(s.Persona.Nombre)}.</p>"
                             + $"<p>Adjuntamos la simulación de lo que costaría cancelar sus créditos el {s.Fecha:dd/MM/yyyy}. Es un cálculo a esa fecha, "
                             + "no una orden de pago.</p>"
                             + "<p>Si tiene alguna pregunta, comuníquese con la cooperativa.</p>";
                await correo.SendAsync(new EmailMessage(s.Persona.Correo!, $"Simulación de cancelación al {s.Fecha:dd/MM/yyyy}", cuerpo,
                    Attachments: [new EmailAttachment($"{NombreDeLaSimulacion(s)}.pdf", "application/pdf", pdf)]), ct);
                await auditoria.EmitirExportacionAsync(ClaveDeLaSimulacion,
                    new { person = body.PersonPublicId, date = s.Fecha, loans = s.Creditos.Select(c => c.LoanPublicId), to = "titular" }, "email-pdf",
                    s.Creditos.Count, ct);
                return Results.Ok(new { sentTo = Enmascarar(s.Persona.Correo!), products = s.Creditos.Count });
            })
            .WithName("Lending_PersonStatement_PayoffSimulation_Send")
            .ConClaveDeOperacion()
            .RequirePermission(Enviar);
    }

    /// <summary>Las columnas pedidas en <c>columns</c> (códigos separados por coma; vacío = las de por defecto).</summary>
    private static IReadOnlyList<string> Columnas(string? columns) =>
        (columns ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NombreDelArchivo(EstadoDeCuentaDeLaPersonaDto e) => $"estado-de-cuenta-{e.Persona.Documento}-{e.Fecha:yyyyMMdd}";

    private static string NombreDeLaSimulacion(SimulacionDeCancelacionDto s) => $"simulacion-de-cancelacion-{s.Persona.Documento}-{s.Fecha:yyyyMMdd}";

    /// <summary>El correo a medias para la respuesta (<c>ju***@dominio.com</c>): quien envía no necesita verlo entero.</summary>
    private static string Enmascarar(string correo)
    {
        var arroba = correo.IndexOf('@');
        if (arroba <= 0) return "***";
        var usuario = correo[..arroba];
        return (usuario.Length <= 2 ? usuario[..1] : usuario[..2]) + "***" + correo[arroba..];
    }

    /// <summary>
    /// El envío: la persona, la fecha, las columnas que se ven, si van los productos cerrados y (2026-11-16) si los valores van con centavos (por
    /// defecto, sin ellos).
    /// </summary>
    public sealed record EnvioRequest(Guid PersonPublicId, DateOnly? Date, IReadOnlyList<string>? Columns, bool? IncludeClosed, bool? Decimals = null);

    /// <summary>La simulación (2026-11-16): la persona, los créditos (de ella como deudora, hasta 50), la fecha de pago y, para el PDF, los centavos.</summary>
    public sealed record SimulacionRequest(Guid PersonPublicId, IReadOnlyList<Guid>? Loans, DateOnly Date, bool? Decimals = null);
}
