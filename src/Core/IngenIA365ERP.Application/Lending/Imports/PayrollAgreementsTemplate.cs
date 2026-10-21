using System.Globalization;
using FluentValidation;
using IngenIA365ERP.Application.Common.Audit;
using IngenIA365ERP.Application.Common.Behaviors;
using IngenIA365ERP.Application.Common.Imports;
using IngenIA365ERP.Application.Common.Interfaces;
using IngenIA365ERP.Application.Common.Models;
using IngenIA365ERP.Application.Lending.Common;
using IngenIA365ERP.Application.Lending.Payroll;
using IngenIA365ERP.Application.Lending.Payroll.Agreements;
using IngenIA365ERP.Domain.Entities.Lending.Payroll;
using IngenIA365ERP.Domain.Enums.Core;
using IngenIA365ERP.Domain.Enums.Lending;
using MediatR;
using Microsoft.EntityFrameworkCore;
using C = IngenIA365ERP.Application.Lending.Imports.PayrollAgreementsTemplate.Columnas;
using H = IngenIA365ERP.Application.Lending.Imports.PayrollAgreementsTemplate.Hojas;

namespace IngenIA365ERP.Application.Lending.Imports;

/// <summary>
/// Plantilla 8 — convenios de pagaduría (feature 013, K2, T0834; plantillas.md §8; FR-108): hojas <c>Convenios</c> (una versión por fila, llave
/// <c>codigo</c> + <c>vigenteDesde</c>), <c>Contactos</c> y <c>CodigosDeRespuesta</c> (llave convenio + vigente desde + código). Escribe los
/// convenios, sus versiones, contactos y códigos con las <b>mismas reglas que el alta unitaria</b> (<see cref="CreatePayrollAgreementCommandHandler"/>,
/// <see cref="AltaDeVersionDeConvenio"/>). Ninguna plantilla crea personas. (nuevo)
/// </summary>
public static class PayrollAgreementsTemplate
{
    public const string Clave = "lending.payroll-agreements";

    public static class Hojas
    {
        public const string Convenios = "Convenios";
        public const string Contactos = "Contactos";
        public const string CodigosDeRespuesta = "CodigosDeRespuesta";
    }

    public static class Columnas
    {
        public const string Codigo = "codigo";
        public const string VigenteDesde = "vigenteDesde";
        public const string VigenteHasta = "vigenteHasta";
        public const string Pagaduria = "pagaduria";
        public const string EsNominaPropia = "esNominaPropia";
        public const string Nombre = "nombre";
        public const string DiaDeCorte = "diaDeCorte";
        public const string DiaDeEnvio = "diaDeEnvio";
        public const string DiaDePago = "diaDePago";
        public const string AntelacionDias = "antelacionDias";
        public const string FormatoDeIda = "formatoDeIda";
        public const string FormatoDeVuelta = "formatoDeVuelta";
        public const string TopeDeDescuento = "topeDeDescuento";
        public const string IncluyeVencido = "incluyeVencido";
        public const string VentanaDeGiroDias = "ventanaDeGiroDias";
        public const string AccionNoAplicado = "accionNoAplicado";
        public const string FormaDePagoAlterna = "formaDePagoAlterna";
        public const string Notas = "notas";

        public const string Convenio = "convenio";
        public const string Persona = "persona";
        public const string Cargo = "cargo";
        public const string RecibeArchivo = "recibeArchivo";

        public const string Descripcion = "descripcion";
        public const string Descontado = "descontado";
        public const string Motivo = "motivo";
    }

    /// <summary>La definición de la plantilla (plantillas.md §8).</summary>
    public static DefinicionDePlantilla Definicion { get; } = new(Clave, "Convenios de pagaduría", ModuloDeAuditoria.Lending,
    [
        new HojaDePlantilla(H.Convenios,
        [
            new(C.Codigo, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave; la nomenclatura de la cooperativa", Ejemplo: "PAGMUN01"),
            new(C.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; yyyy-MM-dd; una versión nueva empieza después de la anterior", Ejemplo: "2027-01-01"),
            new(C.VigenteHasta, TipoDeValor.Fecha, Reglas: "vacía = hasta la versión siguiente", Ejemplo: ""),
            new(C.Pagaduria, TipoDeValor.Persona, Obligatoria: true, Largo: 20,
                Reglas: "NIT de la persona jurídica en Maestros › Personas, sin dígito de verificación (esta plantilla no crea personas); la nómina propia lleva el NIT de la cooperativa",
                Ejemplo: ""),
            new(C.EsNominaPropia, TipoDeValor.SiNo, Reglas: "vacío = no; la nómina propia no genera archivo y es una sola", Ejemplo: "no"),
            new(C.Nombre, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Secretaría de Educación Municipal"),
            new(C.DiaDeCorte, TipoDeValor.Entero, Obligatoria: true, Reglas: "1 a 31", Ejemplo: "20"),
            new(C.DiaDeEnvio, TipoDeValor.Entero, Reglas: "1 a 31; vacío = el de corte", Ejemplo: "25"),
            new(C.DiaDePago, TipoDeValor.Entero, Obligatoria: true, Reglas: "1 a 31", Ejemplo: "30"),
            new(C.AntelacionDias, TipoDeValor.Entero, Reglas: "días antes del envío en que se genera el archivo; vacío = 0", Ejemplo: "5"),
            new(C.FormatoDeIda, TipoDeValor.Codigo, Obligatoria: true, Largo: 40,
                Reglas: "código de un formato bancario genérico del ámbito PayrollDeductionRequest (Maestros › Formatos bancarios)", Ejemplo: "PAGMUN-IDA"),
            new(C.FormatoDeVuelta, TipoDeValor.Codigo, Obligatoria: true, Largo: 40,
                Reglas: "código de un formato bancario genérico del ámbito PayrollDeductionResult", Ejemplo: "PAGMUN-VUELTA"),
            new(C.TopeDeDescuento, TipoDeValor.Porcentaje, Reglas: "en puntos (40 = 40 %); nunca por encima del límite legal; vacío = sin tope propio", Ejemplo: "40"),
            new(C.IncluyeVencido, TipoDeValor.SiNo, Reglas: "vacío = no", Ejemplo: "no"),
            new(C.VentanaDeGiroDias, TipoDeValor.Entero, Reglas: "vacío = el parámetro Libranza.VentanaDeGiroDias; nunca por encima del límite legal", Ejemplo: ""),
            new(C.AccionNoAplicado, TipoDeValor.Enumeracion,
                Reglas: "IncludedInNextFile (incluir en el siguiente), MovedToCashPayment (pasar a caja) o SentToCollection (enviar a cobranza); vacío = MovedToCashPayment",
                Ejemplo: "MovedToCashPayment"),
            new(C.FormaDePagoAlterna, TipoDeValor.Enumeracion, Reglas: "Cashier (caja) o DirectDebit (débito automático); vacío = Cashier", Ejemplo: "Cashier"),
            new(C.Notas, TipoDeValor.Texto, Largo: 400, Reglas: "reglas de novedades del convenio; vacío = el motivo de la importación", Ejemplo: ""),
        ]),
        new HojaDePlantilla(H.Contactos,
        [
            new(C.Convenio, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "un convenio de la hoja Convenios o ya registrado", Ejemplo: "PAGMUN01"),
            new(C.Persona, TipoDeValor.Persona, Obligatoria: true, Largo: 20, Reglas: "documento de la persona del maestro; su correo y teléfono salen de allí", Ejemplo: ""),
            new(C.Cargo, TipoDeValor.Texto, Largo: 80, Reglas: "Payroll (nómina), Treasury (tesorería), Legal u Other; vacío = según recibeArchivo", Ejemplo: "Payroll"),
            new(C.RecibeArchivo, TipoDeValor.SiNo, Reglas: "vacío = no; quien recibe el archivo es el contacto principal", Ejemplo: "sí"),
        ], Obligatoria: false),
        new HojaDePlantilla(H.CodigosDeRespuesta,
        [
            new(C.Convenio, TipoDeValor.Codigo, Obligatoria: true, Largo: 10, Reglas: "llave", Ejemplo: "PAGMUN01"),
            new(C.VigenteDesde, TipoDeValor.Fecha, Obligatoria: true, Reglas: "llave; una versión del convenio", Ejemplo: "2027-01-01"),
            new(C.Codigo, TipoDeValor.Texto, Obligatoria: true, Largo: 10, Reglas: "llave; como lo escribe la pagaduría en su archivo de vuelta", Ejemplo: "07"),
            new(C.Descripcion, TipoDeValor.Texto, Obligatoria: true, Largo: 120, Ejemplo: "Licencia no remunerada"),
            new(C.Descontado, TipoDeValor.SiNo, Obligatoria: true, Reglas: "sí = descontado completo; no = no descontado", Ejemplo: "no"),
            new(C.Motivo, TipoDeValor.Enumeracion,
                Reglas: "con «no»: DeductionCap, InsufficientNet, Termination, Leave, Disability, DeductionPriority u Other", Ejemplo: "Leave"),
        ], Obligatoria: false),
    ]);
}

/// <summary>
/// Importa la plantilla <c>lending.payroll-agreements</c> (api.md §13.1, <c>POST /api/lending/payroll-agreements/import?mode=review|apply</c>,
/// <c>Lending.PayrollAgreements.Manage</c>, con clave). (nuevo)
/// </summary>
public sealed record ImportPayrollAgreementsCommand(ModoDeImportacion? Mode, ArchivoDeImportacion File, string Reason = "")
    : IRequest<Result<ImportResultDto>>, IComandoDeImportacion, IOperacionIdempotente
{
    public Guid OperationKey { get; init; }
}

public sealed class ImportPayrollAgreementsCommandValidator : AbstractValidator<ImportPayrollAgreementsCommand>
{
    public ImportPayrollAgreementsCommandValidator()
    {
        RuleFor(x => x.File).NotNull();
        RuleFor(x => x.Reason).MaximumLength(ValidadorConMotivo<ImportPayrollAgreementsCommand>.LargoMaximo);
    }
}

/// <summary>
/// Cada fila de <c>Convenios</c> es una versión: la primera de un código nuevo crea el convenio (pagaduría jurídica viva, la nómina propia única y de la
/// cooperativa, el registro de operador de libranza); una versión con la misma llave ya registrada queda «sin cambio»; las demás se registran en orden
/// de <c>vigenteDesde</c> con las reglas de <see cref="AltaDeVersionDeConvenio"/>. Los códigos de respuesta van con su versión (o se agregan a una ya
/// registrada) y los contactos son personas del maestro (<c>Import.Cell.NotFound</c> si no). Todo o nada; las vigencias piden motivo. (nuevo)
/// </summary>
public sealed class ImportPayrollAgreementsCommandHandler(
    IApplicationDbContext db,
    EjecutorDeImportacion ejecutor,
    ConveniosDeLibranza convenios,
    AltaDeVersionDeConvenio alta,
    IDateTimeService reloj) : IRequestHandler<ImportPayrollAgreementsCommand, Result<ImportResultDto>>
{
    private const string MotivoPorDefecto = "Importación de convenios de pagaduría";

    private sealed class VersionLeida(FilaDeImportacion fila, string codigo, DateOnly desde)
    {
        public FilaDeImportacion Fila { get; } = fila;
        public string Codigo { get; } = codigo;
        public DateOnly Desde { get; } = desde;
        public DateOnly? Hasta { get; set; }
        public PayrollAgreementVersionInput Entrada { get; set; } = null!;
        public (int Ida, int Vuelta) Formatos { get; set; }
        public List<PayrollResultCodeInput> Codigos { get; } = [];
        public string Llave => $"{Codigo}|{Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
    }

    private sealed record CodigoPendiente(FilaDeImportacion Fila, string Convenio, DateOnly Desde, PayrollResultCodeInput Entrada);

    private sealed record ContactoPendiente(FilaDeImportacion Fila, string Convenio, int Persona, string Papel, bool Principal);

    private sealed class Plan
    {
        public List<VersionLeida> Versiones { get; } = [];
        public List<CodigoPendiente> CodigosSueltos { get; } = [];
        public List<ContactoPendiente> Contactos { get; } = [];
    }

    public Task<Result<ImportResultDto>> Handle(ImportPayrollAgreementsCommand request, CancellationToken ct)
    {
        var plan = new Plan();
        return ejecutor.EjecutarAsync(PayrollAgreementsTemplate.Definicion, request, (ctx, c) => ProcesarAsync(ctx, plan, c), ct,
            (ctx, c) => AplicarAsync(ctx, plan, c));
    }

    // ---------------------------------------------------------------------------------------------------------- revisar --

    private async Task ProcesarAsync(ContextoDeImportacion ctx, Plan plan, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var hojaDeConvenios = ctx.Hoja(H.Convenios);
        var hojaDeContactos = ctx.Hoja(H.Contactos);
        var hojaDeCodigos = ctx.Hoja(H.CodigosDeRespuesta);

        // Las personas y los formatos citados, en bloque.
        var documentos = hojaDeConvenios.Filas.Select(f => f.Crudo(C.Pagaduria)).Concat(hojaDeContactos.Filas.Select(f => f.Crudo(C.Persona)))
            .OfType<string>().Select(d => d.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var personas = (await db.People.AsNoTracking().Where(p => documentos.Contains(p.TaxId) && !p.IsDeleted).ToListAsync(ct))
            .GroupBy(p => p.TaxId, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var codigosDeFormato = hojaDeConvenios.Filas.SelectMany(f => new[] { f.Crudo(C.FormatoDeIda), f.Crudo(C.FormatoDeVuelta) }).OfType<string>()
            .Select(c => c.Trim().ToUpperInvariant()).Distinct().ToList();
        var formatos = (await db.BankFileFormats.AsNoTracking().Where(f => codigosDeFormato.Contains(f.Code) && !f.IsDeleted)
                .Select(f => new { f.PublicId, f.Code, f.Scope }).ToListAsync(ct))
            .GroupBy(f => f.Code, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var deFormato = formatos.ToDictionary(kv => kv.Key, kv => (kv.Value.PublicId, kv.Value.Scope), StringComparer.OrdinalIgnoreCase);
        var leidas = new List<VersionLeida>();
        foreach (var fila in hojaDeConvenios.Filas)
        {
            var codigo = fila.Codigo(C.Codigo, 10);
            var desde = fila.Fecha(C.VigenteDesde);
            var hasta = fila.Fecha(C.VigenteHasta);
            fila.Texto(C.Nombre, 120);
            fila.Requerido(C.Nombre);
            var corte = fila.Entero(C.DiaDeCorte);
            var envio = fila.Entero(C.DiaDeEnvio);
            var pago = fila.Entero(C.DiaDePago);
            var antelacion = fila.Entero(C.AntelacionDias);
            var tope = fila.Porcentaje(C.TopeDeDescuento);
            var ventana = fila.Entero(C.VentanaDeGiroDias);
            var accion = fila.Enumeracion<NonDeductionFollowUpAction>(C.AccionNoAplicado);
            var alterna = fila.Enumeracion<RepaymentChannel>(C.FormaDePagoAlterna);
            var notas = fila.Texto(C.Notas, 400);
            fila.SiNo(C.EsNominaPropia);
            fila.SiNo(C.IncluyeVencido);
            var ida = Formato(fila, C.FormatoDeIda, deFormato, BankFileScope.PayrollDeductionRequest);
            var vuelta = Formato(fila, C.FormatoDeVuelta, deFormato, BankFileScope.PayrollDeductionResult);
            var llave = $"{codigo}|{desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            if (!hojaDeConvenios.LlaveUnica(fila, llave, C.Codigo) || fila.TieneErrores || codigo is null || desde is null || ida is null || vuelta is null)
                continue;
            if (hasta is { } h && h < desde)
            {
                fila.Error(C.VigenteHasta, ImportErrors.CellFormat, "La vigencia no puede terminar antes de empezar.");
                continue;
            }
            var entrada = new PayrollAgreementVersionInput(desde.Value, corte ?? 0, pago ?? 0, ida.Value, vuelta.Value,
                notas ?? ctx.Motivo ?? MotivoPorDefecto, envio, antelacion ?? 0, tope, fila.SiNo(C.IncluyeVencido), null, ventana, accion, alterna, null, null, notas);
            var forma = new PayrollAgreementVersionInputValidator().Validate(entrada);
            if (!forma.IsValid)
            {
                foreach (var e in forma.Errors) fila.Error(Columna(e.PropertyName), ImportErrors.CellFormat, e.ErrorMessage);
                continue;
            }
            leidas.Add(new VersionLeida(fila, codigo, desde.Value) { Hasta = hasta, Entrada = entrada });
        }

        // Los códigos de respuesta, con su versión.
        var porLlave = leidas.ToDictionary(v => v.Llave, StringComparer.Ordinal);
        foreach (var fila in hojaDeCodigos.Filas)
        {
            var convenio = fila.Codigo(C.Convenio, 10);
            var desde = fila.Fecha(C.VigenteDesde);
            var codigo = fila.Texto(C.Codigo, 10);
            var descripcion = fila.Texto(C.Descripcion, 120);
            fila.Requerido(C.Descripcion);
            fila.Requerido(C.Descontado);
            var descontado = fila.SiNo(C.Descontado);
            var motivo = fila.Enumeracion<NonDeductionReason>(C.Motivo);
            var llave = $"{convenio}|{desde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}|{codigo}";
            if (!hojaDeCodigos.LlaveUnica(fila, llave, C.Codigo) || fila.TieneErrores || convenio is null || desde is null || codigo is null || descripcion is null)
                continue;
            if (!descontado && motivo is null)
            {
                fila.Error(C.Motivo, ImportErrors.CellRequired, "Un código de no descuento lleva su motivo.");
                continue;
            }
            var entrada = new PayrollResultCodeInput(codigo, descripcion, descontado ? PayrollResultOutcome.Deducted : PayrollResultOutcome.NotDeducted,
                descontado ? null : motivo);
            var clave = $"{convenio}|{desde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            if (porLlave.TryGetValue(clave, out var suya)) suya.Codigos.Add(entrada);
            else plan.CodigosSueltos.Add(new CodigoPendiente(fila, convenio, desde.Value, entrada));
        }

        // Los convenios y sus versiones, en orden de vigencia.
        var codigos = leidas.Select(v => v.Codigo).Concat(plan.CodigosSueltos.Select(c => c.Convenio))
            .Concat(hojaDeContactos.Filas.Select(f => f.Crudo(C.Convenio)?.Trim().ToUpperInvariant()).OfType<string>()).Distinct().ToList();
        var existentes = await db.PayrollAgreements.AsNoTracking().Where(a => codigos.Contains(a.Code) && !a.IsDeleted).ToDictionaryAsync(a => a.Code, ct);
        var idsExistentes = existentes.Values.Select(a => a.Id).ToList();
        var registradas = (await db.PayrollAgreementVersions.AsNoTracking().Where(v => idsExistentes.Contains(v.PayrollAgreementId) && !v.IsDeleted).ToListAsync(ct))
            .ToLookup(v => v.PayrollAgreementId);
        var nuevos = new HashSet<string>(StringComparer.Ordinal);
        var registroVerificado = false;
        string? nominaPropiaEnElArchivo = null;
        foreach (var grupo in leidas.GroupBy(v => v.Codigo).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            DateOnly? anterior = null;
            foreach (var v in grupo.OrderBy(v => v.Desde))
            {
                if (existentes.TryGetValue(v.Codigo, out var convenio))
                {
                    var igual = registradas[convenio.Id].FirstOrDefault(r => r.ValidFrom == v.Desde);
                    if (igual is not null)
                    {
                        ctx.Registrar(v.Fila, v.Llave, AccionDeImportacion.Unchanged);
                        foreach (var c in v.Codigos) plan.CodigosSueltos.Add(new CodigoPendiente(v.Fila, v.Codigo, v.Desde, c));
                        v.Codigos.Clear();
                        continue;
                    }
                    if (convenio.Status == PayrollAgreementStatus.Closed)
                    {
                        v.Fila.Error(C.Codigo, ErroresDeLibranza.CodigoConvenioNoDisponible, $"El convenio {convenio.Code} está cerrado: no recibe versiones.");
                        continue;
                    }
                }
                else if (!nuevos.Contains(v.Codigo))
                {
                    // El primer registro de un código nuevo crea el convenio con su pagaduría.
                    var documento = v.Fila.Crudo(C.Pagaduria)!.Trim();
                    if (!personas.TryGetValue(documento, out var pagaduria))
                    {
                        v.Fila.Error(C.Pagaduria, ImportErrors.CellNotFound,
                            $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                        break;
                    }
                    if (!ConveniosDeLibranza.EsJuridica(pagaduria))
                    {
                        Error(v.Fila, C.Pagaduria, ErroresDeLibranza.PagadorNoJuridico(ReferenciasDeGobierno.Nombre(pagaduria)));
                        break;
                    }
                    if (!registroVerificado)
                    {
                        if (await convenios.RegistroDeOperadorAsync(hoy, ct) is { } sinRegistro)
                        {
                            Error(v.Fila, C.Codigo, sinRegistro);
                            break;
                        }
                        registroVerificado = true;
                    }
                    var nominaPropia = v.Fila.SiNo(C.EsNominaPropia);
                    if (nominaPropia)
                    {
                        var otra = (await convenios.NominaPropiaAsync(ct))?.Code ?? nominaPropiaEnElArchivo;
                        if (otra is not null)
                        {
                            Error(v.Fila, C.EsNominaPropia, ErroresDeLibranza.NominaPropiaRepetida(otra));
                            break;
                        }
                        if ((await convenios.PersonaDeLaCooperativaAsync(ct))?.Id != pagaduria.Id)
                        {
                            Error(v.Fila, C.Pagaduria, ErroresDeLibranza.NominaPropiaNoEsLaCooperativa());
                            break;
                        }
                    }
                    if (nominaPropia) nominaPropiaEnElArchivo = v.Codigo;
                    db.PayrollAgreements.Add(new PayrollAgreement
                    {
                        Code = v.Codigo, PaymasterPersonId = pagaduria.Id, IsOwnPayroll = nominaPropia, Description = v.Fila.Crudo(C.Nombre)!.Trim(),
                    });
                    nuevos.Add(v.Codigo);
                }
                if (anterior is { } a && v.Desde <= a)
                {
                    Error(v.Fila, C.VigenteDesde, ErroresDeLibranza.VersionCruzada(v.Desde, a));
                    continue;
                }
                var entrada = v.Entrada with { ResultCodes = v.Codigos };
                var verificada = await alta.VerificarAsync(existentes.TryGetValue(v.Codigo, out var de) ? de.Id : null, entrada, ct);
                if (verificada.IsFailure)
                {
                    Error(v.Fila, ColumnaDelError(verificada.Error.Code), verificada.Error);
                    continue;
                }
                v.Entrada = entrada;
                v.Formatos = verificada.Value;
                anterior = v.Desde;
                plan.Versiones.Add(v);
                ctx.PedirMotivo();
                ctx.Registrar(v.Fila, v.Llave, nuevos.Contains(v.Codigo) && plan.Versiones.Count(x => x.Codigo == v.Codigo) == 1
                    ? AccionDeImportacion.Create : AccionDeImportacion.Update,
                    [new(C.VigenteDesde, null, v.Desde.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))]);
            }
        }

        // Los códigos de respuesta de una versión ya registrada.
        foreach (var c in plan.CodigosSueltos.ToList())
        {
            if (!existentes.TryGetValue(c.Convenio, out var convenio) || registradas[convenio.Id].FirstOrDefault(r => r.ValidFrom == c.Desde) is not { } version)
            {
                if (!plan.Versiones.Any(v => v.Codigo == c.Convenio && v.Desde == c.Desde))
                    c.Fila.Error(C.VigenteDesde, ImportErrors.CellNotFound,
                        $"No hay una versión del convenio {c.Convenio} que empiece el {c.Desde:yyyy-MM-dd}: regístrela en la hoja Convenios.");
                plan.CodigosSueltos.Remove(c);
                continue;
            }
            var yaEsta = await db.PayrollAgreementResultCodes.AsNoTracking()
                .AnyAsync(r => r.PayrollAgreementVersionId == version.Id && r.PaymasterCode == c.Entrada.Code && !r.IsDeleted, ct);
            if (yaEsta)
            {
                if (c.Fila.Hoja.Nombre == H.CodigosDeRespuesta) ctx.Registrar(c.Fila, $"{c.Convenio}|{c.Desde:yyyy-MM-dd}|{c.Entrada.Code}", AccionDeImportacion.Unchanged);
                plan.CodigosSueltos.Remove(c);
                continue;
            }
            if (c.Fila.Hoja.Nombre == H.CodigosDeRespuesta)
                ctx.Registrar(c.Fila, $"{c.Convenio}|{c.Desde:yyyy-MM-dd}|{c.Entrada.Code}", AccionDeImportacion.Create);
        }

        // Los contactos: personas del maestro de un convenio registrado o de la hoja.
        foreach (var fila in hojaDeContactos.Filas)
        {
            var convenio = fila.Codigo(C.Convenio, 10);
            var documento = fila.Texto(C.Persona, 20);
            var cargo = fila.Texto(C.Cargo, 80);
            var recibe = fila.SiNo(C.RecibeArchivo);
            if (fila.TieneErrores || convenio is null || documento is null) continue;
            if (!existentes.ContainsKey(convenio) && !nuevos.Contains(convenio))
            {
                fila.Error(C.Convenio, ImportErrors.CellNotFound, $"No hay un convenio «{convenio}»: regístrelo en la hoja Convenios.");
                continue;
            }
            if (!personas.TryGetValue(documento, out var persona))
            {
                fila.Error(C.Persona, ImportErrors.CellNotFound,
                    $"No hay una persona viva con documento «{documento}». Créela en Maestros › Personas: esta plantilla no crea personas.");
                continue;
            }
            var papel = cargo is not null && PayrollAgreementContact.Papeles.Contains(cargo, StringComparer.OrdinalIgnoreCase)
                ? PayrollAgreementContact.Papeles.First(p => string.Equals(p, cargo, StringComparison.OrdinalIgnoreCase))
                : recibe ? "Payroll" : "Other";
            var llave = $"{convenio}|{documento}|{papel}";
            if (!hojaDeContactos.LlaveUnica(fila, llave, C.Persona)) continue;
            var vigente = existentes.TryGetValue(convenio, out var registrado) && await db.PayrollAgreementContacts.AsNoTracking()
                .AnyAsync(c => c.PayrollAgreementId == registrado.Id && c.PersonId == persona.Id && c.Role == papel && !c.IsDeleted
                               && c.ValidFrom <= hoy && (c.ValidTo == null || c.ValidTo >= hoy), ct);
            if (vigente)
            {
                ctx.Registrar(fila, llave, AccionDeImportacion.Unchanged);
                continue;
            }
            plan.Contactos.Add(new ContactoPendiente(fila, convenio, persona.Id, papel, recibe));
            ctx.Registrar(fila, llave, AccionDeImportacion.Create);
        }
    }

    // ---------------------------------------------------------------------------------------------------------- aplicar --

    private async Task AplicarAsync(ContextoDeImportacion ctx, Plan plan, CancellationToken ct)
    {
        var hoy = reloj.HoyLocal;
        var codigos = plan.Versiones.Select(v => v.Codigo).Concat(plan.CodigosSueltos.Select(c => c.Convenio)).Concat(plan.Contactos.Select(c => c.Convenio))
            .Distinct().ToList();
        var deCodigo = await db.PayrollAgreements.AsNoTracking().Where(a => codigos.Contains(a.Code) && !a.IsDeleted).ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        foreach (var v in plan.Versiones.OrderBy(v => v.Codigo, StringComparer.Ordinal).ThenBy(v => v.Desde))
        {
            var version = await alta.RegistrarAsync(deCodigo[v.Codigo], v.Entrada with { Reason = v.Entrada.Notes ?? ctx.Motivo ?? MotivoPorDefecto }, v.Formatos, ct);
            if (v.Hasta is { } hasta) version.ValidTo = hasta;
        }
        foreach (var c in plan.CodigosSueltos)
        {
            var version = await db.PayrollAgreementVersions.AsNoTracking()
                .FirstAsync(r => r.PayrollAgreementId == deCodigo[c.Convenio] && r.ValidFrom == c.Desde && !r.IsDeleted, ct);
            db.PayrollAgreementResultCodes.Add(new PayrollAgreementResultCode
            {
                PayrollAgreementVersionId = version.Id, PaymasterCode = c.Entrada.Code.Trim(), Description = c.Entrada.Description.Trim(), Outcome = c.Entrada.Outcome,
                NonDeductionReason = c.Entrada.Reason,
            });
        }
        foreach (var c in plan.Contactos)
            db.PayrollAgreementContacts.Add(new PayrollAgreementContact
            {
                PayrollAgreementId = deCodigo[c.Convenio], PersonId = c.Persona, Role = c.Papel, IsPrimary = c.Principal, ValidFrom = hoy,
            });
    }

    // ----------------------------------------------------------------------------------------------------- utilidades --

    private static Guid? Formato(FilaDeImportacion fila, string columna, IReadOnlyDictionary<string, (Guid PublicId, BankFileScope Scope)> formatos, BankFileScope ambito)
    {
        var codigo = fila.Codigo(columna, 40);
        fila.Requerido(columna);
        if (codigo is null) return null;
        if (!formatos.TryGetValue(codigo, out var f))
        {
            fila.Error(columna, ImportErrors.CellNotFound, $"No hay un formato bancario «{codigo}». Créelo en Maestros › Formatos bancarios.");
            return null;
        }
        if (f.Scope != ambito)
        {
            fila.Error(columna, "Core.BankFileFormat.ScopeMismatch", $"El formato {codigo} es del ámbito {f.Scope}; esta columna pide uno del ámbito {ambito}.");
            return null;
        }
        return f.PublicId;
    }

    private static void Error(FilaDeImportacion fila, string columna, Error e) => fila.Error(columna, e.Code, e.Message);

    /// <summary>La columna donde cae cada error del alta unitaria.</summary>
    private static string ColumnaDelError(string codigo) => codigo switch
    {
        ErroresDeLibranza.CodigoVersionCruzada or ErroresDeLibranza.CodigoVersionEnUso => C.VigenteDesde,
        "Core.BankFileFormat.ScopeMismatch" or "Core.BankFileFormat.NotFound" => C.FormatoDeIda,
        Common.ErroresDeParametrosDeCartera.CodigoMenosEstrictoQueLaLey => C.TopeDeDescuento,
        ErroresDeLibranza.CodigoCodigoDeRespuestaRepetido => C.Codigo,
        _ => C.Codigo,
    };

    /// <summary>La columna de un error de forma de la versión.</summary>
    private static string Columna(string propiedad) => propiedad switch
    {
        nameof(PayrollAgreementVersionInput.CutoffDay) => C.DiaDeCorte,
        nameof(PayrollAgreementVersionInput.PayDay) => C.DiaDePago,
        nameof(PayrollAgreementVersionInput.SendDay) => C.DiaDeEnvio,
        nameof(PayrollAgreementVersionInput.SendLeadDays) => C.AntelacionDias,
        nameof(PayrollAgreementVersionInput.DeductionCap) => C.TopeDeDescuento,
        nameof(PayrollAgreementVersionInput.TransferWindowDays) => C.VentanaDeGiroDias,
        nameof(PayrollAgreementVersionInput.NotAppliedAction) => C.AccionNoAplicado,
        nameof(PayrollAgreementVersionInput.AlternateRepaymentChannel) => C.FormaDePagoAlterna,
        _ => C.Notas,
    };
}
